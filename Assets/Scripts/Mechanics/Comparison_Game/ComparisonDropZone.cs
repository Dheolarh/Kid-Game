using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KidGame.Mechanics.Comparison
{
    public class ComparisonDropZone : MonoBehaviour, IDropHandler
    {
        private ComparisonSign _expectedSign;
        private bool _isAnswered;
        private Image _background;
        private System.Action _onCorrect;

        public bool IsAnswered => _isAnswered;

        private void Awake()
        {
            _background = GetComponent<Image>();
        }

        public void Setup(ComparisonSign expectedSign, System.Action onCorrect)
        {
            _expectedSign = expectedSign;
            _onCorrect = onCorrect;
            _isAnswered = false;
        }

        public void TryAccept(ComparisonCard card)
        {
            if (_isAnswered) return;

            if (card.Sign == _expectedSign)
            {
                Debug.Log($"[ComparisonGame] ✓ Correct! Zone expected {_expectedSign}, dropped {card.Sign}.");
                AcceptCard(card);
            }
            else
            {
                Debug.Log($"[ComparisonGame] ✗ Wrong! Zone expected {_expectedSign}, dropped {card.Sign}.");
                // The card handles its own destroy on failure since _isAccepted remains false
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            var card = eventData.pointerDrag?.GetComponent<ComparisonCard>();
            if (card != null)
            {
                TryAccept(card);
            }
        }

        private void AcceptCard(ComparisonCard card)
        {
            _isAnswered = true;
            if (_background != null)
            {
                _background.color = card.CardColor;
            }
            card.AcceptedByZone(transform);
            _onCorrect?.Invoke();
        }
    }
}
