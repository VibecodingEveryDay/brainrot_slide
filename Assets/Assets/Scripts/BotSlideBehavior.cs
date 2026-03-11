using UnityEngine;

/// <summary>
/// Бот на слайде: та же модель и анимации что у игрока, используется только анимация slide.
/// Движется вперёд по плоскости слайда; при столкновении с RedCone уничтожается (RedCone обрабатывает тег Bot).
/// Уничтожается через lifetimeSeconds после спавна.
/// </summary>
public class BotSlideBehavior : MonoBehaviour
{
    [Header("Ссылки (slidePlane задаётся спавнером через Init)")]
    [SerializeField] private Transform slidePlane;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [Tooltip("Опционально: дочерняя модель для наклона по X (как у игрока в slide).")]
    [SerializeField] private Transform modelTransform;

    [Header("Параметры движения")]
    [SerializeField] private float slideSpeed = 15f;
    [SerializeField] private float tiltAngleX = 20f;
    [Tooltip("Смещение бота по Y над плоскостью слайда. Можно задать в префабе или из спавнера через Init.")]
    [SerializeField] private float botSlideOffsetY = 0f;
    [Tooltip("Время сглаживания позиции по Y до траектории слайда.")]
    [SerializeField] private float slideYOffsetLerpTime = 0.5f;
    [SerializeField] private float lifetimeSeconds = 30f;
    [Tooltip("Если Z не меняется дольше этого времени (сек) — бот считается застрявшим и удаляется.")]
    [SerializeField] private float stuckZTimeout = 0.3f;

    [Header("Границы по X (как у игрока на слайде)")]
    [SerializeField] private float slideXMin = -170.8f;
    [SerializeField] private float slideXMax = 106f;

    [Header("Анимация")]
    [Tooltip("Имя параметра аниматора для slide (например Speed — тогда выставляется постоянное значение). Должен совпадать с контрактом аниматора игрока.")]
    [SerializeField] private string slideAnimatorParameter = "Speed";
    [Tooltip("Значение параметра при скольжении (например 1 для Speed).")]
    [SerializeField] private float slideAnimatorValue = 1f;

    private Plane _slideWorldPlane;
    private bool _hasSlideWorldPlane;
    private bool _initialized;
    private float _lastZ;
    private float _lastZChangeTime;
    private const float ZChangeEpsilon = 0.001f;

    /// <summary>
    /// Вызвать из BotSlideSpawner после Instantiate: задаёт плоскость слайда, наклон модели, скорость и смещение по Y.
    /// </summary>
    public void Init(Transform plane, float tiltX, float speed, float offsetY = 0f)
    {
        slidePlane = plane;
        tiltAngleX = tiltX;
        slideSpeed = speed;
        botSlideOffsetY = offsetY;
        BuildSlidePlane();
        _initialized = slidePlane != null;
    }

    private void Start()
    {
        if (slidePlane != null && !_initialized)
            BuildSlidePlane();
        _initialized = _initialized || (slidePlane != null);

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (characterController == null)
            characterController = GetComponent<CharacterController>();
        if (modelTransform == null && animator != null)
            modelTransform = animator.transform;

        Destroy(gameObject, lifetimeSeconds);
        _lastZ = transform.position.z;
        _lastZChangeTime = Time.time;
    }

    private void BuildSlidePlane()
    {
        _hasSlideWorldPlane = false;
        if (slidePlane == null) return;
        Vector3 f = slidePlane.forward;
        Vector3 r = slidePlane.right;
        if (f.sqrMagnitude > 0.0001f && r.sqrMagnitude > 0.0001f)
        {
            Vector3 n = Vector3.Cross(r, f);
            if (n.sqrMagnitude > 0.0001f)
            {
                n.Normalize();
                _slideWorldPlane = new Plane(n, slidePlane.position);
                _hasSlideWorldPlane = true;
            }
        }
    }

    private bool TryProjectPointOnSlidePlane(Vector3 worldPos, out Vector3 projected)
    {
        if (!_hasSlideWorldPlane)
        {
            projected = worldPos;
            return false;
        }
        float distance = _slideWorldPlane.GetDistanceToPoint(worldPos);
        projected = worldPos - _slideWorldPlane.normal * distance;
        return true;
    }

    private void Update()
    {
        if (slidePlane == null || characterController == null) return;

        Vector3 forward = slidePlane.forward;
        if (forward.sqrMagnitude < 0.001f) return;
        forward.Normalize();

        Vector3 move = forward * slideSpeed;
        if (characterController.enabled)
            characterController.Move(move * Time.deltaTime);

        Vector3 pos = transform.position;
        if (pos.x < slideXMin || pos.x > slideXMax)
        {
            pos.x = Mathf.Clamp(pos.x, slideXMin, slideXMax);
            characterController.enabled = false;
            transform.position = pos;
            characterController.enabled = true;
        }

        if (slideYOffsetLerpTime > 0f && TryProjectPointOnSlidePlane(transform.position, out Vector3 projected))
        {
            float targetY = projected.y + botSlideOffsetY;
            Vector3 curPos = transform.position;
            float dy = targetY - curPos.y;
            if (Mathf.Abs(dy) > 0.0001f)
            {
                float maxDelta = Mathf.Abs(dy) * (Time.deltaTime / slideYOffsetLerpTime);
                float newY = Mathf.MoveTowards(curPos.y, targetY, maxDelta);
                characterController.enabled = false;
                curPos.y = newY;
                transform.position = curPos;
                characterController.enabled = true;
            }
        }

        Vector3 lookDir = forward;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir.normalized);

        if (modelTransform != null)
            modelTransform.localRotation = Quaternion.Euler(tiltAngleX, 0f, 0f);

        if (animator != null && !string.IsNullOrEmpty(slideAnimatorParameter))
        {
            int hash = Animator.StringToHash(slideAnimatorParameter);
            animator.SetFloat(hash, slideAnimatorValue);
        }

        float z = transform.position.z;
        if (Mathf.Abs(z - _lastZ) > ZChangeEpsilon)
        {
            _lastZ = z;
            _lastZChangeTime = Time.time;
        }
        else if (stuckZTimeout > 0f && (Time.time - _lastZChangeTime) >= stuckZTimeout)
        {
            Destroy(gameObject);
        }
    }
}
