using System.Collections.Generic;
using System.IO;
using Chainbound.Combat;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class DevelopmentAndroidBuildCommand
{
    private static bool queued;
    private static OwnershipDeploymentEnvironment queuedEnvironment;

    public static OwnershipDeploymentEnvironment? ActiveDeploymentEnvironment { get; private set; }

    [CliCommand("build_development_android", "Queue an explicit Development Android APK build.", MainThreadRequired = true)]
    public static string QueueBuild()
    {
        return QueueBuild(OwnershipDeploymentEnvironment.DevnetOwnership);
    }

    [CliCommand("build_mainnet_smoke_android", "Queue a Mainnet Smoke Android APK build. Fails closed until Mainnet configuration is complete.", MainThreadRequired = true)]
    public static string QueueMainnetSmokeBuild()
    {
        return QueueBuild(OwnershipDeploymentEnvironment.MainnetSmoke);
    }

    private static string QueueBuild(OwnershipDeploymentEnvironment environment)
    {
        if (queued)
            return "already_queued";

        queued = true;
        queuedEnvironment = environment;
        // BuildPipeline.BuildPlayer blocks the editor for the duration of the
        // build. Scheduling through update avoids the Pipeline request being
        // invalidated by a domain reload or a nested delayCall dispatch.
        EditorApplication.update -= Build;
        EditorApplication.update += Build;
        return "queued";
    }

    private static void Build()
    {
        EditorApplication.update -= Build;
        bool originalStripEngineCode = PlayerSettings.stripEngineCode;
        bool originalUseCustomKeystore = PlayerSettings.Android.useCustomKeystore;
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
                queuedEnvironment == OwnershipDeploymentEnvironment.MainnetSmoke
                    ? "Chainbound-MainnetSmoke-Development.apk"
                    : "Chainbound-CandyMachineDevStripFix-Development-v2.apk");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            // Unity 6000.0.24f1 stripped the profiler internal-call binding required by UGUI
            // from this Android Development player. Keep production settings untouched while
            // retaining every engine binding needed for on-device diagnostics and UI testing.
            PlayerSettings.stripEngineCode = false;

            // Development APKs can use Unity's standard debug signer when the
            // editor-local custom keystore password cache is unavailable. Do
            // not silently change release signing: this fallback is limited to
            // this explicit Development build and the authored setting is
            // restored in finally below.
            if (originalUseCustomKeystore &&
                (string.IsNullOrWhiteSpace(PlayerSettings.Android.keystorePass) ||
                 string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasPass)))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[DevelopmentAndroidBuild] Custom keystore credentials are unavailable; using Unity debug signing for this Development APK. Release signing settings remain unchanged.");
            }

            ActiveDeploymentEnvironment = queuedEnvironment;
            string deploymentDefine = queuedEnvironment == OwnershipDeploymentEnvironment.MainnetSmoke
                ? "CHAINBOUND_MAINNET_SMOKE"
                : "CHAINBOUND_DEVNET_OWNERSHIP";
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "SIGNALJAM_BUILD_SOLANA_DAPP_STORE", deploymentDefine },
            });

            Debug.Log($"[DevelopmentAndroidBuild] {queuedEnvironment}: {report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}; output={outputPath}");
        }
        finally
        {
            ActiveDeploymentEnvironment = null;
            PlayerSettings.stripEngineCode = originalStripEngineCode;
            PlayerSettings.Android.useCustomKeystore = originalUseCustomKeystore;
            queued = false;
        }
    }
}
