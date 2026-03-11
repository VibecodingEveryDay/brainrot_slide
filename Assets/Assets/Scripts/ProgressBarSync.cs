using UnityEngine;

/// <summary>
/// Синхронизирует вертикальный UI-ползунок (Slider в Canvas)
/// с реальным прогрессом игрока по Z между точками Start → ... → End в мире.
/// </summary>
public class ProgressBarSync : MonoBehaviour
{
    [Header("World")]
    [Tooltip("Родитель Points в мире: дети Start, Common, Rare, Epic, Legendary, Divine, Secret, End (по Z).")]
    [SerializeField] private Transform worldPointsRoot;

    [Header("UI")]
    [Tooltip("Родитель Points внутри ProgressBar в Canvas: дети Start, Common, Rare, Epic, Legendary, Divine, Secret, End (по Y).")]
    [SerializeField] private Transform uiPointsRoot;
    [Tooltip("RectTransform объекта Slider внутри ProgressBar, который двигается по Y.")]
    [SerializeField] private RectTransform sliderTransform;

    [Header("Player")]
    [Tooltip("Трансформ игрока (его позиция Z используется для расчёта прогресса).")]
    [SerializeField] private Transform playerTransform;

    // Имена точек в порядке прогресса.
    private static readonly string[] PointNames =
    {
        "Start",
        "Common",
        "Rare",
        "Epic",
        "Legendary",
        "Divine",
        "Secret",
        "End"
    };

    // Массивы координат по Z (мир) и Y (UI) для всех точек.
    private float[] worldZ;
    private float[] uiY;
    
    private bool isInitialized;

    private void Awake()
    {
        TryInitialize();
    }

    private void OnValidate()
    {
        // Попытаться переинициализировать в редакторе при изменении ссылок.
        if (Application.isPlaying)
            return;
        TryInitialize();
    }

    private void TryInitialize()
    {
        isInitialized = false;

        if (worldPointsRoot == null || uiPointsRoot == null || sliderTransform == null || playerTransform == null)
            return;

        int count = PointNames.Length;
        worldZ = new float[count];
        uiY = new float[count];
        
        for (int i = 0; i < count; i++)
        {
            string name = PointNames[i];
            
            Transform w = worldPointsRoot.Find(name);
            Transform u = uiPointsRoot.Find(name);
            
            if (w == null || u == null)
            {
                Debug.LogError($"[ProgressBarSync] Не найдена точка \"{name}\" в {(w == null ? "worldPointsRoot" : "uiPointsRoot")}.");
                return;
            }
            
            worldZ[i] = w.position.z;
            uiY[i] = ((RectTransform)u).localPosition.y;
        }
        
        // Проверка, что первая и последняя точки не совпадают по Z
        if (Mathf.Approximately(worldZ[0], worldZ[count - 1]))
        {
            Debug.LogError("[ProgressBarSync] Z Start и Z End совпадают — прогресс не может быть рассчитан.");
            return;
        }
        
        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
        {
            // Попробуем инициализироваться ещё раз, если ссылки появились позже (например, через инстанциирование Canvas).
            TryInitialize();
            if (!isInitialized)
                return;
        }

        if (playerTransform == null || sliderTransform == null)
            return;

        float playerZ = playerTransform.position.z;
        float targetY = uiY[0];
        
        // Если игрок до первой точки — ставим Slider на Start.
        if (playerZ <= worldZ[0])
        {
            targetY = uiY[0];
        }
        // Если игрок дальше последней точки — ставим Slider на End.
        else if (playerZ >= worldZ[worldZ.Length - 1])
        {
            targetY = uiY[uiY.Length - 1];
        }
        else
        {
            // Находим отрезок [i, i+1], в котором находится Z игрока,
            // и интерполируем между соответствующими UI-точками.
            for (int i = 0; i < worldZ.Length - 1; i++)
            {
                float z0 = worldZ[i];
                float z1 = worldZ[i + 1];
                
                // Если этот сегмент вырождён, пропускаем.
                if (Mathf.Approximately(z0, z1))
                    continue;
                
                bool between =
                    (playerZ >= z0 && playerZ <= z1) ||
                    (playerZ >= z1 && playerZ <= z0);
                
                if (!between)
                    continue;
                
                float t = Mathf.InverseLerp(z0, z1, playerZ);
                targetY = Mathf.Lerp(uiY[i], uiY[i + 1], t);
                break;
            }
        }

        Vector3 pos = sliderTransform.localPosition;
        pos.y = targetY;
        sliderTransform.localPosition = pos;
    }
}

