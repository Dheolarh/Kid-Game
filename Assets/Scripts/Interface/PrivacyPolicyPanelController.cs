using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using KidGame.Audio;

namespace KidGame.Interface
{
    /// <summary>
    /// Controller for the Privacy Policy popup panel.
    /// Prompts parents/guardians to accept the privacy policy before filling profile details.
    /// Handles rich text formatting, single-line paragraph spacing, and opens hosted URL:
    /// https://osirisxstudios.xyz/privacy/numeracy via button or text link.
    /// </summary>
    public class PrivacyPolicyPanelController : MonoBehaviour, IPointerClickHandler
    {
        public static PrivacyPolicyPanelController Instance { get; private set; }

        [Header("Panel References")]
        [Tooltip("The main container GameObject of the Privacy Policy popup.")]
        [SerializeField] private GameObject policyPanelObject;

        [Tooltip("The RectTransform content panel that pops up and scales.")]
        [SerializeField] private RectTransform contentPanel;

        [Header("Text & Link Elements")]
        [SerializeField] private TMP_Text policyBodyText;
        [SerializeField] private Button readFullPolicyButton;
        [SerializeField] private string policyUrl = "https://osirisxstudios.xyz/privacy/numeracy";

        [Header("Action Buttons")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button rejectButton;

        [Header("Manager References")]
        [SerializeField] private ProfileScreenController profileScreenController;

        [Header("Pop Settings for Buttons")]
        [SerializeField] private float buttonPressScale = 0.85f;
        [SerializeField] private float buttonPopDuration = 0.15f;

        private Vector3 _contentOriginalScale = Vector3.one;
        private Vector3 _acceptBtnOriginalScale = Vector3.one;
        private Vector3 _rejectBtnOriginalScale = Vector3.one;
        private Vector3 _linkBtnOriginalScale = Vector3.one;
        private bool _isTransitioning = false;

        public bool HasAcceptedPolicy => PlayerPrefs.GetInt("HasAcceptedPrivacyPolicy", 0) == 1;

        public const string DefaultRichText = 
            "<b>Welcome to Numeracy!</b>\n" +
            "Before we begin, we need a little information to personalize your child's experience.\n" +
            "We will ask for your child's name and age. This information is stored <b>only on your device</b> and is <b>never shared or sent anywhere</b>.\n" +
            "By tapping <b>"Accept"</b>, you confirm that you are the parent or guardian of the child using this app and that you consent to this.\n\n" +
            "<color=#007AFF><link="https://osirisxstudios.xyz/privacy/numeracy"><u>Read full Privacy Policy</u></link></color>";

        private void Awake()
        {
            Instance = this;
            if (profileScreenController == null)
            {
                profileScreenController = FindObjectOfType<ProfileScreenController>(true);
            }
        }

        private void Start()
        {
            if (contentPanel != null)
            {
                _contentOriginalScale = contentPanel.localScale;
            }

            // Always apply clean rich text formatting
            if (policyBodyText != null)
            {
                policyBodyText.text = DefaultRichText;
            }

            if (readFullPolicyButton != null)
            {
                _linkBtnOriginalScale = readFullPolicyButton.transform.localScale;
                readFullPolicyButton.onClick.AddListener(OnReadFullPolicyClicked);
            }

            if (acceptButton != null)
            {
                _acceptBtnOriginalScale = acceptButton.transform.localScale;
                acceptButton.onClick.AddListener(OnAcceptClicked);
            }

            if (rejectButton != null)
            {
                _rejectBtnOriginalScale = rejectButton.transform.localScale;
                rejectButton.onClick.AddListener(OnRejectClicked);
            }

            // Ensure the panel starts closed
            if (policyPanelObject != null)
            {
                policyPanelObject.SetActive(false);
            }
        }

        public void OpenPanel()
        {
            if (policyPanelObject == null || contentPanel == null) return;

            _isTransitioning = true;

            if (policyBodyText != null)
            {
                policyBodyText.text = DefaultRichText;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDialoguePopSfx();
            }

            policyPanelObject.SetActive(true);

            contentPanel.DOKill();
            contentPanel.localScale = Vector3.zero;

            contentPanel.DOScale(_contentOriginalScale, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() => _isTransitioning = false);
        }

        public void ClosePanel()
        {
            if (policyPanelObject == null || contentPanel == null) return;

            _isTransitioning = true;

            contentPanel.DOKill();
            contentPanel.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    policyPanelObject.SetActive(false);
                    contentPanel.localScale = _contentOriginalScale;
                    _isTransitioning = false;
                });
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (policyBodyText == null) return;

            Canvas canvas = policyBodyText.canvas;
            Camera cam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera) ? canvas.worldCamera : null;

            int linkIndex = TMP_TextUtilities.FindIntersectingLink(policyBodyText, eventData.position, cam);
            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = policyBodyText.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();
                if (string.IsNullOrEmpty(linkId)) linkId = policyUrl;

                OpenPrivacyUrl(linkId);
            }
        }

        private void OnReadFullPolicyClicked()
        {
            if (readFullPolicyButton != null) PlayButtonPop(readFullPolicyButton.transform, _linkBtnOriginalScale);
            OpenPrivacyUrl(policyUrl);
        }

        private void OpenPrivacyUrl(string targetUrl)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClickSfx();

            string url = string.IsNullOrEmpty(targetUrl) ? policyUrl : targetUrl;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            Debug.Log($"[PrivacyPolicyPanelController] Opening Privacy Policy URL: {url}");
            Application.OpenURL(url);
        }

        private void OnAcceptClicked()
        {
            if (_isTransitioning) return;
            if (acceptButton != null) PlayButtonPop(acceptButton.transform, _acceptBtnOriginalScale);

            // Save acceptance
            PlayerPrefs.SetInt("HasAcceptedPrivacyPolicy", 1);
            PlayerPrefs.Save();

            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClickSfx();

            // Close panel with animation and start profile setup intro
            ClosePanel();

            if (profileScreenController != null)
            {
                profileScreenController.PlaySetupIntro();
            }
        }

        private void OnRejectClicked()
        {
            if (_isTransitioning) return;
            if (rejectButton != null) PlayButtonPop(rejectButton.transform, _rejectBtnOriginalScale);

            if (AudioManager.Instance != null) AudioManager.Instance.PlayButtonClickSfx();

            Debug.Log("[PrivacyPolicyPanelController] Player rejected Privacy Policy. Exiting Application.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
            if (Instance == this) Instance = null;
            if (contentPanel != null) contentPanel.DOKill();
            if (acceptButton != null) acceptButton.transform.DOKill();
            if (rejectButton != null) rejectButton.transform.DOKill();
            if (readFullPolicyButton != null) readFullPolicyButton.transform.DOKill();
        }
    }
}
