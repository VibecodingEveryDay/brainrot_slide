using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Эффект «спидлайнов» (motion streaks): полосы в локальном пространстве камеры появляются в центре
/// и быстро движутся к краям экрана. Пул мешей-квадов, меш генерируется в коде.
/// Инструкция: повесить на камеру или указать ссылку на Transform камеры; назначить материал (Unlit + прозрачность)
/// или оставить пусто — будет использован дефолтный; настроить spawn interval, lifetime, radius, streak length/width.
/// </summary>
public class SpeedlinesEffect : MonoBehaviour
{
    [Header("Камера")]
    [Tooltip("Если не задан — берётся камера для Game View (см. Use Main Camera For Game View).")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("В игре привязать контейнер к камере, которая рисует Game View (MainCamera). Включи, если в Play линий не видно.")]
    [SerializeField] private bool useMainCameraForGameView = true;

    [Header("Спавн и движение")]
    [SerializeField] private float spawnInterval = 0.05f;
    [SerializeField] private float lifetime = 0.4f;
    [Tooltip("Расстояние от центра до «края» в локальных единицах (половина размера экрана).")]
    [SerializeField] private float radius = 0.4f;
    [Tooltip("Расстояние контейнера полос от камеры по оси вперёд.")]
    [SerializeField] private float distanceFromCamera = 0.5f;
    [Tooltip("В Unity камера смотрит по -Z, линии должны быть впереди (z < 0). Включи только если у тебя вперёд = +Z и в Game View линий не видно.")]
    [SerializeField] private bool forwardIsPositiveZ = false;

    [Header("Полоса")]
    [SerializeField] private float streakLength = 0.15f;
    [SerializeField] private float streakWidth = 0.008f;
    [SerializeField] private int maxStreaks = 40;

    [Header("Внешний вид")]
    [Tooltip("Если не задан — создаётся дефолтный материал (Unlit с альфой).")]
    [SerializeField] private Material streakMaterial;
    [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.6f);
    [Tooltip("0 = не спавнить полосы, 1 = полная интенсивность.")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 1f;
    [Tooltip("Включить эффект только когда игрок в режиме slide (SlideManager.IsSlideActive).")]
    [SerializeField] private bool onlyDuringSlide = true;

    private Transform _container;
    private readonly List<StreakItem> _pool = new List<StreakItem>();
    private float _spawnAccum;
    private Material _usedMaterial;
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    private class StreakItem
    {
        public Transform transform;
        public MeshRenderer renderer;
        public Vector3 startLocalPos;
        public Vector3 endLocalPos;
        public float startTime;
        public bool inUse;
        public MaterialPropertyBlock block;
    }

    private void Awake()
    {
        if (cameraTransform == null)
        {
            var cam = GetComponent<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
            if (cameraTransform == null)
                cameraTransform = transform;
        }

        if (streakMaterial == null)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Particles/Alpha Blended")
                ?? Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _usedMaterial = new Material(shader);
                _usedMaterial.SetInt("_ZWrite", 0);
                _usedMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _usedMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            else
                _usedMaterial = new Material(Shader.Find("Unlit/Color"));
        }
        else
            _usedMaterial = streakMaterial;

        Color c = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.5f));
        _usedMaterial.color = c;
        _usedMaterial.renderQueue = 3000; // Transparent, рисуем поверх сцены
        if (_usedMaterial.HasProperty(TintColorId))
            _usedMaterial.SetColor(TintColorId, c);
    }

    private void Start()
    {
        if (useMainCameraForGameView)
        {
            Camera cam = Camera.main;
            if (cam == null)
                cam = FindFirstObjectByType<Camera>();
            if (cam != null)
                cameraTransform = cam.transform;
        }

        if (cameraTransform == null) return;

        GameObject containerGo = new GameObject("SpeedlinesContainer");
        _container = containerGo.transform;
        _container.SetParent(cameraTransform, false);
        float z = forwardIsPositiveZ ? distanceFromCamera : -distanceFromCamera;
        _container.localPosition = new Vector3(0f, 0f, z);
        _container.localRotation = Quaternion.identity;
        _container.localScale = Vector3.one;
        // Чтобы камера точно рисовала линии — тот же слой, что и у камеры (учёт Culling Mask).
        _container.gameObject.layer = cameraTransform.gameObject.layer;

        Mesh sharedMesh = CreateStreakMesh();
        int layer = _container.gameObject.layer;
        for (int i = 0; i < maxStreaks; i++)
        {
            var item = CreateStreak(sharedMesh, layer);
            item.transform.SetParent(_container, false);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one;
            item.transform.gameObject.SetActive(false);
            _pool.Add(item);
        }
    }

    private static Mesh CreateStreakMesh()
    {
        float l = 0.15f;
        float w = 0.008f;
        var mesh = new Mesh { name = "Speedline" };
        mesh.SetVertices(new List<Vector3>
        {
            new Vector3(-l * 0.5f, -w * 0.5f, 0f),
            new Vector3( l * 0.5f, -w * 0.5f, 0f),
            new Vector3( l * 0.5f,  w * 0.5f, 0f),
            new Vector3(-l * 0.5f,  w * 0.5f, 0f)
        });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        mesh.RecalculateBounds();
        return mesh;
    }

    private StreakItem CreateStreak(Mesh sharedMesh, int layer)
    {
        var go = new GameObject("Streak");
        go.layer = layer;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = sharedMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = _usedMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        var item = new StreakItem
        {
            transform = go.transform,
            renderer = mr,
            block = new MaterialPropertyBlock()
        };
        return item;
    }

    private void Update()
    {
        if (_container == null) return;

        float effectiveIntensity = onlyDuringSlide
            ? (SlideManager.Instance != null && SlideManager.Instance.IsSlideActive() ? intensity : 0f)
            : intensity;

        float dt = Time.deltaTime;

        for (int i = 0; i < _pool.Count; i++)
        {
            var item = _pool[i];
            if (!item.inUse) continue;

            float t = (Time.time - item.startTime) / lifetime;
            if (t >= 1f)
            {
                item.inUse = false;
                item.transform.gameObject.SetActive(false);
                continue;
            }

            Vector3 localPos = Vector3.Lerp(item.startLocalPos, item.endLocalPos, t);
            item.transform.localPosition = localPos;

            float alpha = Mathf.Max((1f - t) * color.a * effectiveIntensity, 0.2f);
            Color c = new Color(color.r, color.g, color.b, alpha);
            item.block.SetColor(ColorId, c);
            item.block.SetColor(TintColorId, c);
            item.renderer.SetPropertyBlock(item.block);
        }

        if (effectiveIntensity <= 0f || spawnInterval <= 0f) return;

        _spawnAccum += dt;
        while (_spawnAccum >= spawnInterval)
        {
            _spawnAccum -= spawnInterval;
            TrySpawnStreak();
        }
    }

    private void TrySpawnStreak()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float dx = Mathf.Cos(angle);
        float dy = Mathf.Sin(angle);

        Vector3 startLocal = new Vector3(dx * 0.02f, dy * 0.02f, 0f);
        Vector3 endLocal = new Vector3(dx * radius, dy * radius, 0f);

        float rotZ = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

        for (int i = 0; i < _pool.Count; i++)
        {
            var item = _pool[i];
            if (item.inUse) continue;

            item.startLocalPos = startLocal;
            item.endLocalPos = endLocal;
            item.startTime = Time.time;
            item.inUse = true;
            item.transform.localPosition = startLocal;
            item.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            item.transform.localScale = new Vector3(streakLength / 0.15f, streakWidth / 0.008f, 1f);
            Color spawnColor = new Color(color.r, color.g, color.b, Mathf.Max(color.a, 0.5f));
            item.block.SetColor(ColorId, spawnColor);
            item.block.SetColor(TintColorId, spawnColor);
            item.renderer.SetPropertyBlock(item.block);
            item.transform.gameObject.SetActive(true);
            return;
        }
    }

    private void OnDestroy()
    {
        if (streakMaterial == null && _usedMaterial != null)
            Destroy(_usedMaterial);
    }
}
