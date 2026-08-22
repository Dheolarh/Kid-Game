using UnityEngine;
using UnityEditor;

public class BatchRename : EditorWindow
{
    [MenuItem("Tools/Batch Rename 1 to 40")]
    public static void RenameObjects()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        System.Array.Sort(selectedObjects, (a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));

        int index = 1;
        foreach (GameObject obj in selectedObjects)
        {
            if (index > 40) break;
            Undo.RecordObject(obj, "Batch Rename");
            obj.name = index.ToString();
            index++;
        }
    }
}
