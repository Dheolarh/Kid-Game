using System.Collections.Generic;
using UnityEngine;

namespace KidGame.Interface
{
    [System.Serializable]
    public class PremadeBoxData
    {
        [Tooltip("The expected answer value (e.g. '1', '2', '15', 'A', 'B').")]
        public string value = "1";

        [Tooltip("Is this an Answer Box (missing slot to fill) or a static displayed box?")]
        public bool isAnswerBox = true;

        [Tooltip("If this is an Answer Box, should it display a faint hint?")]
        public bool showHint = true;
    }

    [System.Serializable]
    public class PremadeRowData
    {
        [Tooltip("Name of this row e.g. Row 1")]
        public string rowName = "Row 1";

        [Tooltip("List of boxes in this row (up to 10 max per row).")]
        public List<PremadeBoxData> boxes = new List<PremadeBoxData>();
    }

    /// <summary>
    /// ScriptableObject data container representing a premade slot layout (e.g. 1-20 sequence, 1-10 sequence, A-Z letter spelling).
    /// Used in place of creating individual slot prefabs for every level sequence.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPremadeSlotData", menuName = "Level Select/Premade Slot Data")]
    public class PremadeSlotData : ScriptableObject
    {
        [Tooltip("Name or description of this premade layout (e.g. '1 - 20 Sequence', 'Level 12 Layout').")]
        public string layoutName = "Premade Layout";

        [Tooltip("Optional custom base template prefab to spawn. If unassigned, uses the default template prefab.")]
        public GameObject templatePrefab;

        [Tooltip("If true, colorize the number text instead of the box background image on answer drop.")]
        public bool colorizeTextInsteadOfBox = false;

        [Tooltip("Configured rows (e.g. Row 1 and Row 2, up to 10 boxes per row).")]
        public List<PremadeRowData> rows = new List<PremadeRowData>();
    }
}
