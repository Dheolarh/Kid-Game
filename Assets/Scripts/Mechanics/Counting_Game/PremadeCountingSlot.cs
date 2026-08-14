using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using KidGame.Audio;
using KidGame.Mechanics.NumberRecall;

namespace KidGame.Mechanics.Counting
{
    [System.Serializable]
    public class PremadeCountingBoxEntry
    {
        [Tooltip("Name or description of this box.")]
        public string boxName = "Box";

        [Tooltip("The Box GameObject in scene / prefab.")]
        public GameObject boxObject;

        [Tooltip("Is this an Answer Box (missing slot to drop answer card into) or a static Box?")]
        public bool isAnswerBox = true;

        [Tooltip("The expected letter, word, or number answer (e.g. 'A', 'B', '5', '10').")]
        public string value = "1";

        [Tooltip("If this is an Answer Box, should it display a faint hint?")]
        public bool showHint = true;
    }

    [System.Serializable]
    public class PremadeCountingRow
    {
        [Tooltip("Name of this row e.g. Row 1")]
        public string rowName = "Row 1";

        [Tooltip("The Row GameObject container (optional).")]
        public GameObject rowObject;

        [Tooltip("Optional explicit row number sound value to play on completion (e.g. 1 for ONE, 2 for TWO). If 0, uses row index + 1.")]
        public int rowNumberValue = 0;

        [Tooltip("List of boxes inside this row.")]
        public List<PremadeCountingBoxEntry> boxes = new List<PremadeCountingBoxEntry>();
    }

    /// <summary>
    /// Attached to a premade slot / container GameObject for Counting Game level layouts.
    /// Manages manually configured rows and boxes with expected answers.
    /// Automatically plays the number sound when a row's last answer box is solved.
    /// Supports automatic hierarchy scanning if unassigned.
    /// </summary>
    public class PremadeCountingSlot : MonoBehaviour
    {
        [Header("Premade Counting Layout Configuration")]
        [Tooltip("Configure rows and their boxes directly in Inspector.")]
        public List<PremadeCountingRow> rows = new List<PremadeCountingRow>();

        private class RowRuntimeState
        {
            public int rowIndex;
            public int numberValue;
            public int totalAnswers;
            public int solvedAnswers;
        }

        private int _totalAnswerBoxesCount = 0;
        private int _solvedAnswerBoxesCount = 0;
        private Action _onCompleted;
        private readonly List<RowRuntimeState> _rowStates = new List<RowRuntimeState>();

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
        /// Configures box values, answer states, hints, and toggles active status for extra boxes/rows.
        /// </summary>
        public void ApplySlotData(KidGame.Interface.PremadeSlotData slotData)
        {
            if (slotData == null || slotData.rows == null || slotData.rows.Count == 0) return;

            if (rows == null || rows.Count == 0)
            {
                AutoScanChildSlots();
            }

            for (int r = 0; r < rows.Count; r++)
            {
                var physicalRow = rows[r];
                if (physicalRow == null) continue;

                if (r < slotData.rows.Count)
                {
                    var dataRow = slotData.rows[r];
                    if (physicalRow.rowObject != null)
                    {
                        physicalRow.rowObject.SetActive(true);
                    }

                    for (int b = 0; b < physicalRow.boxes.Count; b++)
                    {
                        var physicalBox = physicalRow.boxes[b];
                        if (physicalBox == null || physicalBox.boxObject == null) continue;

                        if (b < dataRow.boxes.Count)
                        {
                            var dataBox = dataRow.boxes[b];
                            physicalBox.boxObject.SetActive(true);
                            physicalBox.value = dataBox.value;
                            physicalBox.isAnswerBox = dataBox.isAnswerBox;
                            physicalBox.showHint = dataBox.showHint;
                        }
                        else
                        {
                            physicalBox.boxObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    if (physicalRow.rowObject != null)
                    {
                        physicalRow.rowObject.SetActive(false);
                    }
                }
            }
        }

        public (List<int> numberAnswers, List<string> stringAnswers) Setup(Action onCompleted, bool isLearningMode = true)
        {
            _onCompleted = onCompleted;
            _totalAnswerBoxesCount = 0;
            _solvedAnswerBoxesCount = 0;
            _rowStates.Clear();

            List<int> numberAnswers = new List<int>();
            List<string> stringAnswers = new List<string>();
            int globalBoxIdx = 0;

            if (rows == null || rows.Count == 0)
            {
                AutoScanChildSlots();
            }

            Dictionary<GameObject, RowRuntimeState> rowStateMap = new Dictionary<GameObject, RowRuntimeState>();

            RowRuntimeState GetOrCreateState(GameObject boxObj)
            {
                Transform parentT = GetRowParentTransform(boxObj.transform);
                GameObject parentGo = parentT.gameObject;

                if (!rowStateMap.TryGetValue(parentGo, out var state))
                {
                    int rowNum = ExtractRowNumber(parentGo, rowStateMap.Count + 1);
                    state = new RowRuntimeState
                    {
                        rowIndex = rowStateMap.Count,
                        numberValue = rowNum,
                        totalAnswers = 0,
                        solvedAnswers = 0
                    };
                    rowStateMap[parentGo] = state;
                    _rowStates.Add(state);
                }
                return state;
            }

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row == null || row.boxes == null) continue;

                foreach (var entry in row.boxes)
                {
                    if (entry == null || entry.boxObject == null) continue;

                    GameObject boxGo = entry.boxObject;
                    RowRuntimeState rowState = GetOrCreateState(boxGo);
                    if (row.rowNumberValue > 0)
                    {
                        rowState.numberValue = row.rowNumberValue;
                    }

                    Color col = Palette[globalBoxIdx % Palette.Length];
                    globalBoxIdx++;

                    string valStr = entry.value ?? "";
                    bool isNumber = int.TryParse(valStr, out int parsedNum);

                    if (entry.isAnswerBox)
                    {
                        _totalAnswerBoxesCount++;
                        rowState.totalAnswers++;

                        if (isNumber)
                        {
                            numberAnswers.Add(parsedNum);
                        }
                        else
                        {
                            stringAnswers.Add(valStr);
                        }

                        bool boxShowHint = entry.showHint && isLearningMode;
                        Action onBoxSolved = () => OnBoxInRowSolved(rowState);

                        var letterSlot = boxGo.GetComponent<LetterAnswerSlot>();
                        if (letterSlot == null) letterSlot = boxGo.GetComponentInChildren<LetterAnswerSlot>(true);

                        var recallSlot = boxGo.GetComponent<RecallAnswerSlot>();
                        if (recallSlot == null) recallSlot = boxGo.GetComponentInChildren<RecallAnswerSlot>(true);

                        if (letterSlot != null)
                        {
                            letterSlot.SetupSlot(valStr, true, boxShowHint, onBoxSolved);
                        }
                        else if (recallSlot != null && isNumber)
                        {
                            recallSlot.SetupSlot(parsedNum, true, boxShowHint, onBoxSolved);
                        }
                        else
                        {
                            var dropZone = boxGo.GetComponent<AnswerDropZone>();
                            if (dropZone == null) dropZone = boxGo.AddComponent<AnswerDropZone>();

                            int dropZoneCode = isNumber ? parsedNum : (!string.IsNullOrEmpty(valStr) ? (int)valStr[0] : 0);
                            dropZone.Setup(dropZoneCode, onBoxSolved, customHint: valStr, showHint: boxShowHint);
                        }
                    }
                    else
                    {
                        var letterSlot = boxGo.GetComponent<LetterAnswerSlot>();
                        if (letterSlot == null) letterSlot = boxGo.GetComponentInChildren<LetterAnswerSlot>(true);

                        var recallSlot = boxGo.GetComponent<RecallAnswerSlot>();
                        if (recallSlot == null) recallSlot = boxGo.GetComponentInChildren<RecallAnswerSlot>(true);

                        if (letterSlot != null)
                        {
                            letterSlot.SetupSlot(valStr, false, false, null);
                        }
                        else if (recallSlot != null && isNumber)
                        {
                            recallSlot.SetupSlot(parsedNum, false, false, null);
                        }
                        else
                        {
                            var textComp = boxGo.GetComponentInChildren<TMP_Text>(true);
                            if (textComp != null)
                            {
                                textComp.text = valStr;
                                textComp.color = Color.white;
                                textComp.gameObject.SetActive(true);
                            }

                            var img = boxGo.GetComponent<Image>();
                            if (img == null) img = boxGo.GetComponentInChildren<Image>(true);
                            if (img != null) img.color = col;
                        }
                    }
                }
            }

            return (numberAnswers, stringAnswers);
        }

        private void OnBoxInRowSolved(RowRuntimeState rowState)
        {
            _solvedAnswerBoxesCount++;

            if (rowState != null)
            {
                rowState.solvedAnswers++;
            }

            if (_solvedAnswerBoxesCount >= _totalAnswerBoxesCount)
            {
                _onCompleted?.Invoke();
            }
        }

        public void AutoScanChildSlots()
        {
            rows = new List<PremadeCountingRow>();

            var letterSlots = GetComponentsInChildren<LetterAnswerSlot>(true);
            var recallSlots = GetComponentsInChildren<RecallAnswerSlot>(true);
            var dropZones = GetComponentsInChildren<AnswerDropZone>(true);

            Dictionary<GameObject, PremadeCountingRow> rowMap = new Dictionary<GameObject, PremadeCountingRow>();
            HashSet<GameObject> processedObjects = new HashSet<GameObject>();

            PremadeCountingRow GetOrCreateRow(GameObject childObj)
            {
                Transform parentT = GetRowParentTransform(childObj.transform);
                GameObject parentGo = parentT.gameObject;

                if (!rowMap.TryGetValue(parentGo, out var row))
                {
                    int numVal = ExtractRowNumber(parentGo, rowMap.Count + 1);

                    row = new PremadeCountingRow
                    {
                        rowName = parentGo.name,
                        rowObject = parentGo,
                        rowNumberValue = numVal,
                        boxes = new List<PremadeCountingBoxEntry>()
                    };
                    rowMap[parentGo] = row;
                    rows.Add(row);
                }
                return row;
            }

            if (letterSlots != null)
            {
                foreach (var slot in letterSlots)
                {
                    if (slot == null || processedObjects.Contains(slot.gameObject)) continue;
                    processedObjects.Add(slot.gameObject);

                    var targetRow = GetOrCreateRow(slot.gameObject);
                    targetRow.boxes.Add(new PremadeCountingBoxEntry
                    {
                        boxName = slot.gameObject.name,
                        boxObject = slot.gameObject,
                        isAnswerBox = slot.IsAnswer,
                        value = slot.ExpectedLetter,
                        showHint = slot.ShowHint
                    });
                }
            }

            if (recallSlots != null)
            {
                foreach (var slot in recallSlots)
                {
                    if (slot == null || processedObjects.Contains(slot.gameObject)) continue;
                    processedObjects.Add(slot.gameObject);

                    var targetRow = GetOrCreateRow(slot.gameObject);
                    targetRow.boxes.Add(new PremadeCountingBoxEntry
                    {
                        boxName = slot.gameObject.name,
                        boxObject = slot.gameObject,
                        isAnswerBox = slot.IsAnswer,
                        value = slot.ExpectedAnswer.ToString(),
                        showHint = slot.ShowHint
                    });
                }
            }

            if (dropZones != null)
            {
                foreach (var dz in dropZones)
                {
                    if (dz == null || processedObjects.Contains(dz.gameObject)) continue;
                    processedObjects.Add(dz.gameObject);

                    var targetRow = GetOrCreateRow(dz.gameObject);
                    targetRow.boxes.Add(new PremadeCountingBoxEntry
                    {
                        boxName = dz.gameObject.name,
                        boxObject = dz.gameObject,
                        isAnswerBox = true,
                        value = "1",
                        showHint = true
                    });
                }
            }

            Debug.Log($"[PremadeCountingSlot] Auto-scanned {rows.Count} row(s) from hierarchy.");
        }

        private Transform GetRowParentTransform(Transform slotTransform)
        {
            Transform current = slotTransform;
            while (current != null && current.parent != null && current.parent != transform)
            {
                if (current.parent.name.Equals("Content", StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
                current = current.parent;
            }
            return (current != null && current != transform) ? current : slotTransform;
        }

        private int ExtractRowNumber(GameObject rowGo, int fallbackIndex)
        {
            if (rowGo == null) return fallbackIndex;
            string name = rowGo.name;

            if (int.TryParse(name, out int directParsed) && directParsed > 0)
            {
                return directParsed;
            }

            var match = Regex.Match(name, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int extracted) && extracted > 0)
            {
                return extracted;
            }

            return fallbackIndex;
        }
    }
}
