using UnityEngine;
using YG;

/// <summary>
/// Обработка событий покупки скинов (YG2.onPurchaseSuccess/Failed). Консумирование — ConsumePurchasesYG.
/// Логика скинов (применение, текущий, купленные) — в SkinManager.
/// </summary>
[RequireComponent(typeof(ConsumePurchasesYG))]
public class SkinPurchaseHandler : MonoBehaviour
{
    public const string SkinIdScarf = "skin_scarf";
    public const string SkinIdNinja = "skin_ninja";
    public const string SkinIdGold = "skin_gold";
    
    private void OnEnable()
    {
        YG2.onPurchaseSuccess += OnPurchaseSuccess;
        YG2.onPurchaseFailed += OnPurchaseFailed;
    }
    
    private void OnDisable()
    {
        YG2.onPurchaseSuccess -= OnPurchaseSuccess;
        YG2.onPurchaseFailed -= OnPurchaseFailed;
    }
    
    private void Start()
    {
        // Консумирование в Start вызовет onPurchaseSuccess для необработанных покупок. Применяем сохранённый скин.
        SkinManager sm = SkinManager.Instance;
        if (GameStorage.Instance != null)
        {
            string current = GameStorage.Instance.GetCurrentSkinId();
            if (!string.IsNullOrEmpty(current))
                GameStorage.Instance.AddOwnedSkinId(current); // миграция: текущий скин считаем купленным
            if (sm != null)
                sm.ApplySkinById(current ?? "");
        }
        else if (sm != null)
            sm.ApplySkinById("");
    }
    
    private void OnPurchaseSuccess(string id)
    {
        if (!IsSkinId(id)) return;
        SkinManager sm = SkinManager.Instance;
        if (sm != null)
        {
            sm.AddOwnedSkinId(id);
            sm.SetCurrentSkinId(id);
        }
    }
    
    private void OnPurchaseFailed(string id)
    {
        Debug.Log("[SkinPurchaseHandler] Покупка не совершена: " + id);
    }
    
    private bool IsSkinId(string id)
    {
        return id == SkinIdScarf || id == SkinIdNinja || id == SkinIdGold;
    }
    
    /// <summary> Открыть окно покупки (вызывать с кнопки). </summary>
    public void PurchaseSkin(string productId)
    {
        YG2.BuyPayments(productId);
    }
    
    public void PurchaseScarf() => PurchaseSkin(SkinIdScarf);
    public void PurchaseNinja() => PurchaseSkin(SkinIdNinja);
    public void PurchaseGold() => PurchaseSkin(SkinIdGold);
}
