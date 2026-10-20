using UnityEngine;
using UnityEngine.UI;

public class MenuTriangle : MaskableGraphic
{
    [SerializeField] private bool filled = true;
    [Tooltip("Grosor del borde cuando no está relleno")]
    [SerializeField] private float thickness = 3f;

    public bool Filled
    {
        get => filled;
        set
        {
            if (filled == value) return;
            filled = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();
        Vector2 a = new Vector2(r.xMin, r.yMin);
        Vector2 b = new Vector2(r.xMin, r.yMax);
        Vector2 c = new Vector2(r.xMax, r.center.y);

        AddVert(vh, a);
        AddVert(vh, b);
        AddVert(vh, c);

        if (filled)
        {
            vh.AddTriangle(0, 1, 2);
            return;
        }

        // Triángulo interior más pequeño para dejar solo el borde
        float ab = Vector2.Distance(a, b);
        float bc = Vector2.Distance(b, c);
        float ca = Vector2.Distance(c, a);
        float perimeter = ab + bc + ca;
        float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
        float inradius = area / (perimeter * 0.5f);
        Vector2 incenter = (a * bc + b * ca + c * ab) / perimeter;
        float k = Mathf.Clamp01(1f - thickness / inradius);

        AddVert(vh, incenter + (a - incenter) * k);
        AddVert(vh, incenter + (b - incenter) * k);
        AddVert(vh, incenter + (c - incenter) * k);

        vh.AddTriangle(0, 1, 4);
        vh.AddTriangle(0, 4, 3);
        vh.AddTriangle(1, 2, 5);
        vh.AddTriangle(1, 5, 4);
        vh.AddTriangle(2, 0, 3);
        vh.AddTriangle(2, 3, 5);
    }

    private void AddVert(VertexHelper vh, Vector2 p)
    {
        vh.AddVert(p, color, Vector4.zero);
    }
}