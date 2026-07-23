using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KidGame.Interface;
using KidGame.Mechanics.Matching;

namespace KidGame.Editor
{
    public class LevelCreatorWindow : EditorWindow
    {
        private int _levelNumber = 1;
        private string _levelName = "New Level";
        private string _levelSubtitle = "";
        private string _levelEndTip = "";
        private bool _isUnlockedByDefault = false;
        private string _themePresetName = "";
        private Color _levelThemeColor = Color.white;
        private Sprite _levelBackgroundSprite;
        private List<PageData> _pages = new List<PageData>();
        private LevelData _levelDataToImport;

        // Editor Scroll View
        private Vector2 _scrollPos;

        // DB Status
        private LevelDatabase _cachedDatabase;
        private ThemeDatabase _cachedThemeDatabase;
        private string _statusText = "Ready";
        private MessageType _statusType = MessageType.Info;

        [MenuItem("Tools/Level Creator")]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelCreatorWindow>("Level Creator");
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable()
        {
            FindOrCreateDatabase();
            FindOrCreateThemeDatabase();
            CheckLevelStatus();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Level Creator & Lesson Manager", new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            GUILayout.Space(10);

            // Database status banner
            if (_cachedDatabase != null)
            {
                EditorGUILayout.HelpBox($"Level Database Loaded: '{_cachedDatabase.name}' (Contains {_cachedDatabase.allLevels.Count} levels)", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("Warning: LevelDatabase asset not found! Generate will create one in 'Assets/Levels'.", MessageType.Warning);
            }

            // Level detection status
            EditorGUILayout.HelpBox(_statusText, _statusType);

            GUILayout.Space(10);

            // Import section
            GUILayout.Label("Import Level Data", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal();
            _levelDataToImport = (LevelData)EditorGUILayout.ObjectField("Import Template", _levelDataToImport, typeof(LevelData), false);
            if (GUILayout.Button("Import", GUILayout.Width(80)))
            {
                ImportLevelDetails(_levelDataToImport);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);

            // Scrollable view starts
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            // 1. Level Meta Data
            GUILayout.Label("Level Config", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _levelNumber = EditorGUILayout.IntField("Level Number", _levelNumber);
            if (EditorGUI.EndChangeCheck())
            {
                if (_levelNumber < 1) _levelNumber = 1;
                CheckLevelStatus();
            }

            _levelName = EditorGUILayout.TextField("Level Name", _levelName);
            _levelSubtitle = EditorGUILayout.TextField("Level Subtitle", _levelSubtitle);
            _levelEndTip = EditorGUILayout.TextField("Level End Tip", _levelEndTip);
            _isUnlockedByDefault = EditorGUILayout.Toggle("Unlocked by Default", _isUnlockedByDefault);

            // Theme Preset Selection
            if (_cachedThemeDatabase != null && _cachedThemeDatabase.presets.Count > 0)
            {
                List<string> themeOptions = new List<string> { "None (Custom)" };
                int selectedThemeIndex = 0;
                for (int t = 0; t < _cachedThemeDatabase.presets.Count; t++)
                {
                    string pName = _cachedThemeDatabase.presets[t].themeName;
                    themeOptions.Add(pName);
                    if (pName == _themePresetName)
                    {
                        selectedThemeIndex = t + 1;
                    }
                }

                int newThemeIndex = EditorGUILayout.Popup("Theme Preset", selectedThemeIndex, themeOptions.ToArray());
                if (newThemeIndex != selectedThemeIndex)
                {
                    if (newThemeIndex == 0)
                    {
                        _themePresetName = "";
                    }
                    else
                    {
                        _themePresetName = themeOptions[newThemeIndex];
                        // Auto-fill color and background from preset
                        var preset = _cachedThemeDatabase.presets[newThemeIndex - 1];
                        _levelThemeColor = preset.themeColor;
                        _levelBackgroundSprite = preset.backgroundSprite;
                    }
                }
            }

            _levelThemeColor = EditorGUILayout.ColorField("Level Theme Color", _levelThemeColor);
            _levelBackgroundSprite = (Sprite)EditorGUILayout.ObjectField("Background Image", _levelBackgroundSprite, typeof(Sprite), false);

            GUILayout.Space(15);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            GUILayout.Space(5);

            // 2. Page Configuration List
            GUILayout.Label($"Pages & Rounds ({_pages.Count})", EditorStyles.boldLabel);
            GUILayout.Space(5);

            for (int i = 0; i < _pages.Count; i++)
            {
                PageData page = _pages[i];

                // Page Foldout Box Style
                GUILayout.BeginVertical("box");
                
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Page {i + 1}", EditorStyles.boldLabel);
                
                // Move Page Up/Down & Delete buttons
                GUI.enabled = (i > 0);
                if (GUILayout.Button("▲", GUILayout.Width(25)))
                {
                    SwapPages(i, i - 1);
                    break;
                }
                GUI.enabled = (i < _pages.Count - 1);
                if (GUILayout.Button("▼", GUILayout.Width(25)))
                {
                    SwapPages(i, i + 1);
                    break;
                }
                GUI.enabled = true;

                if (GUILayout.Button("Delete Page", GUILayout.Width(90)))
                {
                    _pages.RemoveAt(i);
                    break;
                }
                GUILayout.EndHorizontal();

                // Page parameters
                page.gameType = (GameType)EditorGUILayout.EnumPopup("Game Mode Type", page.gameType);

                // Dialogue lines editing section
                GUILayout.Label("Dialogue Lines (Supports {playername}):", EditorStyles.boldLabel);
                if (page.dialogueLines == null) page.dialogueLines = new List<DialogueLine>();
                for (int d = 0; d < page.dialogueLines.Count; d++)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Line {d + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        page.dialogueLines.RemoveAt(d);
                        break;
                    }
                    GUILayout.EndHorizontal();

                    if (page.dialogueLines[d] == null) page.dialogueLines[d] = new DialogueLine();
                    page.dialogueLines[d].text = EditorGUILayout.TextField("Text", page.dialogueLines[d].text);
                    page.dialogueLines[d].mascotAnimationTrigger = EditorGUILayout.TextField("Mascot Trigger", page.dialogueLines[d].mascotAnimationTrigger);
                    GUILayout.EndVertical();
                }
                if (GUILayout.Button("Add Dialogue Line", GUILayout.Width(130)))
                {
                    page.dialogueLines.Add(new DialogueLine());
                }

                GUILayout.Space(5);

                // Completion Dialogue lines editing section
                GUILayout.Label("Completion Dialogue Lines (Supports {playername}):", EditorStyles.boldLabel);
                if (page.completionDialogueLines == null) page.completionDialogueLines = new List<DialogueLine>();
                for (int d = 0; d < page.completionDialogueLines.Count; d++)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Line {d + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        page.completionDialogueLines.RemoveAt(d);
                        break;
                    }
                    GUILayout.EndHorizontal();

                    if (page.completionDialogueLines[d] == null) page.completionDialogueLines[d] = new DialogueLine();
                    page.completionDialogueLines[d].text = EditorGUILayout.TextField("Text", page.completionDialogueLines[d].text);
                    page.completionDialogueLines[d].mascotAnimationTrigger = EditorGUILayout.TextField("Mascot Trigger", page.completionDialogueLines[d].mascotAnimationTrigger);
                    GUILayout.EndVertical();
                }
                if (GUILayout.Button("Add Completion Line", GUILayout.Width(130)))
                {
                    page.completionDialogueLines.Add(new DialogueLine());
                }

                GUILayout.Space(5);

                // Show context-aware fields depending on GameType
                switch (page.gameType)
                {
                    case GameType.Counting:
                        page.countingSlotCount = EditorGUILayout.IntSlider("Slot Count", page.countingSlotCount, 1, 10);
                        page.countingMinCount = EditorGUILayout.IntSlider("Min Objects", page.countingMinCount, 1, 12);
                        page.countingMaxCount = EditorGUILayout.IntSlider("Max Objects", page.countingMaxCount, 1, 12);
                        page.countingDiceMode = EditorGUILayout.Toggle("Dice Mode", page.countingDiceMode);
                        page.countingFingerMode = EditorGUILayout.Toggle("Finger Mode", page.countingFingerMode);
                        page.countingActiveThemeName = EditorGUILayout.TextField("Theme Name (Optional)", page.countingActiveThemeName);
                        break;

                    case GameType.Addition:
                        page.additionSlotCount = EditorGUILayout.IntSlider("Slot Count", page.additionSlotCount, 1, 10);
                        page.additionMinPerGrid = EditorGUILayout.IntSlider("Min Per Grid Side", page.additionMinPerGrid, 1, 12);
                        page.additionMaxPerGrid = EditorGUILayout.IntSlider("Max Per Grid Side", page.additionMaxPerGrid, 1, 12);
                        page.additionDiceMode = EditorGUILayout.Toggle("Dice Mode", page.additionDiceMode);
                        page.additionFingerMode = EditorGUILayout.Toggle("Finger Mode", page.additionFingerMode);
                        page.additionCountAddMode = EditorGUILayout.Toggle("Count-Add Mode", page.additionCountAddMode);
                        page.additionNumbersOnlyMode = EditorGUILayout.Toggle("Numbers Only Mode", page.additionNumbersOnlyMode);
                        page.additionMinOperandCount = EditorGUILayout.IntSlider("Min Operands", page.additionMinOperandCount, 2, 5);
                        page.additionMaxOperandCount = EditorGUILayout.IntSlider("Max Operands", page.additionMaxOperandCount, 2, 5);
                        page.additionMinNumberValue = EditorGUILayout.IntField("Min Operand Value", page.additionMinNumberValue);
                        page.additionMaxNumberValue = EditorGUILayout.IntField("Max Operand Value", page.additionMaxNumberValue);
                        page.additionActiveThemeName = EditorGUILayout.TextField("Theme Name (Optional)", page.additionActiveThemeName);
                        break;

                    case GameType.Comparison:
                        page.comparisonSlotCount = EditorGUILayout.IntSlider("Slot Count", page.comparisonSlotCount, 1, 10);
                        page.comparisonMinVal = EditorGUILayout.IntField("Min Value", page.comparisonMinVal);
                        page.comparisonMaxVal = EditorGUILayout.IntField("Max Value", page.comparisonMaxVal);
                        page.comparisonMixAdditionEquations = EditorGUILayout.Toggle("Mix Addition", page.comparisonMixAdditionEquations);
                        page.comparisonNumbersOnlyMode = EditorGUILayout.Toggle("Numbers Only Mode", page.comparisonNumbersOnlyMode);
                        page.comparisonActiveThemeName = EditorGUILayout.TextField("Theme Name (Optional)", page.comparisonActiveThemeName);
                        break;

                    case GameType.Matching:
                        page.matchingLeftVariant = (MatchVariant)EditorGUILayout.EnumPopup("Left Side Type", page.matchingLeftVariant);
                        page.matchingRightVariant = (MatchVariant)EditorGUILayout.EnumPopup("Right Side Type", page.matchingRightVariant);
                        page.matchingSlotCount = EditorGUILayout.IntSlider("Slot Count (Pairs)", page.matchingSlotCount, 3, 10);
                        page.matchingMinVal = EditorGUILayout.IntField("Min Value", page.matchingMinVal);
                        page.matchingMaxVal = EditorGUILayout.IntField("Max Value", page.matchingMaxVal);
                        page.matchingShuffleLeftColumn = EditorGUILayout.Toggle("Shuffle Left Column", page.matchingShuffleLeftColumn);
                        break;

                    case GameType.Recall:
                        page.recallSlotCount = EditorGUILayout.IntSlider("Slot Count", page.recallSlotCount, 1, 10);
                        page.recallMinSequenceLength = EditorGUILayout.IntField("Min Seq Length", page.recallMinSequenceLength);
                        page.recallMaxSequenceLength = EditorGUILayout.IntField("Max Seq Length", page.recallMaxSequenceLength);
                        page.recallMinStartValue = EditorGUILayout.IntField("Min Start Val", page.recallMinStartValue);
                        page.recallMaxStartValue = EditorGUILayout.IntField("Max Start Val", page.recallMaxStartValue);
                        page.recallStep = EditorGUILayout.IntField("Step Count", page.recallStep);
                        page.recallCountBackwards = EditorGUILayout.Toggle("Count Backwards", page.recallCountBackwards);
                        page.recallMinConsecutiveRevealed = EditorGUILayout.IntSlider("Min Revealed", page.recallMinConsecutiveRevealed, 1, 5);
                        page.recallMaxConsecutiveRevealed = EditorGUILayout.IntSlider("Max Revealed", page.recallMaxConsecutiveRevealed, 1, 5);
                        page.recallMinConsecutiveHidden = EditorGUILayout.IntSlider("Min Hidden", page.recallMinConsecutiveHidden, 1, 5);
                        page.recallMaxConsecutiveHidden = EditorGUILayout.IntSlider("Max Hidden", page.recallMaxConsecutiveHidden, 1, 5);
                        break;

                    case GameType.Tracing:
                        page.tracingSpellModeActive = EditorGUILayout.Toggle("Spell Mode Active", page.tracingSpellModeActive);
                        if (page.tracingSpellModeActive)
                        {
                            page.tracingIsLearningMode = EditorGUILayout.Toggle("Is Learning Mode (Show Hints)", page.tracingIsLearningMode);
                        }
                        page.tracingCustomSpawnCount = EditorGUILayout.IntSlider("Custom Spawn Count", page.tracingCustomSpawnCount, 1, 4);
                        
                        // List of values to trace
                        GUILayout.Label("Values To Trace:", EditorStyles.boldLabel);
                        if (page.tracingValuesToTrace == null) page.tracingValuesToTrace = new List<string>();
                        for (int j = 0; j < page.tracingValuesToTrace.Count; j++)
                        {
                            GUILayout.BeginHorizontal();
                            page.tracingValuesToTrace[j] = EditorGUILayout.TextField($"Element {j}", page.tracingValuesToTrace[j]);
                            if (GUILayout.Button("X", GUILayout.Width(25)))
                            {
                                page.tracingValuesToTrace.RemoveAt(j);
                                break;
                            }
                            GUILayout.EndHorizontal();
                        }
                        if (GUILayout.Button("Add Value", GUILayout.Width(100)))
                        {
                            page.tracingValuesToTrace.Add("");
                        }
                        break;
                }

                GUILayout.EndVertical();
                GUILayout.Space(10);
            }

            if (GUILayout.Button("Add Page / Round", GUILayout.Height(30)))
            {
                _pages.Add(new PageData());
            }

            GUILayout.Space(25);
            EditorGUILayout.EndScrollView();

            // 3. Save / Generate buttons
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save / Generate Level", GUILayout.Height(40)))
            {
                GenerateLevelAsset();
            }
            if (GUILayout.Button("Load Level Details", GUILayout.Height(40)))
            {
                LoadExistingLevel();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        private void SwapPages(int a, int b)
        {
            PageData temp = _pages[a];
            _pages[a] = _pages[b];
            _pages[b] = temp;
        }

        private void CheckLevelStatus()
        {
            if (_cachedDatabase == null)
            {
                FindOrCreateDatabase();
            }

            LevelData existing = FindLevelInDatabase(_levelNumber);
            if (existing != null)
            {
                _statusText = $"Level {_levelNumber} already exists in database ('{existing.levelName}'). Loading details will overwrite window fields.";
                _statusType = MessageType.Warning;
            }
            else
            {
                _statusText = $"Level {_levelNumber} is a new level and does not exist in the database.";
                _statusType = MessageType.Info;
            }
        }

        private LevelData FindLevelInDatabase(int number)
        {
            if (_cachedDatabase == null) return null;
            foreach (var lvl in _cachedDatabase.allLevels)
            {
                if (lvl != null && lvl.levelNumber == number)
                {
                    return lvl;
                }
            }
            return null;
        }

        private void FindOrCreateDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedDatabase = AssetDatabase.LoadAssetAtPath<LevelDatabase>(path);
            }
        }

        private void FindOrCreateThemeDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:ThemeDatabase");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _cachedThemeDatabase = AssetDatabase.LoadAssetAtPath<ThemeDatabase>(path);
            }
        }

        private void ImportLevelDetails(LevelData source)
        {
            if (source != null)
            {
                LoadLevelDetailsInternal(source);
                ShowNotification(new GUIContent($"Imported fields from '{source.name}'!"));
            }
            else
            {
                ShowNotification(new GUIContent("Please assign a Level Data asset to import."));
            }
        }

        private void LoadExistingLevel()
        {
            LevelData existing = FindLevelInDatabase(_levelNumber);
            if (existing != null)
            {
                LoadLevelDetailsInternal(existing);
                ShowNotification(new GUIContent($"Loaded Level {_levelNumber} details!"));
            }
            else
            {
                ShowNotification(new GUIContent($"Level {_levelNumber} not found to load."));
            }
        }

        private void LoadLevelDetailsInternal(LevelData source)
        {
            _levelName = source.levelName;
            _levelSubtitle = source.levelSubtitle;
            _levelEndTip = source.levelEndTip;
            _isUnlockedByDefault = source.isUnlockedByDefault;
            _themePresetName = source.themePresetName;
            _levelThemeColor = source.levelThemeColor;
            _levelBackgroundSprite = source.levelBackgroundSprite;
            _pages = new List<PageData>();
            
            foreach (var page in source.pages)
            {
                PageData copy = new PageData
                {
                    gameType = page.gameType,
                    dialogueLines = CopyDialogueLines(page.dialogueLines),
                    completionDialogueLines = CopyDialogueLines(page.completionDialogueLines),
                    countingSlotCount = page.countingSlotCount,
                    countingMinCount = page.countingMinCount,
                    countingMaxCount = page.countingMaxCount,
                    countingDiceMode = page.countingDiceMode,
                    countingFingerMode = page.countingFingerMode,
                    countingActiveThemeName = page.countingActiveThemeName,
                    additionSlotCount = page.additionSlotCount,
                    additionMinPerGrid = page.additionMinPerGrid,
                    additionMaxPerGrid = page.additionMaxPerGrid,
                    additionDiceMode = page.additionDiceMode,
                    additionFingerMode = page.additionFingerMode,
                    additionCountAddMode = page.additionCountAddMode,
                    additionNumbersOnlyMode = page.additionNumbersOnlyMode,
                    additionMinOperandCount = page.additionMinOperandCount,
                    additionMaxOperandCount = page.additionMaxOperandCount,
                    additionMinNumberValue = page.additionMinNumberValue,
                    additionMaxNumberValue = page.additionMaxNumberValue,
                    additionActiveThemeName = page.additionActiveThemeName,
                    comparisonSlotCount = page.comparisonSlotCount,
                    comparisonMinVal = page.comparisonMinVal,
                    comparisonMaxVal = page.comparisonMaxVal,
                    comparisonMixAdditionEquations = page.comparisonMixAdditionEquations,
                    comparisonNumbersOnlyMode = page.comparisonNumbersOnlyMode,
                    comparisonActiveThemeName = page.comparisonActiveThemeName,
                    matchingLeftVariant = page.matchingLeftVariant,
                    matchingRightVariant = page.matchingRightVariant,
                    matchingSlotCount = page.matchingSlotCount,
                    matchingMinVal = page.matchingMinVal,
                    matchingMaxVal = page.matchingMaxVal,
                    matchingShuffleLeftColumn = page.matchingShuffleLeftColumn,
                    recallSlotCount = page.recallSlotCount,
                    recallMinSequenceLength = page.recallMinSequenceLength,
                    recallMaxSequenceLength = page.recallMaxSequenceLength,
                    recallMinStartValue = page.recallMinStartValue,
                    recallMaxStartValue = page.recallMaxStartValue,
                    recallStep = page.recallStep,
                    recallCountBackwards = page.recallCountBackwards,
                    recallMinConsecutiveRevealed = page.recallMinConsecutiveRevealed,
                    recallMaxConsecutiveRevealed = page.recallMaxConsecutiveRevealed,
                    recallMinConsecutiveHidden = page.recallMinConsecutiveHidden,
                    recallMaxConsecutiveHidden = page.recallMaxConsecutiveHidden,
                    tracingSpellModeActive = page.tracingSpellModeActive,
                    tracingIsLearningMode = page.tracingIsLearningMode,
                    tracingCustomSpawnCount = page.tracingCustomSpawnCount,
                    tracingValuesToTrace = page.tracingValuesToTrace != null ? new List<string>(page.tracingValuesToTrace) : new List<string>()
                };
                _pages.Add(copy);
            }
        }

        private List<DialogueLine> CopyDialogueLines(List<DialogueLine> original)
        {
            if (original == null) return new List<DialogueLine>();
            List<DialogueLine> copyList = new List<DialogueLine>();
            foreach (var line in original)
            {
                if (line != null)
                {
                    copyList.Add(new DialogueLine
                    {
                        text = line.text,
                        mascotAnimationTrigger = line.mascotAnimationTrigger
                    });
                }
            }
            return copyList;
        }

        private void GenerateLevelAsset()
        {
            // Ensure folder structure exists
            if (!AssetDatabase.IsValidFolder("Assets/Levels"))
            {
                AssetDatabase.CreateFolder("Assets", "Levels");
            }

            string assetPath = $"Assets/Levels/Level_{_levelNumber}.asset";
            LevelData asset = AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);

            bool isNew = (asset == null);
            if (isNew)
            {
                asset = CreateInstance<LevelData>();
            }

            // Populate fields
            asset.levelNumber = _levelNumber;
            asset.levelName = _levelName;
            asset.levelSubtitle = _levelSubtitle;
            asset.levelEndTip = _levelEndTip;
            asset.isUnlockedByDefault = _isUnlockedByDefault;
            asset.themePresetName = _themePresetName;
            asset.levelThemeColor = _levelThemeColor;
            asset.levelBackgroundSprite = _levelBackgroundSprite;
            asset.pages = new List<PageData>(_pages);

            if (isNew)
            {
                AssetDatabase.CreateAsset(asset, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Refresh database entry
            FindOrCreateDatabase();

            if (_cachedDatabase == null)
            {
                // Create a database if none exists
                _cachedDatabase = CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(_cachedDatabase, "Assets/Levels/LevelDatabase.asset");
                AssetDatabase.SaveAssets();
            }

            if (_cachedDatabase != null)
            {
                if (!_cachedDatabase.allLevels.Contains(asset))
                {
                    _cachedDatabase.allLevels.Add(asset);
                }
                
                // Keep the database sorted by level number
                _cachedDatabase.allLevels.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
                EditorUtility.SetDirty(_cachedDatabase);
                AssetDatabase.SaveAssets();
            }

            CheckLevelStatus();
            ShowNotification(new GUIContent($"Successfully generated/saved Level {_levelNumber}!"));
        }
    }
}
