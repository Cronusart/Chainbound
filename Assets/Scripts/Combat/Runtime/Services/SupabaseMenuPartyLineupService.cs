using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseMenuPartyLineupService : MonoBehaviour, IMenuPartyLineupService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<MenuMutationResult> SavePartyLineupAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string[] orderedPartyChainlingBackendIds)
        {
            if (supabaseConfig == null)
                return MenuMutationResult.Failed("SupabaseConfigSO was not assigned.");

            var result = await SupabasePartyLineupMutation.ProfileSetPartyAsync(
                supabaseConfig,
                playerProfileId,
                walletAddress,
                authUserId,
                orderedPartyChainlingBackendIds);

            return result.Ok
                ? MenuMutationResult.Ok()
                : MenuMutationResult.Failed(result.ErrorMessage);
        }
    }
}
