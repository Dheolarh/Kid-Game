using UnityEditor;
using UnityEngine;

namespace KidGame.Editor
{
    public static class CleanMissingScriptsTool
    {
        [MenuItem("Tools/Remove Missing Scripts From Selected Prefabs")]
        public static void RemoveMissingScriptsFromSelection()
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[CleanMissingScripts] No GameObjects or Prefabs selected in Project/Hierarchy.");
                return;
            }

            int totalRemoved = 0;
            foreach (GameObject go in selectedObjects)
            {
                int count = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                totalRemoved += count;
                if (count > 0)
                {
                    EditorUtility.SetDirty(go);
                    Debug.Log($"[CleanMissingScripts] Removed {count} missing script(s) from '{go.name}'.");
                }

                // Also check all children recursively
                Transform[] allChildren = go.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in allChildren)
                {
                    if (child.gameObject == go) continue;
                    int childCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    totalRemoved += childCount;
                    if (childCount > 0)
                    {
                        EditorUtility.SetDirty(child.gameObject);
                        Debug.Log($"[CleanMissingScripts] Removed {childCount} missing script(s) from child '{child.name}'.");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CleanMissingScripts] Total missing scripts removed: {totalRemoved}.");
        }
    }
}
