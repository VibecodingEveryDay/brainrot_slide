using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка остановки скольжения. Повесь на тот же GameObject, что и Button.
/// По нажатию: отключает slide в SlideManager и сообщает TeleportManager о телепортации на базу.
/// Подписка на onClick в OnEnable — в инспекторе OnClick настраивать не нужно.
/// </summary>
[RequireComponent(typeof(Button))]
public class StopSlideButton : MonoBehaviour
{
    [Tooltip("Писать в консоль при нажатии.")]
    [SerializeField] private bool debug = true;
    
    private Button _button;
    
    private void OnEnable()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnPressed);
    }
    
    private void OnDisable()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnPressed);
    }
    
    private void OnPressed()
    {
        if (debug)
            Debug.Log("[StopSlideButton] Нажатие.");
        
        // 1) Отключаем slide в SlideManager (игрок + скрыть кнопку)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var controller = player.GetComponent<ThirdPersonController>();
            if (controller != null)
                controller.ExitSlide();
        }
        SlideManager sm = SlideManager.Instance;
        if (sm == null)
            sm = FindFirstObjectByType<SlideManager>();
        if (sm != null)
            sm.ExitSlide();
        
        // 2) Сообщаем TeleportManager о телепортации на базу
        TeleportManager tm = TeleportManager.Instance;
        if (tm == null)
            tm = FindFirstObjectByType<TeleportManager>();
        if (tm != null)
        {
            if (debug)
                Debug.Log("[StopSlideButton] Вызов TeleportManager.TeleportToHouse().");
            tm.TeleportToHouse();
        }
        else if (debug)
            Debug.LogWarning("[StopSlideButton] TeleportManager не найден в сцене.");
    }
}
