using UnityEngine;
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class AutoFitter : MonoBehaviour
{
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

    [Header("Source image native pixel dimensions (match your sprite)")]
    public float nativeWidth  = 1038f;
    public float nativeHeight = 1003f;

    [Header("Children to position inside the image")]
    public ChildEntry[] children;

    RectTransform _rt;

    void Awake()  => _rt = GetComponent<RectTransform>();
    void Start()  => Fit();
    void Update() { if (!Application.isPlaying) Fit(); }
    void OnRectTransformDimensionsChange() => Fit();

    void Fit()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (children == null || nativeWidth <= 0f || nativeHeight <= 0f) return;

        Rect r = _rt.rect;
        if (r.width <= 0f || r.height <= 0f) return;

        float containerAspect = r.width / r.height;
        float imageAspect     = nativeWidth / nativeHeight;

        float renderedW, renderedH, offsetX, offsetY;

        // Compute how the image fits inside the container with Preserve Aspect
        if (containerAspect > imageAspect)
        {
            // Container is wider than image - pillarboxed: fit by height
            renderedH = r.height;
            renderedW = renderedH * imageAspect;
            offsetX   = (r.width - renderedW) / 2f;
            offsetY   = 0f;
        }
        else
        {
            // Container is taller than image - letterboxed: fit by width
            renderedW = r.width;
            renderedH = renderedW / imageAspect;
            offsetX   = 0f;
            offsetY   = (r.height - renderedH) / 2f;
        }

        foreach (var c in children)
        {
            if (c == null || c.target == null) continue;

            // Ensure center anchors so anchoredPosition is the center offset from parent pivot
            c.target.anchorMin = new Vector2(0.5f, 0.5f);
            c.target.anchorMax = new Vector2(0.5f, 0.5f);
            c.target.pivot     = new Vector2(0.5f, 0.5f);

            // Pixel position of child centre within the container rect
            // rendered rect spans: X from offsetX to offsetX+renderedW
            //                      Y from offsetY to offsetY+renderedH
            // normX/normY are 0-1 within the rendered rect
            float pixelX = offsetX + c.normX * renderedW;
            float pixelY = offsetY + c.normY * renderedH;

            // anchoredPosition is relative to parent pivot (centre of container when anchor=0.5)
            float anchoredX = pixelX - r.width  * 0.5f;
            float anchoredY = pixelY - r.height * 0.5f;

            c.target.anchoredPosition = new Vector2(anchoredX, anchoredY);
            c.target.sizeDelta        = new Vector2(c.normW * renderedW, c.normH * renderedH);
        }
    }

    /// <summary>
    /// Call this from the Inspector context menu once children are positioned correctly
    /// in the Editor at the reference resolution to auto-compute normX/Y/W/H values.
    /// </summary>
    [ContextMenu("Capture From Scene")]
    public void CaptureFromScene()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();

        Rect r = _rt.rect;
        if (r.width <= 0f || r.height <= 0f || nativeWidth <= 0f || nativeHeight <= 0f)
        {
            Debug.LogWarning("[AutoFitter] Container or native size is zero - cannot capture.");
            return;
        }

        float containerAspect = r.width / r.height;
        float imageAspect     = nativeWidth / nativeHeight;

        float renderedW, renderedH, offsetX, offsetY;

        if (containerAspect > imageAspect)
        {
            renderedH = r.height;
            renderedW = renderedH * imageAspect;
            offsetX   = (r.width - renderedW) / 2f;
            offsetY   = 0f;
        }
        else
        {
            renderedW = r.width;
            renderedH = renderedW / imageAspect;
            offsetX   = 0f;
            offsetY   = (r.height - renderedH) / 2f;
        }

        foreach (var c in children)
        {
            if (c == null || c.target == null) continue;

            // Read current world position relative to container
            Vector3 localPos = _rt.InverseTransformPoint(c.target.position);

            // Convert from container-local (origin = bottom-left when using rect) to pixel within container
            float pixelX = localPos.x + r.width  * 0.5f;
            float pixelY = localPos.y + r.height * 0.5f;

            // Convert to normalised coords within rendered image
            c.normX = (pixelX - offsetX) / renderedW;
            c.normY = (pixelY - offsetY) / renderedH;

            // Size
            c.normW = c.target.rect.width  / renderedW;
            c.normH = c.target.rect.height / renderedH;

            // Clamp to 0-1 for sanity
            c.normX = Mathf.Clamp01(c.normX);
            c.normY = Mathf.Clamp01(c.normY);
            c.normW = Mathf.Clamp01(c.normW);
            c.normH = Mathf.Clamp01(c.normH);
        }

        Debug.Log($"[AutoFitter] Captured {children.Length} children. rendered={renderedW:F1}x{renderedH:F1} offset=({offsetX:F1},{offsetY:F1})");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
        if (!Application.isPlaying && gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }
}