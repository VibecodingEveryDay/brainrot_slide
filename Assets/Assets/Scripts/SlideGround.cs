using UnityEngine;

/// <summary>
/// Платформа скольжения. Вешается на родителя с дочерними мешами (как GroundChessColor).
/// Коллайдера на родителе не требуется: на объект с MeshCollider (isTrigger) добавь SlideTriggerZone.
/// </summary>
public class SlideGround : MonoBehaviour
{
    [Header("Наклон модели при скольжении")]
    [Tooltip("Угол наклона модели персонажа по X (градусы), например 20 для платформы с уклоном 20°.")]
    [SerializeField] private float tiltAngleX = 20f;
    
    /// <summary>
    /// Вызывается из SlideGroundTrigger при входе игрока в зону. Не вызывай вручную.
    /// </summary>
    public void OnPlayerEnter(Collider other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        if (SlideManager.Instance != null)
            SlideManager.Instance.EnterSlide(transform, tiltAngleX);
        var controller = other.GetComponent<ThirdPersonController>();
        if (controller != null)
            controller.EnterSlide();
    }
    
    /// <summary>
    /// Вызывается из SlideGroundTrigger при выходе игрока из зоны. Не вызывай вручную.
    /// </summary>
    public void OnPlayerExit(Collider other)
    {
        if (other == null || !other.CompareTag("Player")) return;
        var controller = other.GetComponent<ThirdPersonController>();
        if (controller != null)
            controller.ExitSlide();
        if (SlideManager.Instance != null)
            SlideManager.Instance.ExitSlide();
    }
    
    /// <summary>
    /// Вход игрока в зону (без ссылки на collider — игрок ищется по тегу). Для использования из SlideGroundTrigger при проверке overlap.
    /// </summary>
    public void OnPlayerEnter()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        if (SlideManager.Instance != null)
            SlideManager.Instance.EnterSlide(transform, tiltAngleX);
        var controller = player.GetComponent<ThirdPersonController>();
        if (controller != null)
            controller.EnterSlide();
    }
    
    /// <summary>
    /// Выход игрока из зоны. Для использования из SlideGroundTrigger при проверке overlap.
    /// </summary>
    public void OnPlayerExit()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        var controller = player.GetComponent<ThirdPersonController>();
        if (controller != null)
            controller.ExitSlide();
        if (SlideManager.Instance != null)
            SlideManager.Instance.ExitSlide();
    }
    
    /// <summary>
    /// Направление скольжения вдоль наклона платформы (forward с учётом наклона, не горизонталь).
    /// </summary>
    public Vector3 GetSlideDirection()
    {
        Vector3 f = transform.forward;
        if (f.sqrMagnitude < 0.001f)
            return Vector3.forward;
        return f.normalized;
    }
    
    /// <summary>
    /// Направление «вправо» на склоне (горизонтальное, для стрейфа A/D).
    /// </summary>
    public Vector3 GetSlideRight()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.001f)
            return transform.right;
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        return right;
    }
    
    /// <summary>
    /// Угол наклона модели по X при скольжении (градусы).
    /// </summary>
    public float GetTiltAngleX()
    {
        return tiltAngleX;
    }
}
