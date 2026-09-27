using System;

namespace Chainbound.Combat
{
    [Serializable]
    public enum BattleMode
    {
        PvE = 0,
        PvP = 1,
    }

    public static class BattleModeUtility
    {
        public static BattleMode FromBattleType(BattleType battleType)
        {
            return battleType == BattleType.PvP ? BattleMode.PvP : BattleMode.PvE;
        }
    }
}
