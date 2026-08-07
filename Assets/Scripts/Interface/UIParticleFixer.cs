using UnityEngine;

namespace GoldenPenny.Animations
{
    /// <summary>
    /// Add this script to any Particle System placed in a UI Canvas.
    /// It automatically finds all child particle renderers and forces their sorting order
    /// to be higher than the Canvas so they render correctly on top of UI elements.
    /// </summary>
    [ExecuteAlways]
    public class UIParticleFixer : MonoBehaviour
    {
        [Tooltip("The sorting order offset to apply above the Canvas.")]
        public int sortingOrderOffset = 100;

        private void OnEnable()
        {
            FixSorting();
        }

        private void OnValidate()
        {
            FixSorting();
        }

        [ContextMenu("Force Fix Sorting")]
        public void FixSorting()
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null) return;

            string targetLayer = parentCanvas.sortingLayerName;
            int targetOrder = parentCanvas.sortingOrder + sortingOrderOffset;

            ParticleSystemRenderer[] renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            foreach (var r in renderers)
            {
                r.sortingLayerName = targetLayer;
                r.sortingOrder = targetOrder;
            }

            // Also ensure Z position is slightly closer to camera to prevent clipping
            Vector3 localPos = transform.localPosition;
            if (localPos.z >= 0)
            {
                localPos.z = -10f;
                transform.localPosition = localPos;
            }
        }
    }
}
