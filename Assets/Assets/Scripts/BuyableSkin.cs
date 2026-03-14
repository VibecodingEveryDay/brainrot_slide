using UnityEngine;
using YG;

/// <summary>
/// Интерактивный меш скина в мире (ВИТРИНА): взаимодействие открывает покупку скина (Яны).
/// Текст подсказки: «Купить» / «Buy».
///
/// Важно: это НЕ игрок. Два разных объекта:
/// • BuyableSkin вешается на ВИТРИНУ (меш в мире, который стоит на месте). Аниматор витрины только для показа — всегда isGrounded=true, им никто не управляет.
/// • ThirdPersonController на ИГРОКЕ. После покупки скин применяется на игрока (ApplySkin) — это другой экземпляр модели, им управляет игрок (Speed, isGrounded с проверки земли и т.д.).
/// У витрины и у игрока могут быть префабы одной и той же модели, но это разные экземпляры и разные аниматоры.
/// </summary>
public class BuyableSkin : InteractableObject
{
    /// <summary> Какой скин продаёт этот меш — выбирается в инспекторе, чтобы не перепутать ID. </summary>
    public enum SkinProduct
    {
        Scarf = 0,
        Ninja = 1,
        Gold = 2
    }
    
    [Header("Покупка скина")]
    [Tooltip("Выберите скин, который продаёт этот меш (должен совпадать с мешем/витриной)")]
    [SerializeField] private SkinProduct skinProduct = SkinProduct.Scarf;
    
    [Tooltip("Обработчик покупок. Если не назначен — ищется в сцене")]
    [SerializeField] private SkinPurchaseHandler purchaseHandler;
    
    [Header("Аниматор витрины (только здесь isGrounded принудительно true, игрок не затронут)")]
    [Tooltip("Аниматор модели на витрине. Если не назначен — ищется у потомков. Параметр isGrounded всегда true только у этой модели.")]
    [SerializeField] private Animator displayAnimator;
    
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private string ProductId => GetProductId(skinProduct);
    
    private static string GetProductId(SkinProduct product)
    {
        switch (product)
        {
            case SkinProduct.Ninja: return SkinPurchaseHandler.SkinIdNinja;
            case SkinProduct.Gold:  return SkinPurchaseHandler.SkinIdGold;
            default:                return SkinPurchaseHandler.SkinIdScarf;
        }
    }
    
    protected override void Awake()
    {
        base.Awake(); // инициализация InteractableObject: радиус, камера, игрок — без этого взаимодействие не работает
        if (purchaseHandler == null)
            purchaseHandler = FindFirstObjectByType<SkinPurchaseHandler>();
        if (displayAnimator == null)
            displayAnimator = GetComponentInChildren<Animator>();
    }
    
    protected override void Update()
    {
        base.Update(); // проверка дистанции, создание UI, обработка E/тапа — без этого взаимодействие не работает
        // Только аниматор витрины: всегда isGrounded = true (стоячая поза), если параметр есть в контроллере.
        if (displayAnimator != null && displayAnimator.isActiveAndEnabled && AnimatorHasParameter(displayAnimator, IsGroundedHash))
            displayAnimator.SetBool(IsGroundedHash, true);
    }
    
    private static bool AnimatorHasParameter(Animator anim, int nameHash)
    {
        if (anim == null || anim.runtimeAnimatorController == null) return false;
        foreach (UnityEngine.AnimatorControllerParameter p in anim.parameters)
            if (p.nameHash == nameHash) return true;
        return false;
    }
    
    /// <summary>
    /// Текст кнопки взаимодействия: «Купить» / «Buy» по локализации.
    /// </summary>
    public override string GetInteractionButtonText()
    {
        string lang = GetCurrentLanguage();
        return (lang == "ru" || string.IsNullOrEmpty(lang)) ? "Купить" : "Buy";
    }
    
    protected override void ConfigureInteractionText(InteractionTextUpdater textUpdater)
    {
        if (textUpdater != null)
            textUpdater.SetCustomInteractionText("Купить", "Buy");
    }
    
    protected override void CompleteInteraction()
    {
        if (purchaseHandler != null)
            purchaseHandler.PurchaseSkin(ProductId);
        else if (SkinManager.Instance != null)
            SkinManager.Instance.PurchaseSkin(ProductId);
        base.CompleteInteraction();
    }
    
    private static string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
            return YG2.lang;
#endif
        return LocalizationManager.GetCurrentLanguage();
    }
}
