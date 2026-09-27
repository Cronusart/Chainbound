using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatNarrationComposerTests
    {
        [Test]
        public void ComposeAction_FormatsActionAndAggregatesMultiHitKo()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.MoveUsed,
                turn = 1,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
            });
            log.LogDamage(1, "actor", "target", "move", 12, true, 1f);
            log.LogDamage(1, "actor", "target", "move", 8, false, 1f);
            log.LogKO(1, "target", "actor", "move", 1, 0);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 1, 0);

            Assert.AreEqual("Validape used Snap Strike...", plan.actionPage.text);
            StringAssert.Contains("Critical hit!", plan.resolutionPage.text);
            StringAssert.Contains("Kitsauto was hit 2 times for 20 damage and was knocked out!", plan.resolutionPage.text);
            Assert.AreEqual(0, plan.firstEventTimestamp);
            Assert.AreEqual(3, plan.lastEventTimestamp);
        }

        [Test]
        public void ComposeAction_AggregatesMultipleTargetsWithoutLosingEitherOutcome()
        {
            CombatEventLog log = NewNamedLog();
            log.RegisterName("target2", "Gilarime");
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            log.LogDamage(2, "actor", "target", "move", 10, false, 1f);
            log.LogDamage(2, "actor", "target2", "move", 15, false, 1f);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 2, 1);

            StringAssert.Contains("Kitsauto took 10 damage.", plan.resolutionPage.text);
            StringAssert.Contains("Gilarime took 15 damage.", plan.resolutionPage.text);
        }

        [Test]
        public void ComposeAction_SeparatesPrimaryAndConsequencePages()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            log.LogBarrierBlocked(3, "actor", "target", "move");
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.StatusApplied,
                sourceId = "actor",
                targetId = "target",
                statusId = StatusId.Corroded,
            });

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 3, 0);

            StringAssert.Contains("Kitsauto's barrier blocked the attack.", plan.resolutionPage.text);
            StringAssert.Contains("Kitsauto became Corroded.", plan.consequencePage.text);
            Assert.AreEqual(3, plan.transcriptEntries.Count);
            CollectionAssert.AreEqual(
                new[] { CombatNarrationPageKind.Action, CombatNarrationPageKind.Resolution, CombatNarrationPageKind.Consequence },
                plan.transcriptEntries.Select(entry => entry.kind).ToArray());
        }

        [Test]
        public void ComposeAction_FormatsFullWallBlockWithAbsorbedAmount()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            var payload = WallInterceptPayload.Create(
                hitIndex: 1,
                totalHits: 1,
                mode: WallInterceptMode.WallBlocked,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: true,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 1f,
                incomingDamage: 38,
                finalHpDamage: 0,
                barrierFullyBlocked: true,
                mitigationSourcesExpired: 0,
                barriersExpired: 0,
                wallExpiredThisStep: false,
                partialPenetration: false,
                wallSourceTag: "barrier_wall",
                damageAbsorbed: 38);
            log.LogBarrierBlocked(1, "actor", "target", "move", payload.ToJson());

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 1, 0);

            Assert.AreEqual("Validape used Snap Strike!", plan.actionPage.text);
            StringAssert.Contains("Barrier Wall blocked all 38 damage.", plan.resolutionPage.text);
            Assert.IsNull(plan.consequencePage);
        }

        [Test]
        public void ComposeAction_FormatsWallMitigationBeforeResidualDamage()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            var payload = WallInterceptPayload.Create(
                hitIndex: 1,
                totalHits: 1,
                mode: WallInterceptMode.WallMitigated,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: true,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 0.25f,
                incomingDamage: 38,
                finalHpDamage: 17,
                barrierFullyBlocked: false,
                mitigationSourcesExpired: 0,
                barriersExpired: 0,
                wallExpiredThisStep: false,
                partialPenetration: false,
                wallSourceTag: "barrier_wall",
                damageAbsorbed: 21,
                reflectedDamage: 5,
                attackerApDrained: 1);
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.DamageDealt,
                turn = 1,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
                intValue = 17,
                metadataJson = payload.ToJson(),
            });

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 1, 0);

            Assert.AreEqual("Validape used Snap Strike!", plan.actionPage.text);
            StringAssert.StartsWith("Barrier Wall blocked 21 damage.", plan.resolutionPage.text);
            StringAssert.Contains("Kitsauto took 17 damage.", plan.resolutionPage.text);
            StringAssert.Contains("Validape took 5 reflected damage.", plan.consequencePage.text);
            StringAssert.Contains("Validape lost 1 AP.", plan.consequencePage.text);
        }

        [Test]
        public void ComposeAction_FormatsBrokenWallAbsorption()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            var payload = WallInterceptPayload.Create(
                hitIndex: 1,
                totalHits: 1,
                mode: WallInterceptMode.WallBrokenThrough,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: false,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 0f,
                incomingDamage: 40,
                finalHpDamage: 28,
                barrierFullyBlocked: false,
                mitigationSourcesExpired: 0,
                barriersExpired: 1,
                wallExpiredThisStep: true,
                partialPenetration: true,
                wallSourceTag: "ice_wall",
                damageAbsorbed: 12);
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.DamageDealt,
                turn = 2,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
                intValue = 28,
                metadataJson = payload.ToJson(),
            });

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 2, 0);

            StringAssert.Contains("Ice Wall broke and absorbed 12 damage.", plan.resolutionPage.text);
            StringAssert.Contains("Kitsauto took 28 damage.", plan.resolutionPage.text);
        }

        [Test]
        public void ComposeAction_FormatsPriorityMoveActionLine()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.MoveUsed,
                turn = 1,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
                boolValue = true,
            });

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 1, 0);

            Assert.AreEqual("Validape struck first with priority Snap Strike!", plan.actionPage.text);
        }

        [Test]
        public void ComposeAction_FormatsMultiHitResolutionWithExclamation()
        {
            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent { eventType = CombatEventType.MoveUsed, sourceId = "actor", targetId = "target", moveId = "move" });
            log.LogDamage(1, "actor", "target", "move", 10, false, 1f);
            log.LogDamage(1, "actor", "target", "move", 8, false, 1f);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, null, 1, 0);

            Assert.AreEqual("Validape used Snap Strike...", plan.actionPage.text);
            Assert.AreEqual("Kitsauto was hit 2 times for 18 damage!", plan.resolutionPage.text);
        }

        [Test]
        public void ComposeAction_PassUsesLoggedReadableMessage()
        {
            CombatEventLog log = NewNamedLog();
            log.LogMessage(CombatEventType.ActionSelected, "Validape passed", 4);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, 1, null, 4, 0);

            Assert.AreEqual("Validape passed", plan.actionPage.text);
            Assert.IsNull(plan.resolutionPage);
        }

        [Test]
        public void ComposeAction_StatusInterruptedTurnIsStillRegistered()
        {
            CombatEventLog log = NewNamedLog();
            log.LogMessage(CombatEventType.SystemMessage, "Validape is Rekt and lost their action!", 4);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, 1, null, 4, 0);

            Assert.AreEqual("Validape is Rekt and lost their action!", plan.actionPage.text);
            Assert.AreEqual(1, plan.transcriptEntries.Count);
        }

        [Test]
        public void ComposeAction_SwapRegistersOutgoingAndIncomingChainlings()
        {
            CombatEventLog log = NewNamedLog();
            log.RegisterName("bench", "Gilarime");
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.SwapPerformed,
                turn = 5,
                sourceId = "actor",
                targetId = "bench",
                message = "Validape swapped out, Gilarime swapped in",
            });

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, 1, null, 5, 0);

            Assert.AreEqual("Validape is switching out!", plan.actionPage.text);
            Assert.AreEqual("Gilarime entered the battle.", plan.resolutionPage.text);
        }

        [Test]
        public void ReplayTranscript_AssignsStableGlobalOrdering()
        {
            var replay = new CombatReplayData();
            replay.AppendNarration(new[]
            {
                new CombatNarrationTranscriptEntry { turnIndex = 1, actionIndex = 0, sequenceIndex = 99, text = "First" },
                new CombatNarrationTranscriptEntry { turnIndex = 1, actionIndex = 0, sequenceIndex = 99, text = "Second" },
            });
            replay.AppendNarration(new[]
            {
                new CombatNarrationTranscriptEntry { turnIndex = 1, actionIndex = 1, sequenceIndex = 0, text = "Third" },
            });

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, replay.narrationTranscript.Select(entry => entry.sequenceIndex).ToArray());
            CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, replay.narrationTranscript.Select(entry => entry.text).ToArray());
            Assert.AreEqual("1.1", replay.schemaVersion);
        }

        [Test]
        public void ComposeBattleResult_FallsBackWithoutProfile()
        {
            var result = new BattleResult { winnerTeamIndex = 0, turnCount = 6 };

            CombatNarrationTranscriptEntry entry = CombatNarrationComposer.ComposeBattleResult(result, null, 4);

            Assert.AreEqual("Victory! Your team won in 6 turns.", entry.text);
            Assert.AreEqual(CombatNarrationPageKind.BattleResult, entry.kind);
            Assert.AreEqual(4, entry.sequenceIndex);
        }

        [Test]
        public void ComposeBattleResult_CaptureTerminal_IsNotDraw()
        {
            var result = new BattleResult
            {
                winnerTeamIndex = -1,
                turnCount = 4,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
            };

            CombatNarrationTranscriptEntry entry = CombatNarrationComposer.ComposeBattleResult(result, null, 0);

            Assert.That(entry.text, Does.Contain("Signal Lock complete"));
            Assert.That(entry.text, Does.Not.Contain("draw"));
        }

        [Test]
        public void ComposeBattleResult_VictoryAfterCapture_NamesRecordedChainling()
        {
            var result = new BattleResult
            {
                winnerTeamIndex = 0,
                turnCount = 6,
                captureAttempted = true,
                captureSucceeded = true,
                capturedBackendChainlingId = "abc",
                playerParticipants =
                {
                    new BattleParticipant { backendChainlingId = "abc", displayName = "Kitsauto" }
                }
            };

            CombatNarrationTranscriptEntry entry = CombatNarrationComposer.ComposeBattleResult(result, null, 0);

            Assert.That(entry.text, Does.Contain("Victory"));
            Assert.That(entry.text, Does.Contain("Kitsauto"));
        }

        [Test]
        public void ComposeAction_UsesAuthoredTemplatesAndTokenValues()
        {
            CombatNarrationProfileSO profile = ScriptableObject.CreateInstance<CombatNarrationProfileSO>();
            SetPrivateField(profile, "moveTemplate", "{actor} launched {move} at {target}.");
            SetPrivateField(profile, "damageTemplate", "{target} lost exactly {amount} HP.");

            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.MoveUsed,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
            });
            log.LogDamage(1, "actor", "target", "move", 17, false, 1f);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, profile, 1, 0);

            Assert.AreEqual("Validape launched Snap Strike at Kitsauto.", plan.actionPage.text);
            Assert.AreEqual("Kitsauto lost exactly 17 HP.", plan.resolutionPage.text);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void ComposeAction_RespectsTranscriptOnlyDeliveryOverride()
        {
            CombatNarrationProfileSO profile = ScriptableObject.CreateInstance<CombatNarrationProfileSO>();
            SetPrivateField(profile, "eventRules", new List<CombatNarrationEventRule>
            {
                new CombatNarrationEventRule
                {
                    eventType = CombatEventType.DamageDealt,
                    delivery = CombatNarrationDelivery.TranscriptOnly,
                },
            });

            CombatEventLog log = NewNamedLog();
            log.Log(new CombatEvent
            {
                eventType = CombatEventType.MoveUsed,
                sourceId = "actor",
                targetId = "target",
                moveId = "move",
            });
            log.LogDamage(1, "actor", "target", "move", 9, false, 1f);

            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(log, 0, log.EventCount, profile, 1, 0);

            Assert.IsNull(plan.resolutionPage);
            Assert.AreEqual(2, plan.transcriptEntries.Count);
            Assert.AreEqual(CombatNarrationPageKind.System, plan.transcriptEntries[1].kind);
            StringAssert.Contains("9 damage", plan.transcriptEntries[1].text);
            Object.DestroyImmediate(profile);
        }

        private static CombatEventLog NewNamedLog()
        {
            var log = new CombatEventLog(7);
            log.RegisterName("actor", "Validape");
            log.RegisterName("target", "Kitsauto");
            log.RegisterName("move", "Snap Strike");
            return log;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
