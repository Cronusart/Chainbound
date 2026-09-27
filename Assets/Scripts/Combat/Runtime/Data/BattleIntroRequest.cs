using System;

namespace Chainbound.Combat
{
    [Serializable]
    public struct BattleIntroRequest
    {
        public bool isPvP;
        public bool isBoss;
        public string opponentTeamName;
        public string messageText;
        public EncounterDifficulty difficulty;
        public int opponentActiveCount;
        public BattleIntroVfxProfileSO vfxProfile;
        public string leadOpponentSpeciesId;
        public string leadOpponentVisualVariantId;
        public string[] opponentSpeciesIds;
        public bool hasEntangledOpponent;
        public BattleIntroCopyConfigSO copyConfig;
        public BattleIntroCopySpan[] copySpans;

        /// <summary>
        /// When false, local intro panel ignores skip input for this play.
        /// </summary>
        public bool allowSkip;
    }
}
