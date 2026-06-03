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

        public void OnDrop(PointerEventData eventData)
        {
            if (_isAnswered) return;

            var card = eventData.pointerDrag?.GetComponent<AnswerCard>();
            if (card == null) return;

            if (card.Value == _expectedCount)
            {
                Debug.Log($"[CountingGame] ✓ Correct! Slot expected {_expectedCount}, dropped {card.Value}.");
                AcceptCard(card);
            }
            else
            {
                Debug.Log($"[CountingGame] ✗ Wrong!   Slot expected {_expectedCount}, dropped {card.Value}. Card returns to tray.");
                // Card returns home automatically via AnswerCard.ReturnHomeIfNotAccepted().
            }
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
