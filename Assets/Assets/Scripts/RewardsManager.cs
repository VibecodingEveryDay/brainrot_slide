using UnityEngine;

/// <summary>
/// Отсчитывает время сессии (с момента старта игры).
/// Используется RewardsModalController для расчёта оставшегося времени до награды.
/// </summary>
public class RewardsManager : MonoBehaviour
{
    public static RewardsManager Instance { get; private set; }

    /// <summary>Сколько секунд прошло с начала сессии.</summary>
    public float SessionTime { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        SessionTime += Time.deltaTime;
    }
}
