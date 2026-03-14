using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Плавно анимированный градиент для UI Image. Модифицирует вертексные цвета —
/// border (9-slice) остаётся нетронутым, потому что тонируются только вершины меша.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UIGradientAnimation : BaseMeshEffect
{
    [Header("Gradient")]
    [Tooltip("Цветовой градиент. Задайте несколько ключей для красивого перелива.")]
    [SerializeField] private Gradient gradient = new Gradient();

    [Header("Direction")]
    [Tooltip("Угол градиента (0 = →, 90 = ↑, 45 = диагональ).")]
    [SerializeField] private float angle = 0f;

    [Header("Animation")]
    [Tooltip("Скорость сдвига градиента (циклов в секунду).")]
    [SerializeField] private float speed = 0.15f;
    [Tooltip("Масштаб градиента: 1 = один проход по всей ширине, 2 = два повтора и т.д.")]
    [SerializeField] private float scale = 1f;

    private float _offset;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (gradient.colorKeys.Length < 2)
        {
            gradient.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.45f, 0.2f, 0.85f), 0f),
                    new GradientColorKey(new Color(0.15f, 0.6f, 0.95f), 0.33f),
                    new GradientColorKey(new Color(0.95f, 0.3f, 0.55f), 0.66f),
                    new GradientColorKey(new Color(0.45f, 0.2f, 0.85f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
        }
    }

    private void Update()
    {
        _offset += speed * Time.deltaTime;
        if (_offset > 1f) _offset -= 1f;
        if (graphic != null)
            graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);
        if (verts.Count == 0) return;

        float minProj = float.MaxValue;
        float maxProj = float.MinValue;

        float rad = angle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        for (int i = 0; i < verts.Count; i++)
        {
            float proj = Vector2.Dot(new Vector2(verts[i].position.x, verts[i].position.y), dir);
            if (proj < minProj) minProj = proj;
            if (proj > maxProj) maxProj = proj;
        }

        float range = maxProj - minProj;
        if (range < 0.001f) range = 1f;

        for (int i = 0; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            float proj = Vector2.Dot(new Vector2(v.position.x, v.position.y), dir);
            float t = (proj - minProj) / range;
            t = t * scale + _offset;
            t = t - Mathf.Floor(t);

            Color gc = gradient.Evaluate(t);
            v.color = gc * v.color;
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
