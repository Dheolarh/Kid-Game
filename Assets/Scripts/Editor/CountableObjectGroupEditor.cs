using UnityEditor;
using UnityEngine;
using KidGame.Mechanics.Counting;

namespace KidGame.Editor
{
    [CustomEditor(typeof(CountableObjectGroup))]
    public class CountableObjectGroupEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CountableObjectGroup group = (CountableObjectGroup)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inspector Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Assign Child Objects", GUILayout.Height(30)))
            {
                group.InitializeGroup();
                EditorUtility.SetDirty(group);
                Debug.Log($"[CountableObjectGroup] Auto-assigned {group.Count} child objects to count group.");
            }

            if (Application.isPlaying)
            {
                if (GUILayout.Button("Test Pop All Items", GUILayout.Height(30)))
                {
                    for (int i = 0; i < group.Count; i++)
                    {
                        group.OnItemTapped(i, group.transform.GetChild(i).gameObject);
                    }
                }
            }
        }
    }
}
