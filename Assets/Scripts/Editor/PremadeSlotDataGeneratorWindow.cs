using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using KidGame.Interface;

namespace KidGame.Editor
{
    public class PremadeSlotDataGeneratorWindow : EditorWindow
    {
        private PremadeSlotData _slotDataToImport;
        private string _assetName = "Premade_1_to_20_Data";
        private string _saveFolderPath = "Assets/Levels/PremadeSlotData";
        private GameObject _templatePrefab;
        private int _rowCount = 2;
        private int _maxBoxesPerRow = 10;

        // Quick fill generator settings
        private int _quickFillStartVal = 1;
        private int _quickFillStep = 1;
        private QuickFillMode _quickFillMode = QuickFillMode.Numbers;
        private AnswerPattern _answerPattern = AnswerPattern.Alternate;
        private bool _quickFillShowHint = true;

        // Working data for the editor
        private List<PremadeRowData> _workingRows = new List<PremadeRowData>();

        private Vector2 _scrollPos;

        private enum QuickFillMode
        {
            Numbers,
            Alphabet
        }

        private enum AnswerPattern
        {
            AllAnswers,
            Alternate,
            EveryThird,
            EvenNumbersOnly,
            OddNumbersOnly
        }

        [MenuItem("Tools/Premade Slot Data Generator")]
        public static void OpenWindow()
        {
            PremadeSlotDataGeneratorWindow window = GetWindow<PremadeSlotDataGeneratorWindow>("Slot Data Generator");
            window.minSize = new Vector2(480, 600);
            window.Show();
        }

        private void OnEnable()
        {
            if (_workingRows == null || _workingRows.Count == 0)
            {
                InitializeDefaultWorkingData();
            }
        }

        private void InitializeDefaultWorkingData()
        {
            _workingRows = new List<PremadeRowData>();
            for (int r = 0; r < _rowCount; r++)
            {
                PremadeRowData rowData = new PremadeRowData
                {
                    rowName = $"Row {r + 1}",
                    boxes = new List<PremadeBoxData>()
                };

                for (int b = 0; b < _maxBoxesPerRow; b++)
                {
                    int val = (r * _maxBoxesPerRow) + b + 1;
                    rowData.boxes.Add(new PremadeBoxData
                    {
                        value = val.ToString(),
                        isAnswerBox = (val % 2 == 0),
                        showHint = true
                    });
                }
                _workingRows.Add(rowData);
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            GUILayout.Label("🛠️ Premade Slot Data Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Create ScriptableObject sequence layout data for Premade levels without creating separate prefabs for every level sequence.", MessageType.Info);

            EditorGUILayout.Space(5);

            // ── Import Existing Slot Data ───────────────────────────────────────
            EditorGUILayout.BeginHorizontal();
            _slotDataToImport = (PremadeSlotData)EditorGUILayout.ObjectField("Import Existing Data", _slotDataToImport, typeof(PremadeSlotData), false);
            if (GUILayout.Button("Import", GUILayout.Width(80)))
            {
                ImportPremadeSlotData(_slotDataToImport);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // ── Section 1: Output & Template Configuration ──────────────────────
            EditorGUILayout.Space(10);
            GUILayout.Label("1. Asset Output & Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _assetName = EditorGUILayout.TextField("Asset Name", _assetName);
            _saveFolderPath = EditorGUILayout.TextField("Save Folder Path", _saveFolderPath);
            _templatePrefab = (GameObject)EditorGUILayout.ObjectField("Template Prefab (Optional)", _templatePrefab, typeof(GameObject), false);

            EditorGUILayout.Space(5);
            EditorGUI.BeginChangeCheck();
            _rowCount = EditorGUILayout.IntSlider("Number of Rows", _rowCount, 1, 4);
            _maxBoxesPerRow = EditorGUILayout.IntSlider("Max Boxes Per Row", _maxBoxesPerRow, 1, 10);
            if (EditorGUI.EndChangeCheck())
            {
                AdjustWorkingDataDimensions();
            }

            EditorGUILayout.EndVertical();

            // ── Section 2: Quick Fill Sequence Generator ────────────────────────
            EditorGUILayout.Space(10);
            GUILayout.Label("2. ⚡ Quick Sequence Generator", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _quickFillMode = (QuickFillMode)EditorGUILayout.EnumPopup("Sequence Fill Type", _quickFillMode);
            if (_quickFillMode == QuickFillMode.Numbers)
            {
                _quickFillStartVal = EditorGUILayout.IntField("Start Number Value", _quickFillStartVal);
                _quickFillStep = EditorGUILayout.IntField("Step Value", _quickFillStep);
            }
            _answerPattern = (AnswerPattern)EditorGUILayout.EnumPopup("Answer Pattern", _answerPattern);
            _quickFillShowHint = EditorGUILayout.Toggle("Show Hints on Answer Boxes", _quickFillShowHint);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("⚡ Auto-Fill All Rows & Boxes", GUILayout.Height(30)))
            {
                ExecuteQuickFill();
            }
            EditorGUILayout.EndVertical();

            // ── Section 3: Interactive Row & Box Editor ────────────────────────
            EditorGUILayout.Space(10);
            GUILayout.Label("3. Configure Rows & Boxes", EditorStyles.boldLabel);

            for (int r = 0; r < _workingRows.Count; r++)
            {
                PremadeRowData row = _workingRows[r];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Row {r + 1} ({row.boxes.Count} boxes)", EditorStyles.boldLabel);
                if (GUILayout.Button("+ Add Box", GUILayout.Width(80)) && row.boxes.Count < 10)
                {
                    row.boxes.Add(new PremadeBoxData { value = (row.boxes.Count + 1).ToString(), isAnswerBox = true, showHint = true });
                }
                if (GUILayout.Button("- Remove", GUILayout.Width(80)) && row.boxes.Count > 0)
                {
                    row.boxes.RemoveAt(row.boxes.Count - 1);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                for (int b = 0; b < row.boxes.Count; b++)
                {
                    PremadeBoxData box = row.boxes[b];
                    EditorGUILayout.BeginHorizontal();
                    
                    GUILayout.Label($"#{b + 1}", GUILayout.Width(30));
                    box.value = EditorGUILayout.TextField(box.value, GUILayout.Width(70));
                    box.isAnswerBox = EditorGUILayout.ToggleLeft("Is Answer", box.isAnswerBox, GUILayout.Width(85));
                    if (box.isAnswerBox)
                    {
                        box.showHint = EditorGUILayout.ToggleLeft("Hint", box.showHint, GUILayout.Width(55));
                    }
                    else
                    {
                        GUILayout.Space(55);
                    }

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            EditorGUILayout.EndScrollView();

            // ── Section 4: Generate Action ─────────────────────────────────────
            EditorGUILayout.Space(10);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.3f);
            if (GUILayout.Button("💾 Create & Save PremadeSlotData Asset", GUILayout.Height(40)))
            {
                CreateAndSaveAsset();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Space(10);
        }

        private void ImportPremadeSlotData(PremadeSlotData source)
        {
            if (source == null)
            {
                EditorUtility.DisplayDialog("Import Error", "Please assign a valid PremadeSlotData asset to import.", "OK");
                return;
            }

            _assetName = source.name;
            _templatePrefab = source.templatePrefab;

            if (source.rows != null && source.rows.Count > 0)
            {
                _rowCount = source.rows.Count;
                int maxBoxes = 1;
                foreach (var r in source.rows)
                {
                    if (r != null && r.boxes != null && r.boxes.Count > maxBoxes)
                    {
                        maxBoxes = r.boxes.Count;
                    }
                }
                _maxBoxesPerRow = maxBoxes;

                _workingRows = new List<PremadeRowData>();
                for (int r = 0; r < source.rows.Count; r++)
                {
                    var srcRow = source.rows[r];
                    PremadeRowData newRow = new PremadeRowData
                    {
                        rowName = !string.IsNullOrEmpty(srcRow.rowName) ? srcRow.rowName : $"Row {r + 1}",
                        boxes = new List<PremadeBoxData>()
                    };

                    if (srcRow.boxes != null)
                    {
                        foreach (var srcBox in srcRow.boxes)
                        {
                            newRow.boxes.Add(new PremadeBoxData
                            {
                                value = srcBox.value,
                                isAnswerBox = srcBox.isAnswerBox,
                                showHint = srcBox.showHint
                            });
                        }
                    }
                    _workingRows.Add(newRow);
                }
            }

            Repaint();
            Debug.Log($"[PremadeSlotDataGenerator] Imported '{source.name}' successfully.");
        }

        private void AdjustWorkingDataDimensions()
        {
            while (_workingRows.Count < _rowCount)
            {
                _workingRows.Add(new PremadeRowData { rowName = $"Row {_workingRows.Count + 1}", boxes = new List<PremadeBoxData>() });
            }
            while (_workingRows.Count > _rowCount)
            {
                _workingRows.RemoveAt(_workingRows.Count - 1);
            }

            for (int r = 0; r < _workingRows.Count; r++)
            {
                var row = _workingRows[r];
                while (row.boxes.Count < _maxBoxesPerRow)
                {
                    int val = (r * _maxBoxesPerRow) + row.boxes.Count + 1;
                    row.boxes.Add(new PremadeBoxData { value = val.ToString(), isAnswerBox = true, showHint = true });
                }
                while (row.boxes.Count > _maxBoxesPerRow)
                {
                    row.boxes.RemoveAt(row.boxes.Count - 1);
                }
            }
        }

        private void ExecuteQuickFill()
        {
            int currentNum = _quickFillStartVal;
            int globalIndex = 0;

            for (int r = 0; r < _workingRows.Count; r++)
            {
                var row = _workingRows[r];
                for (int b = 0; b < row.boxes.Count; b++)
                {
                    PremadeBoxData box = row.boxes[b];
                    
                    if (_quickFillMode == QuickFillMode.Numbers)
                    {
                        box.value = currentNum.ToString();
                        currentNum += _quickFillStep;
                    }
                    else
                    {
                        char c = (char)('A' + (globalIndex % 26));
                        box.value = c.ToString();
                    }

                    box.isAnswerBox = DetermineIsAnswer(globalIndex, currentNum);
                    box.showHint = _quickFillShowHint;
                    globalIndex++;
                }
            }
        }

        private bool DetermineIsAnswer(int index, int numValue)
        {
            switch (_answerPattern)
            {
                case AnswerPattern.AllAnswers: return true;
                case AnswerPattern.Alternate: return (index % 2 == 1);
                case AnswerPattern.EveryThird: return (index % 3 == 2);
                case AnswerPattern.EvenNumbersOnly: return (numValue % 2 == 0);
                case AnswerPattern.OddNumbersOnly: return (numValue % 2 != 0);
                default: return true;
            }
        }

        private void CreateAndSaveAsset()
        {
            if (string.IsNullOrEmpty(_assetName))
            {
                EditorUtility.DisplayDialog("Error", "Please enter a valid Asset Name.", "OK");
                return;
            }

            string folder = string.IsNullOrEmpty(_saveFolderPath) ? "Assets/Levels/PremadeSlotData" : _saveFolderPath;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string fullPath = Path.Combine(folder, _assetName.EndsWith(".asset") ? _assetName : _assetName + ".asset").Replace('\\', '/');

            PremadeSlotData asset = ScriptableObject.CreateInstance<PremadeSlotData>();
            asset.layoutName = _assetName;
            asset.templatePrefab = _templatePrefab;
            asset.rows = new List<PremadeRowData>();

            foreach (var r in _workingRows)
            {
                PremadeRowData newRow = new PremadeRowData
                {
                    rowName = r.rowName,
                    boxes = new List<PremadeBoxData>()
                };
                foreach (var b in r.boxes)
                {
                    newRow.boxes.Add(new PremadeBoxData
                    {
                        value = b.value,
                        isAnswerBox = b.isAnswerBox,
                        showHint = b.showHint
                    });
                }
                asset.rows.Add(newRow);
            }

            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"[PremadeSlotDataGenerator] Saved PremadeSlotData asset at '{fullPath}'.");
            EditorUtility.DisplayDialog("Success", "Created PremadeSlotData asset at:\n" + fullPath, "OK");
        }
    }
}
