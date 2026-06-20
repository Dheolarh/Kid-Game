using UnityEngine;
using UnityEditor;
using KidGame.Interface;

namespace KidGame.EditorTools
{
    public class LevelGeneratorWindow : EditorWindow
    {
        private string _baseLevelName = "Level ";
        private string _baseSceneName = "LevelScene_";
        private int _startNumber = 1;
        private int _count = 18;
        private bool _isUnlockedByDefault = false;
        private string _saveFolder = "Assets/LevelsData";
        private LevelDatabase _targetDatabase;

        private Vector2 _scrollPosition;

        [MenuItem("Tools/Level Generator")]
        public static void ShowWindow()
        {
            GetWindow<LevelGeneratorWindow>("Level Generator");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(10);
            GUILayout.Label("Level Generation Tool", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Create multiple LevelData ScriptableObjects instantly and register them in a LevelDatabase.", EditorStyles.miniLabel);
            EditorGUILayout.Space(10);

            // Level settings block
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Level Data Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _baseLevelName = EditorGUILayout.TextField("Base Level Name", _baseLevelName);
            _baseSceneName = EditorGUILayout.TextField("Base Scene Name", _baseSceneName);
            _startNumber = EditorGUILayout.IntField("Start Number", _startNumber);
            _count = EditorGUILayout.IntField("Number of Levels", _count);
            _isUnlockedByDefault = EditorGUILayout.Toggle("Unlocked By Default", _isUnlockedByDefault);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);

            // Asset settings block
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Asset Target Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _saveFolder = EditorGUILayout.TextField("Save Folder Path", _saveFolder);
            _targetDatabase = (LevelDatabase)EditorGUILayout.ObjectField("Target Database", _targetDatabase, typeof(LevelDatabase), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(20);

            // Action button
            EditorGUI.BeginDisabledGroup(_targetDatabase == null || _count <= 0);
            if (GUILayout.Button("Generate and Add to Database", GUILayout.Height(40)))
            {
                GenerateLevels();
            }
            EditorGUI.EndDisabledGroup();

            if (_targetDatabase == null)
            {
                EditorGUILayout.HelpBox("Please assign a Target Database to enable generation.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }

        private void GenerateLevels()
        {
            // Validate and create the save folder if it doesn't exist
            if (!AssetDatabase.IsValidFolder(_saveFolder))
            {
                // Create intermediate folders if necessary
                string[] folders = _saveFolder.Split('/');
                string currentPath = folders[0]; // should be Assets

                for (int i = 1; i < folders.Length; i++)
                {
                    string targetPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(targetPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = targetPath;
                }
            }

            Undo.RecordObject(_targetDatabase, "Generate Levels");

            int generatedCount = 0;
            for (int i = 0; i < _count; i++)
            {
                int currentNum = _startNumber + i;
                string levelName = $"{_baseLevelName}{currentNum}";
                string sceneToLoad = $"{_baseSceneName}{currentNum}";

                // Create individual LevelData asset
                LevelData data = CreateInstance<LevelData>();
                data.levelName = levelName;
                data.sceneToLoad = sceneToLoad;
                data.isUnlockedByDefault = _isUnlockedByDefault;

                // Path name setup
                string assetPath = $"{_saveFolder}/Level_{currentNum}.asset";
                
                // Prevent overriding existing level data unless intended (or save sequentially)
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                AssetDatabase.CreateAsset(data, assetPath);
                generatedCount++;

                // Add to database
                _targetDatabase.allLevels.Add(data);
            }

            // Save and refresh database asset state
            EditorUtility.SetDirty(_targetDatabase);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Refresh Level Select view in editor if present
            LevelSelectManager manager = FindAnyObjectByType<LevelSelectManager>();
            if (manager != null)
            {
                manager.InitializeLevelSelect();
            }

            EditorUtility.DisplayDialog("Success", $"Successfully generated {generatedCount} LevelData assets under {_saveFolder} and registered them in {_targetDatabase.name}!", "OK");
        }
    }
}