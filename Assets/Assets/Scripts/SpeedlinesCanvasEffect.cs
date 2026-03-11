using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Спидлайны на Canvas: линии из спрайта появляются на внутреннем овале 16:9 и движутся к внешнему овалу (за край канваса).
/// Повесить на объект с RectTransform под Canvas. Назначить спрайт линии.
/// </summary>
public class SpeedlinesCanvasEffect : MonoBehaviour
{
    [Header("Спрайт и канвас")]
    [Tooltip("Спрайт линии (полоска).")]
    [SerializeField] private Sprite lineSprite;
    [Tooltip("Канвас или родитель с RectTransform для размера области. Если не задан — используется RectTransform этого объекта.")]
    [SerializeField] private RectTransform canvasOrRoot;

    [Header("Овал 16:9")]
    [Tooltip("Соотношение сторон овала: ширина к высоте (например 16 и 9).")]
    [SerializeField] private float ovalAspectWidth = 16f;
    [SerializeField] private float ovalAspectHeight = 9f;
    [Tooltip("Внутренний «радиус» (0–1): линия спавнится на этой эллипсе. 0 = в центре, 0.5 = половина экрана по длинной оси.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float innerRadiusNormalized = 0.02f;
    [Tooltip("Внешний «радиус» (0–1+): линия уходит за край. >0.5 = за пределы видимой области.")]
    [Range(0.1f, 1.2f)]
    [SerializeField] private float outerRadiusNormalized = 0.65f;

    [Header("Спавн и движение")]
    [SerializeField] private float spawnInterval = 0.05f;
    [SerializeField] private float lifetime = 0.4f;
    [Tooltip("Максимум линий в пуле.")]
    [SerializeField] private int maxLines = 50;

    [Header("Размер и вид линии")]
    [Tooltip("Длина линии в пикселях (по длинной стороне спрайта).")]
    [SerializeField] private float lineLengthPixels = 120f;
    [Tooltip("Ширина линии в пикселях.")]
    [SerializeField] private float lineWidthPixels = 6f;
    [Tooltip("Случайный разброс длины (0 = без разброса).")]
    [Range(0f, 1f)]
    [SerializeField] private float lengthRandom = 0.2f;
    [Tooltip("Случайный разброс ширины (0 = без разброса).")]
    [Range(0f, 1f)]
    [SerializeField] private float widthRandom = 0.2f;

    [Header("Цвет и прозрачность")]
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("Минимальная альфа в конце жизни (чтобы линия не исчезала полностью).")]
    [Range(0f, 1f)]
    [SerializeField] private float minAlpha = 0.15f;
    [Tooltip("0 = не спавнить, 1 = полная интенсивность.")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 1f;
    [Tooltip("Включить эффект только когда игрок в режиме slide (SlideManager.IsSlideActive).")]
    [SerializeField] private bool onlyDuringSlide = true;

    [Header("Дополнительно")]
    [Tooltip("Случайный разброс угла (градусы), 0 = строго по радиусу.")]
    [Range(0f, 15f)]
    [SerializeField] private float angleSpreadDegrees = 0f;

    private RectTransform _container;
    private RectTransform _rootRect;
    private readonly List<LineItem> _pool = new List<LineItem>();
    private float _spawnAccum;

    private class LineItem
    {
        public RectTransform rectTransform;
        public Image image;
        public Vector2 startPos;
        public Vector2 endPos;
        public float startTime;
        public float length;
        public float width;
        public bool inUse;
    }

    private void Awake()
    {
        if (canvasOrRoot == null)
            canvasOrRoot = GetComponent<RectTransform>();
        if (canvasOrRoot == null) return;
        _rootRect = canvasOrRoot;
    }

    private void Start()
    {
        if (_rootRect == null || lineSprite == null) return;

        GameObject containerGo = new GameObject("SpeedlinesCanvasContainer");
        _container = containerGo.AddComponent<RectTransform>();
        _container.SetParent(_rootRect, false);
        _container.anchorMin = Vector2.zero;
        _container.anchorMax = Vector2.one;
        _container.offsetMin = Vector2.zero;
        _container.offsetMax = Vector2.zero;
        _container.pivot = new Vector2(0.5f, 0.5f);
        _container.anchoredPosition = Vector2.zero;
        _container.localScale = Vector3.one;

        for (int i = 0; i < maxLines; i++)
        {
            var item = CreateLineItem();
            item.rectTransform.SetParent(_container, false);
            item.rectTransform.gameObject.SetActive(false);
            _pool.Add(item);
        }
    }

    private LineItem CreateLineItem()
    {
        var go = new GameObject("Line");
        var rt = go.AddComponent<RectTransform>();
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        // Спрайт: малая ширина, большая высота (треугольник — остриё вверх). X = толщина, Y = длина.
        rt.sizeDelta = new Vector2(lineWidthPixels, lineLengthPixels);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;

        var image = go.AddComponent<Image>();
        image.sprite = lineSprite;
        image.color = color;
        image.raycastTarget = false;
        image.preserveAspect = false;

        return new LineItem
        {
            rectTransform = rt,
            image = image,
            length = lineLengthPixels,
            width = lineWidthPixels
        };
    }

    private void Update()
    {
        if (_container == null || _rootRect == null) return;

        float effectiveIntensity = onlyDuringSlide
            ? (SlideManager.Instance != null && SlideManager.Instance.IsSlideActive() ? intensity : 0f)
            : intensity;

        float dt = Time.deltaTime;

        for (int i = 0; i < _pool.Count; i++)
        {
            var item = _pool[i];
            if (!item.inUse) continue;

            float t = (Time.time - item.startTime) / lifetime;
            if (t >= 1f)
            {
                item.inUse = false;
                item.rectTransform.gameObject.SetActive(false);
                continue;
            }

            Vector2 pos = Vector2.Lerp(item.startPos, item.endPos, t);
            item.rectTransform.anchoredPosition = pos;

            float alpha = Mathf.Lerp(color.a, minAlpha, t) * effectiveIntensity;
            item.image.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (effectiveIntensity <= 0f || spawnInterval <= 0f) return;

        _spawnAccum += dt;
        while (_spawnAccum >= spawnInterval)
        {
            _spawnAccum -= spawnInterval;
            TrySpawnLine();
        }
    }

    private void TrySpawnLine()
    {
        float w = _rootRect.rect.width;
        float h = _rootRect.rect.height;
        float halfW = w * 0.5f;
        float halfH = h * 0.5f;

        float aspectX = ovalAspectWidth;
        float aspectY = ovalAspectHeight;
        if (aspectX < 0.001f) aspectX = 16f;
        if (aspectY < 0.001f) aspectY = 9f;
        float scaleX = 1f;
        float scaleY = aspectY / aspectX;
        float maxAxis = Mathf.Max(halfW, halfH);
        float innerX = innerRadiusNormalized * maxAxis * scaleX;
        float innerY = innerRadiusNormalized * maxAxis * scaleY;
        float outerX = outerRadiusNormalized * maxAxis * scaleX;
        float outerY = outerRadiusNormalized * maxAxis * scaleY;

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        if (angleSpreadDegrees > 0f)
            angle += Random.Range(-angleSpreadDegrees, angleSpreadDegrees) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        Vector2 startPos = new Vector2(innerX * cos, innerY * sin);
        Vector2 endPos = new Vector2(outerX * cos, outerY * sin);

        // Остриё спрайта (колючка) смотрит к центру — поворот на 180° от направления движения.
        float rotZ = angle * Mathf.Rad2Deg - 90f + 180f;

        for (int i = 0; i < _pool.Count; i++)
        {
            var item = _pool[i];
            if (item.inUse) continue;

            float len = lineLengthPixels * (1f + Random.Range(-lengthRandom, lengthRandom));
            float wid = lineWidthPixels * (1f + Random.Range(-widthRandom, widthRandom));
            item.length = len;
            item.width = wid;
            item.startPos = startPos;
            item.endPos = endPos;
            item.startTime = Time.time;
            item.inUse = true;

            item.rectTransform.sizeDelta = new Vector2(wid, len);
            item.rectTransform.anchoredPosition = startPos;
            item.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            item.image.color = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.5f) * intensity);
            item.rectTransform.gameObject.SetActive(true);
            return;
        }
    }

    private void OnDestroy()
    {
        if (_container != null && _container.gameObject != null)
            Destroy(_container.gameObject);
    }
}
