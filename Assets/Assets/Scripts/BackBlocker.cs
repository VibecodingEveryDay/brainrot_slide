using UnityEngine;

/// <summary>
/// Блокер позади платформы: пока игрок скользит — коллайдер триггер (пропускает),
/// когда игрок прошёл через него и слайд закончился — становится твёрдым, чтобы нельзя было вернуться назад.
/// При следующем входе в slide снова становится триггером.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BackBlocker : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Писать отладочные сообщения в консоль.")]
    [SerializeField] private bool debug = false;

    private Collider blockerCollider;
    private ThirdPersonController cachedController;

    private void Awake()
    {
        blockerCollider = GetComponent<Collider>();
        if (blockerCollider == null)
        {
            Debug.LogError("[BackBlocker] На объекте нет Collider.");
            enabled = false;
            return;
        }

        // По умолчанию считаем, что при старте сцены игрок ещё не скользит,
        // поэтому коллайдер должен быть твёрдым ИЛИ настраивается в инспекторе.
        // Если нужно, можно вручную включить isTrigger в сцене.
    }

    private void Update()
    {
        // Если игрок в slide — блокер должен быть триггером (пропускает назад/вперёд).
        var controller = GetPlayerController();
        if (controller == null || blockerCollider == null)
            return;

        bool isSliding = controller.IsOnSlide();

        if (isSliding && !blockerCollider.isTrigger)
        {
            blockerCollider.isTrigger = true;
            if (debug)
                Debug.Log("[BackBlocker] Игрок снова в slide: collider = trigger (проходной).");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var controller = GetPlayerControllerFrom(other);
        if (controller == null)
            return;

        // Пока BackBlocker неосязаемый (isTrigger=true) и игрок касается его, блокируем движение назад (S).
        if (blockerCollider != null && blockerCollider.isTrigger)
        {
            controller.SetBackwardMovementBlocked(true);
        }

        // Если игрок скользит и проходит через BackBlocker — прерываем slide.
        if (controller.IsOnSlide())
        {
            if (debug)
                Debug.Log("[BackBlocker] Player вошёл в BackBlocker во время slide: выключаю slide.");

            controller.ExitSlide();

            SlideManager sm = SlideManager.Instance;
            if (sm == null)
                sm = FindFirstObjectByType<SlideManager>();
            if (sm != null)
                sm.ExitSlide();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        var controller = GetPlayerControllerFrom(other);
        if (controller == null || blockerCollider == null)
            return;

        // Игрок прошёл через плоскость BackBlocker.
        // Если он уже НЕ в slide, делаем коллайдер твёрдым, чтобы нельзя было вернуться назад.
        if (!controller.IsOnSlide())
        {
            blockerCollider.isTrigger = false;
            // Коллайдер стал осязаемым — разблокируем движение назад.
            controller.SetBackwardMovementBlocked(false);
            if (debug)
                Debug.Log("[BackBlocker] Player прошёл через BackBlocker, slide выключен: collider = solid (isTrigger=false), backward movement unblocked.");
        }
    }

    private ThirdPersonController GetPlayerController()
    {
        if (cachedController != null)
            return cachedController;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return null;

        cachedController = player.GetComponent<ThirdPersonController>();
        return cachedController;
    }

    private ThirdPersonController GetPlayerControllerFrom(Collider other)
    {
        // Пытаемся найти контроллер на объекте, родителях или детях.
        ThirdPersonController controller = other.GetComponent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInChildren<ThirdPersonController>();

        return controller;
    }
}

