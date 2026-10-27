using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chainbound.Combat.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class ManifestParityTests
    {
        [Test]
        public void GameplayManifestMatchesEveryImportedMoveAndStatusId()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../backend/Chainbound.BattleService/manifests/gameplay-manifest.json"));
            Assert.That(File.Exists(path), Is.True, $"Missing gameplay manifest: {path}");
            JObject root = JObject.Parse(File.ReadAllText(path));
            Assert.That((int)root["manifestVersion"], Is.EqualTo(3));
            Dictionary<string, JObject> manifestMoves = root["moves"]!.Children<JObject>()
                .ToDictionary(x => (string)x["moveId"], StringComparer.Ordinal);

            string[] moveGuids = AssetDatabase.FindAssets("t:MoveDefinitionSO", new[] { "Assets/Data/Combat/Moves" });
            Assert.That(manifestMoves.Count, Is.EqualTo(moveGuids.Length));
            foreach (string guid in moveGuids)
            {
                MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(manifestMoves.TryGetValue(move.moveId, out JObject dto), Is.True, move.moveId);
                AssertMove(move, dto);
            }

            Dictionary<int, JObject> statuses = root["statuses"]!.Children<JObject>()
                .ToDictionary(x => (int)x["numericId"]);
            foreach (StatusId statusId in Enum.GetValues(typeof(StatusId)))
            {
                if (statusId == StatusId.None) continue;
                Assert.That(statuses.ContainsKey((int)statusId), Is.True,
                    $"Manifest is missing StatusId.{statusId} ({(int)statusId}).");
            }

            string[] statusGuids = AssetDatabase.FindAssets("t:StatusDefinitionSO");
            foreach (string guid in statusGuids)
            {
                StatusDefinitionSO status = AssetDatabase.LoadAssetAtPath<StatusDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (status == null || status.statusId == StatusId.None) continue;
                JObject dto = statuses[(int)status.statusId];
                Assert.That((string)dto["statusId"], Is.EqualTo(status.statusId.ToString()));
                Assert.That((string)dto["displayName"], Is.EqualTo(status.displayName));
                Assert.That((string)dto["effectSummary"], Is.EqualTo(status.effectSummary));
                Assert.That((int)dto["statusClass"], Is.EqualTo((int)status.statusClass));
                Assert.That((int)dto["origin"], Is.EqualTo((int)status.origin));
                Assert.That((int)dto["defaultDuration"], Is.EqualTo(status.defaultDuration));
                Assert.That((int)dto["counterThreshold"], Is.EqualTo(status.counterThreshold));
                Assert.That((bool)dto["isBuff"], Is.EqualTo(status.isBuff));
                Assert.That((bool)dto["isDebuff"], Is.EqualTo(status.isDebuff));
                Assert.That((bool)dto["preventsAction"], Is.EqualTo(status.preventsAction));
                Assert.That((int)dto["endOfTurnDamageBasisPoints"],
                    Is.EqualTo(Mathf.RoundToInt(status.endOfTurnDamagePercent * 10000f)));
                Assert.That((int)dto["endOfTurnApChange"], Is.EqualTo(status.endOfTurnAPChange));
            }
        }

        [Test]
        public void ExporterHashUsesBattleServiceCanonicalAlgorithm()
        {
            // Golden value from Chainbound.BattleService CanonicalJsonHash (System.Text.Json): numbers keep their
            // written text and '+'/'<' are escaped as \u002B/\u003C.
            const string json = "{\"b\":1.10,\"a\":[0.15000000596046449,\"x+y<z\"],\"c\":true}";
            Assert.That(AuthoritativeGameplayManifestExporter.HashCanonicalJson(json),
                Is.EqualTo("94fc1f9c4a30ab58d92aff747db3203cb86af9e218543ab2f97c36f58dcd73d0"));
        }

        [Test]
        public void ClientConfigHashMatchesCommittedGameplayManifest()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "../backend/Chainbound.BattleService/manifests/gameplay-manifest.json"));
            Assert.That(File.Exists(path), Is.True, $"Missing gameplay manifest: {path}");
            var config = AssetDatabase.LoadAssetAtPath<AuthoritativeCombatServiceConfigSO>(
                "Assets/Data/Combat/Capture/AuthoritativeCombatServiceConfig.asset");
            Assert.That(config, Is.Not.Null);
            string manifestHash = AuthoritativeGameplayManifestExporter.HashCanonicalJson(
                File.ReadAllText(path, System.Text.Encoding.UTF8));
            Assert.That(config.gameplayManifestHash, Is.EqualTo(manifestHash),
                "Re-run Tools/Chainbound/Capture/Export Authoritative Manifests.");
        }
        private static void AssertMove(MoveDefinitionSO move, JObject dto)
        {
            Assert.That((string)dto["displayName"], Is.EqualTo(move.displayName), move.moveId);
            Assert.That((int)dto["category"], Is.EqualTo((int)move.category), move.moveId);
            Assert.That((int)dto["baseType"], Is.EqualTo((int)move.baseType), move.moveId);
            Assert.That((int)dto["rarity"], Is.EqualTo((int)move.rarity), move.moveId);
            Assert.That((int)dto["targeting"], Is.EqualTo((int)move.targeting), move.moveId);
            Assert.That((int)dto["contactType"], Is.EqualTo((int)move.contactType), move.moveId);
            Assert.That((int)dto["power"], Is.EqualTo(move.power), move.moveId);
            Assert.That((int)dto["apCost"], Is.EqualTo(move.apCost), move.moveId);
            Assert.That((int)dto["accuracy"], Is.EqualTo(move.accuracy), move.moveId);
            Assert.That((int)dto["hitCountMin"], Is.EqualTo(move.hitCountMin), move.moveId);
            Assert.That((int)dto["hitCountMax"], Is.EqualTo(move.hitCountMax), move.moveId);
            Assert.That((int)dto["rechargeTurns"], Is.EqualTo(move.rechargeTurns), move.moveId);
            CollectionAssert.AreEqual(
                (move.tags ?? new List<string>()).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                dto["tags"]!.Values<string>().ToArray(), move.moveId);
            Assert.That((string)dto["stageChanges"], Is.EqualTo(CombatCsvRoundTrip.FormatStageChangesPublic(move.stageChanges)), move.moveId);
            Assert.That((string)dto["utilityEffects"], Is.EqualTo(CombatCsvRoundTrip.FormatUtilityEffectsPublic(move.utilityEffects)), move.moveId);
            AssertFloat(dto, "hpCostPercent", move.hpCostPercent, move.moveId);
            Assert.That((int)dto["healPercent"], Is.EqualTo(move.healPercentMaxHP), move.moveId);
            Assert.That((bool)dto["bossOnly"], Is.EqualTo(MoveAvailability.IsBossOnly(move)), move.moveId);
            Assert.That((bool)dto["spendsRemainingAp"],
                Is.EqualTo((move.tags ?? new List<string>()).Any(x =>
                    string.Equals(x, "SpendRemainingAp", StringComparison.OrdinalIgnoreCase))), move.moveId);
            Assert.That((bool)dto["moveEffectsOncePerTarget"], Is.EqualTo(move.resolveProfile?.moveEffectsOncePerTarget ?? false), move.moveId);

            List<JObject> statuses = dto["statusEffects"]!.Children<JObject>().ToList();
            Assert.That(statuses.Count, Is.EqualTo(move.statusEffects.Count), move.moveId);
            for (int i = 0; i < statuses.Count; i++)
            {
                Assert.That((string)statuses[i]["statusId"], Is.EqualTo(move.statusEffects[i].statusId.ToString()), move.moveId);
                Assert.That((int)statuses[i]["duration"], Is.EqualTo(move.statusEffects[i].duration), move.moveId);
                Assert.That((int)statuses[i]["chanceBasisPoints"], Is.EqualTo(Mathf.RoundToInt(move.statusEffects[i].chance * 10000f)), move.moveId);
            }

            MoveResolveProfile profile = move.resolveProfile ?? new MoveResolveProfile();
            MoveResolveProfileUtility.EnsureNestedDefaults(profile);
            JObject resolve = (JObject)dto["resolveProfile"]!;
            Assert.That((string)resolve["primaryTiming"], Is.EqualTo(profile.primaryTiming.ToString()), move.moveId);
            Assert.That((string)resolve["utilityTiming"], Is.EqualTo(profile.utilityExecutionTiming.ToString()), move.moveId);
            Assert.That((string)resolve["conditionalPolicy"], Is.EqualTo(profile.conditionalEffectPolicy.ToString()), move.moveId);
            Assert.That((string)resolve["conditions"], Is.EqualTo(CombatCsvRoundTrip.FormatResolveConditionsPublic(profile.resolveConditions)), move.moveId);
            Assert.That((bool)resolve["requireAll"], Is.EqualTo(profile.requireAllResolveConditions), move.moveId);
            AssertFloat(resolve, "damagePowerMultiplier", profile.damage.powerMultiplier, move.moveId);
            AssertFloat(resolve, "damagePreMitigationMultiplier", profile.damage.preMitigationDamageMultiplier, move.moveId);
            AssertFloat(resolve, "damageFinalMultiplier", profile.damage.finalDamageMultiplier, move.moveId);
            Assert.That((bool)resolve["damageOverrideAttackStat"], Is.EqualTo(profile.damage.overrideAttackStat), move.moveId);
            Assert.That((string)resolve["damageAttackStat"], Is.EqualTo(profile.damage.attackStatOverride.ToString()), move.moveId);
            AssertFloat(resolve, "conditionalDamageMultiplier", profile.conditionalDamage.multiplier, move.moveId);
            Assert.That((string)resolve["conditionalDamageConditions"], Is.EqualTo(CombatCsvRoundTrip.FormatResolveConditionsPublic(profile.conditionalDamage.conditions)), move.moveId);
            Assert.That((bool)resolve["conditionalDamageRequireAll"], Is.EqualTo(profile.conditionalDamage.requireAllConditions), move.moveId);
            Assert.That((string)resolve["conditionalDamageScalingCondition"], Is.EqualTo(
                profile.conditionalDamage.useScalingCondition
                    ? CombatCsvRoundTrip.FormatResolveConditionPublic(profile.conditionalDamage.scalingCondition)
                    : string.Empty), move.moveId);
            AssertFloat(resolve, "conditionalDamageBonusPerCount", profile.conditionalDamage.bonusMultiplierPerCount, move.moveId);
            AssertFloat(resolve, "conditionalDamageMaxMultiplier", profile.conditionalDamage.maxMultiplier, move.moveId);
            Assert.That((string)resolve["randomDamageMultipliers"],
                Is.EqualTo(CombatCsvRoundTrip.FormatFloatListPublic(profile.conditionalDamage.randomMultipliers)), move.moveId);

            JObject accuracy = (JObject)dto["accuracyTuning"]!;
            Assert.That((bool)accuracy["forceHit"], Is.EqualTo(profile.accuracy.forceHit), move.moveId);
            Assert.That((bool)accuracy["forceMiss"], Is.EqualTo(profile.accuracy.forceMiss), move.moveId);
            AssertFloat(accuracy, "multiplier", profile.accuracy.accuracyMultiplier, move.moveId);
            AssertFloat(accuracy, "bonusPercent", profile.accuracy.accuracyBonusPercent, move.moveId);
            Assert.That((int)accuracy["ignoreEvasionStages"], Is.EqualTo(profile.accuracy.ignoreEvasionStages), move.moveId);
            AssertFloat(accuracy, "minHitChance", profile.accuracy.minHitChance, move.moveId);
            AssertFloat(accuracy, "maxHitChance", profile.accuracy.maxHitChance, move.moveId);
        }

        private static void AssertFloat(JObject dto, string name, float expected, string moveId) =>
            Assert.That((float)dto[name], Is.EqualTo(expected).Within(0.0001f), $"{moveId}.{name}");
    }
}
