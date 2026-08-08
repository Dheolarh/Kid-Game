using UnityEngine;

namespace KidGame.Mechanics.NumberRecall
{
    /// <summary>
    /// Component attached directly to a Box GameObject inside a premade recall layout.
    /// Defines whether it is an Answer Box (missing slot) or static Number Box, and its expected value.
    /// </summary>
    public class PremadeRecallBox : MonoBehaviour
    {
        [Tooltip("Is this an Answer Box (missing slot for player to drop card into) or a static Number Box?")]
        public bool isAnswerBox = false;

        [Tooltip("The static number value (if Number Box) or expected correct answer (if Answer Box).")]
        public int numberValue = 1;

        [Tooltip("If this is an Answer Box, should it display a hint (faint placeholder text)?")]
        public bool showHint = true;
    }
}
