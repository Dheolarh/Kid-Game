using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{
    /// <summary>
    /// Attach this component to any UI prefabs (like answer slots, buttons, or frames)
    /// to dynamically colorize them with the active theme color when they are spawned.
    /// </summary>
    public class ThemeColorBinder : MonoBehaviour
    {
        [Tooltip("GameObjects whose Outline components (including children) should be tinted with the active theme color.")]
        [SerializeField] private GameObject[] outlineHolders;

        [Tooltip("Specific Outline components to tint with the active theme color.")]
        [SerializeField] private Outline[] specificOutlines;

        [Tooltip("Image components to tint with the active theme color.")]
        [SerializeField] private Image[] images;

        private void OnEnable()
        {
            ApplyThemeColor();
        }

        private void Start()
        {
            ApplyThemeColor();
        }

        /// <summary>
        /// Retrieves the active theme color from GameFlowManager and applies it.
        /// </summary>
        public void ApplyThemeColor()
        {
            if (GameFlowManager.Instance == null) return;

            Color themeColor = GameFlowManager.Instance.GetActiveThemeColor();

            // 1. Tint outlines inside registered GameObjects/children
            if (outlineHolders != null)
            {
                foreach (var holder in outlineHolders)
                {
                    if (holder != null)
                    {
                        var foundOutlines = holder.GetComponentsInChildren<Outline>(true);
                        foreach (var outline in foundOutlines)
                        {
                            if (outline != null)
                            {
                                outline.effectColor = themeColor;
                            }
                        }
                    }
                }
            }

            // 2. Tint specific outlines
            if (specificOutlines != null)
            {
                foreach (var outline in specificOutlines)
                {
                    if (outline != null)
                    {
                        outline.effectColor = themeColor;
                    }
                }
            }

            // 3. Tint images
            if (images != null)
            {
                foreach (var img in images)
                {
                    if (img != null)
                    {
                        img.color = themeColor;
                    }
                }
            }
        }
    }
}
