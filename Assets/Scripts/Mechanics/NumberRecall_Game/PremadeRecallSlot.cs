using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KidGame.Mechanics.Counting;

namespace KidGame.Mechanics.NumberRecall
{
    [System.Serializable]
    public class PremadeRecallBoxEntry
    {
        [Tooltip("Name or description of this box.")]
        public string boxName = "Box";

        [Tooltip("The Box GameObject in scene / prefab.")]
        public GameObject boxObject;

        [Tooltip("Is this an Answer Box (missing slot to drop answer card into) or a static Number Box?")]
        public bool isAnswerBox = false;

        [Tooltip("The static number value (if Number Box) or expected correct answer (if Answer Box).")]
        public int value = 1;

        [Tooltip("If this is an Answer Box, should it display a hint (faint placeholder text)?")]
        public bool showHint = true;
    }

    [System.Serializable]
    public class PremadeRecallRow
    {
        [Tooltip("Name of this row e.g. Row 1")]
        public string rowName = "Row 1";

        [Tooltip("The Row GameObject container (optional).")]
        public GameObject rowObject;

        [Tooltip("List of number boxes and answer boxes inside this row.")]
        public List<PremadeRecallBoxEntry> boxes = new List<PremadeRecallBoxEntry>();
    }

    /// <summary>
    /// Attached to a slot / row level container GameObject that will be spawned into the game content parent.
    /// Manages manually configured rows and boxes (Number boxes vs Answer boxes with expected answers).
    /// </summary>
    public class PremadeRecallSlot : MonoBehaviour
    {
        [Header("Premade Layout Configuration")]
        [Tooltip("Configure rows and their boxes directly in Inspector.")]
        public List<PremadeRecallRow> rows = new List<PremadeRecallRow>();

        private int _totalAnswerBoxesCount = 0;
        private int _solvedAnswerBoxesCount = 0;
        private System.Action _onCompleted;

        private static readonly Color[] Palette =
        {
            new Color(0.91f, 0.30f, 0.24f),   // red
            new Color(0.20f, 0.60f, 0.86f),   // blue
            new Color(0.95f, 0.61f, 0.07f),   // orange
            new Color(0.15f, 0.68f, 0.38f),   // green
            new Color(0.61f, 0.35f, 0.71f),   // purple
            new Color(0.10f, 0.74f, 0.61f),   // teal
        };

        /// <summary>
        /// Initializes the premade recall slot, configures boxes, and returns all required answer values for the answer tray.
        /// </summary>
        public List<int> Setup(System.Action onCompleted, bool isLearningMode = true)
        {
            _onCompleted = onCompleted;
            _totalAnswerBoxesCount = 0;
            _solvedAnswerBoxesCount = 0;

            List<int> requiredAnswers = new List<int>();
            int globalBoxIdx = 0;

            foreach (var row in rows)
            {
                if (row == null || row.boxes == null) continue;

                foreach (var entry in row.boxes)
                {
                    if (entry == null || entry.boxObject == null) continue;

                    GameObject boxGo = entry.boxObject;
                    Color col = Palette[globalBoxIdx % Palette.Length];
                    globalBoxIdx++;

                    if (entry.isAnswerBox)
                    {
                        _totalAnswerBoxesCount++;
                        requiredAnswers.Add(entry.value);

                        // Ensure AnswerDropZone component exists on answer box
                        var dropZone = boxGo.GetComponent<AnswerDropZone>();
                        if (dropZone == null) dropZone = boxGo.AddComponent<AnswerDropZone>();

                        int expectedVal = entry.value;
                        bool boxShowHint = entry.showHint && isLearningMode;
                        dropZone.Setup(expectedVal, () =>
                        {
                            _solvedAnswerBoxesCount++;
                            if (_solvedAnswerBoxesCount >= _totalAnswerBoxesCount)
                            {
                                _onCompleted?.Invoke();
                            }
                        }, showHint: boxShowHint);

                        if (!boxShowHint)
                        {
                            var textComponents = boxGo.GetComponentsInChildren<TMP_Text>(true);
                            foreach (var txt in textComponents)
                            {
                                txt.gameObject.SetActive(false);
                            }
                        }
                    }
                    else
                    {
                        // Number Box (static revealed number)
                        var label = boxGo.GetComponentInChildren<TMP_Text>();
                        if (label != null)
                        {
                            label.text = entry.value.ToString();
                        }

                        var bgImage = boxGo.GetComponent<Image>();
                        if (bgImage != null)
                        {
                            bgImage.color = col;
                        }

                        // Remove AnswerCard drag component if present on a static number box
                        var card = boxGo.GetComponent<AnswerCard>();
                        if (card != null)
                        {
                            Destroy(card);
                        }
                    }
                }
            }

            return requiredAnswers;
        }

        /// <summary>
        /// Auto-detects child rows and boxes from hierarchy.
        /// </summary>
        [ContextMenu("Auto-Scan Children Rows & Boxes")]
        public void AutoScanChildren()
        {
            rows.Clear();

            foreach (Transform child in transform)
            {
                PremadeRecallRow newRow = new PremadeRecallRow();
                newRow.rowName = child.name;
                newRow.rowObject = child.gameObject;

                if (child.childCount > 0)
                {
                    foreach (Transform boxChild in child)
                    {
                        PremadeRecallBoxEntry entry = CreateBoxEntry(boxChild.gameObject);
                        newRow.boxes.Add(entry);
                    }
                }
                else
                {
                    PremadeRecallBoxEntry entry = CreateBoxEntry(child.gameObject);
                    newRow.boxes.Add(entry);
                }

                rows.Add(newRow);
            }

            Debug.Log($"[PremadeRecallSlot] Auto-scanned {rows.Count} row(s) from hierarchy.");
        }

        private PremadeRecallBoxEntry CreateBoxEntry(GameObject go)
        {
            PremadeRecallBoxEntry entry = new PremadeRecallBoxEntry();
            entry.boxObject = go;
            entry.boxName = go.name;

            // Check if object has PremadeRecallBox helper
            var premadeBox = go.GetComponent<PremadeRecallBox>();
            if (premadeBox != null)
            {
                entry.isAnswerBox = premadeBox.isAnswerBox;
                entry.value = premadeBox.numberValue;
                entry.showHint = premadeBox.showHint;
            }
            else
            {
                string lowerName = go.name.ToLower();
                if (lowerName.Contains("answer") || lowerName.Contains("drop") || lowerName.Contains("empty") || lowerName.Contains("target"))
                {
                    entry.isAnswerBox = true;
                }

                var match = System.Text.RegularExpressions.Regex.Match(go.name, @"\d+");
                if (match.Success)
                {
                    int.TryParse(match.Value, out entry.value);
                }
            }

            return entry;
        }
    }
}
