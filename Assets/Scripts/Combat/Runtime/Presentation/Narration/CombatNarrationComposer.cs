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
        }

        private sealed class HealSummary
        {
            public string targetId;
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
            float actionHold = profile != null ? profile.ActionLeadSeconds : 0.22f;
            float resolutionHold = profile != null ? profile.PrimaryResolutionSeconds : 0.70f;
            float consequenceHold = profile != null ? profile.ConsequenceSeconds : 0.55f;

            CombatEvent? actionEvent = null;
            var damageByTarget = new Dictionary<string, DamageSummary>(StringComparer.Ordinal);
            var healsByTarget = new Dictionary<string, HealSummary>(StringComparer.Ordinal);
            var damageTargetOrder = new List<string>();
            var healTargetOrder = new List<string>();
            var wallSentences = new List<string>();
            var primarySentences = new List<string>();
            var consequenceSentences = new List<string>();
            var transcriptOnlySentences = new List<string>();
            var koTargets = new HashSet<string>(StringComparer.Ordinal);
            var koTargetOrder = new List<string>();

            for (int i = fromIndex; i < toExclusive; i++)
            {
                CombatEvent evt = events[i];
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
                            AddDamage(damageByTarget, damageTargetOrder, evt);
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

                    case CombatEventType.SwapPerformed:
                    case CombatEventType.AutoSwapIn:
                        primarySentences.Add(Format(Get(profile, p => p.SwapResultTemplate, "{target} entered the battle."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.StatusApplied:
                    case CombatEventType.ChemistryStatusApplied:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusAppliedTemplate, "{target} became {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusRefreshed:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusRefreshedTemplate, "{target}'s {status} was refreshed."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusImmune:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusImmuneTemplate, "{target} resisted {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusCleansed:
                    case CombatEventType.StatusConsumed:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusCleansedTemplate, "{target} was cleansed of {status}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.StatusExpired:
                        consequenceSentences.Add(Format(Get(profile, p => p.StatusExpiredTemplate, "{target}'s {status} ended."), evt, log, 0, 0));
                        break;

                    case CombatEventType.StageChanged:
                        consequenceSentences.Add(Format(Get(profile, p => p.StageChangedTemplate, "{target}'s {stat} changed by {amount}."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.PassiveTriggered:
                        consequenceSentences.Add(Format(Get(profile, p => p.PassiveTemplate, "{actor}'s passive activated."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.ModifierTriggered:
                        consequenceSentences.Add(Format(Get(profile, p => p.ModifierTemplate, "A combat modifier changed the outcome."),
                            evt, log, 0, 0));
                        break;

                    case CombatEventType.APGainBlocked:
                        consequenceSentences.Add(Format(Get(profile, p => p.ApBlockedTemplate, "{target} could not gain AP."),
                            evt, log, evt.intValue, 0));
                        break;

                    case CombatEventType.APStolen:
                        consequenceSentences.Add(Format(Get(profile, p => p.ApStolenTemplate, "{target} lost {amount} AP."),
                            evt, log, Math.Abs(evt.intValue), 0));
                        break;

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
                resolutionSentences.Add(FormatDamage(damageByTarget[damageTargetOrder[i]], log, profile));
            resolutionSentences.AddRange(primarySentences);

            for (int i = 0; i < healTargetOrder.Count; i++)
            {
                HealSummary heal = healsByTarget[healTargetOrder[i]];
                var evt = new CombatEvent { targetId = heal.targetId };
                resolutionSentences.Add(Format(Get(profile, p => p.HealTemplate, "{target} recovered {amount} HP."),
                    evt, log, heal.amount, 0));
            }

            string actionText = BuildActionText(actionEvent, log, profile, IsMultiStrikeAction(events, fromIndex, toExclusive));
            string resolutionText = JoinSentences(resolutionSentences);
            string consequenceText = JoinSentences(consequenceSentences);

            if (!string.IsNullOrWhiteSpace(actionText))
                plan.actionPage = NewPage(CombatNarrationPageKind.Action, speaker, actionText, actionHold);
            if (!string.IsNullOrWhiteSpace(resolutionText))
                plan.resolutionPage = NewPage(CombatNarrationPageKind.Resolution, speaker, resolutionText, resolutionHold);
            if (!string.IsNullOrWhiteSpace(consequenceText))
                plan.consequencePage = NewPage(CombatNarrationPageKind.Consequence, speaker, consequenceText, consequenceHold);

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
            return text;
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
                        Get(profile, p => p.WallBrokenTemplate, "{wall} broke and absorbed {amount} damage."),
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

        private static CombatNarrationPage NewPage(CombatNarrationPageKind kind, string speaker, string text, float hold)
        {
            return new CombatNarrationPage
            {
                kind = kind,
                speaker = speaker,
                text = NormalizeSentence(text),
                minimumDisplaySeconds = Math.Max(0f, hold),
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
