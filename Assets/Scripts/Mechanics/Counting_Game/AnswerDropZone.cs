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
        private CountingSlot        _ownerSlot;
        private CountingGameManager _manager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _background = GetComponent<Image>();
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        public void Setup(int expectedCount, CountingSlot slot, CountingGameManager manager)
        {
            _expectedCount = expectedCount;
            _ownerSlot     = slot;
            _manager       = manager;
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

        private void AcceptCard(AnswerCard card)
        {
            _isAnswered       = true;
            _background.color = card.CardColor;   // zone takes the card's color
            card.AcceptedByZone(transform);
            _manager.OnSlotAnswered(_ownerSlot);
        }
    }
}
