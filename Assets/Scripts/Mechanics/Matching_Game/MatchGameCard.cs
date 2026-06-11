using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KidGame.Mechanics.Matching
{
    public class MatchGameCard : MonoBehaviour, IPointerDownHandler
    {
        private int _matchId;
        private bool _isLeftCard;
        private MatchGameManager _manager;
        private RectTransform _rectTransform;
        private readonly List<Outline> _originalOutlines = new List<Outline>();
        private Outline _highlightOutline;
        private bool _isShaking = false;
        private Coroutine _scaleCoroutine;

        public int MatchId => _matchId;
        public bool IsLeftCard => _isLeftCard;
        public RectTransform RectTransform => _rectTransform;
        public bool IsMatched { get; private set; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Setup(int id, bool left, MatchGameManager manager)
        {
            _matchId = id;
            _isLeftCard = left;
            _manager = manager;
            IsMatched = false;
            
            // Find all existing outlines (which are the original ones)
            _originalOutlines.Clear();
            var outlines = GetComponents<Outline>();
            foreach (var o in outlines)
            {
                if (o == _highlightOutline) continue;
                _originalOutlines.Add(o);
                o.enabled = true; // Restore original outlines to default visible state
            }
            
            if (_highlightOutline != null) _highlightOutline.enabled = false;
            transform.localScale = Vector3.one;
        }

        private void EnsureHighlightOutline()
        {
            if (_highlightOutline == null)
            {
                _highlightOutline = gameObject.AddComponent<Outline>();
                _highlightOutline.enabled = false;
            }
        }

        public void SetSelected(bool selected)
        {
            if (IsMatched || _isShaking) return;
            EnsureHighlightOutline();
            if (selected)
            {
                // Disable original outlines so they don't show black edges
                foreach (var o in _originalOutlines) if (o) o.enabled = false;

                _highlightOutline.enabled = true;
                _highlightOutline.effectColor = Color.yellow;
                _highlightOutline.effectDistance = new Vector2(3f, 3f);
                StartScaleAnimation(new Vector3(1.1f, 1.1f, 1f));
            }
            else
            {
                _highlightOutline.enabled = false;

                // Re-enable original outlines
                foreach (var o in _originalOutlines) if (o) o.enabled = true;

                StartScaleAnimation(Vector3.one);
            }
        }

        public void SetMatched(Color outlineColor)
        {
            IsMatched = true;
            EnsureHighlightOutline();

            // Disable original outlines so they don't show black edges
            foreach (var o in _originalOutlines) if (o) o.enabled = false;

            _highlightOutline.enabled = true;
            _highlightOutline.effectColor = outlineColor;
            _highlightOutline.effectDistance = new Vector2(3f, 3f);
            
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            transform.localScale = Vector3.one;
        }

        public void ShowMismatch()
        {
            if (_isShaking) return;
            StartCoroutine(ShakeCoroutine());
        }

        private void StartScaleAnimation(Vector3 targetScale)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleCoroutine(targetScale));
        }

        private System.Collections.IEnumerator ScaleCoroutine(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float duration = 0.15f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.localScale = targetScale;
        }

        private System.Collections.IEnumerator ShakeCoroutine()
        {
            _isShaking = true;
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);

            EnsureHighlightOutline();

            // Disable original outlines during shake
            foreach (var o in _originalOutlines) if (o) o.enabled = false;

            _highlightOutline.enabled = true;
            _highlightOutline.effectColor = Color.red;
            _highlightOutline.effectDistance = new Vector2(3f, 3f);

            Vector3 startScale = transform.localScale;
            Vector2 originalPos = _rectTransform.anchoredPosition;
            float duration = 0.4f;
            float magnitude = 10f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float xOffset = Random.Range(-1f, 1f) * magnitude;
                _rectTransform.anchoredPosition = new Vector2(originalPos.x + xOffset, originalPos.y);
                
                // Smoothly scale back to normal during the shake
                transform.localScale = Vector3.Lerp(startScale, Vector3.one, elapsed / duration);
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            _rectTransform.anchoredPosition = originalPos;
            _highlightOutline.enabled = false;

            // Restore original outlines after mismatch shake is finished
            foreach (var o in _originalOutlines) if (o) o.enabled = true;

            transform.localScale = Vector3.one;
            _isShaking = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsMatched || _isShaking || _manager == null) return;
            _manager.OnCardSelected(this);
        }
    }
}
