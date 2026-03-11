using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Кнопка GetIt во время slide:
/// - активируется только в активной зоне и когда у игрока в руках брейнрот;
/// - сначала блокирована и показывает анимацию Lock (fillAmount 1→0 за 4 секунды);
/// - после зарядки становится доступной и по нажатию телепортирует игрока домой с текстом «Вы получили брейнрота».
/// Важно: скрипт должен висеть на родителе кнопки. В Root To Show/Hide укажи объект кнопки (или панель с кнопкой и Lock).
/// </summary>
public class SlideGetItButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image lockImage;
    [SerializeField] private float chargeDuration = 4f;

    [Tooltip("Объект, который скрываем/показываем (кнопка или панель с кнопкой и Lock). Скрипт должен висеть на родителе.")]
    [SerializeField] private GameObject rootToShowHide;

    private PlayerCarryController playerCarry;
    private Coroutine chargeCoroutine;
    private bool requirementsMet;
    private bool charged;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (rootToShowHide == null && button != null)
            rootToShowHide = button.gameObject;
        if (rootToShowHide == gameObject)
            Debug.LogWarning("[SlideGetItButton] Root To Show/Hide указывает на тот же объект, что и скрипт. Перенеси скрипт на родительский объект, а Root To Show/Hide назначь на кнопку (или панель с кнопкой и Lock).", this);
    }

    private void OnEnable()
    {
        FindPlayerCarry();
        if (button != null)
            button.onClick.AddListener(OnClick);
        ResetState();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }
    }

    private void Update()
    {
        if (playerCarry == null)
            FindPlayerCarry();

        bool inActiveZone = SlideManager.Instance != null && SlideManager.Instance.IsInActiveZone();
        bool hasBrainrot = playerCarry != null && playerCarry.GetCurrentCarriedObject() != null;

        bool newRequirementsMet = inActiveZone && hasBrainrot;

        if (!newRequirementsMet)
        {
            if (requirementsMet || charged)
                ResetState();
            requirementsMet = false;
            if (button != null)
                button.interactable = false;
            if (rootToShowHide != null && rootToShowHide.activeSelf)
                rootToShowHide.SetActive(false);
            return;
        }

        // Показываем кнопку (объект rootToShowHide).
        if (rootToShowHide != null && !rootToShowHide.activeSelf)
            rootToShowHide.SetActive(true);

        // Условия начали выполняться
        if (!requirementsMet)
        {
            requirementsMet = true;
            StartCharge();
        }

        // Пока не заряжено — кнопка недоступна.
        if (button != null)
            button.interactable = charged;
    }

    private void StartCharge()
    {
        charged = false;
        if (lockImage != null)
        {
            lockImage.fillAmount = 1f;
            lockImage.enabled = true;
        }
        if (chargeCoroutine != null)
            StopCoroutine(chargeCoroutine);
        if (chargeDuration > 0f)
            chargeCoroutine = StartCoroutine(ChargeCoroutine());
        else
            FinishCharge();
        if (button != null)
            button.interactable = false;
    }

    private IEnumerator ChargeCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < chargeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / chargeDuration);
            if (lockImage != null)
                lockImage.fillAmount = 1f - t;
            yield return null;
        }
        chargeCoroutine = null;
        FinishCharge();
    }

    private void FinishCharge()
    {
        charged = true;
        if (lockImage != null)
            lockImage.fillAmount = 0f;
        if (button != null)
            button.interactable = true;
    }

    private void ResetState()
    {
        if (chargeCoroutine != null)
        {
            StopCoroutine(chargeCoroutine);
            chargeCoroutine = null;
        }
        charged = false;
        if (lockImage != null)
        {
            lockImage.fillAmount = 1f;
            lockImage.enabled = true;
        }
        if (button != null)
            button.interactable = false;
    }

    private void OnClick()
    {
        if (!charged)
            return;

        if (playerCarry == null)
            FindPlayerCarry();
        if (playerCarry == null)
            return;

        BrainrotObject carried = playerCarry.GetCurrentCarriedObject();
        if (carried == null)
            return;

        TeleportManager tm = TeleportManager.Instance;
        if (tm == null)
            tm = FindFirstObjectByType<TeleportManager>();
        if (tm == null)
            return;

        // Телепортируем игрока домой с брейнротом и текстом «Вы получили брейнрота».
        tm.OnPlayerGotBrainrotViaSlide(carried);

        // Активная зона сбрасывается внутри TeleportManager, но на всякий случай можем продублировать.
        var slideManager = SlideManager.Instance;
        if (slideManager != null)
            slideManager.SetInActiveZone(false);
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

