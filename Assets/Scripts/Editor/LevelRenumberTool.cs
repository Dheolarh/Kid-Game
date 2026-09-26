using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using KidGame.Interface;

namespace KidGame.Editor
{
    /// <summary>
    /// One-shot utility that reorders the level database so 10 mis-placed spelling lessons
    /// land in their correct slots.
    ///
    /// It works with two buckets, as requested:
    ///   - OLD bucket: every current lesson, sorted by its existing levelNumber.
    ///   - NEW bucket: built up 1..185. At each insertion slot the designated mover is pulled
    ///     from the old bucket; otherwise the next unused lesson is pulled in ascending order.
    ///
    /// Nothing is added or deleted: 185 lessons in, 185 out. Each lesson keeps all of its
    /// content (pages, prefabs, theme) and only its levelNumber / levelName are rewritten.
    ///
    /// SAFETY: "Preview Mapping" prints the full old -> new table and writes nothing.
    ///         Commit to git before pressing "Apply".
    /// </summary>
    public class LevelRenumberTool : EditorWindow
    {
        private const string LevelsFolder = "Assets/Levels";

        private const int FirstLevel = 1;
        private const int LastLevel = 185;

        /// <summary>
        /// Target slot -> the CURRENT lesson number that belongs there.
        /// The 10 spelling levels currently parked at 176..185 go into these slots.
        /// </summary>
        private static readonly KeyValuePair<int, int>[] Insertions =
        {
            new KeyValuePair<int, int>(7, 176),
            new KeyValuePair<int, int>(14, 177),
            new KeyValuePair<int, int>(19, 178),
            new KeyValuePair<int, int>(26, 179),
            new KeyValuePair<int, int>(32, 180),
            new KeyValuePair<int, int>(38, 181),
            new KeyValuePair<int, int>(43, 182),
            new KeyValuePair<int, int>(51, 183),
            new KeyValuePair<int, int>(58, 184),
            new KeyValuePair<int, int>(64, 185),
        };

        private Vector2 _scroll;
        private string _previewText = "";
        private List<Mapping> _mapping;

        private class Mapping
        {
            public int OldNumber;
            public int NewNumber;
            public LevelData Asset;
            public bool IsMover;
        }

        [MenuItem("Tools/Level Renumber Tool")]
        public static void Open()
        {
            var w = GetWindow<LevelRenumberTool>("Level Renumber");
            w.minSize = new Vector2(420, 500);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Level Renumber Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Reorders 10 mis-placed spelling lessons into slots 7, 14, 19, 26, 32, 38, 43, 51, 58, 64.\n\n" +
                "Step 1: press Preview Mapping and check the table.\n" +
                "Step 2: commit to git.\n" +
                "Step 3: press Apply.\n\n" +
                "185 lessons in, 185 out. Content is preserved; only levelNumber / levelName change.",
                MessageType.Info);

            GUILayout.Space(8);

            if (GUILayout.Button("1. Preview Mapping", GUILayout.Height(30)))
            {
                BuildMapping();
                _previewText = RenderPreview();
                Debug.Log(_previewText);
                Debug.Log("[LevelRenumberTool] Preview built. Nothing was written.");
            }

            if (_mapping != null)
            {
                int movers = _mapping.Count(m => m.IsMover);
                EditorGUILayout.LabelField($"Mapped {_mapping.Count} lessons ({movers} movers).", EditorStyles.miniLabel);
            }

            GUILayout.Space(8);

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("2. Apply Renumber (writes files)", GUILayout.Height(30)))
            {
                if (_mapping == null)
                {
                    EditorUtility.DisplayDialog("Preview first",
                        "Press 'Preview Mapping' first and check the result.", "OK");
                }
                else if (EditorUtility.DisplayDialog("Confirm",
                    $"Rewrite {_mapping.Count} level assets and rename their files?\n\n" +
                    "Make sure you have committed to git first.", "Apply", "Cancel"))
                {
                    Apply();
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (!string.IsNullOrEmpty(_previewText))
            {
                EditorGUILayout.TextArea(_previewText, GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Mapping ───────────────────────────────────────────────────────────

        private void BuildMapping()
        {
            _mapping = new List<Mapping>();

            // Load every LevelData asset in the project, keyed by its CURRENT levelNumber.
            var byOldNumber = new Dictionary<int, LevelData>();
            foreach (string guid in AssetDatabase.FindAssets("t:LevelData", new[] { LevelsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null) continue;

                if (byOldNumber.ContainsKey(level.levelNumber))
                {
                    Debug.LogError($"[LevelRenumberTool] Duplicate levelNumber {level.levelNumber} " +
                                   $"({path} and {AssetDatabase.GetAssetPath(byOldNumber[level.levelNumber])}). Aborting.");
                    _mapping = null;
                    return;
                }
                byOldNumber[level.levelNumber] = level;
            }

            var insertionBySlot = Insertions.ToDictionary(kv => kv.Key, kv => kv.Value);
            var moverOldNumbers = new HashSet<int>(Insertions.Select(kv => kv.Value));

            // Old bucket: non-movers in ascending order. Movers are held back for their slot.
            var pool = new Queue<LevelData>(
                byOldNumber.Keys
                    .Where(n => !moverOldNumbers.Contains(n))
                    .OrderBy(n => n)
                    .Where(n => byOldNumber[n] != null)
                    .Select(n => byOldNumber[n]));

            // New bucket: fill 1..185 in order.
            for (int newNumber = FirstLevel; newNumber <= LastLevel; newNumber++)
            {
                LevelData chosen;
                bool isMover = false;

                if (insertionBySlot.TryGetValue(newNumber, out int moverOldNumber))
                {
                    if (!byOldNumber.TryGetValue(moverOldNumber, out chosen) || chosen == null)
                    {
                        Debug.LogError($"[LevelRenumberTool] Mover lesson {moverOldNumber} not found. Aborting.");
                        _mapping = null;
                        return;
                    }
                    isMover = true;
                }
                else
                {
                    if (pool.Count == 0)
                    {
                        Debug.LogError($"[LevelRenumberTool] Ran out of lessons at new slot {newNumber}. " +
                                       "The level set is not 1..185 contiguous. Aborting.");
                        _mapping = null;
                        return;
                    }
                    chosen = pool.Dequeue();
                }

                _mapping.Add(new Mapping
                {
                    OldNumber = chosen.levelNumber,
                    NewNumber = newNumber,
                    Asset = chosen,
                    IsMover = isMover
                });
            }

            if (pool.Count != 0)
            {
                Debug.LogWarning($"[LevelRenumberTool] {pool.Count} lesson(s) left over and unused: " +
                                 string.Join(", ", pool.Select(l => l.levelNumber)));
            }
        }

        private string RenderPreview()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== LEVEL RENUMBER PREVIEW ({_mapping.Count} lessons) ===");

            foreach (var m in _mapping)
            {
                if (m.OldNumber != m.NewNumber)
                {
                    sb.AppendLine($"{(m.IsMover ? "MOVER" : "     ")}  old {m.OldNumber,3}  ->  new {m.NewNumber,3}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("=== FIRST 30 SLOTS (final order) ===");
            foreach (var m in _mapping.Take(30))
            {
                sb.AppendLine($"  slot {m.NewNumber,3}  <=  old {m.OldNumber,3}{(m.IsMover ? "   <-- inserted" : "")}");
            }

            int unchanged = _mapping.Count(m => m.OldNumber == m.NewNumber);
            sb.AppendLine();
            sb.AppendLine($"Unchanged: {unchanged}   Moved: {_mapping.Count - unchanged}   Total: {_mapping.Count}");
            return sb.ToString();
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        private void Apply()
        {
            // Renaming files while their numbers are in flux means names collide (Level_7 -> Level_8
            // while Level_8 still exists). So every asset is first moved to a unique staging name,
            // then to its final name. MoveAsset keeps the GUID, so the LevelDatabase references survive.
            const string stagingPrefix = "__renumber_tmp_";

            try
            {
                AssetDatabase.StartAssetEditing();

                // Pass 1: everything to a unique staging name.
                foreach (var m in _mapping)
                {
                    string currentPath = AssetDatabase.GetAssetPath(m.Asset);
                    string stagingPath = $"{LevelsFolder}/{stagingPrefix}{m.OldNumber}.asset";

                    if (currentPath != stagingPath)
                    {
                        string err = AssetDatabase.MoveAsset(currentPath, stagingPath);
                        if (!string.IsNullOrEmpty(err))
                        {
                            Debug.LogError($"[LevelRenumberTool] Staging move failed for {currentPath}: {err}");
                            return;
                        }
                    }
                }

                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                AssetDatabase.StartAssetEditing();

                // Pass 2: staging name -> final name, and rewrite the data fields.
                foreach (var m in _mapping)
                {
                    string stagingPath = $"{LevelsFolder}/{stagingPrefix}{m.OldNumber}.asset";
                    string finalPath = $"{LevelsFolder}/Level_{m.NewNumber}.asset";

                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(stagingPath);
                    if (level == null)
                    {
                        Debug.LogError($"[LevelRenumberTool] Could not reload staged asset {stagingPath}. Aborting.");
                        return;
                    }

                    level.levelNumber = m.NewNumber;
                    level.levelName = $"LESSON {m.NewNumber}";
                    EditorUtility.SetDirty(level);

                    string err = AssetDatabase.MoveAsset(stagingPath, finalPath);
                    if (!string.IsNullOrEmpty(err))
                    {
                        Debug.LogError($"[LevelRenumberTool] Final move failed for {stagingPath}: {err}");
                        return;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Rebuild the database list in the new order.
            var dbGuids = AssetDatabase.FindAssets("t:LevelDatabase");
            if (dbGuids.Length == 0)
            {
                Debug.LogWarning("[LevelRenumberTool] No LevelDatabase found to reorder.");
            }
            else
            {
                var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));
                if (db != null)
                {
                    db.allLevels = _mapping
                        .OrderBy(m => m.NewNumber)
                        .Select(m => AssetDatabase.LoadAssetAtPath<LevelData>($"{LevelsFolder}/Level_{m.NewNumber}.asset"))
                        .Where(l => l != null)
                        .ToList();

                    EditorUtility.SetDirty(db);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[LevelRenumberTool] Database reordered with {db.allLevels.Count} levels.");
                }
            }

            Debug.Log("[LevelRenumberTool] DONE. Re-import if any asset looks stale.");
            _mapping = null;
            _previewText = "";
        }
    }
}
