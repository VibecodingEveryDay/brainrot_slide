using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вешается на UI-кнопку RewardsButton: по нажатию открывает модальное окно наград.
/// </summary>
public class OpenRewardsModalButton : MonoBehaviour
{
    [SerializeField] private RewardsModalController modalController;

    [Header("Debug")]
    [SerializeField] private bool debug = false;

    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OpenRewardsModal);
            if (debug) Debug.Log("[OpenRewardsModalButton] Подписка на кнопку добавлена.");
        }
    }

    public void OpenRewardsModal()
    {
        if (debug) Debug.Log("[OpenRewardsModalButton] OpenRewardsModal() вызван.");
        if (modalController != null)
        {
            modalController.Open();
            return;
        }
        Debug.LogWarning("[OpenRewardsModalButton] Modal Controller не назначен.");
    }
}
