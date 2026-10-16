using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves type chemistry interactions when a damaging hit lands.
    /// Chemistry outputs are ONLY produced here (Spec 01 Â§5, 03A Â§5).
    /// </summary>
    public class ChemistryResolver
    {
        private readonly TypeChemistryMatrixSO _matrix;
        private readonly StatusCatalogSO _statusCatalog;
        private readonly CombatEventLog _log;
        private readonly bool _strictMode;
        private readonly CombatResolverConfigSO _resolverConfig;

        public ChemistryResolver(TypeChemistryMatrixSO matrix, StatusCatalogSO statusCatalog,
            CombatEventLog log, bool strictMode = true, CombatResolverConfigSO resolverConfig = null)
        {
            _matrix = matrix;
            _statusCatalog = statusCatalog;
            _log = log;
            _strictMode = strictMode;
            _resolverConfig = resolverConfig != null
                ? resolverConfig
                : BalanceConfigRuntime.ResolverConfig;
        }

        /// <summary>
        /// Resolve stackless chemistry for a single hit. Dual-type matches collapse
        /// to one Advantage, Neutral, or Resisted relation, and at most one reaction
        /// is applied. Callers may suppress repeat reaction application while still
        /// resolving the relation for subsequent hits in the same action.
        /// </summary>
        public ChemistryResult Resolve(
            ChainlingType attackType,
            ChainlingInstance attacker,
            ChainlingInstance defender,
            int turn,
            CombatRng rng,
            bool captureDetails,
            out ChemistryResolvedPayload forensicPayload,
            bool allowAttackerReaction = true,
            bool allowDefenderReaction = true)
        {
            var result = new ChemistryResult
            {
                advantageFactor = _resolverConfig.chemistryAdvantageFactor,
                disadvantageFactor = _resolverConfig.chemistryDisadvantageFactor,
            };
            if (defender == null)
            {
                forensicPayload = null;
                return result;
            }

            var defenderTypes = defender.GetResolvedTypes();
            forensicPayload = null;

            if (captureDetails)
            {
                var targetNames = new string[defenderTypes.Length];
                for (int i = 0; i < defenderTypes.Length; i++)
                    targetNames[i] = CombatLogTypeNames.GetTypeName(defenderTypes[i]);
                forensicPayload = new ChemistryResolvedPayload
                {
                    moveType = attackType,
                    moveTypeName = CombatLogTypeNames.GetTypeName(attackType),
                    coatingType = defender.coatingType,
                    targetTypes = defenderTypes,
                    targetTypeNames = targetNames,
                };
            }

            int relationScore = 0;
            bool hasSelectedReaction = false;
            ChemistryOutput selectedOutput = default;
            ChainlingType selectedDefenderType = ChainlingType.None;

            // Primary type is evaluated first, so it deterministically owns the
            // reaction when both defender types are weak to the attack type.
            for (int t = 0; t < defenderTypes.Length; t++)
            {
                ChainlingType defenderType = defenderTypes[t];
                if (_matrix != null && _matrix.TryGetReaction(attackType, defenderType, out ChemistryOutput forwardOutput))
                {
                    relationScore++;
                    if (!hasSelectedReaction)
                    {
                        selectedOutput = forwardOutput;
                        selectedDefenderType = defenderType;
                        hasSelectedReaction = true;
                    }
                    continue;
                }

                if (_matrix != null && _matrix.TryGetReaction(defenderType, attackType, out _))
                    relationScore--;
            }

            result.relation = relationScore > 0
                ? ChemistryRelation.Advantage
                : relationScore < 0
                    ? ChemistryRelation.Resisted
                    : ChemistryRelation.Neutral;

            if (result.relation == ChemistryRelation.Advantage && hasSelectedReaction)
            {
                bool allowed = selectedOutput.recipient == ChemistryRecipient.Attacker
                    ? allowAttackerReaction
                    : allowDefenderReaction;
                if (allowed)
                {
                    ChainlingInstance recipient = selectedOutput.recipient == ChemistryRecipient.Attacker
                        ? attacker
                        : defender;
                    ProcessOutput(
                        attackType,
                        selectedDefenderType,
                        selectedOutput,
                        recipient,
                        attacker != null ? attacker.instanceId : string.Empty,
                        turn,
                        rng,
                        forensicPayload,
                        ref result);
                }
                else if (forensicPayload != null)
                {
                    forensicPayload.reactions.Add(new ChemistryReactionDecisionPayload
                    {
                        attackType = attackType,
                        targetType = selectedDefenderType,
                        statusId = selectedOutput.statusId,
                        parameterType = selectedOutput.parameterType,
                        recipient = selectedOutput.recipient,
                        applied = false,
                        blockedReason = "OncePerActionLimit",
                    });
                }
            }

            if (forensicPayload != null)
            {
                forensicPayload.relation = result.relation;
                forensicPayload.chemAdv = result.chemAdvCount;
                forensicPayload.chemDis = result.chemDisCount;
                forensicPayload.exponent = result.NetChemistry;
                forensicPayload.chemistryMultiplier = result.DamageMultiplier;
            }

            return result;
        }

        private void ProcessOutput(
            ChainlingType attackType,
            ChainlingType defenderType,
            ChemistryOutput output,
            ChainlingInstance recipient,
            string attackerId,
            int turn,
            CombatRng rng,
            ChemistryResolvedPayload forensicPayload,
            ref ChemistryResult result)
        {
            var decision = forensicPayload != null
                ? new ChemistryReactionDecisionPayload
                {
                    attackType = attackType,
                    targetType = defenderType,
                    statusId = output.statusId,
                    parameterType = output.parameterType,
                    recipient = output.recipient,
                }
                : null;

            if (recipient == null)
            {
                BlockDecision(decision, forensicPayload, "MissingRecipient");
                return;
            }

            if (_strictMode && !ChemistryStatusRules.IsRecipientValid(output.statusId, output.recipient))
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"CHEMISTRY RECIPIENT VIOLATION: {output.statusId} cannot target {output.recipient}.", turn);
                BlockDecision(decision, forensicPayload, "InvalidRecipient");
                return;
            }

            // Validate this is a chemistry-only status
            if (_strictMode && !_statusCatalog.IsChemistryOnly(output.statusId))
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"CHEMISTRY VIOLATION: {output.statusId} is not ChemistryOnly!", turn);
                if (decision != null)
                {
                    decision.blockedReason = "NonChemistryStatusInMatrix";
                    forensicPayload.reactions.Add(decision);
                }
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = recipient.displayName,
                        statusId = output.statusId.ToString(),
                        origin = StatusOrigin.ChemistryOnly.ToString(),
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "NonChemistryStatusInMatrix",
                        sourceId = attackerId,
                    },
                    attackerId,
                    new List<string> { recipient.instanceId },
                    turn);
                return;
            }

            var statusDef = _statusCatalog.Get(output.statusId);
            if (statusDef == null)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"Unknown status in chemistry matrix: {output.statusId}", turn);
                if (decision != null)
                {
                    decision.blockedReason = "UnknownStatus";
                    forensicPayload.reactions.Add(decision);
                }
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = recipient.displayName,
                        statusId = output.statusId.ToString(),
                        origin = StatusOrigin.ChemistryOnly.ToString(),
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "UnknownStatus",
                        sourceId = attackerId,
                    },
                    attackerId,
                    new List<string> { recipient.instanceId },
                    turn);
                return;
            }

            if (statusDef.isDebuff && FindActiveStatus(recipient, StatusId.EncryptedSeal) != null)
            {
                _log.LogStatus(turn, CombatEventType.StatusImmune,
                    recipient.instanceId, output.statusId, 0, attackerId);
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = recipient.displayName,
                        statusId = output.statusId.ToString(),
                        origin = StatusOrigin.ChemistryOnly.ToString(),
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "EncryptedSeal",
                        sourceId = attackerId,
                    },
                    attackerId,
                    new List<string> { recipient.instanceId },
                    turn);
                BlockDecision(decision, forensicPayload, "EncryptedSeal");
                return;
            }

            // Every matrix cell applies exactly one reaction.
            if (output.statusId == StatusId.TypeCoating || output.statusId == StatusId.HypedTypeCoating)
            {
                ApplyCoating(recipient, output.parameterType, statusDef.defaultDuration, turn, attackerId);
                MarkApplied(decision, forensicPayload, output.recipient, ref result);
                return;
            }

            if (statusDef.statusClass == StatusClass.Instant)
            {
                ApplyInstantChemistry(output.statusId, recipient, statusDef, turn, attackerId);
                MarkApplied(decision, forensicPayload, output.recipient, ref result);
                return;
            }

            if (statusDef.statusClass == StatusClass.Counter)
            {
                ApplyCounterChemistry(output.statusId, recipient, statusDef, turn, attackerId, decision);
                MarkApplied(decision, forensicPayload, output.recipient, ref result);
                return;
            }

            if (output.statusId == StatusId.Rugged)
            {
                StatusInstance rugged = FindActiveStatus(recipient, StatusId.Rugged);
                if (rugged != null)
                {
                    int beforeCounter = Math.Max(1, rugged.counterValue);
                    rugged.turnsRemaining = Math.Max(rugged.turnsRemaining, statusDef.defaultDuration);
                    if (decision != null)
                    {
                        decision.counterBefore = beforeCounter;
                        decision.counterAfter = beforeCounter;
                    }
                    _log.LogStatus(turn, CombatEventType.StatusRefreshed,
                        recipient.instanceId, output.statusId, rugged.turnsRemaining, attackerId);
                }
                else
                {
                    var newRugged = new StatusInstance(output.statusId, statusDef.statusClass,
                        StatusOrigin.ChemistryOnly, statusDef.defaultDuration, attackerId)
                    {
                        counterValue = 1,
                    };
                    recipient.statuses.Add(newRugged);
                    if (decision != null)
                    {
                        decision.counterBefore = 0;
                        decision.counterAfter = 1;
                    }
                    _log.LogStatus(turn, CombatEventType.ChemistryStatusApplied,
                        recipient.instanceId, output.statusId, statusDef.defaultDuration, attackerId);
                }
                MarkApplied(decision, forensicPayload, output.recipient, ref result);
                return;
            }

            var instance = new StatusInstance(
                output.statusId,
                statusDef.statusClass,
                StatusOrigin.ChemistryOnly,
                statusDef.defaultDuration,
                attackerId)
            {
                parameterType = output.parameterType,
            };
            if (StatusInstance.IsNextActionStatus(output.statusId))
                instance.HoldThroughGrantTurn(turn);

            bool refreshed = false;
            for (int s = 0; s < recipient.statuses.Count; s++)
            {
                if (recipient.statuses[s].statusId == output.statusId && !recipient.statuses[s].IsExpired)
                {
                    int before = recipient.statuses[s].turnsRemaining;
                    recipient.statuses[s].turnsRemaining = statusDef.defaultDuration;
                    if (StatusInstance.IsNextActionStatus(output.statusId))
                        recipient.statuses[s].HoldThroughGrantTurn(turn);
                    refreshed = true;
                    _log.LogStatus(turn, CombatEventType.StatusRefreshed,
                        recipient.instanceId, output.statusId, statusDef.defaultDuration, attackerId);
                    _log.LogForensic(
                        CombatForensicEventType.StatusRefreshed,
                        new StatusEventPayload
                        {
                            targetDisplayName = recipient.displayName,
                            statusId = output.statusId.ToString(),
                            origin = StatusOrigin.ChemistryOnly.ToString(),
                            durationBefore = before,
                            durationAfter = statusDef.defaultDuration,
                            blockReason = string.Empty,
                            sourceId = attackerId,
                        },
                        attackerId,
                        new List<string> { recipient.instanceId },
                        turn);
                    break;
                }
            }

            if (!refreshed)
            {
                recipient.statuses.Add(instance);
                _log.LogStatus(turn, CombatEventType.ChemistryStatusApplied,
                    recipient.instanceId, output.statusId, statusDef.defaultDuration, attackerId);
                _log.LogForensic(
                    CombatForensicEventType.StatusApplied,
                    new StatusEventPayload
                    {
                        targetDisplayName = recipient.displayName,
                        statusId = output.statusId.ToString(),
                        origin = StatusOrigin.ChemistryOnly.ToString(),
                        durationBefore = 0,
                        durationAfter = statusDef.defaultDuration,
                        blockReason = string.Empty,
                        sourceId = attackerId,
                    },
                    attackerId,
                    new List<string> { recipient.instanceId },
                    turn);

                if (statusDef.stageEffects != null)
                {
                    for (int e = 0; e < statusDef.stageEffects.Length; e++)
                    {
                        var eff = statusDef.stageEffects[e];
                        if (!eff.applyOnce)
                            continue;
                        int appliedDelta = instance.ApplyOwnedStage(recipient, eff.stat, eff.stages);
                        if (appliedDelta != 0)
                        {
                            _log.LogStageChange(turn, recipient.instanceId, eff.stat,
                                appliedDelta, recipient.GetStage(eff.stat));
                        }
                    }
                }
            }

            if (output.statusId == StatusId.Firewall && !refreshed)
            {
                recipient.AddMitigationSource(
                    _resolverConfig.firewallProjectileMitigation,
                    1,
                    1,
                    "Chemistry.Firewall",
                    "Chemistry.Firewall",
                    0,
                    UtilityContactFilter.Projectile);
            }

            if (output.statusId == StatusId.Slowed && !refreshed)
            {
                int oldSpeed = recipient.GetStage(CoreStat.Speed);
                int requested = _resolverConfig.slowedSpeedStages;
                recipient.SetStage(CoreStat.Speed, oldSpeed + requested);
                instance.hasTemporaryStageDelta = true;
                instance.temporaryStageStat = CoreStat.Speed;
                instance.temporaryStageDelta = recipient.GetStage(CoreStat.Speed) - oldSpeed;
                _log.LogStageChange(turn, recipient.instanceId, CoreStat.Speed,
                    recipient.GetStage(CoreStat.Speed) - oldSpeed, recipient.GetStage(CoreStat.Speed));
            }

            MarkApplied(decision, forensicPayload, output.recipient, ref result);
        }

        private static void BlockDecision(
            ChemistryReactionDecisionPayload decision,
            ChemistryResolvedPayload forensicPayload,
            string reason)
        {
            if (decision == null || forensicPayload == null)
                return;
            decision.applied = false;
            decision.blockedReason = reason;
            forensicPayload.reactions.Add(decision);
        }

        private static void MarkApplied(
            ChemistryReactionDecisionPayload decision,
            ChemistryResolvedPayload forensicPayload,
            ChemistryRecipient recipient,
            ref ChemistryResult result)
        {
            if (recipient == ChemistryRecipient.Attacker)
                result.attackerReactionApplied = true;
            else
                result.defenderReactionApplied = true;

            if (decision == null || forensicPayload == null)
                return;
            decision.applied = true;
            forensicPayload.reactions.Add(decision);
        }

        private void ApplyCoating(ChainlingInstance defender, ChainlingType coatType,
            int duration, int turn, string sourceId)
        {
            defender.coatingType = coatType;

            // Remove existing coating status, add new one
            defender.statuses.RemoveAll(s =>
                s.statusId == StatusId.TypeCoating || s.statusId == StatusId.HypedTypeCoating);

            defender.statuses.Add(new StatusInstance(
                StatusId.TypeCoating, StatusClass.Duration, StatusOrigin.ChemistryOnly, duration, sourceId)
            {
                parameterType = coatType
            });

            _log.LogMessage(CombatEventType.CoatingApplied,
                $"{defender.instanceId} coated to {coatType}", turn);
        }

        private void ApplyInstantChemistry(StatusId id, ChainlingInstance defender,
            StatusDefinitionSO def, int turn, string sourceId)
        {
            switch (id)
            {
                case StatusId.Shattered:
                    int requestedHpLoss = Math.Max(1, (int)(defender.currentHP * _resolverConfig.shatteredCurrentHpDamage));
                    int hpLoss = Math.Min(Math.Max(0, defender.currentHP - 1), requestedHpLoss);
                    defender.currentHP -= hpLoss;
                    _log.LogMessage(CombatEventType.HPDotDamage,
                        $"Shattered: {defender.instanceId} lost {hpLoss} HP", turn);
                    int oldDef = defender.GetStage(CoreStat.MeleeDef);
                    defender.SetStage(CoreStat.MeleeDef, oldDef + _resolverConfig.shatteredDefenseStages);
                    _log.LogStageChange(turn, defender.instanceId, CoreStat.MeleeDef,
                        defender.GetStage(CoreStat.MeleeDef) - oldDef, defender.GetStage(CoreStat.MeleeDef));
                    break;

                case StatusId.Defueled:
                    // Cleanse exactly one harmful chemistry reaction from self.
                    for (int i = defender.statuses.Count - 1; i >= 0; i--)
                    {
                        StatusInstance status = defender.statuses[i];
                        if (status == null || status.origin != StatusOrigin.ChemistryOnly ||
                            ChemistryStatusRules.GetPolarity(status.statusId) != ChemistryStatusPolarity.Harmful)
                            continue;

                        _log.LogStatus(turn, CombatEventType.StatusCleansed,
                            defender.instanceId, status.statusId, 0, sourceId);
                        if (status.hasTemporaryStageDelta)
                        {
                            int beforeStage = defender.GetStage(status.temporaryStageStat);
                            defender.SetStage(status.temporaryStageStat, beforeStage - status.temporaryStageDelta);
                            _log.LogStageChange(turn, defender.instanceId, status.temporaryStageStat,
                                defender.GetStage(status.temporaryStageStat) - beforeStage,
                                defender.GetStage(status.temporaryStageStat));
                        }
                        else if (status.statusId == StatusId.Slowed)
                        {
                            int beforeStage = defender.GetStage(CoreStat.Speed);
                            defender.SetStage(CoreStat.Speed, beforeStage - _resolverConfig.slowedSpeedStages);
                            _log.LogStageChange(turn, defender.instanceId, CoreStat.Speed,
                                defender.GetStage(CoreStat.Speed) - beforeStage,
                                defender.GetStage(CoreStat.Speed));
                        }
                        defender.statuses.RemoveAt(i);
                        break;
                    }
                    break;

                default:
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"Instant chemistry status {id} applied to {defender.instanceId}", turn);
                    break;
            }
        }

        private void ApplyCounterChemistry(StatusId id, ChainlingInstance defender,
            StatusDefinitionSO def, int turn, string sourceId, ChemistryReactionDecisionPayload decision)
        {
            // Find existing counter
            for (int i = 0; i < defender.statuses.Count; i++)
            {
                if (defender.statuses[i].statusId == id)
                {
                    int beforeCounter = defender.statuses[i].counterValue;
                    bool triggered = defender.statuses[i].IncrementCounter();
                    if (decision != null)
                    {
                        decision.counterBefore = beforeCounter;
                        decision.counterAfter = triggered ? 0 : defender.statuses[i].counterValue;
                    }
                    if (triggered)
                    {
                        // Cascade Failure: KO on 3rd apply
                        if (id == StatusId.CascadeFailure)
                        {
                            int damage = Math.Max(1, (int)(defender.EffectiveMaxHP * _resolverConfig.cascadeFailureMaxHpDamage));
                            defender.currentHP = Math.Max(0, defender.currentHP - damage);
                            _log.LogMessage(CombatEventType.HPDotDamage,
                                $"Cascade Failure: {defender.instanceId} lost {damage} HP", turn);
                            if (defender.currentHP <= 0)
                            {
                                defender.isAlive = false;
                                _log.LogKO(turn, defender.instanceId);
                            }
                        }
                        defender.statuses.RemoveAt(i);
                    }
                    else
                    {
                        _log.LogStatus(turn, CombatEventType.ChemistryStatusApplied,
                            defender.instanceId, id, defender.statuses[i].counterValue, sourceId);
                    }
                    return;
                }
            }

            // New counter
            var instance = new StatusInstance(id, StatusClass.Counter, StatusOrigin.ChemistryOnly, -1, sourceId);
            instance.counterThreshold = def.counterThreshold > 0 ? def.counterThreshold : 3;
            instance.IncrementCounter();
            if (decision != null)
            {
                decision.counterBefore = 0;
                decision.counterAfter = instance.counterValue;
            }
            defender.statuses.Add(instance);
            _log.LogStatus(turn, CombatEventType.ChemistryStatusApplied,
                defender.instanceId, id, 1, sourceId);
        }

        private static StatusInstance FindActiveStatus(ChainlingInstance defender, StatusId id)
        {
            if (defender?.statuses == null)
                return null;
            for (int i = 0; i < defender.statuses.Count; i++)
            {
                StatusInstance status = defender.statuses[i];
                if (status != null && status.statusId == id && !status.IsExpired)
                    return status;
            }
            return null;
        }
    }

    /// <summary>
    /// Result of chemistry resolution for a single hit.
    /// </summary>
    public struct ChemistryResult
    {
        public ChemistryRelation relation;
        public bool attackerReactionApplied;
        public bool defenderReactionApplied;

        /// <summary>Legacy-facing binary projection of the stackless relation.</summary>
        public int chemAdvCount => relation == ChemistryRelation.Advantage ? 1 : 0;

        /// <summary>Legacy-facing binary projection of the stackless relation.</summary>
        public int chemDisCount => relation == ChemistryRelation.Resisted ? 1 : 0;

        /// <summary>
        /// Advantage factor used by <see cref="DamageMultiplier"/>.
        /// 0 falls back to <see cref="CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR"/>.
        /// </summary>
        public float advantageFactor;

        /// <summary>
        /// Disadvantage factor used by <see cref="DamageMultiplier"/>.
        /// 0 falls back to <see cref="CombatConstants.CHEMISTRY_DISADVANTAGE_FACTOR"/>.
        /// </summary>
        public float disadvantageFactor;

        /// <summary>Net chemistry delta (ChemAdv - ChemDis).</summary>
        public int NetChemistry => (int)relation;

        /// <summary>Chemistry damage multiplier: adv^ChemAdv × dis^ChemDis.</summary>
        public float DamageMultiplier
        {
            get
            {
                float advFactor = advantageFactor > 0f
                    ? advantageFactor
                    : CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR;
                float disFactor = disadvantageFactor > 0f
                    ? disadvantageFactor
                    : CombatConstants.CHEMISTRY_DISADVANTAGE_FACTOR;

                if (relation == ChemistryRelation.Advantage)
                    return advFactor;
                if (relation == ChemistryRelation.Resisted)
                    return disFactor;
                return 1f;
            }
        }
    }
}
