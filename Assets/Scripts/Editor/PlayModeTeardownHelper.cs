using UnityEditor;

namespace KidGame.Editor
{

    [InitializeOnLoad]
    public static class PlayModeTeardownHelper
    {
        static PlayModeTeardownHelper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Selection.activeObject = null;
            }
        }
    }
}
