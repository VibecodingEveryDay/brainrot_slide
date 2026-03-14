using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Один слот модального окна скинов (Obby, Scarf, Ninja, Gold): текст кнопки (TAKEN/BUY/PICK) и действие по клику.
/// </summary>
public class SkinSlotUI : MonoBehaviour
{
    public enum SkinSlot
    {
        Obby,
        Scarf,
        Ninja,
        Gold
    }
    
    [SerializeField] private SkinSlot slot = SkinSlot.Obby;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI buttonLabel;
    
    [Header("Optional: закрыть модалку после выбора скина")]
    [SerializeField] private SkinModalController modalController;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private string SkinId
    {
        get
        {
            switch (slot)
            {
                case SkinSlot.Scarf: return SkinManager.SkinIdScarf;
                case SkinSlot.Ninja: return SkinManager.SkinIdNinja;
                case SkinSlot.Gold: return SkinManager.SkinIdGold;
                default: return SkinManager.SkinIdDefault;
            }
        }
    }
    
    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (buttonLabel == null) buttonLabel = GetComponentInChildren<TextMeshProUGUI>();
        if (button != null) button.onClick.AddListener(OnClick);
    }
    
    private void OnEnable()
    {
        RefreshLabel();
    }
    
    private void OnBecameVisible()
    {
        RefreshLabel();
    }
    
    private float _nextRefresh;
    
    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.3f;
        if (transform.lossyScale.sqrMagnitude < 0.001f) return; // модалка скрыта (scale=0) — не обновляем
        RefreshLabel();
    }
    
    /// <summary> Обновить подпись кнопки по состоянию скина (взят/не куплен/выбрать). </summary>
    public void RefreshLabel()
    {
        if (buttonLabel == null) return;
        SkinManager sm = SkinManager.Instance;
        if (sm == null)
        {
            buttonLabel.text = GetLocalizedBuy();
            return;
        }
        string current = sm.GetCurrentSkinId();
        bool owned = sm.HasOwnedSkin(SkinId);
        bool taken = current == SkinId;
        
        if (taken)
            buttonLabel.text = GetLocalizedTaken();
        else if (!owned)
            buttonLabel.text = GetLocalizedBuy();
        else
            buttonLabel.text = GetLocalizedPick();
        if (debug) Debug.Log($"[SkinSlotUI] {slot}: RefreshLabel -> {buttonLabel.text} (owned={owned}, taken={taken})");
    }
    
    private void OnClick()
    {
        SkinManager sm = SkinManager.Instance;
        if (sm == null) return;
        bool owned = sm.HasOwnedSkin(SkinId);
        string currentId = sm.GetCurrentSkinId() ?? "";
        string myId = SkinId ?? "";
        bool taken = currentId == myId;
        
        if (!owned)
        {
            if (debug) Debug.Log($"[SkinSlotUI] {slot}: не куплен, открываю покупку.");
            if (!string.IsNullOrEmpty(myId))
                sm.PurchaseSkin(myId);
            return;
        }
        // Всегда применяем скин (даже если taken — на случай если модель рассинхронизировалась после ClearStorage)
        if (debug) Debug.Log($"[SkinSlotUI] {slot}: выбираю скин '{myId}' (taken={taken}).");
        sm.SetCurrentSkinId(myId);
        RefreshAllSlots();
        if (modalController != null) modalController.Close();
    }
    
    /// <summary> Обновить текст во ВСЕХ слотах (чтобы при выборе одного скина другие тоже поменяли статус). </summary>
    private static void RefreshAllSlots()
    {
        foreach (SkinSlotUI s in FindObjectsByType<SkinSlotUI>(FindObjectsSortMode.None))
            s.RefreshLabel();
    }
    
    private static string GetLocalizedTaken()
    {
        return LocalizationManager.GetCurrentLanguage() == "ru" ? "ВЗЯТ" : "TAKEN";
    }
    
    private static string GetLocalizedBuy()
    {
        return LocalizationManager.GetCurrentLanguage() == "ru" ? "КУПИТЬ" : "BUY";
    }
    
    private static string GetLocalizedPick()
    {
        return LocalizationManager.GetCurrentLanguage() == "ru" ? "ВЫБРАТЬ" : "PICK";
    }
}
