using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KidGame.Mechanics.Counting
{
    public class AnswerDropZone : MonoBehaviour, IDropHandler
    {
        // ── State ─────────────────────────────────────────────────────────────

        private int                 _expectedCount;
        private bool                _isAnswered;
        private Image               _background;
        private System.Action       _onCorrect;
        private System.Func<bool>   _canAccept;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _background = GetComponent<Image>();
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        /// <param name="onCorrect">Invoked once when the correct card is dropped.</param>
        public void Setup(int expectedCount, System.Action onCorrect, System.Func<bool> canAccept = null)
        {
            _expectedCount = expectedCount;
            _onCorrect     = onCorrect;
            _canAccept     = canAccept;
            _isAnswered    = false;
        }

        // ── IDropHandler ──────────────────────────────────────────────────────

        /// <summary>
        /// Called directly by AnswerCard when it detects this zone under the card center.
        /// Also called by IDropHandler.OnDrop as a fallback.
        /// </summary>
        public void TryAccept(AnswerCard card)
        {
            if (_isAnswered) return;

            if (_canAccept != null && !_canAccept())
            {
                Debug.Log($"[CountingGame] ✗ Drop rejected: requirements not met.");
                return;
            }

            if (card.Value == _expectedCount)
            {
                Debug.Log($"[CountingGame] ✓ Correct! Slot expected {_expectedCount}, dropped {card.Value}.");
                AcceptCard(card);
            }
            else
            {
                Debug.Log($"[CountingGame] ✗ Wrong!   Slot expected {_expectedCount}, dropped {card.Value}. Card returns to tray.");
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            // Fallback: fired by Unity's event system when blocksRaycasts = false.
            // The primary path is TryAccept() called from AnswerCard.OnEndDrag via card-center raycast.
            var card = eventData.pointerDrag?.GetComponent<AnswerCard>();
            if (card != null) TryAccept(card);
        }

        // ── Private ───────────────────────────────────────────────────────────

        public bool IsAnswered => _isAnswered;

        private void AcceptCard(AnswerCard card)
        {
            _isAnswered       = true;
            _background.color = card.CardColor;
            card.AcceptedByZone(transform);
            _onCorrect?.Invoke();
        }
    }
}
