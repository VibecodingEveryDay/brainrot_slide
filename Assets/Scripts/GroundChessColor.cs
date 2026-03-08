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

    [Header("Объединённый меш (ProBuilder)")]
    [Tooltip("Включить, если земля — один объединённый меш. Цвет задаётся шейдером по мировой позиции (материал должен использовать шейдер Ground/ChessGround).")]
    [SerializeField] private bool useMergedMesh = false;

    [Tooltip("Размер клетки в мировых единицах (только для режима объединённого меша). Одна клетка = 5x5 по albedo.")]
    [SerializeField] private float mergedCellSize = 5f;

    [Header("Настройки цвета")]
    [Tooltip("Базовый цвет шахматного поля (по нему считается светлая/тёмная ячейка).")]
    [SerializeField] private Color baseColor = new Color(0.3f, 0.8f, 0.3f, 1f);

    [Tooltip("Во сколько раз тёмная ячейка темнее базового цвета по яркости (значение по V в HSV).")]
    [Range(0.3f, 1f)]
    [SerializeField] private float darkBrightnessMultiplier = 0.75f;

    [Tooltip("Применять перекраску автоматически в Start(). Если выключить — вызывай ApplyChessColor() вручную.")]
    [SerializeField] private bool applyOnStart = true;

    private static readonly int ChessColorLightId = Shader.PropertyToID("_ChessColorLight");
    private static readonly int ChessColorDarkId = Shader.PropertyToID("_ChessColorDark");
    private static readonly int ChessCellSizeId = Shader.PropertyToID("_ChessCellSize");

    private static Shader _chessGroundShader;

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
        Color.RGBToHSV(baseColor, out float h, out float s, out float v);
        Color lightColor = Color.HSVToRGB(h, s, v);
        float darkV = Mathf.Clamp01(v * darkBrightnessMultiplier);
        Color darkColor = Color.HSVToRGB(h, s, darkV);

        if (useMergedMesh)
        {
            ApplyChessColorMerged(lightColor, darkColor);
            return;
        }

        if (_lightCells.Count == 0 && _darkCells.Count == 0)
            CollectCells();

        ApplyColorToRenderers(_lightCells, lightColor);
        ApplyColorToRenderers(_darkCells, darkColor);
    }

    /// <summary>
    /// Для объединённого меша: подменяет материал на шейдер Ground/ChessGround при необходимости
    /// и выставляет _ChessColorLight, _ChessColorDark, _ChessCellSize.
    /// </summary>
    private void ApplyChessColorMerged(Color lightColor, Color darkColor)
    {
        if (_chessGroundShader == null)
            _chessGroundShader = Shader.Find("Ground/ChessGround");
        if (_chessGroundShader == null)
            return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null)
                continue;

            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                    continue;
                if (!mats[i].HasProperty(ChessColorLightId))
                    mats[i] = new Material(_chessGroundShader);

                mats[i].SetColor(ChessColorLightId, lightColor);
                mats[i].SetColor(ChessColorDarkId, darkColor);
                mats[i].SetFloat(ChessCellSizeId, mergedCellSize);
            }
            r.materials = mats;
        }
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

