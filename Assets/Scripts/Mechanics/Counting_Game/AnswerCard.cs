using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace KidGame.Mechanics.Counting
{
    /// <summary>
    /// A draggable answer card showing a number in a colored rounded box.
    ///
    /// Expected prefab structure:
    /// <code>
    /// AnswerCard (root — Image + CanvasGroup + this script)
    /// └── Label  (TMP_Text — the number)
    /// </code>
    ///
    /// Drag behaviour:
    ///  • OnBeginDrag — reparent to canvas root so it renders above everything.
    ///  • OnDrag      — follow the pointer.
    ///  • OnEndDrag   — yield one frame (so AnswerDropZone.OnDrop fires first),
    ///                  then DOTween back home if not accepted.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AnswerCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [SerializeField] private Image    background;
        [SerializeField] private TMP_Text label;

        [Header("Animation")]
        [Tooltip("Duration of the snap-to-zone tween.")]
        [SerializeField] private float snapDuration   = 0.2f;
        [Tooltip("Duration of the return-to-tray tween.")]
        [SerializeField] private float returnDuration = 0.18f;

        // ── Public state ──────────────────────────────────────────────────────

        public int   Value     { get; private set; }
        public Color CardColor { get; private set; }

        // ── Private state ─────────────────────────────────────────────────────

        private CanvasGroup _canvasGroup;
        private Transform   _homeParent;
        private int         _homeSiblingIndex;
        private Vector3     _homeWorldPosition;
        private bool        _isAccepted;

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
            label.text       = value.ToString();
            background.color = color;

            // Pop in on spawn
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        // ── Drag Handlers ─────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;

            // Kill any in-progress return animation
            DOTween.Kill(transform);

            // Cache home before reparenting
            _homeParent        = transform.parent;
            _homeSiblingIndex  = transform.GetSiblingIndex();
            _homeWorldPosition = transform.position;

            // Reparent to canvas root → renders above all other UI
            var canvas = GetComponentInParent<Canvas>();
            var root   = canvas != null ? canvas.rootCanvas.transform : _homeParent;
            transform.SetParent(root, worldPositionStays: true);

            // Allow raycasts through the card so drop zones can receive them
            _canvasGroup.blocksRaycasts = false;

            // Slight scale-up while dragging so it feels "picked up"
            transform.DOScale(Vector3.one * 1.08f, 0.12f).SetEase(Ease.OutSine);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _canvasGroup.blocksRaycasts = true;
            transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine);

            // OnDrop (on the drop zone) fires in the same frame but AFTER OnEndDrag.
            // Yield one frame so AcceptedByZone() has a chance to set _isAccepted = true
            // before we decide to return home.
            if (!_isAccepted)
                StartCoroutine(ReturnHomeIfNotAccepted());
        }

        // ── Public API (called by AnswerDropZone) ─────────────────────────────

        /// <summary>Called by the drop zone when this card's value is correct.</summary>
        public void AcceptedByZone(Transform zoneTransform)
        {
            _isAccepted = true;
            DOTween.Kill(transform);

            transform.SetParent(zoneTransform, worldPositionStays: true);

            // Snap to zone center, then stretch-fill it and punch scale
            transform.DOMove(zoneTransform.position, snapDuration)
                     .SetEase(Ease.OutBack)
                     .OnComplete(() =>
                     {
                         // Stretch to fill the drop zone exactly — no overflow
                         var rt = GetComponent<RectTransform>();
                         if (rt != null)
                         {
                             rt.anchorMin        = Vector2.zero;
                             rt.anchorMax        = Vector2.one;
                             rt.offsetMin        = Vector2.zero;   // left / bottom padding
                             rt.offsetMax        = Vector2.zero;   // right / top padding
                         }
                         transform.DOPunchScale(Vector3.one * 0.2f, 0.35f, 6, 0.5f);
                     });
        }

        // ── Coroutines ────────────────────────────────────────────────────────

        private IEnumerator ReturnHomeIfNotAccepted()
        {
            // One frame: let OnDrop fire and potentially set _isAccepted = true
            yield return null;
            if (_isAccepted) yield break;

            // Animate back to cached home world position
            transform.DOMove(_homeWorldPosition, returnDuration)
                     .SetEase(Ease.OutCubic)
                     .OnComplete(() =>
                     {
                         // Reparent back into the tray — layout group repositions it
                         transform.SetParent(_homeParent, worldPositionStays: false);
                         transform.SetSiblingIndex(_homeSiblingIndex);
                     });
        }
    }
}
