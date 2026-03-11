using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка Drop во время slide: активна только в активной зоне и когда у игрока в руках брейнрот.
/// При нажатии бросает брейнрота из рук на землю.
/// Важно: скрипт должен висеть на родителе кнопки (например, панели). В Root To Show/Hide укажи саму кнопку —
/// тогда при скрытии отключается только кнопка, а Update продолжает работать и может снова показать кнопку.
/// </summary>
public class SlideDropButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [Tooltip("Объект, который скрываем/показываем (обычно сама кнопка). Скрипт должен висеть на родителе этого объекта.")]
    [SerializeField] private GameObject rootToShowHide;

    private PlayerCarryController playerCarry;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (rootToShowHide == null && button != null)
            rootToShowHide = button.gameObject;
        if (rootToShowHide == gameObject)
            Debug.LogWarning("[SlideDropButton] Root To Show/Hide указывает на тот же объект, что и скрипт. Перенеси скрипт на родительский объект (панель), а Root To Show/Hide назначь на саму кнопку.", this);
    }

    private void OnEnable()
    {
        FindPlayerCarry();
        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }

    private void Update()
    {
        if (playerCarry == null)
            FindPlayerCarry();

        bool inActiveZone = SlideManager.Instance != null && SlideManager.Instance.IsInActiveZone();
        bool hasBrainrot = playerCarry != null && playerCarry.GetCurrentCarriedObject() != null;

        bool shouldBeVisible = inActiveZone && hasBrainrot;

        if (rootToShowHide == null)
            return;

        if (!shouldBeVisible)
        {
            if (button != null)
                button.interactable = false;
            if (rootToShowHide.activeSelf)
                rootToShowHide.SetActive(false);
            return;
        }

        if (!rootToShowHide.activeSelf)
            rootToShowHide.SetActive(true);

        if (button != null)
            button.interactable = true;
    }

    private void OnClick()
    {
        if (playerCarry == null)
            FindPlayerCarry();
        if (playerCarry == null)
            return;

        if (playerCarry.GetCurrentCarriedObject() != null)
        {
            playerCarry.DropObject();
        }
    }

    private void FindPlayerCarry()
    {
        if (playerCarry != null)
            return;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerCarry = player.GetComponent<PlayerCarryController>();
    }
}

