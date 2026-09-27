using System;
using System.Collections.Generic;
using System.IO;
using Chainbound.Combat;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Prevents an Android beta from shipping with an MWA bridge that can never authorize.
/// Device behavior still requires an installed MWA-compatible wallet and on-device testing.
/// </summary>
public sealed class SolanaMobileReleaseValidator : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
{
    private const string ConfigPath = "Assets/Data/Solana/SolanaConfig.asset";
    private const string MainManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string MwaLibraryPath = "Assets/Plugins/Android/SignalJamSolanaMwa.androidlib";
    private const string MwaGradlePath = MwaLibraryPath + "/build.gradle";
    private const string MwaManifestPath = MwaLibraryPath + "/src/main/AndroidManifest.xml";
    private const string RequiredMwaDependency = "mobile-wallet-adapter-clientlib-ktx:2.0.7";
    private const string RequiredKotlinPlugin = "id 'org.jetbrains.kotlin.android'";
    private const string KotlinPluginVersion = "2.1.10";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        List<string> errors = CollectErrors(report.summary.options);
        if (errors.Count > 0)
        {
            throw new BuildFailedException(
                "Solana Mobile release validation failed:\n - " + string.Join("\n - ", errors));
        }
    }

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string rootBuildGradle = Path.GetFullPath(Path.Combine(path, "..", "build.gradle"));
        if (!File.Exists(rootBuildGradle))
            throw new BuildFailedException($"Generated Android root build.gradle was not found at {rootBuildGradle}.");

        string kotlinPluginLine =
            $"    id 'org.jetbrains.kotlin.android' version '{KotlinPluginVersion}' apply false";
        string[] lines = File.ReadAllLines(rootBuildGradle);
        if (Array.Exists(lines, line => string.Equals(line.Trim(), kotlinPluginLine.Trim(), StringComparison.Ordinal)))
            return;

        int androidLibraryPlugin = Array.FindIndex(
            lines,
            line => line.Contains("id 'com.android.library' version", StringComparison.Ordinal));
        if (androidLibraryPlugin < 0)
            throw new BuildFailedException("Could not locate the Android library plugin in generated root build.gradle.");

        var patched = new List<string>(lines);
        patched.Insert(androidLibraryPlugin + 1, kotlinPluginLine);
        File.WriteAllLines(rootBuildGradle, patched);
        Debug.Log($"[SolanaMobileReleaseValidator] Pinned generated Kotlin plugin to {KotlinPluginVersion}.");
    }

    [MenuItem("Tools/Chainbound/Solana Mobile/Validate Android Release")]
    public static void ValidateFromMenu()
    {
        List<string> errors = CollectErrors(BuildOptions.None);
        if (errors.Count == 0)
        {
            Debug.Log("[SolanaMobileReleaseValidator] Android MWA release configuration passed static validation.");
            return;
        }

        Debug.LogError(
            "[SolanaMobileReleaseValidator] Android MWA release configuration is not ready:\n - " +
            string.Join("\n - ", errors));
    }

    private static List<string> CollectErrors(BuildOptions options)
    {
        var errors = new List<string>();
        SolanaConfigSO config = AssetDatabase.LoadAssetAtPath<SolanaConfigSO>(ConfigPath);
        if (config == null)
        {
            errors.Add($"Missing SolanaConfigSO at {ConfigPath}.");
        }
        else
        {
            if (!Uri.TryCreate(config.mwaIdentityUri, UriKind.Absolute, out Uri identity) ||
                identity.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(identity.Host))
            {
                errors.Add("MWA identity URI must be an HTTPS URL owned by Chainbound; SolanaConfig currently has no valid value.");
            }

            if (string.IsNullOrWhiteSpace(config.mwaIdentityName))
                errors.Add("MWA identity name is empty.");

            if (!IsRelativeIconUri(config.mwaIconUri))
                errors.Add("MWA icon URI must be a relative path beneath the identity URI (for example, icons/chainbound.png).");

            if (config.useStubServices)
                errors.Add("SolanaConfig is configured to use stub services.");
        }

        if (!File.Exists(MainManifestPath))
            errors.Add($"Missing custom Android manifest at {MainManifestPath}.");
        if (!Directory.Exists(MwaLibraryPath) ||
            !File.Exists(MwaGradlePath))
            errors.Add($"Missing native Mobile Wallet Adapter Android library at {MwaLibraryPath}.");
        else if (!File.ReadAllText(MwaGradlePath).Contains(RequiredMwaDependency, StringComparison.Ordinal))
            errors.Add($"Native MWA library must pin the reviewed {RequiredMwaDependency} dependency.");
        else if (!File.ReadAllText(MwaGradlePath).Contains(RequiredKotlinPlugin, StringComparison.Ordinal))
            errors.Add("Native MWA library must apply the Kotlin Android plugin required by MWA 2.0.7.");

        if (!File.Exists(MwaManifestPath) ||
            !File.ReadAllText(MwaManifestPath).Contains("SignalJamMwaActivity", StringComparison.Ordinal))
            errors.Add("Native MWA host activity is missing from the Android library manifest.");

        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
            errors.Add("Android minimum SDK must be API 24 or newer for this MWA bridge.");
        if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            errors.Add("ARM64 must be enabled for Seeker releases.");
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
            errors.Add("Android scripting backend must be IL2CPP for the release build.");
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft &&
            PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            errors.Add("Android default orientation must be landscape or landscape-only autorotation.");

        string applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if (string.IsNullOrWhiteSpace(applicationId) || applicationId.StartsWith("com.Company", StringComparison.OrdinalIgnoreCase))
            errors.Add("Configure a stable Android application identifier.");
        if (PlayerSettings.Android.bundleVersionCode < 1)
            errors.Add("Android bundle version code must be positive.");

        bool developmentBuild = (options & BuildOptions.Development) != 0;
        if (!developmentBuild && !PlayerSettings.Android.useCustomKeystore)
            errors.Add("Non-development dApp Store APKs must use a dedicated custom signing keystore.");

        return errors;
    }

    private static bool IsRelativeIconUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Uri.TryCreate(value.Trim(), UriKind.Relative, out Uri relative))
            return false;
        return !value.Contains("..", StringComparison.Ordinal);
    }
}
