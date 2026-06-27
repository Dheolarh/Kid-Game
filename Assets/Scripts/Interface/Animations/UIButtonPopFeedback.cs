using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace KidGame.Interface.Animations
{
    /// <summary>
    /// Attach to any UI Button to automatically trigger a bouncy press/release pop feedback
    /// when the button is clicked. Works independently of other button event listeners.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class UIButtonPopFeedback : MonoBehaviour
    {
        [Header("Pop Settings")]
        [Tooltip("Target scale multiplier when the button is pressed (squashed).")]
        [SerializeField] private float pressScale = 0.85f;

        [Tooltip("Duration of the squash down phase.")]
        [SerializeField] private float durationDown = 0.08f;

        [Tooltip("Duration of the bounce back up phase.")]
        [SerializeField] private float durationUp = 0.12f;

        [Tooltip("Easing function for scaling down.")]
        [SerializeField] private Ease easeDown = Ease.OutQuad;

        [Tooltip("Easing function for bouncing back up.")]
        [SerializeField] private Ease easeUp = Ease.OutBack;

        private Button _button;
        private Vector3 _originalScale;

        private void Start()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;

            if (_button != null)
            {
                _button.onClick.AddListener(PlayPopAnimation);
            }
        }

        /// <summary>
        /// Manually triggers the click pop animation.
        /// </summary>
        public void PlayPopAnimation()
        {
            transform.DOKill();
            transform.DOScale(_originalScale * pressScale, durationDown)
                .SetEase(easeDown)
                .SetUpdate(true) // Ensure it animates even if the game is paused
                .OnComplete(() =>
                {
                    transform.DOScale(_originalScale, durationUp)
                        .SetEase(easeUp)
                        .SetUpdate(true);
                });
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
