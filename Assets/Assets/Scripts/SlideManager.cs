using UnityEngine;

/// <summary>
/// Менеджер режима скольжения. Singleton: хранит slideSpeed и ссылку на кнопку остановки.
/// Кнопка скрыта при старте, показывается при входе в slide.
/// На кнопку повесь StopSlideButtonHook — тогда OnClick настраивать не нужно; иначе в OnClick вызови StopSlide или StopSlideStatic.
/// </summary>
public class SlideManager : MonoBehaviour
{
    public static SlideManager Instance { get; private set; }
    
    [Header("Кнопка и скорость")]
    [Tooltip("Кнопка остановки скольжения (скрыта при старте, показывается при входе в slide). В OnClick вызови SlideManager.StopSlide.")]
    [SerializeField] private GameObject stopSlideButton;
    
    [Tooltip("Скорость скольжения по платформе.")]
    [SerializeField] private float slideSpeed = 10f;
    
    [Tooltip("Смещение игрока по Y в режиме slide (например, опустить ниже или поднять выше).")]
    [SerializeField] private float playerSlideOffsetY = 0f;
    [Tooltip("Скорость плавного поворота модели влево/вправо при A/D в slide (градусов в секунду).")]
    [SerializeField] private float playerRotationSpeed = 120f;
    
    [Header("VFX во время slide")]
    [Tooltip("VFX эффект (дым и т.п.): объект в сцене или префаб из проекта. Если префаб — создаётся экземпляр при старте.")]
    [SerializeField] private Transform slideVfx;
    [Tooltip("Масштаб VFX.")]
    [SerializeField] private Vector3 vfxScale = Vector3.one;
    [Tooltip("Поворот VFX (углы Эйлера).")]
    [SerializeField] private Vector3 vfxRotation = Vector3.zero;
    [Tooltip("Смещение VFX относительно игрока в локальных осях склона: X — вправо, Y — вверх, Z — назад (отрицательный Z = позади игрока).")]
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0f, -2f);
    [Tooltip("Второй VFX: объект/префаб или оставь пустым — тогда создаётся второй экземпляр из первого префаба (позиция по Vfx 2 Offset).")]
    [SerializeField] private Transform slideVfx2;
    [Tooltip("Смещение второго VFX относительно игрока (в локальных осях склона).")]
    [SerializeField] private Vector3 vfx2Offset = new Vector3(0f, 0f, -4f);
    
    [Header("Debug")]
    [Tooltip("Писать в консоль при нажатии кнопки Stop и на каждом шаге (игрок, контроллер, TeleportManager, телепорт).")]
    [SerializeField] private bool debug;
    
    [Header("Плавная подгонка по Y")]
    [Tooltip("Время (сек), за которое Y игрока подгоняется к траектории slide. 0 = без сглаживания (мгновенный телепорт).")]
    [SerializeField] private float slideYOffsetLerpTime = 0.2f;

    [Header("Slide Speed Decay")]
    [Tooltip("Минимальное время (в секундах), к которому нормируется затухание скорости slide. Чем больше значение, тем дольше скорость остаётся высокой.")]
    [SerializeField] private float minSecondsLimit = 5f;
    [Tooltip("Сколько секунд добавлять к MinSecondsLimit за каждый уровень скорости игрока (по данным GameStorage/ShopSpeedManager). Может быть отрицательным, если с ростом уровня нужно сокращать лимит.")]
    [SerializeField] private float addSecondsByLevel = 0f;

    [Header("Camera restriction during slide")]
    [Tooltip("Когда камера сзади игрока (Z камеры > Z игрока), камера не может опуститься по Y ниже чем Y игрока + этот offset. Чтобы камера не уходила под наклонную плоскость.")]
    [SerializeField] private float cameraMinYOffsetWhenBehindPlayer = 0.5f;

    /// <summary> Плоскость скольжения (forward = направление, right = стрейф). Null = по умолчанию вперёд по миру. </summary>
    private Transform currentSlidePlane;
    private float currentSlideTiltAngleX;
    
    // Геометрия плоскости скольжения в мире (для проекции игрока при входе в slide).
    private Plane slideWorldPlane;
    private bool hasSlideWorldPlane;
    private GameObject cachedPlayer;
    private ThirdPersonController cachedPlayerController;
    private Transform runtimeVfx1;
    private Transform runtimeVfx2;

    // Активная зона slide: становится true при первом входе в slide и сбрасывается при телепорте.
    private bool isInActiveZone;

    // Текущая скорость slide и время нахождения в режиме скольжения.
    private float currentSlideSpeed;
    private float slideElapsedTime;
    private float effectiveMinSeconds = 1f;
    private int lastSpeedLevel = -1;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureVfxInstances();
    }
    
    private void EnsureVfxInstances()
    {
        Transform sourceForSecond = null; // префаб/объект, из которого создать второй экземпляр, если slideVfx2 не задан
        if (slideVfx != null)
        {
            if (!slideVfx.gameObject.scene.IsValid())
            {
                GameObject go = Instantiate(slideVfx.gameObject, transform);
                go.name = slideVfx.name + "(Clone)";
                runtimeVfx1 = go.transform;
                runtimeVfx1.gameObject.SetActive(false);
                sourceForSecond = slideVfx; // префаб для второго экземпляра
            }
            else
                runtimeVfx1 = slideVfx;
        }
        if (slideVfx2 != null)
        {
            if (!slideVfx2.gameObject.scene.IsValid())
            {
                GameObject go = Instantiate(slideVfx2.gameObject, transform);
                go.name = slideVfx2.name + "(Clone)";
                runtimeVfx2 = go.transform;
                runtimeVfx2.gameObject.SetActive(false);
            }
            else
                runtimeVfx2 = slideVfx2;
        }
        else if (sourceForSecond != null)
        {
            // Второй слот не задан — создаём второй экземпляр из того же префаба, что и первый (другая позиция по vfx2Offset)
            GameObject go = Instantiate(sourceForSecond.gameObject, transform);
            go.name = sourceForSecond.name + "(Clone) 2";
            runtimeVfx2 = go.transform;
            runtimeVfx2.gameObject.SetActive(false);
        }
    }
    
    private void Start()
    {
        isInActiveZone = false;
        RecalculateEffectiveMinSeconds(true);
        if (stopSlideButton != null)
            stopSlideButton.SetActive(false);
        if (runtimeVfx1 != null)
            runtimeVfx1.gameObject.SetActive(false);
        if (runtimeVfx2 != null)
            runtimeVfx2.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Применяет масштаб VFX так, чтобы мировой размер был равен scale (не зависит от scale родителя).
    /// </summary>
    private static void ApplyVfxScale(Transform vfxTransform, Vector3 scale)
    {
        if (vfxTransform == null) return;
        Transform parent = vfxTransform.parent;
        if (parent != null)
        {
            Vector3 p = parent.lossyScale;
            vfxTransform.localScale = new Vector3(
                Mathf.Approximately(p.x, 0f) ? scale.x : scale.x / p.x,
                Mathf.Approximately(p.y, 0f) ? scale.y : scale.y / p.y,
                Mathf.Approximately(p.z, 0f) ? scale.z : scale.z / p.z
            );
        }
        else
        {
            vfxTransform.localScale = scale;
        }
    }
    
    private void Update()
    {
        // При изменении уровня скорости игрока обновляем конечный лимит секунд,
        // не сбрасывая накопленное время и текущую скорость.
        var storage = GameStorage.Instance;
        if (storage != null)
        {
            int level = storage.GetPlayerSpeedLevel();
            if (level != lastSpeedLevel)
            {
                RecalculateEffectiveMinSeconds(false);
            }
        }

        // Обновляем затухание скорости только пока активен slide.
        if (currentSlidePlane != null)
        {
            slideElapsedTime += Time.deltaTime;

            // Каждую секунду уменьшаем скорость на долю, равную 1 / effectiveMinSeconds (дискретно).
            float factor = 1f - (Time.deltaTime / effectiveMinSeconds);
            factor = Mathf.Clamp(factor, 0f, 1f);
            currentSlideSpeed *= factor;

            // Минимальный порог — абсолютное значение 0.1 единиц скорости.
            // Ниже этого значения скорость не опускаем.
            const float minSpeed = 0.1f;
            if (currentSlideSpeed < minSpeed)
                currentSlideSpeed = minSpeed;
        }
    }

    private void LateUpdate()
    {
        if (currentSlidePlane == null) return;
        if (runtimeVfx1 == null && runtimeVfx2 == null) return;
        
        if (cachedPlayer == null)
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (cachedPlayer == null) return;
        
        Transform player = cachedPlayer.transform;
        Transform slideT = currentSlidePlane;
        Quaternion rot = slideT.rotation * Quaternion.Euler(vfxRotation);
        
        if (runtimeVfx1 != null)
        {
            runtimeVfx1.position = player.position + slideT.TransformDirection(vfxOffset);
            runtimeVfx1.rotation = rot;
            ApplyVfxScale(runtimeVfx1, vfxScale);
        }
        if (runtimeVfx2 != null)
        {
            runtimeVfx2.position = player.position + slideT.TransformDirection(vfx2Offset);
            runtimeVfx2.rotation = rot;
            ApplyVfxScale(runtimeVfx2, vfxScale);
        }
    }
    
    /// <summary>
    /// Включить режим slide. Вызывается из SlideTriggerZone при входе в триггер.
    /// </summary>
    /// <param name="slidePlane">Transform плоскости скольжения (forward = направление). Может быть null — тогда используется мир по умолчанию.</param>
    /// <param name="tiltAngleX">Угол наклона модели персонажа по X (градусы).</param>
    public void EnterSlide(Transform slidePlane, float tiltAngleX)
    {
        // Если игрок уже проиграл и ждёт телепортацию (например, после удара охранника),
        // не показываем UI и не включаем визуальные эффекты slide.
        TeleportManager tm = TeleportManager.Instance;
        if (tm != null && tm.IsTeleportingDueToLose())
        {
            if (debug)
                Debug.Log("[SlideManager] EnterSlide: отменён, т.к. идёт телепортация из-за поражения.");
            return;
        }

        // Первый вход в slide после телепорта или рестарта помечает игрока как находящегося в активной зоне.
        if (!isInActiveZone)
            isInActiveZone = true;

        currentSlidePlane = slidePlane;
        currentSlideTiltAngleX = tiltAngleX;
        
        // Строим мировую плоскость скольжения: по forward/right объекта slidePlane.
        hasSlideWorldPlane = false;
        if (slidePlane != null)
        {
            Vector3 f = slidePlane.forward;
            Vector3 r = slidePlane.right;
            if (f.sqrMagnitude > 0.0001f && r.sqrMagnitude > 0.0001f)
            {
                Vector3 n = Vector3.Cross(r, f);
                if (n.sqrMagnitude > 0.0001f)
                {
                    n.Normalize();
                    slideWorldPlane = new Plane(n, slidePlane.position);
                    hasSlideWorldPlane = true;
                }
            }
        }
        
        if (stopSlideButton != null)
            stopSlideButton.SetActive(true);
        if (runtimeVfx1 != null)
        {
            ApplyVfxScale(runtimeVfx1, vfxScale);
            runtimeVfx1.gameObject.SetActive(true);
        }
        if (runtimeVfx2 != null)
        {
            ApplyVfxScale(runtimeVfx2, vfxScale);
            runtimeVfx2.gameObject.SetActive(true);
        }
    }
    
    /// <summary>
    /// Вызывается при выходе из зоны или при нажатии кнопки остановки.
    /// </summary>
    public void ExitSlide()
    {
        currentSlidePlane = null;
        if (stopSlideButton != null)
            stopSlideButton.SetActive(false);
        if (runtimeVfx1 != null)
            runtimeVfx1.gameObject.SetActive(false);
        if (runtimeVfx2 != null)
            runtimeVfx2.gameObject.SetActive(false);
    }
    
    /// <summary>
    /// Вызвать из OnClick кнопки остановки скольжения. Прерывает slide и телепортирует игрока на базу через TeleportManager (housePos).
    /// </summary>
    public void StopSlide()
    {
        if (debug)
            Debug.Log("[SlideManager] StopSlide: кнопка нажата.");
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (debug)
            Debug.Log(player != null ? "[SlideManager] Игрок найден по тегу Player." : "[SlideManager] Игрок не найден (тег Player?).");
        
        if (player != null)
        {
            var controller = player.GetComponent<ThirdPersonController>();
            if (debug)
                Debug.Log(controller != null ? "[SlideManager] ThirdPersonController найден, вызываю ExitSlide()." : "[SlideManager] ThirdPersonController не найден на игроке.");
            if (controller != null)
                controller.ExitSlide();
        }
        ExitSlide();
        
        TeleportManager tm = TeleportManager.Instance;
        if (tm == null)
            tm = FindFirstObjectByType<TeleportManager>();
        if (debug)
            Debug.Log(tm != null ? "[SlideManager] TeleportManager найден, вызываю TeleportToHouse()." : "[SlideManager] TeleportManager не найден в сцене.");
        if (tm != null)
        {
            // StopSlide тоже считается телепортацией домой — сбрасываем активную зону и состояние затухания скорости.
            SetInActiveZone(false);
            tm.TeleportToHouse();
            tm.StartBrainrotRespawnAsync();
        }
        else
            Debug.LogWarning("[SlideManager] TeleportManager не найден в сцене. Добавь объект с TeleportManager и назначь House Pos.");
    }
    
    /// <summary>
    /// Статический вызов для кнопки: не требует ссылки на объект SlideManager. Находит SlideManager и вызывает StopSlide().
    /// </summary>
    public static void StopSlideStatic()
    {
        if (Instance != null)
        {
            Instance.StopSlide();
            return;
        }
        SlideManager sm = FindFirstObjectByType<SlideManager>();
        if (sm != null)
            sm.StopSlide();
        else if (Application.isPlaying)
            Debug.LogWarning("[SlideManager] StopSlideStatic: SlideManager не найден в сцене (Instance=null, FindFirstObjectByType тоже null).");
    }
    
    /// <summary> Активен ли режим slide (игрок на склоне). </summary>
    public bool IsSlideActive()
    {
        return currentSlidePlane != null;
    }

    /// <summary> Offset по Y: при slide, если камера сзади (Z камеры &gt; Z игрока), камера не опускается ниже Y игрока + это значение. </summary>
    public float GetCameraMinYOffsetWhenBehindPlayer()
    {
        return cameraMinYOffsetWhenBehindPlayer;
    }

    /// <summary>
    /// Скорость скольжения (для ThirdPersonController).
    /// </summary>
    public float GetSlideSpeed()
    {
        // Возвращаем текущую (затухающую) скорость скольжения.
        return currentSlidePlane != null && currentSlideSpeed > 0f ? currentSlideSpeed : slideSpeed;
    }
    
    /// <summary> Направление скольжения (вперёд по склону). </summary>
    public Vector3 GetSlideDirection()
    {
        if (currentSlidePlane != null)
        {
            Vector3 f = currentSlidePlane.forward;
            if (f.sqrMagnitude > 0.001f) return f.normalized;
        }
        return Vector3.forward;
    }
    
    /// <summary> Направление «вправо» на склоне (для стрейфа A/D). </summary>
    public Vector3 GetSlideRight()
    {
        if (currentSlidePlane != null)
        {
            Vector3 f = currentSlidePlane.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 0.001f) return Vector3.Cross(Vector3.up, f).normalized;
            return currentSlidePlane.right;
        }
        return Vector3.right;
    }
    
    /// <summary> Угол наклона модели по X при скольжении (градусы). </summary>
    public float GetSlideTiltAngleX()
    {
        return currentSlideTiltAngleX;
    }
    
    /// <summary>
    /// Проецирует заданную мировую точку на плоскость скольжения.
    /// Возвращает true, если плоскость определена (есть активный slidePlane).
    /// </summary>
    public bool TryProjectPointOnSlidePlane(Vector3 worldPos, out Vector3 projected)
    {
        if (!hasSlideWorldPlane)
        {
            projected = worldPos;
            return false;
        }
        
        // Проекция на плоскость: смещаем точку вдоль нормали на расстояние до плоскости.
        float distance = slideWorldPlane.GetDistanceToPoint(worldPos);
        projected = worldPos - slideWorldPlane.normal * distance;
        return true;
    }
    
    /// <summary>
    /// Возвращает Transform игрока (кэш).
    /// </summary>
    public Transform GetPlayerTransform()
    {
        if (cachedPlayer == null)
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        return cachedPlayer != null ? cachedPlayer.transform : null;
    }
    
    /// <summary>
    /// Возвращает ThirdPersonController игрока (кэш, один GetComponent на сцену).
    /// </summary>
    public ThirdPersonController GetPlayerController()
    {
        if (cachedPlayerController == null && cachedPlayer == null)
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (cachedPlayerController == null && cachedPlayer != null)
            cachedPlayerController = cachedPlayer.GetComponent<ThirdPersonController>();
        return cachedPlayerController;
    }
    
    /// <summary>
    /// Смещение игрока по Y в режиме slide.
    /// </summary>
    public float GetPlayerSlideOffsetY()
    {
        return playerSlideOffsetY;
    }
    
    /// <summary>
    /// Скорость плавного поворота модели при A/D в slide (градусов в секунду).
    /// </summary>
    public float GetPlayerRotationSpeed()
    {
        return playerRotationSpeed;
    }
    
    /// <summary>
    /// Время сглаживания позиции игрока по Y до траектории slide.
    /// </summary>
    public float GetSlideYOffsetLerpTime()
    {
        return slideYOffsetLerpTime;
    }

    /// <summary>
    /// Находится ли сейчас игрок в активной зоне slide.
    /// </summary>
    public bool IsInActiveZone()
    {
        return isInActiveZone;
    }

    /// <summary>
    /// Сбрасывает флаг активной зоны (например, при любом телепорте домой).
    /// </summary>
    public void SetInActiveZone(bool value)
    {
        isInActiveZone = value;

        // По условию дизайна «секундный лимит» и состояние затухания скорости
        // обновляются только при телепортациях домой (когда активная зона сбрасывается).
        if (!isInActiveZone)
        {
            // Телепорт домой: пересчитываем лимит секунд и полностью сбрасываем состояние скольжения.
            RecalculateEffectiveMinSeconds(true);
        }
    }

    /// <summary>
    /// Пересчитывает конечный лимит секунд затухания скорости с учётом уровня игрока.
    /// При resetState=true дополнительно сбрасывает таймер и текущую скорость.
    /// </summary>
    private void RecalculateEffectiveMinSeconds(bool resetState)
    {
        int level = 0;
        var storage = GameStorage.Instance;
        if (storage != null)
        {
            level = storage.GetPlayerSpeedLevel();
        }
        lastSpeedLevel = level;
        effectiveMinSeconds = Mathf.Max(1f, minSecondsLimit + addSecondsByLevel * level);

        if (resetState)
        {
            slideElapsedTime = 0f;
            currentSlideSpeed = slideSpeed;
        }
    }
}
