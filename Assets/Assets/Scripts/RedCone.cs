using UnityEngine;

/// <summary>
/// Префаб с MeshCollider (Is Trigger). При столкновении игрока с конусом —
/// поражение: телепорт на базу с текстом «Вы проиграли», как при ударе охранника.
/// </summary>
public class RedCone : MonoBehaviour
{
    [Tooltip("Тег объекта игрока")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        if (other.CompareTag("Bot"))
        {
            Destroy(other.gameObject);
            return;
        }

        if (!other.CompareTag(playerTag))
            return;

        TeleportManager tm = TeleportManager.Instance;
        if (tm == null)
        {
            tm = FindFirstObjectByType<TeleportManager>();
            if (tm == null)
            {
                Debug.LogWarning("[RedCone] TeleportManager не найден в сцене.");
                return;
            }
        }

        // Единая механика поражения через синглтон TeleportManager.
        tm.HandleLoseFromObstacle(0f);
    }
}
