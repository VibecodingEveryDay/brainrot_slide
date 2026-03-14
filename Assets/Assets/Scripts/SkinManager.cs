using UnityEngine;
using YG;

/// <summary>
/// Единая точка контроля скинов игрока: какой скин надет, какие куплены, применение скина на персонажа.
/// Используется SkinPurchaseHandler (после покупки) и UI модального окна скинов.
/// </summary>
public class SkinManager : MonoBehaviour
{
    public const string SkinIdDefault = "";   // Obby / дефолт
    public const string SkinIdScarf = "skin_scarf";
    public const string SkinIdNinja = "skin_ninja";
    public const string SkinIdGold = "skin_gold";
    
    [Header("Player")]
    [SerializeField] private ThirdPersonController player;
    
    [Header("Skin prefabs")]
    [Tooltip("Префаб дефолтного скина (Obby). Если не задан, при выборе дефолта не меняем модель.")]
    [SerializeField] private GameObject defaultSkinPrefab;
    [SerializeField] private GameObject scarfSkinPrefab;
    [SerializeField] private GameObject ninjaSkinPrefab;
    [SerializeField] private GameObject goldSkinPrefab;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private static SkinManager _instance;
    public static SkinManager Instance => _instance != null ? _instance : (_instance = FindFirstObjectByType<SkinManager>());
    
    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this) Destroy(this);
    }
    
    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }
    
    /// <summary> Текущий надетый скин (ID). </summary>
    public string GetCurrentSkinId()
    {
        return GameStorage.Instance != null ? GameStorage.Instance.GetCurrentSkinId() : SkinIdDefault;
    }
    
    /// <summary> Установить текущий скин: применить на игрока и сохранить. </summary>
    public void SetCurrentSkinId(string skinId)
    {
        skinId = skinId ?? SkinIdDefault;
        if (GameStorage.Instance != null)
            GameStorage.Instance.SetCurrentSkinId(skinId);
        ApplySkinById(skinId);
        if (debug) Debug.Log($"[SkinManager] SetCurrentSkinId: {skinId}");
    }
    
    /// <summary> Куплен ли скин (дефолт всегда «куплен»). </summary>
    public bool HasOwnedSkin(string skinId)
    {
        return GameStorage.Instance != null && GameStorage.Instance.HasOwnedSkin(skinId);
    }
    
    /// <summary> Добавить скин в купленные (вызывать после успешной покупки). </summary>
    public void AddOwnedSkinId(string skinId)
    {
        if (GameStorage.Instance != null)
            GameStorage.Instance.AddOwnedSkinId(skinId);
    }
    
    /// <summary> Применить скин по ID на игрока (без сохранения — для старта игры и консумирования). </summary>
    public void ApplySkinById(string skinId)
    {
        if (player == null) { if (debug) Debug.Log("[SkinManager] ApplySkinById: player == null"); return; }
        GameObject prefab = GetPrefabForSkinId(skinId);
        if (prefab == null)
        {
            Debug.LogWarning($"[SkinManager] ApplySkinById: префаб для '{skinId}' == null. Назначьте Default Skin Prefab (Obby) в SkinManager!");
            return;
        }
        player.ApplySkin(prefab);
        if (debug) Debug.Log($"[SkinManager] ApplySkinById: применён '{skinId}', prefab='{prefab.name}'");
    }
    
    /// <summary> Открыть окно покупки (Яны). </summary>
    public void PurchaseSkin(string productId)
    {
        YG2.BuyPayments(productId);
    }
    
    public GameObject GetPrefabForSkinId(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
        {
            if (defaultSkinPrefab != null) return defaultSkinPrefab;
            if (player != null && player.DefaultSkinTemplate != null) return player.DefaultSkinTemplate;
            return null;
        }
        if (skinId == SkinIdScarf) return scarfSkinPrefab;
        if (skinId == SkinIdNinja) return ninjaSkinPrefab;
        if (skinId == SkinIdGold) return goldSkinPrefab;
        return null;
    }
}
