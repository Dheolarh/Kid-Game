using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;

namespace KidGame.Interface.Animations
{
    /// <summary>
    /// Animates two UI curtain panels (left and right) splitting away from the middle 
    /// using DOTween to reveal the screen behind, with optional foreground element fading.
    /// </summary>
    public class CurtainTransition : MonoBehaviour
    {
        [Header("Curtain References")]
        [Tooltip("The left curtain/panel. Anchor: Min (0,0) Max (0.5,1), Pivot (1,0.5).")]
        [SerializeField] private RectTransform leftCurtain;

        [Tooltip("The right curtain/panel. Anchor: Min (0.5,0) Max (1,1), Pivot (0,0.5).")]
        [SerializeField] private RectTransform rightCurtain;

        [Header("Foreground References")]
        [Tooltip("Optional CanvasGroup containing logo or text elements to fade out before or during the curtain slide.")]
        [SerializeField] private CanvasGroup foregroundGroup;

        [Header("Animation Settings")]
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float slideDuration = 1.0f;
        [SerializeField] private Ease slideEase = Ease.InOutCubic;
        [SerializeField] private float delayBetweenFadeAndSlide = 0.1f;

        [Header("Events")]
        public UnityEvent onTransitionStart;
        public UnityEvent onTransitionComplete;

        private Sequence _transitionSequence;

        private void Awake()
        {
            // Set up curtains to closed state initially if references are provided
            ResetTransitionState();
        }

        /// <summary>
        /// Resets the curtains to their closed (center-meeting) state and makes foreground visible.
        /// </summary>
        public void ResetTransitionState()
        {
            if (_transitionSequence != null && _transitionSequence.IsActive())
            {
                _transitionSequence.Kill();
            }

            if (leftCurtain != null)
            {
                leftCurtain.anchoredPosition = new Vector2(0f, leftCurtain.anchoredPosition.y);
            }

            if (rightCurtain != null)
            {
                rightCurtain.anchoredPosition = new Vector2(0f, rightCurtain.anchoredPosition.y);
            }

            if (foregroundGroup != null)
            {
                foregroundGroup.alpha = 1f;
                foregroundGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// Plays the curtain opening transition.
        /// </summary>
        /// <param name="onCompleteCallback">Callback when animation finishes.</param>
        public void PlayTransition(System.Action onCompleteCallback = null)
        {
            if (leftCurtain == null || rightCurtain == null)
            {
                Debug.LogError("[CurtainTransition] Missing left or right curtain references!", this);
                onCompleteCallback?.Invoke();
                return;
            }

            // Kill any active transitions
            if (_transitionSequence != null && _transitionSequence.IsActive())
            {
                _transitionSequence.Kill();
            }

            onTransitionStart?.Invoke();

            // Calculate width to slide off-screen responsively
            float leftTargetX = -leftCurtain.rect.width;
            float rightTargetX = rightCurtain.rect.width;

            _transitionSequence = DOTween.Sequence();

            // 1. Fade out splash foreground (logo, text) first if reference is provided
            if (foregroundGroup != null)
            {
                foregroundGroup.blocksRaycasts = false;
                _transitionSequence.Append(foregroundGroup.DOFade(0f, fadeDuration));
                _transitionSequence.AppendInterval(delayBetweenFadeAndSlide);
            }

            // 2. Slide curtains left and right
            _transitionSequence.Append(
                leftCurtain.DOAnchorPosX(leftTargetX, slideDuration)
                    .SetEase(slideEase)
            );
            _transitionSequence.Join(
                rightCurtain.DOAnchorPosX(rightTargetX, slideDuration)
                    .SetEase(slideEase)
            );

            // 3. Trigger events and callback on complete
            _transitionSequence.OnComplete(() =>
            {
                onTransitionComplete?.Invoke();
                onCompleteCallback?.Invoke();
            });
        }
    }
}
