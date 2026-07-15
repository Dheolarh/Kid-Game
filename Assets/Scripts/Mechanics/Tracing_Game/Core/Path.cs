using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace KidGame.Mechanics.Tracing
{
    public class Path : MonoBehaviour
    {
        // ── Settings ──────────────────────────────────────────────
        public bool  flip;
        public FillMethod fillMethod;
        public ShapeType  type = ShapeType.Vertical;

        public float offset            = 90f;
        public float completeOffset    = 0.85f;
        public float radialAngleOffset = 0f;
        public bool  quarterRestriction = true;

        public Transform firstNumber;
        public Transform secondNumber;

        [HideInInspector] public bool  completed;
        [HideInInspector] public Shape shape;

        private bool _autoFill;

        // ── Lifecycle ─────────────────────────────────────────────
        private void Awake()
        {
            shape = GetComponentInParent<Shape>();
        }

        // ── Public API ────────────────────────────────────────────
        public void AutoFill() => StartCoroutine(AutoFillCoroutine());

        // Cached references to avoid per-call GetComponent lookups in hot paths
        private Collider2D  _startCollider;
        private readonly List<Transform> _numberBuffer = new List<Transform>(4);

        private void Start()
        {
            // Cache start collider once
            _startCollider = transform.Find("Start")?.GetComponent<Collider2D>();
        }

        public void SetNumbersStatus(bool active)
        {
            // Zero-allocation: reuse _numberBuffer instead of creating a new list
            TracingUtil.FindChildrenByTag(transform.Find("Numbers"), "Number", _numberBuffer);
            Color c = Color.white;

            foreach (Transform n in _numberBuffer)
            {
                if (n == null) continue;

                if (active)
                {
                    EnableStartCollider();
                    n.GetComponent<Animator>()?.SetBool("Select", true);
                    c.a = 1f;
                }
                else
                {
                    if (shape != null && (shape.enablePriorityOrder || completed))
                        DisableStartCollider();
                    if (shape != null && shape.enablePriorityOrder)
                    {
                        n.GetComponent<Animator>()?.SetBool("Select", false);
                        c.a = 0.3f;
                    }
                }

                n.GetComponent<Image>().color = c;
            }
        }

        public void SetNumbersVisibility(bool visible)
        {
            // Zero-allocation: reuse _numberBuffer instead of creating a new list
            TracingUtil.FindChildrenByTag(transform.Find("Numbers"), "Number", _numberBuffer);
            foreach (Transform n in _numberBuffer)
                if (n != null) n.gameObject.SetActive(visible);
        }

        public void EnableStartCollider()
        {
            var col = transform.Find("Start")?.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        public void DisableStartCollider()
        {
            var col = transform.Find("Start")?.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }

        public void Reset()
        {
            SetNumbersVisibility(true);
            completed = false;
            if (shape != null && !shape.enablePriorityOrder)
                SetNumbersStatus(true);
            StartCoroutine(ReleaseCoroutine());
        }

        // ── Coroutines ────────────────────────────────────────────
        private IEnumerator AutoFillCoroutine()
        {
            var img = TracingUtil.FindChildByTag(transform, "Fill")
                                 ?.GetComponent<Image>();
            if (img == null) yield break;

            while (img.fillAmount < 1f)
            {
                img.fillAmount += 0.02f;
                yield return new WaitForSeconds(0.001f);
            }
        }

        private IEnumerator ReleaseCoroutine()
        {
            var img = TracingUtil.FindChildByTag(transform, "Fill")
                                 ?.GetComponent<Image>();
            if (img == null) yield break;

            while (img.fillAmount > 0f)
            {
                img.fillAmount -= 0.02f;
                yield return new WaitForSeconds(0.005f);
            }
        }

        // ── Enums ─────────────────────────────────────────────────
        public enum ShapeType   { Horizontal, Vertical }
        public enum FillMethod  { Radial, Linear, Point }
        public enum CenterReference { PATH_GAMEOBJECT, FILL_GAMEOBJECT }
    }
}