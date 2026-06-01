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

        /// <summary>Finds all direct children with the given tag.</summary>
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
