using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KidGame.Mechanics.Comparison
{
    public class ComparisonDropZone : MonoBehaviour
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
            if (card == null || card.IsAccepted) return;
            if (_isAnswered) return;

            if (card.Sign == _expectedSign)
            {
                Debug.Log($"[ComparisonGame] ✓ Correct! Zone expected {_expectedSign}, dropped {card.Sign}.");
                AcceptCard(card);
            }
            else
            {
                Debug.Log($"[ComparisonGame] ✗ Wrong! Zone expected {_expectedSign}, dropped {card.Sign}.");
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

        private void AcceptCard(ComparisonCard card)
        {
            _isAnswered = true;
            if (_background != null)
            {
                _background.color = card.CardColor;
            }
            card.AcceptedByZone(transform);

            if (KidGame.Interface.GameFlowManager.Instance != null)
            {
                KidGame.Interface.GameFlowManager.Instance.RegisterCorrectAnswer();
            }

            _onCorrect?.Invoke();
        }
    }
}
