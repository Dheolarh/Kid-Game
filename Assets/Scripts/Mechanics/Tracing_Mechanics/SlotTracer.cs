using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace KidGame.Mechanics.Tracing
{
    public class SlotTracer : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────

        [Header("Shape")]
        [Tooltip("Drag a prefab from Art/Tracing/Prefabs/Numbers (or Letters).")]
        [SerializeField] private GameObject shapePrefab;

        public GameObject ShapePrefab
        {
            get => shapePrefab;
            set => shapePrefab = value;
        }

        [Tooltip("Color the child draws the stroke in.")]
        [SerializeField] private Color traceColor = new Color(0.2f, 0.55f, 1f, 1f);

        [Tooltip("How much smaller than the slot the shape should be (0 = fill slot, 0.3 = 30% smaller).")]
        [SerializeField, Range(0f, 0.5f)] private float sizePadding = 0.3f;

        public float SizePadding
        {
            get => sizePadding;
            set => sizePadding = value;
        }

        [Tooltip("Color of the square dot that marks where to start each stroke.")]
        [SerializeField] private Color startDotColor = Color.green;

        [Tooltip("Color of the square dot that marks where each stroke ends.")]
        [SerializeField] private Color endDotColor   = Color.red;

        [Header("Tutorial Info")]
        [Tooltip("Shown in the big Example TMP  e.g.  0")]
        [SerializeField] internal string exampleText              = "";
        [Tooltip("The {word} part of the description  e.g.  zero")]
        [SerializeField] internal string descriptionWord          = "";
        [Tooltip("The {number} part of the description  e.g.  0")]
        [SerializeField] internal string descriptionNumber        = "";
        [Tooltip("If set, replaces the auto-built sentence entirely. Leave blank to use: " +
                 "\"Let's practice writing {word} \"{number}\"\"")]
        [SerializeField] internal string customDescriptionSentence = "";

        [Header("Events")]
        [Tooltip("Fires when every path in the shape has been traced.")]
        [SerializeField] private UnityEvent onCompleted = new UnityEvent();

        public UnityEvent OnCompletedEvent
        {
            get
            {
                if (onCompleted == null)
                {
                    onCompleted = new UnityEvent();
                }
                else
                {
                    try
                    {
                        onCompleted.GetPersistentEventCount();
                    }
                    catch (System.NullReferenceException)
                    {
                        onCompleted = new UnityEvent();
                    }
                }
                return onCompleted;
            }
        }

        private void Awake()
        {
            if (onCompleted == null)
            {
                onCompleted = new UnityEvent();
            }
            else
            {
                try
                {
                    onCompleted.GetPersistentEventCount();
                }
                catch (System.NullReferenceException)
                {
                    onCompleted = new UnityEvent();
                }
            }
        }

        // ──────────────────────────────────────────────────
        // Internal State (read by SlotTracingManager)
        // ──────────────────────────────────────────────────

        internal Shape shape;
        internal Path activePath;
        internal Image activePathFillImage;
        internal float targetQuarter = 90f;   // used by manager for radial fill

        public bool IsCompleted  => shape != null && shape.completed;
        public Color TraceColor  => traceColor;

        // ──────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────

        private IEnumerator Start()
        {
            if (shapePrefab == null) yield break;

            // Frame 1: wait for Canvas layout rects to be computed
            yield return null;
            EnsureShapeSpawned();

            // Frame 2: wait for Shape.Start() to call ShowPathNumbers() which
            //          resets Image colors — then we override with our own colors
            yield return null;
            ColorizeStartDots();
        }

        public void EnsureShapeSpawned()
        {
            if (shape == null)
            {
                SpawnShape();
                if (shape != null)
                {
                    shape.InitializeShape();
                }
            }
        }

        // ──────────────────────────────────────────────────
        // Setup
        // ──────────────────────────────────────────────────

        private void SpawnShape()
        {
            var go = Instantiate(shapePrefab);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.name = shapePrefab.name;
            shape   = go.GetComponent<Shape>();

            RescaleShape();

            // Tint the start-point indicator dots
            ColorizeStartDots();
        }

        public void RescaleShape()
        {
            if (shape == null) return;

            // If we are playing and TracingModeManager is present, let it handle the scale!
            if (Application.isPlaying)
            {
                var manager = FindObjectOfType<TracingModeManager>();
                if (manager != null)
                {
                    return;
                }
            }

            var slotRT  = GetComponent<RectTransform>();
            var shapeRT = shape.GetComponent<RectTransform>();


            if (slotRT != null && shapeRT != null)
            {
                Vector2 slotSize  = slotRT.rect.size;
                Vector2 shapeSize = shapeRT.rect.size;

                if (slotSize.sqrMagnitude > 0f && shapeSize.sqrMagnitude > 0f)
                {
                    float scale = Mathf.Min(slotSize.x / shapeSize.x,
                                            slotSize.y / shapeSize.y)
                                  * (1f - sizePadding);
                    shape.transform.localScale = Vector3.one * scale;
                }
                else
                {
                    shape.transform.localScale = shapePrefab.transform.localScale;
                }
            }
        }

        internal void ColorizeStartDots()
        {
            if (shape == null) return;
            foreach (var path in shape.paths)
            {
                if (path.firstNumber == null) continue;

                // The "Start" child (PolygonCollider) physically marks where tracing begins.
                // Whichever number dot is closer to it is the start; the other is the end.
                var startMarker = path.transform.Find("Start");

                Transform startNum, endNum;

                if (startMarker != null && path.secondNumber != null)
                {
                    float d1 = Vector3.Distance(startMarker.position, path.firstNumber.position);
                    float d2 = Vector3.Distance(startMarker.position, path.secondNumber.position);
                    startNum = d1 <= d2 ? path.firstNumber  : path.secondNumber;
                    endNum   = d1 <= d2 ? path.secondNumber : path.firstNumber;
                }
                else
                {
                    // Fallback: no Start child or only one number — treat firstNumber as start
                    startNum = path.firstNumber;
                    endNum   = path.secondNumber;
                }

                var startImg = startNum?.GetComponent<Image>();
                if (startImg != null) startImg.color = startDotColor;

                var endImg = endNum?.GetComponent<Image>();
                if (endImg != null) endImg.color = endDotColor;
            }
        }

        // ──────────────────────────────────────────────────
        // Called by SlotTracingManager
        // ──────────────────────────────────────────────────

        /// <summary>Begin tracing on the given path.</summary>
        internal void BeginPath(Path path)
        {
            activePath = path;
            activePathFillImage = TracingUtil
                .FindChildByTag(path.transform, "Fill")
                ?.GetComponent<Image>();

            if (activePathFillImage != null)
            {
                path.StopAllCoroutines();
                activePathFillImage.color = traceColor;
            }

            // Hide the tracing hand guide while the player is drawing
            shape.CancelInvoke();
            shape.DisableTracingHand();
        }

        /// <summary>Called when the finger/mouse is released mid-path.</summary>
        internal void ReleasePath()
        {
            if (activePath != null)
                activePath.Reset();

            ClearPath();
            targetQuarter = 90f;

            // Resume the animated hand guide after 1 second
            shape.Invoke("EnableTracingHand", 1f);
        }

        /// <summary>
        /// Called when fill amount passes the completion threshold.
        /// Marks the path done and checks if all paths are complete.
        /// </summary>
        internal void OnPathFillComplete()
        {
            if (activePath == null) return;

            activePath.completed = true;
            activePath.AutoFill();
            activePath.SetNumbersVisibility(false);

            ClearPath();

            if (AllPathsDone())
            {
                shape.completed = true;

                // Play completion animation
                var animator = shape.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.SetBool(shape.name, false);
                    animator.SetTrigger("Completed");
                }

                onCompleted?.Invoke();
            }
            else
            {
                // Move number indicators to the next incomplete path
                shape.ShowPathNumbers(shape.GetCurrentPathIndex());
                shape.Invoke("EnableTracingHand", 1f);
            }
        }

        // ──────────────────────────────────────────────────
        // Private Helpers
        // ──────────────────────────────────────────────────

        private void ClearPath()
        {
            activePath          = null;
            activePathFillImage = null;
        }

        private bool AllPathsDone()
        {
            var paths = shape.GetComponentsInChildren<Path>();
            foreach (var p in paths)
                if (!p.completed) return false;
            return true;
        }

        // ──────────────────────────────────────────────────
        // Editor Helper
        // ──────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Reset Shape")]
        private void ResetShapeEditor()
        {
            if (shape == null) return;
            shape.completed = false;

            var paths = shape.GetComponentsInChildren<Path>();
            foreach (var p in paths) p.Reset();

            shape.ShowPathNumbers(0);
            targetQuarter = 90f;
        }
#endif
    }
}
