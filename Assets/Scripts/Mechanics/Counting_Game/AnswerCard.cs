using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace KidGame.Mechanics.Counting
{
    [RequireComponent(typeof(CanvasGroup))]
    public class AnswerCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField] private Image    background;
        [SerializeField] private TMP_Text label;

        [Header("Drag")]
        [Tooltip("Pixels the card hovers ABOVE the finger so the number stays visible.")]
        [SerializeField] private float dragOffsetY  = 120f;
        [Tooltip("Duration of the snap-to-zone tween.")]
        [SerializeField] private float snapDuration  = 0.2f;
        [Tooltip("Duration of the return-to-tray tween.")]
        [SerializeField] private float returnDuration = 0.18f;

        // ── Public state ──────────────────────────────────────────────────────

        public int   Value      { get; private set; }
        public Color CardColor  { get; private set; }
        /// <summary>True once accepted by a drop zone. Accepted cards stay in the zone, not the tray.</summary>
        public bool  IsAccepted => _isAccepted;

        /// <summary>Called by the manager on orientation change — updates home so return-home works correctly.</summary>
        public void UpdateHomeParent(Transform newParent)
        {
            if (_isAccepted) return;
            _homeParent        = newParent;
            _homeWorldPosition = transform.position;
        }

        // ── Private state ─────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Canvas      _cachedCanvas;
        private Transform   _homeParent;
        private int         _homeSiblingIndex;
        private Vector3     _homeWorldPosition;
        private bool        _isAccepted;
        private bool        _isScrolling;
        private ScrollRect  _activeScrollRect;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void Setup(int value, Color color)
        {
            Value            = value;
            CardColor        = color;
            
            if (value == 32 || (value >= 65 && value <= 90) || (value >= 97 && value <= 122))
            {
                label.text = ((char)value).ToString();
            }
            else
            {
                label.text = value.ToString();
            }
            
            background.color = color;

            // Pop-in on spawn
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        // ── Drag Handlers ─────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;

            // Check if we should forward drag events to parent ScrollRect for scrolling
            var scrollRect = GetComponentInParent<ScrollRect>();
            _isScrolling = false;
            _activeScrollRect = null;

            if (scrollRect != null)
            {
                Vector2 totalDelta = eventData.position - eventData.pressPosition;
                if (scrollRect.vertical && !scrollRect.horizontal)
                {
                    if (Mathf.Abs(totalDelta.y) > Mathf.Abs(totalDelta.x))
                    {
                        _isScrolling = true;
                    }
                }
                else if (scrollRect.horizontal && !scrollRect.vertical)
                {
                    if (Mathf.Abs(totalDelta.x) > Mathf.Abs(totalDelta.y))
                    {
                        _isScrolling = true;
                    }
                }
                
                if (_isScrolling)
                {
                    _activeScrollRect = scrollRect;
                    _activeScrollRect.OnBeginDrag(eventData);
                    return;
                }
            }

            DOTween.Kill(transform);

            // Cache home state before reparenting
            _homeParent        = transform.parent;
            _homeSiblingIndex  = transform.GetSiblingIndex();
            _homeWorldPosition = transform.position;

            // Cache canvas before reparenting (GetComponentInParent only works on current hierarchy)
            var canvas = GetComponentInParent<Canvas>();
            _cachedCanvas = canvas?.rootCanvas;
            var root = _cachedCanvas != null ? _cachedCanvas.transform : _homeParent;
            transform.SetParent(root, worldPositionStays: true);

            // Disable raycasts through card so underlying drop zones stay hit-testable
            _canvasGroup.blocksRaycasts = false;

            // Slight scale-up: feels "picked up"
            transform.DOScale(Vector3.one * 1.08f, 0.12f).SetEase(Ease.OutSine);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;

            if (_isScrolling && _activeScrollRect != null)
            {
                _activeScrollRect.OnDrag(eventData);
                return;
            }

            // Offset the card above the thumb — child can always see the number
            transform.position = new Vector3(
                eventData.position.x,
                eventData.position.y + dragOffsetY,
                transform.position.z);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isScrolling && _activeScrollRect != null)
            {
                _activeScrollRect.OnEndDrag(eventData);
                _isScrolling = false;
                _activeScrollRect = null;
                return;
            }

            transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine);

            if (!_isAccepted)
            {
                // Raycast from the card's VISUAL CENTER (not the thumb position).
                // blocksRaycasts is still false here, so the card doesn't block its own raycast.
                var zone = FindDropZoneAtCardCenter();
                if (zone != null) zone.TryAccept(this);
            }

            // Re-enable after drop detection
            _canvasGroup.blocksRaycasts = true;

            if (!_isAccepted) ReturnHome();
        }

        // ── Public API (called by AnswerDropZone) ─────────────────────────────

        public void AcceptedByZone(Transform zoneTransform)
        {
            _isAccepted = true;
            DOTween.Kill(transform);

            transform.SetParent(zoneTransform, worldPositionStays: true);

            transform.DOMove(zoneTransform.position, snapDuration)
                     .SetEase(Ease.OutBack)
                     .OnComplete(() =>
                     {
                         // Stretch to fill the drop zone exactly — no overflow
                         var rt = GetComponent<RectTransform>();
                         if (rt != null)
                         {
                             rt.anchorMin = Vector2.zero;
                             rt.anchorMax = Vector2.one;
                             rt.offsetMin = Vector2.zero;
                             rt.offsetMax = Vector2.zero;
                         }
                         transform.DOPunchScale(Vector3.one * 0.2f, 0.35f, 6, 0.5f);
                     });
        }

        // ── Private Helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Fires a UI raycast from the card's world center converted to screen space.
        /// Returns the first AnswerDropZone hit, or null.
        /// </summary>
        private AnswerDropZone FindDropZoneAtCardCenter()
        {
            // Convert card world position → screen position
            Camera cam = (_cachedCanvas != null && _cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                ? _cachedCanvas.worldCamera : null;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);

            var fakeEvent = new PointerEventData(EventSystem.current) { position = screenPos };
            var results   = new List<RaycastResult>();
            EventSystem.current.RaycastAll(fakeEvent, results);

            foreach (var r in results)
            {
                var zone = r.gameObject.GetComponent<AnswerDropZone>();
                if (zone != null) return zone;
            }
            return null;
        }

        private void ReturnHome()
        {
            // Animate card back to its tray position, then reparent so the layout group takes over
            transform.DOMove(_homeWorldPosition, returnDuration)
                     .SetEase(Ease.OutCubic)
                     .OnComplete(() =>
                     {
                         transform.SetParent(_homeParent, worldPositionStays: false);
                         transform.SetSiblingIndex(_homeSiblingIndex);
                     });
        }
    }
}
