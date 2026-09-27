using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Hook point for post-battle reward routing.
    /// Implementations will handle experience, loot, NFT minting, etc.
    /// For now this is a stub â€” no reward logic exists yet.
    /// </summary>
    public interface ICombatRewardsRouter
    {
        /// <summary>
        /// Called when a battle ends. The implementation decides what happens next
        /// (transition to reward screen, grant XP, mint drops, etc.).
        /// </summary>
        void RouteToRewards(BattleResult result);
    }

    /// <summary>
    /// Optional pre-presentation phase. Reward routers use this to calculate one authoritative
    /// summary and begin asynchronous grants before the post-battle report reaches its rewards page.
    /// </summary>
    public interface ICombatRewardPreparation
    {
        PreparedBattleRewards PrepareRewards(BattleResult result);
    }

    public sealed class PreparedBattleRewards
    {
        public BattleResult result;
        public BattleRewardSummary summary;
        public Task progressionCompletion = Task.CompletedTask;
        public Task victoryKnownCompletion = Task.CompletedTask;

        public bool IsProgressionComplete => progressionCompletion == null || progressionCompletion.IsCompleted;
        public bool ProgressionFailed => progressionCompletion != null && progressionCompletion.IsFaulted;
        public bool IsVictoryKnownComplete => victoryKnownCompletion == null || victoryKnownCompletion.IsCompleted;
    }
}
