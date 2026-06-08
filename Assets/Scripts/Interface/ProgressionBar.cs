using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KidGame.Interface
{
    public class ProgressionBar : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // Inspector References
        // ──────────────────────────────────────────────────

        [Header("UI References")]
        [Tooltip("The Image component used as the fill (Image Type = Filled).")]
        [SerializeField] private Image fillImage;

        [Tooltip("Optional: label that shows e.g. '75 / 100' or '75%'.")]
        [SerializeField] private TMP_Text progressLabel;

        // ──────────────────────────────────────────────────
        // Settings
        // ──────────────────────────────────────────────────

        [Header("Settings")]
        [Tooltip("Smoothing speed when the bar animates toward the target value (units per second). Set to 0 for instant.")]
        [SerializeField, Min(0f)] private float smoothSpeed = 3f;

        [Tooltip("Show value as percentage (true) or raw current/max (false).")]
        [SerializeField] private bool showAsPercentage = true;

        [Tooltip("Maximum value used when displaying the label in raw mode.")]
        [SerializeField, Min(1f)] private float maxValue = 100f;

        // ──────────────────────────────────────────────────
        // Private State
        // ──────────────────────────────────────────────────

        private float _targetFill;   // 0 – 1
        private float _currentFill;  // 0 – 1  (animated)

        // ──────────────────────────────────────────────────
        // Public API
        // ──────────────────────────────────────────────────

        /// <summary>Current fill value between 0 and 1.</summary>
        public float CurrentFill => _currentFill;

        /// <summary>Target fill value between 0 and 1.</summary>
        public float TargetFill => _targetFill;

        /// <summary>
        /// Set progress using a normalised value (0 = empty, 1 = full).
        /// </summary>
        public void SetFill(float normalised)
        {
            _targetFill = Mathf.Clamp01(normalised);
        }

        /// <summary>
        /// Set progress using a raw value and a maximum.
        /// </summary>
        public void SetValue(float current, float max)
        {
            maxValue = max;
            _targetFill = Mathf.Clamp01(max > 0f ? current / max : 0f);
        }

        /// <summary>
        /// Jump to the target immediately with no animation.
        /// </summary>
        public void SnapToTarget()
        {
            _currentFill = _targetFill;
            ApplyFill();
        }

        // ──────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Initialise from whatever is already set on the fill image
            if (fillImage != null)
                _currentFill = _targetFill = fillImage.fillAmount;
        }

        private void Update()
        {
            if (Mathf.Approximately(_currentFill, _targetFill))
                return;

            _currentFill = smoothSpeed > 0f
                ? Mathf.MoveTowards(_currentFill, _targetFill, smoothSpeed * Time.deltaTime)
                : _targetFill;

            ApplyFill();
        }

        // ──────────────────────────────────────────────────
        // Private Helpers
        // ──────────────────────────────────────────────────

        private void ApplyFill()
        {
            if (fillImage != null)
                fillImage.fillAmount = _currentFill;

            if (progressLabel != null)
            {
                progressLabel.text = showAsPercentage
                    ? $"{Mathf.RoundToInt(_currentFill * 100f)}%"
                    : $"{Mathf.RoundToInt(_currentFill * maxValue)} / {Mathf.RoundToInt(maxValue)}";
            }
        }

        // ──────────────────────────────────────────────────
        // Editor Helpers
        // ──────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEditor.EditorApplication.isCompiling) return;

            // Live-preview in the Editor while not playing
            ApplyFill();
        }
#endif
    }
}
