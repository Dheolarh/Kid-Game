using UnityEditor;
using UnityEngine;
using KidGame.Mechanics.Counting;

namespace KidGame.Editor
{
    [CustomEditor(typeof(PremadeLetterSlot))]
    public class PremadeLetterSlotEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PremadeLetterSlot slot = (PremadeLetterSlot)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Inspector Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Auto-Scan Hierarchy for Slots", GUILayout.Height(30)))
            {
                slot.AutoScanChildSlots();
                EditorUtility.SetDirty(slot);
            }
        }
    }
}
