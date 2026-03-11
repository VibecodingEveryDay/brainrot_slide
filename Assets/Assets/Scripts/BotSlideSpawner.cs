using UnityEngine;
using System.Collections;

/// <summary>
/// Спавнит бота на слайде раз в случайный интервал [spawnIntervalMin, spawnIntervalMax] секунд.
/// Бот создаётся с BotSlideBehavior; плоскость слайда и параметры передаются через Init.
/// </summary>
public class BotSlideSpawner : MonoBehaviour
{
    [Header("Префаб и интервал")]
    [SerializeField] private GameObject botPrefab;
    [SerializeField] private float spawnIntervalMin = 3f;
    [SerializeField] private float spawnIntervalMax = 7f;

    [Header("Плоскость слайда (тот же объект, что у SlideGround)")]
    [SerializeField] private Transform slidePlane;
    [SerializeField] private float tiltAngleX = 20f;
    [Tooltip("Скорость скольжения бота — случайное значение в диапазоне [Min, Max].")]
    [SerializeField] private float slideSpeedMin = 12f;
    [SerializeField] private float slideSpeedMax = 18f;
    [Tooltip("Смещение бота по Y над плоскостью слайда (передаётся в BotSlideBehavior.Init).")]
    [SerializeField] private float botSlideOffsetY = 0f;

    [Header("Точка спавна")]
    [Tooltip("Если не задан — используется position/rotation этого объекта.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Диапазон по X для случайной позиции спавна (в мировых координатах). Рисуется в сцене как Gizmos.")]
    [SerializeField] private float spawnXMin = -10f;
    [SerializeField] private float spawnXMax = 10f;

    private void Start()
    {
        if (botPrefab != null && slidePlane != null)
            StartCoroutine(SpawnLoop());
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float delay = Random.Range(spawnIntervalMin, spawnIntervalMax);
            yield return new WaitForSeconds(delay);

            Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            float x = Random.Range(spawnXMin, spawnXMax);
            Vector3 pos = new Vector3(x, basePos.y, basePos.z);
            GameObject bot = Instantiate(botPrefab, pos, rot);

            float speed = Random.Range(slideSpeedMin, slideSpeedMax);
            var behavior = bot.GetComponent<BotSlideBehavior>();
            if (behavior != null)
                behavior.Init(slidePlane, tiltAngleX, speed, botSlideOffsetY);
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 pMin = new Vector3(spawnXMin, basePos.y, basePos.z);
        Vector3 pMax = new Vector3(spawnXMax, basePos.y, basePos.z);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(pMin, pMax);
        float h = 1f;
        Gizmos.DrawLine(pMin, pMin + Vector3.up * h);
        Gizmos.DrawLine(pMin, pMin - Vector3.up * h);
        Gizmos.DrawLine(pMax, pMax + Vector3.up * h);
        Gizmos.DrawLine(pMax, pMax - Vector3.up * h);
    }
}
