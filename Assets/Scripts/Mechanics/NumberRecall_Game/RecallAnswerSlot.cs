using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.NumberRecall
{
    /// <summary>
    /// Component attached directly to an answer slot in Number Recall game.
    /// Holds text, image, expected answer value, hint flag, and answer slot flag.
    /// When a correct answer card is dropped, the card disappears, the slot reveals
    /// a random preset color, and the number text pops up and down.
    /// </summary>
    [DisallowMultipleComponent]
    public class RecallAnswerSlot : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("The text component displaying the number or hint.")]
        [SerializeField] private TMP_Text slotText;

        [Tooltip("The background image component of this slot.")]
        [SerializeField] private Image slotImage;

        [Header("Slot Configuration")]
        [Tooltip("The expected answer value for this slot.")]
        [SerializeField] private int expectedAnswer = 1;

        [Tooltip("Is this an active answer slot (requiring player drop) or a static slot?")]
        [SerializeField] private bool isAnswer = true;

        [Tooltip("Should this slot display a faint hint number before being answered?")]
        [SerializeField] private bool showHint = true;

        [Tooltip("If true, colorize the number text instead of the box background image on answer drop.")]
        [SerializeField] private bool colorizeTextInsteadOfBox = false;

        [Header("Preset Random Colors")]
        [Tooltip("Preset colors to pick a random color from when revealed.")]
        [SerializeField] private Color[] presetColors = new Color[]
        {
            new Color(0.91f, 0.30f, 0.24f), // Red
            new Color(0.20f, 0.60f, 0.86f), // Blue
            new Color(0.95f, 0.61f, 0.07f), // Orange
            new Color(0.15f, 0.68f, 0.38f), // Green
            new Color(0.61f, 0.35f, 0.71f), // Purple
            new Color(0.10f, 0.74f, 0.61f), // Teal
            new Color(0.90f, 0.49f, 0.13f), // Amber
            new Color(0.15f, 0.55f, 0.82f)  // Ocean
        };

        [Header("Pop Up & Down Animation Settings")]
        [Tooltip("Total duration of the text pop up and down reveal animation.")]
        [SerializeField] private float popDuration = 0.45f;

        [Tooltip("Peak scale multiplier for the pop up effect.")]
        [SerializeField] private float popScaleMultiplier = 1.45f;

        // ── Public Accessors ──────────────────────────────────────────────────

        public TMP_Text SlotText { get => slotText; set => slotText = value; }
        public Image SlotImage { get => slotImage; set => slotImage = value; }
        public int ExpectedAnswer { get => expectedAnswer; set => expectedAnswer = value; }
        public bool IsAnswer { get => isAnswer; set => isAnswer = value; }
        public bool ShowHint { get => showHint; set => showHint = value; }
        public bool ColorizeTextInsteadOfBox { get => colorizeTextInsteadOfBox; set => colorizeTextInsteadOfBox = value; }

        // ── Private State ─────────────────────────────────────────────────────

        private bool _isSolved = false;
        private System.Action _onSolvedCallback;
        private AnswerDropZone _dropZone;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            AutoBindComponents();
        }

        private void Reset()
        {
            AutoBindComponents();
        }

        private void AutoBindComponents()
        {
            if (slotText == null)
            {
                slotText = GetComponentInChildren<TMP_Text>(true);
            }
            if (slotImage == null)
            {
                slotImage = GetComponent<Image>();
                if (slotImage == null) slotImage = GetComponentInChildren<Image>(true);
            }
        }

        private void Start()
        {
            InitSlotState();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SetupSlot(int value, bool isAnswerSlot, bool hintEnabled, System.Action onSolved = null)
        {
            expectedAnswer = value;
            isAnswer = isAnswerSlot;
            showHint = hintEnabled;
            _onSolvedCallback = onSolved;

            AutoBindComponents();
            InitSlotState();
        }

        public void InitSlotState()
        {
            AutoBindComponents();
            _isSolved = false;

            if (isAnswer)
            {
                _dropZone = GetComponent<AnswerDropZone>();
                if (_dropZone == null) _dropZone = gameObject.AddComponent<AnswerDropZone>();

                _dropZone.Setup(expectedAnswer, null, showHint: showHint);

                if (slotText != null)
                {
                    if (showHint)
                    {
                        slotText.text = expectedAnswer.ToString();
                        slotText.color = new Color(0.35f, 0.35f, 0.35f, 0.45f);
                        slotText.gameObject.SetActive(true);
                    }
                    else
                    {
                        slotText.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                if (colorizeTextInsteadOfBox)
                {
                    if (slotText != null)
                    {
                        slotText.text = expectedAnswer.ToString();
                        if (presetColors != null && presetColors.Length > 0)
                        {
                            slotText.color = presetColors[Random.Range(0, presetColors.Length)];
                        }
                        else
                        {
                            slotText.color = Color.white;
                        }
                        slotText.gameObject.SetActive(true);
                    }
                }
                else
                {
                    if (slotText != null)
                    {
                        slotText.text = expectedAnswer.ToString();
                        slotText.color = Color.white;
                        slotText.gameObject.SetActive(true);
                    }

                    if (slotImage != null && presetColors != null && presetColors.Length > 0)
                    {
                        slotImage.color = presetColors[Random.Range(0, presetColors.Length)];
                    }
                }
            }
        }

        // ── Drop Reveal Handling ──────────────────────────────────────────────

        public void OnCorrectAnswerDropped(AnswerCard card, System.Action externalOnCorrect = null)
        {
            if (_isSolved) return;
            _isSolved = true;

            // 1. Answer card disappears
            if (card != null)
            {
                card.Disappear(0.2f);
            }

            Color targetColor = (card != null) ? card.CardColor : Color.white;

            if (colorizeTextInsteadOfBox)
            {
                // Box background remains untouched; colorize text & trigger pop up/down animation
                if (slotText != null)
                {
                    slotText.text = (card != null) ? card.Value.ToString() : expectedAnswer.ToString();
                    slotText.color = targetColor; // Text gets the card's color!
                    slotText.gameObject.SetActive(true);

                    DOTween.Kill(slotText.transform);
                    slotText.transform.localScale = Vector3.zero;

                    // Pop up to popScaleMultiplier, then pop down to 1.0
                    slotText.transform.DOScale(Vector3.one * popScaleMultiplier, popDuration * 0.5f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() =>
                        {
                            slotText.transform.DOScale(Vector3.one, popDuration * 0.5f)
                                .SetEase(Ease.InOutQuad);
                        });
                }
            }
            else
            {
                // Standard: Box background matches dropped card's color, text is white
                if (slotImage != null)
                {
                    slotImage.DOColor(targetColor, 0.25f);
                }

                if (slotText != null)
                {
                    slotText.text = (card != null) ? card.Value.ToString() : expectedAnswer.ToString();
                    slotText.color = Color.white;
                    slotText.gameObject.SetActive(true);

                    DOTween.Kill(slotText.transform);
                    slotText.transform.localScale = Vector3.zero;

                    // Pop up to popScaleMultiplier, then pop down to 1.0
                    slotText.transform.DOScale(Vector3.one * popScaleMultiplier, popDuration * 0.5f)
                        .SetEase(Ease.OutBack)
                        .OnComplete(() =>
                        {
                            slotText.transform.DOScale(Vector3.one, popDuration * 0.5f)
                                .SetEase(Ease.InOutQuad);
                        });
                }
            }

            // Play SFX & voice
            KidGame.Audio.AudioManager.Instance?.PlayAnswerDropSfx();
            KidGame.Audio.AudioManager.Instance?.PlayNumberVoice(expectedAnswer.ToString());

            if (KidGame.Interface.GameFlowManager.Instance != null)
            {
                KidGame.Interface.GameFlowManager.Instance.RegisterCorrectAnswer();
            }

            externalOnCorrect?.Invoke();
            _onSolvedCallback?.Invoke();
        }
    }
}
