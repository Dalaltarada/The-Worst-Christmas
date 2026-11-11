using UnityEngine;
using UnityEngine.UI;

public sealed class EyeOpeningGraphic : MaskableGraphic
{
    private float openness;

    public float Openness
    {
        get => openness;
        set { openness = Mathf.Clamp01(value); SetVerticesDirty(); }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var bounds = rectTransform.rect;
        const int segments = 64;
        for (int i = 0; i < segments; i++)
        {
            float left = i / (float)segments;
            float right = (i + 1) / (float)segments;
            float x0 = Mathf.Lerp(bounds.xMin, bounds.xMax, left);
            float x1 = Mathf.Lerp(bounds.xMin, bounds.xMax, right);
            float opening0 = OpeningHeight(left, bounds.height * 0.5f);
            float opening1 = OpeningHeight(right, bounds.height * 0.5f);
            Band(mesh, new Vector2(x0, bounds.yMax), new Vector2(x1, bounds.yMax),
                new Vector2(x1, bounds.center.y + opening1), new Vector2(x0, bounds.center.y + opening0));
            Band(mesh, new Vector2(x0, bounds.center.y - opening0), new Vector2(x1, bounds.center.y - opening1),
                new Vector2(x1, bounds.yMin), new Vector2(x0, bounds.yMin));
        }
    }

    private float OpeningHeight(float horizontal, float halfHeight)
    {
        float curve = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(horizontal * Mathf.PI)), 0.6f);
        return Mathf.Min(halfHeight, halfHeight * openness * Mathf.Lerp(curve, 1.05f, openness));
    }

    private void Band(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
