using System.Collections;
using UnityEngine;
using DG.Tweening;
using KidGame.Audio;

namespace KidGame.Interface.Animations
{
    /// <summary>
    /// Periodically plays a pop up and down (scale bounce) animation on an Image
    /// or UI GameObject at successive time intervals to attract attention or add lively motion.
    /// Operates purely on scale so it will never displace or teleport UI layout positions.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPopIntervalAnimator : MonoBehaviour
    {
        [Header("Interval Timing")]
        [Tooltip("Time in seconds between successive pop-up animations.")]
        [SerializeField] private float interval = 4.0f;

        [Tooltip("Initial delay in seconds before the first pop animation starts.")]
        [SerializeField] private float initialDelay = 1.0f;

        [Tooltip("Optional random time variation (+/- seconds) added to the interval.")]
        [SerializeField] private float intervalRandomVariance = 0.0f;

        [Header("Pop Scale Settings")]
        [Tooltip("Peak scale multiplier during the pop up.")]
        [SerializeField] private float popScaleMultiplier = 1.25f;

        [Tooltip("Duration of the pop up and down sequence in seconds.")]
        [SerializeField] private float animationDuration = 0.5f;

        [Tooltip("Ease function used when popping up.")]
        [SerializeField] private Ease popUpEase = Ease.OutBack;

        [Tooltip("Ease function used when returning back to normal scale.")]
        [SerializeField] private Ease popDownEase = Ease.InOutQuad;

        [Header("Audio")]
        [Tooltip("If true, plays a pop sound effect each time the animation triggers.")]
        [SerializeField] private bool playSound = false;
        [SerializeField] private AudioClip customPopSfx;

        [Header("Auto Play")]
        [Tooltip("If true, starts the periodic interval loop automatically when enabled.")]
        [SerializeField] private bool playOnEnable = true;

        // ── Private State ─────────────────────────────────────────────────────

        private Vector3 _originalScale = Vector3.one;
        private Coroutine _loopCoroutine;
        private Sequence _activeSequence;
        private bool _scaleCached = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            CacheOriginalScale();
        }

        private void Start()
        {
            CacheOriginalScale();
        }

        private void OnEnable()
        {
            CacheOriginalScale();
            if (playOnEnable)
            {
                StartIntervalLoop();
            }
        }

        private void OnDisable()
        {
            StopIntervalLoop();
            KillActiveTweens();
            if (_scaleCached)
            {
                transform.localScale = _originalScale;
            }
        }

        private void OnDestroy()
        {
            StopIntervalLoop();
            KillActiveTweens();
        }

        private void CacheOriginalScale()
        {
            if (!_scaleCached)
            {
                if (transform.localScale != Vector3.zero)
                {
                    _originalScale = transform.localScale;
                    _scaleCached = true;
                }
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Starts or restarts the periodic pop interval loop.
        /// </summary>
        public void StartIntervalLoop()
        {
            StopIntervalLoop();
            if (gameObject.activeInHierarchy && enabled)
            {
                _loopCoroutine = StartCoroutine(IntervalLoopCoroutine());
            }
        }

        /// <summary>
        /// Stops the periodic pop interval loop.
        /// </summary>
        public void StopIntervalLoop()
        {
            if (_loopCoroutine != null)
            {
                StopCoroutine(_loopCoroutine);
                _loopCoroutine = null;
            }
        }

        /// <summary>
        /// Manually triggers the pop up and down scale animation once.
        /// </summary>
        [ContextMenu("Test Pop Animation")]
        public void PlayPopAnimation()
        {
            if (!gameObject.activeInHierarchy) return;

            CacheOriginalScale();
            KillActiveTweens();
            transform.localScale = _originalScale;

            _activeSequence = DOTween.Sequence();
            _activeSequence.SetUpdate(true);

            float halfDuration = animationDuration * 0.5f;

            // 1. Pop Up Phase (scales up)
            _activeSequence.Append(transform.DOScale(_originalScale * popScaleMultiplier, halfDuration).SetEase(popUpEase));

            // 2. Pop Down Phase (returns to original scale)
            _activeSequence.Append(transform.DOScale(_originalScale, halfDuration).SetEase(popDownEase));

            _activeSequence.OnComplete(() =>
            {
                transform.localScale = _originalScale;
                _activeSequence = null;
            });

            // Play SFX if configured
            if (playSound)
            {
                if (customPopSfx != null)
                {
                    var audioSource = GetComponent<AudioSource>();
                    if (audioSource != null)
                    {
                        audioSource.PlayOneShot(customPopSfx);
                    }
                }
                else if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayDialoguePopSfx();
                }
            }
        }

        // ── Coroutine Loop ────────────────────────────────────────────────────

        private IEnumerator IntervalLoopCoroutine()
        {
            if (initialDelay > 0f)
            {
                yield return new WaitForSeconds(initialDelay);
            }

            while (true)
            {
                PlayPopAnimation();

                float currentWait = interval;
                if (intervalRandomVariance > 0f)
                {
                    currentWait += Random.Range(-intervalRandomVariance, intervalRandomVariance);
                    if (currentWait < 0.1f) currentWait = 0.1f;
                }

                yield return new WaitForSeconds(currentWait);
            }
        }

        private void KillActiveTweens()
        {
            if (_activeSequence != null && _activeSequence.IsActive())
            {
                _activeSequence.Kill();
                _activeSequence = null;
            }
            transform.DOKill();
        }
    }
}
