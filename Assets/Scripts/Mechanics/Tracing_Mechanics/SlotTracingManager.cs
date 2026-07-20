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

        // Cached path geometry — populated once per BeginPath to avoid per-frame allocations
        private RectTransform  _activePathRt;
        private Vector3        _cachedPos1;         // start world-space point for linear fill
        private Vector3        _cachedPos2;         // end world-space point for linear fill
        private bool           _linearCacheDirty = true;

        // ──────────────────────────────────────────────────
        // Unity Lifecycle
        // ──────────────────────────────────────────────────

        private void Awake()
        {
            // Resolve the camera once
            if (tracingCamera == null)
            {
#if UNITY_2023_1_OR_NEWER
                tracingCamera = Camera.main ?? FindFirstObjectByType<Camera>();
#else
                tracingCamera = Camera.main ?? FindObjectOfType<Camera>();
#endif
            }

            // Also push it to the Canvas so Screen Space – Camera renders correctly
            var canvas = GetComponent<Canvas>();
            if (canvas != null
                && canvas.renderMode == RenderMode.ScreenSpaceCamera
                && canvas.worldCamera == null
                && tracingCamera != null)
            {
                canvas.worldCamera = tracingCamera;
            }
        }

        private Vector3 GetWorldPos()
        {
            Vector2 screenPos = Vector2.zero;

            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
                screenPos = Touchscreen.current.touches[0].position.ReadValue();
            else if (Mouse.current != null)
                screenPos = Mouse.current.position.ReadValue();

            if (tracingCamera == null) return Vector3.zero;

            var pos = tracingCamera.ScreenToWorldPoint(
                          new Vector3(screenPos.x, screenPos.y, tracingCamera.nearClipPlane));
            pos.z = 0f;
            return pos;
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

            // Cache the path's RectTransform so LinearFill doesn't call GetComponent every frame
            _activePathRt = path.GetComponent<RectTransform>();
            _linearCacheDirty = true;

            // Lock the ScrollRect so dragging draws instead of scrolling
            _scrollRect = tracer.GetComponentInParent<ScrollRect>();
            if (_scrollRect != null) _scrollRect.enabled = false;
        }

        private void OnPointerUp()
        {
            if (_activeTracer != null)
            {
                _activeTracer.ReleasePath();
                _activeTracer = null;
            }

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

            // Recompute the start/end world positions only when a new path segment begins.
            // Avoids new Rect() allocation and GetComponent call every frame.
            if (_linearCacheDirty && _activePathRt != null)
            {
                _linearCacheDirty = false;

                var rotation = path.transform.eulerAngles;
                rotation.z -= path.offset;

                // Use lossyScale to convert local rect size to world space (no new Rect allocation)
                float width  = _activePathRt.rect.width  * _activePathRt.lossyScale.x;
                float height = _activePathRt.rect.height * _activePathRt.lossyScale.y;

                Vector3 center = path.transform.position;

                if (path.type == Path.ShapeType.Horizontal)
                {
                    _cachedPos1.x = center.x - Mathf.Sin(rotation.z * Mathf.Deg2Rad) * width  / 2f;
                    _cachedPos1.y = center.y - Mathf.Cos(rotation.z * Mathf.Deg2Rad) * width  / 2f;
                    _cachedPos2.x = center.x + Mathf.Sin(rotation.z * Mathf.Deg2Rad) * width  / 2f;
                    _cachedPos2.y = center.y + Mathf.Cos(rotation.z * Mathf.Deg2Rad) * width  / 2f;
                }
                else
                {
                    _cachedPos1.x = center.x - Mathf.Cos(rotation.z * Mathf.Deg2Rad) * height / 2f;
                    _cachedPos1.y = center.y - Mathf.Sin(rotation.z * Mathf.Deg2Rad) * height / 2f;
                    _cachedPos2.x = center.x + Mathf.Cos(rotation.z * Mathf.Deg2Rad) * height / 2f;
                    _cachedPos2.y = center.y + Mathf.Sin(rotation.z * Mathf.Deg2Rad) * height / 2f;
                }

                _cachedPos1.z = _cachedPos2.z = center.z;

                if (path.flip) { var tmp = _cachedPos2; _cachedPos2 = _cachedPos1; _cachedPos1 = tmp; }
            }

            _clickPos.x = Mathf.Clamp(_clickPos.x, Mathf.Min(_cachedPos1.x, _cachedPos2.x), Mathf.Max(_cachedPos1.x, _cachedPos2.x));
            _clickPos.y = Mathf.Clamp(_clickPos.y, Mathf.Min(_cachedPos1.y, _cachedPos2.y), Mathf.Max(_cachedPos1.y, _cachedPos2.y));

            _fillAmount          = Vector2.Distance(_clickPos, _cachedPos1) / Vector2.Distance(_cachedPos1, _cachedPos2);
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

                    // FIX: Must update the geometry cache for the new path so it doesn't 
                    // use the previous path's dimensions and instantly autocomplete!
                    _activePathRt = nextPath.GetComponent<RectTransform>();
                    _linearCacheDirty = true;
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
        // Helpers  (GetWorldPos is defined earlier, near Awake)
        // ──────────────────────────────────────────────────
    }
}
