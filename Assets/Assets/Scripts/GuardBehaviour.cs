using System.Collections;
using UnityEngine;

/// <summary>
/// Поведение охранника:
/// - Патрулирует вокруг своей позиции (idle → выбор случайной точки → walk → idle).
/// - Если игрок держит брейнрот и входит в радиус обнаружения — преследует игрока.
/// - При входе в радиус атаки бьёт игрока, сбрасывает брейнрот, отбрасывает и через задержку телепортирует на базу.
/// - Cylinder AttackArea отображает прогресс до удара цветом (желтый → красный).
/// </summary>
public class GuardBehaviour : MonoBehaviour
{
    /// <summary>
    /// Глобальный флаг: идёт ли сейчас атака какого‑то охранника.
    /// Нужен, чтобы одна атака не прерывала/перебивала другую.
    /// </summary>
    private static bool anyGuardAttacking = false;

    /// <summary>
    /// Общий 2D‑аудиоисточник для звука удара (одно и то же svx‑аудио на всю сцену).
    /// </summary>
    private static AudioSource sharedHitAudioSource;

    private enum GuardState
    {
        Idle,
        Patrolling,
        Chasing,
        Attacking
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform attackAreaTransform;
    [SerializeField] private Renderer attackAreaRenderer;
    [SerializeField] private AudioClip hitSfx;

    [Header("Patrol")]
    [Tooltip("Время простоя в состоянии Idle перед выбором новой точки патруля (сек).")]
    [SerializeField] private float idleDuration = 1.5f;
    [Tooltip("Максимальная дистанция случайной точки патруля от текущей позиции (по XZ).")]
    [SerializeField] private float patrolRadius = 5f;
    [Tooltip("Скорость ходьбы охранника.")]
    [SerializeField] private float walkSpeed = 2f;

    [Header("Detection & Attack")]
    [Tooltip("Радиус обнаружения игрока с брейнротом (XZ).")]
    [SerializeField] private float detectionRadius = 10f;
    [Tooltip("Радиус атаки (расстояние до игрока по XZ, при котором наносится удар).")]
    [SerializeField] private float attackRange = 1.5f;
    [Tooltip("Радиус, внутри которого зона начинает краснеть (между ним и AttackRange плавный переход к красному).")]
    [SerializeField] private float yellowMinimalArea = 5f;
    [Tooltip("Сила горизонтального отбрасывания игрока при ударе (по XZ).")]
    [SerializeField] private float knockbackForce = 8f;
    [Tooltip("Дополнительная вертикальная составляющая отбрасывания (по Y), чтобы игрок взлетал в воздух.")]
    [SerializeField] private float knockbackVerticalForce = 5f;
    [Tooltip("Задержка перед отбрасыванием игрока после начала анимации удара (сек).")]
    [SerializeField] private float knockbackDelay = 0.2f;
    [Tooltip("Задержка перед телепортом игрока на базу после удара (сек).")]
    [SerializeField] private float postHitDelay = 1.5f;

    [Header("Attack Area Visual")]
    [Tooltip("Цвет AttackArea, когда игрок далеко (по умолчанию ~#333400).")]
    [SerializeField] private Color attackAreaSafeColor = new Color(0.2f, 0.2f, 0f);
    [Tooltip("Цвет AttackArea, когда удар неизбежен (игрок в радиусе атаки).")]
    [SerializeField] private Color attackAreaDangerColor = Color.red;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;

    [Header("Patrol Area (optional)")]
    [Tooltip("Если назначен PlaneBrSpawner — точки патруля выбираются случайно внутри его плоскости.")]
    [SerializeField] private PlaneBrSpawner patrolSpawner;

    private GuardState state = GuardState.Idle;
    private float idleTimer;
    private Vector3 patrolTarget;

    private Transform playerTransform;
    private PlayerCarryController playerCarry;
    private ThirdPersonController playerController;
    private TeleportManager teleportManager;

    private bool isAttacking;
    private float baseY;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (attackAreaTransform == null)
        {
            var t = transform.Find("AttackArea");
            if (t != null)
                attackAreaTransform = t;
        }

        if (attackAreaRenderer == null && attackAreaTransform != null)
            attackAreaRenderer = attackAreaTransform.GetComponent<Renderer>();

        // Находим или создаём общий 2D‑AudioSource для звука удара.
        if (sharedHitAudioSource == null)
        {
            // Пытаемся взять AudioSource с основной камеры (типичный вариант 2D‑звука).
            var mainCam = Camera.main;
            if (mainCam != null)
                sharedHitAudioSource = mainCam.GetComponent<AudioSource>();

            // Если у камеры нет AudioSource — создаём отдельный объект под 2D‑звук удара.
            if (sharedHitAudioSource == null)
            {
                var go = new GameObject("GuardHit2DAudio");
                sharedHitAudioSource = go.AddComponent<AudioSource>();
                DontDestroyOnLoad(go);
                // Настройка 2D‑звука: spatialBlend = 0 (делается здесь один раз).
                sharedHitAudioSource.spatialBlend = 0f;
            }
        }

        baseY = transform.position.y;
    }

    private void Start()
    {
        teleportManager = TeleportManager.Instance;
    }

    private void Update()
    {
        // Обновляем ссылки на игрока по тегу Player, если они потерялись
        EnsurePlayerReferences();

        UpdateAttackAreaColor();

        switch (state)
        {
            case GuardState.Idle:
                UpdateIdle();
                break;
            case GuardState.Patrolling:
                UpdatePatrolling();
                break;
            case GuardState.Chasing:
                UpdateChasing();
                break;
            case GuardState.Attacking:
                // Логика атаки выполняется корутиной/триггером, здесь только ожидание.
                break;
        }
    }

    #region State Updates

    private void UpdateIdle()
    {
        if (animator != null)
            animator.SetBool("Walk", false);

        idleTimer += Time.deltaTime;

        if (ShouldChasePlayer())
        {
            state = GuardState.Chasing;
            return;
        }

        if (idleTimer >= idleDuration)
        {
            idleTimer = 0f;
            patrolTarget = GetRandomPatrolPoint();
            state = GuardState.Patrolling;
        }
    }

    private void UpdatePatrolling()
    {
        if (animator != null)
            animator.SetBool("Walk", true);

        if (ShouldChasePlayer())
        {
            state = GuardState.Chasing;
            return;
        }

        MoveTowardsXZ(patrolTarget, walkSpeed);

        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetXZ = new Vector2(patrolTarget.x, patrolTarget.z);
        if (Vector2.Distance(currentXZ, targetXZ) <= 0.1f)
        {
            state = GuardState.Idle;
            idleTimer = 0f;
        }
    }

    private void UpdateChasing()
    {
        if (playerTransform == null || !PlayerHasBrainrotInHands())
        {
            state = GuardState.Idle;
            idleTimer = 0f;
            return;
        }

        if (animator != null)
            animator.SetBool("Walk", true);

        float distToPlayer = DistanceXZ(transform.position, playerTransform.position);

        if (distToPlayer > detectionRadius * 1.1f)
        {
            // Игрок вышел из зоны обнаружения
            state = GuardState.Idle;
            idleTimer = 0f;
            return;
        }

        if (distToPlayer <= attackRange)
        {
            StartAttack();
            return;
        }

        MoveTowardsXZ(playerTransform.position, walkSpeed);
    }

    #endregion

    #region Helpers

    private void EnsurePlayerReferences()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerCarry = playerTransform.GetComponent<PlayerCarryController>();
                playerController = playerTransform.GetComponent<ThirdPersonController>();
            }
        }
        else
        {
            if (playerCarry == null)
                playerCarry = playerTransform.GetComponent<PlayerCarryController>();
            if (playerController == null)
                playerController = playerTransform.GetComponent<ThirdPersonController>();
        }
    }

    private bool PlayerHasBrainrotInHands()
    {
        if (playerCarry == null)
            return false;

        BrainrotObject carried = playerCarry.GetCurrentCarriedObject();
        return carried != null;
    }

    private bool ShouldChasePlayer()
    {
        if (playerTransform == null || !PlayerHasBrainrotInHands())
            return false;

        float dist = DistanceXZ(transform.position, playerTransform.position);
        return dist <= detectionRadius;
    }

    private Vector3 GetRandomPatrolPoint()
    {
        // Если есть привязка к PlaneBrSpawner — патрулируем по его плоскости.
        if (patrolSpawner != null)
        {
            Vector3 world = patrolSpawner.GetRandomPointOnPlane();
            // Фиксируем Y на базовом уровне охранника, чтобы он не «проваливался».
            world.y = baseY;
            return world;
        }

        // Иначе — старое поведение: случайный круг вокруг текущей позиции.
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        Vector3 basePos = transform.position;
        Vector3 target = new Vector3(basePos.x + randomCircle.x, basePos.y, basePos.z + randomCircle.y);
        return target;
    }

    /// <summary>
    /// Вызывается PlaneBrSpawner при спавне охранника, чтобы задать область патруля.
    /// </summary>
    public void SetPatrolSpawner(PlaneBrSpawner spawner)
    {
        patrolSpawner = spawner;
    }

    /// <summary>
    /// Задать скорость ходьбы охранника (например, для разных по сложности платформ).
    /// </summary>
    public void SetWalkSpeed(float newWalkSpeed)
    {
        if (newWalkSpeed > 0f)
            walkSpeed = newWalkSpeed;
    }

    private void MoveTowardsXZ(Vector3 targetWorldPos, float speed)
    {
        Vector3 current = transform.position;
        Vector3 toTarget = targetWorldPos - current;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
            return;

        Vector3 dir = toTarget.normalized;
        Vector3 delta = dir * (speed * Time.deltaTime);

        Vector3 newPos = current + delta;
        newPos.y = baseY;
        transform.position = newPos;

        // Поворачиваем охранника лицом к направлению движения
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 10f * Time.deltaTime);
        }
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    #endregion

    #region Attack

    private void StartAttack()
    {
        // Если этот охранник уже атакует — выходим.
        if (isAttacking)
            return;

        // Если уже идёт атака другого охранника — не запускаем новую,
        // чтобы одна атака не прерывала другую.
        if (anyGuardAttacking)
            return;

        isAttacking = true;
        anyGuardAttacking = true;
        state = GuardState.Attacking;

        if (animator != null)
        {
            animator.SetBool("Walk", false);
            animator.SetTrigger("Kick");
        }

        StartCoroutine(AttackCoroutine());
    }

    private IEnumerator AttackCoroutine()
    {
        // Небольшая задержка для синхронизации с началом анимации удара:
        // отдача игрока происходит через knockbackDelay секунд после старта анимации.
        if (knockbackDelay > 0f)
            yield return new WaitForSeconds(knockbackDelay);

        // В момент удара/отдачи сразу удаляем брейнрот из рук игрока.
        if (teleportManager == null)
        {
            teleportManager = TeleportManager.Instance;
        }
        if (teleportManager != null)
        {
            teleportManager.RemoveCarriedBrainrot();
        }
        else if (playerCarry != null)
        {
            BrainrotObject carried = playerCarry.GetCurrentCarriedObject();
            if (carried != null)
            {
                playerCarry.DropObject();
                Destroy(carried.gameObject);
            }
        }

        // В момент удара/отдачи проигрываем ОДИН общий 2D‑звук удара, если он назначен.
        if (sharedHitAudioSource != null && hitSfx != null)
        {
            sharedHitAudioSource.PlayOneShot(hitSfx);
        }

        // Отбрасываем игрока
        if (playerTransform != null && playerController == null)
        {
            // Фоллбек: пытаемся найти контроллер игрока в сцене, если ссылка потерялась.
            playerController = FindFirstObjectByType<ThirdPersonController>();
        }

        if (playerController != null && playerTransform != null)
        {
            // Считаем направление по XZ от охранника к игроку.
            Vector3 dir = playerTransform.position - transform.position;
            dir.y = 0f;

            // Если игрок совсем "внутри" охранника и вектор почти нулевой —
            // используем направление «от охранника вперёд» как fallback.
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = transform.forward;
                dir.y = 0f;
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                // Горизонтальная составляющая отбрасывания (по XZ)
                Vector3 horizontalKnock = dir * knockbackForce;
                // Вертикальная составляющая (по Y), чтобы игрок взлетал вверх
                Vector3 verticalKnock = Vector3.up * knockbackVerticalForce;
                Vector3 knock = horizontalKnock + verticalKnock;
                playerController.AddKnockback(knock);
            }
        }
        
        // Запускаем отложенный телепорт на базу.
        // Сама надпись «Вы проиграли» и удаление брейнрота будут выполнены в TeleportManager
        // непосредственно перед телепортацией.
        if (teleportManager == null)
        {
            teleportManager = TeleportManager.Instance;
        }
        if (teleportManager != null)
        {
            teleportManager.TeleportPlayerToHouseAfterDelay(postHitDelay);
        }
        else
        {
            Debug.LogWarning("[GuardBehaviour] TeleportManager не найден, телепорт не выполнен.");
        }

        // Держим охранника в состоянии атаки, пока не сработает телепорт.
        // Это гарантирует, что AttackArea остаётся красной до момента телепортации.
        if (postHitDelay > 0f)
        {
            yield return new WaitForSeconds(postHitDelay);
        }

        // Возвращаемся в Idle после завершения всей "сцены поражения"
        isAttacking = false;
        anyGuardAttacking = false;
        state = GuardState.Idle;
        idleTimer = 0f;
    }

    #endregion

    #region AttackArea Visual

    private void UpdateAttackAreaColor()
    {
        if (attackAreaRenderer == null || playerTransform == null)
            return;

        float t = 0f;

        // Во время самой атаки всегда держим зону полностью красной,
        // чтобы удар визуально выглядел завершённым.
        if (state == GuardState.Attacking)
        {
            t = 1f;
        }
        else if (PlayerHasBrainrotInHands())
        {
            float d = DistanceXZ(transform.position, playerTransform.position);

            // Полностью красная зона, когда игрок в радиусе атаки
            if (d <= attackRange)
            {
                t = 1f;
            }
            // Вне зоны начала «желтой» области — безопасный цвет
            else if (d >= yellowMinimalArea)
            {
                t = 0f;
            }
            // Между yellowMinimalArea и attackRange делаем плавный переход
            // от безопасного цвета к полностью красному.
            else
            {
                t = Mathf.InverseLerp(yellowMinimalArea, attackRange, d);
            }
        }

        t = Mathf.Clamp01(t);
        Color c = Color.Lerp(attackAreaSafeColor, attackAreaDangerColor, t);
        attackAreaRenderer.material.color = c;
    }

    #endregion

    #region Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        // Радиус обнаружения (зелёный)
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Радиус начала «жёлтой» зоны, откуда область начинает краснеть (жёлтый)
        if (yellowMinimalArea > 0f)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, yellowMinimalArea);
        }

        // Радиус атаки (красный)
        Gizmos.color = new Color(1f, 0f, 0f, 0.75f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
#endif

    #endregion
}

