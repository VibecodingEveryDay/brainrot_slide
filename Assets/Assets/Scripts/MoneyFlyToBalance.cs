using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Спавнит спрайты денег в центре экрана (на Canvas) и анимирует их полётом
/// по дуге к цели (например, иконка/текст баланса). Когда каждый спрайт
/// достигает цели, вызывается пульсация у BalanceCountUI.
/// </summary>
public class MoneyFlyToBalance : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Основной Canvas, в котором будут создаваться UI-спрайты денег")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("Префаб UI-спрайта денег (Image внутри Canvas)")]
    [SerializeField] private Image moneyImagePrefab;

    [Tooltip("Цель полёта — RectTransform иконки/текста баланса")]
    [SerializeField] private RectTransform balanceTarget;

    [Tooltip("Компонент, отвечающий за отображение баланса (для пульсации)")]
    [SerializeField] private BalanceCountUI balanceCountUI;

    [Header("Spawn Settings")]
    [Tooltip("Количество спрайтов, которые будут созданы за один запуск")]
    [SerializeField] private int moneyCount = 8;

    [Tooltip("Задержка между спавном отдельных спрайтов (секунды)")]
    [SerializeField] private float spawnDelay = 0.05f;

    [Tooltip("Максимальное случайное смещение точки старта от центра экрана (в пикселях)")]
    [SerializeField] private float startPositionJitter = 40f;

    [Header("Path Settings")]
    [Tooltip("Общая длительность полёта одного спрайта (секунды)")]
    [SerializeField] private float flightDuration = 0.7f;

    [Tooltip("Максимальное отклонение дуги от прямой (в пикселях)")]
    [SerializeField] private float pathOffsetRadius = 150f;

    [Tooltip("Вертикальный коэффициент растяжения овала траектории (1 = круг, >1 = более вытянутый по вертикали)")]
    [SerializeField] private float verticalOvalScale = 1.2f;

    [Header("Scale & Fade")]
    [Tooltip("Минимальный случайный масштаб спрайта")]
    [SerializeField] private float minRandomScale = 0.6f;
    
    [Tooltip("Максимальный случайный масштаб спрайта")]
    [SerializeField] private float maxRandomScale = 1.0f;
    
    [Tooltip("Множитель масштаба по пути (начальный/конечный относительный скейл)")]
    [SerializeField] private float startScaleMultiplier = 0.9f;
    
    [Tooltip("Множитель масштаба по пути у цели")]
    [SerializeField] private float endScaleMultiplier = 1.1f;

    [Tooltip("Затухание альфы к концу полёта")]
    [SerializeField] private bool fadeOnFlightEnd = true;

    [Tooltip("Минимальная видимая альфа в конце полёта (0–1)")]
    [SerializeField] [Range(0f, 1f)] private float endAlpha = 0.6f;

    [Header("Other")]
    [Tooltip("Использовать независимую от Time.timeScale анимацию (через unscaledDeltaTime)")]
    [SerializeField] private bool useUnscaledTime = true;

    [Tooltip("Степень ускорения по мере приближения к цели (1 = равномерно, >1 = ускорение)")]
    [SerializeField] private float flightAccelerationPower = 2.0f;

    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;

    private Camera _uiCamera;

    private void Awake()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }

        if (targetCanvas != null)
        {
            _uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;
        }

        if (balanceCountUI == null && balanceTarget != null)
        {
            balanceCountUI = balanceTarget.GetComponentInParent<BalanceCountUI>();
        }
    }

    /// <summary>
    /// Публичный метод для запуска анимации. Можно вызывать из EarnPanel.
    /// Использует настройки по умолчанию из инспектора.
    /// </summary>
    public void Play()
    {
        if (!gameObject.activeInHierarchy) return;
        StartCoroutine(PlayRoutine(moneyCount));
    }

    /// <summary>
    /// Запуск анимации с указанием количества спрайтов в коде.
    /// </summary>
    public void Play(int customCount)
    {
        if (!gameObject.activeInHierarchy) return;
        int count = Mathf.Max(0, customCount);
        StartCoroutine(PlayRoutine(count));
    }

    private IEnumerator PlayRoutine(int count)
    {
        if (targetCanvas == null || moneyImagePrefab == null || balanceTarget == null)
        {
            if (debug)
            {
                Debug.LogWarning("[MoneyFlyToBalance] Не хватает ссылок: Canvas / moneyImagePrefab / balanceTarget.");
            }
            yield break;
        }

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null) yield break;

        for (int i = 0; i < count; i++)
        {
            SpawnOneMoney(canvasRect);

            if (spawnDelay > 0f && i < count - 1)
            {
                if (useUnscaledTime)
                    yield return new WaitForSecondsRealtime(spawnDelay);
                else
                    yield return new WaitForSeconds(spawnDelay);
            }
        }
    }

    private void SpawnOneMoney(RectTransform canvasRect)
    {
        Image img = Instantiate(moneyImagePrefab, canvasRect);
        RectTransform rt = img.rectTransform;

        // Гарантируем, что якоря и pivot по центру, чтобы (0,0) было геометрическим центром Canvas
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // Случайное смещение старта вокруг центра
        Vector2 startOffset = Random.insideUnitCircle * startPositionJitter;
        Vector2 startPos = startOffset; // относительно центра экрана (0,0)

        // Цель в координатах Canvas
        Vector2 targetPos = WorldToCanvasPosition(balanceTarget.position, canvasRect);

        // Строим эллиптическую дугу с помощью одной контрольной точки (Bezier)
        Vector2 direction = (targetPos - startPos);
        float distance = direction.magnitude;
        if (distance <= 0.01f) distance = 0.01f;
        Vector2 dirNormalized = direction / distance;

        // Перпендикуляр к направлению
        Vector2 perpendicular = new Vector2(-dirNormalized.y, dirNormalized.x);
        float side = Random.value < 0.5f ? -1f : 1f;

        float offsetMagnitude = Random.Range(pathOffsetRadius * 0.4f, pathOffsetRadius);
        Vector2 controlOffset = perpendicular * offsetMagnitude * side;
        controlOffset.y *= verticalOvalScale;

        Vector2 controlPoint = Vector2.Lerp(startPos, targetPos, 0.5f) + controlOffset;

        // Случайный базовый масштаб для этого экземпляра
        float baseScale = Random.Range(minRandomScale, maxRandomScale);
        if (baseScale < 0.01f) baseScale = 0.01f;

        // Инициализируем визуальные параметры
        rt.anchoredPosition = startPos;
        rt.localScale = Vector3.one * (baseScale * startScaleMultiplier);

        Color baseColor = img.color;
        float startAlpha = baseColor.a;

        StartCoroutine(FlyCoroutine(rt, img, startPos, controlPoint, targetPos, startAlpha, baseScale));
    }

    private IEnumerator FlyCoroutine(RectTransform rt, Image img, Vector2 p0, Vector2 p1, Vector2 p2, float startAlpha, float baseScale)
    {
        float time = 0f;
        float duration = Mathf.Max(0.01f, flightDuration);

        while (time < duration)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            time += dt;

            float t = Mathf.Clamp01(time / duration);
            // Чем больше flightAccelerationPower, тем сильнее ускорение к концу.
            float s = Mathf.Pow(t, Mathf.Max(1f, flightAccelerationPower));

            // Квадратичная Bezier-кривая
            Vector2 pos = Mathf.Pow(1f - s, 2) * p0 +
                          2f * (1f - s) * s * p1 +
                          Mathf.Pow(s, 2) * p2;

            rt.anchoredPosition = pos;

            // Масштаб: вдоль пути слегка увеличиваем
            float scaleMul = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, s);
            rt.localScale = Vector3.one * (baseScale * scaleMul);

            // Плавное затухание
            if (fadeOnFlightEnd)
            {
                float a = Mathf.Lerp(startAlpha, endAlpha, s);
                Color c = img.color;
                c.a = a;
                img.color = c;
            }

            yield return null;
        }

        // По достижении цели — гарантируем финальное положение, уничтожаем спрайт и пульсируем баланс
        rt.anchoredPosition = p2;

        if (balanceCountUI != null)
        {
            balanceCountUI.PlayPulseOnce();
        }

        Destroy(rt.gameObject);
    }

    private Vector2 WorldToCanvasPosition(Vector3 worldPos, RectTransform canvasRect)
    {
        if (targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // В этом режиме координаты экрана = координатам Canvas
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, worldPos);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out localPoint);
            return canvasRect.rect.center + localPoint;
        }
        else
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(_uiCamera, worldPos);
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, _uiCamera, out localPoint);
            return canvasRect.rect.center + localPoint;
        }
    }
}

