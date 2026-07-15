using UnityEngine;
using System.Collections.Generic;

namespace KidGame.Mechanics.Tracing
{
    public static class TracingUtil
    {
        /// <summary>Converts a RectTransform to screen-space Rect.</summary>
        public static Rect RectTransformToScreenSpace(RectTransform transform)
        {
            Vector2 size = Vector2.Scale(transform.rect.size, transform.lossyScale);
            return new Rect(transform.position.x,
                            Screen.height - transform.position.y,
                            size.x, size.y);
        }

        /// <summary>Finds the first direct child with the given tag.</summary>
        public static Transform FindChildByTag(Transform parent, string tag)
        {
            if (parent == null || string.IsNullOrEmpty(tag)) return null;
            foreach (Transform child in parent)
                if (child.CompareTag(tag)) return child;
            return null;
        }

        // Shared reusable list to avoid per-call allocation in FindChildrenByTag hot paths.
        private static readonly List<Transform> _sharedChildBuffer = new List<Transform>(8);

        /// <summary>
        /// Fills <paramref name="results"/> with all direct children that match the tag.
        /// Does NOT allocate — clears and reuses the provided list.
        /// </summary>
        public static void FindChildrenByTag(Transform parent, string tag, List<Transform> results)
        {
            results.Clear();
            if (parent == null || string.IsNullOrEmpty(tag)) return;
            foreach (Transform child in parent)
                if (child.CompareTag(tag)) results.Add(child);
        }

        /// <summary>
        /// Finds all direct children with the given tag.
        /// Allocates a new list — prefer the overload with a pre-allocated list for hot paths.
        /// </summary>
        public static List<Transform> FindChildrenByTag(Transform parent, string tag)
        {
            var list = new List<Transform>();
            if (parent == null || string.IsNullOrEmpty(tag)) return list;
            foreach (Transform child in parent)
                if (child.CompareTag(tag)) list.Add(child);
            return list;
        }
    }
}
