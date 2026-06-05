using System.Collections;
using UnityEngine;

namespace KidGame.Mechanics.Tracing
{
    /// <summary>
    /// Handles portrait ↔ landscape switching for the tracing game.
    ///
    /// Setup in Inspector:
    /// 1. Assign <see cref="portraitPanel"/> and <see cref="landscapePanel"/> (the top-level
    ///    GameObjects that contain each layout's SlotTracers).
    /// 2. Populate <see cref="tracerPairs"/> — one entry per letter/shape slot.
    ///    Each entry links the portrait SlotTracer to its landscape counterpart.
    ///
    /// How it works:
    /// - Both panels are activated for 3 frames at Start so every SlotTracer
    ///   can spawn and size its shape.
    /// - After that, the inactive-orientation panel is disabled (disabling its
    ///   Physics2D colliders so they don't interfere with raycasts).
    /// - On rotation, completion state is copied path-by-path before the switch
    ///   so the child resumes exactly where they left off.
    /// </summary>
    public class TracingOrientationAdapter : MonoBehaviour
    {
        // ── Data ──────────────────────────────────────────────────────────────

        [System.Serializable]
        public struct TracerPair
        {
            [Tooltip("The SlotTracer in the PORTRAIT layout for this slot.")]
            public SlotTracer portrait;
            [Tooltip("The SlotTracer in the LANDSCAPE layout for this slot.")]
            public SlotTracer landscape;
        }

        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Panels")]
        [Tooltip("Root GameObject of the portrait layout (contains all portrait SlotTracers).")]
        [SerializeField] private GameObject portraitPanel;
        [Tooltip("Root GameObject of the landscape layout (contains all landscape SlotTracers).")]
        [SerializeField] private GameObject landscapePanel;

        [Header("Tracer Pairs")]
        [Tooltip("One entry per letter/shape. Match portrait[i] to landscape[i].")]
        [SerializeField] private TracerPair[] tracerPairs;

        // ── Runtime state ─────────────────────────────────────────────────────

        private bool _wasLandscape;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private IEnumerator Start()
        {
            // Activate BOTH panels so every SlotTracer runs its Start() coroutine
            // and spawns + sizes its shape correctly for its own rect dimensions.
            portraitPanel.SetActive(true);
            landscapePanel.SetActive(true);

            // SlotTracer.Start() does: yield null → SpawnShape → yield null → ColorizeStartDots
            // We wait 3 frames to be safe.
            yield return null;
            yield return null;
            yield return null;

            // Now hide the panel that doesn't match the current orientation
            _wasLandscape = IsLandscape;
            (IsLandscape ? portraitPanel : landscapePanel).SetActive(false);

            Debug.Log($"[TracingOrientation] Initialized in {(IsLandscape ? "landscape" : "portrait")} mode.");
        }

        private void Update()
        {
            bool landscape = IsLandscape;
            if (landscape != _wasLandscape)
            {
                _wasLandscape = landscape;
                SwitchOrientation(landscape);
            }
        }

        // ── Orientation Switch ────────────────────────────────────────────────

        private void SwitchOrientation(bool toLandscape)
        {
            // 1. Sync progress from active → incoming panel
            foreach (var pair in tracerPairs)
            {
                var from = toLandscape ? pair.portrait  : pair.landscape;
                var to   = toLandscape ? pair.landscape : pair.portrait;
                SyncTracerState(from, to);
            }

            // 2. Swap panels
            portraitPanel .SetActive(!toLandscape);
            landscapePanel.SetActive( toLandscape);

            Debug.Log($"[TracingOrientation] Switched to {(toLandscape ? "landscape" : "portrait")}.");
        }

        /// <summary>
        /// Copies path completion state from <paramref name="from"/> to <paramref name="to"/>
        /// so the incoming panel reflects the player's current progress.
        /// </summary>
        private static void SyncTracerState(SlotTracer from, SlotTracer to)
        {
            if (from == null || to == null)   return;
            if (from.shape == null || to.shape == null) return;

            var fromPaths = from.shape.GetComponentsInChildren<Path>(includeInactive: true);
            var toPaths   = to  .shape.GetComponentsInChildren<Path>(includeInactive: true);

            int count = Mathf.Min(fromPaths.Length, toPaths.Length);

            for (int i = 0; i < count; i++)
            {
                if (!fromPaths[i].completed || toPaths[i].completed) continue;

                // Mark path as done and fill it visually
                toPaths[i].completed = true;
                toPaths[i].AutoFill();
                toPaths[i].SetNumbersVisibility(false);
            }

            // Sync overall shape completion
            if (from.shape.completed && !to.shape.completed)
            {
                to.shape.completed = true;

                var anim = to.shape.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetBool(to.shape.name, false);
                    anim.SetTrigger("Completed");
                }
            }

            // Update number-dot positions on the incoming panel to show the next path
            if (!to.IsCompleted)
            {
                int idx = to.shape.GetCurrentPathIndex();
                to.shape.ShowPathNumbers(idx);
                to.ColorizeStartDots();
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static bool IsLandscape => Screen.width > Screen.height;
    }
}
