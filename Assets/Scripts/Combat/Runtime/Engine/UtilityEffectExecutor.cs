using System;

namespace Chainbound.Combat
{
    public sealed class UtilityEffectExecutionContext
    {
        public CombatState state;
        public TeamState sourceTeam;
        public TeamState targetTeam;
        public TurnActionLedger ledger;
        public int damageDealt;
        public int lastStatusStripCount;
    }

    /// <summary>
    /// Executes typed utility effects from move metadata.
    /// </summary>
    public sealed class UtilityEffectExecutor
    {
        private readonly CombatEventLog _log;
        private readonly CombatApService _apService;
        private readonly StatusProcessor _statusProcessor;

        public UtilityEffectExecutor(CombatEventLog log, CombatApService apService = null, StatusProcessor statusProcessor = null)
        {
            _log = log;
            _apService = apService;
            _statusProcessor = statusProcessor;
        }

        public void Execute(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn,
            bool shouldApplyConditionalEffects,
            CombatRng rng = null,
            TeamState opposingTeam = null,
            UtilityEffectExecutionContext context = null)
        {
            if (!shouldApplyConditionalEffects)
                return;
            if (source == null || target == null || move == null)
                return;
            if (move.utilityEffects == null || move.utilityEffects.Count == 0)
                return;

            if (context != null)
                context.lastStatusStripCount = 0;

            for (int i = 0; i < move.utilityEffects.Count; i++)
            {
                UtilityEffectPayload effect = move.utilityEffects[i];
                if (effect == null)
                    continue;
                if (!ShouldApplyEffect(effect, rng, source, target, move, turn))
                    continue;

                switch (effect.effectType)
                {
                    case UtilityEffectType.MitigationWindow:
                        ApplyMitigationWindow(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.GainApOnDamageIfSpeedHigher:
                        ApplyReactiveApWindow(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.FullAbsorbBarrier:
                        ApplyFullAbsorbBarrier(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.DamageThresholdBarrier:
                        ApplyDamageThresholdBarrier(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.WallHitRetaliation:
                        // Metadata consumed while the paired wall payload is installed.
                        break;

                    case UtilityEffectType.GainAP:
                        _apService?.Gain(ResolveUtilityTarget(source, target, effect), ResolveApAmount(effect), turn, move.definitionId);
                        break;

                    case UtilityEffectType.DrainAP:
                        DrainApEffect(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.StealAP:
                        _apService?.Steal(source, ResolveUtilityTarget(source, target, effect), ResolveApAmount(effect), turn, move.definitionId);
                        break;

                    case UtilityEffectType.TransferAP:
                        _apService?.TransferPaidCost(source, ResolveUtilityTarget(source, target, effect), ResolveApAmount(effect), turn, move.definitionId);
                        break;

                    case UtilityEffectType.RefundAP:
                        _apService?.Refund(source, ResolveApAmount(effect), turn, move.definitionId);
                        break;

                    case UtilityEffectType.NextMoveAPSurcharge:
                        ApplyNextMoveApSurcharge(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.TurnStartPendingStatus:
                        QueueTurnStartStatus(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.Cleanse:
                        ApplyCleanse(source, target, move, effect, turn);
                        break;

                    case UtilityEffectType.ApplyChemistryStatus:
                        ApplyChemistryStatus(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.StripBuffs:
                        ApplyStripBuffs(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.StripAllStatuses:
                        ApplyStripAllStatuses(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.StripBuffsThenStage:
                        ApplyStripBuffsThenStage(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.HealSelfPerTargetDebuff:
                        ApplyHealSelfPerTargetDebuff(source, target, move, effect, turn, context);
                        break;

                    case UtilityEffectType.BonusHealPercentIfSpeedHigher:
                        ApplyBonusHealIfFaster(source, target, move, effect, turn, opposingTeam);
                        break;

                    case UtilityEffectType.ChangeStage:
                        ApplyStageChange(source, target, move, effect, turn, rng, context);
                        break;

                    case UtilityEffectType.RandomStageChange:
                        ApplyRandomStageChange(source, target, move, effect, turn, rng, context);
                        break;

                    case UtilityEffectType.ArmUtilityWindow:
                        ArmUtilityWindow(source, target, move, effect, turn, rng, context);
                        break;

                    case UtilityEffectType.BankAPUntilNextTurn:
                        BankApUntilNextTurn(source, move, effect, turn);
                        break;

                    case UtilityEffectType.GainApIfDistinctTargetsHitAtLeast:
                        ApplyLedgerTargetHitPayoff(source, move, effect, turn, context);
                        break;

                    case UtilityEffectType.FieldEntryStageChange:
                        ApplyFieldEntryEffect(source, move, effect, turn, context);
                        break;

                    case UtilityEffectType.LifestealPercent:
                        ApplyLifesteal(source, move, effect, turn, context);
                        break;

                    case UtilityEffectType.HealPercentIfStageAtLeast:
                        ApplyStageThresholdHeal(source, target, move, effect, turn, rng, context);
                        break;

                    case UtilityEffectType.ExtendBuff:
                        _statusProcessor?.TryExtendShortestBuff(
                            ResolveUtilityTarget(source, target, effect, rng, context),
                            turn,
                            move != null ? move.instanceId : string.Empty);
                        break;

                    case UtilityEffectType.RaiseLowestStage:
                        ApplyRaiseLowestStage(source, turn, context);
                        break;

                    case UtilityEffectType.IncomingDamageMitigation:
                    case UtilityEffectType.MoveAPDiscountWhileHPAbove:
                        // Always-on passive metadata consumed by the damage/eligibility paths.
                        break;
                }
            }
        }

        private void ApplyRaiseLowestStage(ChainlingInstance source, int turn, UtilityEffectExecutionContext context)
        {
            TeamState team = context != null ? context.sourceTeam : null;
            if (source == null || team == null)
                return;

            TurnActionLedger ledger = context.state != null ? context.state.turnActionLedger : null;
            int serial = ledger != null ? ledger.actionSerial : 0;
            if (ledger != null && ledger.raiseLowestStageActionSerial == serial)
                return;
            if (ledger != null)
                ledger.raiseLowestStageActionSerial = serial;

            var allies = team.GetActiveChainlings();
            var order = new[]
            {
                CoreStat.MeleeAtk,
                CoreStat.MeleeDef,
                CoreStat.RangedAtk,
                CoreStat.RangedDef,
                CoreStat.Speed,
            };
            for (int i = 0; i < allies.Count; i++)
            {
                ChainlingInstance ally = allies[i];
                if (ally == null || !ally.isAlive)
                    continue;

                bool found = false;
                int lowest = int.MaxValue;
                CoreStat chosen = CoreStat.MeleeAtk;
                for (int s = 0; s < order.Length; s++)
                {
                    int value = ally.GetStage(order[s]);
                    if (value >= 6)
                        continue;
                    if (!found || value < lowest)
                    {
                        found = true;
                        lowest = value;
                        chosen = order[s];
                    }
                }

                if (!found)
                {
                    _log?.LogMessage(
                        CombatEventType.SystemMessage,
                        $"{ally.displayName}'s stages are already at +6.",
                        turn);
                    continue;
                }

                int before = ally.GetStage(chosen);
                int requested = BuffGate.FilterPositiveStage(ally, chosen, 1, "RaiseLowestStage", turn);
                ally.SetStage(chosen, before + requested);
                int actual = ally.GetStage(chosen) - before;
                if (requested == 0)
                {
                    _log?.LogMessage(
                        CombatEventType.BuffBlocked,
                        $"Cipher Lock blocked {ally.displayName}'s {chosen} +1.",
                        turn);
                }
                if (actual == 0)
                    continue;

                _log?.LogStageChange(turn, ally.instanceId, chosen, actual, ally.GetStage(chosen));
                _log?.LogMessage(
                    CombatEventType.SystemMessage,
                    $"Network Sync raises {ally.displayName}'s {chosen} by {actual}.",
                    turn);
                if (ally != source)
                    ledger?.NoteUplinkAlly(source.instanceId, ally.instanceId);
            }
        }

        private void ApplyStageChange(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            CombatRng rng,
            UtilityEffectExecutionContext context)
        {
            if (effect.affectAllAllies && context?.sourceTeam != null)
            {
                var allies = context.sourceTeam.GetActiveChainlings();
                for (int i = 0; i < allies.Count; i++)
                    ApplyStageDelta(source, allies[i], move, effect, turn, context);
                return;
            }

            ChainlingInstance stageTarget = ResolveUtilityTarget(source, target, effect, rng, context);
            ApplyStageDelta(source, stageTarget, move, effect, turn, context);
        }

        private void ApplyRandomStageChange(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            CombatRng rng,
            UtilityEffectExecutionContext context)
        {
            ChainlingInstance stageTarget = ResolveUtilityTarget(source, target, effect, rng, context);
            if (stageTarget == null || rng == null)
                return;

            int statIndex = rng.NextInt(0, 4, out float statRoll);
            int minAmount = effect.stageAmount;
            int maxAmount = effect.stageAmountMax != 0 ? effect.stageAmountMax : minAmount;
            if (maxAmount < minAmount)
            {
                int swap = minAmount;
                minAmount = maxAmount;
                maxAmount = swap;
            }
            int amount = rng.NextInt(minAmount, maxAmount, out float amountRoll);
            var rolled = new UtilityEffectPayload
            {
                stageStat = (CombatStageStat)statIndex,
                stageAmount = amount,
                stageCap = effect.stageCap,
            };
            ApplyStageDelta(source, stageTarget, move, rolled, turn, context);
            _log?.LogForensic(
                CombatForensicEventType.ProcRoll,
                new ProcRollPayload
                {
                    procId = $"UtilityEffect.{move.instanceId}.RandomStage.{rolled.stageStat}.{amount}",
                    chance = 1f,
                    roll = (statRoll + amountRoll) * 0.5f,
                    triggered = true,
                },
                source != null ? source.instanceId : string.Empty,
                new System.Collections.Generic.List<string> { stageTarget.instanceId },
                turn);
        }

        private void ApplyStageDelta(
            ChainlingInstance source,
            ChainlingInstance stageTarget,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context = null)
        {
            if (stageTarget == null || effect.stageAmount == 0)
                return;

            int before = stageTarget.GetCombatStage(effect.stageStat);
            int requested = BuffGate.FilterPositiveStage(
                stageTarget, effect.stageStat, effect.stageAmount, move.instanceId, turn);
            if (effect.stageAmount > 0 && requested == 0)
            {
                _log?.LogMessage(
                    CombatEventType.BuffBlocked,
                    $"Cipher Lock blocked {stageTarget.displayName}'s {effect.stageStat} +{effect.stageAmount}.",
                    turn);
                return;
            }
            int desired = before + requested;
            if (effect.stageCap != 0)
            {
                desired = requested > 0
                    ? Math.Min(effect.stageCap, desired)
                    : Math.Max(effect.stageCap, desired);
            }
            stageTarget.SetCombatStage(effect.stageStat, desired);
            int actual = stageTarget.GetCombatStage(effect.stageStat) - before;
            if (actual == 0)
                return;

            if (actual > 0 && effect.stageAmount > 0 && stageTarget != source && context?.state?.turnActionLedger != null)
                context.state.turnActionLedger.NoteUplinkAlly(source != null ? source.instanceId : null, stageTarget.instanceId);

            if (TryCoreStat(effect.stageStat, out CoreStat coreStat))
            {
                _log?.LogStageChange(turn, stageTarget.instanceId, coreStat, actual, stageTarget.GetCombatStage(effect.stageStat));
            }
            else
            {
                _log?.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{stageTarget.displayName} {effect.stageStat} changed {actual:+#;-#;0} ({move.displayName}).",
                    turn);
            }
        }

        private void ArmUtilityWindow(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            CombatRng rng,
            UtilityEffectExecutionContext context)
        {
            ChainlingInstance owner = ResolveUtilityTarget(source, target, effect, rng, context);
            if (owner == null || effect.armedTrigger == ArmedWindowTrigger.None)
                return;
            if (owner.armedUtilityWindows == null)
                owner.armedUtilityWindows = new System.Collections.Generic.List<ArmedUtilityWindow>();

            var window = new ArmedUtilityWindow
            {
                trigger = effect.armedTrigger,
                turnsRemaining = effect.turnBudget > 0 ? effect.turnBudget : 1,
                hitsRemaining = effect.hitBudget == 0 ? 1 : effect.hitBudget,
                activateOnTurn = effect.armedTrigger == ArmedWindowTrigger.OwnerTurnStart ? turn + 1 : turn,
                apDelta = effect.apGain,
                healPercentMaxHP = effect.healPercentMaxHP,
                stageStat = effect.stageStat,
                stageAmount = effect.stageAmount,
                stageCap = effect.stageCap,
                affectAllAllies = effect.affectAllAllies,
                counterDamagePercentMaxHP = effect.counterDamagePercentMaxHP,
                stackKey = effect.stackKey ?? string.Empty,
                sourceTag = move.definitionId ?? move.displayName,
            };

            bool triggeredImmediately = WasArmedTriggerAlreadySatisfied(window.trigger, context);
            if (triggeredImmediately)
            {
                ApplyArmedWindowEffects(owner, context?.sourceTeam, null, window, turn);
                return;
            }

            RefreshOrAddWindow(owner.armedUtilityWindows, window);
            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{owner.displayName} armed {window.trigger} ({move.displayName}).",
                turn);
        }

        private void BankApUntilNextTurn(
            ChainlingInstance source,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            if (source == null)
                return;
            int cap = effect.threshold > 0 ? effect.threshold : 5;
            int banked = Math.Min(Math.Max(0, source.currentAP), cap);
            if (banked <= 0)
                return;

            _apService?.Drain(source, banked, turn, move.definitionId);
            if (source.armedUtilityWindows == null)
                source.armedUtilityWindows = new System.Collections.Generic.List<ArmedUtilityWindow>();
            var window = new ArmedUtilityWindow
            {
                trigger = ArmedWindowTrigger.OwnerTurnStart,
                turnsRemaining = 1,
                hitsRemaining = 1,
                activateOnTurn = turn + 1,
                apDelta = banked + Math.Max(0, effect.apGain),
                bankedAP = banked,
                stackKey = effect.stackKey ?? move.definitionId,
                sourceTag = move.definitionId ?? move.displayName,
            };
            RefreshOrAddWindow(source.armedUtilityWindows, window);
            _log?.LogMessage(CombatEventType.SystemMessage,
                $"{source.displayName} banked {banked} AP for next turn.", turn);
        }

        private void ApplyLedgerTargetHitPayoff(
            ChainlingInstance source,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (source == null || context?.ledger == null)
                return;
            int threshold = effect.threshold > 0 ? effect.threshold : 2;
            int count = context.sourceTeam != null
                ? context.ledger.CountDistinctTargetsHitThisTurnByTeam(context.sourceTeam.teamIndex)
                : context.ledger.CountDistinctTargetsHitThisTurn(source.instanceId);
            if (count < threshold)
                return;
            _apService?.Gain(source, effect.apGain > 0 ? effect.apGain : 1, turn, move.definitionId);
        }

        private void ApplyFieldEntryEffect(
            ChainlingInstance source,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (source == null || context?.state == null || context.sourceTeam == null)
                return;
            if (context.state.fieldEntryEffects == null)
                context.state.fieldEntryEffects = new System.Collections.Generic.List<CombatFieldEntryEffect>();

            string key = effect.stackKey ?? move.definitionId ?? string.Empty;
            for (int i = 0; i < context.state.fieldEntryEffects.Count; i++)
            {
                CombatFieldEntryEffect existing = context.state.fieldEntryEffects[i];
                if (existing != null && existing.sourceTeamIndex == context.sourceTeam.teamIndex &&
                    string.Equals(existing.stackKey, key, StringComparison.Ordinal))
                {
                    existing.turnsRemaining = effect.turnBudget > 0 ? effect.turnBudget : 1;
                    existing.stageStat = effect.stageStat;
                    existing.stageAmount = effect.stageAmount;
                    existing.chance = effect.chance > 0f ? effect.chance : 1f;
                    return;
                }
            }

            context.state.fieldEntryEffects.Add(new CombatFieldEntryEffect
            {
                sourceTeamIndex = context.sourceTeam.teamIndex,
                affectsOpposingTeam = true,
                turnsRemaining = effect.turnBudget > 0 ? effect.turnBudget : 1,
                stageStat = effect.stageStat,
                stageAmount = effect.stageAmount,
                chance = effect.chance > 0f ? effect.chance : 1f,
                stackKey = key,
                sourceTag = move.definitionId ?? move.displayName,
            });
            _log?.LogMessage(CombatEventType.SystemMessage,
                $"{move.displayName} created a {effect.turnBudget}-turn entry field.", turn);
        }

        private void ApplyLifesteal(
            ChainlingInstance source,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (source == null || context == null || context.damageDealt <= 0)
                return;
            float fraction = effect.magnitude > 1f ? effect.magnitude / 100f : effect.magnitude;
            int requested = Math.Max(1, (int)Math.Floor(context.damageDealt * Math.Max(0f, fraction)));
            Heal(source, source, move, requested, turn, "lifesteal");
        }

        private void ApplyStageThresholdHeal(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            CombatRng rng,
            UtilityEffectExecutionContext context)
        {
            ChainlingInstance healTarget = ResolveUtilityTarget(source, target, effect, rng, context);
            if (healTarget == null || healTarget.GetCombatStage(effect.stageStat) < effect.threshold)
                return;
            float pct = effect.healPercentMaxHP > 0f ? effect.healPercentMaxHP : effect.magnitude;
            if (pct <= 1f)
                pct *= 100f;
            int requested = Math.Max(1, (int)Math.Floor(healTarget.EffectiveMaxHP * pct / 100f));
            Heal(source, healTarget, move, requested, turn, "stage threshold");
        }

        public bool ApplyArmedWindowEffects(
            ChainlingInstance owner,
            TeamState ownerTeam,
            ChainlingInstance eventSource,
            ArmedUtilityWindow window,
            int turn)
        {
            if (owner == null || window == null)
                return false;
            bool applied = false;
            if (window.apDelta > 0)
                applied |= (_apService?.Gain(owner, window.apDelta, turn, window.sourceTag) ?? 0) > 0;
            else if (window.apDelta < 0)
                applied |= (_apService?.Drain(owner, Math.Abs(window.apDelta), turn, window.sourceTag) ?? 0) > 0;

            if (window.healPercentMaxHP > 0f)
            {
                if (window.affectAllAllies && ownerTeam != null)
                {
                    var allies = ownerTeam.GetActiveChainlings();
                    for (int i = 0; i < allies.Count; i++)
                        applied |= HealPercent(owner, allies[i], window.healPercentMaxHP, window.sourceTag, turn);
                }
                else
                {
                    applied |= HealPercent(owner, owner, window.healPercentMaxHP, window.sourceTag, turn);
                }
            }

            if (window.stageAmount != 0)
            {
                var payload = new UtilityEffectPayload
                {
                    stageStat = window.stageStat,
                    stageAmount = window.stageAmount,
                    stageCap = window.stageCap,
                };
                var pseudoMove = new MoveInstance { definitionId = window.sourceTag, displayName = window.sourceTag };
                int before = owner.GetCombatStage(window.stageStat);
                ApplyStageDelta(owner, owner, pseudoMove, payload, turn);
                applied |= owner.GetCombatStage(window.stageStat) != before;
            }

            if (eventSource != null && eventSource.isAlive && window.counterDamagePercentMaxHP > 0f)
            {
                float fraction = window.counterDamagePercentMaxHP > 1f
                    ? window.counterDamagePercentMaxHP / 100f
                    : window.counterDamagePercentMaxHP;
                int damage = Math.Max(1, (int)Math.Floor(eventSource.EffectiveMaxHP * fraction));
                eventSource.currentHP = Math.Max(0, eventSource.currentHP - damage);
                if (eventSource.currentHP <= 0)
                    eventSource.isAlive = false;
                _log?.LogMessage(CombatEventType.PassiveDamageDealt,
                    $"{owner.displayName} countered {eventSource.displayName} for {damage} damage.", turn);
                applied = true;
            }
            return applied;
        }

        private bool HealPercent(ChainlingInstance source, ChainlingInstance target, float percent, string sourceTag, int turn)
        {
            if (target == null || !target.isAlive)
                return false;
            float normalized = percent > 1f ? percent / 100f : percent;
            int requested = Math.Max(1, (int)Math.Floor(target.EffectiveMaxHP * normalized));
            var pseudoMove = new MoveInstance { definitionId = sourceTag, displayName = sourceTag };
            return Heal(source, target, pseudoMove, requested, turn, sourceTag);
        }

        private bool Heal(ChainlingInstance source, ChainlingInstance target, MoveInstance move, int requested, int turn, string reason)
        {
            if (target == null || !target.isAlive || requested <= 0)
                return false;
            int before = target.currentHP;
            target.currentHP = Math.Min(target.EffectiveMaxHP, target.currentHP + requested);
            int actual = target.currentHP - before;
            if (actual <= 0)
                return false;
            _log?.Log(new CombatEvent
            {
                eventType = CombatEventType.HealApplied,
                turn = turn,
                sourceId = source != null ? source.instanceId : string.Empty,
                targetId = target.instanceId,
                moveId = move != null ? move.instanceId : string.Empty,
                intValue = actual,
                message = $"{target.displayName} healed {actual} HP ({reason}).",
            });
            return true;
        }

        private static void RefreshOrAddWindow(
            System.Collections.Generic.List<ArmedUtilityWindow> windows,
            ArmedUtilityWindow incoming)
        {
            if (windows == null || incoming == null)
                return;
            if (!string.IsNullOrWhiteSpace(incoming.stackKey))
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    ArmedUtilityWindow existing = windows[i];
                    if (existing != null && string.Equals(existing.stackKey, incoming.stackKey, StringComparison.Ordinal))
                    {
                        windows[i] = incoming;
                        return;
                    }
                }
            }
            windows.Add(incoming);
        }

        private static bool WasArmedTriggerAlreadySatisfied(
            ArmedWindowTrigger trigger,
            UtilityEffectExecutionContext context)
        {
            if (context?.ledger == null)
                return false;
            if (trigger == ArmedWindowTrigger.FoeKO && context.targetTeam != null)
                return context.ledger.DidTeamSufferKoThisTurn(context.targetTeam.teamIndex);
            if (trigger == ArmedWindowTrigger.EnemyMiss && context.targetTeam != null)
            {
                var foes = context.targetTeam.GetActiveChainlings();
                for (int i = 0; i < foes.Count; i++)
                {
                    if (foes[i] != null && context.ledger.DidActorMissThisTurn(foes[i].instanceId))
                        return true;
                }
            }
            return false;
        }

        private static bool TryCoreStat(CombatStageStat stageStat, out CoreStat coreStat)
        {
            switch (stageStat)
            {
                case CombatStageStat.MeleeAtk: coreStat = CoreStat.MeleeAtk; return true;
                case CombatStageStat.MeleeDef: coreStat = CoreStat.MeleeDef; return true;
                case CombatStageStat.RangedAtk: coreStat = CoreStat.RangedAtk; return true;
                case CombatStageStat.RangedDef: coreStat = CoreStat.RangedDef; return true;
                case CombatStageStat.Speed: coreStat = CoreStat.Speed; return true;
                default: coreStat = CoreStat.MaxHP; return false;
            }
        }

        private void ApplyCleanse(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            if (_statusProcessor == null)
                return;

            ChainlingInstance cleanseTarget = ResolveUtilityTarget(source, target, effect);
            if (cleanseTarget == null)
                return;

            int maxCleanse = effect.apGain > 0 ? effect.apGain : Math.Max(1, (int)Math.Round(effect.magnitude));
            if (maxCleanse <= 0)
                maxCleanse = 1;

            int cleansed = _statusProcessor.Cleanse(cleanseTarget, maxCleanse, turn);
            if (cleansed > 0)
            {
                _log?.LogMessage(
                    CombatEventType.SystemMessage,
                    $"{cleanseTarget.displayName} cleansed {cleansed} debuff{(cleansed == 1 ? string.Empty : "s")} ({move.displayName}).",
                    turn);
            }
        }

        private void ApplyBonusHealIfFaster(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            TeamState opposingTeam)
        {
            if (!IsFasterThanAnyActiveFoe(source, opposingTeam))
                return;

            float bonusPercent = effect.magnitude;
            if (bonusPercent <= 0f)
                return;
            if (bonusPercent > 1f)
                bonusPercent /= 100f;

            ChainlingInstance healTarget = ResolveUtilityTarget(source, target, effect);
            if (healTarget == null || !healTarget.isAlive)
                return;

            int healAmount = Math.Max(1, (int)(healTarget.EffectiveMaxHP * bonusPercent));
            int before = healTarget.currentHP;
            healTarget.currentHP = Math.Min(healTarget.EffectiveMaxHP, healTarget.currentHP + healAmount);
            int actual = healTarget.currentHP - before;
            if (actual <= 0)
                return;

            _log?.Log(new CombatEvent
            {
                eventType = CombatEventType.HealApplied,
                turn = turn,
                sourceId = source.instanceId,
                targetId = healTarget.instanceId,
                moveId = move.instanceId,
                intValue = actual,
                message = $"{healTarget.displayName} healed {actual} HP (speed bonus {move.displayName})",
            });
            _log?.LogMessage(
                CombatEventType.HPHealed,
                $"{healTarget.displayName} healed {actual} HP from {move.displayName} (faster than foe)",
                turn);
        }

        private static bool IsFasterThanAnyActiveFoe(ChainlingInstance source, TeamState opposingTeam)
        {
            if (source == null || opposingTeam == null)
                return false;

            int sourceSpeed = source.GetStatBreakdown(CoreStat.Speed).finalInt;
            var foes = opposingTeam.GetActiveChainlings();
            for (int i = 0; i < foes.Count; i++)
            {
                ChainlingInstance foe = foes[i];
                if (foe == null || !foe.isAlive)
                    continue;

                if (sourceSpeed > foe.GetStatBreakdown(CoreStat.Speed).finalInt)
                    return true;
            }

            return false;
        }

        private void ApplyNextMoveApSurcharge(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            ChainlingInstance surchargeTarget = ResolveUtilityTarget(source, target, effect);
            if (surchargeTarget == null)
                return;

            int amount = ResolveApAmount(effect);
            if (amount <= 0)
                return;

            surchargeTarget.nextMoveApSurcharge = Math.Max(surchargeTarget.nextMoveApSurcharge, amount);
            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{surchargeTarget.displayName} next move costs +{amount} AP ({move.displayName}).",
                turn);
        }

        private void QueueTurnStartStatus(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            ChainlingInstance statusTarget = ResolveUtilityTarget(source, target, effect);
            if (statusTarget == null || effect.statusId == StatusId.None)
                return;

            int duration = effect.statusDuration > 0 ? effect.statusDuration : 1;
            if (statusTarget.pendingTurnStartStatuses == null)
                statusTarget.pendingTurnStartStatuses = new System.Collections.Generic.List<PendingStatusApplication>();

            statusTarget.pendingTurnStartStatuses.Add(new PendingStatusApplication
            {
                statusId = effect.statusId,
                duration = duration,
                sourceId = move.definitionId ?? move.displayName,
                applyOnTurn = turn + 1,
            });

            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{statusTarget.displayName} will gain {effect.statusId} next turn.",
                turn);
        }

        private void ApplyMitigationWindow(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            float mitigation = effect.magnitude;
            if (mitigation > 1f)
                mitigation /= 100f;
            if (mitigation <= 0f)
                return;

            int turns = effect.turnBudget > 0 ? effect.turnBudget : 1;
            int hits = effect.hitBudget;
            string stackKey = !string.IsNullOrWhiteSpace(effect.stackKey)
                ? effect.stackKey.Trim()
                : $"{move.definitionId}:{effect.effectType}";

            int apDrain = CombatWallEffectUtility.ResolveWallHitApDrainFromMove(move);
            target.AddMitigationSource(
                mitigation,
                turns,
                hits,
                move.definitionId ?? move.displayName,
                stackKey,
                apDrain);

            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{target.displayName} gained {MathfRoundPercent(mitigation)} mitigation ({turns}T).",
                turn);
        }

        private void ApplyReactiveApWindow(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            int apGain = effect.apGain > 0 ? effect.apGain : 1;
            int turns = effect.turnBudget > 0 ? effect.turnBudget : 1;
            string stackKey = !string.IsNullOrWhiteSpace(effect.stackKey)
                ? effect.stackKey.Trim()
                : $"{move.definitionId}:{effect.effectType}";

            target.AddReactiveApWindow(
                turnsRemaining: turns,
                apGain: apGain,
                requireSpeedHigher: true,
                sourceTag: move.definitionId ?? move.displayName,
                stackKey: stackKey);

            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{target.displayName} gained reactive AP window (+{apGain} AP, {turns} turn).",
                turn);
        }

        private void ApplyFullAbsorbBarrier(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            int turns = effect.turnBudget > 0 ? effect.turnBudget : 3;
            int hits = effect.hitBudget;
            if (hits == 0)
                hits = 3;
            string sourceTag = !string.IsNullOrWhiteSpace(move.definitionId)
                ? move.definitionId
                : move.displayName;
            int apDrain = CombatWallEffectUtility.ResolveWallHitApDrainFromMove(move);

            bool replaced = target.AddBarrier(
                BarrierType.SingleHit,
                hitsRemaining: hits,
                damageThreshold: 0,
                turnsRemaining: turns,
                sourceTag: sourceTag,
                reflectPercent: 0f,
                attackerApDrainOnHit: apDrain);

            string replaceNote = replaced ? " (replaced prior wall/shield)" : string.Empty;
            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{target.displayName} gained a full-absorb barrier ({turns}T/{FormatHits(hits)}){replaceNote}.",
                turn);
        }

        private void ApplyDamageThresholdBarrier(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn)
        {
            int pool = Math.Max(0, (int)Math.Round(effect.magnitude));
            if (pool <= 0)
                return;

            int turns = effect.turnBudget > 0 ? effect.turnBudget : 1;
            float reflectPercent = effect.reflectPercent > 1f
                ? effect.reflectPercent / 100f
                : effect.reflectPercent;
            string sourceTag = !string.IsNullOrWhiteSpace(move.definitionId)
                ? move.definitionId
                : move.displayName;
            int apDrain = CombatWallEffectUtility.ResolveWallHitApDrainFromMove(move);

            bool replaced = target.AddBarrier(
                BarrierType.DamageThreshold,
                hitsRemaining: 0,
                damageThreshold: pool,
                turnsRemaining: turns,
                sourceTag: sourceTag,
                reflectPercent: reflectPercent,
                attackerApDrainOnHit: apDrain);

            string replaceNote = replaced ? " (replaced prior wall/shield)" : string.Empty;
            _log?.LogMessage(
                CombatEventType.SystemMessage,
                $"{target.displayName} gained a {pool}-damage barrier ({turns}T, reflect {MathfRoundPercent(reflectPercent)}){replaceNote}.",
                turn);
        }

        private static string MathfRoundPercent(float mitigation)
        {
            int pct = (int)Math.Round(Math.Max(0f, mitigation) * 100f);
            return $"{pct}%";
        }

        private static string FormatHits(int hits)
        {
            if (hits < 0)
                return "unlimited hits";
            return $"{hits} hit{(hits == 1 ? string.Empty : "s")}";
        }

        private void DrainApEffect(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            int amount = ResolveApAmount(effect);
            if (amount <= 0 || _apService == null)
                return;

            if (effect != null && effect.targetOverride == MoveTargeting.AllEnemies)
            {
                TeamState foes = context?.targetTeam;
                if (foes == null)
                    return;
                System.Collections.Generic.List<ChainlingInstance> activeFoes = foes.GetActiveChainlings();
                if (activeFoes == null || activeFoes.Count == 0)
                    return;
                bool higherApOnly = string.Equals(move.definitionId, "fee_collector", StringComparison.Ordinal);
                for (int i = 0; i < activeFoes.Count; i++)
                {
                    ChainlingInstance foe = activeFoes[i];
                    if (foe == null || !foe.isAlive || foe == source)
                        continue;
                    if (higherApOnly && foe.currentAP <= source.currentAP)
                        continue;
                    int drained = _apService.Drain(foe, amount, turn, move.definitionId);
                    LogDrain(source, foe, move, drained, turn);
                }
                return;
            }

            ChainlingInstance resolvedTarget = ResolveUtilityTarget(source, target, effect);
            int resolvedDrain = _apService.Drain(resolvedTarget, amount, turn, move.definitionId);
            LogDrain(source, resolvedTarget, move, resolvedDrain, turn);
        }

        private void ApplyChemistryStatus(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (_statusProcessor == null || effect.statusId == StatusId.None)
                return;
            ChainlingInstance statusTarget = ResolveUtilityTarget(source, target, effect, null, context);
            if (statusTarget == null)
                return;
            _statusProcessor.ApplyStatus(
                statusTarget,
                effect.statusId,
                Math.Max(1, effect.statusDuration),
                move.definitionId ?? move.displayName,
                turn,
                bypassChemistryCheck: true);
        }

        private void ApplyStripBuffs(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (_statusProcessor == null)
                return;
            ChainlingInstance stripTarget = ResolveUtilityTarget(source, target, effect, null, context);
            int maxCount = Math.Max(1, (int)Math.Round(effect.magnitude));
            int removed = _statusProcessor.StripBuffs(stripTarget, maxCount, turn);
            if (context != null)
                context.lastStatusStripCount = removed;
            if (removed > 0)
                _log?.LogMessage(CombatEventType.SystemMessage,
                    $"{move.displayName} stripped {removed} buff{(removed == 1 ? string.Empty : "s")}.", turn);
        }

        private void ApplyStripAllStatuses(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (_statusProcessor == null)
                return;
            ChainlingInstance stripTarget = ResolveUtilityTarget(source, target, effect, null, context);
            int removed = _statusProcessor.StripAllGeneralStatuses(stripTarget, turn);
            if (context != null)
                context.lastStatusStripCount = removed;
            if (removed > 0)
                _log?.LogMessage(CombatEventType.SystemMessage,
                    $"{move.displayName} stripped {removed} status{(removed == 1 ? string.Empty : "es")}.", turn);
        }

        private void ApplyStripBuffsThenStage(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (_statusProcessor == null)
                return;
            ChainlingInstance stripTarget = ResolveUtilityTarget(source, target, effect, null, context);
            int maxCount = Math.Max(1, (int)Math.Round(effect.magnitude));
            int removed = _statusProcessor.StripBuffs(stripTarget, maxCount, turn);
            if (context != null)
                context.lastStatusStripCount = removed;
            if (removed <= 0)
                return;

            var stageEffect = new UtilityEffectPayload
            {
                stageStat = effect.stageStat,
                stageAmount = effect.stageAmount * removed,
                stageCap = 6,
            };
            ApplyStageDelta(source, source, move, stageEffect, turn);
            _log?.LogMessage(CombatEventType.SystemMessage,
                $"{move.displayName} stripped {removed} buff{(removed == 1 ? string.Empty : "s")} and raised {effect.stageStat} by {effect.stageAmount * removed}.",
                turn);
        }

        private void ApplyHealSelfPerTargetDebuff(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            UtilityEffectPayload effect,
            int turn,
            UtilityEffectExecutionContext context)
        {
            if (_statusProcessor == null || source == null)
                return;
            ChainlingInstance debuffedTarget = ResolveUtilityTarget(source, target, effect, null, context);
            int count = _statusProcessor.CountGeneralDebuffs(debuffedTarget);
            if (count <= 0)
                return;
            HealPercent(source, source, effect.magnitude * count, move.definitionId, turn);
        }

        private void LogDrain(
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int drained,
            int turn)
        {
            if (drained <= 0 || target == null)
                return;
            target.lastApDrainSourceName = source != null ? source.displayName : move?.displayName ?? "an AP drain";
            target.lastApDrainTurn = turn;
            _log?.LogMessage(
                CombatEventType.APSpent,
                $"{(source != null ? source.displayName : move?.displayName ?? "The effect")} drained {drained} AP from {target.displayName}.",
                turn);
        }

        private static int ResolveApAmount(UtilityEffectPayload effect)
        {
            if (effect == null)
                return 0;
            if (effect.apGain > 0)
                return effect.apGain;
            return Math.Max(0, (int)Math.Round(effect.magnitude));
        }

        private static ChainlingInstance ResolveUtilityTarget(
            ChainlingInstance source,
            ChainlingInstance target,
            UtilityEffectPayload effect,
            CombatRng rng = null,
            UtilityEffectExecutionContext context = null)
        {
            if (effect != null && effect.targetOverride == MoveTargeting.Self)
                return source;
            if (effect != null && effect.targetOverride == MoveTargeting.RandomEnemy && context?.targetTeam != null)
            {
                var foes = context.targetTeam.GetActiveChainlings();
                if (foes.Count == 0)
                    return null;
                int index = rng != null ? rng.NextInt(0, foes.Count - 1) : 0;
                return foes[index];
            }
            return target;
        }

        private bool ShouldApplyEffect(
            UtilityEffectPayload effect,
            CombatRng rng,
            ChainlingInstance source,
            ChainlingInstance target,
            MoveInstance move,
            int turn)
        {
            float chance = effect != null && effect.chance > 0f ? effect.chance : 1f;
            if (chance >= 1f || rng == null)
                return true;

            float roll;
            bool triggered = rng.Check(chance, out roll);
            _log?.LogForensic(
                CombatForensicEventType.ProcRoll,
                new ProcRollPayload
                {
                    procId = $"UtilityEffect.{move.instanceId}.{effect.effectType}",
                    chance = chance,
                    roll = roll,
                    triggered = triggered,
                },
                source != null ? source.instanceId : string.Empty,
                target != null ? new System.Collections.Generic.List<string> { target.instanceId } : null,
                turn);
            return triggered;
        }
    }
}
