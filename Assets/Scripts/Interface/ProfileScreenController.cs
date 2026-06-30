using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

namespace KidGame.Interface
{
    /// <summary>
    /// Controls the progressive setup steps on the Profile (Age Select) screen,
    /// handling slide-ins, fade-ins, auto-focus inputs, age adjustments, and tutor selection.
    /// </summary>
    public class ProfileScreenController : MonoBehaviour
    {
        [System.Serializable]
        public class TutorOption
        {
            public string tutorName;
            public Button button;
            public RectTransform rectTransform;
            [Tooltip("Optional animator for tutor wave/idle animations.")]
            public Animator animator;
        }

        [Header("Main Panel Reference")]
        [Tooltip("The main container for the profile setup UI, which will slide off-screen to transition to Home.")]
        [SerializeField] private RectTransform profilePanel;

        [Header("Title Settings")]
        [SerializeField] private RectTransform titleRect;
        [SerializeField] private float titleSlideDuration = 0.6f;
        [SerializeField] private float titleSlideOffset = 400f; // Height above original position to start from

        [Header("Name Section")]
        [SerializeField] private CanvasGroup nameSectionGroup;
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private float nameRevealDelay = 0.2f;

        [Header("Age Section")]
        [SerializeField] private CanvasGroup ageSectionGroup;
        [SerializeField] private TMP_Text ageQuestionText;
        [SerializeField] private string ageQuestionFormat = "HOW OLD IS {PLAYERNAME}?";
        [SerializeField] private TMP_Text ageValueText;
        [SerializeField] private Button ageUpButton;
        [SerializeField] private Button ageDownButton;
        [SerializeField] private Image ageBackgroundImage;
        [SerializeField] private int minAge = 3;
        [SerializeField] private int maxAge = 10;
        [SerializeField] private int defaultAge = 3;

        [Header("Age Display Colors")]
        [SerializeField] private Color[] ageColors = new Color[]
        {
            new Color(0.96f, 0.57f, 0.12f), // Vivid Orange
            new Color(0.12f, 0.73f, 0.96f), // Light Blue
            new Color(0.24f, 0.82f, 0.44f), // Emerald Green
            new Color(0.91f, 0.29f, 0.24f), // Vibrant Red
            new Color(0.61f, 0.35f, 0.71f), // Amethyst Purple
            new Color(0.95f, 0.77f, 0.06f), // Sun Yellow
            new Color(0.92f, 0.40f, 0.60f)  // Rose Pink
        };

        [Header("Picture (Tutor) Section")]
        [SerializeField] private CanvasGroup pictureSectionGroup;
        [SerializeField] private TutorOption[] tutors;
        [SerializeField] private float tutorSelectedScale = 1.15f;

        [Header("Next Button Section")]
        [SerializeField] private CanvasGroup nextButtonGroup;
        [SerializeField] private Button nextButton;

        [Header("Transition Settings")]
        [SerializeField] private float sectionRevealDuration = 0.5f;
        [SerializeField] private Ease sectionRevealEase = Ease.OutBack;
        [SerializeField] private float panelExitDuration = 0.6f;
        [SerializeField] private Ease panelExitEase = Ease.InBack;

        [Header("Manager Reference")]
        [SerializeField] private MenuScreenManager menuScreenManager;

        private int _currentAge;
        private Vector2 _titleTargetPos;
        private bool _hasRevealedAge = false;
        private bool _hasRevealedPicture = false;
        private bool _hasRevealedNext = false;
        private TutorOption _selectedTutor = null;

        private void Awake()
        {
            // Cache title original position
            if (titleRect != null)
            {
                _titleTargetPos = titleRect.anchoredPosition;
            }

            SetupInitialUIState();
        }

        private void Start()
        {
            // Hook up input field listeners
            if (nameInputField != null)
            {
                nameInputField.onValueChanged.AddListener(OnNameValueChanged);
                nameInputField.onEndEdit.AddListener(OnNameInputEndEdit);
            }

            // Hook up age button listeners (flipped: up button decreases age, down button increases age)
            if (ageUpButton != null) ageUpButton.onClick.AddListener(DecrementAge);
            if (ageDownButton != null) ageDownButton.onClick.AddListener(IncrementAge);

            // Hook up tutor button listeners
            foreach (var tutor in tutors)
            {
                if (tutor.button != null)
                {
                    tutor.button.onClick.AddListener(() => SelectTutor(tutor));
                }
            }

            // Hook up next button listener
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(TransitionToHomeScreen);
            }

            // Initialize age display
            _currentAge = defaultAge;
            UpdateAgeDisplay();
        }

        /// <summary>
        /// Resets all sections to transparent, scaled down, and non-interactable.
        /// </summary>
        public void SetupInitialUIState()
        {
            // Position Title off-screen
            if (titleRect != null)
            {
                titleRect.anchoredPosition = new Vector2(_titleTargetPos.x, _titleTargetPos.y + titleSlideOffset);
            }

            // Hide and disable all groups
            HideSection(nameSectionGroup);
            HideSection(ageSectionGroup);
            HideSection(pictureSectionGroup);
            HideSection(nextButtonGroup);

            _hasRevealedAge = false;
            _hasRevealedPicture = false;
            _hasRevealedNext = false;
            _selectedTutor = null;

            // Reset tutor scales
            foreach (var tutor in tutors)
            {
                if (tutor.rectTransform != null)
                {
                    tutor.rectTransform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Triggers the intro animation sequence. Call this when the canvas becomes active.
        /// </summary>
        public void PlaySetupIntro()
        {
            SetupInitialUIState();

            Sequence introSequence = DOTween.Sequence();

            // 1. Slide Title in from top
            if (titleRect != null)
            {
                introSequence.Append(titleRect.DOAnchorPosY(_titleTargetPos.y, titleSlideDuration).SetEase(Ease.OutBack));
            }

            // 2. Reveal Name Section immediately after
            introSequence.AppendInterval(nameRevealDelay);
            introSequence.AppendCallback(() =>
            {
                RevealSection(nameSectionGroup, () =>
                {
                    // Focus the input field and trigger keyboard
                    StartCoroutine(FocusNameInputFieldCoroutine());
                });
            });
        }

        private IEnumerator FocusNameInputFieldCoroutine()
        {
            // A short delay ensures Unity finishes canvas lay-outs before focusing
            yield return new WaitForSeconds(0.1f);
            if (nameInputField != null)
            {
                nameInputField.Select();
                nameInputField.ActivateInputField();
            }
        }

        // ──────────────────────────────────────────────────
        // Name Section Logic
        // ──────────────────────────────────────────────────

        private void OnNameValueChanged(string newName)
        {
            UpdateAgeQuestionText(newName);
        }

        private void OnNameInputEndEdit(string value)
        {
            // Only progress if name is not empty
            if (!string.IsNullOrEmpty(value.Trim()))
            {
                RevealAgeSection();
            }
        }

        private void UpdateAgeQuestionText(string name)
        {
            if (ageQuestionText == null) return;

            string displayName = string.IsNullOrEmpty(name.Trim()) ? "YOUR NAME" : name;
            ageQuestionText.text = ageQuestionFormat.Replace("{PLAYERNAME}", displayName).Replace("{playername}", displayName);
        }

        private void RevealAgeSection()
        {
            if (_hasRevealedAge) return;
            _hasRevealedAge = true;

            RevealSection(ageSectionGroup);
        }

        // ──────────────────────────────────────────────────
        // Age Section Logic
        // ──────────────────────────────────────────────────

        private void IncrementAge()
        {
            if (_currentAge < maxAge)
            {
                _currentAge++;
                UpdateAgeDisplay();
                SetRandomAgeColor();
                PlayAgeBounceEffect();
                RevealPictureSection();
            }
        }

        private void DecrementAge()
        {
            if (_currentAge > minAge)
            {
                _currentAge--;
                UpdateAgeDisplay();
                SetRandomAgeColor();
                PlayAgeBounceEffect();
                RevealPictureSection();
            }
        }

        private void UpdateAgeDisplay()
        {
            if (ageValueText != null)
            {
                ageValueText.text = _currentAge.ToString();
            }
        }

        private void PlayAgeBounceEffect()
        {
            if (ageValueText != null)
            {
                // Quick playful scale pop using punch tween
                ageValueText.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 10, 1f)
                    .OnComplete(() => ageValueText.transform.localScale = Vector3.one);
            }
        }

        private void SetRandomAgeColor()
        {
            if (ageBackgroundImage == null || ageColors == null || ageColors.Length == 0) return;

            // Choose a random color from the list that is different from the current color
            int randomIndex = Random.Range(0, ageColors.Length);
            if (ageColors.Length > 1 && ageColors[randomIndex] == ageBackgroundImage.color)
            {
                randomIndex = (randomIndex + 1) % ageColors.Length;
            }

            // Smoothly transition the color of the background image
            ageBackgroundImage.DOColor(ageColors[randomIndex], 0.25f);
        }

        private void RevealPictureSection()
        {
            if (_hasRevealedPicture) return;
            _hasRevealedPicture = true;

            RevealSection(pictureSectionGroup);
        }

        // ──────────────────────────────────────────────────
        // Tutor Section Logic
        // ──────────────────────────────────────────────────

        private void SelectTutor(TutorOption selected)
        {
            _selectedTutor = selected;

            // Handle scale shifts and animator parameters for all tutors
            foreach (var tutor in tutors)
            {
                bool isThisSelected = (tutor == selected);

                if (tutor.rectTransform != null)
                {
                    float targetScale = isThisSelected ? tutorSelectedScale : 1.0f;
                    tutor.rectTransform.DOScale(targetScale, 0.3f).SetEase(Ease.OutBack);
                }

                if (tutor.animator != null)
                {
                    tutor.animator.SetBool("IsSelected", isThisSelected);
                    
                    if (isThisSelected)
                    {
                        // Trigger wave action for the selected tutor
                        tutor.animator.SetTrigger("Wave");
                    }
                }
            }

            RevealNextButton();
        }

        private void RevealNextButton()
        {
            if (_hasRevealedNext) return;
            _hasRevealedNext = true;

            RevealSection(nextButtonGroup);
        }

        // ──────────────────────────────────────────────────
        // Transition to Home Screen
        // ──────────────────────────────────────────────────

        private void TransitionToHomeScreen()
        {
            if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text.Trim()))
            {
                PlayerPrefs.SetString("PlayerName", nameInputField.text.Trim());
                PlayerPrefs.Save();
            }

            if (profilePanel == null)
            {
                if (menuScreenManager != null)
                {
                    menuScreenManager.TriggerAgeSelectToHomeTransition();
                }
                return;
            }

            // Lock Next button interaction during animation
            if (nextButton != null) nextButton.interactable = false;

            // Calculate target position to slide off to the left
            float screenWidth = Screen.width;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                RectTransform canvasRt = canvas.GetComponent<RectTransform>();
                if (canvasRt != null)
                {
                    screenWidth = canvasRt.rect.width;
                }
            }

            float targetX = -screenWidth;

            // Animate entire panel off-screen left and fade out
            CanvasGroup panelGroup = profilePanel.GetComponent<CanvasGroup>();
            if (panelGroup == null)
            {
                panelGroup = profilePanel.gameObject.AddComponent<CanvasGroup>();
            }

            Sequence exitSequence = DOTween.Sequence();
            
            exitSequence.Append(profilePanel.DOAnchorPosX(targetX, panelExitDuration).SetEase(panelExitEase));
            exitSequence.Join(panelGroup.DOFade(0f, panelExitDuration * 0.8f));

            exitSequence.OnComplete(() =>
            {
                if (menuScreenManager != null)
                {
                    menuScreenManager.TriggerAgeSelectToHomeTransition();
                }
                
                // Restore state so it's fresh if reopened
                panelGroup.alpha = 1f;
                profilePanel.anchoredPosition = Vector2.zero;
                if (nextButton != null) nextButton.interactable = true;
            });
        }

        // ──────────────────────────────────────────────────
        // Helper Reveal/Hide Methods
        // ──────────────────────────────────────────────────

        private void RevealSection(CanvasGroup group, System.Action onComplete = null)
        {
            if (group == null)
            {
                onComplete?.Invoke();
                return;
            }

            group.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            group.alpha = 0f;
            group.transform.localScale = Vector3.zero;

            Sequence revealSeq = DOTween.Sequence();
            revealSeq.Append(group.DOFade(1f, sectionRevealDuration));
            revealSeq.Join(group.transform.DOScale(Vector3.one, sectionRevealDuration).SetEase(sectionRevealEase));
            revealSeq.OnComplete(() => onComplete?.Invoke());
        }

        private void HideSection(CanvasGroup group)
        {
            if (group == null) return;

            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.transform.localScale = Vector3.zero;
            group.gameObject.SetActive(false);
        }
    }
}
