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
        /// Deals one end-of-turn percent. Returns false when the Chainling is knocked out.
        /// </summary>
        private bool ApplyEndOfTurnDamage(
            ChainlingInstance chainling,
            StatusInstance status,
            float damagePercent,
            int turn,
            List<TurnEndStatusTickEntry> forensicTicks,
            string verb)
        {
            int hpBefore = chainling.currentHP;
            int dot = Math.Max(1, (int)(chainling.EffectiveMaxHP * damagePercent));
            chainling.currentHP -= dot;
            _log.Log(new CombatEvent
            {
                eventType = CombatEventType.HPDotDamage,
                turn = turn,
                sourceId = chainling.instanceId,
                targetId = chainling.instanceId,
                intValue = dot,
                message = $"{verb} {chainling.displayName} for {dot}",
            });
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
                return false;
            }

            return true;
        }

        /// <summary>
        /// Process End-of-Turn effects for a Chainling.
        /// Per 02A Â§3: DoT/regen, status AP changes, decrement durations.
        /// </summary>
        public void ProcessEndOfTurn(ChainlingInstance chainling, int turn, List<TurnEndStatusTickEntry> forensicTicks = null)
        {
            // Passive and move burns land before chemistry and other status ticks.
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                StatusInstance kindled = chainling.statuses[i];
                if (kindled.IsExpired || kindled.statusId != StatusId.Kindled)
                    continue;
                if (!ApplyEndOfTurnDamage(chainling, kindled, 0.06f, turn, forensicTicks, "Kindled burns"))
                    return;
            }

            // Process effects BEFORE decrementing durations
            for (int i = 0; i < chainling.statuses.Count; i++)
            {
                var status = chainling.statuses[i];
                if (status.IsExpired) continue;

                var def = _catalog.Get(status.statusId);
                if (def == null) continue;

                // End-of-Turn damage (Rugged, FOMO, etc.). Kindled and Leaked are not this loop.
                if (status.statusId != StatusId.Kindled &&
                    status.statusId != StatusId.Leaked &&
                    def.hasEndOfTurnDamage && def.endOfTurnDamagePercent > 0)
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
                    string dotVerb = status.statusId == StatusId.Rugged ? "Rugged scorches" : null;
                    if (dotVerb != null)
                    {
                        _log.Log(new CombatEvent
                        {
                            eventType = CombatEventType.HPDotDamage,
                            turn = turn,
                            sourceId = chainling.instanceId,
                            targetId = chainling.instanceId,
                            intValue = dot,
                            message = $"{dotVerb} {chainling.displayName} for {dot}",
                        });
                    }
                    else
                    {
                        _log.LogMessage(CombatEventType.HPDotDamage,
                            $"{chainling.instanceId} took {dot} DoT from {status.statusId}", turn);
                    }
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

                // Per-turn stage effects (e.g., Corroded = Def -1 at End of Turn).
                // Each tick is owned by the status and reverted when it expires.
                if (def.stageEffects != null)
                {
                    for (int e = 0; e < def.stageEffects.Length; e++)
                    {
                        var eff = def.stageEffects[e];
                        if (!eff.applyOnce)
                        {
                            int requested = BuffGate.FilterPositiveStage(
                                chainling, eff.stat, eff.stages, status.sourceId, turn);
                            int applied = status.ApplyOwnedStage(chainling, eff.stat, requested);
                            if (eff.stages > 0 && requested == 0)
                                LogBuffStageBlocked(chainling, eff.stat, eff.stages, turn);
                            if (applied != 0)
                            {
                                _log.LogStageChange(turn, chainling.instanceId, eff.stat,
                                    applied, chainling.GetStage(eff.stat));
                            }
                        }
                    }
                }

                // HODLed: an extra Def +1/turn while Def is below +3, on top of its repeating stage.
                // Both writes are owned and revert together when the status expires.
                if (status.statusId == StatusId.HODLed)
                {
                    int currentDef = chainling.GetStage(CoreStat.MeleeDef);
                    if (currentDef < 3)
                    {
                        int requested = BuffGate.FilterPositiveStage(
                            chainling, CoreStat.MeleeDef, 1, status.sourceId, turn);
                        int applied = status.ApplyOwnedStage(chainling, CoreStat.MeleeDef, requested);
                        if (requested == 0)
                            LogBuffStageBlocked(chainling, CoreStat.MeleeDef, 1, turn);
                        if (applied != 0)
                        {
                            _log.LogStageChange(turn, chainling.instanceId, CoreStat.MeleeDef,
                                applied, chainling.GetStage(CoreStat.MeleeDef));
                        }
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

                    if (status.statusId == StatusId.ThreadLock)
                        chainling.threadLockBlockedUntilTurn = turn + 2;

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

        /// <summary>
        /// Recharge from a used move keeps counting on the bench. Other statuses stay paused.
        /// </summary>
        public void TickRechargeWhileBenched(ChainlingInstance chainling, int turn)
        {
            if (chainling?.statuses == null)
                return;

            for (int i = chainling.statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance status = chainling.statuses[i];
                if (status == null || status.statusId != StatusId.Recharging || status.IsExpired)
                    continue;

                bool stillActive = status.Tick(turn);
                if (stillActive)
                    continue;

                _log.LogMessage(
                    CombatEventType.MoveRechargeComplete,
                    $"Move {status.rechargingMoveId} finished recharging",
                    turn);
                _log.LogStatus(turn, CombatEventType.StatusExpired,
                    chainling.instanceId, status.statusId, 0);
                chainling.statuses.RemoveAt(i);
            }
        }

        private void RevertChemistryStageEffects(
            ChainlingInstance chainling,
            StatusInstance status,
            StatusDefinitionSO definition,
            int turn)
        {
            bool revertedOwned = false;
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
                revertedOwned = true;
            }

            if (status.ownedCombatStageDeltas != null && status.ownedCombatStageDeltas.Count > 0)
            {
                for (int i = 0; i < status.ownedCombatStageDeltas.Count; i++)
                {
                    OwnedCombatStageDelta owned = status.ownedCombatStageDeltas[i];
                    int before = chainling.GetCombatStage(owned.stat);
                    chainling.SetCombatStage(owned.stat, before - owned.delta);
                    _log.LogMessage(
                        CombatEventType.StageChanged,
                        $"{chainling.displayName} {owned.stat} stage returned by {-owned.delta}.",
                        turn);
                }
                revertedOwned = true;
            }

            if (revertedOwned)
                return;

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
                        origin = def.origin.ToString(),
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
            if (!BuffGate.AllowsBuffStatus(target, def, sourceId, turn))
            {
                _log.LogMessage(
                    CombatEventType.BuffBlocked,
                    $"Cipher Lock blocked {def.displayName} on {target.displayName}.",
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
                                origin = def.origin.ToString(),
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
            if (statusId == StatusId.ThreadLock && IsThreadLockReapplyBlocked(target, turn))
            {
                _log.LogMessage(CombatEventType.SystemMessage,
                    $"{target.displayName} cannot be Thread Locked again yet.", turn);
                _log.LogForensic(
                    CombatForensicEventType.StatusBlocked,
                    new StatusEventPayload
                    {
                        targetDisplayName = target.displayName,
                        statusId = statusId.ToString(),
                        origin = def.origin.ToString(),
                        durationBefore = 0,
                        durationAfter = 0,
                        blockReason = "ThreadLockReapplyGap",
                        sourceId = sourceId,
                    },
                    sourceId,
                    new List<string> { target.instanceId },
                    turn);
                return false;
            }

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
                            origin = def.origin.ToString(),
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
            var instance = new StatusInstance(statusId, def.statusClass, def.origin, duration, sourceId);
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
                    origin = def.origin.ToString(),
                    durationBefore = 0,
                    durationAfter = duration,
                    blockReason = string.Empty,
                    sourceId = sourceId,
                },
                sourceId,
                new List<string> { target.instanceId },
                turn);

            ApplyApplyOnceStages(target, instance, def, _log, turn);
            return true;
        }

        private static bool IsThreadLockReapplyBlocked(ChainlingInstance target, int turn)
        {
            if (target == null)
                return false;
            if (turn < target.threadLockBlockedUntilTurn)
                return true;
            if (target.statuses == null)
                return false;

            for (int i = 0; i < target.statuses.Count; i++)
            {
                StatusInstance existing = target.statuses[i];
                if (existing != null && existing.statusId == StatusId.ThreadLock && !existing.IsExpired)
                    return true;
            }

            return false;
        }

        public static void ApplyApplyOnceStages(
            ChainlingInstance target,
            StatusInstance instance,
            StatusDefinitionSO def,
            CombatEventLog log,
            int turn)
        {
            if (target == null || instance == null || def?.stageEffects == null)
                return;

            for (int e = 0; e < def.stageEffects.Length; e++)
            {
                StatusStageEffect eff = def.stageEffects[e];
                if (!eff.applyOnce)
                    continue;

                if (eff.usesCombatStage)
                {
                    int oldCombat = target.GetCombatStage(eff.combatStage);
                    int requested = BuffGate.FilterPositiveStage(
                        target, eff.combatStage, eff.stages, instance.sourceId, turn);
                    target.SetCombatStage(eff.combatStage, oldCombat + requested);
                    int combatDelta = target.GetCombatStage(eff.combatStage) - oldCombat;
                    instance.AddOwnedCombatStage(eff.combatStage, combatDelta);
                    if (combatDelta != 0 && log != null)
                    {
                        log.LogMessage(
                            CombatEventType.StageChanged,
                            $"{target.displayName} {eff.combatStage} {combatDelta:+#;-#;0}.",
                            turn);
                    }
                    continue;
                }

                // Apply-once stages are owned by this instance (shared PR #9 helper).
                // Refresh does not re-apply them, so a second application extends
                // duration without stacking. Expiry reverts the clamped delta once.
                // FOMO keeps its temporary delta so Defueled cleanse can still revert it.
                int appliedDelta;
                int requestedStages = BuffGate.FilterPositiveStage(
                    target, eff.stat, eff.stages, instance.sourceId, turn);
                if (instance.statusId == StatusId.FOMO)
                {
                    int oldStage = target.GetStage(eff.stat);
                    target.SetStage(eff.stat, oldStage + requestedStages);
                    appliedDelta = target.GetStage(eff.stat) - oldStage;
                    instance.hasTemporaryStageDelta = true;
                    instance.temporaryStageStat = eff.stat;
                    instance.temporaryStageDelta = appliedDelta;
                }
                else
                {
                    appliedDelta = instance.ApplyOwnedStage(target, eff.stat, requestedStages);
                }

                log?.LogStageChange(turn, target.instanceId, eff.stat, appliedDelta, target.GetStage(eff.stat));
            }
        }

        public bool TryExtendShortestBuff(ChainlingInstance target, int turn, string sourceId)
        {
            if (target?.statuses == null)
                return false;
            if (BuffGate.IsCipherLocked(target))
            {
                _log.LogMessage(
                    CombatEventType.BuffBlocked,
                    $"Cipher Lock blocked buff extension on {target.displayName}.",
                    turn);
                return false;
            }

            int bestIndex = -1;
            int bestTurns = int.MaxValue;
            for (int i = 0; i < target.statuses.Count; i++)
            {
                StatusInstance status = target.statuses[i];
                if (status == null || status.IsExpired || status.statusClass != StatusClass.Duration)
                    continue;
                StatusDefinitionSO def = _catalog != null ? _catalog.Get(status.statusId) : null;
                if (def == null || !def.isBuff || def.origin != StatusOrigin.General || def.isDebuff)
                    continue;
                if (status.turnsRemaining < bestTurns)
                {
                    bestTurns = status.turnsRemaining;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                _log.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{target.displayName} has no buff to extend.",
                    turn);
                return false;
            }

            StatusInstance chosen = target.statuses[bestIndex];
            chosen.turnsRemaining += 1;
            _log.LogMessage(
                CombatEventType.SystemMessage,
                $"Extend Protocol adds 1 turn to {target.displayName}'s {chosen.statusId}.",
                turn);
            _log.LogStatus(turn, CombatEventType.StatusRefreshed, target.instanceId, chosen.statusId, chosen.turnsRemaining, sourceId);
            return true;
        }

        private void LogBuffStageBlocked(ChainlingInstance target, CoreStat stat, int amount, int turn)
        {
            _log.LogMessage(
                CombatEventType.BuffBlocked,
                $"Cipher Lock blocked {target.displayName}'s {stat} +{amount}.",
                turn);
        }

        public bool ApplySlowedForTurns(ChainlingInstance target, int duration, string sourceId, int turn)
        {
            if (target == null)
                return false;

            bool already = false;
            for (int i = 0; i < target.statuses.Count; i++)
            {
                if (target.statuses[i].statusId == StatusId.Slowed && !target.statuses[i].IsExpired)
                {
                    already = true;
                    break;
                }
            }

            bool applied = ApplyStatus(target, StatusId.Slowed, Math.Max(1, duration), sourceId, turn, true);
            if (!applied || already)
                return applied;

            StatusInstance instance = null;
            for (int i = 0; i < target.statuses.Count; i++)
            {
                if (target.statuses[i].statusId == StatusId.Slowed && !target.statuses[i].IsExpired)
                    instance = target.statuses[i];
            }

            int oldSpeed = target.GetStage(CoreStat.Speed);
            int requested = _resolverConfig != null ? _resolverConfig.slowedSpeedStages : -2;
            target.SetStage(CoreStat.Speed, oldSpeed + requested);
            int delta = target.GetStage(CoreStat.Speed) - oldSpeed;
            if (instance != null)
            {
                instance.hasTemporaryStageDelta = true;
                instance.temporaryStageStat = CoreStat.Speed;
                instance.temporaryStageDelta = delta;
            }
            _log.LogStageChange(turn, target.instanceId, CoreStat.Speed, delta, target.GetStage(CoreStat.Speed));
            return true;
        }

        /// <summary>
        /// Cleanse debuffs from a Chainling (General only by default). Per 02A Â§4.
        /// </summary>
        public int CountGeneralDebuffs(ChainlingInstance target)
        {
            if (target?.statuses == null || _catalog == null)
                return 0;

            int count = 0;
            for (int i = 0; i < target.statuses.Count; i++)
            {
                StatusInstance status = target.statuses[i];
                StatusDefinitionSO def = status != null ? _catalog.Get(status.statusId) : null;
                if (status != null && !status.IsExpired && status.origin == StatusOrigin.General &&
                    def != null && def.isDebuff)
                    count++;
            }
            return count;
        }

        public int StripBuffs(ChainlingInstance target, int maxCount, int turn)
        {
            if (target?.statuses == null || _catalog == null || maxCount <= 0)
                return 0;

            int removed = 0;
            while (removed < maxCount)
            {
                int bestIndex = -1;
                int shortest = int.MaxValue;
                for (int i = 0; i < target.statuses.Count; i++)
                {
                    StatusInstance status = target.statuses[i];
                    StatusDefinitionSO def = status != null ? _catalog.Get(status.statusId) : null;
                    if (status == null || status.IsExpired || status.origin != StatusOrigin.General ||
                        def == null || !def.isBuff || def.isDebuff)
                        continue;
                    if (status.turnsRemaining < shortest)
                    {
                        shortest = status.turnsRemaining;
                        bestIndex = i;
                    }
                }

                if (bestIndex < 0)
                    break;

                StatusInstance chosen = target.statuses[bestIndex];
                StatusDefinitionSO chosenDef = _catalog.Get(chosen.statusId);
                RevertChemistryStageEffects(target, chosen, chosenDef, turn);
                _log.LogStatus(turn, CombatEventType.StatusCleansed, target.instanceId, chosen.statusId, 0);
                target.statuses.RemoveAt(bestIndex);
                removed++;
            }
            return removed;
        }

        public int StripAllGeneralStatuses(ChainlingInstance target, int turn)
        {
            if (target?.statuses == null || _catalog == null)
                return 0;

            int removed = 0;
            for (int i = target.statuses.Count - 1; i >= 0; i--)
            {
                StatusInstance status = target.statuses[i];
                StatusDefinitionSO def = status != null ? _catalog.Get(status.statusId) : null;
                if (status == null || status.origin != StatusOrigin.General || def == null ||
                    (!def.isBuff && !def.isDebuff))
                    continue;

                RevertChemistryStageEffects(target, status, def, turn);
                if (status.statusId == StatusId.ThreadLock)
                    target.threadLockBlockedUntilTurn = 0;
                _log.LogStatus(turn, CombatEventType.StatusCleansed, target.instanceId, status.statusId, 0);
                target.statuses.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        public int Cleanse(ChainlingInstance target, int maxCleanse, int turn)
        {
            int cleansed = 0;
            for (int i = target.statuses.Count - 1; i >= 0 && cleansed < maxCleanse; i--)
            {
                var status = target.statuses[i];
                var def = _catalog.Get(status.statusId);
                if (def != null && def.isDebuff && status.origin == StatusOrigin.General)
                {
                    RevertChemistryStageEffects(target, status, def, turn);
                    if (status.statusId == StatusId.ThreadLock)
                        target.threadLockBlockedUntilTurn = 0;
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
