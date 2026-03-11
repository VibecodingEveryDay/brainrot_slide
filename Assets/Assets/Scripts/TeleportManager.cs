using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Управляет телепортацией (дом, зона старта/финиша). При столкновении игрока с мячом — UI проигрыша,
/// телепорт на базу, удаление брейнрота из рук и всех мячей. При 100% огня и нахождении в зоне StartFinish — то же.
/// </summary>
public class TeleportManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Префаб Canvas с Image для fade эффекта (черный экран)")]
    [SerializeField] private GameObject fadeCanvasPrefab;
    
    [Tooltip("Скорость fade эффекта (время затемнения/осветления в секундах)")]
    [SerializeField] private float fadeSpeed = 0.5f;
    
    [Header("References")]
    [Tooltip("Позиция дома для телепортации.")]
    [SerializeField] private Transform housePos;
    
    [Header("Lose Text (100% fire + player in tower zone)")]
    [Tooltip("Текст для сообщения о поражении (красный)")]
    [SerializeField] private TextMeshProUGUI loseText;
    
    [Tooltip("Текст поражения (русский)")]
    [SerializeField] private string loseTextRu = "Вы проиграли!";
    
    [Tooltip("Текст поражения (английский)")]
    [SerializeField] private string loseTextEn = "You lose!";
    
    [Header("Got Brainrot Text (player with brainrot exits tower zone)")]
    [Tooltip("Текст для сообщения «Вы получили {имя брейнрота}» (ярко-зелёный)")]
    [SerializeField] private TextMeshProUGUI gotBrainrotText;
    
    [Tooltip("Формат сообщения (русский), {0} = имя брейнрота")]
    [SerializeField] private string gotBrainrotFormatRu = "Вы получили {0}";
    
    [Tooltip("Формат сообщения (английский), {0} = имя брейнрота")]
    [SerializeField] private string gotBrainrotFormatEn = "You got {0}";
    
    [Header("Notification Animation")]
    [Tooltip("Через сколько секунд скрывать уведомление (0 = не скрывать автоматически)")]
    [SerializeField] private float notificationHideAfterSeconds = 3f;
    
    [Tooltip("Длительность fade-анимации скрытия текста (сек)")]
    [SerializeField] private float notificationFadeDuration = 0.5f;
    
    [Tooltip("Длительность одного пульса масштаба текста (сек)")]
    [SerializeField] private float notificationPulseDuration = 0.4f;
    
    [Tooltip("Максимальный масштаб при пульсе (1 = без увеличения)")]
    [SerializeField] private float notificationPulseMaxScale = 1.2f;
    
    [Header("Debug")]
    [Tooltip("Писать в консоль при вызове TeleportToHouse/TeleportToPosition и причинах отмены (housePos, isFading, игрок).")]
    [SerializeField] private bool debug;
    
    private GameObject fadeCanvasInstance;
    private Image fadeImage;
    private bool isFading = false;
    
    // Позиция игрока в лобби (для возврата)
    private Vector3 lobbyPlayerPosition;
    private Quaternion lobbyPlayerRotation;
    
    // Ссылка на игрока
    private Transform playerTransform;
    private ThirdPersonController playerController;
    
    private bool teleportingDueToLose = false;
    
    private Coroutine notificationHideCoroutine;
    private Coroutine notificationPulseCoroutine;
    private Vector3 cachedLoseTextBaseScale = Vector3.one;
    private Vector3 cachedGotBrainrotTextBaseScale = Vector3.one;
    
    private static TeleportManager instance;
    
    // Корутина для поэтапного респавна брейнротов, чтобы избежать микрофризов
    private Coroutine brainrotRespawnCoroutine;
    
    // Корутина отложенного телепорта на базу (после удара мячом)
    private Coroutine teleportToHouseAfterDelayCoroutine;
    
    public static TeleportManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TeleportManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Создаем fade canvas если префаб не назначен
        if (fadeCanvasPrefab == null)
        {
            CreateFadeCanvas();
        }
    }
    
    private void Start()
    {
        FindPlayer();
        
        if (loseText != null)
            loseText.gameObject.SetActive(false);
        if (gotBrainrotText != null)
            gotBrainrotText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Вызывается, когда игрок в slide-настройке нажал GetIt с брейнротом в руках:
    /// удаляет текущий экземпляр брейнрота и после телепортации создаёт в руках у игрока
    /// новый экземпляр такого же брейнрота (по имени), с сохранением ключевых параметров.
    /// </summary>
    public void OnPlayerGotBrainrotViaSlide(BrainrotObject sourceBrainrot)
    {
        if (sourceBrainrot == null)
            return;

        // Имя для UI и поиска префаба
        string name = sourceBrainrot.GetObjectName();

        // Показываем текст «Вы получили {имя брейнрота}»
        if (!string.IsNullOrEmpty(name) && gotBrainrotText != null)
        {
            string format = IsRussianLanguage() ? gotBrainrotFormatRu : gotBrainrotFormatEn;
            gotBrainrotText.text = string.Format(format, name);
            gotBrainrotText.color = new Color(0.2f, 1f, 0.2f);
            gotBrainrotText.gameObject.SetActive(true);
            cachedGotBrainrotTextBaseScale = gotBrainrotText.transform.localScale;
            SetNotificationAlpha(gotBrainrotText, 1f);
            StartNotificationPulse(gotBrainrotText.transform, cachedGotBrainrotTextBaseScale);
            StartNotificationHideAfterDelay(gotBrainrotText, cachedGotBrainrotTextBaseScale);
        }

        // Любой «победный» телепорт тоже должен выйти из активной зоны slide.
        var slideManager = SlideManager.Instance;
        if (slideManager != null)
            slideManager.SetInActiveZone(false);

        // Мгновенно телепортируем игрока домой (БЕЗ fade / ожиданий).
        if (housePos == null)
        {
            Debug.LogError("[TeleportManager] HousePos не назначен! Невозможно телепортировать игрока по GetIt.");
            return;
        }

        // Убеждаемся, что есть ссылка на игрока
        if (playerTransform == null)
            FindPlayer();
        if (playerTransform == null)
        {
            Debug.LogError("[TeleportManager] Игрок не найден при OnPlayerGotBrainrotViaSlide.");
            return;
        }

        // Отключаем CharacterController перед перемещением
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }

        // Мгновенный телепорт игрока в дом
        playerTransform.position = housePos.position;
        playerTransform.rotation = housePos.rotation;

        // Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
        }

        // Сбрасываем камеру
        ResetCameraAfterTeleport();

        // Контроллер переноски — нужен до уничтожения источника, чтобы освободить руки.
        PlayerCarryController carryController = playerTransform.GetComponent<PlayerCarryController>();
        if (carryController == null)
            carryController = FindFirstObjectByType<PlayerCarryController>();
        if (carryController == null)
        {
            Debug.LogError("[TeleportManager] PlayerCarryController не найден при GetIt.");
            return;
        }

        // Сохраняем параметры исходного брейнрота
        int level = sourceBrainrot.GetLevel();
        string rarity = sourceBrainrot.GetRarity();
        long baseIncome = sourceBrainrot.GetBaseIncome();

        // Освобождаем руки до уничтожения: иначе currentCarriedObject указывает на уничтоженный объект и CanCarry() остаётся false.
        carryController.DropObject();
        // Удаляем исходный экземпляр (slide-версию) из сцены
        Destroy(sourceBrainrot.gameObject);

        // Ищем префаб брейнрота по имени (те же ресурсы, что и лобби/placement)
        GameObject prefab = FindBrainrotPrefabByName(name);
        if (prefab == null)
        {
            Debug.LogError($"[TeleportManager] Не удалось найти префаб брейнрота с именем '{name}' для GetIt.");
            return;
        }

        // Создаём новый экземпляр
        GameObject instance = Instantiate(prefab);
        BrainrotObject newBrainrot = instance.GetComponent<BrainrotObject>();
        if (newBrainrot == null)
        {
            Debug.LogError("[TeleportManager] У префаба брейнрота нет компонента BrainrotObject.");
            Destroy(instance);
            return;
        }

        // Восстанавливаем параметры (уровень, редкость, базовый доход)
        newBrainrot.SetLevel(level);
        if (!string.IsNullOrEmpty(rarity))
            newBrainrot.SetRarity(rarity);
        if (baseIncome > 0)
            newBrainrot.SetBaseIncome(baseIncome);

        // Чтобы CarryObject не отклонил объект (проверка IsUnfought)
        newBrainrot.SetUnfought(false);
        newBrainrot.gameObject.SetActive(true);
        foreach (Transform child in newBrainrot.transform)
        {
            if (child != null)
                child.gameObject.SetActive(true);
        }
        Renderer[] renderers = newBrainrot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.gameObject.SetActive(true);
            }
        }

        // Начальная позиция «в руках», чтобы не мелькал в (0,0,0) до первого LateUpdate
        Vector3 carryOffset = new Vector3(
            newBrainrot.GetCarryOffsetX(),
            newBrainrot.GetCarryOffsetY(),
            newBrainrot.GetCarryOffsetZ()
        );
        newBrainrot.transform.position = playerTransform.position +
            playerTransform.forward * carryOffset.z +
            playerTransform.right * carryOffset.x +
            playerTransform.up * carryOffset.y;
        // Сразу помещаем в руки через TakeBy (явно передаём carryController), чтобы объект вёл себя как «в руках» и LateUpdate тянул его за игроком
        newBrainrot.TakeBy(carryController);

        // После телепорта и взятия в руки обновляем видимость брейнротов относительно игрока,
        // чтобы этот брейнрот гарантированно был показан.
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
        }

        // После победного телепорта по GetIt тоже респавним брейнроты в плоскостях спавна (поэтапно).
        StartBrainrotRespawnAsync();
    }
    
    /// <summary>
    /// Запускает поэтапный респавн всех брейнротов в spawn plane по одному спавнеру за кадр.
    /// Вызывать при любой телепортации домой (поражение, победа, Stop).
    /// </summary>
    public void StartBrainrotRespawnAsync()
    {
        if (!gameObject.activeInHierarchy)
        {
            // На всякий случай активируем объект, чтобы корутина могла стартовать
            gameObject.SetActive(true);
        }
        
        if (brainrotRespawnCoroutine != null)
        {
            StopCoroutine(brainrotRespawnCoroutine);
            brainrotRespawnCoroutine = null;
        }
        
        brainrotRespawnCoroutine = StartCoroutine(RespawnAllBrainrotsGradually());
    }
    
    /// <summary>
    /// Корутина: респавнит брейнротов по одному спавнеру за кадр, уменьшая пиковую нагрузку.
    /// </summary>
    private IEnumerator RespawnAllBrainrotsGradually()
    {
        PlaneBrSpawner[] spawners = FindObjectsByType<PlaneBrSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].RespawnAll();
            }
            
            // Ждём следующий кадр после обработки каждого спавнера,
            // чтобы распределить Instantiate/Destroy по времени и избежать микрофриза.
            yield return null;
        }
        
        // Обновляем кэш направляющих после завершения респавна
        Guide.InvalidateAllGuidesCache();
        
        brainrotRespawnCoroutine = null;
    }

    /// <summary>
    /// Единая точка проигрыша от любых препятствий (RedCone, охранник и т.п.),
    /// привязанная к синглтону TeleportManager.
    /// Останавливает slide, помечает телепортацию как поражение, удаляет брейнрот из рук и
    /// запускает телепорт домой с текстом «Вы проиграли» и респавном брейнротов.
    /// </summary>
    public void HandleLoseFromObstacle(float teleportDelaySeconds)
    {
        // Телепорт из-за поражения: блокируем повторный вход в slide.
        teleportingDueToLose = true;

        // Сразу выключаем режим slide у игрока (ThirdPersonController.isSliding), иначе после телепорта он продолжит скольжение.
        if (playerTransform == null)
            FindPlayer();
        if (playerTransform != null)
        {
            ThirdPersonController controller = playerTransform.GetComponent<ThirdPersonController>();
            if (controller != null)
                controller.ExitSlide();
        }

        // Выход из активной зоны и остановка slide в SlideManager (кнопка, VFX, лимит времени).
        SlideManager sm = SlideManager.Instance;
        if (sm != null)
        {
            sm.SetInActiveZone(false);
            sm.ExitSlide();
        }

        // Убираем брейнрот из рук.
        RemoveCarriedBrainrot();

        // Стартуем стандартную телепортацию домой с текстом поражения и респавном.
        TeleportPlayerToHouseAfterDelay(Mathf.Max(0f, teleportDelaySeconds));
    }
    
    /// <summary>
    /// Показывает текст поражения (красный, с пульсом и автоскрытием). Публичный для вызова из Ball и др.
    /// </summary>
    public void ShowLoseText()
    {
        if (loseText == null) return;
        loseText.text = IsRussianLanguage() ? loseTextRu : loseTextEn;
        loseText.color = Color.red;
        loseText.gameObject.SetActive(true);
        cachedLoseTextBaseScale = loseText.transform.localScale;
        SetNotificationAlpha(loseText, 1f);
        StartNotificationPulse(loseText.transform, cachedLoseTextBaseScale);
        StartNotificationHideAfterDelay(loseText, cachedLoseTextBaseScale);
    }
    
    private void StartNotificationPulse(Transform textTransform, Vector3 baseScale)
    {
        if (textTransform == null || notificationPulseDuration <= 0f) return;
        if (notificationPulseCoroutine != null)
            StopCoroutine(notificationPulseCoroutine);
        notificationPulseCoroutine = StartCoroutine(PulseScaleOnceCoroutine(textTransform, notificationPulseDuration, notificationPulseMaxScale, baseScale));
    }
    
    private IEnumerator PulseScaleOnceCoroutine(Transform textTransform, float duration, float maxScale, Vector3 baseScale)
    {
        if (textTransform == null) yield break;
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(1f, maxScale, t);
            textTransform.localScale = baseScale * s;
            yield return null;
        }
        textTransform.localScale = baseScale * maxScale;
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(maxScale, 1f, t);
            textTransform.localScale = baseScale * s;
            yield return null;
        }
        textTransform.localScale = baseScale;
        notificationPulseCoroutine = null;
    }
    
    private void SetNotificationAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text == null) return;
        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }
    
    private void StartNotificationHideAfterDelay(TextMeshProUGUI text, Vector3 baseScale)
    {
        if (text == null || notificationHideAfterSeconds <= 0f) return;
        if (notificationHideCoroutine != null)
            StopCoroutine(notificationHideCoroutine);
        notificationHideCoroutine = StartCoroutine(HideNotificationAfterDelayCoroutine(text, baseScale));
    }
    
    private IEnumerator HideNotificationAfterDelayCoroutine(TextMeshProUGUI text, Vector3 baseScale)
    {
        if (text == null) yield break;
        yield return new WaitForSeconds(notificationHideAfterSeconds);
        if (text == null) yield break;
        if (notificationFadeDuration <= 0f)
        {
            text.transform.localScale = baseScale;
            text.gameObject.SetActive(false);
            notificationHideCoroutine = null;
            yield break;
        }
        float elapsed = 0f;
        Color startColor = text.color;
        while (elapsed < notificationFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / notificationFadeDuration));
            Color c = startColor;
            c.a = alpha;
            text.color = c;
            yield return null;
        }
        SetNotificationAlpha(text, 0f);
        text.transform.localScale = baseScale;
        text.gameObject.SetActive(false);
        notificationHideCoroutine = null;
    }
    
    /// <summary>
    /// Удаляет брейнрот из рук игрока (например, при ударе мячом). Публичный для вызова из Ball.
    /// </summary>
    public void RemoveCarriedBrainrot()
    {
        if (playerTransform == null) return;
        PlayerCarryController carry = playerTransform.GetComponent<PlayerCarryController>();
        if (carry == null)
            carry = FindFirstObjectByType<PlayerCarryController>();
        if (carry == null) return;
        
        BrainrotObject carried = carry.GetCurrentCarriedObject();
        if (carried != null)
        {
            carry.DropObject();
            if (carried.gameObject != null)
                Destroy(carried.gameObject);
        }
    }

    /// <summary>
    /// Идёт ли сейчас телепортация из-за поражения (мяч, огонь, удар охранника).
    /// Можно использовать, чтобы блокировать вход в slide и другие действия.
    /// </summary>
    public bool IsTeleportingDueToLose()
    {
        return teleportingDueToLose;
    }
    
    private static bool IsRussianLanguage()
    {
        string lang = LocalizationManager.GetCurrentLanguage();
        return lang == "ru" || string.IsNullOrEmpty(lang);
    }
    
    /// <summary>
    /// Находит игрока в сцене
    /// </summary>
    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerController = player.GetComponent<ThirdPersonController>();
            }
            else
            {
                ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
                if (controller != null)
                {
                    playerTransform = controller.transform;
                    playerController = controller;
                }
            }
        }
    }
    
    /// <summary>
    /// Создает fade canvas вручную если префаб не назначен
    /// </summary>
    private void CreateFadeCanvas()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Высокий приоритет
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        
        Image image = imageObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f); // Прозрачный
        
        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        fadeCanvasInstance = canvasObj;
        fadeImage = image;
        
        // Скрываем canvas по умолчанию
        canvasObj.SetActive(false);
    }

    /// <summary>
    /// Находит префаб брейнрота по его отображаемому имени, используя ту же логику, что и PlacementPanel.
    /// </summary>
    private GameObject FindBrainrotPrefabByName(string brainrotName)
    {
        if (string.IsNullOrEmpty(brainrotName))
            return null;

#if UNITY_EDITOR
        string folderPath = "Assets/Assets/Resources/game/Brainrots";
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
            if (brainrotObject == null) continue;

            string prefabName = brainrotObject.GetObjectName();
            if (prefabName == brainrotName)
                return prefab;
        }
        return null;
#else
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("game/Brainrots");
        for (int i = 0; i < allPrefabs.Length; i++)
        {
            GameObject prefab = allPrefabs[i];
            if (prefab == null) continue;

            BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
            if (brainrotObject == null) continue;

            string prefabName = brainrotObject.GetObjectName();
            if (prefabName == brainrotName)
                return prefab;
        }
        return null;
#endif
    }
    
    /// <summary>
    /// Телепортирует игрока в зону сражения (ОТКЛЮЧЕНО - BattleZone удалена)
    /// </summary>
    public void TeleportToBattleZone(BrainrotObject brainrotObject)
    {
        Debug.LogWarning("[TeleportManager] TeleportToBattleZone отключен - BattleZone удалена из проекта");
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию
    /// </summary>
    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        if (isFading)
        {
            if (debug)
                Debug.Log("[TeleportManager] TeleportToPosition: пропуск — уже идёт fade (isFading=true).");
            Debug.LogWarning("[TeleportManager] Телепортация уже выполняется, пропускаем");
            return;
        }
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                if (debug)
                    Debug.Log("[TeleportManager] TeleportToPosition: игрок не найден после FindPlayer().");
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        if (debug)
            Debug.Log($"[TeleportManager] TeleportToPosition: запуск корутины, позиция={position}, игрок={playerTransform.name}");
        StartCoroutine(TeleportToPositionCoroutine(position, rotation));
    }
    
    /// <summary>
    /// Телепортирует игрока в дом (после победы над боссом)
    /// </summary>
    public void TeleportToHouse()
    {
        if (debug)
            Debug.Log("[TeleportManager] TeleportToHouse вызван.");
        if (housePos == null)
        {
            if (debug)
                Debug.Log("[TeleportManager] TeleportToHouse: отмена — House Pos не назначен в инспекторе.");
            Debug.LogError("[TeleportManager] HousePos не назначен! Установите Transform в инспекторе.");
            return;
        }
        if (debug)
            Debug.Log($"[TeleportManager] TeleportToHouse: вызов TeleportToPosition(housePos), позиция={housePos.position}");
        TeleportToPosition(housePos.position, housePos.rotation);
    }
    
    /// <summary>
    /// Через заданное количество секунд телепортирует игрока на базу (например, после удара мячом).
    /// </summary>
    public void TeleportPlayerToHouseAfterDelay(float seconds)
    {
        // Считаем, что с этого момента игрок уже «проиграл» и ждёт телепорта.
        // Это нужно, чтобы, например, блокировать вход в slide сразу после удара.
        teleportingDueToLose = true;

        // Телепорт по поражению также выводит игрока из активной зоны slide.
        var slideManager = SlideManager.Instance;
        if (slideManager != null)
            slideManager.SetInActiveZone(false);

        if (teleportToHouseAfterDelayCoroutine != null)
            StopCoroutine(teleportToHouseAfterDelayCoroutine);
        teleportToHouseAfterDelayCoroutine = StartCoroutine(TeleportToHouseAfterDelayCoroutine(seconds));
    }
    
    private IEnumerator TeleportToHouseAfterDelayCoroutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        teleportToHouseAfterDelayCoroutine = null;

        // Перед самым телепортом:
        // 1) показываем текст поражения,
        // 2) телепортируем в дом.
        // (брейнрот уже был удалён в момент удара охранника).
        ShowLoseText();
        TeleportToHouse();
        StartBrainrotRespawnAsync();
    }
    
    /// <summary>
    /// Телепортирует игрока в дом и помещает брейнрота в руки после телепортации
    /// </summary>
    public void TeleportToHouseWithBrainrot(BrainrotObject brainrot)
    {
        if (housePos == null)
        {
            Debug.LogError("[TeleportManager] HousePos не назначен!");
            return;
        }
        
        StartCoroutine(TeleportToHouseWithBrainrotCoroutine(brainrot));
    }
    
    private IEnumerator TeleportToHouseWithBrainrotCoroutine(BrainrotObject brainrot)
    {
        isFading = true;
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // Отключаем CharacterController перед телепортацией
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Телепортируем игрока в дом
        playerTransform.position = housePos.position;
        playerTransform.rotation = housePos.rotation;
        
        // Ждём кадр чтобы позиция применилась
        yield return null;
        
        // Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
        }
        
        // Сбрасываем камеру
        ResetCameraAfterTeleport();
        
        // Обновляем видимость брейнротов
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
        }
        
        // ПОСЛЕ телепортации помещаем брейнрота в руки
        if (brainrot != null)
        {
            PlayerCarryController carryController = playerTransform.GetComponent<PlayerCarryController>();
            if (carryController == null)
            {
                carryController = FindFirstObjectByType<PlayerCarryController>();
            }
            
            if (carryController != null && carryController.CanCarry())
            {
                // Активируем брейнрота
                brainrot.SetUnfought(false);
                brainrot.gameObject.SetActive(true);
                
                // Активируем все дочерние объекты и рендереры
                foreach (Transform child in brainrot.transform)
                {
                    if (child != null)
                    {
                        child.gameObject.SetActive(true);
                    }
                }
                
                Renderer[] renderers = brainrot.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                        renderer.gameObject.SetActive(true);
                    }
                }
                
                // Устанавливаем позицию брейнрота к игроку
                Vector3 carryOffset = new Vector3(
                    brainrot.GetCarryOffsetX(),
                    brainrot.GetCarryOffsetY(),
                    brainrot.GetCarryOffsetZ()
                );
                brainrot.transform.position = playerTransform.position + 
                    playerTransform.forward * carryOffset.z + 
                    playerTransform.right * carryOffset.x + 
                    playerTransform.up * carryOffset.y;
                
                // Берём брейнрота в руки
                brainrot.Take();
            }
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    /// <summary>
    /// Телепортирует игрока обратно в дом (после поражения)
    /// </summary>
    public void TeleportToLobby()
    {
        // Останавливаем все текущие корутины телепортации
        StopAllCoroutines();
        isFading = false;
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        StartCoroutine(TeleportToLobbyCoroutine());
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию (корутина). Сначала мгновенно перемещает игрока (чтобы он сразу ушёл с платформы и не входил в slide снова), затем fade.
    /// </summary>
    private IEnumerator TeleportToPositionCoroutine(Vector3 position, Quaternion rotation)
    {
        isFading = true;
        
        if (playerTransform == null)
            FindPlayer();
        
        // Сразу перемещаем игрока, чтобы он вышел из зоны slide до следующего FixedUpdate
        if (playerTransform != null)
        {
            CharacterController characterController = playerTransform.GetComponent<CharacterController>();
            bool wasControllerEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
                characterController.enabled = false;
            playerTransform.position = position;
            playerTransform.rotation = rotation;
            if (characterController != null && wasControllerEnabled)
                characterController.enabled = true;
        }
        
        yield return null;
        ResetCameraAfterTeleport();
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
            distanceHider.ForceRefresh();
        
        yield return StartCoroutine(FadeOut());
        yield return StartCoroutine(FadeIn());
        
        if (teleportingDueToLose)
            teleportingDueToLose = false;
        isFading = false;
    }
    
    private IEnumerator TeleportToLobbyCoroutine()
    {
        isFading = true;
        
        // Ждём 2 секунды перед телепортацией (чтобы игрок увидел результат поражения)
        yield return new WaitForSeconds(2f);
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // ВАЖНО: Отключаем CharacterController перед телепортацией
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Телепортируем игрока в дом (housePos), а не в лобби
        if (housePos != null)
        {
            playerTransform.position = housePos.position;
            playerTransform.rotation = housePos.rotation;
        }
        else
        {
            // Fallback на лобби если housePos не задан
            playerTransform.position = lobbyPlayerPosition;
            playerTransform.rotation = lobbyPlayerRotation;
        }
        
        // Ждём кадр чтобы позиция применилась
        yield return null;
        
        // Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
        }
        
        // Сбрасываем камеру после телепортации
        ResetCameraAfterTeleport();
        
        // ВАЖНО: Обновляем видимость брейнротов после телепортации
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    /// <summary>
    /// Затемняет экран
    /// </summary>
    private IEnumerator FadeOut()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeSpeed);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Устанавливаем полностью черный экран
        color.a = 1f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
    }
    
    /// <summary>
    /// Осветляет экран
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / fadeSpeed));
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Полностью прозрачный черный
        color.a = 0f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
        
        // Скрываем canvas после завершения
        if (fadeCanvasInstance != null)
        {
            fadeCanvasInstance.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// Сбрасывает камеру после телепортации, чтобы предотвратить слишком сильное приближение
    /// </summary>
    private void ResetCameraAfterTeleport()
    {
        // Находим камеру
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera != null)
        {
            // Сначала обновляем ThirdPersonCamera, чтобы камера была на правильной позиции
            ThirdPersonCamera thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera != null)
            {
                // Принудительно обновляем позицию камеры
                // Это гарантирует, что камера будет на правильном расстоянии
                thirdPersonCamera.ForceUpdateCameraPosition();
            }
            
            // Затем сбрасываем CameraCollisionHandler, чтобы он пересчитал расстояние
            CameraCollisionHandler collisionHandler = mainCamera.GetComponent<CameraCollisionHandler>();
            if (collisionHandler != null)
            {
                // Обновляем цель камеры, если нужно
                if (playerTransform != null)
                {
                    Transform cameraTarget = playerTransform.Find("CameraTarget");
                    if (cameraTarget != null)
                    {
                        collisionHandler.SetTarget(cameraTarget);
                    }
                }
                
                // Принудительно сбрасываем камеру после телепортации
                // Это полностью пересчитывает направление и расстояние, предотвращая быстрое приближение
                collisionHandler.ForceResetAfterTeleport();
            }
        }
    }
}
