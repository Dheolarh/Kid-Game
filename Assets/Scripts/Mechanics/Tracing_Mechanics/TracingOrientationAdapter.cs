using System.Collections;
using UnityEngine;
using UnityEngine.UI;


namespace KidGame.Mechanics.Tracing
{
    [ExecuteAlways]
    public class TracingOrientationAdapter : MonoBehaviour
    {
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
            portraitPanel.SetActive(true);
            landscapePanel.SetActive(true);

            if (Application.isPlaying)
            {
                for (int i = 0; i < 5; i++) yield return null;

                _wasLandscape = IsLandscape;
                (IsLandscape ? portraitPanel : landscapePanel).SetActive(false);

                Debug.Log($"[TracingOrientation] Initialized in {(IsLandscape ? "landscape" : "portrait")} mode.");
            }
            else
            {
                _wasLandscape = IsLandscape;
                (IsLandscape ? portraitPanel : landscapePanel).SetActive(false);
                yield break;
            }
        }

        private void Update()
        {
            if (this == null) return;
            if (portraitPanel == null || landscapePanel == null) return;

            bool landscape = IsLandscape;
            if (landscape != _wasLandscape)
            {
                _wasLandscape = landscape;
                if (Application.isPlaying)
                {
                    StartCoroutine(SwitchOrientationRoutine(landscape));
                }
                else
                {
                    portraitPanel.SetActive(!landscape);
                    landscapePanel.SetActive(landscape);
                }
            }
        }

        // ── Orientation Switch ────────────────────────────────────────────────

        private IEnumerator SwitchOrientationRoutine(bool toLandscape)
        {
            if (this == null || portraitPanel == null || landscapePanel == null) yield break;

            // Enable incoming panel, disable outgoing
            portraitPanel .SetActive(!toLandscape);
            landscapePanel.SetActive( toLandscape);

            // Wait for Animators to initialize from their entry state
            yield return null;
            if (this == null || portraitPanel == null || landscapePanel == null) yield break;
            yield return null;
            if (this == null || portraitPanel == null || landscapePanel == null) yield break;
            yield return null;
            if (this == null || portraitPanel == null || landscapePanel == null) yield break;

            // Source panel is now inactive; destination is active
            var fromPanel = toLandscape ? portraitPanel  : landscapePanel;
            var toPanel   = toLandscape ? landscapePanel : portraitPanel;

            // 1. Sync inspector-assigned pairs (if any)
            if (tracerPairs != null)
            {
                foreach (var pair in tracerPairs)
                {
                    if (this == null) yield break;
                    var from = toLandscape ? pair.portrait  : pair.landscape;
                    var to   = toLandscape ? pair.landscape : pair.portrait;
                    SyncTracerState(from, to);
                }
            }

            if (this == null || fromPanel == null || toPanel == null) yield break;

            var fromTracers = fromPanel.GetComponentsInChildren<SlotTracer>(includeInactive: true);
            var toTracers   = toPanel  .GetComponentsInChildren<SlotTracer>(includeInactive: true);

            int count = Mathf.Min(fromTracers.Length, toTracers.Length);
            for (int i = 0; i < count; i++)
            {
                if (this == null) yield break;
                SyncTracerState(fromTracers[i], toTracers[i]);
            }

            Debug.Log($"[TracingOrientation] Switched to {(toLandscape ? "landscape" : "portrait")}. Synced {count} tracer(s).");
        }


        private static void SyncTracerState(SlotTracer from, SlotTracer to)
        {
            if (from == null || to == null) return;

            from.EnsureShapeSpawned();
            to.EnsureShapeSpawned();

            from.RescaleShape();
            to.RescaleShape();

            if (from.shape == null || to.shape == null) return;

            var fromPaths = from.shape.GetComponentsInChildren<Path>(includeInactive: true);
            var toPaths   = to  .shape.GetComponentsInChildren<Path>(includeInactive: true);

            int count = Mathf.Min(fromPaths.Length, toPaths.Length);

            for (int i = 0; i < count; i++)
            {
                if (fromPaths[i] == null || toPaths[i] == null) continue;
                if (!fromPaths[i].completed || toPaths[i].completed) continue;

                // Apply the tracer's stroke color to the fill image BEFORE auto-filling,
                // otherwise it stays white (BeginPath was never called on this panel's path).
                var fillImg = TracingUtil.FindChildByTag(toPaths[i].transform, "Fill")
                                         ?.GetComponent<Image>();
                if (fillImg != null)
                    fillImg.color = to.TraceColor;

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
