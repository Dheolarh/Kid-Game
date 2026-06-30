using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KidGame.Interface
{
    public class LevelButtonUI : MonoBehaviour
    {
        [Tooltip("The text component displaying the level number.")]
        public TMP_Text levelNumberText;

        [Tooltip("The main background image of the button to be colored.")]
        public Image buttonBackground;

        [Tooltip("The 3 star images indicating level performance (e.g. index 0 = star 1, index 1 = star 2, index 2 = star 3).")]
        public Image[] stars = new Image[3];

        [Tooltip("The unlocked/active color of the stars (usually yellow).")]
        public Color activeStarColor = Color.yellow;

        [Tooltip("The locked/unearned color of the stars (usually white, gray, or transparent/hidden).")]
        public Color inactiveStarColor = new Color(1f, 1f, 1f, 0.2f);
    }
}
