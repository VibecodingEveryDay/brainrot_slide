using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Вешается на UI-кнопку: по нажатию открывает модальное окно громкости.
/// Если на том же объекте есть Button, клик подписывается автоматически.
/// </summary>
public class OpenMusicModalButton : MonoBehaviour
{
    [SerializeField] private MusicModalController modalController;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OpenMusicModal);
            if (debug) Debug.Log("[OpenMusicModalButton] Подписка на кнопку добавлена автоматически.");
        }
    }
    
    public void OpenMusicModal()
    {
        if (debug) Debug.Log("[OpenMusicModalButton] OpenMusicModal() вызван.");
        if (modalController != null)
        {
            modalController.Open();
            return;
        }
        Debug.LogWarning("[OpenMusicModalButton] Не назначен Modal Controller — укажите MusicModalController в инспекторе.");
    }
}
