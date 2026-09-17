using UnityEditor;
using UnityEngine;

public class BuildScript
{
    public static void BuildiOS()
    {
        string outputPath = System.Environment.GetEnvironmentVariable("OUTPUT_PATH") ?? "iOS Build";
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenes();
        buildPlayerOptions.locationPathName = outputPath;
        buildPlayerOptions.target = BuildTarget.iOS;
        buildPlayerOptions.options = BuildOptions.None;

        BuildPipeline.BuildPlayer(buildPlayerOptions);
    }

    private static string[] GetScenes()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }
        return scenes.ToArray();
    }
}