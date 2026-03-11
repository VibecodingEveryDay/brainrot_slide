using UnityEngine;

/// <summary>
/// Зона AntiFly: внутри этого триггера игрок НЕ переходит в состояние полёта/падения
/// (анимация fly не включается, даже если CharacterController не на земле).
/// Скрипт вешается на объект с MeshCollider (или любым другим Collider) с isTrigger = true.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AntiFlyTrigger : MonoBehaviour
{
    private void Reset()
    {
        // Автоматически включаем режим триггера для удобства.
        var col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ThirdPersonController controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
            return;

        controller.SetAntiFlyZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        ThirdPersonController controller = other.GetComponentInParent<ThirdPersonController>();
        if (controller == null)
            return;

        controller.SetAntiFlyZone(false);
    }
}

