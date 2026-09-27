using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public interface IBattleRewardPersistence
    {
        bool IsAvailable { get; }
        Task<bool> PersistRewardsAsync(BattleRewardSummary summary, string walletAddress);
        Task<bool> PersistOutcomeAsync(BattleOutcomeBundle outcome, string walletAddress);
    }
}
