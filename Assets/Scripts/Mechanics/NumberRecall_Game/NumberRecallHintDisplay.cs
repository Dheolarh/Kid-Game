using UnityEngine;
using TMPro;

namespace KidGame.Mechanics.NumberRecall
{
    /// <summary>
    /// Component attached to Number Recall answer drop zones to display ghost/hint text
    /// of the correct target value when Learning Mode is active.
    /// </summary>
    public class NumberRecallHintDisplay : MonoBehaviour
    {
        [Header("Inspector References")]
        [Tooltip("The TextMeshPro (TMP_Text) component that displays the hint text.")]
        [SerializeField] private TMP_Text hintText;

        [Header("Hint Styling")]
        [Tooltip("Color of the hint text in Learning Mode (default: semi-transparent ghost text).")]
        [SerializeField] private Color hintColor = new Color(0.25f, 0.25f, 0.25f, 0.45f);

        public TMP_Text HintText => hintText;

        private void Awake()
        {
            if (hintText == null)
            {
                hintText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        /// <summary>
        /// Configures the hint display for a numeric target value.
        /// </summary>
        /// <param name="value">The target number to display.</param>
        /// <param name="showHint">If true, activates the text mesh and displays the value (Learning Mode). If false, hides it (Normal Mode).</param>
        public void SetupHint(int value, bool showHint)
        {
            SetupHint(value.ToString(), showHint);
        }

        /// <summary>
        /// Configures the hint display with a custom text string.
        /// </summary>
        /// <param name="text">The string to display as hint.</param>
        /// <param name="showHint">If true, activates the text mesh and displays text (Learning Mode). If false, hides it (Normal Mode).</param>
        public void SetupHint(string text, bool showHint)
        {
            if (hintText == null)
            {
                hintText = GetComponentInChildren<TMP_Text>(true);
            }

            if (hintText != null)
            {
                hintText.gameObject.SetActive(showHint);
                if (showHint)
                {
                    hintText.text = text;
                    hintText.color = hintColor;
                }
            }
        }

        /// <summary>
        /// Explicitly toggles visibility of the hint text.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (hintText != null)
            {
                hintText.gameObject.SetActive(visible);
            }
        }
    }
}
