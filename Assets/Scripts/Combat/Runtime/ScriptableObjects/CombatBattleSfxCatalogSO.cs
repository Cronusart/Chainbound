using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct CombatStatusSfxEntry
    {
        public StatusId statusId;
        [Tooltip("SoundEventSO played when this status is applied or refreshed.")]
        public UnityEngine.Object appliedSound;
        [Tooltip("SoundEventSO played when this status is cleansed.")]
        public UnityEngine.Object cleansedSound;
        [Tooltip("Optional SoundEventSO played when this status application is immune.")]
        public UnityEngine.Object immuneSound;
    }

    /// <summary>
    /// Data source for universal combat SFX. Author combat sounds on the SFX bus;
    /// UI sounds stay in CombatUiSfxPlayer, and ambience stays scene-authored by AmbienceZone.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatBattleSfxCatalog", menuName = "Chainbound/Combat/Audio/Battle SFX Catalog")]
    public class CombatBattleSfxCatalogSO : ScriptableObject
    {
        [Header("Turn / AP")]
        [SerializeField] private UnityEngine.Object turnStartApGrant;
        [SerializeField] private UnityEngine.Object apGain;
        [SerializeField] private UnityEngine.Object apDrain;
        [SerializeField] private UnityEngine.Object apBlocked;

        [Header("Outcomes")]
        [SerializeField] private UnityEngine.Object hit;
        [SerializeField] private UnityEngine.Object critHit;
        [SerializeField] private UnityEngine.Object block;
        [SerializeField] private UnityEngine.Object miss;
        [SerializeField] private UnityEngine.Object heal;
        [SerializeField] private UnityEngine.Object ko;

        [Header("Status Fallbacks")]
        [SerializeField] private UnityEngine.Object buffApplied;
        [SerializeField] private UnityEngine.Object debuffApplied;
        [SerializeField] private UnityEngine.Object statusImmune;
        [SerializeField] private UnityEngine.Object statusCleanse;

        [Header("Movement / Entry")]
        [SerializeField] private UnityEngine.Object glitchIn;
        [SerializeField] private UnityEngine.Object glitchOut;
        [SerializeField] private UnityEngine.Object swapIn;
        [SerializeField] private UnityEngine.Object swapOut;

        [Header("Post Battle")]
        [SerializeField] private UnityEngine.Object victoryStinger;
        [SerializeField] private UnityEngine.Object defeatStinger;
        [SerializeField] private UnityEngine.Object captureStinger;
        [SerializeField] private UnityEngine.Object xpShare;
        [Tooltip("Short blip played while a granted RAM total counts up.")]
        [SerializeField] private UnityEngine.Object ramGrantTick;
        [Tooltip("SoundEventSO played when a Chainling gains a star after XP share.")]
        [SerializeField] private UnityEngine.Object starUp;
        [SerializeField] private UnityEngine.Object rewardConfirm;

        [Header("Status Overrides")]
        [SerializeField] private List<CombatStatusSfxEntry> statusSfxEntries = new List<CombatStatusSfxEntry>();

        [Header("Wall Presentation")]
        [Tooltip("Material/source keyed wall lifecycle SFX catalog. The combat SFX orchestrator owns resolution and bridge playback.")]
        [SerializeField] private CombatWallSfxCatalogSO wallSfxCatalog;

        [Header("Crit Timing")]
        [Tooltip("Optional lead-in blip played before a critical primary outcome.")]
        [SerializeField] private SoundEventSO critWindupSound;

        [Header("Tuning")]
        [Min(0f)]
        [SerializeField] private float defaultImpactStaggerSeconds = 0.04f;
        [Min(0f)]
        [SerializeField] private float statusAfterImpactDelaySeconds = 0.08f;
        [Min(0f)]
        [SerializeField] private float critAccentDelaySeconds = 0.04f;
        [Min(0f)]
        [SerializeField] private float critWindupLeadSeconds = 0.06f;
        [Min(0f)]
        [SerializeField] private float koDelayAfterImpactSeconds = 0.10f;
        [Min(0f)]
        [SerializeField] private float koStaggerSeconds = 0.04f;
        [Min(1)]
        [SerializeField] private int maxSimultaneousOutcomeVoices = 4;
        [Range(0f, 1f)]
        [SerializeField] private float turnStartApVolumeScale = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] private float multiTargetVolumeScale = 0.82f;
        [Range(0f, 1f)]
        [Tooltip("When a move authors both outcome impactSound and Impact phaseStartSound, the phase accent is played at this scale so phone speakers don't distort. Outcome impact stays at full volume.")]
        [SerializeField] private float pairedImpactAccentVolumeScale = 0.6f;
        [SerializeField] private bool debugLogResolvedSequence;

        private Dictionary<StatusId, CombatStatusSfxEntry> _statusLookup;

        public float DefaultImpactStaggerSeconds => defaultImpactStaggerSeconds;
        public float StatusAfterImpactDelaySeconds => statusAfterImpactDelaySeconds;
        public float CritAccentDelaySeconds => critAccentDelaySeconds;
        public float CritWindupLeadSeconds => critWindupLeadSeconds;
        public float KoDelayAfterImpactSeconds => koDelayAfterImpactSeconds;
        public float KoStaggerSeconds => koStaggerSeconds;
        public int MaxSimultaneousOutcomeVoices => Mathf.Max(1, maxSimultaneousOutcomeVoices);
        public float TurnStartApVolumeScale => turnStartApVolumeScale;
        public float MultiTargetVolumeScale => multiTargetVolumeScale;
        public float PairedImpactAccentVolumeScale => pairedImpactAccentVolumeScale;
        public bool DebugLogResolvedSequence => debugLogResolvedSequence;
        public CombatWallSfxCatalogSO WallSfxCatalog => wallSfxCatalog;
        public SoundEventSO CritWindupSound => critWindupSound;

        /// <summary>
        /// Volume scale for Impact <c>phaseStartSound</c> when an outcome <c>impactSound</c> is also authored.
        /// Outcome impact remains at full volume; the phase cue is treated as an accent.
        /// </summary>
        public float ResolveImpactPhaseAccentVolumeScale(bool hasOutcomeImpactSound)
        {
            return hasOutcomeImpactSound ? Mathf.Clamp01(pairedImpactAccentVolumeScale) : 1f;
        }

        public UnityEngine.Object TurnStartApGrant => turnStartApGrant;
        public UnityEngine.Object Hit => hit;
        public UnityEngine.Object CritHit => critHit;
        public UnityEngine.Object Block => block;
        public UnityEngine.Object Miss => miss;
        public UnityEngine.Object Heal => heal;
        public UnityEngine.Object Ko => ko;
        public UnityEngine.Object GlitchIn => glitchIn;
        public UnityEngine.Object SwapIn => swapIn != null ? swapIn : glitchIn;
        public UnityEngine.Object SwapOut => swapOut != null ? swapOut : glitchOut;
        public UnityEngine.Object XpShare => xpShare;
        public UnityEngine.Object RamGrantTick => ramGrantTick != null ? ramGrantTick : apGain;
        public UnityEngine.Object StarUp => starUp;
        public UnityEngine.Object RewardConfirm => rewardConfirm;
        public UnityEngine.Object VictoryStinger => victoryStinger;
        public UnityEngine.Object DefeatStinger => defeatStinger;
        public UnityEngine.Object CaptureStinger => captureStinger;

        public void BuildLookup()
        {
            _statusLookup = new Dictionary<StatusId, CombatStatusSfxEntry>();
            if (statusSfxEntries == null)
                return;

            for (int i = 0; i < statusSfxEntries.Count; i++)
            {
                var entry = statusSfxEntries[i];
                if (entry.statusId == StatusId.None || _statusLookup.ContainsKey(entry.statusId))
                    continue;

                _statusLookup[entry.statusId] = entry;
            }
        }

        public UnityEngine.Object ResolveApFeedback(CombatEvent evt)
        {
            if (evt.eventType == CombatEventType.APGainBlocked)
                return apBlocked;

            if (evt.eventType == CombatEventType.APSpent ||
                (evt.eventType == CombatEventType.APStolen && evt.intValue < 0))
            {
                return apDrain;
            }

            return apGain;
        }

        public UnityEngine.Object ResolveDamageOutcome(bool isCrit)
        {
            if (isCrit && critHit != null)
                return critHit;

            return hit;
        }

        public UnityEngine.Object ResolveStatusSound(CombatEvent evt, StatusDefinitionSO statusDefinition)
        {
            CombatStatusSfxEntry entry;
            bool hasEntry = TryGetStatusEntry(evt.statusId, out entry);

            if (evt.eventType == CombatEventType.StatusImmune)
                return hasEntry && entry.immuneSound != null ? entry.immuneSound : statusImmune;

            if (evt.eventType == CombatEventType.StatusCleansed)
                return hasEntry && entry.cleansedSound != null ? entry.cleansedSound : statusCleanse;

            if (hasEntry && entry.appliedSound != null)
                return entry.appliedSound;

            if (statusDefinition != null)
            {
                if (statusDefinition.isBuff)
                    return buffApplied != null ? buffApplied : debuffApplied;
                if (statusDefinition.isDebuff)
                    return debuffApplied != null ? debuffApplied : buffApplied;
            }

            return buffApplied != null ? buffApplied : debuffApplied;
        }

        public UnityEngine.Object ResolvePostBattle(BattleResult result)
        {
            if (result == null)
                return null;

            BattleOutcomeKind kind = BattleOutcomePresentation.Resolve(result).Kind;
            switch (kind)
            {
                case BattleOutcomeKind.Capture:
                    return captureStinger != null ? captureStinger : victoryStinger;
                case BattleOutcomeKind.Defeat:
                case BattleOutcomeKind.Withdrawn:
                    return defeatStinger;
                case BattleOutcomeKind.Victory:
                case BattleOutcomeKind.Draw:
                    return victoryStinger;
                default:
                    return victoryStinger;
            }
        }

        public bool TryGetStatusEntry(StatusId statusId, out CombatStatusSfxEntry entry)
        {
            if (_statusLookup == null)
                BuildLookup();

            entry = default;
            return _statusLookup != null && _statusLookup.TryGetValue(statusId, out entry);
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            defaultImpactStaggerSeconds = Mathf.Max(0f, defaultImpactStaggerSeconds);
            statusAfterImpactDelaySeconds = Mathf.Max(0f, statusAfterImpactDelaySeconds);
            critAccentDelaySeconds = Mathf.Max(0f, critAccentDelaySeconds);
            critWindupLeadSeconds = Mathf.Max(0f, critWindupLeadSeconds);
            koDelayAfterImpactSeconds = Mathf.Max(0f, koDelayAfterImpactSeconds);
            koStaggerSeconds = Mathf.Max(0f, koStaggerSeconds);
            maxSimultaneousOutcomeVoices = Mathf.Max(1, maxSimultaneousOutcomeVoices);
            turnStartApVolumeScale = Mathf.Clamp01(turnStartApVolumeScale);
            multiTargetVolumeScale = Mathf.Clamp01(multiTargetVolumeScale);
            pairedImpactAccentVolumeScale = Mathf.Clamp01(pairedImpactAccentVolumeScale);
            BuildLookup();
        }
    }
}
