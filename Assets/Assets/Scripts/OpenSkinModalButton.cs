using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вешается на кнопку SKIN (или на родителя кнопки): по нажатию открывает модальное окно скинов.
/// Если скрипт на том же объекте, что и Button, клик подписывается автоматически.
/// </summary>
public class OpenSkinModalButton : MonoBehaviour
{
    [SerializeField] private GameObject modalWindow;
    [SerializeField] private SkinModalController modalController;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private void Start()
    {
        // Автоподписка на кнопку, если OnClick в инспекторе не настроен
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OpenSkinModal);
            if (debug) Debug.Log("[OpenSkinModalButton] Подписка на кнопку добавлена автоматически.");
        }
    }
    
    public void OpenSkinModal()
    {
        if (debug) Debug.Log("[OpenSkinModalButton] OpenSkinModal() вызван.");
        if (modalController != null)
        {
            if (debug) Debug.Log("[OpenSkinModalButton] Открываю через Modal Controller.");
            modalController.Open();
            return;
        }
        if (modalWindow != null)
        {
            if (debug) Debug.Log("[OpenSkinModalButton] Открываю Modal Window напрямую.");
            modalWindow.SetActive(true);
            return;
        }
        Debug.LogWarning("[OpenSkinModalButton] Не назначены Modal Controller и Modal Window — укажите один из них в инспекторе на кнопке SKIN.");
    }
}
