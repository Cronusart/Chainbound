using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Central configuration for Solana integration.
    /// Stores program IDs, RPC endpoint, confirmation strategy.
    /// </summary>
    [CreateAssetMenu(fileName = "SolanaConfig", menuName = "Chainbound/Platform/Solana Config")]
    public class SolanaConfigSO : ScriptableObject
    {
        [Header("RPC")]
        [Tooltip("Solana RPC endpoint URL (e.g., https://api.devnet.solana.com).")]
        public string rpcEndpoint = "https://api.devnet.solana.com";

        [Tooltip("WebSocket endpoint for subscriptions (optional).")]
        public string wsEndpoint = "wss://api.devnet.solana.com";

        [Header("Program IDs (base58)")]
        [Tooltip("Game program ID (handles profiles, chainlings, encounters).")]
        public string gameProgramId = "";

        [Tooltip("MagicBlock base-layer VRF program ID.")]
        public string vrfProgramId = "Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz";

        [Tooltip("MagicBlock base-layer oracle queue.")]
        public string vrfOracleQueueId = "Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh";

        [Tooltip("Metaplex Core program ID.")]
        public string metaplexCoreProgramId = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";

        [Header("Ownership Spine Trust")]
        [Tooltip("Solana cluster for wallet authorization and Ownership Spine transactions. Supported values are devnet and mainnet-beta.")]
        public string ownershipCluster = "devnet";

        [Tooltip("Expected getGenesisHash result for the configured RPC cluster. Wallet authorization and signing fail closed on mismatch.")]
        public string expectedGenesisHash = OwnershipDeploymentProfileRules.DevnetGenesisHash;

        [Tooltip("Trusted Metaplex Core collection address for Chainbound Stamped moves.")]
        public string coreMovesCollectionId = "";

        [Tooltip("Expected Chainbound program core_authority PDA for the Moves collection.")]
        public string coreMovesUpdateAuthority = "";

        [Tooltip("Supported cb.schema_version for verified Stamped move inventory.")]
        [Min(1)] public int ownershipSchemaVersion = 1;

        [Header("DAS / Core Cosmetics")]
        [Tooltip("DAS-enabled RPC endpoint used by server-side tooling. Unity production cosmetic ownership reads go through Supabase.")]
        public string dasRpcEndpoint = "";

        [Tooltip("Trusted Metaplex Core collection address for Chainbound Cosmetics.")]
        public string coreCosmeticsCollectionId = "";

        [Header("Confirmation Strategy")]
        [Tooltip("Default confirmation timeout in milliseconds.")]
        public int confirmationTimeoutMs = 45000;

        [Tooltip("Whether to wait for finalized commitment on critical transactions.")]
        public bool requireFinalizedForCritical = true;

        [Tooltip("Poll interval for transaction confirmation in milliseconds.")]
        public int confirmPollIntervalMs = 2000;

        [Header("Wallet Adapter")]
        [Tooltip("HTTPS identity URI owned by the game and displayed by MWA wallets.")]
        public string mwaIdentityUri = "";

        [Tooltip("Icon path relative to the MWA identity URI.")]
        public string mwaIconUri = "favicon.ico";

        [Tooltip("Game name displayed by the wallet during authorization.")]
        public string mwaIdentityName = "Chainbound";

        [Tooltip("In the Editor, use the browser-based wallet bridge (Phantom/Solflare) instead of the native adapter. " +
                 "Disable this to test the PSG1/production wallet adapter path from the Editor. " +
                 "Has no effect on production builds - the native mobile adapter is always used.")]
        public bool useDesktopBridgeInEditor = true;

        [Header("PDA Bootstrap")]
        [Tooltip("Automatically create/load player PDAs immediately after any wallet connection. Keep disabled for account login so wallet auth cannot be interrupted by transaction prompts.")]
        public bool autoBootstrapPdasOnWalletConnect = false;

        [Header("Demo / Debug")]
        [Tooltip("Use stub services instead of real Solana integration.")]
        public bool useStubServices = false;

        [Tooltip("Editor/tests only. Production builds always use the real MagicBlock VRF service.")]
        public bool useDeterministicVrfInEditor = true;

        [Tooltip("Log all RPC calls.")]
        public bool enableRpcLogging = true;

        private void OnValidate()
        {
            rpcEndpoint = TrimOrEmpty(rpcEndpoint);
            wsEndpoint = TrimOrEmpty(wsEndpoint);
            gameProgramId = TrimOrEmpty(gameProgramId);
            vrfProgramId = TrimOrEmpty(vrfProgramId);
            vrfOracleQueueId = TrimOrEmpty(vrfOracleQueueId);
            metaplexCoreProgramId = TrimOrEmpty(metaplexCoreProgramId);
            ownershipCluster = TrimOrEmpty(ownershipCluster);
            expectedGenesisHash = TrimOrEmpty(expectedGenesisHash);
            coreMovesCollectionId = TrimOrEmpty(coreMovesCollectionId);
            coreMovesUpdateAuthority = TrimOrEmpty(coreMovesUpdateAuthority);
            ownershipSchemaVersion = Mathf.Max(1, ownershipSchemaVersion);
            dasRpcEndpoint = TrimOrEmpty(dasRpcEndpoint);
            coreCosmeticsCollectionId = TrimOrEmpty(coreCosmeticsCollectionId);
            mwaIdentityUri = TrimOrEmpty(mwaIdentityUri);
            mwaIconUri = TrimOrEmpty(mwaIconUri);
            mwaIdentityName = TrimOrEmpty(mwaIdentityName);
        }

        private static string TrimOrEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }
    }
}
