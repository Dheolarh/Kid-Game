using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace KidGame.Mechanics.Tracing
{
    public class SlotTracingManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────────────

        [Header("Camera")]
        [Tooltip("The camera that renders your Canvas (Screen Space – Camera mode required).")]
        [SerializeField] private Camera tracingCamera;

        // ──────────────────────────────────────────────────
        // Private State
        // ──────────────────────────────────────────────────

        private SlotTracer     _activeTracer;       // slot currently being drawn
        private float          _fillAmount;
        private Vector3        _clickPos;
        private RaycastHit2D   _hit;
        private ScrollRect     _scrollRect;         // locked while tracing

        // ──────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            if (tracingCamera == null)
                tracingCamera = Camera.main;
        }

        private void Update()
        {
            bool isTouch   = Touchscreen.current != null
                             && Touchscreen.current.touches.Count > 0;

            bool pressed  = false;
            bool released = false;
            bool held     = false;

            if (isTouch)
            {
                var phase = Touchscreen.current.touches[0].phase.ReadValue();
                pressed  = phase == UnityEngine.InputSystem.TouchPhase.Began;
                released = phase == UnityEngine.InputSystem.TouchPhase.Ended
                        || phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                held     = phase == UnityEngine.InputSystem.TouchPhase.Moved
                        || phase == UnityEngine.InputSystem.TouchPhase.Stationary
                        || pressed;
            }
            else if (Mouse.current != null)
            {
                pressed  = Mouse.current.leftButton.wasPressedThisFrame;
                released = Mouse.current.leftButton.wasReleasedThisFrame;
                held     = Mouse.current.leftButton.isPressed;
            }

            if (pressed)       OnPointerDown();
            else if (released) OnPointerUp();

            if (held) OnPointerHeld();
        }

        // ──────────────────────────────────────────────────
        // Input Handlers
        // ──────────────────────────────────────────────────

        private void OnPointerDown()
        {
            _hit = Physics2D.Raycast(GetWorldPos(), Vector2.zero);
            if (_hit.collider == null) return;
            if (!_hit.transform.CompareTag("Start")) return;

            var path   = _hit.transform.GetComponentInParent<Path>();
            var tracer = _hit.transform.GetComponentInParent<SlotTracer>();

            if (tracer == null || path == null) return;
            if (path.completed) return;
            if (!tracer.shape.IsCurrentPath(path)) return;

            // Release any previously active tracer
            if (_activeTracer != null && _activeTracer != tracer)
                _activeTracer.ReleasePath();

            _activeTracer = tracer;
            _activeTracer.BeginPath(path);

            // Lock the ScrollRect so dragging draws instead of scrolling
            _scrollRect = tracer.GetComponentInParent<ScrollRect>();
            if (_scrollRect != null) _scrollRect.enabled = false;
        }

        private void OnPointerUp()
        {
            if (_activeTracer == null) return;
            _activeTracer.ReleasePath();
            _activeTracer = null;

            // Restore scroll
            if (_scrollRect != null)
            {
                _scrollRect.enabled = true;
                _scrollRect = null;
            }
        }

        private void OnPointerHeld()
        {
            if (_activeTracer == null) return;

            var path      = _activeTracer.activePath;
            var fillImage = _activeTracer.activePathFillImage;

            if (path == null || fillImage == null || path.completed) return;

            switch (path.fillMethod)
            {
                case Path.FillMethod.Radial:
                    RadialFill(path, fillImage);
                    break;

                case Path.FillMethod.Linear:
                    LinearFill(path, fillImage);
                    break;

                case Path.FillMethod.Point:
                    PointFill(path, fillImage);
                    break;
            }
        }

        // ──────────────────────────────────────────────────
        // Fill Methods  (ported from GameManager, standalone)
        // ──────────────────────────────────────────────────

        private void RadialFill(Path path, Image fillImage)
        {
            _clickPos = GetWorldPos();

            var direction     = (Vector2)(_clickPos - path.transform.position);
            var clockWise     = fillImage.fillClockwise ? 1f : -1f;
            float angleOffset = 0f;

            switch (fillImage.fillOrigin)
            {
                case 0: angleOffset = 0f;                  break; // Bottom
                case 1: angleOffset = clockWise  * 90f;   break; // Right
                case 2: angleOffset = -180f;               break; // Top
                case 3: angleOffset = -clockWise * 90f;   break; // Left
            }

            float angle = Mathf.Atan2(-clockWise * direction.x, -direction.y)
                          * Mathf.Rad2Deg + angleOffset;

            if (angle < 0f) angle += 360f;
            angle = Mathf.Clamp(angle, 0f, 360f);
            angle -= path.radialAngleOffset;

            if (path.quarterRestriction)
            {
                float tq = _activeTracer.targetQuarter;
                if (!(angle >= 0f && angle <= tq))
                {
                    fillImage.fillAmount = 0f;
                    return;
                }
                if (angle >= tq / 2f) tq += 90f;
                else if (angle < 45f) tq  = 90f;

                _activeTracer.targetQuarter = Mathf.Clamp(tq, 90f, 360f);
            }

            _fillAmount              = Mathf.Abs(angle / 360f);
            fillImage.fillAmount     = _fillAmount;
            CheckFillComplete(path);
        }

        private void LinearFill(Path path, Image fillImage)
        {
            _clickPos = GetWorldPos();

            var rotation = path.transform.eulerAngles;
            rotation.z -= path.offset;

            var rect = TracingUtil.RectTransformToScreenSpace(
                           path.GetComponent<RectTransform>());

            Vector3 pos1 = Vector3.zero, pos2 = Vector3.zero;

            if (path.type == Path.ShapeType.Horizontal)
            {
                pos1.x = path.transform.position.x - Mathf.Sin(rotation.z * Mathf.Deg2Rad) * rect.width  / 2f;
                pos1.y = path.transform.position.y - Mathf.Cos(rotation.z * Mathf.Deg2Rad) * rect.width  / 2f;
                pos2.x = path.transform.position.x + Mathf.Sin(rotation.z * Mathf.Deg2Rad) * rect.width  / 2f;
                pos2.y = path.transform.position.y + Mathf.Cos(rotation.z * Mathf.Deg2Rad) * rect.width  / 2f;
            }
            else
            {
                pos1.x = path.transform.position.x - Mathf.Cos(rotation.z * Mathf.Deg2Rad) * rect.height / 2f;
                pos1.y = path.transform.position.y - Mathf.Sin(rotation.z * Mathf.Deg2Rad) * rect.height / 2f;
                pos2.x = path.transform.position.x + Mathf.Cos(rotation.z * Mathf.Deg2Rad) * rect.height / 2f;
                pos2.y = path.transform.position.y + Mathf.Sin(rotation.z * Mathf.Deg2Rad) * rect.height / 2f;
            }

            pos1.z = pos2.z = path.transform.position.z;

            if (path.flip) { var tmp = pos2; pos2 = pos1; pos1 = tmp; }

            _clickPos.x = Mathf.Clamp(_clickPos.x, Mathf.Min(pos1.x, pos2.x), Mathf.Max(pos1.x, pos2.x));
            _clickPos.y = Mathf.Clamp(_clickPos.y, Mathf.Min(pos1.y, pos2.y), Mathf.Max(pos1.y, pos2.y));

            _fillAmount          = Vector2.Distance(_clickPos, pos1) / Vector2.Distance(pos1, pos2);
            fillImage.fillAmount = _fillAmount;
            CheckFillComplete(path);
        }

        private void PointFill(Path path, Image fillImage)
        {
            _fillAmount          = 1f;
            fillImage.fillAmount = 1f;
            CheckFillComplete(path);
        }

        // ──────────────────────────────────────────────────
        // Completion Check
        // ──────────────────────────────────────────────────

        private void CheckFillComplete(Path path)
        {
            if (_fillAmount < path.completeOffset) return;

            _activeTracer.OnPathFillComplete();
            _activeTracer.ColorizeStartDots();   // re-apply after ShowPathNumbers() reset colors

            // Try to chain immediately into the next path if still touching it
            _hit = Physics2D.Raycast(GetWorldPos(), Vector2.zero);
            if (_hit.collider != null && _hit.transform.CompareTag("Start"))
            {
                var nextPath   = _hit.transform.GetComponentInParent<Path>();
                var nextTracer = _hit.transform.GetComponentInParent<SlotTracer>();
                if (nextTracer != null && nextPath != null
                    && !nextPath.completed
                    && nextTracer.shape.IsCurrentPath(nextPath))
                {
                    _activeTracer = nextTracer;
                    _activeTracer.BeginPath(nextPath);
                }
                else
                {
                    _activeTracer = null;
                }
            }
            else
            {
                _activeTracer = null;
            }
        }

        // ──────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────

        private Vector3 GetWorldPos()
        {
            Vector2 screenPos = Vector2.zero;

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
                screenPos = Touchscreen.current.touches[0].position.ReadValue();
            else if (Mouse.current != null)
                screenPos = Mouse.current.position.ReadValue();

            var pos = tracingCamera.ScreenToWorldPoint(
                          new Vector3(screenPos.x, screenPos.y, 0f));
            pos.z = 0f;
            return pos;
        }
    }
}
