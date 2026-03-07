using UnityEngine;

/// <summary>
/// Зона скольжения. Вешается на того же объекте, что и SlideGround.
/// Вход в slide: по OnTriggerEnter (если есть коллайдер-триггер) ИЛИ по проверке позиции игрока в зоне-боксе каждый кадр (резерв).
/// Выход только по кнопке Stop.
/// </summary>
public class SlideGroundTrigger : MonoBehaviour
{
    [Header("Зона slide (бокс в локальных координатах SlideGround)")]
    [Tooltip("Размер зоны. Игрок входит в slide, когда его позиция попадает в этот бокс (в локальных координатах родителя с SlideGround).")]
    [SerializeField] private Vector3 zoneSize = new Vector3(10f, 5f, 10f);
    [Tooltip("Центр зоны в локальных координатах SlideGround.")]
    [SerializeField] private Vector3 zoneCenter = Vector3.zero;
    
    [Header("Триггер (опционально)")]
    [Tooltip("Создать BoxCollider-триггер на этом объекте при старте, если коллайдера нет. Дополняет проверку по зоне.")]
    [SerializeField] private bool createTriggerIfMissing = true;
    
    [Header("Gizmos")]
    [Tooltip("Рисовать в сцене зону Zone Size / Zone Center.")]
    [SerializeField] private bool drawGizmos = true;
    [Tooltip("Подсветить края зоны красным (End Area).")]
    [SerializeField] private bool endarea = false;
    
    private SlideGround slideGround;
    private GameObject cachedPlayer;
    private ThirdPersonController cachedController;
    
    private void Awake()
    {
        slideGround = GetComponent<SlideGround>();
        if (slideGround == null)
            slideGround = GetComponentInParent<SlideGround>();
        if (slideGround == null)
        {
            Debug.LogWarning($"[SlideGroundTrigger] {gameObject.name}: SlideGround не найден на этом объекте или родителе.");
            return;
        }
        
        if (createTriggerIfMissing)
            EnsureTriggerCollider();
    }
    
    private void EnsureTriggerCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
            return;
        if (col != null)
        {
            col.isTrigger = true;
            return;
        }
        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = zoneSize;
        box.center = zoneCenter;
    }
    
    private void FixedUpdate()
    {
        if (slideGround == null) return;
        
        if (cachedPlayer == null)
        {
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
            cachedController = cachedPlayer != null ? cachedPlayer.GetComponent<ThirdPersonController>() : null;
        }
        if (cachedPlayer == null || cachedController == null) return;
        if (cachedController.IsOnSlide()) return;
        
        // Резерв: вход в slide по позиции — игрок в боксе зоны (в локальных координатах SlideGround)
        Transform slideTransform = slideGround.transform;
        Vector3 playerWorld = cachedPlayer.transform.position;
        Vector3 playerLocal = slideTransform.InverseTransformPoint(playerWorld);
        Vector3 half = zoneSize * 0.5f;
        if (playerLocal.x >= zoneCenter.x - half.x && playerLocal.x <= zoneCenter.x + half.x &&
            playerLocal.y >= zoneCenter.y - half.y && playerLocal.y <= zoneCenter.y + half.y &&
            playerLocal.z >= zoneCenter.z - half.z && playerLocal.z <= zoneCenter.z + half.z)
        {
            slideGround.OnPlayerEnter();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (slideGround == null) return;
        if (other == null || !other.CompareTag("Player")) return;
        
        var controller = other.GetComponent<ThirdPersonController>();
        if (controller != null && controller.IsOnSlide())
            return;
        
        slideGround.OnPlayerEnter(other);
    }
    
    private void OnDrawGizmos()
    {
        SlideGround sg = slideGround != null ? slideGround : GetComponent<SlideGround>();
        if (sg == null) sg = GetComponentInParent<SlideGround>();
        if (sg == null) return;
        
        Transform t = sg.transform;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = t.localToWorldMatrix;
        
        if (drawGizmos)
        {
            // Заливка — полупрозрачный объём (хорошо видно зону)
            Gizmos.color = new Color(0f, 1f, 1f, 0.35f);
            Gizmos.DrawCube(zoneCenter, zoneSize);
            
            // Контур «потолще» — несколько вложенных WireCube с небольшим сдвигом масштаба
            Gizmos.color = new Color(0f, 0.8f, 1f, 1f);
            float[] offsets = { -0.02f, -0.01f, 0f, 0.01f, 0.02f };
            foreach (float o in offsets)
            {
                Vector3 s = zoneSize * (1f + o);
                Gizmos.DrawWireCube(zoneCenter, s);
            }
        }
        
        if (endarea)
        {
            // End Area — края зоны красным
            Gizmos.color = Color.red;
            float[] thick = { -0.03f, -0.015f, 0f, 0.015f, 0.03f };
            foreach (float o in thick)
            {
                Vector3 s = zoneSize * (1f + o);
                Gizmos.DrawWireCube(zoneCenter, s);
            }
        }
        
        Gizmos.matrix = oldMatrix;
    }
}
