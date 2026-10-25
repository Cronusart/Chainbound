using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class CombatNarrationEventRule
    {
        public CombatEventType eventType;
        public CombatNarrationDelivery delivery = CombatNarrationDelivery.OutcomeSummary;
    }

    [CreateAssetMenu(fileName = "CombatNarrationProfile", menuName = "Chainbound/Combat/Config/Combat Narration Profile")]
    public sealed class CombatNarrationProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string speakerLabel = "BATTLE LINK";

        [Header("Advance And Timing")]
        [SerializeField] private CombatNarrationAdvanceMode advanceMode = CombatNarrationAdvanceMode.AutoWithOptionalAdvance;
        [Min(1f)] [SerializeField] private float charactersPerSecond = 45f;
        [Min(0f)] [SerializeField] private float actionLeadSeconds = 0.4f;
        [Min(0f)] [SerializeField] private float primaryResolutionSeconds = 1.7f;
        [Min(0f)] [SerializeField] private float consequenceSeconds = 1.45f;
        [Tooltip("Full-text time before a damage, heal, mitigation, or passive page can be skipped.")]
        [Min(0f)] [SerializeField] private float effectPageSkipLockoutSeconds = 1.15f;

        [Header("Action Templates")]
        [SerializeField] private string moveTemplate = "{actor} used {move}!";
        [SerializeField] private string multiHitMoveTemplate = "{actor} used {move}...";
        [SerializeField] private string priorityMoveTemplate = "{actor} struck first with priority {move}!";
        [SerializeField] private string swapStartTemplate = "{actor} is switching out!";
        [SerializeField] private string actionFailedTemplate = "{actor} could not use {move}.";
        [SerializeField] private string passTemplate = "{actor} passed.";
        [SerializeField] private string genericActionTemplate = "{actor} acted.";

        [Header("Resolution Templates")]
        [SerializeField] private string damageTemplate = "{target} took {amount} damage.";
        [SerializeField] private string multiHitDamageTemplate = "{target} was hit {hits} times for {amount} damage!";
        [SerializeField] private string damageKoTemplate = "{target} took {amount} damage and was knocked out!";
        [SerializeField] private string multiHitDamageKoTemplate = "{target} was hit {hits} times for {amount} damage and was knocked out!";
        [SerializeField] private string criticalPrefix = "Critical hit! ";
        [SerializeField] private string healTemplate = "{target} recovered {amount} HP.";
        [SerializeField] private string missTemplate = "{actor}'s attack missed {target}.";
        [SerializeField] private string dodgeTemplate = "{target} dodged the attack.";
        [SerializeField] private string barrierTemplate = "{target}'s barrier blocked the attack.";
        [SerializeField] private string wallBlockedTemplate = "{wall} blocked all {amount} damage.";
        [SerializeField] private string wallMitigatedTemplate = "{wall} blocked {amount} damage.";
        [SerializeField] private string wallBrokenTemplate = "{actor} broke {target}'s {wall} (-{amount}).";
        [SerializeField] private string wallHeldTemplate = "{wall} held firm.";
        [SerializeField] private string koTemplate = "{target} was knocked out!";
        [SerializeField] private string swapResultTemplate = "{target} entered the battle.";

        [Header("Consequence Templates")]
        [SerializeField] private string statusAppliedTemplate = "{target} became {status}.";
        [SerializeField] private string statusRefreshedTemplate = "{target}'s {status} was refreshed.";
        [SerializeField] private string statusImmuneTemplate = "{target} resisted {status}.";
        [SerializeField] private string statusCleansedTemplate = "{target} was cleansed of {status}.";
        [SerializeField] private string statusExpiredTemplate = "{target}'s {status} ended.";
        [SerializeField] private string stageChangedTemplate = "{target}'s {stat} changed by {amount}.";
        [SerializeField] private string passiveTemplate = "{actor}'s passive activated.";
        [SerializeField] private string modifierTemplate = "{owner}'s {modifier} activated.";
        [SerializeField] private string apBlockedTemplate = "{target} could not gain AP.";
        [SerializeField] private string apStolenTemplate = "{target} stole {amount} AP.";
        [SerializeField] private string apStealTemplate = "{actor} stole {amount} AP from {target}.";
        [SerializeField] private string wallReflectTemplate = "{actor} took {amount} reflected damage.";
        [SerializeField] private string wallApDrainTemplate = "{actor} lost {amount} AP.";
        [SerializeField] private string rechargingTemplate = "{move} began recharging.";
        [SerializeField] private string bonusActionTemplate = "{actor} gained another action.";

        [Header("Modifier Templates")]
        [SerializeField] private string modifierHealTemplate = "{owner}'s {modifier} restored {amount} HP.";
        [SerializeField] private string modifierHealOtherTemplate = "{owner}'s {modifier} restored {amount} HP to {target}.";
        [SerializeField] private string modifierApGainTemplate = "{owner}'s {modifier} granted {amount} AP.";
        [SerializeField] private string modifierApGainOtherTemplate = "{owner}'s {modifier} granted {target} {amount} AP.";
        [SerializeField] private string modifierApDrainTemplate = "{owner}'s {modifier} drained {amount} AP from {target}.";
        [SerializeField] private string modifierStageRaisedTemplate = "{owner}'s {modifier} raised {target}'s {stat} by {amount}.";
        [SerializeField] private string modifierStageLoweredTemplate = "{owner}'s {modifier} lowered {target}'s {stat} by {amount}.";
        [SerializeField] private string modifierStatusTemplate = "{owner}'s {modifier} made {target} {status}.";
        [SerializeField] private string modifierCleanseTemplate = "{owner}'s {modifier} cleansed {target} of {status}.";
        [SerializeField] private string modifierDamageTemplate = "{owner}'s {modifier} dealt {amount} damage to {target}.";
        [SerializeField] private string modifierGuardTemplate = "{owner}'s {modifier} put up a guard for the rest of the turn.";
        [SerializeField] private string modifierGuardOtherTemplate = "{owner}'s {modifier} guarded {target} for the rest of the turn.";
        [SerializeField] private string modifierAccuracyTemplate = "{owner}'s {modifier} sharpened {target}'s aim.";
        [SerializeField] private string modifierBarrierHitsTemplate = "{owner}'s {modifier} raised a barrier. Blocks {amount} {hitWord}.";
        [SerializeField] private string modifierBarrierPoolTemplate = "{owner}'s {modifier} raised a barrier. Absorbs up to {amount} damage.";
        [SerializeField] private string modifierCastTemplate = "{owner}'s {modifier} cast {move}!";
        [SerializeField] private string modifierReflectTemplate = "{owner}'s {modifier} reflected the effect back to {target}.";
        [SerializeField] private string modifierShareTemplate = "{owner}'s {modifier} shared the buffs with {target}.";
        [SerializeField] private string modifierBonusActionTemplate = "{owner}'s {modifier} granted another action.";

        [Header("Battle Result Templates")]
        [SerializeField] private string victoryTemplate = "Victory! Your team won in {turns} turns.";
        [SerializeField] private string defeatTemplate = "Defeat. The opposing team won in {turns} turns.";
        [SerializeField] private string drawTemplate = "The battle ended in a draw after {turns} turns.";
        [SerializeField] private string fledTemplate = "Your team withdrew from battle.";

        [Header("Event Policy Overrides")]
        [SerializeField] private List<CombatNarrationEventRule> eventRules = new List<CombatNarrationEventRule>();

        public string SpeakerLabel => string.IsNullOrWhiteSpace(speakerLabel) ? "BATTLE LINK" : speakerLabel.Trim();
        public CombatNarrationAdvanceMode AdvanceMode => advanceMode;
        public float CharactersPerSecond => Mathf.Max(1f, charactersPerSecond);
        public float ActionLeadSeconds => Mathf.Max(0f, actionLeadSeconds);
        public float PrimaryResolutionSeconds => Mathf.Max(0f, primaryResolutionSeconds);
        public float ConsequenceSeconds => Mathf.Max(0f, consequenceSeconds);
        public float EffectPageSkipLockoutSeconds => Mathf.Max(0f, effectPageSkipLockoutSeconds);

        internal string MoveTemplate => moveTemplate;
        internal string MultiHitMoveTemplate => multiHitMoveTemplate;
        internal string PriorityMoveTemplate => priorityMoveTemplate;
        internal string SwapStartTemplate => swapStartTemplate;
        internal string ActionFailedTemplate => actionFailedTemplate;
        internal string PassTemplate => passTemplate;
        internal string GenericActionTemplate => genericActionTemplate;
        internal string DamageTemplate => damageTemplate;
        internal string MultiHitDamageTemplate => multiHitDamageTemplate;
        internal string DamageKoTemplate => damageKoTemplate;
        internal string MultiHitDamageKoTemplate => multiHitDamageKoTemplate;
        internal string CriticalPrefix => criticalPrefix;
        internal string HealTemplate => healTemplate;
        internal string MissTemplate => missTemplate;
        internal string DodgeTemplate => dodgeTemplate;
        internal string BarrierTemplate => barrierTemplate;
        internal string WallBlockedTemplate => wallBlockedTemplate;
        internal string WallMitigatedTemplate => wallMitigatedTemplate;
        internal string WallBrokenTemplate => wallBrokenTemplate;
        internal string WallHeldTemplate => wallHeldTemplate;
        internal string KoTemplate => koTemplate;
        internal string SwapResultTemplate => swapResultTemplate;
        internal string StatusAppliedTemplate => statusAppliedTemplate;
        internal string StatusRefreshedTemplate => statusRefreshedTemplate;
        internal string StatusImmuneTemplate => statusImmuneTemplate;
        internal string StatusCleansedTemplate => statusCleansedTemplate;
        internal string StatusExpiredTemplate => statusExpiredTemplate;
        internal string StageChangedTemplate => stageChangedTemplate;
        internal string PassiveTemplate => passiveTemplate;
        internal string ModifierTemplate => modifierTemplate;
        internal string ApBlockedTemplate => apBlockedTemplate;
        internal string ApStolenTemplate => apStolenTemplate;
        internal string ApStealTemplate => apStealTemplate;
        internal string WallReflectTemplate => wallReflectTemplate;
        internal string WallApDrainTemplate => wallApDrainTemplate;
        internal string RechargingTemplate => rechargingTemplate;
        internal string BonusActionTemplate => bonusActionTemplate;
        internal string ModifierHealTemplate => modifierHealTemplate;
        internal string ModifierHealOtherTemplate => modifierHealOtherTemplate;
        internal string ModifierApGainTemplate => modifierApGainTemplate;
        internal string ModifierApGainOtherTemplate => modifierApGainOtherTemplate;
        internal string ModifierApDrainTemplate => modifierApDrainTemplate;
        internal string ModifierStageRaisedTemplate => modifierStageRaisedTemplate;
        internal string ModifierStageLoweredTemplate => modifierStageLoweredTemplate;
        internal string ModifierStatusTemplate => modifierStatusTemplate;
        internal string ModifierCleanseTemplate => modifierCleanseTemplate;
        internal string ModifierDamageTemplate => modifierDamageTemplate;
        internal string ModifierGuardTemplate => modifierGuardTemplate;
        internal string ModifierGuardOtherTemplate => modifierGuardOtherTemplate;
        internal string ModifierAccuracyTemplate => modifierAccuracyTemplate;
        internal string ModifierBarrierHitsTemplate => modifierBarrierHitsTemplate;
        internal string ModifierBarrierPoolTemplate => modifierBarrierPoolTemplate;
        internal string ModifierCastTemplate => modifierCastTemplate;
        internal string ModifierReflectTemplate => modifierReflectTemplate;
        internal string ModifierShareTemplate => modifierShareTemplate;
        internal string ModifierBonusActionTemplate => modifierBonusActionTemplate;
        internal string VictoryTemplate => victoryTemplate;
        internal string DefeatTemplate => defeatTemplate;
        internal string DrawTemplate => drawTemplate;
        internal string FledTemplate => fledTemplate;

        public CombatNarrationDelivery ResolveDelivery(CombatEventType eventType)
        {
            for (int i = 0; eventRules != null && i < eventRules.Count; i++)
            {
                CombatNarrationEventRule rule = eventRules[i];
                if (rule != null && rule.eventType == eventType)
                    return rule.delivery;
            }

            return ResolveDefaultDelivery(eventType);
        }

        public static CombatNarrationDelivery ResolveDefaultDelivery(CombatEventType eventType)
        {
            switch (eventType)
            {
                case CombatEventType.MoveUsed:
                case CombatEventType.ActionFailed:
                case CombatEventType.SwapPerformed:
                case CombatEventType.ActionSelected:
                    return CombatNarrationDelivery.Live;

                case CombatEventType.DamageDealt:
                case CombatEventType.PassiveDamageDealt:
                case CombatEventType.HealApplied:
                case CombatEventType.HPDotDamage:
                case CombatEventType.HPRecoilDamage:
                case CombatEventType.Miss:
                case CombatEventType.Dodge:
                case CombatEventType.BarrierBlocked:
                case CombatEventType.WallExpired:
                case CombatEventType.BlockIgnoredDot:
                case CombatEventType.ChainlingKO:
                case CombatEventType.AutoSwapIn:
                case CombatEventType.StatusApplied:
                case CombatEventType.ChemistryStatusApplied:
                case CombatEventType.StatusRefreshed:
                case CombatEventType.StatusImmune:
                case CombatEventType.StatusCleansed:
                case CombatEventType.StageChanged:
                case CombatEventType.PassiveTriggered:
                case CombatEventType.ModifierTriggered:
                case CombatEventType.APGainBlocked:
                case CombatEventType.APStolen:
                case CombatEventType.MoveRecharging:
                case CombatEventType.BonusActionGranted:
                case CombatEventType.BuffBlocked:
                case CombatEventType.BossTell:
                    return CombatNarrationDelivery.OutcomeSummary;

                case CombatEventType.StatusExpired:
                case CombatEventType.StatusConsumed:
                case CombatEventType.MoveRechargeComplete:
                    return CombatNarrationDelivery.TranscriptOnly;

                case CombatEventType.HitCheck:
                case CombatEventType.TargetingPhaseStarted:
                case CombatEventType.TargetsSelected:
                case CombatEventType.TargetsResolved:
                case CombatEventType.InitiativeResolved:
                case CombatEventType.SpeedTieResolved:
                case CombatEventType.StatBreakdown:
                case CombatEventType.ValidationError:
                case CombatEventType.HPHealed:
                    return CombatNarrationDelivery.Silent;

                default:
                    return CombatNarrationDelivery.PopupOrHudOnly;
            }
        }
    }
}
