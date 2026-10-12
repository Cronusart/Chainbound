using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public interface ICombatSfxOrchestrator
    {
        void PlayTurnStartApGrant(IReadOnlyList<CombatEvent> events, Vector3? position = null);
        void PlayApFeedback(CombatEvent evt, Combat2ChainlingView targetView);
        void PlayOutcomeBatch(CombatSfxOutcomeBatch batch);
        void PlayWallCue(CombatWallSfxRequest request);
        void PlayStatusAfterImpact(CombatSfxOutcomeContext context, float delaySeconds);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayDamageOutcome(CombatEvent evt, Combat2ChainlingView targetView, bool isCrit);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayMiss(CombatEvent evt, Combat2ChainlingView targetView);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayBlock(CombatEvent evt, Combat2ChainlingView targetView);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayHeal(CombatEvent evt, Combat2ChainlingView targetView);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayStatus(CombatEvent evt, Combat2ChainlingView targetView, StatusDefinitionSO statusDefinition);
        [System.Obsolete("Use PlayOutcomeBatch; this wrapper is retained for compatibility.", false)]
        void PlayKo(CombatEvent evt, Combat2ChainlingView targetView);
        void PlaySpawn(Combat2ChainlingView view);
        void PlaySwapOut(CombatEvent evt, Combat2ChainlingView outgoingView);
        void PlaySwapIn(CombatEvent evt, Combat2ChainlingView incomingView);
        void PlayPostBattle(BattleResult result);
        void FadeOutPostBattleSting();
        void PlayXpShare(BattleRewardSummary summary);
        void PlayRamTick();
        void PlayStarUp(BattleRewardSummary summary);
        void PlayRewardApplied(BattleRewardSummary summary);
        void StopBattleMusic(float fadeSeconds = 0f);
    }
}
