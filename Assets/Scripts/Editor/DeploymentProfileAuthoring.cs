using System;
using System.IO;
using Chainbound.Combat;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

public static class DeploymentProfileAuthoring
{
    public const string DevnetProfilePath = "Assets/Resources/DeploymentProfiles/DevnetOwnership.asset";
    public const string MainnetProfilePath = "Assets/Resources/DeploymentProfiles/MainnetSmoke.asset";
    public const string DevnetSolanaPath = "Assets/Data/Solana/SolanaConfig.asset";
    public const string MainnetSolanaPath = "Assets/Data/Solana/SolanaConfig_MainnetSmoke.asset";
    public const string DevnetSupabasePath = "Assets/Data/Supabase/SupabaseConfig.asset";
    public const string MainnetSupabasePath = "Assets/Data/Supabase/SupabaseConfig_MainnetSmoke.asset";
    private const string LegacyPlatformProfilePath = "Assets/Data/Solana/PlatformBuildProfile_SolanaDappStore.asset";
    private const string DevnetMetadataBase = "https://jhzqcbrzkausrrtcavuu.supabase.co/storage/v1/object/public/chainbound-ownership-metadata";

    private static bool queued;

    [CliCommand("author_deployment_profiles", "Author DevnetOwnership and fail-closed MainnetSmoke deployment assets.", MainThreadRequired = true)]
    public static string QueueAuthoring()
    {
        if (queued)
            return "already_queued";
        queued = true;
        EditorApplication.delayCall += Author;
        return "queued";
    }

    [MenuItem("Tools/Chainbound/Solana Mobile/Author Deployment Profiles")]
    public static void Author()
    {
        EditorApplication.delayCall -= Author;
        try
        {
            Directory.CreateDirectory("Assets/Resources/DeploymentProfiles");

            SolanaConfigSO devSolana = Require<SolanaConfigSO>(DevnetSolanaPath);
            SupabaseConfigSO devSupabase = Require<SupabaseConfigSO>(DevnetSupabasePath);
            PlatformBuildProfileSO legacy = Require<PlatformBuildProfileSO>(LegacyPlatformProfilePath);

            devSolana.ownershipCluster = "devnet";
            devSolana.expectedGenesisHash = OwnershipDeploymentProfileRules.DevnetGenesisHash;
            EditorUtility.SetDirty(devSolana);

            SolanaConfigSO mainSolana = LoadOrCreate<SolanaConfigSO>(MainnetSolanaPath, out bool createdMainSolana);
            if (createdMainSolana)
                ConfigureMainnetSolana(mainSolana, devSolana);
            SupabaseConfigSO mainSupabase = LoadOrCreate<SupabaseConfigSO>(MainnetSupabasePath, out bool createdMainSupabase);
            if (createdMainSupabase)
                ConfigureMainnetSupabase(mainSupabase);

            PlatformBuildProfileSO devnet = LoadOrCreateProfile(DevnetProfilePath, legacy, out _);
            ConfigureProfile(
                devnet,
                OwnershipDeploymentEnvironment.DevnetOwnership,
                devSolana,
                devSupabase,
                DevnetMetadataBase,
                allowDevelopmentTools: true);
            ConfigureProfile(
                legacy,
                OwnershipDeploymentEnvironment.DevnetOwnership,
                devSolana,
                devSupabase,
                DevnetMetadataBase,
                allowDevelopmentTools: true);

            PlatformBuildProfileSO mainnet = LoadOrCreateProfile(MainnetProfilePath, legacy, out bool createdMainnetProfile);
            if (createdMainnetProfile)
            {
                ConfigureProfile(
                    mainnet,
                    OwnershipDeploymentEnvironment.MainnetSmoke,
                    mainSolana,
                    mainSupabase,
                    "",
                    allowDevelopmentTools: false);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[DeploymentProfileAuthoring] DevnetOwnership is ready; MainnetSmoke remains intentionally incomplete.");
        }
        finally
        {
            queued = false;
        }
    }

    private static T Require<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException("Missing required asset: " + path);
    }

    private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            created = false;
            return asset;
        }
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        created = true;
        return asset;
    }

    private static PlatformBuildProfileSO LoadOrCreateProfile(
        string path,
        PlatformBuildProfileSO template,
        out bool created)
    {
        PlatformBuildProfileSO profile = AssetDatabase.LoadAssetAtPath<PlatformBuildProfileSO>(path);
        if (profile != null)
        {
            created = false;
            return profile;
        }
        profile = ScriptableObject.CreateInstance<PlatformBuildProfileSO>();
        EditorUtility.CopySerialized(template, profile);
        AssetDatabase.CreateAsset(profile, path);
        created = true;
        return profile;
    }

    private static void ConfigureMainnetSolana(SolanaConfigSO config, SolanaConfigSO identitySource)
    {
        config.rpcEndpoint = "";
        config.wsEndpoint = "";
        config.gameProgramId = "";
        config.vrfProgramId = "";
        config.vrfOracleQueueId = "";
        config.ownershipCluster = "mainnet-beta";
        config.expectedGenesisHash = OwnershipDeploymentProfileRules.MainnetGenesisHash;
        config.coreMovesCollectionId = "";
        config.coreMovesUpdateAuthority = "";
        config.coreArchivesCollectionId = "";
        config.coreCoversCollectionId = "";
        config.dasRpcEndpoint = "";
        config.coreCosmeticsCollectionId = "";
        config.mwaIdentityUri = identitySource.mwaIdentityUri;
        config.mwaIconUri = identitySource.mwaIconUri;
        config.mwaIdentityName = identitySource.mwaIdentityName;
        config.useDesktopBridgeInEditor = false;
        config.autoBootstrapPdasOnWalletConnect = false;
        config.useStubServices = false;
        config.useDeterministicVrfInEditor = false;
        config.enableRpcLogging = false;
        EditorUtility.SetDirty(config);
    }

    private static void ConfigureMainnetSupabase(SupabaseConfigSO config)
    {
        config.supabaseUrl = "";
        config.supabaseAnonKey = "";
        config.edgeFunctionBaseUrl = "";
        config.requestTimeoutSeconds = 10;
        config.maxRetries = 1;
        config.enableDebugLogs = false;
        EditorUtility.SetDirty(config);
    }

    private static void ConfigureProfile(
        PlatformBuildProfileSO profile,
        OwnershipDeploymentEnvironment environment,
        SolanaConfigSO solana,
        SupabaseConfigSO supabase,
        string metadataBase,
        bool allowDevelopmentTools)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("buildPlatform").enumValueIndex = (int)BuildPlatform.SolanaDappStore;
        serialized.FindProperty("backendEnvironment").stringValue =
            environment == OwnershipDeploymentEnvironment.MainnetSmoke ? "mainnet-smoke" : "devnet";
        serialized.FindProperty("ownershipEnvironment").enumValueIndex = (int)environment;
        serialized.FindProperty("solanaConfig").objectReferenceValue = solana;
        serialized.FindProperty("supabaseConfig").objectReferenceValue = supabase;
        serialized.FindProperty("ownershipMetadataBaseUrl").stringValue = metadataBase;
        serialized.FindProperty("enableOwnershipRuntime").boolValue = true;
        serialized.FindProperty("allowDevelopmentTools").boolValue = allowDevelopmentTools;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }
}
