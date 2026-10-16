using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum OwnershipDeploymentEnvironment
    {
        Disabled = 0,
        DevnetOwnership = 1,
        MainnetSmoke = 2,
    }

    [CreateAssetMenu(fileName = "PlatformBuildProfile", menuName = "Chainbound/Platform/Accounts/Platform Build Profile")]
    public class PlatformBuildProfileSO : ScriptableObject, IPlatformBuildProfile
    {
        [SerializeField] private BuildPlatform buildPlatform = BuildPlatform.Editor;
        [SerializeField] private PlatformCapabilities capabilities = PlatformCapabilities.EditorDefaults();
        [SerializeField] private string backendEnvironment = "production";
        [SerializeField] private bool walletForwardStart;

        [Header("Ownership Deployment")]
        [SerializeField] private OwnershipDeploymentEnvironment ownershipEnvironment;
        [SerializeField] private SolanaConfigSO solanaConfig;
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private string ownershipMetadataBaseUrl = "";
        [SerializeField] private bool enableOwnershipRuntime;
        [SerializeField] private bool allowDevelopmentTools;

        public BuildPlatform BuildPlatform => buildPlatform;
        public PlatformCapabilities Capabilities => capabilities;
        public string BackendEnvironment => backendEnvironment;
        public bool WalletForwardStart => walletForwardStart;
        public OwnershipDeploymentEnvironment OwnershipEnvironment => ownershipEnvironment;
        public SolanaConfigSO SolanaConfig => solanaConfig;
        public SupabaseConfigSO SupabaseConfig => supabaseConfig;
        public string OwnershipMetadataBaseUrl => ownershipMetadataBaseUrl;
        public bool EnableOwnershipRuntime => enableOwnershipRuntime;
        public bool AllowDevelopmentTools => allowDevelopmentTools;
    }

    public static class OwnershipDeploymentProfileRules
    {
        public const string DevnetGenesisHash = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
        public const string MainnetGenesisHash = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d";
        public const string DevnetResourcePath = "DeploymentProfiles/DevnetOwnership";
        public const string MainnetResourcePath = "DeploymentProfiles/MainnetSmoke";

        public static string ExpectedCluster(OwnershipDeploymentEnvironment environment)
        {
            switch (environment)
            {
                case OwnershipDeploymentEnvironment.DevnetOwnership:
                    return "devnet";
                case OwnershipDeploymentEnvironment.MainnetSmoke:
                    return "mainnet-beta";
                default:
                    return "";
            }
        }

        public static string ExpectedGenesisHash(OwnershipDeploymentEnvironment environment)
        {
            switch (environment)
            {
                case OwnershipDeploymentEnvironment.DevnetOwnership:
                    return DevnetGenesisHash;
                case OwnershipDeploymentEnvironment.MainnetSmoke:
                    return MainnetGenesisHash;
                default:
                    return "";
            }
        }

        public static List<string> Validate(PlatformBuildProfileSO profile, bool requireComplete)
        {
            var errors = new List<string>();
            if (profile == null)
            {
                errors.Add("Deployment profile is missing.");
                return errors;
            }

            if (profile.OwnershipEnvironment == OwnershipDeploymentEnvironment.Disabled)
                errors.Add("Ownership deployment environment is disabled.");
            if (!profile.EnableOwnershipRuntime)
                errors.Add("Ownership runtime is disabled.");
            if (string.IsNullOrWhiteSpace(profile.BackendEnvironment))
                errors.Add("Backend environment identifier is empty.");
            string expectedBackendEnvironment = profile.OwnershipEnvironment == OwnershipDeploymentEnvironment.DevnetOwnership
                ? "devnet"
                : profile.OwnershipEnvironment == OwnershipDeploymentEnvironment.MainnetSmoke
                    ? "mainnet-smoke"
                    : "";
            if (!string.Equals(profile.BackendEnvironment?.Trim(), expectedBackendEnvironment, StringComparison.Ordinal))
                errors.Add($"Backend environment must be '{expectedBackendEnvironment}' for the selected deployment profile.");

            SolanaConfigSO chain = profile.SolanaConfig;
            SupabaseConfigSO backend = profile.SupabaseConfig;
            if (chain == null)
                errors.Add("Solana configuration is missing.");
            if (backend == null)
                errors.Add("Supabase configuration is missing.");

            if (chain != null)
            {
                string expectedCluster = ExpectedCluster(profile.OwnershipEnvironment);
                string cluster = NormalizeCluster(chain.ownershipCluster);
                if (!string.Equals(cluster, expectedCluster, StringComparison.Ordinal))
                    errors.Add($"Ownership cluster '{cluster}' does not match profile environment '{expectedCluster}'.");

                string expectedGenesis = ExpectedGenesisHash(profile.OwnershipEnvironment);
                if (!string.Equals(chain.expectedGenesisHash?.Trim(), expectedGenesis, StringComparison.Ordinal))
                    errors.Add("Configured genesis hash does not match the selected deployment environment.");

                if (requireComplete)
                {
                    RequireHttps(chain.rpcEndpoint, "Solana RPC endpoint", errors);
                    RequireHttps(chain.dasRpcEndpoint, "DAS RPC endpoint", errors);
                    RequireValue(chain.gameProgramId, "Game program ID", errors);
                    RequireValue(chain.coreMovesCollectionId, "Core Moves collection", errors);
                    RequireValue(chain.coreMovesUpdateAuthority, "Core Moves update authority", errors);
                    RequireValue(chain.coreArchivesCollectionId, "Core Archives collection", errors);
                    RequireValue(chain.coreCoversCollectionId, "Core Covers collection", errors);
                    RequireValue(chain.coreCosmeticsCollectionId, "Core Cosmetics collection", errors);
                }
            }

            if (backend != null && requireComplete)
            {
                RequireHttps(backend.supabaseUrl, "Supabase project URL", errors);
                RequireValue(backend.supabaseAnonKey, "Supabase public key", errors);
                RequireHttps(backend.ResolvedEdgeFunctionBaseUrl, "Supabase Edge Function URL", errors);
            }

            if (requireComplete)
            {
                RequireHttps(profile.OwnershipMetadataBaseUrl, "Ownership metadata base URL", errors);
                if (backend != null &&
                    Uri.TryCreate(backend.supabaseUrl, UriKind.Absolute, out Uri backendUri) &&
                    Uri.TryCreate(profile.OwnershipMetadataBaseUrl, UriKind.Absolute, out Uri metadataUri) &&
                    !string.Equals(backendUri.Host, metadataUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Ownership metadata and Supabase project hosts must match for environment isolation.");
                }
            }

            if (profile.OwnershipEnvironment == OwnershipDeploymentEnvironment.MainnetSmoke &&
                profile.AllowDevelopmentTools)
            {
                errors.Add("Mainnet Smoke must not expose development grant tools.");
            }

            return errors;
        }

        public static string NormalizeCluster(string value)
        {
            string normalized = value?.Trim().ToLowerInvariant();
            return normalized == "mainnet" ? "mainnet-beta" : normalized ?? "";
        }

        private static void RequireValue(string value, string label, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(value))
                errors.Add(label + " is empty.");
        }

        private static void RequireHttps(string value, string label, ICollection<string> errors)
        {
            if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(uri.Host))
            {
                errors.Add(label + " must be an absolute HTTPS URL.");
            }
        }
    }
}
