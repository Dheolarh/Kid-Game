using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;

namespace KidGame.Interface
{
    /// <summary>
    /// Controls the progressive setup steps on the new Profile greetings canvas,
    /// handling Name input, Age selection, and Buddy avatar selection.
    /// </summary>
    public class ProfileScreenController : MonoBehaviour
    {
        [Header("Pages References")]
        [SerializeField] private GameObject namePage;
        [SerializeField] private GameObject agePage;
        [SerializeField] private GameObject avatarPage;

        [Header("Name Page Elements")]
        [SerializeField] private TMP_InputField nameInputField;
        [SerializeField] private Button nameNextButton;
        [SerializeField] private TMP_Text nameMascotSpeechText;
        [SerializeField] private Animator nameMascotAnimator;
        [SerializeField] private GameObject nameMessageBubble;
        [SerializeField] private GameObject nameMascotGreetings;
        [SerializeField] private GameObject playerNameContainer;

        [Header("Age Page Elements")]
        [SerializeField] private TMP_Text ageMascotSpeechText;
        [SerializeField] private Animator ageMascotAnimator;
        [SerializeField] private GameObject ageMessageBubble;
        [SerializeField] private GameObject ageMascotGreetings;
        [SerializeField] private GameObject playerAgeContainer;
        [SerializeField] private Button agePreviousButton;
        [SerializeField] private Button ageNextButton;
        [SerializeField] private GameObject ageAnswerSlot;
        [Tooltip("The visual GameObjects representing ages 3 to 10 inside the age answer slot. Order should match ages 3 to 10.")]
        [SerializeField] private GameObject[] ageAnswerVisuals;
        [SerializeField] private RectTransform ageButtonsContainer;

        [Header("Avatar Page Elements")]
        [SerializeField] private TMP_Text avatarMascotSpeechText;
        [SerializeField] private Animator avatarMascotAnimator;
        [SerializeField] private GameObject avatarMessageBubble;
        [SerializeField] private GameObject avatarMascotGreetings;
        [SerializeField] private GameObject playerAvatarContainer;
        [SerializeField] private Button avatarPreviousButton;
        [SerializeField] private Button avatarNextButton;
        [SerializeField] private RectTransform avatarButtonsContainer;
        [SerializeField] private TMP_Text topRightPlayerNameText;
        [SerializeField] private Image topRightAvatarImage;

        [Header("Buddy Sprites")]
        [Tooltip("The profile avatar sprites used at the top-right (e.g. Khloe2, Caleb2).")]
        [SerializeField] private Sprite[] profileSprites;

        [Header("Manager & Settings")]
        [SerializeField] private MenuScreenManager menuScreenManager;
        [SerializeField] private float typewriterCharDelay = 0.02f;
        [SerializeField] private float mascotHiDuration = 2.0f;

        private string _selectedBuddyName = "";
        private int _selectedAge = -1;
        private Coroutine _typewriterCoroutine;
        private Coroutine _mascotAnimCoroutine;
        private Coroutine _finalTransitionCoroutine;

        private void Awake()
        {
            // Deactivate all pages at startup so they don't overlap in the editor
            if (namePage != null) namePage.SetActive(false);
            if (agePage != null) agePage.SetActive(false);
            if (avatarPage != null) avatarPage.SetActive(false);

            if (menuScreenManager == null)
            {
                menuScreenManager = FindComponentEvenInactive<MenuScreenManager>();
            }

            // Auto-resolve scroll container references if unassigned in Inspector
            if (ageButtonsContainer == null && agePage != null)
            {
                ScrollRect scrollRect = agePage.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null) ageButtonsContainer = scrollRect.content;
            }
            if (avatarButtonsContainer == null && avatarPage != null)
            {
                ScrollRect scrollRect = avatarPage.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null) avatarButtonsContainer = scrollRect.content;
            }
        }

        private void Start()
        {
            if (menuScreenManager == null)
            {
                menuScreenManager = FindComponentEvenInactive<MenuScreenManager>();
            }

            // Set up page listeners
            if (nameInputField != null)
            {
                nameInputField.onValueChanged.AddListener(OnNameValueChanged);
                nameInputField.onEndEdit.AddListener(OnNameInputEndEdit);
            }

            if (nameNextButton != null)
            {
                nameNextButton.onClick.AddListener(OnNameNextClicked);
            }

            if (agePreviousButton != null)
            {
                agePreviousButton.onClick.AddListener(OnAgePreviousClicked);
            }

            if (ageNextButton != null)
            {
                ageNextButton.onClick.AddListener(OnAgeNextClicked);
            }

            if (avatarPreviousButton != null)
            {
                avatarPreviousButton.onClick.AddListener(OnAvatarPreviousClicked);
            }

            if (avatarNextButton != null)
            {
                avatarNextButton.onClick.AddListener(TransitionToHomeScreen);
            }

            EnsureScrollSetup(ageButtonsContainer as RectTransform, isHorizontal: true);
            EnsureScrollSetup(avatarButtonsContainer as RectTransform, isHorizontal: true);

            InitializeAgeButtons();
            InitializeAvatarButtons();
        }

        private void EnsureScrollSetup(RectTransform container, bool isHorizontal)
        {
            if (container == null) return;

            // Stop any horizontal/vertical stretching based on scroll direction
            if (isHorizontal)
            {
                // Anchor to Left-Stretch: Left edge (X=0), stretch vertically (Y=0 to 1)
                container.anchorMin = new Vector2(0f, 0f);
                container.anchorMax = new Vector2(0f, 1f);
                container.pivot = new Vector2(0f, 0.5f);
            }
            else
            {
                // Anchor to Top-Stretch: Top edge (Y=1), stretch horizontally (X=0 to 1)
                container.anchorMin = new Vector2(0f, 1f);
                container.anchorMax = new Vector2(1f, 1f);
                container.pivot = new Vector2(0.5f, 1f);
            }

            // Ensure ContentSizeFitter is present and configured
            var fitter = container.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = container.gameObject.AddComponent<ContentSizeFitter>();
            }

            if (isHorizontal)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            else
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // Ensure ScrollRect is configured for horizontal/vertical only
            ScrollRect scrollRect = container.GetComponentInParent<ScrollRect>();
            if (scrollRect != null)
            {
                scrollRect.horizontal = isHorizontal;
                scrollRect.vertical = !isHorizontal;
                scrollRect.movementType = ScrollRect.MovementType.Elastic;
            }

            // Reset position initially
            container.anchoredPosition = new Vector2(0f, container.anchoredPosition.y);
        }

        /// <summary>
        /// Triggers the intro animation sequence. Called by MenuScreenManager.
        /// </summary>
        public void PlaySetupIntro()
        {
            Debug.Log("[ProfileScreenController] PlaySetupIntro execution started.");
            
            if (namePage != null)
            {
                namePage.SetActive(true);
                Debug.Log($"[ProfileScreenController] Set namePage active. ActiveSelf: {namePage.activeSelf}");
            }
            else
            {
                Debug.LogError("[ProfileScreenController] namePage reference is NULL!");
            }
            
            if (agePage != null) agePage.SetActive(false);
            if (avatarPage != null) avatarPage.SetActive(false);

            if (nameNextButton != null)
            {
                nameNextButton.gameObject.SetActive(false);
                nameNextButton.transform.localScale = Vector3.zero;
            }

            // Animate name components in
            if (nameMascotGreetings != null)
            {
                nameMascotGreetings.transform.localScale = Vector3.zero;
                nameMascotGreetings.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            if (playerNameContainer != null)
            {
                playerNameContainer.transform.localScale = Vector3.zero;
                playerNameContainer.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f);
            }

            if (nameMessageBubble != null)
            {
                nameMessageBubble.transform.localScale = Vector3.zero;
                nameMessageBubble.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetDelay(0.3f).OnComplete(() =>
                {
                    TriggerNameGreeting();
                });
                // Play dialogue pop SFX
                KidGame.Audio.AudioManager.Instance?.PlayDialoguePopSfx();
            }
            else
            {
                TriggerNameGreeting();
            }
        }

        private void TriggerNameGreeting()
        {
            string greetingText = "Hello\nMy name is Bee\nWhat should I call you ?";
            if (nameMascotSpeechText != null)
            {
                StartTypewriter(nameMascotSpeechText, greetingText);
            }
            if (nameMascotAnimator != null)
            {
                PlayMascotGreeting(nameMascotAnimator);
            }
            StartCoroutine(FocusNameInputFieldCoroutine());
        }

        private IEnumerator FocusNameInputFieldCoroutine()
        {
            yield return new WaitForSeconds(0.1f);
            if (nameInputField != null)
            {
                nameInputField.Select();
                nameInputField.ActivateInputField();
            }
        }

        // ──────────────────────────────────────────────────
        // Name Page Logic
        // ──────────────────────────────────────────────────

        private void OnNameValueChanged(string value)
        {
            bool hasValidName = !string.IsNullOrEmpty(value.Trim());
            if (nameNextButton != null)
            {
                if (hasValidName && !nameNextButton.gameObject.activeSelf)
                {
                    nameNextButton.gameObject.SetActive(true);
                    nameNextButton.transform.localScale = Vector3.zero;
                    nameNextButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                }
                else if (!hasValidName && nameNextButton.gameObject.activeSelf)
                {
                    nameNextButton.transform.DOScale(Vector3.zero, 0.25f).SetEase(Ease.InBack).OnComplete(() =>
                    {
                        nameNextButton.gameObject.SetActive(false);
                    });
                }
            }
        }

        private void OnNameInputEndEdit(string value)
        {
            if (!string.IsNullOrEmpty(value.Trim()))
            {
                OnNameNextClicked();
            }
        }

        private void OnNameNextClicked()
        {
            string fullName = nameInputField.text.Trim();
            if (string.IsNullOrEmpty(fullName)) return;

            // Apply Title Case: each word starts with a capital, rest lowercase (e.g. "john doe" -> "John Doe")
            fullName = ToTitleCase(fullName);

            // Extract single word name (first name only)
            string firstName = fullName.Split(' ')[0];

            PlayerPrefs.SetString("PlayerName", fullName);
            PlayerPrefs.SetString("SingleWordName", firstName);
            PlayerPrefs.Save();

            SwitchPage(namePage, agePage, PlayAgeIntro);
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool capitalizeNext = true;
            foreach (char c in input)
            {
                if (char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                    capitalizeNext = true;
                }
                else if (capitalizeNext)
                {
                    sb.Append(char.ToUpper(c));
                    capitalizeNext = false;
                }
                else
                {
                    sb.Append(char.ToLower(c));
                }
            }
            return sb.ToString();
        }

        // ──────────────────────────────────────────────────
        // Age Page Logic
        // ──────────────────────────────────────────────────

        private void InitializeAgeButtons()
        {
            if (ageButtonsContainer == null)
            {
                Debug.LogWarning("[ProfileScreenController] ageButtonsContainer is null!");
                return;
            }

            int count = 0;
            foreach (Transform child in ageButtonsContainer)
            {
                Button btn = child.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    string buttonName = child.gameObject.name;
                    int ageVal;
                    if (int.TryParse(buttonName, out ageVal))
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => OnAgeButtonClicked(ageVal, child.gameObject));
                        count++;
                    }
                }
            }
            Debug.Log($"[ProfileScreenController] Hooked up {count} age buttons in scroll container.");
        }

        private void PlayAgeIntro()
        {
            string firstName = PlayerPrefs.GetString("SingleWordName", "Buddy");

            // Animate age page components in
            if (ageMascotGreetings != null)
            {
                ageMascotGreetings.transform.localScale = Vector3.zero;
                ageMascotGreetings.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            if (playerAgeContainer != null)
            {
                playerAgeContainer.transform.localScale = Vector3.zero;
                playerAgeContainer.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f);
            }

            if (ageMessageBubble != null)
            {
                ageMessageBubble.transform.localScale = Vector3.zero;
                ageMessageBubble.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    TriggerAgeGreeting(firstName);
                });
                // Play dialogue pop SFX
                KidGame.Audio.AudioManager.Instance?.PlayDialoguePopSfx();
            }
            else
            {
                TriggerAgeGreeting(firstName);
            }
        }

        private void TriggerAgeGreeting(string firstName)
        {
            string greetingText = $"Hi {firstName}\nNice to meet you\nHow old are you?";
            if (ageMascotSpeechText != null)
            {
                StartTypewriter(ageMascotSpeechText, greetingText);
            }
            if (ageMascotAnimator != null)
            {
                PlayMascotGreeting(ageMascotAnimator);
            }
        }

        private void OnAgeButtonClicked(int age, GameObject buttonObj)
        {
            _selectedAge = age;

            // Play click pop animation on the clicked age button
            if (buttonObj != null)
            {
                buttonObj.transform.DOKill();
                buttonObj.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 10, 1f)
                    .OnComplete(() => buttonObj.transform.localScale = Vector3.one);
            }

            ShowSelectedAgeVisual(age);

            // Play answer drop SFX since selected age drops into slot
            KidGame.Audio.AudioManager.Instance?.PlayAnswerDropSfx();

            if (ageAnswerSlot != null)
            {
                ageAnswerSlot.transform.DOKill();
                ageAnswerSlot.transform.localScale = Vector3.one;
                ageAnswerSlot.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1f)
                    .OnComplete(() => ageAnswerSlot.transform.localScale = Vector3.one);
            }


            // Reveal the next button
            if (ageNextButton != null && !ageNextButton.gameObject.activeSelf)
            {
                ageNextButton.gameObject.SetActive(true);
                ageNextButton.transform.localScale = Vector3.zero;
                ageNextButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
            }
        }

        private void OnAgePreviousClicked()
        {
            SwitchPage(agePage, namePage, PlaySetupIntro);
        }

        private void OnAgeNextClicked()
        {
            if (_selectedAge == -1) return;

            PlayerPrefs.SetInt("PlayerAge", _selectedAge);
            PlayerPrefs.Save();

            SwitchPage(agePage, avatarPage, PlayAvatarIntro);
        }

        // ──────────────────────────────────────────────────
        // Avatar Page Logic
        // ──────────────────────────────────────────────────

        private void InitializeAvatarButtons()
        {
            if (avatarButtonsContainer == null)
            {
                Debug.LogWarning("[ProfileScreenController] avatarButtonsContainer is null!");
                return;
            }

            int count = 0;
            foreach (Transform child in avatarButtonsContainer)
            {
                Button btn = child.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    string buddyName = child.gameObject.name;
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => OnAvatarButtonClicked(buddyName, child.gameObject));
                    count++;
                }
            }
            Debug.Log($"[ProfileScreenController] Hooked up {count} avatar buttons in scroll container.");
        }

        private void PlayAvatarIntro()
        {
            string firstName = PlayerPrefs.GetString("SingleWordName", "Buddy");

            if (topRightPlayerNameText != null)
            {
                topRightPlayerNameText.text = firstName;
            }

            // Animate avatar page components in
            if (avatarMascotGreetings != null)
            {
                avatarMascotGreetings.transform.localScale = Vector3.zero;
                avatarMascotGreetings.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            if (playerAvatarContainer != null)
            {
                playerAvatarContainer.transform.localScale = Vector3.zero;
                playerAvatarContainer.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack).SetDelay(0.15f);
            }

            // Pop message bubble and wave mascot
            if (avatarMessageBubble != null)
            {
                avatarMessageBubble.transform.localScale = Vector3.zero;
                avatarMessageBubble.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).OnComplete(() =>
                {
                    TriggerAvatarGreeting();
                });
                // Play dialogue pop SFX
                KidGame.Audio.AudioManager.Instance?.PlayDialoguePopSfx();
            }
            else
            {
                TriggerAvatarGreeting();
            }
        }

        private void TriggerAvatarGreeting()
        {
            UpdateAvatarSpeechBubbleText("", isFirstGreeting: true);
            if (avatarMascotAnimator != null)
            {
                PlayMascotGreeting(avatarMascotAnimator);
            }
        }

        private void OnAvatarButtonClicked(string buddyName, GameObject buttonObj)
        {
            // Click bounce
            if (buttonObj != null)
            {
                buttonObj.transform.DOKill();
                buttonObj.transform.DOPunchScale(Vector3.one * 0.15f, 0.2f, 10, 1f)
                    .OnComplete(() => buttonObj.transform.localScale = Vector3.one);
            }

            SelectBuddy(buddyName, buttonObj, triggerSpeech: true);
        }

        private void SelectBuddy(string buddyName, GameObject buttonObj, bool triggerSpeech)
        {
            _selectedBuddyName = buddyName;

            // Scale selected buddy up, reset others
            if (avatarButtonsContainer != null)
            {
                foreach (Transform child in avatarButtonsContainer)
                {
                    child.DOKill();
                    if (child.gameObject == buttonObj || child.gameObject.name == buddyName)
                    {
                        child.DOScale(1.15f, 0.3f).SetEase(Ease.OutBack);
                    }
                    else
                    {
                        child.DOScale(1.0f, 0.25f);
                    }
                }
            }

            // Update top right avatar image live
            if (topRightAvatarImage != null)
            {
                Sprite profileSpr = GetProfileSprite(buddyName);
                if (profileSpr != null)
                {
                    topRightAvatarImage.sprite = profileSpr;
                    topRightAvatarImage.gameObject.SetActive(true);
                    
                    // Punch scale the top right display picture for visual feedback
                    topRightAvatarImage.transform.DOKill();
                    topRightAvatarImage.transform.localScale = Vector3.one;
                    topRightAvatarImage.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1f)
                        .OnComplete(() => topRightAvatarImage.transform.localScale = Vector3.one);
                }
            }

            if (triggerSpeech)
            {
                UpdateAvatarSpeechBubbleText(buddyName, isFirstGreeting: false);
            }

            // Reveal next button on selection
            if (avatarNextButton != null && !avatarNextButton.gameObject.activeSelf)
            {
                avatarNextButton.gameObject.SetActive(true);
                avatarNextButton.transform.DOKill();
                avatarNextButton.transform.localScale = Vector3.zero;
                avatarNextButton.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
                avatarNextButton.interactable = true;
            }
        }


        private void UpdateAvatarSpeechBubbleText(string buddyName, bool isFirstGreeting)
        {
            string speechText = isFirstGreeting 
                ? "Let's choose your learn buddy" 
                : $"That's {buddyName}";
            StartTypewriter(avatarMascotSpeechText, speechText);
        }

        private void OnAvatarPreviousClicked()
        {
            SwitchPage(avatarPage, agePage, PlayAgeIntro);
        }

        // ──────────────────────────────────────────────────
        // Helper Methods
        // ──────────────────────────────────────────────────

        private void SwitchPage(GameObject fromPage, GameObject toPage, System.Action onComplete = null)
        {
            if (fromPage != null)
            {
                CanvasGroup fromGroup = fromPage.GetComponent<CanvasGroup>();
                if (fromGroup == null) fromGroup = fromPage.AddComponent<CanvasGroup>();

                // Disable clicks on outgoing page immediately
                fromGroup.interactable = false;
                fromGroup.blocksRaycasts = false;

                fromGroup.DOFade(0f, 0.25f).SetUpdate(true).OnComplete(() =>
                {
                    fromPage.SetActive(false);
                    ShowPage(toPage, onComplete);
                });
            }
            else
            {
                ShowPage(toPage, onComplete);
            }
        }

        private void ShowPage(GameObject page, System.Action onComplete = null)
        {
            if (page == null)
            {
                onComplete?.Invoke();
                return;
            }

            CanvasGroup group = page.GetComponent<CanvasGroup>();
            if (group == null) group = page.AddComponent<CanvasGroup>();

            // Hide and scale down initially so there is NO flicker on active
            group.alpha = 0f;
            group.transform.localScale = Vector3.one * 0.95f;

            // Enable clicks on incoming page
            group.interactable = true;
            group.blocksRaycasts = true;

            // Prepare sub-elements' scales to zero so they don't pop at size 1
            PreparePageElements(page);

            // Pre-initialize and position layout elements immediately before activating
            PreInitializePage(page);

            page.SetActive(true);
            Debug.Log($"[ProfileScreenController] Showing page: {page.name}");

            Sequence seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(group.DOFade(1f, 0.3f));
            seq.Join(group.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
            seq.OnComplete(() => onComplete?.Invoke());
        }

        private void PreInitializePage(GameObject page)
        {
            if (page == agePage)
            {
                // Reset age scroll position immediately
                if (ageButtonsContainer != null)
                {
                    // Force Left anchors to prevent stretch issues
                    ageButtonsContainer.anchorMin = new Vector2(0f, 0f);
                    ageButtonsContainer.anchorMax = new Vector2(0f, 1f);
                    ageButtonsContainer.pivot = new Vector2(0f, 0.5f);
                    ageButtonsContainer.anchoredPosition = new Vector2(0f, ageButtonsContainer.anchoredPosition.y);

                    ScrollRect scrollRect = ageButtonsContainer.GetComponentInParent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        scrollRect.velocity = Vector2.zero;
                        scrollRect.StopMovement();
                        scrollRect.horizontalNormalizedPosition = 0f;
                        if (scrollRect.horizontalScrollbar != null)
                        {
                            scrollRect.horizontalScrollbar.value = 0f;
                        }
                    }
                }

                if (ageNextButton != null)
                {
                    ageNextButton.gameObject.SetActive(false);
                    ageNextButton.transform.localScale = Vector3.zero;
                }

                if (agePreviousButton != null)
                {
                    agePreviousButton.gameObject.SetActive(true);
                }

                // Hide all age answer visuals initially
                if (ageAnswerVisuals != null)
                {
                    foreach (var visual in ageAnswerVisuals)
                    {
                        if (visual != null)
                        {
                            visual.SetActive(false);
                        }
                    }
                }

                _selectedAge = -1;
            }
            else if (page == avatarPage)
            {
                // Reset avatar scroll position immediately
                if (avatarButtonsContainer != null)
                {
                    // Force Left anchors to prevent stretch issues
                    avatarButtonsContainer.anchorMin = new Vector2(0f, 0f);
                    avatarButtonsContainer.anchorMax = new Vector2(0f, 1f);
                    avatarButtonsContainer.pivot = new Vector2(0f, 0.5f);
                    avatarButtonsContainer.anchoredPosition = new Vector2(0f, avatarButtonsContainer.anchoredPosition.y);

                    ScrollRect scrollRect = avatarButtonsContainer.GetComponentInParent<ScrollRect>();
                    if (scrollRect != null)
                    {
                        scrollRect.velocity = Vector2.zero;
                        scrollRect.StopMovement();
                        scrollRect.horizontalNormalizedPosition = 0f;
                        if (scrollRect.horizontalScrollbar != null)
                        {
                            scrollRect.horizontalScrollbar.value = 0f;
                        }
                    }
                }

                if (avatarPreviousButton != null)
                {
                    avatarPreviousButton.gameObject.SetActive(true);
                }

                // Hide Next button and top-right profile image initially until selected
                if (avatarNextButton != null)
                {
                    avatarNextButton.gameObject.SetActive(false);
                    avatarNextButton.transform.localScale = Vector3.zero;
                }

                if (topRightAvatarImage != null)
                {
                    topRightAvatarImage.gameObject.SetActive(false);
                }

                _selectedBuddyName = ""; // No buddy selected first

                // Reset all buddy card scales to 1.0f initially
                if (avatarButtonsContainer != null)
                {
                    foreach (Transform child in avatarButtonsContainer)
                    {
                        child.localScale = Vector3.one;
                    }
                }
            }
        }

        private void PreparePageElements(GameObject page)
        {
            if (page == namePage)
            {
                if (nameMascotGreetings != null) nameMascotGreetings.transform.localScale = Vector3.zero;
                if (playerNameContainer != null) playerNameContainer.transform.localScale = Vector3.zero;
                if (nameMessageBubble != null) nameMessageBubble.transform.localScale = Vector3.zero;
            }
            else if (page == agePage)
            {
                if (ageMascotGreetings != null) ageMascotGreetings.transform.localScale = Vector3.zero;
                if (playerAgeContainer != null) playerAgeContainer.transform.localScale = Vector3.zero;
                if (ageMessageBubble != null) ageMessageBubble.transform.localScale = Vector3.zero;
            }
            else if (page == avatarPage)
            {
                if (avatarMascotGreetings != null) avatarMascotGreetings.transform.localScale = Vector3.zero;
                if (playerAvatarContainer != null) playerAvatarContainer.transform.localScale = Vector3.zero;
                if (avatarMessageBubble != null) avatarMessageBubble.transform.localScale = Vector3.zero;
            }
        }

        private void StartTypewriter(TMP_Text textComponent, string fullText)
        {
            if (textComponent == null) return;

            if (_typewriterCoroutine != null)
            {
                StopCoroutine(_typewriterCoroutine);
            }
            _typewriterCoroutine = StartCoroutine(TypewriterCoroutine(textComponent, fullText));
        }

        private IEnumerator TypewriterCoroutine(TMP_Text textComponent, string fullText)
        {
            textComponent.text = "";
            for (int i = 0; i <= fullText.Length; i++)
            {
                textComponent.text = fullText.Substring(0, i);
                yield return new WaitForSeconds(typewriterCharDelay);
            }
        }

        private void PlayMascotGreeting(Animator animator)
        {
            if (animator == null) return;

            if (_mascotAnimCoroutine != null)
            {
                StopCoroutine(_mascotAnimCoroutine);
            }
            _mascotAnimCoroutine = StartCoroutine(MascotAnimCoroutine(animator));
        }

        private IEnumerator MascotAnimCoroutine(Animator animator)
        {
            animator.ResetTrigger("isHi");
            animator.ResetTrigger("isIdle");

            animator.SetTrigger("isHi");
            yield return new WaitForSeconds(mascotHiDuration);
            animator.SetTrigger("isIdle");
        }

        private void ShowSelectedAgeVisual(int age)
        {
            if (ageAnswerVisuals == null || ageAnswerVisuals.Length == 0) return;

            // Deactivate all first
            foreach (var visual in ageAnswerVisuals)
            {
                if (visual != null)
                {
                    visual.SetActive(false);
                }
            }

            // Match by name first (e.g. if the GameObject is named "3", "Age 3", "Age_3", etc.)
            GameObject matchedVisual = null;
            string ageStr = age.ToString();
            foreach (var visual in ageAnswerVisuals)
            {
                if (visual != null && (
                    visual.name == ageStr || 
                    visual.name.Contains("age " + ageStr) || 
                    visual.name.Contains("age_" + ageStr) || 
                    visual.name.EndsWith(ageStr)
                ))
                {
                    matchedVisual = visual;
                    break;
                }
            }

            // Fallback to index if no name match (Index 0 = Age 3, Index 1 = Age 4, etc.)
            if (matchedVisual == null)
            {
                int index = age - 3;
                if (index >= 0 && index < ageAnswerVisuals.Length)
                {
                    matchedVisual = ageAnswerVisuals[index];
                }
            }

            // Activate the matched visual
            if (matchedVisual != null)
            {
                matchedVisual.SetActive(true);
            }
        }

        private Sprite GetProfileSprite(string buddyName)
        {
            if (profileSprites == null) return null;

            // Matches "Khloe2" or "Khloe_2" or "Khloe"
            string targetName = buddyName + "2";
            foreach (var s in profileSprites)
            {
                if (s != null && (
                    string.Equals(s.name, targetName, System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.name, buddyName, System.StringComparison.OrdinalIgnoreCase)
                ))
                {
                    return s;
                }
            }
            return null;
        }

        private void TransitionToHomeScreen()
        {
            if (string.IsNullOrEmpty(_selectedBuddyName)) return;

            // Save choices
            PlayerPrefs.SetString("SelectedBuddy", _selectedBuddyName);
            PlayerPrefs.SetInt("HasCompletedProfile", 1);
            PlayerPrefs.Save();

            // Disable button interaction immediately so they can't double-click next
            if (avatarNextButton != null)
            {
                avatarNextButton.interactable = false;
            }

            // Show final typewriter text: "Alright!\nLet's start learning"
            string finalGreeting = "Alright!\nLet's start learning";
            StartTypewriter(avatarMascotSpeechText, finalGreeting);

            if (avatarMascotAnimator != null)
            {
                PlayMascotGreeting(avatarMascotAnimator);
            }

            // Start timed delay before closing the curtains
            if (_finalTransitionCoroutine != null) StopCoroutine(_finalTransitionCoroutine);
            _finalTransitionCoroutine = StartCoroutine(FinalTransitionCoroutine());
        }

        private IEnumerator FinalTransitionCoroutine()
        {
            Debug.Log("[ProfileScreenController] FinalTransitionCoroutine started. Waiting 2s.");
            yield return new WaitForSeconds(2.0f);

            Debug.Log($"[ProfileScreenController] Transitioning. SceneTransitionManager.Instance is null? {SceneTransitionManager.Instance == null}");

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.CloseCurtains(() =>
                {
                    Debug.Log("[ProfileScreenController] Curtains closed callback. Calling TriggerAgeSelectToHomeTransition.");
                    if (menuScreenManager != null)
                    {
                        menuScreenManager.TriggerAgeSelectToHomeTransition();
                    }
                    SceneTransitionManager.Instance.OpenCurtains();
                });
            }
            else
            {
                if (menuScreenManager != null)
                {
                    Debug.Log("[ProfileScreenController] No SceneTransitionManager. Calling TriggerAgeSelectToHomeTransition directly.");
                    menuScreenManager.TriggerAgeSelectToHomeTransition();
                }
                else
                {
                    Debug.LogError("[ProfileScreenController] menuScreenManager is NULL in FinalTransitionCoroutine!");
                }
            }
        }

        private T FindComponentEvenInactive<T>() where T : Component
        {
            // First try finding using the standard native inclusion of inactive objects (faster and safer)
            T comp = FindObjectOfType<T>(true);
            if (comp != null) return comp;

            T[] components = Resources.FindObjectsOfTypeAll<T>();
            foreach (var c in components)
            {
                if (c != null && c.gameObject != null && !c.gameObject.hideFlags.HasFlag(HideFlags.HideInHierarchy))
                {
                    return c;
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            if (_typewriterCoroutine != null) StopCoroutine(_typewriterCoroutine);
            if (_mascotAnimCoroutine != null) StopCoroutine(_mascotAnimCoroutine);
            if (_finalTransitionCoroutine != null) StopCoroutine(_finalTransitionCoroutine);
        }
    }
}
