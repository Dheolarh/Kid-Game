using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class AutoFitter : MonoBehaviour
{
    public enum ImageFitMode
    {
        [Tooltip("The Image component has 'Preserve Aspect' checked — Unity letterboxes/pillarboxes the sprite inside the container.")]
        PreserveAspect,

        [Tooltip("The Image component stretches to fill the whole RectTransform — no letterboxing. normX/Y/W/H are relative to the full container rect.")]
        StretchToFill
    }

    [System.Serializable]
    public class ChildEntry
    {
        public RectTransform target;

        [Tooltip("Normalised centre X position inside the rendered image (0 = left edge, 1 = right edge).")]
        [Range(0f, 1f)] public float normX = 0.5f;
        [Tooltip("Normalised centre Y position inside the rendered image (0 = bottom edge, 1 = top edge).")]
        [Range(0f, 1f)] public float normY = 0.5f;
        [Tooltip("Width as a fraction of the rendered image width.")]
        [Range(0f, 1f)] public float normW = 0.1f;
        [Tooltip("Height as a fraction of the rendered image height.")]
        [Range(0f, 1f)] public float normH = 0.1f;
    }

    [Header("How the Image component fits inside this RectTransform")]
    public ImageFitMode fitMode = ImageFitMode.PreserveAspect;

    [Header("Source image native pixel dimensions (match your sprite) — only used in PreserveAspect mode")]
    public float nativeWidth  = 1038f;
    public float nativeHeight = 1003f;

    [Header("Children to position inside the image")]
    public ChildEntry[] children;

    RectTransform _rt;

    void Awake()  => _rt = GetComponent<RectTransform>();
    void Start()  => Fit();
    void Update() { if (!Application.isPlaying) Fit(); }
    void OnRectTransformDimensionsChange() => Fit();

    // ─────────────────────────────────────────────────────────────────────────
    // Compute the rendered rect (in container-local pixels, origin = bottom-left)
    // ─────────────────────────────────────────────────────────────────────────
    void GetRenderedRect(Rect r, out float renderedW, out float renderedH, out float offsetX, out float offsetY)
    {
        if (fitMode == ImageFitMode.StretchToFill)
        {
            // Image fills the whole container — no letterboxing, no offset
            renderedW = r.width;
            renderedH = r.height;
            offsetX   = 0f;
            offsetY   = 0f;
            return;
        }

        // PreserveAspect — compute letterbox / pillarbox
        float containerAspect = r.width / r.height;
        float imageAspect     = nativeWidth / nativeHeight;

        if (containerAspect > imageAspect)
        {
            // Container wider than image → pillarboxed, fit by height
            renderedH = r.height;
            renderedW = renderedH * imageAspect;
            offsetX   = (r.width - renderedW) / 2f;
            offsetY   = 0f;
        }
        else
        {
            // Container taller than image → letterboxed, fit by width
            renderedW = r.width;
            renderedH = renderedW / imageAspect;
            offsetX   = 0f;
            offsetY   = (r.height - renderedH) / 2f;
        }
    }

    void Fit()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (children == null) return;
        if (fitMode == ImageFitMode.PreserveAspect && (nativeWidth <= 0f || nativeHeight <= 0f)) return;

        Rect r = _rt.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        GetRenderedRect(r, out float renderedW, out float renderedH, out float offsetX, out float offsetY);

        foreach (var c in children)
        {
            if (c == null || c.target == null) continue;

            // Center anchors so anchoredPosition is the center offset from parent pivot
            c.target.anchorMin = new Vector2(0.5f, 0.5f);
            c.target.anchorMax = new Vector2(0.5f, 0.5f);
            c.target.pivot     = new Vector2(0.5f, 0.5f);

            // Pixel centre of this child within the container rect
            float pixelX = offsetX + c.normX * renderedW;
            float pixelY = offsetY + c.normY * renderedH;

            // anchoredPosition is relative to parent pivot (centre of container when anchor = 0.5)
            float anchoredX = pixelX - r.width  * 0.5f;
            float anchoredY = pixelY - r.height * 0.5f;

            c.target.anchoredPosition = new Vector2(anchoredX, anchoredY);
            c.target.sizeDelta        = new Vector2(c.normW * renderedW, c.normH * renderedH);
        }
    }

    /// <summary>
    /// Call from the Inspector context menu once children are positioned correctly
    /// in the Editor at the reference resolution to auto-compute normX/Y/W/H values.
    /// </summary>
    [ContextMenu("Capture From Scene")]
    public void CaptureFromScene()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();

        Rect r = _rt.rect;
        if (r.width <= 0f || r.height <= 0f)
        {
            Debug.LogWarning("[AutoFitter] Container size is zero - cannot capture.");
            return;
        }
        if (fitMode == ImageFitMode.PreserveAspect && (nativeWidth <= 0f || nativeHeight <= 0f))
        {
            Debug.LogWarning("[AutoFitter] Native size is zero - cannot capture in PreserveAspect mode.");
            return;
        }

        GetRenderedRect(r, out float renderedW, out float renderedH, out float offsetX, out float offsetY);

        foreach (var c in children)
        {
            if (c == null || c.target == null) continue;

            // Read current position relative to container
            Vector3 localPos = _rt.InverseTransformPoint(c.target.position);

            // Convert from container-local (origin = centre) to pixel within container (origin = bottom-left)
            float pixelX = localPos.x + r.width  * 0.5f;
            float pixelY = localPos.y + r.height * 0.5f;

            // Normalise within the rendered image
            c.normX = (pixelX - offsetX) / renderedW;
            c.normY = (pixelY - offsetY) / renderedH;

            // Size
            c.normW = c.target.rect.width  / renderedW;
            c.normH = c.target.rect.height / renderedH;

            // Clamp for sanity
            c.normX = Mathf.Clamp01(c.normX);
            c.normY = Mathf.Clamp01(c.normY);
            c.normW = Mathf.Clamp01(c.normW);
            c.normH = Mathf.Clamp01(c.normH);
        }

        Debug.Log($"[AutoFitter] Captured {children.Length} children. mode={fitMode} rendered={renderedW:F1}x{renderedH:F1} offset=({offsetX:F1},{offsetY:F1})");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying && gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}
