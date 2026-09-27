namespace Chainbound.Combat
{
    public interface IBattleRewardCalculator
    {
        BattleRewardSummary Calculate(BattleResult result);
    }
}
