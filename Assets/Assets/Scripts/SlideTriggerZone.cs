using UnityEngine;

/// <summary>
/// Вешается на тот же объект, что и Collider (isTrigger).
/// При входе игрока в триггер включается режим slide. Направление скольжения = forward этого объекта.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlideTriggerZone : MonoBehaviour
{
    [Tooltip("Угол наклона модели персонажа по X при скольжении (градусы).")]
    [SerializeField] private float tiltAngleX = 20f;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[SlideTriggerZone] {gameObject.name}: Collider должен быть Is Trigger = true.");
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player"))
            return;

        // Если игрок уже находится в процессе телепортации из-за поражения
        // (например, после удара охранника), запрещаем вход в режим slide.
        var tp = TeleportManager.Instance;
        if (tp != null && tp.IsTeleportingDueToLose())
            return;

        var controller = other.GetComponent<ThirdPersonController>();
        if (controller == null || controller.IsOnSlide())
            return;
        if (SlideManager.Instance == null)
            return;
        SlideManager.Instance.EnterSlide(transform, tiltAngleX);
        controller.EnterSlide();
    }
    
    /// <summary>
    /// Форсированно включает режим slide для указанного контроллера,
    /// если он находится в этом триггере и не в режиме скольжения.
    /// Используется, например, при сходе с платформы, которая временно отключала slide.
    /// </summary>
    public void ForceEnterSlide(ThirdPersonController controller)
    {
        if (controller == null)
            return;
        if (SlideManager.Instance == null)
            return;
        if (controller.IsOnSlide())
            return;
        
        SlideManager.Instance.EnterSlide(transform, tiltAngleX);
        controller.EnterSlide();
    }
}
