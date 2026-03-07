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
    
    [Header("Debug")]
    [Tooltip("Писать в консоль при нажатии кнопки Stop и на каждом шаге (игрок, контроллер, TeleportManager, телепорт).")]
    [SerializeField] private bool debug;
    
    private SlideGround currentSlideGround;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (stopSlideButton != null)
            stopSlideButton.SetActive(false);
    }
    
    /// <summary>
    /// Вызывается из SlideGround при входе игрока в триггер.
    /// </summary>
    public void EnterSlide(SlideGround slideGround)
    {
        currentSlideGround = slideGround;
        if (stopSlideButton != null)
            stopSlideButton.SetActive(true);
    }
    
    /// <summary>
    /// Вызывается при выходе из зоны или при нажатии кнопки остановки.
    /// </summary>
    public void ExitSlide()
    {
        currentSlideGround = null;
        if (stopSlideButton != null)
            stopSlideButton.SetActive(false);
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
            tm.TeleportToHouse();
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
    
    /// <summary>
    /// Скорость скольжения (для ThirdPersonController).
    /// </summary>
    public float GetSlideSpeed()
    {
        return slideSpeed;
    }
    
    /// <summary>
    /// Смещение игрока по Y в режиме slide.
    /// </summary>
    public float GetPlayerSlideOffsetY()
    {
        return playerSlideOffsetY;
    }
}
