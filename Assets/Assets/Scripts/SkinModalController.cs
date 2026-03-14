using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Управляет модальным окном скинов.
/// Скрытие/показ ТОЛЬКО через localScale (0 = скрыто, 1 = видно). SetActive НЕ вызывается — UI не ломается.
/// Клик вне модалки определяется через EventSystem (без backdrop-объекта).
/// </summary>
public class SkinModalController : MonoBehaviour
{
    [Header("Modal")]
    [Tooltip("Модальное окно (ButtonsUI).")]
    [SerializeField] private GameObject modalWindow;
    
    [Header("Закрытие")]
    [Tooltip("Кнопка с иконкой закрытия (крестик). Опционально.")]
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
        HideImmediate();
        if (debug) Debug.Log($"[SkinModalController] Start: modalWindow='{(modalWindow != null ? modalWindow.name : "null")}', скрыт через scale=0.");
    }
    
    private void Update()
    {
        if (!_isOpen) return;
        
        bool clicked = Input.GetMouseButtonDown(0);
#if ENABLE_INPUT_SYSTEM
        if (!clicked && UnityEngine.InputSystem.Mouse.current != null)
            clicked = UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
#endif
        if (!clicked) return;
        
        if (!IsPointerOverModal())
        {
            if (debug) Debug.Log("[SkinModalController] Клик вне модалки — закрываю.");
            Close();
        }
    }
    
    public void Open()
    {
        if (Time.unscaledTime - _closeTime < CloseCooldown) return;
        if (modalWindow == null)
        {
            Debug.LogWarning("[SkinModalController] Не назначен Modal Window.");
            return;
        }
        _isOpen = true;
        
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateOpen());
        
        if (debug) Debug.Log("[SkinModalController] Open.");
    }
    
    public void Close()
    {
        _closeTime = Time.unscaledTime;
        _isOpen = false;
        if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
        HideImmediate();
        if (debug) Debug.Log("[SkinModalController] Close.");
    }
    
    private void HideImmediate()
    {
        if (modalWindow == null) return;
        modalWindow.transform.localScale = Vector3.zero;
        if (_modalGroup != null)
        {
            _modalGroup.interactable = false;
            _modalGroup.blocksRaycasts = false;
        }
    }
    
    private IEnumerator AnimateOpen()
    {
        Transform t = modalWindow.transform;
        if (_modalGroup != null)
        {
            _modalGroup.interactable = true;
            _modalGroup.blocksRaycasts = true;
        }
        
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
    
    private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    
    private bool IsPointerOverModal()
    {
        if (modalWindow == null || EventSystem.current == null) return false;
        PointerEventData ped = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        _raycastResults.Clear();
        EventSystem.current.RaycastAll(ped, _raycastResults);
        for (int i = 0; i < _raycastResults.Count; i++)
        {
            if (_raycastResults[i].gameObject != null && _raycastResults[i].gameObject.transform.IsChildOf(modalWindow.transform))
                return true;
        }
        return false;
    }
}
