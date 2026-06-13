using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/Outline Gradient")]
public class UIOutlineGradient : Shadow
{
    public Color m_color1 = Color.white;
    public Color m_color2 = Color.white;
    [Range(-180f, 180f)]
    public float m_angle = 0f;
    public bool m_ignoreRatio = true;

    protected UIOutlineGradient()
    {
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive())
            return;

        var verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        int originalCount = verts.Count;
        if (originalCount == 0)
            return;

        Rect rect = graphic.rectTransform.rect;
        Vector2 dir = UIGradientUtils.RotationDir(m_angle);
        if (!m_ignoreRatio)
            dir = UIGradientUtils.CompensateAspectRatio(rect, dir);

        UIGradientUtils.Matrix2x3 localPositionMatrix = UIGradientUtils.LocalPositionMatrix(rect, dir);

        Vector2 distance = effectDistance;

        // Keep a copy of the original vertices (which includes the graphic and any previous modifiers)
        var originalVerts = new List<UIVertex>(verts);
        verts.Clear();
        verts.Capacity = originalCount * 5;

        // Apply shadows in the four diagonal directions to create the outline
        ApplyShadowVertices(verts, originalVerts, distance.x, distance.y, localPositionMatrix);
        ApplyShadowVertices(verts, originalVerts, distance.x, -distance.y, localPositionMatrix);
        ApplyShadowVertices(verts, originalVerts, -distance.x, distance.y, localPositionMatrix);
        ApplyShadowVertices(verts, originalVerts, -distance.x, -distance.y, localPositionMatrix);

        // Add the original vertices back at the end so they draw on top of the outlines
        verts.AddRange(originalVerts);

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void ApplyShadowVertices(List<UIVertex> target, List<UIVertex> source, float x, float y, UIGradientUtils.Matrix2x3 localPositionMatrix)
    {
        for (int i = 0; i < source.Count; i++)
        {
            UIVertex vt = source[i];

            Vector3 pos = vt.position;
            pos.x += x;
            pos.y += y;
            vt.position = pos;

            Vector2 localPosition = localPositionMatrix * vt.position;
            Color gradientColor = Color.Lerp(m_color2, m_color1, localPosition.y);

            Color32 finalColor = gradientColor;
            if (useGraphicAlpha)
            {
                finalColor.a = (byte)((finalColor.a * vt.color.a) / 255);
            }
            else
            {
                finalColor.a = (byte)(effectColor.a * 255f);
            }
            vt.color = finalColor;

            target.Add(vt);
        }
    }
}
