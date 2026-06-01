using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace KidGame.Mechanics.Tracing
{
    public class Shape : MonoBehaviour
    {
        // ── Data ──────────────────────────────────────────────────
        public List<Path> paths = new List<Path>();

        [Range(0, 500)] public int threeStarsTimePeriod = 10;
        [Range(0, 500)] public int twoStarsTimePeriod   = 20;

        [HideInInspector] public bool completed;
        [HideInInspector] public bool enablePriorityOrder = true;

        // ── Lifecycle ─────────────────────────────────────────────
        private void Start()
        {
            // Standalone slot mode — always initialise immediately
            if (paths.Count != 0)
            {
                Invoke(nameof(EnableTracingHand), 0.2f);
                ShowPathNumbers(0);
            }
        }

        // ── Public API ────────────────────────────────────────────
        /// <summary>Show the step numbers for the path at <paramref name="index"/>.</summary>
        public void ShowPathNumbers(int index)
        {
            for (int i = 0; i < paths.Count; i++)
                paths[i].SetNumbersStatus(i == index);
        }

        /// <summary>Index of the first incomplete path, or -1 if all done.</summary>
        public int GetCurrentPathIndex()
        {
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].completed) continue;

                bool isNext = true;
                for (int j = 0; j < i; j++)
                    if (!paths[j].completed) { isNext = false; break; }

                if (isNext) return i;
            }
            return -1;
        }

        /// <summary>Returns true when <paramref name="path"/> is the one the child should draw next.</summary>
        public bool IsCurrentPath(Path path)
        {
            if (!enablePriorityOrder) return true;
            if (path == null) return false;

            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i].GetInstanceID() != path.GetInstanceID()) continue;
                for (int j = 0; j < i; j++)
                    if (!paths[j].completed) return false;
                return true;
            }
            return false;
        }

        /// <summary>Triggers the animated hand guide for the current path.</summary>
        public void EnableTracingHand()
        {
            int idx = GetCurrentPathIndex();
            if (idx == -1) return;

            var animator = GetComponent<Animator>();
            if (animator == null) return;

            animator.SetTrigger(name);
            animator.SetTrigger(paths[idx].name.Replace("Path", name.Split('-')[0]));
        }

        /// <summary>Stops the animated hand guide.</summary>
        public void DisableTracingHand()
        {
            int idx = GetCurrentPathIndex();
            if (idx == -1) return;

            var animator = GetComponent<Animator>();
            if (animator == null) return;

            animator.SetBool(paths[idx].name.Replace("Path", name.Split('-')[0]), false);
        }

        /// <summary>Returns the display title of this shape (e.g. "0" from "0-Number").</summary>
        public string GetTitle() => name.Split('-')[0];
    }
}
