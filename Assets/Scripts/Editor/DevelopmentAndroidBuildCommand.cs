using System.Collections.Generic;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DevelopmentAndroidBuildCommand
{
    private static bool queued;

    [CliCommand("build_development_android", "Queue an explicit Development Android APK build.", MainThreadRequired = true)]
    public static string QueueBuild()
    {
        if (queued)
            return "already_queued";

        queued = true;
        EditorApplication.delayCall += Build;
        return "queued";
    }

    private static void Build()
    {
        bool originalStripEngineCode = PlayerSettings.stripEngineCode;
        try
        {
            List<string> scenes = new List<string>();
            EditorBuildSettingsScene[] configuredScenes = EditorBuildSettings.scenes;
            for (int i = 0; i < configuredScenes.Length; i++)
            {
                if (configuredScenes[i].enabled && !string.IsNullOrWhiteSpace(configuredScenes[i].path))
                    scenes.Add(configuredScenes[i].path);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputPath = Path.Combine(
                projectRoot,
                "Builds",
                "Android",
                "Chainbound-CandyMachineDevStripFix-Development-v2.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Unity 6000.0.24f1 stripped the profiler internal-call binding required by UGUI
            // from this Android Development player. Keep production settings untouched while
            // retaining every engine binding needed for on-device diagnostics and UI testing.
            PlayerSettings.stripEngineCode = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
            });

            Debug.Log($"[DevelopmentAndroidBuild] {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; output={outputPath}");
        }
        finally
        {
            PlayerSettings.stripEngineCode = originalStripEngineCode;
            queued = false;
        }
    }
}
