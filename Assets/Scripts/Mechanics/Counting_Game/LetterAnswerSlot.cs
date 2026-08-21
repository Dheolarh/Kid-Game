using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using KidGame.Audio;
using KidGame.Interface;

namespace KidGame.Mechanics.Counting
{
    /// <summary>
    /// Component attached directly to an answer slot in a Letter Counting / Recall game.
    /// Holds text, background image, expected letter string, hint flag, and answer slot flag.
    /// When a correct letter answer card is dropped, the card disappears, the slot matches card color,
    /// and the letter text pops up and down to reveal.
    /// </summary>
    [DisallowMultipleComponent]
    public class LetterAnswerSlot : MonoBehaviour
    {
        [Header("UI Components")]
        [Tooltip("The text component displaying the letter or hint.")]
        [SerializeField] private TMP_Text slotText;

        [Tooltip("The background image component of this slot.")]
        [SerializeField] private Image slotImage;

        [Header("Slot Configuration")]
        [Tooltip("The expected letter string for this slot (e.g. 'A', 'B', 'C').")]
        [SerializeField] private string expectedLetter = "A";

        [Tooltip("Is this an active answer slot (requiring player drop) or a static slot?")]
        [SerializeField] private bool isAnswer = true;

        [Tooltip("Should this slot display a faint hint letter before being answered?")]
        [SerializeField] private bool showHint = true;

        [Header("Preset Random Colors")]
        [Tooltip("Preset colors to pick a random color from when static/revealed.")]
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
        [Tooltip("Total duration of the letter text pop up and down reveal animation.")]
        [SerializeField] private float popDuration = 0.45f;

        [Tooltip("Peak scale multiplier for the pop up effect.")]
        [SerializeField] private float popScaleMultiplier = 1.1f;

        // ── Public Accessors ──────────────────────────────────────────────────

        public TMP_Text SlotText { get => slotText; set => slotText = value; }
        public Image SlotImage { get => slotImage; set => slotImage = value; }
        public string ExpectedLetter { get => expectedLetter; set => expectedLetter = value; }
        public bool IsAnswer { get => isAnswer; set => isAnswer = value; }
        public bool ShowHint { get => showHint; set => showHint = value; }

        /// <summary>
        /// Gets an integer ID for card comparison based on expected letter string.
        /// </summary>
        public int ExpectedValue
        {
            get
            {
                if (string.IsNullOrEmpty(expectedLetter)) return 0;
                return (int)expectedLetter[0];
            }
        }

        // ── Private State ─────────────────────────────────────────────────────

        private bool _isSolved = false;
        private Action _onSolvedCallback;
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

        public void SetupSlot(string letter, bool isAnswerSlot, bool hintEnabled, Action onSolved = null)
        {
            expectedLetter = letter;
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

                _dropZone.Setup(ExpectedValue, null, customHint: expectedLetter, showHint: showHint);

                if (slotText != null)
                {
                    if (showHint)
                    {
                        slotText.text = expectedLetter;
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
                if (slotText != null)
                {
                    slotText.text = expectedLetter;
                    slotText.color = Color.white;
                    slotText.gameObject.SetActive(true);
                }

                if (slotImage != null && presetColors != null && presetColors.Length > 0)
                {
                    slotImage.color = presetColors[UnityEngine.Random.Range(0, presetColors.Length)];
                }
            }
        }

        // ── Drop Reveal Handling ──────────────────────────────────────────────

        public void OnCorrectAnswerDropped(AnswerCard card, Action externalOnCorrect = null)
        {
            if (_isSolved) return;
            _isSolved = true;

            // 1. Answer card is accepted by zone and fills the slot box completely (Image 1 style)
            if (card != null)
            {
                card.AcceptedScaleMultiplier = 1.4f;
                card.AcceptedByZone(transform);
            }

            // 2. Hide slot hint text so the card's text and colored background fill the slot box cleanly
            if (slotText != null)
            {
                slotText.gameObject.SetActive(false);
            }

            // Play SFX & letter voice
            AudioManager.Instance?.PlayAnswerDropSfx();
            AudioManager.Instance?.PlayNumberVoice(expectedLetter);

            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.RegisterCorrectAnswer();
            }

            externalOnCorrect?.Invoke();
            _onSolvedCallback?.Invoke();
        }
    }
}
