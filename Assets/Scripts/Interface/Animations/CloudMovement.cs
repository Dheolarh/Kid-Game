using UnityEngine;

namespace KidGame.Interface.Animations
{
    [RequireComponent(typeof(RectTransform))]
    public class CloudMovement : MonoBehaviour
    {
        [Tooltip("Horizontal speed. Positive moves right, negative moves left.")]
        [SerializeField] private float speed = 15f;

        [Tooltip("If true, the cloud starts at a random horizontal position on screen instead of its editor position.")]
        [SerializeField] private bool startAtRandomX = true;

        [Tooltip("Extra margin (in units/pixels) to slide completely off-screen before wrapping.")]
        [SerializeField] private float wrapPadding = 100f;

        private RectTransform _rectTransform;
        private RectTransform _parentRectTransform;

        private float _leftBoundary;
        private float _rightBoundary;
        private bool _isInitialized = false;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            _rectTransform = GetComponent<RectTransform>();
            _parentRectTransform = transform.parent as RectTransform;

            if (_parentRectTransform == null)
            {
                // Fallback to parent canvas if parent is not a RectTransform
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    _parentRectTransform = canvas.GetComponent<RectTransform>();
                }
            }

            CalculateBoundaries();

            if (startAtRandomX)
            {
                float randomX = Random.Range(_leftBoundary, _rightBoundary);
                _rectTransform.anchoredPosition = new Vector2(randomX, _rectTransform.anchoredPosition.y);
            }

            _isInitialized = true;
        }

        private void CalculateBoundaries()
        {
            if (_parentRectTransform == null) return;

            float parentWidth = _parentRectTransform.rect.width;
            float cloudWidth = _rectTransform.rect.width;

            // Boundaries are relative to parent's center (0,0) in typical UI layouts
            _leftBoundary = -parentWidth / 2f - cloudWidth / 2f - wrapPadding;
            _rightBoundary = parentWidth / 2f + cloudWidth / 2f + wrapPadding;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            // Recalculate boundaries in Update occasionally or when screen changes
            // (In a simple update, we can just rely on initial, but checking Screen dimensions ensures correctness)
            if (Screen.width != lastScreenWidth)
            {
                lastScreenWidth = Screen.width;
                CalculateBoundaries();
            }

            // Move the cloud
            Vector2 pos = _rectTransform.anchoredPosition;
            pos.x += speed * Time.deltaTime;

            // Wrap around boundaries
            if (speed > 0f)
            {
                if (pos.x > _rightBoundary)
                {
                    pos.x = _leftBoundary;
                }
            }
            else if (speed < 0f)
            {
                if (pos.x < _leftBoundary)
                {
                    pos.x = _rightBoundary;
                }
            }

            _rectTransform.anchoredPosition = pos;
        }

        private int lastScreenWidth = 0;
    }
}
