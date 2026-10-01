using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Runtime state of a single Chainling in combat.
    /// Pure C# â€” no MonoBehaviour dependency.
    /// </summary>
    [Serializable]
    public class ChainlingInstance
    {
        // â”€â”€ Identity â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Unique instance ID for this battle.</summary>
        public string instanceId;

        /// <summary>Reference to species definition ID.</summary>
        public string speciesId;

        /// <summary>Display name.</summary>
        public string displayName;

        /// <summary>Primary type.</summary>
        public ChainlingType primaryType;

        /// <summary>Secondary type (None if mono-type).</summary>
        public ChainlingType secondaryType;

        /// <summary>Current coating override type (None if no coating).</summary>
        public ChainlingType coatingType;

        /// <summary>Star count (0-5). Authoritative combat progression tier.</summary>
        public int stars;

        /// <summary>Rebirth count (0-5). Permanent progression applied on top of stars.</summary>
        public int rebirthCount;

        /// <summary>Evolution stage (1-based). Determines slot count.</summary>
        public int evolutionStage;

        /// <summary>Visual variant and cosmetic/effect IDs for presentation systems.</summary>
        public ChainlingVisualIdentity visualIdentity;

        /// <summary>True when this instance is mutated (wild mutation aura).</summary>
        public bool isMutated;

        /// <summary>True when this instance is entangled (type override + variant×type art).</summary>
        public bool isEntangled;

        /// <summary>Entangled type used for combat typing and baked visual lookup.</summary>
        public ChainlingType entangledType;

        /// <summary>Capture-profile mapping hint; never interpreted as a percent chance.</summary>
        public int legacyBaseCaptureRate = 45;

        /// <summary>True when <see cref="captureProfile"/> was stamped from species or a zone override.</summary>
        public bool hasCaptureProfile;

        /// <summary>Species/zone trading-trace kit. Ignored when <see cref="hasCaptureProfile"/> is false.</summary>
        public CaptureVolatilityProfile captureProfile = CaptureVolatilityProfile.Standard;

        /// <summary>Mutation-aura rarity rank 0–5 (Common–Mythic). 0 if not mutated.</summary>
        public int captureMutationRarityRank;

        // â”€â”€ Base Stats (from species + IVs) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public int baseMaxHP;
        public int baseMeleeAtk;
        public int baseMeleeDef;
        public int baseRangedAtk;
        public int baseRangedDef;
        public int baseSpeed;

        // â”€â”€ IVs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public int ivMaxHP;
        public int ivMeleeAtk;
        public int ivMeleeDef;
        public int ivRangedAtk;
        public int ivRangedDef;
        public int ivSpeed;

        /// <summary>
        /// Cycle pick allocation counts [MaxHP..Speed]. Applied as flats, not merged into IVs.
        /// </summary>
        public int[] rebirthIvBonuses;

        // â”€â”€ Current Combat State â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public int currentHP;
        public int currentAP;
        public bool isAlive;

        // ── Display Vitals (HUD snapshot) ───────────────────────────────
        /// <summary>HP shown on the HUD. Updated by the presenter at impact time.</summary>
        public int displayHP;
        /// <summary>AP shown on the HUD. Updated by the presenter at impact time.</summary>
        public int displayAP;

        public bool HasPendingDisplayUpdate => displayHP != currentHP || displayAP != currentAP;

        public void SyncDisplayVitals()
        {
            displayHP = currentHP;
            displayAP = currentAP;
        }

        // â”€â”€ Stages (-6 to +6) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public int stageMeleeAtk;
        public int stageMeleeDef;
        public int stageRangedAtk;
        public int stageRangedDef;
        public int stageSpeed;
        public int stageAccuracy;
        public int stageEvasion;
        public int stageCritRate;

        // â”€â”€ Active Statuses â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public List<StatusInstance> statuses;

        /// <summary>
        /// Temporary mitigation sources (e.g., swap trigger mitigation buffs).
        /// Stacks multiplicatively per mitigation rules.
        /// </summary>
        public List<MitigationSource> mitigationSources;

        /// <summary>
        /// Temporary accuracy bonus sources (e.g., swap trigger accuracy buffs).
        /// Each source is a percent bonus and expires by turn timer.
        /// </summary>
        public List<AccuracySource> accuracySources;

        /// <summary>
        /// Active reactive AP windows granted by utility effects (e.g., Ice Wall).
        /// </summary>
        public List<ReactiveApWindow> reactiveApWindows;

        /// <summary>Event-driven move windows. Cleared on swap-out by resolver policy.</summary>
        public List<ArmedUtilityWindow> armedUtilityWindows;

        /// <summary>
        /// Active barriers (single-hit, damage-threshold). Separate from duration-based MitigationSource.
        /// </summary>
        public List<BarrierInstance> barriers;

        /// <summary>
        /// Flat MaxHP from equipped passive DUR MaxHP% modifiers (already percent-resolved and clamped).
        /// </summary>
        public int passiveMaxHpBonus;

        /// <summary>
        /// Optional battle ruleset for event stat mutators. Applied after Cycle progressed stats.
        /// </summary>
        public CombatRulesetSO battleRuleset;

        /// <summary>
        /// Damage redirect interceptor — the Chainling that will receive redirected damage
        /// for the next single-target attack on this Chainling. Null if no redirect active.
        /// </summary>
        public ChainlingInstance redirectInterceptor;

        /// <summary>Tracks whether AP donation has been used this turn.</summary>
        public bool apDonationUsedThisTurn;

        /// <summary>Status applications queued for the next BeginTurn before AP gain/action selection.</summary>
        public List<PendingStatusApplication> pendingTurnStartStatuses;

        /// <summary>Temporary AP surcharge consumed by the next successfully executed non-passive move.</summary>
        public int nextMoveApSurcharge;

        // â”€â”€ Equipped Moves â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public List<MoveInstance> moves;

        // â”€â”€ Slot Info â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public int maxSlots;

        // â”€â”€ Flags â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public bool isActive;  // Currently on the field
        public bool hasActedThisTurn;
        public bool skippingNextTurn;  // Offline/sleep skip
        public bool lostActionThisTurn; // Reserved skip flag (Slowed no longer sets this)

        // â”€â”€ Passive Trigger Tracking â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        /// <summary>Set of move instance IDs that have already triggered (for once-per-battle passives).</summary>
        public HashSet<string> triggeredPassiveIds;

        /// <summary>Connecting hits taken this turn. Immutable Ledger uses only the first.</summary>
        public int hitsTakenThisTurn;

        /// <summary>Once-per-turn passive keys. Cleared when the stamped turn changes.</summary>
        public int oncePerTurnStamp = -1;
        public List<string> oncePerTurnPassiveIds;

        public ChainlingInstance()
        {
            instanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            statuses = new List<StatusInstance>();
            moves = new List<MoveInstance>();
            mitigationSources = new List<MitigationSource>();
            accuracySources = new List<AccuracySource>();
            reactiveApWindows = new List<ReactiveApWindow>();
            armedUtilityWindows = new List<ArmedUtilityWindow>();
            barriers = new List<BarrierInstance>();
            pendingTurnStartStatuses = new List<PendingStatusApplication>();
            triggeredPassiveIds = new HashSet<string>();
            oncePerTurnPassiveIds = new List<string>();
            isAlive = true;
            primaryType = ChainlingType.None;
            secondaryType = ChainlingType.None;
            coatingType = ChainlingType.None;
            visualIdentity = ChainlingVisualIdentity.CreateDefault();
            rebirthIvBonuses = new int[CombatConstants.STAT_COUNT];
        }

        /// <summary>Returns true the first time this key is used on the given turn.</summary>
        public bool MarkOncePerTurn(string key, int turn)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            if (oncePerTurnPassiveIds == null)
                oncePerTurnPassiveIds = new List<string>();
            if (oncePerTurnStamp != turn)
            {
                oncePerTurnStamp = turn;
                oncePerTurnPassiveIds.Clear();
            }
            if (oncePerTurnPassiveIds.Contains(key))
                return false;
            oncePerTurnPassiveIds.Add(key);
            return true;
        }

        // â”€â”€ Computed Properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>Legacy always-1 multiplier. Combat uses Cycle flats.</summary>
        public float StatScaleMultiplier => 1f;

        /// <summary>Initiative tie-break after speed (higher wins).</summary>
        public int ProgressionTieBreakScore => ChainlingProgressionUtility.GetProgressionTieBreakScore(stars, rebirthCount);

        /// <summary>Effective MaxHP after Cycle flats.</summary>
        public int EffectiveMaxHP => ComputeEffectiveStat(CoreStat.MaxHP);

        /// <summary>Effective stat after base + IV + Cycle flats (before stages).</summary>
        public int GetBaseStat(CoreStat stat)
        {
            return ComputeEffectiveStat(stat);
        }

        /// <summary>Return a full deterministic computation trace for a stat.</summary>
        public StatBreakdown GetStatBreakdown(CoreStat stat)
        {
            int baseValue = GetBaseValue(stat);
            int ivValue = GetIvValue(stat);
            int basePlusIv = baseValue + ivValue;
            int cycleAuto = ChainlingProgressionUtility.GetCycleAutoFlat(stat, rebirthCount, baseValue);
            int cyclePick = ChainlingProgressionUtility.GetCyclePickFlat(stat, rebirthIvBonuses, baseValue);
            int preStage = ChainlingProgressionUtility.ComputeProgressedStat(
                baseValue, ivValue, stat, rebirthCount, rebirthIvBonuses);
            int passiveMaxHp = stat == CoreStat.MaxHP ? Math.Max(0, passiveMaxHpBonus) : 0;
            int stageValue = stat == CoreStat.MaxHP ? 0 : GetStage(stat);
            float stageMult = stat == CoreStat.MaxHP ? 1f : StageMultiplier(stageValue);
            float statusMult = 1f; // Reserved for future status-based scalar modifiers.
            int finalInt = stat == CoreStat.MaxHP
                ? Math.Max(1, preStage + passiveMaxHp)
                : Math.Max(1, (int)(preStage * stageMult * statusMult));
            if (battleRuleset != null)
                finalInt = battleRuleset.ApplyStatMutator(stat, finalInt);

            return new StatBreakdown
            {
                stat = stat,
                baseValue = baseValue,
                ivValue = ivValue,
                basePlusIv = basePlusIv,
                stars = stars,
                rebirthCount = rebirthCount,
                cycleAutoFlat = cycleAuto,
                cyclePickFlat = cyclePick,
                passiveMaxHpBonus = passiveMaxHp,
                progressionMultiplier = 1f,
                stageMultiplier = stageMult,
                stageValue = stageValue,
                statusMultiplier = statusMult,
                finalInt = finalInt,
            };
        }

        public StatsSnapshot GetAllEffectiveStats()
        {
            return new StatsSnapshot
            {
                maxHP = GetStatBreakdown(CoreStat.MaxHP),
                meleeAtk = GetStatBreakdown(CoreStat.MeleeAtk),
                meleeDef = GetStatBreakdown(CoreStat.MeleeDef),
                rangedAtk = GetStatBreakdown(CoreStat.RangedAtk),
                rangedDef = GetStatBreakdown(CoreStat.RangedDef),
                speed = GetStatBreakdown(CoreStat.Speed),
            };
        }

        /// <summary>Get the current stage for a stat.</summary>
        public int GetStage(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MeleeAtk:  return stageMeleeAtk;
                case CoreStat.MeleeDef:  return stageMeleeDef;
                case CoreStat.RangedAtk: return stageRangedAtk;
                case CoreStat.RangedDef: return stageRangedDef;
                case CoreStat.Speed:     return stageSpeed;
                default: return 0; // MaxHP has no stages
            }
        }

        /// <summary>Set a stage value, clamped to resolver config stage bounds.</summary>
        public void SetStage(CoreStat stat, int value)
        {
            CombatResolverConfigSO config = BalanceConfigRuntime.ResolverConfig;
            value = Math.Max(config.stageMin, Math.Min(config.stageMax, value));
            switch (stat)
            {
                case CoreStat.MeleeAtk:  stageMeleeAtk = value; break;
                case CoreStat.MeleeDef:  stageMeleeDef = value; break;
                case CoreStat.RangedAtk: stageRangedAtk = value; break;
                case CoreStat.RangedDef: stageRangedDef = value; break;
                case CoreStat.Speed:     stageSpeed = value; break;
            }
        }

        public int GetCombatStage(CombatStageStat stat)
        {
            switch (stat)
            {
                case CombatStageStat.MeleeAtk: return stageMeleeAtk;
                case CombatStageStat.MeleeDef: return stageMeleeDef;
                case CombatStageStat.RangedAtk: return stageRangedAtk;
                case CombatStageStat.RangedDef: return stageRangedDef;
                case CombatStageStat.Speed: return stageSpeed;
                case CombatStageStat.Accuracy: return stageAccuracy;
                case CombatStageStat.Evasion: return stageEvasion;
                case CombatStageStat.CritRate: return stageCritRate;
                default: return 0;
            }
        }

        public void SetCombatStage(CombatStageStat stat, int value)
        {
            CombatResolverConfigSO config = BalanceConfigRuntime.ResolverConfig;
            value = Math.Max(config.stageMin, Math.Min(config.stageMax, value));
            switch (stat)
            {
                case CombatStageStat.MeleeAtk: stageMeleeAtk = value; break;
                case CombatStageStat.MeleeDef: stageMeleeDef = value; break;
                case CombatStageStat.RangedAtk: stageRangedAtk = value; break;
                case CombatStageStat.RangedDef: stageRangedDef = value; break;
                case CombatStageStat.Speed: stageSpeed = value; break;
                case CombatStageStat.Accuracy: stageAccuracy = value; break;
                case CombatStageStat.Evasion: stageEvasion = value; break;
                case CombatStageStat.CritRate: stageCritRate = value; break;
            }
        }

        public void ClearArmedUtilityWindows()
        {
            armedUtilityWindows?.Clear();
        }

        /// <summary>
        /// Stage multiplier per 02B Â§6.
        /// Positive: (2 + stage) / 2
        /// Negative: 2 / (2 + abs(stage))
        /// </summary>
        public static float StageMultiplier(int stage)
        {
            if (stage >= 0)
                return (2f + stage) / 2f;
            else
                return 2f / (2f + Math.Abs(stage));
        }

        /// <summary>
        /// Get the effective stat value (after progression scaling, IVs, and stage multiplier).
        /// MaxHP is NOT affected by stages (02B Â§1).
        /// </summary>
        public int GetEffectiveStat(CoreStat stat)
        {
            return GetStatBreakdown(stat).finalInt;
        }

        /// <summary>
        /// Get the types currently active for chemistry resolution.
        /// Coating overrides to mono-type per Spec 01 Â§4.
        /// </summary>
        public ChainlingType[] GetResolvedTypes()
        {
            if (coatingType != ChainlingType.None)
                return new[] { coatingType };

            if (secondaryType != ChainlingType.None)
                return new[] { primaryType, secondaryType };

            return new[] { primaryType };
        }

        /// <summary>Reset all stages to 0.</summary>
        public void ResetStages()
        {
            stageMeleeAtk = 0;
            stageMeleeDef = 0;
            stageRangedAtk = 0;
            stageRangedDef = 0;
            stageSpeed = 0;
            stageAccuracy = 0;
            stageEvasion = 0;
            stageCritRate = 0;
        }

        /// <summary>Clamp AP to [0, AP_CAP].</summary>
        public void ClampAP()
        {
            currentAP = Math.Max(0, Math.Min(BalanceConfigRuntime.ResolverConfig.apCap, currentAP));
        }

        /// <summary>
        /// Add a temporary mitigation source for this Chainling.
        /// Backward-compatible overload: no hit budget and no stack key.
        /// </summary>
        public void AddMitigationSource(float mitigation, int turnsRemaining, string sourceTag)
        {
            AddMitigationSource(mitigation, turnsRemaining, -1, sourceTag, string.Empty, 0, UtilityContactFilter.Any);
        }

        /// <summary>
        /// Add or refresh a temporary mitigation source.
        /// turnsRemaining and hitsRemaining are independent expiry budgets.
        /// A non-empty stackKey refreshes an existing entry instead of stacking duplicates.
        /// hitsRemaining: -1 = unlimited hit budget, >0 = consume per damaging hit.
        /// </summary>
        public void AddMitigationSource(float mitigation, int turnsRemaining, int hitsRemaining, string sourceTag, string stackKey)
        {
            AddMitigationSource(mitigation, turnsRemaining, hitsRemaining, sourceTag, stackKey, attackerApDrainOnHit: 0, UtilityContactFilter.Any);
        }

        public void AddMitigationSource(
            float mitigation,
            int turnsRemaining,
            int hitsRemaining,
            string sourceTag,
            string stackKey,
            int attackerApDrainOnHit,
            UtilityContactFilter contactFilter = UtilityContactFilter.Any)
        {
            if (turnsRemaining <= 0)
                return;

            float sourceCap = BalanceConfigRuntime.ResolverConfig.mitigationSingleSourceCap;
            mitigation = Math.Max(0f, Math.Min(sourceCap, mitigation));
            if (mitigation <= 0f)
                return;

            if (hitsRemaining == 0)
                return;

            if (mitigationSources == null)
                mitigationSources = new List<MitigationSource>();

            string normalizedKey = string.IsNullOrWhiteSpace(stackKey) ? string.Empty : stackKey.Trim();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                for (int i = 0; i < mitigationSources.Count; i++)
                {
                    MitigationSource source = mitigationSources[i];
                    if (source == null)
                        continue;
                    if (!string.Equals(source.stackKey, normalizedKey, StringComparison.Ordinal))
                        continue;

                    source.mitigation = mitigation;
                    source.turnsRemaining = turnsRemaining;
                    source.hitsRemaining = hitsRemaining;
                    source.sourceTag = sourceTag ?? string.Empty;
                    source.stackKey = normalizedKey;
                    source.attackerApDrainOnHit = Math.Max(0, attackerApDrainOnHit);
                    source.contactFilter = contactFilter;
                    return;
                }
            }

            mitigationSources.Add(new MitigationSource
            {
                mitigation = mitigation,
                turnsRemaining = turnsRemaining,
                hitsRemaining = hitsRemaining,
                sourceTag = sourceTag ?? string.Empty,
                stackKey = normalizedKey,
                attackerApDrainOnHit = Math.Max(0, attackerApDrainOnHit),
                contactFilter = contactFilter,
            });
        }

        /// <summary>
        /// Get total active mitigation after multiplicative stacking.
        /// </summary>
        public float GetTotalMitigation()
        {
            return GetTotalMitigation(ContactType.None);
        }

        public float GetTotalMitigation(ContactType contactType)
        {
            if (mitigationSources == null || mitigationSources.Count == 0)
                return 0f;

            float sourceCap = BalanceConfigRuntime.ResolverConfig.mitigationSingleSourceCap;
            float surviving = 1f;
            for (int i = 0; i < mitigationSources.Count; i++)
            {
                MitigationSource source = mitigationSources[i];
                if (!IsMitigationSourceActive(source) || !AppliesToContact(source, contactType))
                    continue;

                float perSource = Math.Max(0f, Math.Min(sourceCap, source.mitigation));
                surviving *= (1f - perSource);
            }

            return Math.Max(0f, 1f - surviving);
        }

        /// <summary>
        /// Consume one mitigation hit budget from each active hit-limited source.
        /// Returns number of sources removed after hit-budget exhaustion.
        /// </summary>
        public int ConsumeMitigationHitBudgets()
        {
            return ConsumeMitigationHitBudgets(ContactType.None);
        }

        public int ConsumeMitigationHitBudgets(ContactType contactType)
        {
            if (mitigationSources == null || mitigationSources.Count == 0)
                return 0;

            int removed = 0;
            for (int i = mitigationSources.Count - 1; i >= 0; i--)
            {
                MitigationSource source = mitigationSources[i];
                if (source == null)
                {
                    mitigationSources.RemoveAt(i);
                    removed++;
                    continue;
                }

                if (!IsMitigationSourceActive(source))
                {
                    mitigationSources.RemoveAt(i);
                    removed++;
                    continue;
                }

                if (source.hitsRemaining > 0 && AppliesToContact(source, contactType))
                {
                    source.hitsRemaining--;
                    if (!IsMitigationSourceActive(source))
                    {
                        mitigationSources.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        private static bool AppliesToContact(MitigationSource source, ContactType contactType)
        {
            if (source == null || source.contactFilter == UtilityContactFilter.Any)
                return true;
            if (source.contactFilter == UtilityContactFilter.Contact)
                return contactType == ContactType.Contact;
            if (source.contactFilter == UtilityContactFilter.Projectile)
                return contactType == ContactType.Projectile;
            return contactType == ContactType.None;
        }

        /// <summary>
        /// Tick temporary mitigation durations and remove expired entries.
        /// Returns number of removed sources.
        /// </summary>
        public int TickMitigationSources()
        {
            if (mitigationSources == null || mitigationSources.Count == 0)
                return 0;

            int removed = 0;
            for (int i = mitigationSources.Count - 1; i >= 0; i--)
            {
                MitigationSource source = mitigationSources[i];
                if (source == null)
                {
                    mitigationSources.RemoveAt(i);
                    removed++;
                    continue;
                }

                if (source.turnsRemaining > 0)
                    source.turnsRemaining--;

                if (!IsMitigationSourceActive(source))
                {
                    mitigationSources.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Add a temporary accuracy bonus source for this Chainling.
        /// </summary>
        public void AddAccuracySource(float bonusPercent, int turnsRemaining, string sourceTag)
        {
            if (turnsRemaining <= 0)
                return;

            bonusPercent = Math.Max(0f, Math.Min(1f, bonusPercent));
            if (bonusPercent <= 0f)
                return;

            if (accuracySources == null)
                accuracySources = new List<AccuracySource>();

            accuracySources.Add(new AccuracySource
            {
                bonusPercent = bonusPercent,
                turnsRemaining = turnsRemaining,
                sourceTag = sourceTag ?? string.Empty,
            });
        }

        /// <summary>
        /// Get temporary accuracy multiplier from active bonus sources.
        /// 1.0 means no temporary bonus.
        /// </summary>
        public float GetAccuracySourceMultiplier()
        {
            if (accuracySources == null || accuracySources.Count == 0)
                return 1f;

            float mult = 1f;
            for (int i = 0; i < accuracySources.Count; i++)
            {
                AccuracySource source = accuracySources[i];
                if (source == null || source.turnsRemaining <= 0)
                    continue;

                float bonus = Math.Max(0f, Math.Min(1f, source.bonusPercent));
                mult *= (1f + bonus);
            }

            return Math.Max(1f, mult);
        }

        /// <summary>
        /// Tick temporary accuracy bonus durations and remove expired entries.
        /// Returns number of removed sources.
        /// </summary>
        public int TickAccuracySources()
        {
            if (accuracySources == null || accuracySources.Count == 0)
                return 0;

            int removed = 0;
            for (int i = accuracySources.Count - 1; i >= 0; i--)
            {
                AccuracySource source = accuracySources[i];
                if (source == null)
                {
                    accuracySources.RemoveAt(i);
                    removed++;
                    continue;
                }

                source.turnsRemaining--;
                if (source.turnsRemaining <= 0)
                {
                    accuracySources.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// Add or refresh a reactive AP gain window.
        /// A non-empty stackKey refreshes an existing window instead of stacking duplicates.
        /// </summary>
        public void AddReactiveApWindow(int turnsRemaining, int apGain, bool requireSpeedHigher, string sourceTag, string stackKey)
        {
            if (turnsRemaining <= 0 || apGain <= 0)
                return;

            if (reactiveApWindows == null)
                reactiveApWindows = new List<ReactiveApWindow>();

            string normalizedKey = string.IsNullOrWhiteSpace(stackKey) ? string.Empty : stackKey.Trim();
            if (!string.IsNullOrEmpty(normalizedKey))
            {
                for (int i = 0; i < reactiveApWindows.Count; i++)
                {
                    ReactiveApWindow window = reactiveApWindows[i];
                    if (window == null)
                        continue;
                    if (!string.Equals(window.stackKey, normalizedKey, StringComparison.Ordinal))
                        continue;

                    window.turnsRemaining = turnsRemaining;
                    window.apGain = apGain;
                    window.requireSpeedHigher = requireSpeedHigher;
                    window.sourceTag = sourceTag ?? string.Empty;
                    window.stackKey = normalizedKey;
                    return;
                }
            }

            reactiveApWindows.Add(new ReactiveApWindow
            {
                turnsRemaining = turnsRemaining,
                apGain = apGain,
                requireSpeedHigher = requireSpeedHigher,
                stackKey = normalizedKey,
                sourceTag = sourceTag ?? string.Empty,
            });
        }

        /// <summary>
        /// Evaluate and apply reactive AP gain on damage taken.
        /// Returns total AP gained.
        /// </summary>
        public int GrantReactiveApOnDamage(ChainlingInstance attacker, bool apply = true)
        {
            if (reactiveApWindows == null || reactiveApWindows.Count == 0)
                return 0;

            int totalGain = 0;
            for (int i = 0; i < reactiveApWindows.Count; i++)
            {
                ReactiveApWindow window = reactiveApWindows[i];
                if (window == null || window.turnsRemaining <= 0)
                    continue;

                if (window.requireSpeedHigher)
                {
                    if (attacker == null)
                        continue;

                    int selfSpeed = GetEffectiveStat(CoreStat.Speed);
                    int attackerSpeed = attacker.GetEffectiveStat(CoreStat.Speed);
                    if (selfSpeed <= attackerSpeed)
                        continue;
                }

                totalGain += Math.Max(0, window.apGain);
            }

            if (apply && totalGain > 0)
                currentAP = Math.Min(BalanceConfigRuntime.ResolverConfig.apCap, currentAP + totalGain);

            return totalGain;
        }

        /// <summary>
        /// Tick reactive AP windows and remove expired entries.
        /// Returns number of removed windows.
        /// </summary>
        public int TickReactiveApWindows()
        {
            if (reactiveApWindows == null || reactiveApWindows.Count == 0)
                return 0;

            int removed = 0;
            for (int i = reactiveApWindows.Count - 1; i >= 0; i--)
            {
                ReactiveApWindow window = reactiveApWindows[i];
                if (window == null)
                {
                    reactiveApWindows.RemoveAt(i);
                    removed++;
                    continue;
                }

                window.turnsRemaining--;
                if (window.turnsRemaining <= 0)
                {
                    reactiveApWindows.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        // â”€â”€ Private Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private static bool IsMitigationSourceActive(MitigationSource source)
        {
            if (source == null)
                return false;
            if (source.turnsRemaining <= 0)
                return false;
            if (source.hitsRemaining == 0)
                return false;
            return source.mitigation > 0f;
        }

        // ── Barrier Helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Grant a barrier. When exclusive barriers/walls is enabled on the active resolver config,
        /// clears any existing barriers first (one wall/shield at a time).
        /// </summary>
        /// <returns>True when a prior barrier was replaced.</returns>
        public bool AddBarrier(
            BarrierType type,
            int hitsRemaining,
            int damageThreshold,
            int turnsRemaining,
            string sourceTag,
            float reflectPercent = 0f,
            int attackerApDrainOnHit = 0)
        {
            if (barriers == null)
                barriers = new List<BarrierInstance>();

            bool replaced = false;
            if (BalanceConfigRuntime.ResolverConfig.exclusiveBarriersAndWalls && barriers.Count > 0)
            {
                replaced = true;
                barriers.Clear();
            }

            barriers.Add(new BarrierInstance
            {
                barrierType = type,
                hitsRemaining = hitsRemaining,
                damageAbsorbed = 0,
                damageThreshold = damageThreshold,
                turnsRemaining = turnsRemaining,
                sourceTag = sourceTag ?? string.Empty,
                reflectPercent = reflectPercent,
                attackerApDrainOnHit = Math.Max(0, attackerApDrainOnHit),
            });
            return replaced;
        }

        /// <summary>
        /// Try to absorb incoming damage through barriers. Returns the damage that passes through.
        /// Also returns reflected damage and attacker AP drain when a barrier intercepts a hit.
        /// </summary>
        public int ApplyBarrierAbsorption(
            int incomingDamage,
            bool bypassBarriers,
            out int reflectedDamage,
            out int attackerApDrain)
        {
            reflectedDamage = 0;
            attackerApDrain = 0;
            if (barriers == null || barriers.Count == 0 || bypassBarriers)
                return incomingDamage;

            int remaining = incomingDamage;

            for (int i = barriers.Count - 1; i >= 0; i--)
            {
                if (remaining <= 0) break;
                var barrier = barriers[i];

                if (barrier.barrierType == BarrierType.SingleHit && barrier.hitsRemaining != 0)
                {
                    reflectedDamage += (int)(remaining * barrier.reflectPercent);
                    remaining = 0;
                    attackerApDrain = Math.Max(attackerApDrain, barrier.attackerApDrainOnHit);
                    if (barrier.hitsRemaining > 0)
                    {
                        barrier.hitsRemaining--;
                        if (barrier.hitsRemaining == 0)
                            barriers.RemoveAt(i);
                    }

                    break;
                }

                if (barrier.barrierType == BarrierType.DamageThreshold)
                {
                    int capacity = barrier.damageThreshold - barrier.damageAbsorbed;
                    if (capacity > 0)
                    {
                        int absorbed = Math.Min(remaining, capacity);
                        reflectedDamage += (int)(absorbed * barrier.reflectPercent);
                        barrier.damageAbsorbed += absorbed;
                        remaining -= absorbed;
                        attackerApDrain = Math.Max(attackerApDrain, barrier.attackerApDrainOnHit);
                        if (barrier.damageAbsorbed >= barrier.damageThreshold)
                            barriers.RemoveAt(i);
                    }
                }
            }

            return remaining;
        }

        /// <summary>
        /// Tick barrier durations at end of turn.
        /// Returns number of expired barriers.
        /// </summary>
        public int TickBarriers()
        {
            if (barriers == null || barriers.Count == 0)
                return 0;

            int removed = 0;
            for (int i = barriers.Count - 1; i >= 0; i--)
            {
                var barrier = barriers[i];
                if (barrier.turnsRemaining > 0)
                {
                    barrier.turnsRemaining--;
                    if (barrier.turnsRemaining <= 0)
                    {
                        barriers.RemoveAt(i);
                        removed++;
                    }
                }
            }
            return removed;
        }

        private int GetBaseValue(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MaxHP: return baseMaxHP;
                case CoreStat.MeleeAtk: return baseMeleeAtk;
                case CoreStat.MeleeDef: return baseMeleeDef;
                case CoreStat.RangedAtk: return baseRangedAtk;
                case CoreStat.RangedDef: return baseRangedDef;
                case CoreStat.Speed: return baseSpeed;
                default: return 0;
            }
        }

        private int GetIvValue(CoreStat stat)
        {
            switch (stat)
            {
                case CoreStat.MaxHP: return ivMaxHP;
                case CoreStat.MeleeAtk: return ivMeleeAtk;
                case CoreStat.MeleeDef: return ivMeleeDef;
                case CoreStat.RangedAtk: return ivRangedAtk;
                case CoreStat.RangedDef: return ivRangedDef;
                case CoreStat.Speed: return ivSpeed;
                default: return 0;
            }
        }

        /// <summary>Compute effective base stat with IVs, Cycle flats, and passive MaxHP bonus.</summary>
        private int ComputeEffectiveStat(CoreStat stat)
        {
            int progressed = ChainlingProgressionUtility.ComputeProgressedStat(
                GetBaseValue(stat),
                GetIvValue(stat),
                stat,
                rebirthCount,
                rebirthIvBonuses);
            if (stat == CoreStat.MaxHP)
                progressed = Math.Max(1, progressed + Math.Max(0, passiveMaxHpBonus));
            if (battleRuleset != null)
                progressed = battleRuleset.ApplyStatMutator(stat, progressed);
            return progressed;
        }
    }

    public enum BarrierType
    {
        SingleHit,
        DamageThreshold,
    }

    [Serializable]
    public class BarrierInstance
    {
        public BarrierType barrierType;
        public int hitsRemaining;
        public int damageAbsorbed;
        public int damageThreshold;
        public int turnsRemaining;
        public string sourceTag;
        public float reflectPercent;
        public int attackerApDrainOnHit;
        public UtilityContactFilter contactFilter = UtilityContactFilter.Any;
    }

    [Serializable]
    public class MitigationSource
    {
        public float mitigation;
        public int turnsRemaining;
        public int hitsRemaining = -1;
        public string stackKey;
        public string sourceTag;
        public int attackerApDrainOnHit;
        public UtilityContactFilter contactFilter = UtilityContactFilter.Any;
    }

    [Serializable]
    public class AccuracySource
    {
        public float bonusPercent;
        public int turnsRemaining;
        public string sourceTag;
    }

    [Serializable]
    public class ReactiveApWindow
    {
        public int turnsRemaining;
        public int apGain;
        public bool requireSpeedHigher;
        public string stackKey;
        public string sourceTag;
    }

    [Serializable]
    public class ArmedUtilityWindow
    {
        public ArmedWindowTrigger trigger;
        public int turnsRemaining;
        public int hitsRemaining;
        public int activateOnTurn;
        public int apDelta;
        public float healPercentMaxHP;
        public CombatStageStat stageStat;
        public int stageAmount;
        public int stageCap;
        public bool affectAllAllies;
        public float counterDamagePercentMaxHP;
        public int bankedAP;
        public string stackKey;
        public string sourceTag;
    }

    [Serializable]
    public class PendingStatusApplication
    {
        public StatusId statusId;
        public int duration;
        public string sourceId;
        public ChainlingType parameterType;
        public int applyOnTurn;
    }
}
