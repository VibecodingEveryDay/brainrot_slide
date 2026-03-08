using UnityEngine;
using System.Collections;
using System.Reflection;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if EnvirData_yg
using YG;
#endif

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float baseMoveSpeed = 5f; // Базовая скорость при уровне 0
    [SerializeField] private float speedLevelScaler = 1f; // Множитель для расчета скорости на основе уровня
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float gravity = -9.81f;
    
    [Header("Speed Level Settings")]
    [Tooltip("Использовать уровень скорости из GameStorage (если true, moveSpeed будет вычисляться на основе уровня)")]
    [SerializeField] private bool useSpeedLevel = true;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения о скорости")]
    [SerializeField] private bool debugSpeed = false;
    [Tooltip("Включить визуальную отладку падения (grounded, velocity.y, таймер и флаги падения)")]
    [SerializeField] private bool fallDebug = false;
    
    private float moveSpeed; // Вычисляемая скорость (может изменяться на основе уровня)
    
    [Header("References")]
    [SerializeField] private Transform modelTransform; // Дочерний объект с моделью
    [SerializeField] private Animator animator;
    [SerializeField] private ThirdPersonCamera cameraController;
    
    [Header("Animation")]
    [Tooltip("Скорость воспроизведения анимации бега (вычисляется по уровню скорости: 10 лвл = 1.6, макс лвл = 2.5)")]
    [SerializeField] private float runAnimationSpeed = 1.6f;
    
    private const int RunAnimSpeedMinLevel = 10;   // начальный уровень (как нулевой)
    private const int RunAnimSpeedMaxLevel = 60;   // последний уровень (должен совпадать с ShopSpeedManager.MAX_LEVEL)
    private const float RunAnimSpeedAtMinLevel = 1.6f;
    private const float RunAnimSpeedAtMaxLevel = 2.5f;
    
    [Header("Ground Check (оптимизировано для ступенек и прыжков)")]
    [Tooltip("Длина проверки вниз от ног (лучи + SphereCast).")]
    [SerializeField] private float groundCheckLength = 0.7f;
    [Tooltip("Минимальный normal.y поверхности, чтобы считать её полом (0.35 ≈ ступеньки).")]
    [SerializeField] private float minGroundNormalY = 0.35f;
    [Tooltip("Радиус SphereCast и смещение дополнительных лучей под капсулой.")]
    [SerializeField] private float groundCheckRadius = 0.2f;
    [Tooltip("Буфер времени (сек): после потери контакта ещё считаем на земле (гистерезис для ступенек).")]
    [SerializeField] private float groundedBufferTime = 0.25f;
    [Tooltip("Сколько кадров подряд без контакта — тогда переходим в «полёт».")]
    [SerializeField] private int groundedFramesRequired = 4;
    
    [Header("Jump Rotation")]
    [SerializeField] private float jumpRotationAngle = 10f; // Угол поворота модели при прыжке
    
    [Header("Knockback (отталкивание мячом)")]
    [Tooltip("Скорость затухания отталкивания (чем больше — тем быстрее игрок останавливается после толчка)")]
    [SerializeField] private float knockbackDecay = 5f;
    
    [Header("Falling Speed Boost")]
    [Tooltip("Через сколько миллисекунд непрерывного нахождения в воздухе считать состояние падением (по умолчанию 700 мс).")]
    [SerializeField] private float fallDetectionTimeMs = 700f;
    [Tooltip("На сколько процентов увеличить горизонтальную скорость во время падения (к концу ускорения). Например, 30 = +30%.")]
    [SerializeField] private float fallSpeedBoostPercent = 30f;
    [Tooltip("За сколько секунд при непрерывном падении скорость дорастёт до полного буста (по умолчанию 3 секунды).")]
    [SerializeField] private float fallSpeedBoostDuration = 3f;
    
    
    private CharacterController characterController;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isActuallyGrounded; // Реальное состояние контакта с землёй (без буфера)
    private bool isGroundedForAnimation; // Более «быстрая» приземлённость только для анимации
    private float lastGroundedTime; // Время последнего контакта с землёй
    private int groundedFalseFrameCount; // Подряд кадров без контакта (для гистерезиса)
    private float currentSpeed;
    private bool jumpRequested = false; // Запрос на прыжок от кнопки
    private float jumpRequestTime = -1f; // Время запроса прыжка (для обработки с небольшой задержкой)
    private const float jumpRequestWindow = 0.3f; // Окно времени для обработки запроса прыжка (в секундах) - увеличено для надежности
    private bool isJumping = false; // Флаг прыжка для поворота модели
    private Quaternion savedModelRotation; // Сохраненный поворот модели перед прыжком
    
    // Падение
    private bool isFalling = false;
    private float fallTime = 0f;
    private float ungroundedTimeMs = 0f;
    
    // Ввод от джойстика (для мобильных устройств)
    private Vector2 joystickInput = Vector2.zero;
    
    // GameStorage для получения уровня скорости
    private GameStorage gameStorage;
    
    // Флаг готовности игры (блокирует управление до инициализации GameReady)
    private bool isGameReady = false;
    // Запущен ли таймаут ожидания GameReady
    private bool gameReadyTimeoutStarted = false;
    
    // Отталкивание (мяч и т.п.)
    private Vector3 knockbackVelocity = Vector3.zero;
    
    // Лестница
    private bool isOnLadder = false;
    private Ladder currentLadder = null;
    private float ladderAnimatorSpeed = 1f; // Для остановки анимации лестницы
    
    // Скольжение (параметры из SlideManager)
    private bool isSliding = false;
    private float currentSlideModelTurnY = 0f; // текущий поворот модели влево/вправо при A/D (плавный)
    
    // Параметры аниматора
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsTakingHash = Animator.StringToHash("IsTaking");
    private static readonly int IsJabHash = Animator.StringToHash("IsJab");
    private static readonly int IsUpperCutJabHash = Animator.StringToHash("IsUpperCutJab");
    private static readonly int IsStrongBeat1Hash = Animator.StringToHash("IsStrongBeat1");
    private static readonly int IsLadderHash = Animator.StringToHash("IsLadder");
    
    private int wallBallLayer = -1; // кэш LayerMask.NameToLayer("WallBall")
    private int groundCheckLayerMask = ~0;
    private readonly Vector3[] groundCheckOrigins = new Vector3[5]; // без аллокации в HandleGroundCheck
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        
        // Автоматически найти дочерний объект с моделью, если не назначен
        if (modelTransform == null)
        {
            // Ищем дочерний объект с Animator
            Animator childAnimator = GetComponentInChildren<Animator>();
            if (childAnimator != null)
            {
                modelTransform = childAnimator.transform;
            }
        }
        
        // Автоматически найти Animator, если не назначен
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        
        // ВАЖНО: Отключаем Apply Root Motion в Animator, чтобы анимации не влияли на позицию модели
        // Это предотвращает смещение дочерней модели из-за анимаций
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }
        
        // Автоматически найти камеру, если не назначена
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<ThirdPersonCamera>();
        }
        
        // Инициализируем скорость на основе базовой скорости
        if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
        }
        wallBallLayer = LayerMask.NameToLayer("WallBall");
        groundCheckLayerMask = wallBallLayer >= 0 ? (~0) & ~(1 << wallBallLayer) : ~0;
    }
    
    private void Start()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        
        // Обновляем скорость на основе уровня при старте
        if (useSpeedLevel && gameStorage != null)
        {
            UpdateSpeedFromLevel();
        }
        else if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
        }
        
        // Проверяем готовность игры
        CheckGameReady();
        
        // Подписываемся на событие получения данных SDK (если используется YG2)
#if EnvirData_yg
        if (YG2.onGetSDKData != null)
        {
            YG2.onGetSDKData += OnSDKDataReceived;
        }
#endif
    }
    
    private void OnEnable()
    {
        // Обновляем скорость при включении объекта (на случай если GameStorage был инициализирован после Start)
        if (useSpeedLevel)
        {
            if (gameStorage == null)
            {
                gameStorage = GameStorage.Instance;
            }
            if (gameStorage != null)
            {
                UpdateSpeedFromLevel();
            }
        }
        
        // Проверяем готовность игры при включении
        CheckGameReady();
    }
    
    private void OnDisable()
    {
        // Отписываемся от событий
#if EnvirData_yg
        if (YG2.onGetSDKData != null)
        {
            YG2.onGetSDKData -= OnSDKDataReceived;
        }
#endif
    }
    
#if EnvirData_yg
    private static FieldInfo cachedGameReadyDoneField; // кэш рефлексии, один раз на тип
#endif
    private static FieldInfo cachedHousePosField; // кэш для IsPlayerInHouseArea
    
    /// <summary>
    /// Проверяет, готов ли GameReady (используя рефлексию для доступа к приватному полю)
    /// </summary>
    private void CheckGameReady()
    {
#if EnvirData_yg
        if (cachedGameReadyDoneField == null)
            cachedGameReadyDoneField = typeof(YG2).GetField("gameReadyDone", BindingFlags.NonPublic | BindingFlags.Static);
        
        if (cachedGameReadyDoneField != null)
        {
            bool gameReadyDone = (bool)cachedGameReadyDoneField.GetValue(null);
            if (gameReadyDone && !isGameReady)
            {
                isGameReady = true;
                Debug.Log("[ThirdPersonController] GameReady инициализирован, управление разблокировано");
            }
            else if (!gameReadyDone && !gameReadyTimeoutStarted)
            {
                StartCoroutine(CheckGameReadyDelayed());
                gameReadyTimeoutStarted = true;
            }
        }
        else if (!gameReadyTimeoutStarted)
        {
            StartCoroutine(CheckGameReadyDelayed());
            gameReadyTimeoutStarted = true;
        }
#else
        isGameReady = true;
#endif
    }
    
    /// <summary>
    /// Проверяет GameReady с задержкой (fallback метод)
    /// </summary>
    private System.Collections.IEnumerator CheckGameReadyDelayed()
    {
        // Ждем немного и проверяем снова
        yield return new WaitForSeconds(0.5f);
        CheckGameReady();
        
        // Если все еще не готово, разблокируем через 3 секунды (на случай проблем)
        if (!isGameReady)
        {
            yield return new WaitForSeconds(2.5f);
            if (!isGameReady)
            {
                isGameReady = true;
                Debug.LogWarning("[ThirdPersonController] GameReady не обнаружен, управление разблокировано по таймауту");
            }
        }
    }
    
    /// <summary>
    /// Вызывается при получении данных SDK
    /// </summary>
    private void OnSDKDataReceived()
    {
        CheckGameReady();
    }
    
    private void Update()
    {
        if (characterController == null || !gameObject.activeInHierarchy)
            return;
        if (!characterController.enabled)
            return;
        
        // Периодически проверяем готовность игры, пока она не готова
        if (!isGameReady)
        {
            CheckGameReady();
        }
        
        // Сначала применяем гравитацию, обновляем состояние падения и движение,
        // затем проверяем землю — так isGrounded ставится в тот же кадр, что и приземление (не с задержкой в кадр).
        ApplyGravity();
        UpdateFallingState();
        HandleMovement();
        HandleGroundCheck();
        HandleJump();
        UpdateAnimator();
        
    }
    
    private void LateUpdate()
    {
        // Применяем компенсацию поворота после обновления анимации
        HandleJumpRotation();
    }
    
    private void HandleGroundCheck()
    {
        Vector3 bottom = transform.position + characterController.center + Vector3.down * (characterController.height * 0.5f);
        float len = groundCheckLength;
        var query = QueryTriggerInteraction.Ignore;
        
        bool contact = false;
        
        if (characterController.isGrounded && velocity.y <= 2f)
            contact = true;
        
        if (!contact && groundCheckRadius > 0.001f && Physics.SphereCast(bottom, groundCheckRadius, Vector3.down, out RaycastHit sh, len, groundCheckLayerMask, query))
        {
            if (sh.collider != null && sh.collider.gameObject != gameObject && !sh.collider.isTrigger && sh.normal.y >= minGroundNormalY)
                contact = true;
        }
        
        if (!contact)
        {
            Vector3 fwd = transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
            Vector3 rgt = transform.right;   rgt.y = 0f; if (rgt.sqrMagnitude < 0.01f) rgt = Vector3.right;   rgt.Normalize();
            groundCheckOrigins[0] = bottom;
            groundCheckOrigins[1] = bottom + fwd * groundCheckRadius;
            groundCheckOrigins[2] = bottom - fwd * groundCheckRadius;
            groundCheckOrigins[3] = bottom + rgt * groundCheckRadius;
            groundCheckOrigins[4] = bottom - rgt * groundCheckRadius;
            for (int i = 0; i < groundCheckOrigins.Length; i++)
            {
                if (Physics.Raycast(groundCheckOrigins[i], Vector3.down, out RaycastHit hit, len, groundCheckLayerMask, query))
                {
                    if (hit.collider != null && hit.collider.gameObject != gameObject && !hit.collider.isTrigger && hit.normal.y >= minGroundNormalY)
                    {
                        contact = true;
                        break;
                    }
                }
            }
        }
        
        isActuallyGrounded = contact;
        
        if (contact)
        {
            lastGroundedTime = Time.time;
            groundedFalseFrameCount = 0;
        }
        else
        {
            if (velocity.y <= 0.1f)
                groundedFalseFrameCount++;
            else
                groundedFalseFrameCount = groundedFramesRequired;
        }
        
        if (isActuallyGrounded)
            isGrounded = true;
        else if (velocity.y > 0.2f)
            isGrounded = false;
        else
            isGrounded = (Time.time - lastGroundedTime) < groundedBufferTime || groundedFalseFrameCount < groundedFramesRequired;
        
        if (isOnLadder)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            groundedFalseFrameCount = 0;
        }
        
        if (isSliding)
        {
            isGrounded = true;
            lastGroundedTime = Time.time;
            groundedFalseFrameCount = 0;
        }
        
        // Отдельный более «агрессивный» флаг приземлённости только для анимации:
        // используем мгновенный реальный контакт без буфера, чтобы убрать задержку между приземлением и переходом в бег.
        if (isOnLadder)
        {
            isGroundedForAnimation = true;
        }
        else if (isSliding)
        {
            isGroundedForAnimation = false; // для анимации fly при скольжении
        }
        else
        {
            isGroundedForAnimation = isActuallyGrounded && velocity.y <= 0.1f;
        }
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            isJumping = false;
        }
    }
    
    private void HandleMovement()
    {
        // Блокируем управление, если игра не готова
        if (!isGameReady)
        {
            return;
        }
        
        // Если на лестнице — используем специальную логику движения
        if (isOnLadder && currentLadder != null)
        {
            HandleLadderMovement();
            return;
        }
        
        // Если в режиме скольжения — используем HandleSlideMovement
        if (isSliding)
        {
            HandleSlideMovement();
            return;
        }
        
        // Получаем ввод с клавиатуры или джойстика
        float horizontal = 0f; // A/D
        float vertical = 0f; // W/S
        
        // Приоритет джойстику на мобильных устройствах
        if (joystickInput.magnitude > 0.1f)
        {
            horizontal = joystickInput.x;
            vertical = joystickInput.y;
        }
        else
        {
            // Используем клавиатуру, если джойстик не активен
#if ENABLE_INPUT_SYSTEM
            // Новый Input System
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    horizontal += 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    vertical += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    vertical -= 1f;
            }
#else
            // Старый Input System
            horizontal = Input.GetAxisRaw("Horizontal");
            vertical = Input.GetAxisRaw("Vertical");
#endif
        }
        
        // Вычисляем направление движения относительно камеры
        Vector3 moveDirection = Vector3.zero;
        
        if (cameraController != null)
        {
            // Получаем направление камеры (только горизонтальное вращение)
            Vector3 cameraForward = cameraController.GetCameraForward();
            Vector3 cameraRight = cameraController.GetCameraRight();
            
            // Нормализуем векторы камеры и убираем вертикальную составляющую
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // Вычисляем направление движения относительно камеры
            moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
        }
        else
        {
            // Если камера не найдена, используем мировые оси
            moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        }
        
        // Вычисляем эффективную скорость движения (с учётом ускорения при падении)
        float effectiveMoveSpeed = moveSpeed;
        if (isFalling && fallSpeedBoostDuration > 0f && fallSpeedBoostPercent > 0f)
        {
            float t = Mathf.Clamp01(fallTime / fallSpeedBoostDuration);
            float boostMultiplier = 1f + (fallSpeedBoostPercent / 100f) * t;
            effectiveMoveSpeed = moveSpeed * boostMultiplier;
        }
        
        // Вычисляем скорость движения для аниматора
        currentSpeed = moveDirection.magnitude * effectiveMoveSpeed;
        
        // Применяем движение через CharacterController
        if (moveDirection.magnitude > 0.1f)
        {
            // Движение - проверяем, что CharacterController активен перед вызовом Move
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(moveDirection * effectiveMoveSpeed * Time.deltaTime);
            }
            
            // Плавный поворот корневого объекта в сторону движения
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            
            // Плавный поворот модели для визуального эффекта (только если не в прыжке)
            if (modelTransform != null && !isJumping)
            {
                modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }
    }
    
    private void HandleJump()
    {
        // Блокируем прыжок, если игра не готова, на лестнице или в режиме скольжения
        if (!isGameReady || isOnLadder || isSliding)
        {
            return;
        }
        
        // Проверяем нажатие Space или кнопки прыжка
        bool jumpPressedThisFrame = false;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Keyboard.current != null)
        {
            jumpPressedThisFrame = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#else
        // Старый Input System
        jumpPressedThisFrame = Input.GetKeyDown(KeyCode.Space);
#endif
        
        // ВАЖНО: Сохраняем ВСЕ нажатия Space как запросы, даже если персонаж не на земле в момент нажатия
        // Это исправляет баг, когда 30% прыжков не срабатывают из-за неточной проверки isGrounded
        if (jumpPressedThisFrame)
        {
            jumpRequested = true;
            jumpRequestTime = Time.time;
        }
        
        // Также проверяем запрос от кнопки прыжка (для мобильных устройств)
        // Запрос уже установлен через метод Jump(), просто обновляем время если нужно
        
        // Проверяем, есть ли активный запрос прыжка (в пределах окна времени)
        bool hasActiveJumpRequest = jumpRequested && (jumpRequestTime >= 0f && Time.time - jumpRequestTime <= jumpRequestWindow);
        
        // Выполняем прыжок, если есть активный запрос И персонаж на земле
        if (hasActiveJumpRequest && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            isJumping = true; // Устанавливаем флаг прыжка для поворота модели
            // Сохраняем текущий поворот модели перед прыжком для компенсации
            if (modelTransform != null)
            {
                savedModelRotation = modelTransform.rotation;
            }
            
            // Сбрасываем запрос после успешного прыжка
            jumpRequested = false;
            jumpRequestTime = -1f;
        }
        
        // Сбрасываем устаревшие запросы (если прошло слишком много времени)
        if (jumpRequested && jumpRequestTime >= 0f && Time.time - jumpRequestTime > jumpRequestWindow)
        {
            jumpRequested = false;
            jumpRequestTime = -1f;
        }
        
        // Сбрасываем флаг прыжка при приземлении
        if (isGrounded && isJumping && velocity.y <= 0)
        {
            isJumping = false;
        }
    }
    
    /// <summary>
    /// Обработка поворота модели во время прыжка
    /// Компенсирует возможный поворот анимации прыжка на -10 градусов по Y
    /// </summary>
    private void HandleJumpRotation()
    {
        if (modelTransform == null || !isJumping) return;
        
        // Анимация прыжка поворачивает модель на -10 градусов по Y каждый кадр
        // Компенсируем это, устанавливая поворот модели равным базовому повороту + компенсация
        // Это перезаписывает поворот анимации и предотвращает накопление ошибки
        Quaternion baseRotation = transform.rotation;
        Quaternion compensationRotation = Quaternion.Euler(0f, jumpRotationAngle, 0f);
        
        // Устанавливаем поворот модели напрямую, игнорируя поворот анимации
        // LateUpdate гарантирует, что это применяется после обновления анимации
        modelTransform.rotation = baseRotation * compensationRotation;
    }
    
    /// <summary>
    /// Публичный метод для прыжка (вызывается из UI кнопки)
    /// </summary>
    public void Jump()
    {
        // Устанавливаем запрос на прыжок, который будет обработан в HandleJump()
        // Сохраняем запрос даже если персонаж не на земле в момент вызова
        // Это позволяет обработать прыжок в следующем кадре, когда персонаж уже на земле
            jumpRequested = true;
        jumpRequestTime = Time.time;
    }
    
    private void ApplyGravity()
    {
        // На лестнице или при скольжении не применяем гравитацию
        if (isOnLadder || isSliding) return;
        
        // Проверяем, что CharacterController активен и не null
        if (characterController == null || !characterController.enabled || !gameObject.activeInHierarchy)
        {
            return;
        }
        
        // Применяем гравитацию
        velocity.y += gravity * Time.deltaTime;
        
        // Применяем вертикальное движение
        characterController.Move(velocity * Time.deltaTime);
        
        // Применяем отталкивание (мяч) и затухание
        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            characterController.Move(knockbackVelocity * Time.deltaTime);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecay * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Обновляет состояние падения и таймер ускорения во время падения.
    /// </summary>
    private void UpdateFallingState()
    {
        bool wasFalling = isFalling;
        
        // Время, пока персонаж находится не на земле (и не на лестнице, и не в slide)
        bool inAir = !isGrounded && !isOnLadder && !isSliding;
        
        if (inAir)
        {
            ungroundedTimeMs += Time.deltaTime * 1000f;
        }
        else
        {
            ungroundedTimeMs = 0f;
        }
        
        // Считаем, что персонаж "падает", если он в воздухе дольше порогового времени
        bool fallingNow = inAir && ungroundedTimeMs >= fallDetectionTimeMs;
        
        if (fallingNow)
        {
            fallTime += Time.deltaTime;
            if (fallTime > fallSpeedBoostDuration && fallSpeedBoostDuration > 0f)
            {
                fallTime = fallSpeedBoostDuration;
            }
        }
        else
        {
            fallTime = 0f;
        }
        
        isFalling = fallingNow;
        
        // Логируем момент инициализации падения (переход false -> true)
        if (!wasFalling && isFalling)
        {
            Debug.Log($"[ThirdPersonController][FallDebug] Падение инициализировано: pos={transform.position}, vY={velocity.y:F3}, ungroundedMs={ungroundedTimeMs:F1}");
        }
    }
    
    /// <summary>
    /// Обработка движения по лестнице
    /// </summary>
    private void HandleLadderMovement()
    {
        if (currentLadder == null) return;
        
        // Получаем вертикальный ввод (W/S)
        float verticalInput = 0f;
        
        // Приоритет джойстику
        if (joystickInput.magnitude > 0.1f)
        {
            verticalInput = joystickInput.y;
        }
        else
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    verticalInput += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    verticalInput -= 1f;
            }
#else
            verticalInput = Input.GetAxisRaw("Vertical");
#endif
        }
        
        // Определяем, есть ли движение
        bool isClimbing = Mathf.Abs(verticalInput) > 0.1f;
        
        // Если игрок на земле и нажимает вниз — выходим с лестницы (спрыгиваем)
        if (isGrounded && verticalInput < -0.1f)
        {
            ExitLadder();
            return;
        }
        
        // Управляем скоростью анимации
        // Если игрок не двигается — останавливаем анимацию в текущем кадре
        ladderAnimatorSpeed = isClimbing ? 1f : 0f;
        
        if (isClimbing)
        {
            // Движение по Y
            Vector3 climbMovement = new Vector3(0f, verticalInput * currentLadder.ClimbSpeed * Time.deltaTime, 0f);
            
            // Применяем движение
            if (characterController != null && characterController.enabled)
            {
                characterController.Move(climbMovement);
            }
            
            // Центрируем игрока на лестнице (опционально)
            if (currentLadder.CenterPlayerOnLadder)
            {
                Vector3 ladderCenter = currentLadder.GetLadderCenter();
                Vector3 currentPos = transform.position;
                
                // Плавно перемещаем к центру по X и Z
                float newX = Mathf.Lerp(currentPos.x, ladderCenter.x, currentLadder.CenteringSpeed * Time.deltaTime);
                float newZ = Mathf.Lerp(currentPos.z, ladderCenter.z, currentLadder.CenteringSpeed * Time.deltaTime);
                
                Vector3 centerOffset = new Vector3(newX - currentPos.x, 0f, newZ - currentPos.z);
                characterController.Move(centerOffset);
            }
        }
        
        // Поворачиваем игрока относительно лестницы
        // invertY: выкл = -90° по Y, вкл = +90° по Y
        float yRotationOffset = currentLadder.InvertY ? 90f : -90f;
        Quaternion ladderRotation = currentLadder.transform.rotation;
        Quaternion targetRotation = ladderRotation * Quaternion.Euler(0f, yRotationOffset, 0f);
        
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        
        if (modelTransform != null)
        {
            modelTransform.rotation = Quaternion.Slerp(modelTransform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        
        // Обновляем currentSpeed для аниматора
        currentSpeed = isClimbing ? currentLadder.ClimbSpeed : 0f;
    }
    
    /// <summary>
    /// Вызывается при входе в зону лестницы
    /// </summary>
    public void EnterLadder(Ladder ladder)
    {
        if (ladder == null) return;
        
        isOnLadder = true;
        currentLadder = ladder;
        
        // Сбрасываем вертикальную скорость
        velocity.y = 0f;
        
        // Сбрасываем флаг прыжка
        isJumping = false;
        jumpRequested = false;
        
        Debug.Log("[ThirdPersonController] Вошёл на лестницу");
    }
    
    /// <summary>
    /// Вызывается при выходе из зоны лестницы
    /// </summary>
    public void ExitLadder()
    {
        isOnLadder = false;
        currentLadder = null;
        ladderAnimatorSpeed = 1f; // Восстанавливаем скорость анимации
        
        Debug.Log("[ThirdPersonController] Вышел с лестницы");
    }
    
    /// <summary>
    /// Вызывается при входе в SlideTriggerZone (триггер). Параметры slide берутся из SlideManager.
    /// </summary>
    public void EnterSlide()
    {
        isSliding = true;
        velocity.y = 0f;
        isJumping = false;
        jumpRequested = false;
        jumpRequestTime = -1f;
        float offsetY = (SlideManager.Instance != null) ? SlideManager.Instance.GetPlayerSlideOffsetY() : 0f;
        if (offsetY != 0f && characterController != null)
        {
            characterController.enabled = false;
            transform.position += Vector3.up * offsetY;
            characterController.enabled = true;
        }
        currentSlideModelTurnY = 0f;
        Debug.Log("[ThirdPersonController] Режим скольжения включён");
    }
    
    /// <summary>
    /// Вызывается при выходе из зоны или при нажатии кнопки остановки.
    /// </summary>
    public void ExitSlide()
    {
        isSliding = false;
        if (modelTransform != null)
            modelTransform.localRotation = Quaternion.identity;
        Debug.Log("[ThirdPersonController] Режим скольжения выключен");
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок в режиме скольжения.
    /// </summary>
    public bool IsOnSlide()
    {
        return isSliding;
    }
    
    /// <summary>
    /// Обработка движения в режиме скольжения: полёт без гравитации вдоль наклона + стрейф A/D через CharacterController.
    /// </summary>
    private void HandleSlideMovement()
    {
        SlideManager sm = SlideManager.Instance;
        if (sm == null) return;
        
        // Гравитация отключена в ApplyGravity при isSliding; обнуляем velocity.y каждый кадр
        velocity.y = 0f;
        
        Vector3 forward = sm.GetSlideDirection();
        Vector3 right = sm.GetSlideRight();
        float speed = sm.GetSlideSpeed();
        
        float horizontal = 0f;
        if (joystickInput.magnitude > 0.1f)
            horizontal = joystickInput.x;
        else
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
            }
#else
            horizontal = Input.GetAxisRaw("Horizontal");
#endif
        }
        
        Vector3 move = forward * speed + right * (horizontal * speed);
        if (characterController != null && characterController.enabled)
            characterController.Move(move * Time.deltaTime);
        
        // Ограничение X во время slide: от -170.8 до 106
        const float slideXMin = -170.8f;
        const float slideXMax = 106f;
        Vector3 pos = transform.position;
        if (pos.x < slideXMin || pos.x > slideXMax)
        {
            pos.x = Mathf.Clamp(pos.x, slideXMin, slideXMax);
            characterController.enabled = false;
            transform.position = pos;
            characterController.enabled = true;
        }
        
        Vector3 lookDir = forward;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);
        
        if (modelTransform != null)
        {
            float tiltX = sm.GetSlideTiltAngleX();
            // A: макс rotation -16°, D: макс rotation 35°
            const float slideModelTurnMaxLeft = 16f;
            const float slideModelTurnMaxRight = 35f;
            float targetTurnY = horizontal < 0f ? horizontal * slideModelTurnMaxLeft : horizontal * slideModelTurnMaxRight;
            float rotSpeed = sm != null ? sm.GetPlayerRotationSpeed() : 120f;
            currentSlideModelTurnY = Mathf.MoveTowards(currentSlideModelTurnY, targetTurnY, rotSpeed * Time.deltaTime);
            modelTransform.localRotation = Quaternion.Euler(tiltX, currentSlideModelTurnY, 0f);
        }
        
        currentSpeed = move.magnitude;
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок на лестнице
    /// </summary>
    public bool IsOnLadder()
    {
        return isOnLadder;
    }
    
    private void UpdateAnimator()
    {
        if (animator != null)
        {
            // ВАЖНО: Проверяем, находится ли игрок в области дома
            // Если да, не обновляем аниматор (отключаем анимацию)
            if (IsPlayerInHouseArea())
            {
                // Игрок в области дома - не обновляем аниматор
                return;
            }
            
            // Обработка лестницы
            animator.SetBool(IsLadderHash, isOnLadder);
            
            // Управляем скоростью анимации на лестнице
            // Если игрок на лестнице и не двигается — останавливаем анимацию в текущем кадре
            if (isOnLadder)
            {
                animator.speed = ladderAnimatorSpeed;
            }
            else
            {
                // Скорость анимации: при беге — runAnimationSpeed, иначе нормальная
                float targetSpeed = (currentSpeed > 0.1f) ? runAnimationSpeed : 1f;
                if (Mathf.Abs(animator.speed - targetSpeed) > 0.001f)
                {
                    animator.speed = targetSpeed;
                }
            }
            
            // Обновляем параметр Speed
            animator.SetFloat(SpeedHash, currentSpeed);
            
            // Обновляем параметр isGrounded в аниматоре, используя более быстрый флаг для визуального приземления
            animator.SetBool(IsGroundedHash, isGroundedForAnimation);
        }
    }
    
    /// <summary>
    /// Установить параметр IsTaking в аниматоре
    /// </summary>
    public void SetIsTaking(bool value)
    {
        if (animator != null)
        {
            animator.SetBool(IsTakingHash, value);
        }
    }
    
    // Публичные методы для получения состояния (могут быть полезны для других скриптов)
    public bool IsGrounded()
    {
        return isGrounded;
    }
    
    public bool IsFalling()
    {
        return isFalling;
    }
    
    /// <summary>
    /// Падение для камеры: в воздухе, не на лестнице, не в slide и вертикальная скорость направлена вниз
    /// (без ожидания fallDetectionTimeMs).
    /// </summary>
    public bool IsFallingForCamera()
    {
        return !isGrounded && !isOnLadder && !isSliding && velocity.y < -0.1f;
    }
    
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }
    
    public Vector3 GetVelocity()
    {
        // Возвращаем внутренний velocity, по которому считается гравитация и падение.
        return velocity;
    }
    
    /// <summary>
    /// Добавляет отталкивание (например, от мяча). Направление и величина задаются вектором velocity.
    /// </summary>
    public void AddKnockback(Vector3 knockback)
    {
        knockbackVelocity += knockback;
    }

    private void OnGUI()
    {
        if (!fallDebug) return;

        const int lineHeight = 18;
        int y = 10;

        GUI.Label(new Rect(10, y, 420, lineHeight),
            $"[FallDebug] Grounded={isGrounded}, OnLadder={isOnLadder}, Sliding={isSliding}, vY={velocity.y:F3}");
        y += lineHeight;

        bool inAir = !isGrounded && !isOnLadder && !isSliding;
        GUI.Label(new Rect(10, y, 420, lineHeight),
            $"[FallDebug] InAir={inAir}, UngroundedMs={ungroundedTimeMs:F1}, IsFalling={isFalling}");
        y += lineHeight;

        bool camFall = IsFallingForCamera();
        GUI.Label(new Rect(10, y, 420, lineHeight),
            $"[FallDebug] CamFalling={camFall}, FallTime={fallTime:F2}s, FallDetectMs={fallDetectionTimeMs:F0}");
    }
    
    /// <summary>
    /// Установить ввод от джойстика (вызывается из JoystickManager)
    /// </summary>
    public void SetJoystickInput(Vector2 input)
    {
        joystickInput = input;
    }
    
    /// <summary>
    /// Обновляет скорость на основе уровня из GameStorage
    /// </summary>
    private void UpdateSpeedFromLevel()
    {
        if (!useSpeedLevel)
        {
            moveSpeed = baseMoveSpeed;
            return;
        }
        
        // Если GameStorage еще не инициализирован, пытаемся получить его
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            moveSpeed = baseMoveSpeed;
            return;
        }
        
        int speedLevel = gameStorage.GetPlayerSpeedLevel();
        moveSpeed = baseMoveSpeed + (speedLevel * speedLevelScaler);
        
        // Скорость анимации бега растёт с уровнем: 10 лвл = 1.25, макс (60) = 2.25
        int levelClamped = Mathf.Clamp(speedLevel, RunAnimSpeedMinLevel, RunAnimSpeedMaxLevel);
        runAnimationSpeed = Mathf.Lerp(RunAnimSpeedAtMinLevel, RunAnimSpeedAtMaxLevel,
            (levelClamped - RunAnimSpeedMinLevel) / (float)(RunAnimSpeedMaxLevel - RunAnimSpeedMinLevel));
        
        if (debugSpeed)
        {
            Debug.Log($"[ThirdPersonController] Скорость обновлена: baseMoveSpeed={baseMoveSpeed}, speedLevel={speedLevel}, speedLevelScaler={speedLevelScaler}, moveSpeed={moveSpeed}, runAnimationSpeed={runAnimationSpeed}");
        }
    }
    
    /// <summary>
    /// Установить скорость движения вручную (вызывается из ShopSpeedManager)
    /// </summary>
    public void SetMoveSpeed(float newSpeed)
    {
        moveSpeed = newSpeed;
    }
    
    /// <summary>
    /// Получить текущую скорость движения
    /// </summary>
    public float GetMoveSpeed()
    {
        return moveSpeed;
    }
    
    /// <summary>
    /// Принудительно обновить скорость на основе уровня (можно вызвать из ShopSpeedManager после покупки)
    /// </summary>
    public void RefreshSpeedFromLevel()
    {
        UpdateSpeedFromLevel();
    }
    
    /// <summary>
    /// Получить базовую скорость движения
    /// </summary>
    public float GetBaseMoveSpeed()
    {
        return baseMoveSpeed;
    }
    
    /// <summary>
    /// Получить множитель уровня скорости
    /// </summary>
    public float GetSpeedLevelScaler()
    {
        return speedLevelScaler;
    }
    
    /// <summary>
    /// Вычислить скорость на основе уровня (для отображения в UI)
    /// </summary>
    public float CalculateSpeedFromLevel(int level)
    {
        return baseMoveSpeed + (level * speedLevelScaler);
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок в области дома
    /// </summary>
    private bool IsPlayerInHouseArea()
    {
        TeleportManager teleportManager = TeleportManager.Instance;
        if (teleportManager == null) return false;
        
        if (cachedHousePosField == null)
            cachedHousePosField = typeof(TeleportManager).GetField("housePos", BindingFlags.NonPublic | BindingFlags.Instance);
        if (cachedHousePosField == null) return false;
        
        Transform housePos = cachedHousePosField.GetValue(teleportManager) as Transform;
        if (housePos == null)
        {
            return false;
        }
        
        // Получаем позицию и масштаб области дома
        Vector3 housePosition = housePos.position;
        Vector3 houseScale = housePos.localScale;
        
        // Вычисляем границы области дома (используем масштаб как размер области)
        float halfWidth = houseScale.x / 2f;
        float halfHeight = houseScale.y / 2f;
        float halfDepth = houseScale.z / 2f;
        
        // Получаем позицию игрока
        Vector3 playerPosition = transform.position;
        
        // Проверяем, находится ли игрок в пределах области дома
        bool inXRange = Mathf.Abs(playerPosition.x - housePosition.x) <= halfWidth;
        bool inYRange = Mathf.Abs(playerPosition.y - housePosition.y) <= halfHeight;
        bool inZRange = Mathf.Abs(playerPosition.z - housePosition.z) <= halfDepth;
        
        return inXRange && inYRange && inZRange;
    }
}
