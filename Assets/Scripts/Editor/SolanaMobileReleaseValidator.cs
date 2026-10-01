using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
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
    private const string DevnetProfilePath = "Assets/Resources/DeploymentProfiles/DevnetOwnership.asset";
    private const string MainnetProfilePath = "Assets/Resources/DeploymentProfiles/MainnetSmoke.asset";
    private const string MainManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string MwaLibraryPath = "Assets/Plugins/Android/SignalJamSolanaMwa.androidlib";
    private const string MwaGradlePath = MwaLibraryPath + "/build.gradle";
    private const string MwaManifestPath = MwaLibraryPath + "/src/main/AndroidManifest.xml";
    private const string MwaBridgePath = MwaLibraryPath + "/src/main/java/com/signaljam/solana/mwa/SignalJamMwaBridge.kt";
    private const string RequiredMwaDependency = "mobile-wallet-adapter-clientlib-ktx:2.0.7";
    private const string RequiredKotlinPlugin = "id 'org.jetbrains.kotlin.android'";
    private const string KotlinPluginVersion = "2.1.10";

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android)
            return;

        List<string> errors = CollectErrors(report.summary.options, null);
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
        ValidateFromMenu(OwnershipDeploymentEnvironment.DevnetOwnership);
    }

    [MenuItem("Tools/Chainbound/Solana Mobile/Validate Mainnet Smoke")]
    public static void ValidateMainnetFromMenu()
    {
        ValidateFromMenu(OwnershipDeploymentEnvironment.MainnetSmoke);
    }

    private static void ValidateFromMenu(OwnershipDeploymentEnvironment environment)
    {
        List<string> errors = CollectErrors(BuildOptions.None, environment);
        if (errors.Count == 0)
        {
            Debug.Log($"[SolanaMobileReleaseValidator] {environment} Android configuration passed validation.");
            return;
        }

        Debug.LogError(
            $"[SolanaMobileReleaseValidator] {environment} Android configuration is not ready:\n - " +
            string.Join("\n - ", errors));
    }

    private static List<string> CollectErrors(
        BuildOptions options,
        OwnershipDeploymentEnvironment? environmentOverride)
    {
        var errors = new List<string>();
        OwnershipDeploymentEnvironment? commandSelection = DevelopmentAndroidBuildCommand.ActiveDeploymentEnvironment;
        OwnershipDeploymentEnvironment? selected = environmentOverride
            ?? commandSelection
            ?? ResolveDefinedEnvironment();
        string globalDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        bool globalDevnet = ContainsDefine(globalDefines, "CHAINBOUND_DEVNET_OWNERSHIP");
        bool globalMainnet = ContainsDefine(globalDefines, "CHAINBOUND_MAINNET_SMOKE");
        if (globalDevnet && globalMainnet)
            errors.Add("Android Player Settings contain both deployment defines; exactly one environment is allowed.");
        if (commandSelection == OwnershipDeploymentEnvironment.DevnetOwnership && globalMainnet)
            errors.Add("Devnet build is blocked because Android Player Settings contain the opposite Mainnet deployment define.");
        if (commandSelection == OwnershipDeploymentEnvironment.MainnetSmoke && globalDevnet)
            errors.Add("Mainnet build is blocked because Android Player Settings contain the opposite Devnet deployment define.");
        if (!selected.HasValue)
        {
            errors.Add("Android build must explicitly select CHAINBOUND_DEVNET_OWNERSHIP or CHAINBOUND_MAINNET_SMOKE.");
            selected = OwnershipDeploymentEnvironment.Disabled;
        }

        string profilePath = selected == OwnershipDeploymentEnvironment.MainnetSmoke
            ? MainnetProfilePath
            : DevnetProfilePath;
        PlatformBuildProfileSO profile = AssetDatabase.LoadAssetAtPath<PlatformBuildProfileSO>(profilePath);
        if (profile == null)
        {
            errors.Add($"Missing deployment profile at {profilePath}.");
        }
        else
        {
            errors.AddRange(OwnershipDeploymentProfileRules.Validate(profile, requireComplete: true));
            if (profile.OwnershipEnvironment != selected.Value)
                errors.Add($"Profile environment {profile.OwnershipEnvironment} does not match build selection {selected.Value}.");
        }

        SolanaConfigSO config = profile != null ? profile.SolanaConfig : null;
        if (config != null)
        {
            if (!Uri.TryCreate(config.mwaIdentityUri, UriKind.Absolute, out Uri identity) ||
                identity.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(identity.Host))
            {
                errors.Add("MWA identity URI must be an HTTPS URL owned by Chainbound; SolanaConfig currently has no valid value.");
            }

            if (string.IsNullOrWhiteSpace(config.mwaIdentityName))
                errors.Add("MWA identity name is empty.");

            string cluster = config.ownershipCluster?.Trim().ToLowerInvariant();
            if (cluster != "devnet" && cluster != "mainnet-beta")
                errors.Add("Ownership cluster must be explicitly set to devnet or mainnet-beta for MWA.");

            if (!IsRelativeIconUri(config.mwaIconUri))
                errors.Add("MWA icon URI must be a relative path beneath the identity URI (for example, icons/chainbound.png).");

            if (config.useStubServices)
                errors.Add("SolanaConfig is configured to use stub services.");

            if (!string.IsNullOrWhiteSpace(config.rpcEndpoint) &&
                !string.IsNullOrWhiteSpace(config.expectedGenesisHash))
            {
                try
                {
                    string endpoint = config.rpcEndpoint.Trim();
                    string actualGenesis = Task.Run(async () =>
                    {
                        using (var rpc = new SolanaRpcService(endpoint))
                            return await rpc.GetGenesisHashAsync().ConfigureAwait(false);
                    }).GetAwaiter().GetResult();
                    if (!string.Equals(actualGenesis, config.expectedGenesisHash.Trim(), StringComparison.Ordinal))
                        errors.Add($"RPC genesis mismatch: expected {config.expectedGenesisHash.Trim()}, received {actualGenesis}.");
                }
                catch (Exception exception)
                {
                    errors.Add("RPC genesis verification failed: " + exception.Message);
                }
            }
        }

        ValidateEnvironmentIsolation(selected.Value, profile, errors);

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

        if (!File.Exists(MwaBridgePath))
        {
            errors.Add($"Native MWA bridge source is missing at {MwaBridgePath}.");
        }
        else
        {
            string source = File.ReadAllText(MwaBridgePath);
            int clusterAssignment = source.IndexOf("adapter.rpcCluster =", StringComparison.Ordinal);
            int authTokenRestore = source.IndexOf("adapter.authToken = request.authToken", StringComparison.Ordinal);
            if (!source.Contains("RpcCluster.Devnet", StringComparison.Ordinal) ||
                !source.Contains("RpcCluster.MainnetBeta", StringComparison.Ordinal) ||
                clusterAssignment < 0)
            {
                errors.Add("Native MWA bridge must map devnet and mainnet-beta to explicit RpcCluster values.");
            }
            else if (authTokenRestore < 0 || clusterAssignment > authTokenRestore)
            {
                errors.Add("Native MWA bridge must select its RPC cluster before restoring a cached authorization token.");
            }
        }

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

    private static OwnershipDeploymentEnvironment? ResolveDefinedEnvironment()
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
        bool devnet = ContainsDefine(defines, "CHAINBOUND_DEVNET_OWNERSHIP");
        bool mainnet = ContainsDefine(defines, "CHAINBOUND_MAINNET_SMOKE");
        if (devnet == mainnet)
            return null;
        return mainnet
            ? OwnershipDeploymentEnvironment.MainnetSmoke
            : OwnershipDeploymentEnvironment.DevnetOwnership;
    }

    private static bool ContainsDefine(string defines, string expected)
    {
        string[] values = (defines ?? "").Split(';');
        return Array.Exists(values, value => string.Equals(value.Trim(), expected, StringComparison.Ordinal));
    }

    private static void ValidateEnvironmentIsolation(
        OwnershipDeploymentEnvironment selected,
        PlatformBuildProfileSO selectedProfile,
        ICollection<string> errors)
    {
        if (selected != OwnershipDeploymentEnvironment.MainnetSmoke || selectedProfile == null)
            return;

        PlatformBuildProfileSO devnet = AssetDatabase.LoadAssetAtPath<PlatformBuildProfileSO>(DevnetProfilePath);
        if (devnet == null)
        {
            errors.Add("Devnet deployment profile is missing, so Mainnet isolation cannot be verified.");
            return;
        }

        if (ReferenceEquals(selectedProfile.SolanaConfig, devnet.SolanaConfig))
            errors.Add("Mainnet Smoke must use a separate SolanaConfig asset from Devnet.");
        if (ReferenceEquals(selectedProfile.SupabaseConfig, devnet.SupabaseConfig))
            errors.Add("Mainnet Smoke must use a separate SupabaseConfig asset from Devnet.");

        if (selectedProfile.SupabaseConfig != null && devnet.SupabaseConfig != null &&
            Uri.TryCreate(selectedProfile.SupabaseConfig.supabaseUrl, UriKind.Absolute, out Uri mainnetBackend) &&
            Uri.TryCreate(devnet.SupabaseConfig.supabaseUrl, UriKind.Absolute, out Uri devnetBackend) &&
            string.Equals(mainnetBackend.Host, devnetBackend.Host, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Mainnet Smoke and Devnet must use different Supabase projects.");
        }

        if (selectedProfile.SolanaConfig != null && devnet.SolanaConfig != null)
        {
            if (string.Equals(selectedProfile.SolanaConfig.gameProgramId, devnet.SolanaConfig.gameProgramId, StringComparison.Ordinal))
                errors.Add("Mainnet Smoke must not reuse the Devnet game program ID.");
            if (string.Equals(selectedProfile.SolanaConfig.coreMovesCollectionId, devnet.SolanaConfig.coreMovesCollectionId, StringComparison.Ordinal))
                errors.Add("Mainnet Smoke must not reuse the Devnet Core Moves collection.");
        }
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
