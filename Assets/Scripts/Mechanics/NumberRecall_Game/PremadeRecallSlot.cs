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

        public int TotalAnswerBoxesCount => _totalAnswerBoxesCount;

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
        /// Applies runtime configuration data from a PremadeSlotData ScriptableObject.
        /// Rebuilds row/box structures dynamically from current hierarchy and slotData.
        /// </summary>
        public void ApplySlotData(KidGame.Interface.PremadeSlotData slotData)
        {
            if (slotData == null || slotData.rows == null || slotData.rows.Count == 0) return;

            // 1. Rescan physical hierarchy to get clean physical row containers
            AutoScanChildren();

            List<PremadeRecallRow> configuredRows = new List<PremadeRecallRow>();

            for (int r = 0; r < slotData.rows.Count; r++)
            {
                PremadeRecallRow physicalRow = (r < rows.Count) ? rows[r] : null;
                GameObject rowGo = null;

                if (physicalRow != null && physicalRow.rowObject != null)
                {
                    rowGo = physicalRow.rowObject;
                }
                else if (rows.Count > 0 && rows[0] != null && rows[0].rowObject != null)
                {
                    // Dynamically instantiate extra row container if slotData specifies more rows than template prefab
                    rowGo = Instantiate(rows[0].rowObject, transform);
                    rowGo.name = $"Row {r + 1}";
                }

                if (rowGo == null) continue;
                rowGo.SetActive(true);

                var dataRow = slotData.rows[r];
                PremadeRecallRow newRow = new PremadeRecallRow
                {
                    rowName = rowGo.name,
                    rowObject = rowGo,
                    boxes = new List<PremadeRecallBoxEntry>()
                };

                // Get physical boxes in this row (if any)
                var existingPhysicalBoxes = new List<PremadeRecallBoxEntry>();
                if (physicalRow != null && physicalRow.boxes != null)
                {
                    existingPhysicalBoxes.AddRange(physicalRow.boxes);
                }
                if (existingPhysicalBoxes.Count == 0 && rowGo != transform.gameObject)
                {
                    foreach (Transform child in rowGo.transform)
                    {
                        existingPhysicalBoxes.Add(CreateBoxEntry(child.gameObject));
                    }
                }

                for (int b = 0; b < dataRow.boxes.Count; b++)
                {
                    GameObject boxGo = null;
                    if (b < existingPhysicalBoxes.Count && existingPhysicalBoxes[b] != null)
                    {
                        boxGo = existingPhysicalBoxes[b].boxObject;
                    }
                    else if (existingPhysicalBoxes.Count > 0 && existingPhysicalBoxes[0] != null && existingPhysicalBoxes[0].boxObject != null)
                    {
                        // Dynamically instantiate extra box GameObject to support up to 20 boxes per row!
                        var templateBox = existingPhysicalBoxes[0].boxObject;
                        boxGo = Instantiate(templateBox, templateBox.transform.parent);
                        boxGo.name = $"answer ({b})";
                    }

                    if (boxGo == null) continue;
                    boxGo.SetActive(true);

                    var dataBox = dataRow.boxes[b];
                    int.TryParse(dataBox.value, out int parsedVal);

                    PremadeRecallBoxEntry newEntry = new PremadeRecallBoxEntry
                    {
                        boxName = boxGo.name,
                        boxObject = boxGo,
                        value = parsedVal,
                        isAnswerBox = dataBox.isAnswerBox,
                        showHint = dataBox.showHint
                    };
                    newRow.boxes.Add(newEntry);
                }

                // Disable any unused physical boxes in this row
                for (int b = dataRow.boxes.Count; b < existingPhysicalBoxes.Count; b++)
                {
                    if (existingPhysicalBoxes[b] != null && existingPhysicalBoxes[b].boxObject != null)
                    {
                        existingPhysicalBoxes[b].boxObject.SetActive(false);
                    }
                }

                configuredRows.Add(newRow);
            }

            // Disable any unused physical rows beyond slotData.rows.Count
            for (int r = slotData.rows.Count; r < rows.Count; r++)
            {
                if (rows[r] != null && rows[r].rowObject != null)
                {
                    rows[r].rowObject.SetActive(false);
                }
            }

            rows = configuredRows;
        }

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

                        int expectedVal = entry.value;
                        bool boxShowHint = entry.showHint && isLearningMode;

                        var recallSlot = boxGo.GetComponent<RecallAnswerSlot>();
                        if (recallSlot == null) recallSlot = boxGo.GetComponentInChildren<RecallAnswerSlot>(true);

                        if (recallSlot != null)
                        {
                            recallSlot.SetupSlot(expectedVal, true, boxShowHint, () =>
                            {
                                _solvedAnswerBoxesCount++;
                                if (_solvedAnswerBoxesCount >= _totalAnswerBoxesCount)
                                {
                                    _onCompleted?.Invoke();
                                }
                            });
                        }
                        else
                        {
                            // Standard drop zone setup if RecallAnswerSlot component is not present
                            var dropZone = boxGo.GetComponent<AnswerDropZone>();
                            if (dropZone == null) dropZone = boxGo.AddComponent<AnswerDropZone>();

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

            // Also check for any standalone RecallAnswerSlot components attached directly in hierarchy
            var standaloneRecallSlots = GetComponentsInChildren<RecallAnswerSlot>(true);
            foreach (var slot in standaloneRecallSlots)
            {
                if (slot == null) continue;

                // Check if already processed via rows
                bool alreadyProcessed = false;
                if (rows != null)
                {
                    foreach (var r in rows)
                    {
                        if (r == null || r.boxes == null) continue;
                        foreach (var b in r.boxes)
                        {
                            if (b != null && b.boxObject != null && (b.boxObject == slot.gameObject || slot.transform.IsChildOf(b.boxObject.transform)))
                            {
                                alreadyProcessed = true;
                                break;
                            }
                        }
                        if (alreadyProcessed) break;
                    }
                }

                if (!alreadyProcessed)
                {
                    if (slot.IsAnswer)
                    {
                        _totalAnswerBoxesCount++;
                        requiredAnswers.Add(slot.ExpectedAnswer);

                        bool boxShowHint = slot.ShowHint && isLearningMode;
                        slot.SetupSlot(slot.ExpectedAnswer, true, boxShowHint, () =>
                        {
                            _solvedAnswerBoxesCount++;
                            if (_solvedAnswerBoxesCount >= _totalAnswerBoxesCount)
                            {
                                _onCompleted?.Invoke();
                            }
                        });
                    }
                    else
                    {
                        slot.InitSlotState();
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

            var recallSlot = go.GetComponent<RecallAnswerSlot>();
            var premadeBox = go.GetComponent<PremadeRecallBox>();

            if (recallSlot != null)
            {
                entry.isAnswerBox = recallSlot.IsAnswer;
                entry.value = recallSlot.ExpectedAnswer;
                entry.showHint = recallSlot.ShowHint;
            }
            else if (premadeBox != null)
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

        /// <summary>
        /// Returns true if all answer boxes in this slot have been correctly solved.
        /// </summary>
        public bool IsCompleted()
        {
            if (_totalAnswerBoxesCount == 0) return true;
            return _solvedAnswerBoxesCount >= _totalAnswerBoxesCount;
        }

        public bool IsRoundCompleted() => IsCompleted();
    }
}
