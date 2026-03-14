using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Модальное окно громкости музыки.
/// Видимость — через localScale (0 = скрыто, 1 = видно), аналогично SkinModalController.
/// Внутри Image-трека живёт Image-ползунок. Клик/драг по треку двигает ползунок и меняет громкость.
/// </summary>
public class MusicModalController : MonoBehaviour
{
    [Header("Modal")]
    [Tooltip("Корневой объект модального окна (будет скрываться через localScale).")]
    [SerializeField] private GameObject modalWindow;
    
    [Header("Slider")]
    [Tooltip("Image-трек (фон полоски). Клик по нему задаёт позицию ползунка.")]
    [SerializeField] private RectTransform trackRect;
    [Tooltip("Image-ползунок (хэндл), который перемещается по X внутри трека.")]
    [SerializeField] private RectTransform handleRect;
    
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
    
    private bool _isDragging;
    private Canvas _rootCanvas;
    private Camera _uiCamera;
    
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
        CacheCanvas();
        HideImmediate();
        if (debug) Debug.Log($"[MusicModalController] Start: modalWindow='{(modalWindow != null ? modalWindow.name : "null")}'");
    }
    
    private void CacheCanvas()
    {
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null)
        {
            Canvas root = _rootCanvas.rootCanvas;
            if (root != null) _rootCanvas = root;
            _uiCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
        }
    }
    
    private void Update()
    {
        if (!_isOpen) return;
        
        bool pressed = Input.GetMouseButton(0);
        bool down = Input.GetMouseButtonDown(0);
        
        if (down)
        {
            if (IsPointerOverTrack())
            {
                _isDragging = true;
                ApplyPointerPosition();
            }
            else if (!IsPointerOverModal())
            {
                if (debug) Debug.Log("[MusicModalController] Клик вне модалки — закрываю.");
                Close();
                return;
            }
        }
        
        if (_isDragging && pressed)
        {
            ApplyPointerPosition();
        }
        
        if (Input.GetMouseButtonUp(0))
        {
            if (_isDragging)
            {
                _isDragging = false;
                SaveVolume();
            }
        }
    }
    
    #region Open / Close
    
    public void Open()
    {
        if (Time.unscaledTime - _closeTime < CloseCooldown) return;
        if (modalWindow == null)
        {
            Debug.LogWarning("[MusicModalController] Не назначен Modal Window.");
            return;
        }
        _isOpen = true;
        SyncHandleToVolume();
        
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(AnimateOpen());
        
        if (debug) Debug.Log("[MusicModalController] Open.");
    }
    
    public void Close()
    {
        _closeTime = Time.unscaledTime;
        _isOpen = false;
        _isDragging = false;
        if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
        HideImmediate();
        if (debug) Debug.Log("[MusicModalController] Close.");
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
    
    #endregion
    
    #region Slider logic
    
    private void ApplyPointerPosition()
    {
        if (trackRect == null || handleRect == null) return;
        
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRect, Input.mousePosition, _uiCamera, out localPoint))
            return;
        
        Rect rect = trackRect.rect;
        float halfHandle = handleRect.rect.width * 0.5f;
        float minX = rect.xMin + halfHandle;
        float maxX = rect.xMax - halfHandle;
        
        float clampedX = Mathf.Clamp(localPoint.x, minX, maxX);
        float t = (maxX > minX) ? (clampedX - minX) / (maxX - minX) : 0f;
        
        handleRect.anchoredPosition = new Vector2(clampedX, handleRect.anchoredPosition.y);
        
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(t);
        
        if (debug) Debug.Log($"[MusicModalController] Volume: {t:F2}");
    }
    
    private void SyncHandleToVolume()
    {
        if (trackRect == null || handleRect == null) return;
        float vol = MusicManager.Instance != null ? MusicManager.Instance.GetVolume() : 0.5f;
        
        Rect rect = trackRect.rect;
        float halfHandle = handleRect.rect.width * 0.5f;
        float minX = rect.xMin + halfHandle;
        float maxX = rect.xMax - halfHandle;
        
        float x = Mathf.Lerp(minX, maxX, vol);
        handleRect.anchoredPosition = new Vector2(x, handleRect.anchoredPosition.y);
    }
    
    private void SaveVolume()
    {
        if (MusicManager.Instance == null || GameStorage.Instance == null) return;
        GameStorage.Instance.SetMusicVolume(MusicManager.Instance.GetVolume());
    }
    
    #endregion
    
    #region Pointer detection
    
    private bool IsPointerOverTrack()
    {
        if (trackRect == null) return false;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(trackRect, Input.mousePosition, _uiCamera, out localPoint))
            return false;
        return trackRect.rect.Contains(localPoint);
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
