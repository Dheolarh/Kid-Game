using UnityEngine;
using DG.Tweening;

namespace KidGame.Interface
{
    /// <summary>
    /// Manages the entry animations of elements on the Home (Main) screen:
    /// 1. Sun slides in from the top-right outside the canvas.
    /// 2. Grass slides up quickly from below the screen.
    /// 3. Lottie character pops up (unhides and scales up) once the grass has arrived.
    /// </summary>
    public class HomeScreenIntroController : MonoBehaviour
    {
        [Header("Sun Settings")]
        [SerializeField] private RectTransform sunRect;
        [SerializeField] private Vector2 sunStartOffset = new Vector2(300f, 300f); // Offset to start off-screen top-right
        [SerializeField] private float sunDuration = 0.8f;
        [SerializeField] private Ease sunEase = Ease.OutBack;
        [SerializeField] private float sunRotationDuration = 20f; // Seconds per complete 360-degree rotation

        [Header("Grass Settings")]
        [SerializeField] private RectTransform grassRect;
        [SerializeField] private float grassStartOffset = 400f; // Distance below the screen to start from
        [SerializeField] private float grassDuration = 0.5f;
        [SerializeField] private Ease grassEase = Ease.OutBack;

        [Header("Lottie Character Settings")]
        [SerializeField] private GameObject lottieObject;
        [SerializeField] private float lottieScaleDuration = 0.4f;
        [SerializeField] private Ease lottieScaleEase = Ease.OutBack;

        private Vector2 _originalSunPos;
        private Vector2 _originalGrassPos;
        private bool _isInitialized = false;
        private Tween _sunRotationTween;

        private void Awake()
        {
            InitializeOriginalPositions();
        }

        private void OnEnable()
        {
            PlayIntro();
        }

        /// <summary>
        /// Stores the design-time target positions for Sun and Grass.
        /// </summary>
        private void InitializeOriginalPositions()
        {
            if (_isInitialized) return;

            if (sunRect != null)
            {
                _originalSunPos = sunRect.anchoredPosition;
            }

            if (grassRect != null)
            {
                _originalGrassPos = grassRect.anchoredPosition;
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Resets elements to off-screen/hidden states.
        /// </summary>
        public void ResetIntroState()
        {
            InitializeOriginalPositions();

            // Kill any active sun rotation tween
            if (_sunRotationTween != null && _sunRotationTween.IsActive())
            {
                _sunRotationTween.Kill();
            }

            // Place Sun off-screen and reset rotation
            if (sunRect != null)
            {
                sunRect.anchoredPosition = _originalSunPos + sunStartOffset;
                sunRect.localRotation = Quaternion.identity;
            }

            // Place Grass off-screen bottom (using Abs to ensure it goes down)
            if (grassRect != null)
            {
                grassRect.anchoredPosition = _originalGrassPos + new Vector2(0f, -Mathf.Abs(grassStartOffset));
            }

            // Hide Lottie character
            if (lottieObject != null)
            {
                lottieObject.SetActive(false);
                lottieObject.transform.localScale = Vector3.zero;
            }
        }

        /// <summary>
        /// Plays the sequential intro animation for the Home screen.
        /// </summary>
        public void PlayIntro()
        {
            // Reset to starting off-screen positions
            ResetIntroState();

            Sequence introSequence = DOTween.Sequence();

            // 1. Slide the Sun from top-right
            if (sunRect != null)
            {
                introSequence.Append(
                    sunRect.DOAnchorPos(_originalSunPos, sunDuration)
                        .SetEase(sunEase)
                        .OnComplete(StartSunRotation)
                );
            }

            // 2. Slide the Grass up from below (simultaneously or staggered)
            if (grassRect != null)
            {
                if (sunRect != null)
                {
                    // Stagger grass slightly after sun starts, or run in parallel
                    introSequence.Join(
                        grassRect.DOAnchorPos(_originalGrassPos, grassDuration)
                            .SetEase(grassEase)
                    );
                }
                else
                {
                    introSequence.Append(
                        grassRect.DOAnchorPos(_originalGrassPos, grassDuration)
                            .SetEase(grassEase)
                    );
                }
            }

            // 3. Unhide and scale up Lottie character once grass has finished arriving
            introSequence.OnComplete(() =>
            {
                if (lottieObject != null)
                {
                    lottieObject.SetActive(true);
                    lottieObject.transform.DOScale(Vector3.one, lottieScaleDuration)
                        .SetEase(lottieScaleEase);
                }
            });
        }

        private void StartSunRotation()
        {
            if (sunRect == null || sunRotationDuration <= 0f) return;

            if (_sunRotationTween != null && _sunRotationTween.IsActive())
            {
                _sunRotationTween.Kill();
            }

            // Rotate continuously (360 degrees) slowly clockwise (negative Z rotation)
            _sunRotationTween = sunRect.DORotate(new Vector3(0f, 0f, -360f), sunRotationDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental);
        }

        private void OnDestroy()
        {
            if (_sunRotationTween != null && _sunRotationTween.IsActive())
            {
                _sunRotationTween.Kill();
            }
        }
    }
}
