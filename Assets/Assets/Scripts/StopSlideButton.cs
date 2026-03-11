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

        // Всё поведение StopSlide централизовано в SlideManager.StopSlide:
        // - отключает slide у контроллера и скрывает кнопку
        // - сбрасывает активную зону и таймер скорости
        // - вызывает TeleportManager.TeleportToHouse()
        SlideManager sm = SlideManager.Instance;
        if (sm == null)
            sm = FindFirstObjectByType<SlideManager>();

        if (sm != null)
        {
            sm.StopSlide();
        }
        else if (debug)
        {
            Debug.LogWarning("[StopSlideButton] SlideManager не найден в сцене.");
        }
    }
}
