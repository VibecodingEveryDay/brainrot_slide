using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Красит ячейки "земли" в шахматном порядке,
/// сохраняя разницу светлая/тёмная, но меняя общий цвет.
/// Вешается на родителя ground, который содержит ячейки.
/// </summary>
public class GroundChessColor : MonoBehaviour
{
    [Header("Материалы ячеек (образцы)")]
    [Tooltip("Материал, который сейчас стоит на СВЕТЛЫХ ячейках (образец).")]
    [SerializeField] private Material lightCellMaterialSample;

    [Tooltip("Материал, который сейчас стоит на ТЁМНЫХ ячейках (образец).")]
    [SerializeField] private Material darkCellMaterialSample;

    [Header("Настройки цвета")]
    [Tooltip("Базовый цвет шахматного поля (по нему считается светлая/тёмная ячейка).")]
    [SerializeField] private Color baseColor = new Color(0.3f, 0.8f, 0.3f, 1f);

    [Tooltip("Во сколько раз тёмная ячейка темнее базового цвета по яркости (значение по V в HSV).")]
    [Range(0.3f, 1f)]
    [SerializeField] private float darkBrightnessMultiplier = 0.75f;

    [Tooltip("Применять перекраску автоматически в Start(). Если выключить — вызывай ApplyChessColor() вручную.")]
    [SerializeField] private bool applyOnStart = true;

    private readonly List<Renderer> _lightCells = new List<Renderer>();
    private readonly List<Renderer> _darkCells = new List<Renderer>();

    private void Awake()
    {
        CollectCells();
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyChessColor();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // В редакторе обновляем списки при изменении полей
        CollectCells();
    }
#endif

    /// <summary>
    /// Находит все дочерние Renderer'ы и раскладывает их
    /// по спискам светлых/тёмных ячеек по образцам материалов.
    /// </summary>
    public void CollectCells()
    {
        _lightCells.Clear();
        _darkCells.Clear();

        if (lightCellMaterialSample == null && darkCellMaterialSample == null)
            return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null || r.sharedMaterial == null)
                continue;

            if (lightCellMaterialSample != null && r.sharedMaterial == lightCellMaterialSample)
            {
                _lightCells.Add(r);
            }
            else if (darkCellMaterialSample != null && r.sharedMaterial == darkCellMaterialSample)
            {
                _darkCells.Add(r);
            }
        }
    }

    /// <summary>
    /// Применить цвет к шахматному полю.
    /// Вызывай из контекстного меню или кода.
    /// </summary>
    [ContextMenu("Apply Chess Color")]
    public void ApplyChessColor()
    {
        // Пересчитать клетки на случай изменений в иерархии
        if (_lightCells.Count == 0 && _darkCells.Count == 0)
        {
            CollectCells();
        }

        // Переводим базовый цвет в HSV, чтобы управлять яркостью
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);

        // Светлые клетки — сам базовый цвет
        Color lightColor = Color.HSVToRGB(h, s, v);

        // Тёмные — тот же тон/насыщенность, но ниже яркость
        float darkV = Mathf.Clamp01(v * darkBrightnessMultiplier);
        Color darkColor = Color.HSVToRGB(h, s, darkV);

        // Применяем цвет к светлым и тёмным ячейкам
        ApplyColorToRenderers(_lightCells, lightColor);
        ApplyColorToRenderers(_darkCells, darkColor);
    }

    private void ApplyColorToRenderers(List<Renderer> renderers, Color color)
    {
        if (renderers == null)
            return;

        foreach (var r in renderers)
        {
            if (r == null)
                continue;

            // Создаём отдельный инстанс материала для конкретного рендера
            // (Unity сам дублирует material при записи свойства).
            var mat = r.material;
            if (mat == null)
                continue;

            // Пытаемся выставить цвет в распространённые свойства.
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            else if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }
        }
    }

    /// <summary>
    /// Установить базовый цвет через код и сразу перекрасить.
    /// </summary>
    public void SetBaseColorAndApply(Color newBaseColor)
    {
        baseColor = newBaseColor;
        ApplyChessColor();
    }
}

