using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class JackWindingWheelGraphic : Graphic
{
    [SerializeField, Range(24, 96)] private int segments = 64;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .46f;
        float inner = radius - 5;
        Vector2 centre = rectTransform.rect.center;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
            Quad(vh, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * inner, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * inner);
        }
        for (int i = 0; i < 4; i++)
        {
            Vector2 d = new Vector2(Mathf.Cos(i * Mathf.PI / 2), Mathf.Sin(i * Mathf.PI / 2));
            Vector2 side = new Vector2(-d.y, d.x) * 2;
            Quad(vh, centre + side, centre + d * inner + side, centre + d * inner - side, centre - side);
        }
        Quad(vh, centre + new Vector2(-7,-7), centre + new Vector2(-7,7), centre + new Vector2(7,7), centre + new Vector2(7,-7));
        // A bright grip makes rotation visible even with four symmetric spokes.
        Quad(vh, centre + new Vector2(inner-14,-10), centre + new Vector2(inner-14,10), centre + new Vector2(inner+2,10), centre + new Vector2(inner+2,-10));
    }
    private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(i, i+1, i+2); vh.AddTriangle(i, i+2, i+3);
    }
}
