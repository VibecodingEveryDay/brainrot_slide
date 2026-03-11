using UnityEngine;

/// <summary>
/// Платформа, которая отключает режим slide, когда игрок наступает на неё.
/// Повесь этот скрипт на объект с BoxCollider (Is Trigger = true).
/// </summary>
public class PlatformBrainrot : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Отключать slide только если игрок сейчас в режиме скольжения.")]
    [SerializeField] private bool onlyIfSliding = true;

    private void Reset()
    {
        // Гарантируем, что коллайдер на объекте — триггер.
        var col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        if (other.CompareTag("Bot"))
        {
            Destroy(other.gameObject);
            return;
        }

        if (!other.CompareTag("Player"))
            return;

        // Пытаемся найти контроллер игрока на объекте, родителях или детях.
        ThirdPersonController controller = other.GetComponent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInChildren<ThirdPersonController>();

        if (controller == null)
            return;

        if (onlyIfSliding && !controller.IsOnSlide())
            return;

        // 1) Выключаем локальный режим скольжения у игрока.
        controller.ExitSlide();

        // 2) Сообщаем SlideManager, чтобы он тоже отключил slide (кнопка, VFX и т.п.).
        SlideManager sm = SlideManager.Instance;
        if (sm == null)
            sm = FindFirstObjectByType<SlideManager>();

        if (sm != null)
            sm.ExitSlide();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        
        // Пытаемся найти контроллер игрока на объекте, родителях или детях.
        ThirdPersonController controller = other.GetComponent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
            controller = other.GetComponentInChildren<ThirdPersonController>();
        
        if (controller == null)
            return;
        
        // Если игрок уже в slide — ничего не делаем.
        if (controller.IsOnSlide())
            return;
        
        // Проверяем, находится ли игрок внутри какого-либо SlideTriggerZone.
        Vector3 playerPos = controller.transform.position;
        const float checkRadius = 0.6f;
        Collider[] hits = Physics.OverlapSphere(playerPos, checkRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            
            // Ищем SlideTriggerZone на коллайдере или его родителях.
            SlideTriggerZone slideZone = hit.GetComponent<SlideTriggerZone>();
            if (slideZone == null)
                slideZone = hit.GetComponentInParent<SlideTriggerZone>();
            if (slideZone == null)
                slideZone = hit.GetComponentInChildren<SlideTriggerZone>();
            
            if (slideZone != null)
            {
                // Игрок всё ещё в зоне slide — включаем slide снова.
                slideZone.ForceEnterSlide(controller);
                break;
            }
        }
    }
}

