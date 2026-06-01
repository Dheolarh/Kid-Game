using UnityEngine;

namespace KidGame.Interface
{
    [RequireComponent(typeof(ProgressionBar))]
    public class ProgressionBarTester : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // Inspector Controls
        // ──────────────────────────────────────────────────

        [Header("Simulate Progress")]
        [Tooltip("Drag this slider (0 – 100) to test the progression bar in Play Mode.")]
        [Range(0f, 100f)]
        [SerializeField] private float simulatedProgress = 0f;

        [Space]
        [Header("Auto-Fill Test")]
        [Tooltip("When enabled the bar automatically fills from 0 to 100 and loops.")]
        [SerializeField] private bool autoFill = false;

        [Tooltip("How many seconds it takes to go from 0 % to 100 %.")]
        [SerializeField, Min(0.1f)] private float autoFillDuration = 5f;

        // ──────────────────────────────────────────────────
        // Private
        // ──────────────────────────────────────────────────

        private ProgressionBar _bar;
        private float _previousSimulated = -1f; // track changes
        private float _autoTimer = 0f;

        // ──────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            _bar = GetComponent<ProgressionBar>();
        }

        private void Update()
        {
            if (autoFill)
            {
                _autoTimer += Time.deltaTime;
                if (_autoTimer > autoFillDuration)
                    _autoTimer = 0f;

                float normalised = _autoTimer / autoFillDuration;
                simulatedProgress = normalised * 100f; // keep slider in sync
                _bar.SetFill(normalised);
            }
            else
            {
                // Only push to the bar when the slider value actually changes
                if (!Mathf.Approximately(simulatedProgress, _previousSimulated))
                {
                    _bar.SetFill(simulatedProgress / 100f);
                    _previousSimulated = simulatedProgress;
                }
            }
        }

        // ──────────────────────────────────────────────────
        // Context-Menu Helpers (right-click in Inspector)
        // ──────────────────────────────────────────────────

        [ContextMenu("Test → Set to 0%")]
        private void TestEmpty()
        {
            simulatedProgress = 0f;
            _bar?.SetFill(0f);
            _bar?.SnapToTarget();
        }

        [ContextMenu("Test → Set to 50%")]
        private void TestHalf()
        {
            simulatedProgress = 50f;
            _bar?.SetFill(0.5f);
        }

        [ContextMenu("Test → Set to 100%")]
        private void TestFull()
        {
            simulatedProgress = 100f;
            _bar?.SetFill(1f);
            _bar?.SnapToTarget();
        }
    }
}
