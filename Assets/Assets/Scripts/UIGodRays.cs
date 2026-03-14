using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Эффект вращающихся лучей (god rays) для UI.
/// Ставится на пустой GameObject внутри Canvas. Генерирует Image-лучи и крутит их.
/// Удобно разместить за круглой аватаркой как фоновое свечение.
/// </summary>
[ExecuteAlways]
public class UIGodRays : MonoBehaviour
{
    [Header("Ray Sprite")]
    [Tooltip("UI-спрайт одного луча. Если не задан — используется белый Default.")]
    [SerializeField] private Sprite raySprite;

    [Header("Layout")]
    [Tooltip("Количество лучей (равномерно по кругу).")]
    [SerializeField, Range(3, 24)] private int rayCount = 8;
    [Tooltip("Ширина луча (px).")]
    [SerializeField] private float rayWidth = 40f;
    [Tooltip("Высота (длина) луча (px).")]
    [SerializeField] private float rayHeight = 200f;

    [Header("Color")]
    [SerializeField] private Color rayColor = new Color(1f, 0.95f, 0.6f, 0.35f);

    [Header("Rotation")]
    [Tooltip("Скорость вращения (градусов/сек). Положительное — против часовой.")]
    [SerializeField] private float rotationSpeed = 25f;

    [Header("Fade (опционально)")]
    [Tooltip("Если true, лучи через один будут чуть прозрачнее — даёт объём.")]
    [SerializeField] private bool alternateAlpha = true;
    [SerializeField, Range(0f, 1f)] private float alternateAlphaFactor = 0.5f;

    [Header("Lifetime")]
    [Tooltip("Время жизни (сек). 0 = вечный.")]
    [SerializeField] private float lifetime = 2f;

    private RectTransform _rt;
    private bool _generated;
    private int _lastRayCount;
    private float _spawnTime;

    private void OnEnable()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt == null) _rt = gameObject.AddComponent<RectTransform>();
        _spawnTime = Time.time;
        GenerateRays();
    }

    private void Update()
    {
        if (_rt == null) return;

        if (Application.isPlaying && lifetime > 0f && Time.time - _spawnTime >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (_lastRayCount != rayCount)
            GenerateRays();

        _rt.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
    }

    private void GenerateRays()
    {
        ClearChildren();
        _lastRayCount = rayCount;

        float step = 360f / rayCount;

        for (int i = 0; i < rayCount; i++)
        {
            GameObject go = new GameObject($"Ray_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_rt, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(rayWidth, rayHeight);
            rt.localRotation = Quaternion.Euler(0f, 0f, -step * i);

            Image img = go.GetComponent<Image>();
            img.sprite = raySprite;
            img.raycastTarget = false;
            img.type = raySprite != null ? Image.Type.Simple : Image.Type.Simple;

            Color c = rayColor;
            if (alternateAlpha && i % 2 == 1)
                c.a *= alternateAlphaFactor;
            img.color = c;
        }

        _generated = true;
    }

    private void ClearChildren()
    {
        int count = _rt.childCount;
        for (int i = count - 1; i >= 0; i--)
        {
            GameObject child = _rt.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private void OnValidate()
    {
        if (_rt != null && gameObject.activeInHierarchy)
            GenerateRays();
    }
}
