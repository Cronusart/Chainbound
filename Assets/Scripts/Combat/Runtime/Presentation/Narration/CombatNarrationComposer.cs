using System;
using System.Collections.Generic;
using System.Text;

namespace Chainbound.Combat
{
    public static class CombatNarrationComposer
    {
        private sealed class DamageSummary
        {
            public string targetId;
            public int damage;
            public int hitCount;
            public bool critical;
            public bool knockedOut;
            public readonly List<string> modifierPrefixes = new List<string>();
        }

        private sealed class HealSummary
        {
            public string targetId;
            public int amount;
        }

        private enum ModifierNarrationFamily
        {
            Fallback,
            Prefix,
            Heal,
            ApGain,
            ApDrain,
            StageRaise,
            StageLower,
            Status,
            Cleanse,
            Damage,
            Guard,
            Accuracy,
            BarrierHits,
            BarrierPool,
            Cast,
            Reflect,
            Share,
            BonusAction,
        }

        private sealed class EffectRecord
        {
            public int eventIndex;
            public CombatEvent evt;
            public int consequenceIndex = -1;
        }

        private sealed class ModifierNarrationAggregate
        {
            public ModifierOutcomePayload payload;
            public ModifierNarrationFamily family;
            public CombatEvent effectEvent;
            public int amount;
        }

        public static CombatNarrationActionPlan ComposeAction(
            CombatEventLog log,
            int fromIndex,
            int toExclusive,
            CombatNarrationProfileSO profile,
            int turnIndex,
            int actionIndex,
            bool livePresentationEnabled = true)
        {
            if (log == null || fromIndex < 0 || toExclusive <= fromIndex || toExclusive > log.EventCount)
                return null;

            IReadOnlyList<CombatEvent> events = log.Events;
            var plan = new CombatNarrationActionPlan
            {
                firstEventTimestamp = events[fromIndex].timestamp,
                lastEventTimestamp = events[toExclusive - 1].timestamp,
                turnIndex = turnIndex,
                actionIndex = actionIndex,
                livePresentationEnabled = livePresentationEnabled,
            };

            string speaker = GetSpeaker(profile);
            float actionHold = profile != null ? profile.ActionLeadSeconds : 0.4f;
            float resolutionHold = profile != null ? profile.PrimaryResolutionSeconds : 1.7f;
            float consequenceHold = profile != null ? profile.ConsequenceSeconds : 1.45f;
            float effectLockout = profile != null ? profile.EffectPageSkipLockoutSeconds : 1.15f;

            CombatEvent? actionEvent = null;
            var damageByTarget = new Dictionary<string, DamageSummary>(StringComparer.Ordinal);
            var healsByTarget = new Dictionary<string, HealSummary>(StringComparer.Ordinal);
            var damageTargetOrder = new List<string>();
            var healTargetOrder = new List<string>();
            var wallSentences = new List<string>();
            var primarySentences = new List<string>();
            var consequenceSentences = new List<string>();
            var transcriptOnlySentences = new List<string>();
            var modifierAggregates = new Dictionary<string, ModifierNarrationAggregate>(StringComparer.Ordinal);
            var modifierAggregateOrder = new List<string>();
            var koTargets = new HashSet<string>(StringComparer.Ordinal);
            var koTargetOrder = new List<string>();
            string lastDrainTargetId = string.Empty;
            int lastDrainAmount = 0;
            EffectRecord lastEffect = null;

            for (int i = fromIndex; i < toExclusive; i++)
            {
                CombatEvent evt = events[i];
                if (evt.eventType == CombatEventType.APSpent &&
                    string.Equals(evt.apMutationSource, CombatApMutationSource.Drain.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    lastDrainTargetId = evt.targetId;
                    lastDrainAmount = Math.Abs(evt.intValue);
                }
                if (IsModifierEffectEvent(evt.eventType))
                    lastEffect = new EffectRecord { eventIndex = i, evt = evt };
                CombatNarrationDelivery delivery = ResolveDelivery(profile, evt.eventType);
                if (IsReadableActionInterruption(evt))
                    delivery = CombatNarrationDelivery.Live;
                if (delivery == CombatNarrationDelivery.Silent || delivery == CombatNarrationDelivery.PopupOrHudOnly)
                    continue;
                if (delivery == CombatNarrationDelivery.TranscriptOnly)
                {
                    if (!string.IsNullOrWhiteSpace(evt.message))
                        transcriptOnlySentences.Add(NormalizeSentence(evt.message));
                    continue;
                }

                if (!actionEvent.HasValue &&
                    (IsActionEvent(evt.eventType) ||
                     IsReadableActionInterruption(evt) ||
                     (delivery == CombatNarrationDelivery.Live && !string.IsNullOrWhiteSpace(evt.message))))
                    actionEvent = evt;

                switch (evt.eventType)
                {
                    case CombatEventType.DamageDealt:
                    case CombatEventType.PassiveDamageDealt:
                        AppendWallInterceptNarration(evt, log, profile, wallSentences, consequenceSentences);
                        AddDamage(damageByTarget, damageTargetOrder, evt);
                        break;

                    case CombatEventType.HPDotDamage:
                    case CombatEventType.HPRecoilDamage:
                        if (evt.intValue > 0 && !string.IsNullOrEmpty(evt.targetId))
                        {
                            AddDamage(damageByTarget, damageTargetOrder, evt);
                            if (evt.eventType == CombatEventType.HPDotDamage && StartsNamedTick(evt.message))
                                primarySentences.Add(NormalizeSentence(evt.message));
                        }
                        else if (!string.IsNullOrWhiteSpace(evt.message))
                            primarySentences.Add(NormalizeSentence(evt.message));
                        break;

                    case CombatEventType.HealApplied:
                    case CombatEventType.HPHealed:
                        AddHeal(healsByTarget, healTargetOrder, evt);
                        break;

                    case CombatEventType.ChainlingKO:
                        if (!string.IsNullOrEmpty(evt.targetId) && koTargets.Add(evt.targetId))
                            koTargetOrder.Add(evt.targetId);
                        break;

                    case CombatEventType.Miss:
                        primarySentences.Add(Format(Get(profile, p => p.MissTemplate, "{actor}'s attack missed {target}."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.Dodge:
                        primarySentences.Add(Format(Get(profile, p => p.DodgeTemplate, "{target} dodged the attack."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.BarrierBlocked:
                        AppendWallInterceptNarration(evt, log, profile, wallSentences, consequenceSentences);
                        break;

                    case CombatEventType.WallExpired:
                        AppendWallExpiredNarration(evt, log, wallSentences);
                        break;

                    case CombatEventType.BlockIgnoredDot:
                        if (!string.IsNullOrWhiteSpace(evt.message))
                            primarySentences.Add(NormalizeSentence(evt.message));
                        break;

                    case CombatEventType.SwapPerformed:
                    case CombatEventType.AutoSwapIn:
                        primarySentences.Add(Format(Get(profile, p => p.SwapResultTemplate, "{target} entered the battle."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.StatusApplied:
                    case CombatEventType.ChemistryStatusApplied:
                        if (lastEffect != null && lastEffect.eventIndex == i)
                            lastEffect.consequenceIndex = consequenceSentences.Count;
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusAppliedTemplate, "{target} became {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusRefreshed:
                        if (lastEffect != null && lastEffect.eventIndex == i)
                            lastEffect.consequenceIndex = consequenceSentences.Count;
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusRefreshedTemplate, "{target}'s {status} was refreshed."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusImmune:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusImmuneTemplate, "{target} resisted {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusCleansed:
                    case CombatEventType.StatusConsumed:
                        if (lastEffect != null && lastEffect.eventIndex == i)
                            lastEffect.consequenceIndex = consequenceSentences.Count;
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusCleansedTemplate, "{target} was cleansed of {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusExpired:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusExpiredTemplate, "{target}'s {status} ended."), evt, log, 0, 0));
                        break;

                    case CombatEventType.StageChanged:
                        if (lastEffect != null && lastEffect.eventIndex == i)
                            lastEffect.consequenceIndex = consequenceSentences.Count;
                        consequenceSentences.Add(Format(Get(profile, p => p.StageChangedTemplate, "{target}'s {stat} changed by {amount}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.PassiveTriggered:
                        consequenceSentences.Add(Format(Get(profile, p => p.PassiveTemplate, "{actor}'s passive activated."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.ModifierTriggered:
                    {
                        if (TrySetupFeedSentence(evt, out string setupFeedSentence))
                            consequenceSentences.Add(setupFeedSentence);
                        else if (ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload modifierPayload))
                        {
                            if (!modifierPayload.gameplayApplied)
                            {
                                string unmet = FormatUnmetConditional(modifierPayload, log);
                                if (!string.IsNullOrWhiteSpace(unmet))
                                    transcriptOnlySentences.Add(unmet);
                            }
                            else if (IsNarratedModifierCategory(modifierPayload.category))
                            {
                                AddModifierNarration(
                                    modifierPayload,
                                    lastEffect,
                                    damageByTarget,
                                    healsByTarget,
                                    consequenceSentences,
                                    modifierAggregates,
                                    modifierAggregateOrder,
                                    log);
                            }
                        }
                        lastEffect = null;
                        break;
                    }

                    case CombatEventType.APGainBlocked:
                        consequenceSentences.Add(Format(Get(profile, p => p.ApBlockedTemplate, "{target} could not gain AP."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.BuffBlocked:
                    case CombatEventType.BossTell:
                        if (!string.IsNullOrWhiteSpace(evt.message))
                            consequenceSentences.Add(evt.message);
                        break;

                    case CombatEventType.APStolen:
                    {
                        int amount = Math.Max(Math.Abs(evt.intValue), lastDrainAmount);
                        if (lastEffect != null && lastEffect.eventIndex == i)
                            lastEffect.consequenceIndex = consequenceSentences.Count;
                        if (!string.IsNullOrWhiteSpace(lastDrainTargetId))
                        {
                            string victim = ResolveName(log, lastDrainTargetId);
                            string template = Get(profile, p => p.ApStealTemplate, "{actor} stole {amount} AP from {target}.");
                            var stealEvent = new CombatEvent { sourceId = evt.targetId, targetId = lastDrainTargetId };
                            consequenceSentences.Add(Format(template, stealEvent, log, amount, 0));
                        }
                        else
                        {
                            string template = Get(profile, p => p.ApStolenTemplate, "{target} stole {amount} AP.");
                            consequenceSentences.Add(Format(template, evt, log, amount, 0));
                        }
                        lastDrainTargetId = string.Empty;
                        lastDrainAmount = 0;
                        break;
                    }

                    case CombatEventType.MoveRecharging:
                        consequenceSentences.Add(!string.IsNullOrWhiteSpace(evt.message)
                            ? NormalizeSentence(evt.message)
                            : Format(Get(profile, p => p.RechargingTemplate, "{move} began recharging."), evt, log, 0, 0));
                        break;

                    case CombatEventType.BonusActionGranted:
                        consequenceSentences.Add(Format(Get(profile, p => p.BonusActionTemplate, "{actor} gained another action."),
                            evt, log, evt.intValue, 0));
                        break;

                    default:
                        if (delivery == CombatNarrationDelivery.OutcomeSummary && !string.IsNullOrWhiteSpace(evt.message))
                            consequenceSentences.Add(NormalizeSentence(evt.message));
                        break;
                }
            }

            for (int i = 0; i < koTargetOrder.Count; i++)
            {
                string targetId = koTargetOrder[i];
                if (damageByTarget.TryGetValue(targetId, out DamageSummary damage))
                    damage.knockedOut = true;
                else
                {
                    var koEvent = FindTargetEvent(events, fromIndex, toExclusive, CombatEventType.ChainlingKO, targetId);
                    primarySentences.Add(Format(Get(profile, p => p.KoTemplate, "{target} was knocked out!"),
                        koEvent, log, 0, 0));
                }
            }

            // Wall lines first, then residual HP damage, then other resolution lines.
            var resolutionSentences = new List<string>(wallSentences.Count + damageTargetOrder.Count + primarySentences.Count + 4);
            resolutionSentences.AddRange(wallSentences);
            for (int i = 0; i < damageTargetOrder.Count; i++)
            {
                DamageSummary damage = damageByTarget[damageTargetOrder[i]];
                if (damage.damage > 0)
                    resolutionSentences.Add(FormatDamage(damage, log, profile));
            }
            resolutionSentences.AddRange(primarySentences);

            for (int i = 0; i < healTargetOrder.Count; i++)
            {
                HealSummary heal = healsByTarget[healTargetOrder[i]];
                if (heal.amount <= 0)
                    continue;
                var evt = new CombatEvent { targetId = heal.targetId };
                resolutionSentences.Add(Format(Get(profile, p => p.HealTemplate, "{target} recovered {amount} HP."),
                    evt, log, heal.amount, 0));
            }

            int liveModifierConsequences = 0;
            var modifierConsequenceSentences = new List<string>();
            for (int i = 0; i < modifierAggregateOrder.Count; i++)
            {
                ModifierNarrationAggregate aggregate = modifierAggregates[modifierAggregateOrder[i]];
                string sentence = FormatModifierAggregate(aggregate, log, profile);
                if (string.IsNullOrWhiteSpace(sentence))
                    continue;

                if (aggregate.family == ModifierNarrationFamily.Heal ||
                    aggregate.family == ModifierNarrationFamily.Damage)
                {
                    resolutionSentences.Add(sentence);
                }
                else if (liveModifierConsequences < 3)
                {
                    modifierConsequenceSentences.Add(sentence);
                    liveModifierConsequences++;
                }
                else
                {
                    transcriptOnlySentences.Add(sentence);
                }
            }
            if (modifierConsequenceSentences.Count > 0)
                consequenceSentences.InsertRange(0, modifierConsequenceSentences);

            string actionText = BuildActionText(actionEvent, log, profile, IsMultiStrikeAction(events, fromIndex, toExclusive));
            string resolutionText = JoinSentences(resolutionSentences);
            string consequenceText = JoinSentences(consequenceSentences);

            if (!string.IsNullOrWhiteSpace(actionText))
                plan.actionPage = NewPage(CombatNarrationPageKind.Action, speaker, actionText, actionHold);
            if (!string.IsNullOrWhiteSpace(resolutionText))
                plan.resolutionPage = NewPage(CombatNarrationPageKind.Resolution, speaker, resolutionText, resolutionHold, effectLockout);
            if (!string.IsNullOrWhiteSpace(consequenceText))
                plan.consequencePage = NewPage(CombatNarrationPageKind.Consequence, speaker, consequenceText, consequenceHold, effectLockout);

            AppendTranscript(plan, plan.actionPage);
            AppendTranscript(plan, plan.resolutionPage);
            AppendTranscript(plan, plan.consequencePage);
            for (int i = 0; i < transcriptOnlySentences.Count; i++)
                AppendTranscript(plan, NewPage(CombatNarrationPageKind.System, speaker, transcriptOnlySentences[i], 0f));

            return plan;
        }

        public static CombatNarrationTranscriptEntry ComposeBattleResult(
            BattleResult result,
            CombatNarrationProfileSO profile,
            int sequenceIndex)
        {
            if (result == null)
                return null;

            BattleOutcomePresentation outcome = BattleOutcomePresentation.Resolve(result);
            string template;
            switch (outcome.Kind)
            {
                case BattleOutcomeKind.Capture:
                    template = "Signal Lock complete. " + FormatCapturedName(result) + " joined your collection.";
                    break;
                case BattleOutcomeKind.Escaped:
                    template = "The wild Chainling escaped the encounter.";
                    break;
                case BattleOutcomeKind.Withdrawn:
                    template = Get(profile, p => p.FledTemplate, "Your team withdrew from battle.");
                    break;
                case BattleOutcomeKind.Victory:
                    template = Get(profile, p => p.VictoryTemplate, "Victory! Your team won in {turns} turns.");
                    if (outcome.DidCapture)
                    {
                        List<BattleCaptureRecord> captures = BattleOutcomePresentation.GetSuccessfulCaptures(result);
                        string verb = captures.Count > 1 ? " were recorded." : " was recorded.";
                        template += " " + FormatCapturedName(result) + verb;
                    }
                    break;
                case BattleOutcomeKind.Defeat:
                    template = Get(profile, p => p.DefeatTemplate, "Defeat. The opposing team won in {turns} turns.");
                    break;
                case BattleOutcomeKind.Draw:
                    template = Get(profile, p => p.DrawTemplate, "The battle ended in a draw after {turns} turns.");
                    break;
                default:
                    template = Get(profile, p => p.DrawTemplate, "The battle ended after {turns} turns.");
                    break;
            }

            return new CombatNarrationTranscriptEntry
            {
                turnIndex = Math.Max(0, result.turnCount),
                actionIndex = -1,
                sequenceIndex = sequenceIndex,
                kind = CombatNarrationPageKind.BattleResult,
                speaker = GetSpeaker(profile),
                text = Replace(template, "turns", Math.Max(0, result.turnCount).ToString()),
            };
        }

        private static string FormatCapturedName(BattleResult result)
        {
            string name = BattleOutcomePresentation.ResolveCapturedDisplayName(result);
            if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "the wild Chainling", StringComparison.Ordinal))
                return "The wild Chainling";
            return name;
        }

        private static void AddDamage(
            Dictionary<string, DamageSummary> byTarget,
            List<string> targetOrder,
            CombatEvent evt)
        {
            if (evt.intValue <= 0)
                return;
            string key = evt.targetId ?? string.Empty;
            if (!byTarget.TryGetValue(key, out DamageSummary summary))
            {
                summary = new DamageSummary { targetId = key };
                byTarget.Add(key, summary);
                targetOrder.Add(key);
            }

            summary.damage += Math.Max(0, evt.intValue);
            summary.hitCount++;
            summary.critical |= evt.boolValue;
        }

        private static void AddHeal(
            Dictionary<string, HealSummary> byTarget,
            List<string> targetOrder,
            CombatEvent evt)
        {
            if (evt.intValue <= 0)
                return;
            string key = evt.targetId ?? string.Empty;
            if (!byTarget.TryGetValue(key, out HealSummary summary))
            {
                summary = new HealSummary { targetId = key };
                byTarget.Add(key, summary);
                targetOrder.Add(key);
            }

            summary.amount += Math.Max(0, evt.intValue);
        }

        private static string FormatDamage(DamageSummary damage, CombatEventLog log, CombatNarrationProfileSO profile)
        {
            string template;
            if (damage.knockedOut)
            {
                template = damage.hitCount > 1
                    ? Get(profile, p => p.MultiHitDamageKoTemplate, "{target} was hit {hits} times for {amount} damage and was knocked out!")
                    : Get(profile, p => p.DamageKoTemplate, "{target} took {amount} damage and was knocked out!");
            }
            else
            {
                template = damage.hitCount > 1
                    ? Get(profile, p => p.MultiHitDamageTemplate, "{target} was hit {hits} times for {amount} damage!")
                    : Get(profile, p => p.DamageTemplate, "{target} took {amount} damage.");
            }

            var evt = new CombatEvent { targetId = damage.targetId };
            string text = Format(template, evt, log, damage.damage, damage.hitCount);
            if (damage.critical)
                text = Get(profile, p => p.CriticalPrefix, "Critical hit! ") + text;
            if (damage.modifierPrefixes.Count > 0)
                text = string.Join("! ", damage.modifierPrefixes) + "! " + text;
            return text;
        }

        private static bool IsModifierEffectEvent(CombatEventType type)
        {
            return type == CombatEventType.HealApplied ||
                   type == CombatEventType.StageChanged ||
                   type == CombatEventType.StatusApplied ||
                   type == CombatEventType.ChemistryStatusApplied ||
                   type == CombatEventType.StatusRefreshed ||
                   type == CombatEventType.StatusCleansed ||
                   type == CombatEventType.APGained ||
                   type == CombatEventType.APRefunded ||
                   type == CombatEventType.APStolen ||
                   type == CombatEventType.HPDotDamage ||
                   type == CombatEventType.PassiveDamageDealt ||
                   type == CombatEventType.BonusActionGranted;
        }

        private static void AddModifierNarration(
            ModifierOutcomePayload payload,
            EffectRecord effect,
            Dictionary<string, DamageSummary> damageByTarget,
            Dictionary<string, HealSummary> healsByTarget,
            List<string> consequenceSentences,
            Dictionary<string, ModifierNarrationAggregate> aggregates,
            List<string> aggregateOrder,
            CombatEventLog log)
        {
            ModifierNarrationFamily family = ResolveModifierFamily(payload, effect);
            if (family == ModifierNarrationFamily.Prefix)
            {
                if (!string.IsNullOrWhiteSpace(payload.targetChainlingId) &&
                    damageByTarget.TryGetValue(payload.targetChainlingId, out DamageSummary damage) &&
                    damage.damage > 0)
                {
                    string prefix = ResolveModifierLabel(payload, log);
                    if (!string.IsNullOrWhiteSpace(prefix) && !damage.modifierPrefixes.Contains(prefix))
                        damage.modifierPrefixes.Add(prefix);
                }
                return;
            }

            CombatEvent effectEvent = effect != null ? effect.evt : default;
            int amount = payload.amount > 0 ? payload.amount : Math.Abs(effectEvent.intValue);
            if (effect != null && effect.consequenceIndex >= 0 && effect.consequenceIndex < consequenceSentences.Count &&
                IsFoldFamily(family))
            {
                consequenceSentences[effect.consequenceIndex] = string.Empty;
            }

            if (family == ModifierNarrationFamily.Heal)
            {
                string healedId = !string.IsNullOrWhiteSpace(effectEvent.targetId)
                    ? effectEvent.targetId
                    : payload.sourceChainlingId;
                SubtractHeal(healsByTarget, healedId, amount);
            }
            else if (family == ModifierNarrationFamily.Damage)
            {
                string damagedId = !string.IsNullOrWhiteSpace(effectEvent.targetId)
                    ? effectEvent.targetId
                    : payload.targetChainlingId;
                SubtractDamage(damageByTarget, damagedId, amount);
            }

            string key = string.Concat(
                payload.sourceChainlingId ?? string.Empty, "|",
                payload.sectionId ?? string.Empty, "|",
                payload.targetChainlingId ?? string.Empty, "|",
                family.ToString());
            if (!aggregates.TryGetValue(key, out ModifierNarrationAggregate aggregate))
            {
                aggregate = new ModifierNarrationAggregate
                {
                    payload = payload,
                    family = family,
                    effectEvent = effectEvent,
                };
                aggregates.Add(key, aggregate);
                aggregateOrder.Add(key);
            }

            aggregate.amount += Math.Max(0, amount);
            if (effect != null)
                aggregate.effectEvent = effectEvent;
        }

        private static ModifierNarrationFamily ResolveModifierFamily(
            ModifierOutcomePayload payload,
            EffectRecord effect)
        {
            string section = payload.sectionId ?? string.Empty;
            if (IsDamagePrefixSection(section))
                return ModifierNarrationFamily.Prefix;
            if (section.StartsWith("6.7-", StringComparison.OrdinalIgnoreCase))
                return section.Equals("6.7-c", StringComparison.OrdinalIgnoreCase)
                    ? ModifierNarrationFamily.BarrierPool
                    : ModifierNarrationFamily.BarrierHits;
            if (section.Equals("6.3-c", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("7.1-b", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.displayText, "MITIGATE", StringComparison.OrdinalIgnoreCase))
                return ModifierNarrationFamily.Guard;
            if (section.Equals("6.8-g", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(payload.displayText, "ACCURACY", StringComparison.OrdinalIgnoreCase))
                return ModifierNarrationFamily.Accuracy;
            if (section.Equals("6.8-b", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("6.8-c", StringComparison.OrdinalIgnoreCase))
                return ModifierNarrationFamily.Cast;
            if (section.Equals("6.8-d", StringComparison.OrdinalIgnoreCase))
                return ModifierNarrationFamily.Reflect;
            if (section.Equals("7.1-d", StringComparison.OrdinalIgnoreCase) ||
                section.Equals("7.3-a", StringComparison.OrdinalIgnoreCase))
                return ModifierNarrationFamily.Share;

            CombatEventType effectType = effect != null ? effect.evt.eventType : default;
            if (effectType == CombatEventType.HealApplied)
                return ModifierNarrationFamily.Heal;
            if (effectType == CombatEventType.APStolen)
                return ModifierNarrationFamily.ApDrain;
            if (effectType == CombatEventType.APGained || effectType == CombatEventType.APRefunded)
                return ModifierNarrationFamily.ApGain;
            if (effectType == CombatEventType.StageChanged)
                return effect.evt.intValue < 0 ? ModifierNarrationFamily.StageLower : ModifierNarrationFamily.StageRaise;
            if (effectType == CombatEventType.StatusApplied || effectType == CombatEventType.ChemistryStatusApplied ||
                effectType == CombatEventType.StatusRefreshed)
                return ModifierNarrationFamily.Status;
            if (effectType == CombatEventType.StatusCleansed)
                return ModifierNarrationFamily.Cleanse;
            if (effectType == CombatEventType.HPDotDamage || effectType == CombatEventType.PassiveDamageDealt)
                return ModifierNarrationFamily.Damage;
            if (effectType == CombatEventType.BonusActionGranted)
                return ModifierNarrationFamily.BonusAction;

            return ModifierNarrationFamily.Fallback;
        }

        private static bool IsDamagePrefixSection(string section)
        {
            return string.Equals(section, "6.3-a", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "6.3-b", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "6.3-d", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "6.3-e", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "7.1-a", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "7.2-a", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(section, "7.2-b", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFoldFamily(ModifierNarrationFamily family)
        {
            return family == ModifierNarrationFamily.Heal ||
                   family == ModifierNarrationFamily.ApGain ||
                   family == ModifierNarrationFamily.ApDrain ||
                   family == ModifierNarrationFamily.StageRaise ||
                   family == ModifierNarrationFamily.StageLower ||
                   family == ModifierNarrationFamily.Status ||
                   family == ModifierNarrationFamily.Cleanse ||
                   family == ModifierNarrationFamily.Damage ||
                   family == ModifierNarrationFamily.BonusAction;
        }

        private static void SubtractHeal(Dictionary<string, HealSummary> healsByTarget, string targetId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(targetId) ||
                !healsByTarget.TryGetValue(targetId, out HealSummary heal))
                return;
            heal.amount = Math.Max(0, heal.amount - amount);
        }

        private static void SubtractDamage(Dictionary<string, DamageSummary> damageByTarget, string targetId, int amount)
        {
            if (amount <= 0 || string.IsNullOrWhiteSpace(targetId) ||
                !damageByTarget.TryGetValue(targetId, out DamageSummary damage))
                return;
            damage.damage = Math.Max(0, damage.damage - amount);
            if (damage.damage == 0)
                damage.hitCount = 0;
        }

        private static string FormatModifierAggregate(
            ModifierNarrationAggregate aggregate,
            CombatEventLog log,
            CombatNarrationProfileSO profile)
        {
            if (aggregate == null || aggregate.payload == null)
                return string.Empty;

            ModifierOutcomePayload payload = aggregate.payload;
            CombatEvent effect = aggregate.effectEvent;
            string ownerId = payload.sourceChainlingId;
            string targetId = !string.IsNullOrWhiteSpace(effect.targetId)
                ? effect.targetId
                : payload.targetChainlingId;
            bool selfTarget = string.Equals(ownerId, targetId, StringComparison.Ordinal);
            string template;
            switch (aggregate.family)
            {
                case ModifierNarrationFamily.Heal:
                    template = selfTarget
                        ? Get(profile, p => p.ModifierHealTemplate, "{owner}'s {modifier} restored {amount} HP.")
                        : Get(profile, p => p.ModifierHealOtherTemplate, "{owner}'s {modifier} restored {amount} HP to {target}.");
                    break;
                case ModifierNarrationFamily.ApGain:
                    template = selfTarget
                        ? Get(profile, p => p.ModifierApGainTemplate, "{owner}'s {modifier} granted {amount} AP.")
                        : Get(profile, p => p.ModifierApGainOtherTemplate, "{owner}'s {modifier} granted {target} {amount} AP.");
                    break;
                case ModifierNarrationFamily.ApDrain:
                    targetId = payload.targetChainlingId;
                    template = Get(profile, p => p.ModifierApDrainTemplate, "{owner}'s {modifier} drained {amount} AP from {target}.");
                    break;
                case ModifierNarrationFamily.StageRaise:
                    template = Get(profile, p => p.ModifierStageRaisedTemplate, "{owner}'s {modifier} raised {target}'s {stat} by {amount}.");
                    break;
                case ModifierNarrationFamily.StageLower:
                    template = Get(profile, p => p.ModifierStageLoweredTemplate, "{owner}'s {modifier} lowered {target}'s {stat} by {amount}.");
                    break;
                case ModifierNarrationFamily.Status:
                    template = Get(profile, p => p.ModifierStatusTemplate, "{owner}'s {modifier} made {target} {status}.");
                    break;
                case ModifierNarrationFamily.Cleanse:
                    template = Get(profile, p => p.ModifierCleanseTemplate, "{owner}'s {modifier} cleansed {target} of {status}.");
                    break;
                case ModifierNarrationFamily.Damage:
                    template = Get(profile, p => p.ModifierDamageTemplate, "{owner}'s {modifier} dealt {amount} damage to {target}.");
                    break;
                case ModifierNarrationFamily.Guard:
                    targetId = payload.targetChainlingId;
                    selfTarget = string.Equals(ownerId, targetId, StringComparison.Ordinal);
                    template = selfTarget
                        ? Get(profile, p => p.ModifierGuardTemplate, "{owner}'s {modifier} put up a guard for the rest of the turn.")
                        : Get(profile, p => p.ModifierGuardOtherTemplate, "{owner}'s {modifier} guarded {target} for the rest of the turn.");
                    break;
                case ModifierNarrationFamily.Accuracy:
                    targetId = payload.targetChainlingId;
                    template = Get(profile, p => p.ModifierAccuracyTemplate, "{owner}'s {modifier} sharpened {target}'s aim.");
                    break;
                case ModifierNarrationFamily.BarrierHits:
                    template = Get(profile, p => p.ModifierBarrierHitsTemplate, "{owner}'s {modifier} raised a barrier. Blocks {amount} {hitWord}.");
                    break;
                case ModifierNarrationFamily.BarrierPool:
                    template = Get(profile, p => p.ModifierBarrierPoolTemplate, "{owner}'s {modifier} raised a barrier. Absorbs up to {amount} damage.");
                    break;
                case ModifierNarrationFamily.Cast:
                    template = Get(profile, p => p.ModifierCastTemplate, "{owner}'s {modifier} cast {move}!");
                    break;
                case ModifierNarrationFamily.Reflect:
                    targetId = payload.targetChainlingId;
                    template = Get(profile, p => p.ModifierReflectTemplate, "{owner}'s {modifier} reflected the effect back to {target}.");
                    break;
                case ModifierNarrationFamily.Share:
                    targetId = payload.targetChainlingId;
                    template = Get(profile, p => p.ModifierShareTemplate, "{owner}'s {modifier} shared the buffs with {target}.");
                    break;
                case ModifierNarrationFamily.BonusAction:
                    template = Get(profile, p => p.ModifierBonusActionTemplate, "{owner}'s {modifier} granted another action.");
                    break;
                default:
                    template = Get(profile, p => p.ModifierTemplate, "{owner}'s {modifier} activated.");
                    break;
            }

            string text = template;
            text = Replace(text, "owner", ResolveName(log, ownerId));
            text = Replace(text, "target", ResolveName(log, targetId));
            text = Replace(text, "move", ResolveName(log, payload.moveId));
            text = Replace(text, "modifier", ResolveModifierLabel(payload, log));
            text = Replace(text, "amount", Math.Max(0, aggregate.amount).ToString());
            text = Replace(text, "hitWord", aggregate.amount == 1 ? "hit" : "hits");
            text = Replace(text, "status", Humanize(effect.statusId.ToString()));
            text = Replace(text, "stat", Humanize(effect.coreStat.ToString()));
            return NormalizeSentence(text);
        }

        private static string FormatUnmetConditional(ModifierOutcomePayload payload, CombatEventLog log)
        {
            string target = ResolveName(log, payload.targetChainlingId);
            string owner = ResolveName(log, payload.sourceChainlingId);
            string reason;
            switch (payload.sectionId)
            {
                case "6.3-a": reason = $"{target} was not below half HP"; break;
                case "6.3-b": reason = $"{owner}'s HP was not lower than {target}'s"; break;
                case "6.3-c": reason = $"{owner} was not below 30% HP"; break;
                case "6.3-d": reason = "no ally had fainted this turn"; break;
                case "6.3-e": reason = $"{target} did not miss last turn"; break;
                case "7.1-a": reason = "no partner was buffed"; break;
                case "7.2-a": reason = "no ally had attacked earlier this turn"; break;
                case "7.2-b": reason = $"{target} had not been hit earlier this turn"; break;
                default: return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(payload.reason) ||
                payload.reason.IndexOf("condition", StringComparison.OrdinalIgnoreCase) < 0 &&
                payload.reason.IndexOf("No ", StringComparison.OrdinalIgnoreCase) < 0 &&
                payload.reason.IndexOf("not ", StringComparison.OrdinalIgnoreCase) < 0)
                return string.Empty;

            return NormalizeSentence($"{ResolveModifierLabel(payload, log)} did not trigger: {reason}.");
        }

        private static void AppendWallInterceptNarration(
            CombatEvent evt,
            CombatEventLog log,
            CombatNarrationProfileSO profile,
            List<string> wallSentences,
            List<string> consequenceSentences)
        {
            if (!WallInterceptPayload.TryFromEvent(evt, out WallInterceptPayload payload) || payload == null)
            {
                if (evt.eventType == CombatEventType.BarrierBlocked)
                {
                    wallSentences.Add(Format(
                        Get(profile, p => p.BarrierTemplate, "{target}'s barrier blocked the attack."),
                        evt, log, 0, 0));
                }
                return;
            }

            WallInterceptMode mode = payload.Mode;
            if (mode == WallInterceptMode.None || mode == WallInterceptMode.Bypass)
                return;

            string wallSentence = FormatWallResolution(evt, log, profile, payload, mode);
            if (!string.IsNullOrWhiteSpace(wallSentence))
                wallSentences.Add(wallSentence);

            if (payload.reflectedDamage > 0)
            {
                consequenceSentences.Add(FormatWallToken(
                    Get(profile, p => p.WallReflectTemplate, "{actor} took {amount} reflected damage."),
                    evt, log, payload, payload.reflectedDamage));
            }

            if (payload.attackerApDrained > 0)
            {
                consequenceSentences.Add(FormatWallToken(
                    Get(profile, p => p.WallApDrainTemplate, "{actor} lost {amount} AP."),
                    evt, log, payload, payload.attackerApDrained));
            }
        }

        private static string FormatWallResolution(
            CombatEvent evt,
            CombatEventLog log,
            CombatNarrationProfileSO profile,
            WallInterceptPayload payload,
            WallInterceptMode mode)
        {
            int absorbed = Math.Max(0, payload.damageAbsorbed);
            switch (mode)
            {
                case WallInterceptMode.WallBlocked:
                    if (absorbed <= 0)
                        absorbed = Math.Max(0, payload.incomingDamage);
                    return FormatWallToken(
                        Get(profile, p => p.WallBlockedTemplate, "{wall} blocked all {amount} damage."),
                        evt, log, payload, absorbed);

                case WallInterceptMode.WallMitigated:
                    if (absorbed <= 0)
                        return string.Empty;
                    return FormatWallToken(
                        Get(profile, p => p.WallMitigatedTemplate, "{wall} blocked {amount} damage."),
                        evt, log, payload, absorbed);

                case WallInterceptMode.WallBrokenThrough:
                    return FormatWallToken(
                        Get(profile, p => p.WallBrokenTemplate, "{actor} broke {target}'s {wall} (-{amount})."),
                        evt, log, payload, absorbed);

                case WallInterceptMode.WallHitNoDamage:
                    if (absorbed > 0)
                    {
                        return FormatWallToken(
                            Get(profile, p => p.WallBlockedTemplate, "{wall} blocked all {amount} damage."),
                            evt, log, payload, absorbed);
                    }
                    return FormatWallToken(
                        Get(profile, p => p.WallHeldTemplate, "{wall} held firm."),
                        evt, log, payload, 0);

                default:
                    return string.Empty;
            }
        }

        private static string FormatWallToken(
            string template,
            CombatEvent evt,
            CombatEventLog log,
            WallInterceptPayload payload,
            int amount)
        {
            string text = Format(template, evt, log, amount, 0);
            text = Replace(text, "wall", ResolveWallName(log, payload != null ? payload.wallSourceTag : null));
            return NormalizeSentence(text);
        }

        private static void AppendWallExpiredNarration(
            CombatEvent evt,
            CombatEventLog log,
            List<string> wallSentences)
        {
            string sourceTag = evt.moveId;
            if (WallInterceptPayload.TryFromEvent(evt, out WallInterceptPayload payload) && payload != null &&
                !string.IsNullOrWhiteSpace(payload.wallSourceTag))
            {
                sourceTag = payload.wallSourceTag;
            }

            string targetName = ResolveName(log, evt.targetId);
            string wallName = ResolveWallName(log, sourceTag);
            wallSentences.Add(NormalizeSentence($"{targetName}'s {wallName} faded."));
        }

        private static string ResolveWallName(CombatEventLog log, string wallSourceTag)
        {
            if (string.IsNullOrWhiteSpace(wallSourceTag))
                return "The wall";

            string trimmed = wallSourceTag.Trim();
            string named = ResolveName(log, trimmed);
            if (!string.IsNullOrWhiteSpace(named) &&
                !string.Equals(named, trimmed, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(named, "the target", StringComparison.OrdinalIgnoreCase))
            {
                return named;
            }

            return TitleCaseWords(trimmed.Replace('_', ' ').Replace('-', ' '));
        }

        private static string TitleCaseWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "The wall";

            var builder = new StringBuilder(value.Length + 4);
            bool capitalizeNext = true;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c))
                {
                    builder.Append(' ');
                    capitalizeNext = true;
                    continue;
                }

                if (capitalizeNext)
                {
                    builder.Append(char.ToUpperInvariant(c));
                    capitalizeNext = false;
                }
                else
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "The wall" : result;
        }

        private static string BuildActionText(
            CombatEvent? actionEvent,
            CombatEventLog log,
            CombatNarrationProfileSO profile,
            bool multiStrikeAction)
        {
            if (!actionEvent.HasValue)
                return string.Empty;

            CombatEvent evt = actionEvent.Value;
            switch (evt.eventType)
            {
                case CombatEventType.MoveUsed:
                    if (evt.boolValue)
                    {
                        return Format(
                            Get(profile, p => p.PriorityMoveTemplate, "{actor} struck first with priority {move}!"),
                            evt,
                            log,
                            0,
                            0);
                    }

                    if (multiStrikeAction)
                    {
                        return Format(
                            Get(profile, p => p.MultiHitMoveTemplate, "{actor} used {move}..."),
                            evt,
                            log,
                            0,
                            0);
                    }

                    return Format(Get(profile, p => p.MoveTemplate, "{actor} used {move}!"), evt, log, 0, 0);
                case CombatEventType.SwapPerformed:
                    return Format(Get(profile, p => p.SwapStartTemplate, "{actor} is switching out!"), evt, log, 0, 0);
                case CombatEventType.ActionFailed:
                    return !string.IsNullOrWhiteSpace(evt.message)
                        ? NormalizeSentence(evt.message)
                        : Format(Get(profile, p => p.ActionFailedTemplate, "{actor} could not use {move}."), evt, log, 0, 0);
                case CombatEventType.ActionSelected:
                    return !string.IsNullOrWhiteSpace(evt.message)
                        ? NormalizeSentence(evt.message)
                        : Format(Get(profile, p => p.PassTemplate, "{actor} passed."), evt, log, 0, 0);
                case CombatEventType.SystemMessage:
                    return NormalizeSentence(evt.message);
                default:
                    return !string.IsNullOrWhiteSpace(evt.message)
                        ? NormalizeSentence(evt.message)
                        : Format(Get(profile, p => p.GenericActionTemplate, "{actor} acted."), evt, log, 0, 0);
            }
        }

        private static bool IsMultiStrikeAction(
            IReadOnlyList<CombatEvent> events,
            int fromIndex,
            int toExclusive)
        {
            int strikes = 0;
            for (int i = fromIndex; i < toExclusive; i++)
            {
                CombatEventType type = events[i].eventType;
                if (type == CombatEventType.DamageDealt ||
                    type == CombatEventType.Miss ||
                    type == CombatEventType.Dodge ||
                    type == CombatEventType.BarrierBlocked)
                {
                    strikes++;
                    if (strikes > 1)
                        return true;
                }
            }

            return false;
        }

        private static bool IsActionEvent(CombatEventType type)
        {
            return type == CombatEventType.MoveUsed ||
                   type == CombatEventType.SwapPerformed ||
                   type == CombatEventType.ActionFailed ||
                   type == CombatEventType.ActionSelected;
        }

        private static bool IsReadableActionInterruption(CombatEvent evt)
        {
            if (evt.eventType != CombatEventType.SystemMessage || string.IsNullOrWhiteSpace(evt.message))
                return false;

            return evt.message.IndexOf("skipped their action", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   evt.message.IndexOf("lost their action", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   evt.message.IndexOf("mis-acted", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static CombatNarrationDelivery ResolveDelivery(CombatNarrationProfileSO profile, CombatEventType type)
        {
            return profile != null
                ? profile.ResolveDelivery(type)
                : CombatNarrationProfileSO.ResolveDefaultDelivery(type);
        }

        private static CombatEvent FindTargetEvent(
            IReadOnlyList<CombatEvent> events,
            int fromIndex,
            int toExclusive,
            CombatEventType type,
            string targetId)
        {
            for (int i = fromIndex; i < toExclusive; i++)
            {
                CombatEvent evt = events[i];
                if (evt.eventType == type && string.Equals(evt.targetId, targetId, StringComparison.Ordinal))
                    return evt;
            }

            return new CombatEvent { targetId = targetId };
        }

        private static string Format(string template, CombatEvent evt, CombatEventLog log, int amount, int hits)
        {
            string text = template ?? string.Empty;
            text = Replace(text, "actor", ResolveName(log, evt.sourceId));
            text = Replace(text, "target", ResolveName(log, evt.targetId));
            text = Replace(text, "move", ResolveName(log, evt.moveId));
            text = Replace(text, "amount", amount.ToString());
            text = Replace(text, "hits", hits.ToString());
            text = Replace(text, "status", Humanize(evt.statusId.ToString()));
            text = Replace(text, "stat", Humanize(evt.coreStat.ToString()));
            return NormalizeSentence(text);
        }

        private static string ResolveName(CombatEventLog log, string id)
        {
            return string.IsNullOrEmpty(id) ? "the target" : log.Name(id);
        }

        private static string Replace(string text, string token, string value)
        {
            return (text ?? string.Empty).Replace("{" + token + "}", value ?? string.Empty);
        }

        private static string Humanize(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
                return "the effect";

            var builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                    builder.Append(' ');
                builder.Append(current);
            }

            return builder.ToString();
        }

        private static bool StartsNamedTick(string message)
        {
            return !string.IsNullOrEmpty(message) &&
                   (message.StartsWith("Kindled burns ", StringComparison.Ordinal) ||
                    message.StartsWith("Rugged scorches ", StringComparison.Ordinal) ||
                    message.StartsWith("Leaked taxes ", StringComparison.Ordinal));
        }

        private static bool IsNarratedModifierCategory(string category)
        {
            return string.Equals(category, ModifierCategory.TRG.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(category, ModifierCategory.SYN.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatModifierActivation(
            ModifierOutcomePayload payload,
            CombatEvent evt,
            CombatEventLog log)
        {
            string owner = ResolveName(log, payload.sourceChainlingId);
            string modifier = ResolveModifierLabel(payload, log);
            return NormalizeSentence($"{owner}'s {modifier} activated.");
        }

        private static string ResolveModifierLabel(ModifierOutcomePayload payload, CombatEventLog log)
        {
            if (payload == null)
                return "stamp";

            string modifier = payload.modifierName;
            if (!string.IsNullOrWhiteSpace(modifier) &&
                !string.Equals(modifier, payload.sectionId, StringComparison.OrdinalIgnoreCase))
                return modifier.Trim();

            string move = string.IsNullOrWhiteSpace(payload.moveId) ? string.Empty : log.Name(payload.moveId);
            return !string.IsNullOrWhiteSpace(move) && !string.Equals(move, payload.moveId, StringComparison.Ordinal)
                ? move + " stamp"
                : "stamp";
        }

        private static bool TrySetupFeedSentence(CombatEvent evt, out string sentence)
        {
            sentence = null;
            if (!ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload payload))
                return false;
            if (!string.Equals(payload.sectionId, "7.2-c", StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrWhiteSpace(payload.reason) ||
                !payload.reason.StartsWith("Setup Feed", StringComparison.Ordinal))
                return false;

            sentence = NormalizeSentence(payload.reason);
            return !string.IsNullOrWhiteSpace(sentence);
        }

        private static string NormalizeSentence(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        private static string JoinSentences(List<string> sentences)
        {
            var builder = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sentences.Count; i++)
            {
                string sentence = NormalizeSentence(sentences[i]);
                if (sentence.Length == 0 || !seen.Add(sentence))
                    continue;
                if (builder.Length > 0)
                    builder.Append(' ');
                builder.Append(sentence);
            }
            return builder.ToString();
        }

        private static CombatNarrationPage NewPage(
            CombatNarrationPageKind kind,
            string speaker,
            string text,
            float hold,
            float skipLockout = 0f)
        {
            return new CombatNarrationPage
            {
                kind = kind,
                speaker = speaker,
                text = NormalizeSentence(text),
                minimumDisplaySeconds = Math.Max(0f, hold),
                skipLockoutSeconds = Math.Max(0f, skipLockout),
            };
        }

        private static void AppendTranscript(CombatNarrationActionPlan plan, CombatNarrationPage page)
        {
            if (page == null || !page.IsReadable)
                return;

            plan.transcriptEntries.Add(new CombatNarrationTranscriptEntry
            {
                turnIndex = plan.turnIndex,
                actionIndex = plan.actionIndex,
                sequenceIndex = plan.transcriptEntries.Count,
                kind = page.kind,
                speaker = page.speaker,
                text = page.text,
            });
        }

        private static string GetSpeaker(CombatNarrationProfileSO profile)
        {
            return profile != null ? profile.SpeakerLabel : "BATTLE LINK";
        }

        private static string Get(
            CombatNarrationProfileSO profile,
            Func<CombatNarrationProfileSO, string> selector,
            string fallback)
        {
            string value = profile != null ? selector(profile) : null;
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
