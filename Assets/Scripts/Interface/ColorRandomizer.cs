using UnityEngine;
using UnityEngine.UI;

namespace KidGame.Interface
{
    /// <summary>
    /// Selects a random color from a preset palette and applies it to registered Outline holders,
    /// specific Outlines, or Images.
    /// Overrides and disables ThemeColorBinder if both scripts are attached to the same GameObject.
    /// </summary>
    public class ColorRandomizer : MonoBehaviour
    {
        [Header("Target Slots (Same as Theme Color Binder)")]
        [Tooltip("GameObjects whose Outline components (including children) should be tinted with a random preset color.")]
        [SerializeField] private GameObject[] outlineHolders;

        [Tooltip("Specific Outline components to tint with a random preset color.")]
        [SerializeField] private Outline[] specificOutlines;

        [Tooltip("Image components to tint with a random preset color.")]
        [SerializeField] private Image[] images;

        [Header("Color Preset Palette")]
        [Tooltip("List of preset colors to pick randomly from.")]
        [SerializeField] private Color[] colorPresets = new Color[]
        {
            new Color(0.988f, 0.455f, 0.000f), // Orange
            new Color(0.584f, 0.337f, 1.000f), // Purple
            new Color(0.231f, 0.719f, 1.000f), // Blue
            new Color(0.322f, 0.839f, 0.463f), // Green
            new Color(0.868f, 0.143f, 0.636f), // Pink
            new Color(0.067f, 0.804f, 0.737f), // Teal
            new Color(0.950f, 0.300f, 0.250f), // Coral Red
            new Color(0.960f, 0.720f, 0.100f)  // Golden Yellow
        };

        [Header("Settings")]
        [Tooltip("If true, picks a new random color every time this GameObject is enabled.")]
        [SerializeField] private bool randomizeOnEnable = true;

        [Tooltip("If true, disables any ThemeColorBinder attached to this GameObject so it does not overwrite the random color.")]
        [SerializeField] private bool overrideThemeBinder = true;

        private void Awake()
        {
            CheckAndOverrideThemeBinder();
        }

        private void OnEnable()
        {
            CheckAndOverrideThemeBinder();
            if (randomizeOnEnable)
            {
                ApplyRandomColor();
            }
        }

        private void Start()
        {
            CheckAndOverrideThemeBinder();
            ApplyRandomColor();
        }

        /// <summary>
        /// Disables ThemeColorBinder on the same GameObject if present.
        /// </summary>
        public void CheckAndOverrideThemeBinder()
        {
            if (!overrideThemeBinder) return;

            var binder = GetComponent<ThemeColorBinder>();
            if (binder != null && binder.enabled)
            {
                binder.enabled = false;
                Debug.Log($"[ColorRandomizer] Disabled ThemeColorBinder on '{gameObject.name}' to override theme color with random preset color.");
            }
        }

        /// <summary>
        /// Selects a random color from colorPresets and applies it to outlineHolders, specificOutlines, and images.
        /// </summary>
        public void ApplyRandomColor()
        {
            if (colorPresets == null || colorPresets.Length == 0) return;

            Color chosenColor = colorPresets[Random.Range(0, colorPresets.Length)];
            ApplyColor(chosenColor);
        }

        /// <summary>
        /// Applies a specific color to all assigned target slots.
        /// </summary>
        public void ApplyColor(Color targetColor)
        {
            // 1. Tint outlines inside registered GameObjects / children
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
                                outline.effectColor = targetColor;
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
                        outline.effectColor = targetColor;
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
                        img.color = targetColor;
                    }
                }
            }
        }
    }
}
