using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(AutoFitter))]
public class AutoFitterEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AutoFitter fitter = (AutoFitter)target;

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("Capture From Scene", GUILayout.Height(30)))
        {
            fitter.CaptureFromScene();
        }
        GUI.backgroundColor = Color.white;
    }
}
