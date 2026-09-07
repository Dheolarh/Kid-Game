using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KidGame.Mechanics.Counting
{
    public class AnswerDropZone : MonoBehaviour
    {
        // ── State ─────────────────────────────────────────────────────────────

        [SerializeField] private TMPro.TMP_Text hintText;

        private int                 _expectedCount;
        private bool                _isAnswered;
        private Image               _background;
        private System.Action       _onCorrect;
        private System.Func<bool>   _canAccept;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public TMPro.TMP_Text HintText { get => hintText; set => hintText = value; }

        private void Awake()
        {
            _background = GetComponent<Image>();
            if (hintText == null)
            {
                hintText = GetComponentInChildren<TMPro.TMP_Text>(true);
            }
        }

        // ── Setup ─────────────────────────────────────────────────────────────

        /// <param name="onCorrect">Invoked once when the correct card is dropped.</param>
        public void Setup(int expectedCount, System.Action onCorrect, System.Func<bool> canAccept = null, string customHint = null, bool showHint = true)
        {
            _expectedCount = expectedCount;
            _onCorrect     = onCorrect;
            _canAccept     = canAccept;
            _isAnswered    = false;

            var recallHintDisplay = GetComponent<NumberRecall.NumberRecallHintDisplay>();
            if (recallHintDisplay == null) recallHintDisplay = GetComponentInChildren<NumberRecall.NumberRecallHintDisplay>(true);

            if (recallHintDisplay != null)
            {
                string textToDisplay = !string.IsNullOrEmpty(customHint) ? customHint : expectedCount.ToString();
                recallHintDisplay.SetupHint(textToDisplay, showHint);
            }
            else
            {
                if (hintText == null)
                {
                    hintText = GetComponentInChildren<TMPro.TMP_Text>(true);
                }

                if (hintText != null)
                {
                    hintText.gameObject.SetActive(showHint);
                    if (showHint)
                    {
                        hintText.color = new Color(0.25f, 0.25f, 0.25f, 0.45f); // Visible ghost text
                        if (!string.IsNullOrEmpty(customHint))
                        {
                            hintText.text = customHint;
                        }
                        else
                        {
                            hintText.text = expectedCount.ToString();
                        }
                    }
                }
            }
        }

        // ── Drop Handling ─────────────────────────────────────────────────────

        /// <summary>
        /// Called directly by AnswerCard.OnEndDrag when it detects this zone under the card's visual center.
        /// </summary>
        public void TryAccept(AnswerCard card)
        {
            if (card == null || card.IsAccepted) return;
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
                if (KidGame.Interface.GameFlowManager.Instance != null)
                {
                    KidGame.Interface.GameFlowManager.Instance.RegisterMistake();
                }
                TriggerWrongFeedback();
            }
        }

        // ── Wrong Drop Feedback (Vibration & Red Outline Flash) ────────────────

        private Coroutine _wrongFeedbackCoroutine;

        /// <summary>
        /// Vibrates/shakes the slot and flashes its outline(s) red on an incorrect drop.
        /// </summary>
        public void TriggerWrongFeedback(float duration = 0.4f, float magnitude = 10f)
        {
            var randomizer = GetComponent<KidGame.Interface.ColorRandomizer>();
            if (randomizer == null) randomizer = GetComponentInParent<KidGame.Interface.ColorRandomizer>();
            if (randomizer != null)
            {
                randomizer.FlashWrongMismatch(duration, magnitude);
                return;
            }

            if (_wrongFeedbackCoroutine != null) StopCoroutine(_wrongFeedbackCoroutine);
            _wrongFeedbackCoroutine = StartCoroutine(WrongFeedbackCoroutine(duration, magnitude));
        }

        private System.Collections.IEnumerator WrongFeedbackCoroutine(float duration, float magnitude)
        {
            var outlines = GetComponentsInChildren<Outline>(true);
            var parentOutlines = transform.parent != null ? transform.parent.GetComponents<Outline>() : null;

            var allOutlines = new System.Collections.Generic.List<Outline>();
            if (outlines != null) allOutlines.AddRange(outlines);
            if (parentOutlines != null) allOutlines.AddRange(parentOutlines);

            var originalColors = new System.Collections.Generic.Dictionary<Outline, Color>();
            foreach (var o in allOutlines)
            {
                if (o != null && !originalColors.ContainsKey(o))
                {
                    originalColors[o] = o.effectColor;
                    o.effectColor = new Color(0.95f, 0.2f, 0.2f, 1f);
                }
            }

            RectTransform rt = GetComponent<RectTransform>();
            Vector2 originalPos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector3 originalLocalPos = transform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float decay = 1f - (elapsed / duration);
                float xOffset = Random.Range(-1f, 1f) * magnitude * decay;
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(originalPos.x + xOffset, originalPos.y);
                }
                else
                {
                    transform.localPosition = new Vector3(originalLocalPos.x + xOffset, originalLocalPos.y, originalLocalPos.z);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (rt != null) rt.anchoredPosition = originalPos;
            else transform.localPosition = originalLocalPos;

            foreach (var kvp in originalColors)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.effectColor = kvp.Value;
                }
            }
            _wrongFeedbackCoroutine = null;
        }

        // ── Private ───────────────────────────────────────────────────────────

        public bool IsAnswered => _isAnswered;

        private void AcceptCard(AnswerCard card)
        {
            _isAnswered = true;

            var recallSlot = GetComponent<NumberRecall.RecallAnswerSlot>();
            if (recallSlot == null) recallSlot = GetComponentInParent<NumberRecall.RecallAnswerSlot>();

            if (recallSlot != null)
            {
                recallSlot.OnCorrectAnswerDropped(card, _onCorrect);
                return;
            }

            var letterSlot = GetComponent<LetterAnswerSlot>();
            if (letterSlot == null) letterSlot = GetComponentInParent<LetterAnswerSlot>();

            if (letterSlot != null)
            {
                letterSlot.OnCorrectAnswerDropped(card, _onCorrect);
                return;
            }

            _background.color = card.CardColor;
            if (hintText != null)
            {
                hintText.gameObject.SetActive(false);
            }
            card.AcceptedByZone(transform);

            // Play correct answer card drop SFX
            KidGame.Audio.AudioManager.Instance?.PlayAnswerDropSfx();

            // Play number voice audio for the correct number dropped
            KidGame.Audio.AudioManager.Instance?.PlayNumberVoice(card.Value.ToString());

            if (KidGame.Interface.GameFlowManager.Instance != null)
            {
                KidGame.Interface.GameFlowManager.Instance.RegisterCorrectAnswer();
            }

            _onCorrect?.Invoke();
        }

    }
}
