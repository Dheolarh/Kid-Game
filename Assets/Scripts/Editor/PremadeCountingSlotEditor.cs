using UnityEditor;
using UnityEngine;
using KidGame.Mechanics.Counting;

namespace KidGame.Editor
{
    [CustomEditor(typeof(PremadeCountingSlot))]
    public class PremadeCountingSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PremadeCountingSlot slot = (PremadeCountingSlot)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inspector Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Scan Hierarchy for Counting Slots", GUILayout.Height(30)))
            {
                slot.AutoScanChildSlots();
                EditorUtility.SetDirty(slot);
            }
        }
    }
}
