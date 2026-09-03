using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using KidGame.Audio;

namespace KidGame.Interface
{
    /// <summary>
    /// Controller for the Profile Menu UI popup in the main scene.
    /// Handles pop-up scale animation (Ease.OutBack / Ease.InBack),
    /// profile data display (Name, Age, Avatar, Stars, Streak),
    /// click feedback for open/close buttons, and an Edit Name Panel for single-word name updates.
    /// </summary>
    public class ProfileMenuController : MonoBehaviour
    {
        public static ProfileMenuController Instance { get; private set; }

        [Header("Menu References")]
        [Tooltip("The main container GameObject of the profile menu popup (e.g. Canvas or Overlay Panel).")]
        [SerializeField] private GameObject profileMenuObject;

        [Tooltip("The RectTransform content panel that pops up and scales.")]
        [SerializeField] private RectTransform contentPanel;

        [Header("Profile Display Elements")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text ageText;
        [SerializeField] private TMP_Text starsText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Sprite[] profileSprites;

        [Header("Open Button Avatar Display")]
        [Tooltip("The Image component on/inside the Open Profile Button that displays the player's selected avatar picture.")]
        [SerializeField] private Image openButtonAvatarImage;

        [Header("Triggers")]
        [Tooltip("Button that opens the profile menu.")]
        [SerializeField] private Button openButton;

        [Tooltip("Button that closes the profile menu.")]
        [SerializeField] private Button closeButton;

        [Header("Edit Name Panel References")]
        [Tooltip("Pencil edit button next to the player's name.")]
        [SerializeField] private Button editNameButton;

        [Tooltip("The popup overlay GameObject for editing the name.")]
        [SerializeField] private GameObject editNamePanelObject;

        [Tooltip("The RectTransform content panel for the edit name popup (pop scaling).")]
        [SerializeField] private RectTransform editNameContentPanel;

        [Tooltip("Input field for entering the new single-word name.")]
        [SerializeField] private TMP_InputField editNameInputField;

        [Tooltip("Accept button (Checkmark / Save) to confirm the new name.")]
        [SerializeField] private Button acceptNameButton;

        [Tooltip("Reject button (Cross / Cancel) to discard name changes.")]
        [SerializeField] private Button rejectNameButton;

        [Header("Pop Settings for Buttons")]
        [SerializeField] private float buttonPressScale = 0.85f;
        [SerializeField] private float buttonPopDuration = 0.15f;

        private Vector3 _contentOriginalScale = Vector3.one;
        private Vector3 _editContentOriginalScale = Vector3.one;
        private Vector3 _openBtnOriginalScale = Vector3.one;
        private Vector3 _closeBtnOriginalScale = Vector3.one;
        private Vector3 _editBtnOriginalScale = Vector3.one;
        private Vector3 _acceptBtnOriginalScale = Vector3.one;
        private Vector3 _rejectBtnOriginalScale = Vector3.one;
        private bool _isTransitioning = false;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (contentPanel != null)
            {
                _contentOriginalScale = contentPanel.localScale;
            }
            if (editNameContentPanel != null)
            {
                _editContentOriginalScale = editNameContentPanel.localScale;
            }

            if (openButton != null)
            {
                _openBtnOriginalScale = openButton.transform.localScale;
                openButton.onClick.AddListener(OnOpenClicked);
            }

            if (closeButton != null)
            {
                _closeBtnOriginalScale = closeButton.transform.localScale;
                closeButton.onClick.AddListener(OnCloseClicked);
            }

            if (editNameButton != null)
            {
                _editBtnOriginalScale = editNameButton.transform.localScale;
                editNameButton.onClick.AddListener(OnEditNameClicked);
            }

            if (acceptNameButton != null)
            {
                _acceptBtnOriginalScale = acceptNameButton.transform.localScale;
                acceptNameButton.onClick.AddListener(OnAcceptNameClicked);
            }

            if (rejectNameButton != null)
            {
                _rejectBtnOriginalScale = rejectNameButton.transform.localScale;
                rejectNameButton.onClick.AddListener(OnRejectNameClicked);
            }

            if (editNameInputField != null)
            {
                editNameInputField.shouldHideMobileInput = true;
                editNameInputField.customCaretColor = true;
                editNameInputField.caretColor = new Color32(0xF5, 0xDD, 0x06, 0xFF); // #F5DD06 (Yellow)
                editNameInputField.selectionColor = new Color32(0xA8, 0xCE, 0xFF, 190); // #A8CEFF (Light Blue, Opacity 190)
                editNameInputField.caretWidth = 3;
                editNameInputField.caretBlinkRate = 0.85f;
                editNameInputField.onFocusSelectAll = false;

                editNameInputField.onValueChanged.AddListener(OnEditNameValueChanged);
                editNameInputField.onSubmit.AddListener((val) => OnAcceptNameClicked());
            }

            // Refresh profile data & update open button avatar icon
            RefreshProfileData();

            // Ensure menus start closed
            if (profileMenuObject != null)
            {
                profileMenuObject.SetActive(false);
            }
            if (editNamePanelObject != null)
            {
                editNamePanelObject.SetActive(false);
            }
        }

        private void OnOpenClicked()
        {
            if (_isTransitioning) return;
            if (openButton != null) PlayButtonPop(openButton.transform, _openBtnOriginalScale);
            OpenMenu();
        }

        private void OnCloseClicked()
        {
            if (_isTransitioning) return;
            if (closeButton != null) PlayButtonPop(closeButton.transform, _closeBtnOriginalScale);
            CloseMenu();
        }

        private void OnEditNameClicked()
        {
            if (_isTransitioning) return;
            if (editNameButton != null) PlayButtonPop(editNameButton.transform, _editBtnOriginalScale);
            OpenEditNamePanel();
        }

        private void OnAcceptNameClicked()
        {
            if (_isTransitioning) return;
            if (acceptNameButton != null) PlayButtonPop(acceptNameButton.transform, _acceptBtnOriginalScale);

            if (editNameInputField == null) return;
            string inputName = editNameInputField.text.Trim().Replace(" ", "");

            if (string.IsNullOrEmpty(inputName)) return;

            string singleWordName = ToTitleCase(inputName);

            // Save as both PlayerName and SingleWordName for mascot speech in game
            PlayerPrefs.SetString("PlayerName", singleWordName);
            PlayerPrefs.SetString("SingleWordName", singleWordName);
            PlayerPrefs.Save();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayButtonClickSfx();
            }

            RefreshProfileData();
            CloseEditNamePanel();
        }

        private void OnRejectNameClicked()
        {
            if (_isTransitioning) return;
            if (rejectNameButton != null) PlayButtonPop(rejectNameButton.transform, _rejectBtnOriginalScale);
            CloseEditNamePanel();
        }

        private void OnEditNameValueChanged(string val)
        {
            // Enforce single word (no spaces)
            if (val.Contains(" "))
            {
                string singleWord = val.Replace(" ", "");
                if (editNameInputField != null)
                {
                    editNameInputField.text = singleWord;
                    editNameInputField.caretPosition = singleWord.Length;
                }
            }
        }

        public void OpenMenu()
        {
            if (profileMenuObject == null || contentPanel == null) return;

            _isTransitioning = true;
            RefreshProfileData();

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDialoguePopSfx();
            }

            profileMenuObject.SetActive(true);
            contentPanel.DOKill();
            contentPanel.localScale = Vector3.zero;

            contentPanel.DOScale(_contentOriginalScale, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => _isTransitioning = false);
        }

        public void CloseMenu()
        {
            if (profileMenuObject == null || contentPanel == null) return;

            _isTransitioning = true;
            contentPanel.DOKill();
            contentPanel.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    profileMenuObject.SetActive(false);
                    contentPanel.localScale = _contentOriginalScale;
                    _isTransitioning = false;
                });
        }

        public void OpenEditNamePanel()
        {
            if (editNamePanelObject == null || editNameContentPanel == null) return;

            string currentName = PlayerPrefs.GetString("SingleWordName", PlayerPrefs.GetString("PlayerName", "JOHN"));
            if (editNameInputField != null)
            {
                editNameInputField.text = currentName;
                editNameInputField.Select();
                editNameInputField.ActivateInputField();
                editNameInputField.caretPosition = editNameInputField.text.Length;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDialoguePopSfx();
            }

            editNamePanelObject.SetActive(true);
            editNameContentPanel.DOKill();
            editNameContentPanel.localScale = Vector3.zero;

            editNameContentPanel.DOScale(_editContentOriginalScale, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void CloseEditNamePanel()
        {
            if (editNamePanelObject == null || editNameContentPanel == null) return;

            editNameContentPanel.DOKill();
            editNameContentPanel.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    editNamePanelObject.SetActive(false);
                    editNameContentPanel.localScale = _editContentOriginalScale;
                });
        }

        public void RefreshProfileData()
        {
            // 1. User Single Word Name
            string playerName = PlayerPrefs.GetString("SingleWordName", PlayerPrefs.GetString("PlayerName", "JOHN"));

            if (nameText != null)
            {
                nameText.text = playerName.ToUpper();
            }

            // 2. User Age
            int playerAge = PlayerPrefs.GetInt("PlayerAge", 5);
            if (ageText != null)
            {
                ageText.text = $"AGE: {playerAge}";
            }

            // 3. Stars Count
            int totalStars = GetTotalStarsCount();
            if (starsText != null)
            {
                starsText.text = $"{totalStars} STARS";
            }

            // 4. Streak Count
            int streakDays = PlayerPrefs.GetInt("PlayerStreak", PlayerPrefs.GetInt("StreakDays", 1));
            if (streakText != null)
            {
                streakText.text = $"{streakDays} DAYS";
            }

            // 5. Selected Avatar Picture (without background)
            string selectedBuddy = PlayerPrefs.GetString("SelectedBuddy", "");
            if (profileSprites != null && profileSprites.Length > 0)
            {
                Sprite avatarSprite = GetProfileSprite(selectedBuddy);
                if (avatarSprite != null)
                {
                    if (avatarImage != null)
                    {
                        avatarImage.sprite = avatarSprite;
                        avatarImage.gameObject.SetActive(true);
                    }
                    if (openButtonAvatarImage != null)
                    {
                        openButtonAvatarImage.sprite = avatarSprite;
                        openButtonAvatarImage.gameObject.SetActive(true);
                    }
                }
            }
        }

        private int GetTotalStarsCount()
        {
            if (PlayerPrefs.HasKey("TotalStars"))
            {
                return PlayerPrefs.GetInt("TotalStars");
            }

            int total = 0;
            for (int i = 1; i <= 100; i++)
            {
                total += PlayerPrefs.GetInt($"Level_Stars_{i}", 0);
            }
            return total;
        }

        private Sprite GetProfileSprite(string buddyName)
        {
            if (profileSprites == null) return null;
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
            return profileSprites.Length > 0 ? profileSprites[0] : null;
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return char.ToUpper(input[0]) + input.Substring(1).ToLower();
        }

        private void PlayButtonPop(Transform buttonTransform, Vector3 originalScale)
        {
            buttonTransform.DOKill();
            buttonTransform.DOScale(originalScale * buttonPressScale, buttonPopDuration * 0.4f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    buttonTransform.DOScale(originalScale, buttonPopDuration * 0.6f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true);
                });
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (contentPanel != null) contentPanel.DOKill();
            if (editNameContentPanel != null) editNameContentPanel.DOKill();
            if (openButton != null) openButton.transform.DOKill();
            if (closeButton != null) closeButton.transform.DOKill();
            if (editNameButton != null) editNameButton.transform.DOKill();
            if (acceptNameButton != null) acceptNameButton.transform.DOKill();
            if (rejectNameButton != null) rejectNameButton.transform.DOKill();
        }
    }
}
