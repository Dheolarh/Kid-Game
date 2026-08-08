using UnityEditor;
using UnityEngine;
using KidGame.Mechanics.NumberRecall;

namespace KidGame.Editor
{
    [CustomEditor(typeof(PremadeRecallSlot))]
    public class PremadeRecallSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            PremadeRecallSlot slot = (PremadeRecallSlot)target;

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Premade Recall Slot Layout:\n" +
                "- Assign Rows and Boxes manually below, OR click 'Auto-Scan' to automatically pull child GameObjects from hierarchy.\n" +
                "- For each Box: Check 'Is Answer Box' for empty slots, and type the expected answer value.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.2f, 0.7f, 1.0f);
            if (GUILayout.Button("Auto-Scan Children Rows & Boxes", GUILayout.Height(30)))
            {
                Undo.RecordObject(slot, "Auto-Scan Rows and Boxes");
                slot.AutoScanChildren();
                EditorUtility.SetDirty(slot);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            SerializedProperty rowsProp = serializedObject.FindProperty("rows");
            EditorGUILayout.PropertyField(rowsProp, new GUIContent("Configured Rows & Boxes"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
