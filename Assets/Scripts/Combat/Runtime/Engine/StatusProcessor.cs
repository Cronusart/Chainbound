using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Processes status effects: tick, decay, cleanse, and End-of-Turn effects.
    /// Follows COMBAT_02A_StatusFrameworkAndCatalog_v1.2 Â§3-4.
    /// </summary>
    public class StatusProcessor
    {
        private readonly StatusCatalogSO _catalog;
        private readonly CombatEventLog _log;
        private readonly CombatApService _apService;
        private readonly CombatResolverConfigSO _resolverConfig;

        public StatusProcessor(
            StatusCatalogSO catalog,
            CombatEventLog log,
            CombatApService apService = null,
            CombatResolverConfigSO resolverConfig = null)
        {
            _catalog = catalog;
            _log = log;
            _resolverConfig = resolverConfig != null
                ? resolverConfig
                : BalanceConfigRuntime.ResolverConfig;
            _apService = apService ?? new CombatApService(log, _resolverConfig);
        }

        /// <summary>
        /// Process End-of-Turn effects for a Chainling.
        /// Per 02A Â§3: DoT/regen, status AP changes, decrement durations.
        /// </summary>
        public void ProcessEndOfTurn(ChainlingInstance chainling, int turn, List<TurnEndStatusTickEntry> forensicTicks = null)
        {
            // Process effects BEFORE decrementing durations
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                var status = chainling.statuses[i];
                if (status.IsExpired) continue;

                var def = _catalog.Get(status.statusId);
                if (def == null) continue;

                // End-of-Turn damage (Rugged, FOMO, etc.)
                if (def.hasEndOfTurnDamage && def.endOfTurnDamagePercent > 0)
                {
                    int hpBefore = chainling.currentHP;
                    float damagePercent = def.endOfTurnDamagePercent;
                    if (status.statusId == StatusId.Rugged)
                    {
                        int tick = Math.Max(1, Math.Min(_resolverConfig.ruggedTickCap, status.counterValue));
                        damagePercent = tick / 16f;
                    }
                    else if (status.statusId == StatusId.FOMO)
                    {
                        damagePercent = _resolverConfig.fomoSelfDotPercent;
                    }
                    int dot = Math.Max(1, (int)(chainling.EffectiveMaxHP * damagePercent));
                    chainling.currentHP -= dot;
                    _log.LogMessage(CombatEventType.HPDotDamage,
                        $"{chainling.instanceId} took {dot} DoT from {status.statusId}", turn);
                    if (forensicTicks != null)
                    {
                        forensicTicks.Add(new TurnEndStatusTickEntry
                        {
                            targetDisplayName = chainling.displayName,
                            targetRef = _log.ResolveEntityRef(chainling.instanceId),
                            tickType = "dot",
                            source = status.statusId.ToString(),
                            amount = -dot,
                            hpBefore = hpBefore,
                            hpAfter = Math.Max(0, chainling.currentHP),
                            apBefore = chainling.currentAP,
                            apAfter = chainling.currentAP,
                        });
                    }

                    if (chainling.currentHP <= 0)
                    {
                        chainling.currentHP = 0;
                        chainling.isAlive = false;
                        _log.LogKO(turn, chainling.instanceId);
                        return; // Stop processing if KO'd
                    }

                    if (status.statusId == StatusId.Rugged)
                        status.counterValue = Math.Min(_resolverConfig.ruggedTickCap, Math.Max(1, status.counterValue) + 1);
                }

                if (status.statusId == StatusId.Drip && chainling.isAlive)
                {
                    int hpBefore = chainling.currentHP;
                    int heal = Math.Max(1, (int)Math.Round(
                        chainling.EffectiveMaxHP * _resolverConfig.dripHealPercentMaxHp,
                        MidpointRounding.AwayFromZero));
                    chainling.currentHP = Math.Min(chainling.EffectiveMaxHP, chainling.currentHP + heal);
                    int actual = chainling.currentHP - hpBefore;
                    if (actual > 0)
                    {
                        _log.Log(new CombatEvent
                        {
                            eventType = CombatEventType.HealApplied,
                            turn = turn,
                            sourceId = chainling.instanceId,
                            targetId = chainling.instanceId,
                            intValue = actual,
                            message = $"{chainling.displayName} healed {actual} HP from Drip",
                        });
                        _log.LogMessage(CombatEventType.HPHealed,
                            $"{chainling.displayName} recovered {actual} HP from Drip", turn);
                    }
                }

                // End-of-Turn AP change (Pumped/Yield = +1, Drained = -1, MEV Extracted = steal)
                if (def.hasEndOfTurnAP && def.endOfTurnAPChange != 0)
                {
                    int oldAP = chainling.currentAP;
                    if (def.endOfTurnAPChange > 0)
                        _apService.Gain(chainling, def.endOfTurnAPChange, turn, status.statusId.ToString());
                    else
                        _apService.Drain(chainling, Math.Abs(def.endOfTurnAPChange), turn, status.statusId.ToString());
                    if (forensicTicks != null)
                    {
                        forensicTicks.Add(new TurnEndStatusTickEntry
                        {
                            targetDisplayName = chainling.displayName,
                            targetRef = _log.ResolveEntityRef(chainling.instanceId),
                            tickType = def.endOfTurnAPChange > 0 ? "ap_gain" : "ap_drain",
                            source = status.statusId.ToString(),
                            amount = chainling.currentAP - oldAP,
                            hpBefore = chainling.currentHP,
                            hpAfter = chainling.currentHP,
                            apBefore = oldAP,
                            apAfter = chainling.currentAP,
                        });
                    }
                }

                // Per-turn stage effects (e.g., Corroded = Def -1 at End of Turn)
                if (def.stageEffects != null)
                {
                    for (int e = 0; e < def.stageEffects.Length; e++)
                    {
                        var eff = def.stageEffects[e];
                        if (!eff.applyOnce)
                        {
                            int oldStage = chainling.GetStage(eff.stat);
                            chainling.SetStage(eff.stat, oldStage + eff.stages);
                            _log.LogStageChange(turn, chainling.instanceId, eff.stat,
                                eff.stages, chainling.GetStage(eff.stat));
                        }
                    }
                }

                // HODLed: Def ramps +1/turn (up to +3)
                if (status.statusId == StatusId.HODLed)
                {
                    int currentDef = chainling.GetStage(CoreStat.MeleeDef);
                    if (currentDef < 3)
                    {
                        chainling.SetStage(CoreStat.MeleeDef, currentDef + 1);
                        _log.LogStageChange(turn, chainling.instanceId, CoreStat.MeleeDef,
                            1, chainling.GetStage(CoreStat.MeleeDef));
                    }
                }
            }

            // Decrement durations
            for (int i = chainling.statuses.Count - 1; i >= 0; i--)
            {
                var status = chainling.statuses[i];
                if (status.statusClass == StatusClass.Instant) continue; // Instants don't tick

                bool stillActive = status.Tick(turn);
                if (!stillActive)
                {
                    StatusDefinitionSO expiredDefinition = _catalog.Get(status.statusId);
                    RevertChemistryStageEffects(chainling, status, expiredDefinition, turn);

                    if (status.statusId == StatusId.Recharging)
                    {
                        _log.LogMessage(
                            CombatEventType.MoveRechargeComplete,
                            $"Move {status.rechargingMoveId} finished recharging",
                            turn);
                    }

                    // Handle coating expiry
                    if (status.statusId == StatusId.TypeCoating || status.statusId == StatusId.HypedTypeCoating)
                    {
                        chainling.coatingType = ChainlingType.None;
                        _log.LogMessage(CombatEventType.CoatingExpired,
                            $"{chainling.instanceId} coating expired", turn);
                    }

                    _log.LogStatus(turn, CombatEventType.StatusExpired,
                        chainling.instanceId, status.statusId, 0);
                    chainling.statuses.RemoveAt(i);
                }
            }
        }

        private void RevertChemistryStageEffects(
            ChainlingInstance chainling,
            StatusInstance status,
            StatusDefinitionSO definition,
            int turn)
        {
            if (status.ownedStageDeltas != null && status.ownedStageDeltas.Count > 0)
            {
                for (int i = 0; i < status.ownedStageDeltas.Count; i++)
                {
                    OwnedStageDelta owned = status.ownedStageDeltas[i];
                    int before = chainling.GetStage(owned.stat);
                    chainling.SetStage(owned.stat, before - owned.delta);
                    _log.LogStageChange(turn, chainling.instanceId, owned.stat,
                        chainling.GetStage(owned.stat) - before,
                        chainling.GetStage(owned.stat));
                }
                return;
            }

            if (status.hasTemporaryStageDelta)
            {
                int before = chainling.GetStage(status.temporaryStageStat);
                chainling.SetStage(status.temporaryStageStat, before - status.temporaryStageDelta);
                _log.LogStageChange(turn, chainling.instanceId, status.temporaryStageStat,
                    chainling.GetStage(status.temporaryStageStat) - before,
                    chainling.GetStage(status.temporaryStageStat));
                return;
            }

            if (status.statusId == StatusId.Slowed)
            {
                int before = chainling.GetStage(CoreStat.Speed);
                chainling.SetStage(CoreStat.Speed, before - _resolverConfig.slowedSpeedStages);
                _log.LogStageChange(turn, chainling.instanceId, CoreStat.Speed,
                    chainling.GetStage(CoreStat.Speed) - before, chainling.GetStage(CoreStat.Speed));
                return;
            }

            if (status.statusId != StatusId.FOMO || definition?.stageEffects == null)
                return;

            for (int i = 0; i < definition.stageEffects.Length; i++)
            {
                StatusStageEffect effect = definition.stageEffects[i];
                if (!effect.applyOnce || effect.stages == 0)
                    continue;
                int before = chainling.GetStage(effect.stat);
                chainling.SetStage(effect.stat, before - effect.stages);
                _log.LogStageChange(turn, chainling.instanceId, effect.stat,
                    chainling.GetStage(effect.stat) - before, chainling.GetStage(effect.stat));
            }
        }

        /// <summary>
        /// Process Start-of-Turn triggers for a Chainling.
        /// Per 02A Â§3: Turn-start triggers.
        /// </summary>
        public void ProcessStartOfTurn(ChainlingInstance chainling, int turn, CombatRng rng = null)
        {
            chainling.lostActionThisTurn = false;

            // Firewall is a two-turn status with one projectile charge available
            // on each turn. The initial chemistry application arms the first
            // charge; subsequent turns re-arm it here after the prior charge has
            // expired through the normal mitigation-source lifecycle.
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == StatusId.Firewall && !chainling.statuses[i].IsExpired)
                {
                    chainling.AddMitigationSource(
                        _resolverConfig.firewallProjectileMitigation,
                        1,
                        1,
                        "Chemistry.Firewall",
                        "Chemistry.Firewall",
                        0,
                        UtilityContactFilter.Projectile);
                    break;
                }
            }

            // Check for Offline: still sleeping
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == StatusId.Offline)
                {
                    chainling.skippingNextTurn = true;
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{chainling.instanceId} is Offline (sleeping)", turn);
                    break;
                }
            }

            // Check for Thread Lock: cannot gain AP
            bool threadLocked = false;
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                if (chainling.statuses[i].statusId == StatusId.ThreadLock)
                {
                    threadLocked = true;
                    _log.LogMessage(CombatEventType.SystemMessage,
                        $"{chainling.instanceId} is Thread Locked â€” no AP gain", turn);
                    break;
                }
            }

            // Baseline AP gain per turn; the AP service enforces Thread Lock.
            // Turn-start passives run after this, so Low AP Surge sees the granted AP.
            _apService.Gain(chainling, _resolverConfig.apPerTurn, turn, "TurnRegen");
            chainling.hasActedThisTurn = false;
            chainling.hitsTakenThisTurn = 0;
        }

        /// <summary>
        /// Apply a General status from a move to a target.
        /// Enforces the ChemistryOnly constraint (03A Â§5).
        /// </summary>
        public bool ApplyStatus(ChainlingInstance target, StatusId statusId, int duration,
            string sourceId, int turn, bool bypassChemistryCheck = false)
        {
            var def = _catalog.Get(statusId);
            if (def == null)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"Cannot apply unknown status: {statusId}", turn);
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = target != null ? target.displayName : string.Empty,
                        statusId = statusId.ToString(),
                        origin = "Unknown",
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "UnknownStatus",
                        sourceId = sourceId,
                    },
                    sourceId,
                    target != null ? new List<string> { target.instanceId } : null,
                    turn);
                return false;
            }

            // Enforce ChemistryOnly constraint
            if (!bypassChemistryCheck && def.origin == StatusOrigin.ChemistryOnly)
            {
                _log.LogMessage(CombatEventType.ValidationError,
                    $"VIOLATION: Attempted to directly apply ChemistryOnly status {statusId}!", turn);
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = target.displayName,
                        statusId = statusId.ToString(),
                        origin = StatusOrigin.General.ToString(),
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "ChemistryOnlyGuard",
                        sourceId = sourceId,
                    },
                    sourceId,
                    new List<string> { target.instanceId },
                    turn);
                return false;
            }

            // Check for immunity (Encrypted Seal)
            if (def.isDebuff)
            {
                for (int i = 0; i < target.statuses.Count; i++)
                {
                    if (target.statuses[i].statusId == StatusId.EncryptedSeal)
                    {
                        _log.LogStatus(turn, CombatEventType.StatusImmune,
                            target.instanceId, statusId, 0, sourceId);
                        _log.LogForensic(
                            CombatForensicEventType.StatusBlocked,
                            new StatusEventPayload
                            {
                                targetDisplayName = target.displayName,
                                statusId = statusId.ToString(),
                                origin = StatusOrigin.General.ToString(),
                                durationBefore = 0,
                                durationAfter = 0,
                                blockReason = "EncryptedSeal",
                                sourceId = sourceId,
                            },
                            sourceId,
                            new List<string> { target.instanceId },
                            turn);
                        return false;
                    }
                }
            }

            // Check for existing status â€” refresh duration
            for (int i = 0; i < target.statuses.Count; i++)
            {
                if (target.statuses[i].statusId == statusId && !target.statuses[i].IsExpired)
                {
                    int before = target.statuses[i].turnsRemaining;
                    target.statuses[i].turnsRemaining = Math.Max(target.statuses[i].turnsRemaining, duration);
                    if (StatusInstance.IsNextActionStatus(statusId))
                        target.statuses[i].HoldThroughGrantTurn(turn);
                    _log.LogStatus(turn, CombatEventType.StatusRefreshed,
                        target.instanceId, statusId, target.statuses[i].turnsRemaining, sourceId);
                    _log.LogForensic(
                        CombatForensicEventType.StatusRefreshed,
                        new StatusEventPayload
                        {
                            targetDisplayName = target.displayName,
                            statusId = statusId.ToString(),
                            origin = StatusOrigin.General.ToString(),
                            durationBefore = before,
                            durationAfter = target.statuses[i].turnsRemaining,
                            blockReason = string.Empty,
                            sourceId = sourceId,
                        },
                        sourceId,
                        new List<string> { target.instanceId },
                        turn);
                    return true;
                }
            }

            // Apply new status
            var instance = new StatusInstance(statusId, def.statusClass, StatusOrigin.General, duration, sourceId);
            if (StatusInstance.IsNextActionStatus(statusId))
                instance.HoldThroughGrantTurn(turn);
            target.statuses.Add(instance);
            _log.LogStatus(turn, CombatEventType.StatusApplied,
                target.instanceId, statusId, duration, sourceId);
            _log.LogForensic(
                CombatForensicEventType.StatusApplied,
                new StatusEventPayload
                {
                    targetDisplayName = target.displayName,
                    statusId = statusId.ToString(),
                    origin = StatusOrigin.General.ToString(),
                    durationBefore = 0,
                    durationAfter = duration,
                    blockReason = string.Empty,
                    sourceId = sourceId,
                },
                sourceId,
                new List<string> { target.instanceId },
                turn);

            // Apply one-time stage effects
            if (def.stageEffects != null)
            {
                for (int e = 0; e < def.stageEffects.Length; e++)
                {
                    var eff = def.stageEffects[e];
                    if (eff.applyOnce)
                    {
                        int oldStage = target.GetStage(eff.stat);
                        target.SetStage(eff.stat, oldStage + eff.stages);
                        int appliedDelta = target.GetStage(eff.stat) - oldStage;
                        if (statusId == StatusId.FOMO)
                        {
                            instance.hasTemporaryStageDelta = true;
                            instance.temporaryStageStat = eff.stat;
                            instance.temporaryStageDelta = appliedDelta;
                        }
                        else if (statusId == StatusId.BotSwarm || statusId == StatusId.FiftyOnePercentAttacked)
                        {
                            instance.AddOwnedStage(eff.stat, appliedDelta);
                        }
                        _log.LogStageChange(turn, target.instanceId, eff.stat,
                            appliedDelta, target.GetStage(eff.stat));
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Cleanse debuffs from a Chainling (General only by default). Per 02A Â§4.
        /// </summary>
        public int Cleanse(ChainlingInstance target, int maxCleanse, int turn)
        {
            int cleansed = 0;
            for (int i = target.statuses.Count - 1; i >= 0 && cleansed < maxCleanse; i--)
            {
                var status = target.statuses[i];
                var def = _catalog.Get(status.statusId);
                if (def != null && def.isDebuff && status.origin == StatusOrigin.General)
                {
                    _log.LogStatus(turn, CombatEventType.StatusCleansed,
                        target.instanceId, status.statusId, 0);
                    target.statuses.RemoveAt(i);
                    cleansed++;
                }
            }
            return cleansed;
        }
    }
}
