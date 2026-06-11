using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Mechanics.Matching
{
    [RequireComponent(typeof(RectTransform))]
    public class MatchGameLine : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Image _image;
        private float _progress = 0f;
        private Coroutine _animateCoroutine;

        private Vector2 _startPos;
        private Vector2 _endPos;

        public Vector2 StartPos => _startPos;
        public Vector2 EndPos => _endPos;
        public float Progress => _progress;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
        }

        private void OnEnable()
        {
            StartAnimate();
        }

        public void SetColor(Color color)
        {
            if (_image != null) _image.color = color;
        }

        public void SetProgress(float progress)
        {
            if (_animateCoroutine != null) StopCoroutine(_animateCoroutine);
            _progress = Mathf.Clamp01(progress);
        }

        public void StartAnimate()
        {
            if (_animateCoroutine != null) StopCoroutine(_animateCoroutine);
            _animateCoroutine = StartCoroutine(AnimateLineCoroutine());
        }

        private System.Collections.IEnumerator AnimateLineCoroutine()
        {
            _progress = 0f;
            float duration = 0.35f; // Duration of line drawing animation
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _progress = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            _progress = 1f;
        }

        /// <summary>
        /// Configures the line to stretch from startPos to endPos.
        /// Assumes the line's RectTransform pivot is set to (0f, 0.5f).
        /// </summary>
        public void SetPoints(Vector2 startPos, Vector2 endPos, float thickness)
        {
            _startPos = startPos;
            _endPos = endPos;

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            Vector2 dir = endPos - startPos;
            Vector2 currentEnd = startPos + dir * _progress;

            Vector2 currentDir = currentEnd - startPos;
            float length = currentDir.magnitude;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // Base rotation off the full line direction for stability

            // Enforce pivot for the stretching line
            _rectTransform.pivot = new Vector2(0f, 0.5f);

            _rectTransform.localPosition = new Vector3(startPos.x, startPos.y, 0f);
            _rectTransform.sizeDelta = new Vector2(length, thickness);
            _rectTransform.localRotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
