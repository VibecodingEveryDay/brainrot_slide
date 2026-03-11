using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Скрывает объекты с тегом RedCone на расстоянии больше заданного от игрока (оптимизация).
/// Дальше дистанции — GameObject выключается, ближе — включается.
/// </summary>
public class RedConeDistanceHider : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Transform игрока. Если не задан, ищется по тегу 'Player'.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Дистанция от игрока: дальше неё RedCone скрываются.")]
    [SerializeField] private float hideRange = 40f;

    [Tooltip("Как часто обновлять видимость (сек). 0 = каждый кадр.")]
    [SerializeField] private float updateInterval = 0.2f;

    [Tooltip("Тег объектов-конусов для скрытия")]
    [SerializeField] private string redConeTag = "RedCone";

    [Tooltip("Периодически пересобирать список конусов (если они появляются/исчезают). 0 = не пересобирать.")]
    [SerializeField] private float autoRebuildInterval = 5f;

    [Header("Отладка")]
    [SerializeField] private bool debug = false;

    private float _timer;
    private float _rebuildTimer;
    private bool _playerNotFoundLogged;

    private readonly List<Entry> _entries = new List<Entry>(64);

    private struct Entry
    {
        public GameObject gameObject;
        public bool currentlyHidden;
    }

    private void Awake()
    {
        EnsurePlayer();
        RebuildCache();
    }

    private void OnEnable()
    {
        _timer = 0f;
        _rebuildTimer = 0f;
        EnsurePlayer();
        RebuildCache();
        RefreshNow();
    }

    private void Update()
    {
        EnsurePlayer();
        if (playerTransform == null) return;

        if (autoRebuildInterval > 0f)
        {
            _rebuildTimer += Time.deltaTime;
            if (_rebuildTimer >= autoRebuildInterval)
            {
                _rebuildTimer = 0f;
                RebuildCache();
            }
        }

        _timer += Time.deltaTime;
        if (updateInterval > 0f && _timer < updateInterval) return;
        _timer = 0f;

        RefreshNow();
    }

    private void RebuildCache()
    {
        _entries.Clear();

        // Ищем все RedCone (включая неактивные), чтобы иметь возможность их снова включать.
        RedCone[] cones = FindObjectsByType<RedCone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cones.Length; i++)
        {
            if (cones[i] == null) continue;
            GameObject go = cones[i].gameObject;
            if (go == null) continue;
            if (!string.IsNullOrEmpty(redConeTag) && !go.CompareTag(redConeTag))
                continue;

            _entries.Add(new Entry { gameObject = go, currentlyHidden = !go.activeSelf });
        }

        if (debug)
            Debug.Log($"[RedConeDistanceHider] Кэш: {_entries.Count} объектов (компонент RedCone, тег '{redConeTag}').");
    }

    private void RefreshNow()
    {
        if (playerTransform == null)
        {
            if (!_playerNotFoundLogged)
            {
                _playerNotFoundLogged = true;
                Debug.LogWarning("[RedConeDistanceHider] Игрок не найден (тег 'Player').", this);
            }
            return;
        }

        float hideRangeSqr = hideRange * hideRange;
        Vector3 playerPos = playerTransform.position;

        for (int i = 0; i < _entries.Count; i++)
        {
            Entry e = _entries[i];
            if (e.gameObject == null) continue;

            float distSqr = (e.gameObject.transform.position - playerPos).sqrMagnitude;
            bool shouldHide = distSqr > hideRangeSqr;

            if (shouldHide == e.currentlyHidden) continue;

            e.gameObject.SetActive(!shouldHide);
            e.currentlyHidden = shouldHide;
            _entries[i] = e;

            if (debug)
                Debug.Log($"[RedConeDistanceHider] {(shouldHide ? "HIDE" : "SHOW")} {e.gameObject.name}");
        }
    }

    private void EnsurePlayer()
    {
        if (playerTransform != null) return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player != null ? player.transform : null;
        if (playerTransform != null)
            _playerNotFoundLogged = false;
    }

    /// <summary>
    /// Принудительно пересобрать список конусов и обновить видимость (например после телепорта).
    /// </summary>
    public void ForceRefresh()
    {
        RebuildCache();
        RefreshNow();
    }
}
