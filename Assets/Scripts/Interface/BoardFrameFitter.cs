using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class BoardFrameFitter : MonoBehaviour
{
    [SerializeField] RectTransform easelRect;
    [SerializeField] RectTransform frameRect;

    [Header("Easel Image Aspect Ratio")]
    [SerializeField] float easelNativeWidth  = 1080f;
    [SerializeField] float easelNativeHeight = 1920f;

    [Header("Board cutout in easel image (pixels from each edge)")]
    [SerializeField] float boardLeft   = 120f;
    [SerializeField] float boardRight  = 120f;
    [SerializeField] float boardTop    = 280f;
    [SerializeField] float boardBottom = 520f;

    [Header("Shared top offset (matches levelUIFrame)")]
    [SerializeField] float sharedTop = 150f;

    RectTransform _rt;

    void Awake() => _rt = GetComponent<RectTransform>();
    void Update() { if (Application.isEditor) Fit(); }
    void Start() => Fit();

    void Fit()
    {
        if (easelRect == null) return;

        Rect r = easelRect.rect;

        float frameAspect = r.width / r.height;
        float easelAspect = easelNativeWidth / easelNativeHeight;

        float renderedW, renderedH, offsetX, offsetY;

        if (frameAspect > easelAspect)
        {
            renderedH = r.height;
            renderedW = renderedH * easelAspect;
            offsetX   = (r.width - renderedW) / 2f;
            offsetY   = 0f;
        }
        else
        {
            renderedW = r.width;
            renderedH = renderedW / easelAspect;
            offsetX   = 0f;
            offsetY   = (r.height - renderedH) / 2f;
        }

        float scaleX = renderedW / easelNativeWidth;
        float scaleY = renderedH / easelNativeHeight;

        _rt.offsetMin = new Vector2(easelRect.offsetMin.x + offsetX + boardLeft   * scaleX,  easelRect.offsetMin.y + offsetY + boardBottom * scaleY);
        _rt.offsetMax = new Vector2(easelRect.offsetMax.x - (offsetX + boardRight * scaleX), easelRect.offsetMax.y - (offsetY + boardTop  * scaleY));

        if (frameRect != null)
        {
            frameRect.offsetMin = new Vector2(0f, 0f);
            frameRect.offsetMax = new Vector2(0f, -sharedTop);
        }
    }
}