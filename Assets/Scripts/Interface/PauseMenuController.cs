using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace KidGame.Interface
{
    /// <summary>
    /// Controller for the Pause Menu UI.
    /// Handles pop-up scale animation, slide/fade options, Time.timeScale pausing,
    /// and click feedback for the open/close triggers.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Menu References")]
        [Tooltip("The main container GameObject of the pause menu (typically the Canvas GameObject).")]
        [SerializeField] private GameObject pauseMenuObject;

        [Tooltip("The RectTransform that holds the pause menu panel contents (the one that pops up).")]
        [SerializeField] private RectTransform contentPanel;

        [Header("Triggers")]
        [Tooltip("Button that opens the pause menu.")]
        [SerializeField] private Button openButton;

        [Tooltip("Button that closes the pause menu.")]
        [SerializeField] private Button closeButton;

        [Header("Settings")]
        [Tooltip("If true, freezes time (Time.timeScale = 0) while the pause menu is open.")]
        [SerializeField] private bool pauseTime = false;

        [Header("Pop Settings for Buttons")]
        [Tooltip("Scale multiplier for button click pop feedback.")]
        [SerializeField] private float buttonPressScale = 0.85f;
        [SerializeField] private float buttonPopDuration = 0.15f;

        private Vector3 _contentOriginalScale;
        private Vector3 _openBtnOriginalScale;
        private Vector3 _closeBtnOriginalScale;
        private bool _isTransitioning = false;

        private void Start()
        {
            if (contentPanel != null)
            {
                _contentOriginalScale = contentPanel.localScale;
            }
            else
            {
                _contentOriginalScale = Vector3.one;
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

            // Ensure the menu starts closed
            if (pauseMenuObject != null)
            {
                pauseMenuObject.SetActive(false);
            }
        }

        private void OnOpenClicked()
        {
            if (_isTransitioning) return;

            // 1. Play click pop on open button
            if (openButton != null)
            {
                PlayButtonPop(openButton.transform, _openBtnOriginalScale);
            }

            // 2. Open the pause menu
            OpenMenu();
        }

        private void OnCloseClicked()
        {
            if (_isTransitioning) return;

            // 1. Play click pop on close button
            if (closeButton != null)
            {
                PlayButtonPop(closeButton.transform, _closeBtnOriginalScale);
            }

            // 2. Close the pause menu
            CloseMenu();
        }

        public void OpenMenu()
        {
            if (pauseMenuObject == null || contentPanel == null) return;

            _isTransitioning = true;
            
            // Activate the menu container
            pauseMenuObject.SetActive(true);

            // Scale content down to zero first
            contentPanel.DOKill();
            contentPanel.localScale = Vector3.zero;

            // Pop scale up with a nice bounce
            contentPanel.DOScale(_contentOriginalScale, 0.4f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true) // Ensure it runs even if Time.timeScale is 0
                .OnComplete(() =>
                {
                    if (pauseTime)
                    {
                        Time.timeScale = 0f;
                    }
                    _isTransitioning = false;
                });
        }

        public void CloseMenu()
        {
            if (pauseMenuObject == null || contentPanel == null) return;

            _isTransitioning = true;

            // Resume time first if it was paused
            if (pauseTime)
            {
                Time.timeScale = 1f;
            }

            // Scale content down
            contentPanel.DOKill();
            contentPanel.DOScale(Vector3.zero, 0.3f)
                .SetEase(Ease.InBack)
                .SetUpdate(true) // Ensure it runs even if Time.timeScale is 0
                .OnComplete(() =>
                {
                    pauseMenuObject.SetActive(false);
                    contentPanel.localScale = _contentOriginalScale; // Restore scale for next open
                    _isTransitioning = false;
                });
        }

        private void PlayButtonPop(Transform buttonTransform, Vector3 originalScale)
        {
            buttonTransform.DOKill();
            buttonTransform.DOScale(originalScale * buttonPressScale, buttonPopDuration * 0.4f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true) // Run even if timeScale = 0
                .OnComplete(() =>
                {
                    buttonTransform.DOScale(originalScale, buttonPopDuration * 0.6f)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true);
                });
        }

        private void OnDestroy()
        {
            if (contentPanel != null) contentPanel.DOKill();
            if (openButton != null) openButton.transform.DOKill();
            if (closeButton != null) closeButton.transform.DOKill();
        }
    }
}
