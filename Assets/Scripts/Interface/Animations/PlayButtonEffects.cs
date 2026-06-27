using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

namespace KidGame.Interface.Animations
{
    /// <summary>
    /// Attached to the Play Button on the Home screen to periodically play a 
    /// playful bounce and spin animation to attract the child's attention.
    /// Also hooks up the button click to transition to the level select scene.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PlayButtonEffects : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Time in seconds between automatic attention-grab animations.")]
        [SerializeField] private float idleInterval = 5.0f;

        [Tooltip("Scale multiplier during the bounce.")]
        [SerializeField] private float bounceScale = 1.2f;

        [Tooltip("Total duration of the spin/bounce sequence.")]
        [SerializeField] private float animationDuration = 0.6f;

        [Header("Transition Settings")]
        [Tooltip("The name of the scene to load when this button is clicked.")]
        [SerializeField] private string sceneToLoad = "LevelSelect";

        private Button _button;
        private Vector3 _originalScale;
        private Coroutine _idleCoroutine;

        private void Start()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;

            // Register click handler
            _button.onClick.AddListener(OnPlayButtonClicked);

            // Start periodic attention-grab timer
            _idleCoroutine = StartCoroutine(IdleAnimationCoroutine());
        }

        private IEnumerator IdleAnimationCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(idleInterval);
                PlayAttentionGrab();
            }
        }

        /// <summary>
        /// Plays a quick scale-up, 360-degree spin, and scale-down sequence.
        /// </summary>
        public void PlayAttentionGrab()
        {
            // Clear any active tweens on this object
            transform.DOKill();
            transform.localScale = _originalScale;
            transform.localRotation = Quaternion.identity;

            Sequence animSeq = DOTween.Sequence();

            // 1. Scale up & Spin (Clockwise, negative Z)
            animSeq.Append(transform.DOScale(_originalScale * bounceScale, animationDuration * 0.4f).SetEase(Ease.OutQuad));
            animSeq.Join(transform.DORotate(new Vector3(0f, 0f, -360f), animationDuration, RotateMode.FastBeyond360).SetEase(Ease.InOutCubic));

            // 2. Scale back down to original size
            animSeq.Append(transform.DOScale(_originalScale, animationDuration * 0.4f).SetEase(Ease.InQuad));
            
            // Clean values when complete
            animSeq.OnComplete(() =>
            {
                transform.localScale = _originalScale;
                transform.localRotation = Quaternion.identity;
            });
        }

        private void OnPlayButtonClicked()
        {
            // Prevent multiple clicks
            _button.interactable = false;

            // Stop the periodic animation
            if (_idleCoroutine != null)
            {
                StopCoroutine(_idleCoroutine);
            }

            // Play pop animation on click
            transform.DOKill();
            transform.DOScale(_originalScale * 0.85f, 0.08f).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    transform.DOScale(_originalScale, 0.12f).SetEase(Ease.OutBack);
                });

            // Trigger transition scene load
            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.LoadSceneWithTransition(sceneToLoad);
            }
            else
            {
                Debug.LogWarning("[PlayButtonEffects] SceneTransitionManager not found. Falling back to direct load.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
            }
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
