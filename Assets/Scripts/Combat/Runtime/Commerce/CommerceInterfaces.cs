using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public interface IPurchaseSource
    {
        PurchaseSourceType SourceType { get; }
        BuildPlatform SourcePlatform { get; }
        bool IsAvailable { get; }
        Task<EntitlementGrantRequest> CreateGrantRequestAsync(string playerProfileId, string productId);
    }

    public interface IEntitlementGrantService
    {
        Task<EntitlementGrantResult> ValidateAndGrantAsync(EntitlementGrantRequest request);
    }

    public interface IRewardedAdProvider
    {
        AdRewardSourceType SourceType { get; }
        bool IsAvailable { get; }
        Task<RewardGrantRequest> ShowRewardedAdAsync(string playerProfileId, string placementId);
    }

    [CreateAssetMenu(fileName = "ProductCatalog", menuName = "Chainbound/Commerce/Product Catalog")]
    public class ProductCatalogSO : ScriptableObject
    {
        [SerializeField] private ProductCatalogEntry[] entries = new ProductCatalogEntry[0];

        public ProductCatalogEntry[] Entries => entries;

        public bool TryGetEntry(string productId, out ProductCatalogEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].productId == productId)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }
    }

    public static class EntitlementGrantMapper
    {
        public static EntitlementGrantRequest FromReward(RewardGrantRequest reward)
        {
            if (reward == null)
                return null;

            return new EntitlementGrantRequest
            {
                playerProfileId = reward.playerProfileId,
                sourceType = PurchaseSourceType.RewardedAd,
                sourcePlatform = reward.sourcePlatform,
                sourceTransactionId = reward.placementId,
                skuOrProductId = reward.rewardId,
                amount = reward.amount,
                currencyType = reward.currencyType,
                metadataJson = reward.metadataJson,
            };
        }
    }
}
