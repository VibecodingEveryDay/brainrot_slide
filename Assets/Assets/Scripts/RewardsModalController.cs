using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Модальное окно наград. 5 ячеек с брейнротами, у каждой свой таймер.
/// Когда время сессии >= requiredTime — текст становится «БЕРИ» / «GET», клик спавнит брейнрота.
/// Видимость — через localScale (как SkinModalController).
/// </summary>
public class RewardsModalController : MonoBehaviour
{
    [Header("Modal")]
    [SerializeField] private GameObject modalWindow;

    [Header("Reward Slots")]
    [SerializeField] private RewardSlot[] slots = new RewardSlot[5];

    [Header("God Rays")]
    [Tooltip("Префаб с UIGodRays (будет спавниться в ячейку, когда награда готова).")]
    [SerializeField] private GameObject raysPrefab;

    [Header("Закрытие")]
    [SerializeField] private Button closeButton;

    [Header("Анимация появления")]
    [SerializeField] private float openScaleStart = 0.3f;
    [SerializeField] private float openDuration = 0.2f;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private bool _isOpen;
    private float _closeTime = -999f;
    private const float CloseCooldown = 0.15f;
    private CanvasGroup _modalGroup;
    private Coroutine _animCoroutine;
    private Canvas _rootCanvas;
    private Camera _uiCamera;

    [System.Serializable]
    public class RewardSlot
    {
        [Tooltip("Родитель-Image ячейки (кликабельный).")]
        public RectTransform slotRoot;
        [Tooltip("TextMeshPro с таймером внутри ячейки.")]
        public TextMeshProUGUI timerLabel;
        [Tooltip("Время в секундах сессии, после которого награда доступна.")]
        public float requiredTime = 60f;
        [Tooltip("Префаб брейнрота, который спавнится при получении награды.")]
        public GameObject brainrotPrefab;

        [HideInInspector] public bool claimed;
        [HideInInspector] public GameObject raysInstance;
    }

    private void Awake()
    {
        if (modalWindow != null)
        {
            _modalGroup = modalWindow.GetComponent<CanvasGroup>();
            if (_modalGroup == null) _modalGroup = modalWindow.AddComponent<CanvasGroup>();
        }
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    private void Start()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null)
        {
            Canvas root = _rootCanvas.rootCanvas;
            if (root != null) _rootCanvas = root;
            _uiCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
        }
        HideImmediate();
    }

    private void Update()
    {
        if (!_isOpen) return;

        UpdateTimers();

        bool down = Input.GetMouseButtonDown(0);
        if (!down) return;

        int clickedSlot = GetClickedSlotIndex();
        if (clickedSlot >= 0)
        {
            TryClaimReward(clickedSlot);
            return;
        }

        if (!IsPointerOverModal())
        {
            if (debug) Debug.Log("[RewardsModal] Клик вне модалки — закрываю.");
            Close();
        }
    }

    #region Open / Close

    public void Open()
    {
        if (Time.unscaledTime - _closeTime < CloseCooldown) return;
        if (modalWindow == null) { Debug.LogWarning("[RewardsModal] Modal Window не назначен."); return; }

        _isOpen = true;
        ClearTimerTexts();
        UpdateTimers();

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateOpen());
        if (debug) Debug.Log("[RewardsModal] Open.");
    }

    public void Close()
    {
        _closeTime = Time.unscaledTime;
        _isOpen = false;
        if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
        HideImmediate();
        if (debug) Debug.Log("[RewardsModal] Close.");
    }

    private void ClearTimerTexts()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].timerLabel != null)
                slots[i].timerLabel.text = "";
            slots[i].raysInstance = null;
        }
    }

    private void HideImmediate()
    {
        if (modalWindow == null) return;
        modalWindow.transform.localScale = Vector3.zero;
        if (_modalGroup != null) { _modalGroup.interactable = false; _modalGroup.blocksRaycasts = false; }
    }

    private IEnumerator AnimateOpen()
    {
        Transform t = modalWindow.transform;
        if (_modalGroup != null) { _modalGroup.interactable = true; _modalGroup.blocksRaycasts = true; }
        float elapsed = 0f;
        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / openDuration);
            k = 1f - (1f - k) * (1f - k);
            float s = Mathf.Lerp(openScaleStart, 1f, k);
            t.localScale = new Vector3(s, s, s);
            yield return null;
        }
        t.localScale = Vector3.one;
        _animCoroutine = null;
    }

    #endregion

    #region Timers

    private void UpdateTimers()
    {
        float session = RewardsManager.Instance != null ? RewardsManager.Instance.SessionTime : 0f;
        bool isRu = LocalizationManager.IsRussian();

        for (int i = 0; i < slots.Length; i++)
        {
            RewardSlot slot = slots[i];
            if (slot.timerLabel == null) continue;

            if (slot.claimed)
            {
                slot.timerLabel.text = isRu ? "ПОЛУЧЕНО" : "CLAIMED";
                SpawnRaysIfNeeded(slot);
                continue;
            }

            float remaining = slot.requiredTime - session;
            if (remaining <= 0f)
            {
                slot.timerLabel.text = isRu ? "БЕРИ" : "GET";
                SpawnRaysIfNeeded(slot);
            }
            else
            {
                int totalSec = Mathf.CeilToInt(remaining);
                int min = totalSec / 60;
                int sec = totalSec % 60;
                slot.timerLabel.text = min > 0 ? $"{min}:{sec:D2}" : $"{sec}s";
            }
        }
    }

    private void SpawnRaysIfNeeded(RewardSlot slot)
    {
        if (slot.raysInstance != null) return;
        if (raysPrefab == null || slot.slotRoot == null) return;

        slot.raysInstance = Instantiate(raysPrefab, slot.slotRoot);
        RectTransform rt = slot.raysInstance.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsFirstSibling();
        }
        if (debug) Debug.Log($"[RewardsModal] Rays spawned in slot '{slot.slotRoot.name}'.");
    }

    #endregion

    #region Claim

    private void TryClaimReward(int index)
    {
        RewardSlot slot = slots[index];
        if (slot.claimed) return;

        float session = RewardsManager.Instance != null ? RewardsManager.Instance.SessionTime : 0f;
        if (session < slot.requiredTime)
        {
            if (debug) Debug.Log($"[RewardsModal] Слот {index}: ещё не готов ({slot.requiredTime - session:F0}s).");
            return;
        }

        if (slot.brainrotPrefab == null)
        {
            Debug.LogWarning($"[RewardsModal] Слот {index}: brainrotPrefab не назначен.");
            return;
        }

        Transform playerT = FindPlayerTransform();
        if (playerT == null)
        {
            Debug.LogWarning("[RewardsModal] Не найден игрок для спавна.");
            return;
        }

        Vector3 spawnPos = playerT.position + playerT.forward * 2f;
        spawnPos.y = playerT.position.y;
        Instantiate(slot.brainrotPrefab, spawnPos, Quaternion.identity);

        slot.claimed = true;
        if (debug) Debug.Log($"[RewardsModal] Слот {index}: брейнрот '{slot.brainrotPrefab.name}' заспавнен.");
    }

    private Transform FindPlayerTransform()
    {
        ThirdPersonController pc = FindFirstObjectByType<ThirdPersonController>();
        return pc != null ? pc.transform : null;
    }

    #endregion

    #region Pointer detection

    private int GetClickedSlotIndex()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].slotRoot == null) continue;
            Vector2 lp;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(slots[i].slotRoot, Input.mousePosition, _uiCamera, out lp))
            {
                if (slots[i].slotRoot.rect.Contains(lp))
                    return i;
            }
        }
        return -1;
    }

    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

    private bool IsPointerOverModal()
    {
        if (modalWindow == null || EventSystem.current == null) return false;
        PointerEventData ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _raycastResults);
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            if (_raycastResults[i].gameObject != null && _raycastResults[i].gameObject.transform.IsChildOf(modalWindow.transform))
                return true;
        }
        return false;
    }

    #endregion
}
