using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

namespace KidGame.Mechanics.Comparison
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ComparisonCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;

        [Header("Drag settings")]
        [SerializeField] private float dragOffsetY = 120f;
        [SerializeField] private float snapDuration = 0.2f;

        public ComparisonSign Sign { get; private set; }
        public Color CardColor { get; private set; }
        public bool IsAccepted => _isAccepted;
        public bool IsClone => _isClone;

        private CanvasGroup _canvasGroup;
        private Canvas _cachedCanvas;
        private Transform _homeParent;
        private int _homeSiblingIndex;
        private Vector3 _homeWorldPosition;
        private bool _isAccepted;
        private bool _isClone;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }

        public void Setup(ComparisonSign sign, Color color, bool isClone = false)
        {
            Sign = sign;
            CardColor = color;
            _isClone = isClone;
            _isAccepted = false;

            if (background != null) background.color = color;
            if (label != null)
            {
                switch (sign)
                {
                    case ComparisonSign.LessThan:
                        label.text = "<";
                        break;
                    case ComparisonSign.EqualTo:
                        label.text = "=";
                        break;
                    case ComparisonSign.GreaterThan:
                        label.text = ">";
                        break;
                }
            }

            // Pop-in scale animation
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;

            if (!_isClone)
            {
                // Spawn a replacement card in the tray at our position
                GameObject cloneGo = Instantiate(gameObject, transform.parent);
                cloneGo.name = gameObject.name;
                
                var cloneCard = cloneGo.GetComponent<ComparisonCard>();
                cloneCard.Setup(Sign, CardColor, isClone: false);
                
                cloneGo.transform.SetSiblingIndex(transform.GetSiblingIndex());

                // Mark ourselves as the dragged clone
                _isClone = true;
            }

            DOTween.Kill(transform);

            _homeParent = transform.parent;
            _homeSiblingIndex = transform.GetSiblingIndex();
            _homeWorldPosition = transform.position;

            var canvas = GetComponentInParent<Canvas>();
            _cachedCanvas = canvas?.rootCanvas;
            var root = _cachedCanvas != null ? _cachedCanvas.transform : _homeParent;
            transform.SetParent(root, worldPositionStays: true);

            _canvasGroup.blocksRaycasts = false;
            transform.DOScale(Vector3.one * 1.08f, 0.12f).SetEase(Ease.OutSine);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isAccepted) return;

            transform.position = new Vector3(
                eventData.position.x,
                eventData.position.y + dragOffsetY,
                transform.position.z
            );
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutSine);

            if (!_isAccepted)
            {
                var zone = FindDropZoneAtCardCenter();
                if (zone != null)
                {
                    zone.TryAccept(this);
                }
            }

            _canvasGroup.blocksRaycasts = true;

            if (!_isAccepted)
            {
                // Shrink and self-destroy if not accepted
                transform.DOScale(Vector3.zero, 0.2f)
                         .SetEase(Ease.InBack)
                         .OnComplete(() => Destroy(gameObject));
            }
        }

        public void AcceptedByZone(Transform zoneTransform)
        {
            _isAccepted = true;
            DOTween.Kill(transform);

            transform.SetParent(zoneTransform, worldPositionStays: true);

            transform.DOMove(zoneTransform.position, snapDuration)
                     .SetEase(Ease.OutBack)
                     .OnComplete(() =>
                     {
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

        public void UpdateHomeParent(Transform newParent)
        {
            _homeParent = newParent;
            if (transform.parent != newParent)
            {
                transform.SetParent(newParent, false);
            }
        }

        private ComparisonDropZone FindDropZoneAtCardCenter()
        {
            Camera cam = (_cachedCanvas != null && _cachedCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                ? _cachedCanvas.worldCamera : null;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(cam, transform.position);

            var fakeEvent = new PointerEventData(EventSystem.current) { position = screenPos };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(fakeEvent, results);

            foreach (var r in results)
            {
                var zone = r.gameObject.GetComponent<ComparisonDropZone>();
                if (zone != null) return zone;
            }
            return null;
        }
    }
}
