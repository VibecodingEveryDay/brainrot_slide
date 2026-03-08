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
        var controller = other.GetComponent<ThirdPersonController>();
        if (controller == null || controller.IsOnSlide())
            return;
        if (SlideManager.Instance == null)
            return;
        SlideManager.Instance.EnterSlide(transform, tiltAngleX);
        controller.EnterSlide();
    }
}
