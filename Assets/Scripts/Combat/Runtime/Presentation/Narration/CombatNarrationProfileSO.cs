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
        [Min(1f)] [SerializeField] private float charactersPerSecond = 60f;
        [Min(0f)] [SerializeField] private float actionLeadSeconds = 0.22f;
        [Min(0f)] [SerializeField] private float primaryResolutionSeconds = 0.70f;
        [Min(0f)] [SerializeField] private float consequenceSeconds = 0.55f;

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
        [SerializeField] private string wallBrokenTemplate = "{wall} broke and absorbed {amount} damage.";
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
        [SerializeField] private string modifierTemplate = "A combat modifier changed the outcome.";
        [SerializeField] private string apBlockedTemplate = "{target} could not gain AP.";
        [SerializeField] private string apStolenTemplate = "{target} lost {amount} AP.";
        [SerializeField] private string wallReflectTemplate = "{actor} took {amount} reflected damage.";
        [SerializeField] private string wallApDrainTemplate = "{actor} lost {amount} AP.";
        [SerializeField] private string rechargingTemplate = "{move} began recharging.";
        [SerializeField] private string bonusActionTemplate = "{actor} gained another action.";

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
        internal string WallReflectTemplate => wallReflectTemplate;
        internal string WallApDrainTemplate => wallApDrainTemplate;
        internal string RechargingTemplate => rechargingTemplate;
        internal string BonusActionTemplate => bonusActionTemplate;
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
                    return CombatNarrationDelivery.OutcomeSummary;

                case CombatEventType.StatusExpired:
                case CombatEventType.StatusConsumed:
                case CombatEventType.MoveRechargeComplete:
                case CombatEventType.WallExpired:
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
