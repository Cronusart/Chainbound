using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseMenuVisualIdentityService : MonoBehaviour, IMenuVisualIdentityService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<VisualIdentityMutationResult> SaveVisualIdentityAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string chainlingBackendId,
            ChainlingVisualIdentity visualIdentity,
            IReadOnlyCollection<string> ownedCosmeticIds,
            PlayerRosterSnapshot rosterCacheToPatch,
            ChainlingVisualIdentityDatabaseSO identityDatabase = null,
            string speciesId = null,
            IReadOnlyDictionary<string, string> cosmeticAssetIdsByCosmeticId = null)
        {
            if (supabaseConfig == null)
                return VisualIdentityMutationResult.Failed("SupabaseConfigSO was not assigned.");

            var result = await SupabaseVisualIdentityMutation.ProfileSetVisualIdentityAsync(
                supabaseConfig,
                playerProfileId,
                walletAddress,
                authUserId,
                chainlingBackendId,
                visualIdentity,
                ownedCosmeticIds,
                rosterCacheToPatch,
                identityDatabase,
                speciesId,
                cosmeticAssetIdsByCosmeticId);

            return result.Ok
                ? VisualIdentityMutationResult.Ok(result.VisualIdentity)
                : VisualIdentityMutationResult.Failed(result.ErrorMessage);
        }
    }
}
