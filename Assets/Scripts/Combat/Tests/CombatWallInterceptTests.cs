using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatWallInterceptTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void PayloadRoundTripPreservesModeAndOneBasedHitIndex()
        {
            var payload = WallInterceptPayload.Create(
                hitIndex: 2,
                totalHits: 4,
                mode: WallInterceptMode.WallBrokenThrough,
                wallActiveBeforeHit: true,
                wallActiveAfterHit: false,
                bypassBarrier: false,
                bypassReason: string.Empty,
                contactType: ContactType.Contact,
                moveCategory: MoveCategory.DamagingMelee,
                mitigationPercent: 0.5f,
                incomingDamage: 40,
                finalHpDamage: 20,
                barrierFullyBlocked: false,
                mitigationSourcesExpired: 1,
                barriersExpired: 0,
                wallExpiredThisStep: true,
                partialPenetration: false,
                wallSourceTag: "barrier_wall");

            Assert.IsTrue(WallInterceptPayload.TryParse(payload.ToJson(), out var parsed));
            Assert.AreEqual(2, parsed.hitIndex);
            Assert.AreEqual(4, parsed.totalHits);
            Assert.AreEqual(WallInterceptMode.WallBrokenThrough, parsed.Mode);
            Assert.AreEqual("barrier_wall", parsed.wallSourceTag);
            Assert.IsTrue(parsed.UsesWallForInitialAim());
        }

        [Test]
        public void MoveDefinitionPlacesWallFromUtilityEffectsAndTags()
        {
            Assert.IsTrue(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinition(UtilityEffectType.BlockWindow)));
            Assert.IsFalse(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinition(UtilityEffectType.MitigationWindow)));
            Assert.IsTrue(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinition(UtilityEffectType.FullAbsorbBarrier)));
            Assert.IsTrue(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinition(UtilityEffectType.DamageThresholdBarrier)));
            Assert.IsTrue(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinitionWithTag("WALL")));
            Assert.IsTrue(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinitionWithTag("FULL_ABSORB_WALL")));
            Assert.IsFalse(CombatWallStateUtility.MoveDefinitionPlacesWall(CreateMoveDefinitionWithTag("UTILITY")));
        }

        [Test]
        public void ResolverLogsFullBlockPayloadOnBarrierBlocked()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 45));
            target.AddBarrier(BarrierType.SingleHit, hitsRemaining: 1, damageThreshold: 0, turnsRemaining: 3, sourceTag: "barrier_wall");

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent blocked = resolver.Log.Events.First(e => e.eventType == CombatEventType.BarrierBlocked);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(blocked, out var payload));
            Assert.AreEqual(WallInterceptMode.WallBlocked, payload.Mode);
            Assert.AreEqual(1, payload.hitIndex);
            Assert.IsTrue(payload.wallActiveBeforeHit);
            Assert.IsFalse(payload.wallActiveAfterHit);
            Assert.IsTrue(payload.barrierFullyBlocked);
            Assert.AreEqual(1, payload.barriersExpired);
            Assert.AreEqual("barrier_wall", payload.wallSourceTag);
        }

        [Test]
        public void ResolverLogsRelayBypassPayloadOnDamage()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 35));
            attacker.statuses.Add(new StatusInstance(StatusId.Relay, StatusClass.Consumed, StatusOrigin.General, 1, "test"));
            target.AddBarrier(BarrierType.SingleHit, hitsRemaining: 1, damageThreshold: 0, turnsRemaining: 3, sourceTag: "barrier_wall");

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent damage = resolver.Log.Events.First(e => e.eventType == CombatEventType.DamageDealt);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(damage, out var payload));
            Assert.AreEqual(WallInterceptMode.Bypass, payload.Mode);
            Assert.AreEqual("Relay", payload.bypassReason);
            Assert.IsTrue(payload.wallActiveBeforeHit);
            Assert.IsTrue(payload.wallActiveAfterHit);
            Assert.Greater(payload.finalHpDamage, 0);
        }

        [Test]
        public void ChemistryAdvantageDoesNotBypassReflectingBarrier()
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = ChainlingType.Burned,
                defenderType = ChainlingType.Frozen,
                output = new ChemistryOutput
                {
                    statusId = StatusId.Dusted,
                    recipient = ChemistryRecipient.Defender,
                }
            });

            StatusCatalogSO catalog = CreateStatusCatalog();
            Assert.IsTrue(catalog.IsChemistryOnly(StatusId.Dusted));
            Assert.IsTrue(matrix.TryGetReaction(ChainlingType.Burned, ChainlingType.Frozen, out _));

            var resolver = new CombatResolver(matrix, catalog, seed: 1234, chemistryStrictMode: true);
            var attacker = CreateChainling("attacker", ap: 3, speed: 20, primaryType: ChainlingType.Burned);
            var target = CreateChainling("target", ap: 0, speed: 10, primaryType: ChainlingType.Frozen);
            attacker.moves.Add(CreateMove("burned_strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 35, baseType: ChainlingType.Burned));
            int attackerHpBefore = attacker.currentHP;
            target.AddBarrier(
                BarrierType.SingleHit,
                hitsRemaining: 1,
                damageThreshold: 0,
                turnsRemaining: 3,
                sourceTag: "barrier_wall",
                reflectPercent: 0.5f);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent blocked = resolver.Log.Events.First(e => e.eventType == CombatEventType.BarrierBlocked);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(blocked, out var payload));
            Assert.AreEqual(WallInterceptMode.WallBlocked, payload.Mode);
            Assert.AreNotEqual("ChemistryAdvantage", payload.bypassReason);
            Assert.IsFalse(payload.bypassBarrier);
            Assert.IsTrue(payload.wallActiveBeforeHit);
            Assert.IsFalse(payload.wallActiveAfterHit);
            Assert.Greater(payload.reflectedDamage, 0);
            Assert.Less(attacker.currentHP, attackerHpBefore);
            Assert.IsFalse(resolver.Log.Events.Any(e => e.eventType == CombatEventType.DamageDealt));
        }

        [Test]
        public void ReflectKoStopsRemainingMultiHitDamageEvents()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20, maxHp: 10);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 300);
            attacker.moves.Add(CreateMove(
                "triple_strike",
                MoveCategory.DamagingMelee,
                MoveTargeting.SingleEnemy,
                power: 100,
                hitCount: 3));
            target.AddBarrier(
                BarrierType.DamageThreshold,
                hitsRemaining: 0,
                damageThreshold: 40,
                turnsRemaining: 3,
                sourceTag: "reflecting_wall",
                reflectPercent: 1f);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsFalse(attacker.isAlive);
            Assert.AreEqual(1, resolver.Log.Events.Count(e => e.eventType == CombatEventType.DamageDealt));
            Assert.AreEqual(1, resolver.Log.Events.Count(e =>
                e.eventType == CombatEventType.ChainlingKO && e.targetId == attacker.instanceId));
        }

        [Test]
        public void EncryptedSealBlocksChemistryDebuff()
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = ChainlingType.Burned,
                defenderType = ChainlingType.Frozen,
                output = new ChemistryOutput
                {
                    statusId = StatusId.Dusted,
                    recipient = ChemistryRecipient.Defender,
                }
            });

            StatusCatalogSO catalog = CreateStatusCatalog();
            var resolver = new CombatResolver(matrix, catalog, seed: 1234, chemistryStrictMode: true);
            var attacker = CreateChainling("attacker", ap: 3, speed: 20, primaryType: ChainlingType.Burned);
            var target = CreateChainling("target", ap: 0, speed: 10, primaryType: ChainlingType.Frozen);
            attacker.moves.Add(CreateMove(
                "burned_strike",
                MoveCategory.DamagingMelee,
                MoveTargeting.SingleEnemy,
                power: 35,
                baseType: ChainlingType.Burned));
            target.statuses.Add(new StatusInstance(
                StatusId.EncryptedSeal,
                StatusClass.Duration,
                StatusOrigin.General,
                2,
                "proof_shield"));

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsFalse(target.statuses.Any(s => s.statusId == StatusId.Dusted));
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.StatusImmune && e.targetId == target.instanceId));
        }

        [Test]
        public void ResolverLogsWallExpiredWhenTimerRemovesLastWallWithoutDamage()
        {
            var resolver = CreateResolver();
            var actor = CreateChainling("actor", ap: 0, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10);
            target.AddBarrier(BarrierType.SingleHit, hitsRemaining: 1, damageThreshold: 0, turnsRemaining: 1, sourceTag: "ice_wall");

            var state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            CombatEvent expired = resolver.Log.Events.First(e => e.eventType == CombatEventType.WallExpired);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(expired, out var payload));
            Assert.AreEqual(WallInterceptMode.WallHitNoDamage, payload.Mode);
            Assert.AreEqual(0, payload.hitIndex);
            Assert.IsTrue(payload.wallExpiredThisStep);
            Assert.AreEqual(1, payload.barriersExpired);
            Assert.AreEqual("ice_wall", payload.wallSourceTag);
        }

        [Test]
        public void ResolverSequencesFourHitMoveAgainstOneHitBlock()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 200);
            attacker.moves.Add(CreateMove("quad_strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 18, hitCount: 4));
            target.AddBlock(new MitigationSource
            {
                mitigation = 0.5f,
                turnsRemaining = 2,
                hitsRemaining = 1,
                sourceTag = "ember_wall",
                sourceMoveId = "ember_wall",
                stackKey = "wall_ember",
                contactFilter = UtilityContactFilter.Contact,
                blockKind = BlockKind.Wall,
            });

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            var payloads = resolver.Log.Events
                .Where(e => e.eventType == CombatEventType.DamageDealt)
                .Select(e =>
                {
                    Assert.IsTrue(WallInterceptPayload.TryFromEvent(e, out var payload));
                    return payload;
                })
                .ToList();

            Assert.AreEqual(4, payloads.Count);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, payloads.Select(p => p.hitIndex).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    WallInterceptMode.WallBrokenThrough,
                    WallInterceptMode.None,
                    WallInterceptMode.None,
                    WallInterceptMode.None
                },
                payloads.Select(p => p.Mode).ToArray());
            Assert.Greater(payloads[0].damageAbsorbed, 0);
            Assert.Greater(payloads[0].finalHpDamage, 0);
        }

        [Test]
        public void PercentMitigationWallLogsAbsorbedDamageOnMitigatedHit()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 200);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 40));
            target.AddMitigationSource(0.25f, turnsRemaining: 3, hitsRemaining: 2, sourceTag: "barrier_wall", stackKey: "wall");

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent damage = resolver.Log.Events.First(e => e.eventType == CombatEventType.DamageDealt);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(damage, out var payload));
            Assert.AreEqual(WallInterceptMode.WallMitigated, payload.Mode);
            Assert.Greater(payload.finalHpDamage, 0);
            Assert.Greater(payload.damageAbsorbed, 0);
            Assert.IsTrue(payload.RoutesAttackToWall());
            Assert.GreaterOrEqual(payload.damageAbsorbed + payload.finalHpDamage, payload.incomingDamage);
        }

        [Test]
        public void WallCredit_ExcludesModifierMitigation()
        {
            WallInterceptPayload wallOnly = ResolveWallCredit(includeOrdinaryGuard: false);
            WallInterceptPayload wallAndGuard = ResolveWallCredit(includeOrdinaryGuard: true);

            Assert.AreEqual(WallInterceptMode.WallMitigated, wallOnly.Mode);
            Assert.AreEqual(WallInterceptMode.WallMitigated, wallAndGuard.Mode);
            Assert.Greater(wallOnly.damageAbsorbed, 0);
            Assert.AreEqual(
                wallOnly.damageAbsorbed,
                wallAndGuard.damageAbsorbed,
                "Ordinary guard/passive mitigation must not inflate damage credited to the wall.");
            Assert.Greater(
                wallAndGuard.mitigationPercent,
                wallOnly.mitigationPercent,
                "The control case must still apply the extra non-wall mitigation.");
            Assert.Less(wallAndGuard.finalHpDamage, wallOnly.finalHpDamage);
        }

        [Test]
        public void DamageThresholdBarrierReflectAndAbsorptionAreLoggedOnDamage()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 200);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 100));
            target.AddBarrier(
                BarrierType.DamageThreshold,
                hitsRemaining: 0,
                damageThreshold: 40,
                turnsRemaining: 1,
                sourceTag: "lattice_guard",
                reflectPercent: 0.2f);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent damage = resolver.Log.Events.First(e => e.eventType == CombatEventType.DamageDealt);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(damage, out var payload));
            Assert.GreaterOrEqual(payload.incomingDamage, 41);
            Assert.AreEqual(40, payload.damageAbsorbed);
            Assert.AreEqual(8, payload.reflectedDamage);
            Assert.Greater(payload.finalHpDamage, 0);
            Assert.AreEqual(0.2f, payload.wallReflectPercent, 0.001f);
            Assert.IsTrue(payload.HasWallOnHitFeedback());
        }

        [Test]
        public void WallHitRetaliationDrainsAttackerApOnBlockConsume()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 45));
            target.AddBlock(new MitigationSource
            {
                mitigation = 0.5f,
                turnsRemaining = 2,
                hitsRemaining = 1,
                sourceTag = "ember_wall",
                sourceMoveId = "ember_wall",
                stackKey = "wall_ember",
                attackerApDrainOnHit = 1,
                contactFilter = UtilityContactFilter.Contact,
                blockKind = BlockKind.Wall,
            });

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent blocked = resolver.Log.Events.First(e => e.eventType == CombatEventType.DamageDealt);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(blocked, out var payload));
            Assert.AreEqual(1, payload.attackerApDrained);
            Assert.IsTrue(payload.HasWallOnHitFeedback());
            Assert.AreEqual(3, attacker.currentAP, "3 initial + 2 turn regen - 1 move cost - 1 wall retaliation");
        }

        private CombatResolver CreateResolver(TypeChemistryMatrixSO matrix = null)
        {
            matrix ??= CreateAsset<TypeChemistryMatrixSO>();
            return new CombatResolver(matrix, CreateStatusCatalog(), seed: 1234, chemistryStrictMode: true);
        }

        private WallInterceptPayload ResolveWallCredit(bool includeOrdinaryGuard)
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 200);
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, power: 80));
            target.AddMitigationSource(
                0.25f,
                turnsRemaining: 3,
                hitsRemaining: 2,
                sourceTag: "barrier_wall",
                stackKey: "wall");
            if (includeOrdinaryGuard)
            {
                target.AddMitigationSource(
                    0.50f,
                    turnsRemaining: 3,
                    hitsRemaining: 2,
                    sourceTag: "Modifier.GritGuard",
                    stackKey: "modifier_grit_guard");
            }

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent damage = resolver.Log.Events.First(e => e.eventType == CombatEventType.DamageDealt);
            Assert.IsTrue(WallInterceptPayload.TryFromEvent(damage, out WallInterceptPayload payload));
            return payload;
        }

        private StatusCatalogSO CreateStatusCatalog()
        {
            var catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.Add(CreateStatusDefinition(StatusId.TypeCoating, StatusClass.Duration, StatusOrigin.ChemistryOnly, "Type Coating"));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.HypedTypeCoating, StatusClass.Duration, StatusOrigin.ChemistryOnly, "Hyped Type Coating"));
            StatusDefinitionSO dusted = CreateStatusDefinition(
                StatusId.Dusted, StatusClass.Duration, StatusOrigin.ChemistryOnly, "Dusted");
            dusted.isDebuff = true;
            catalog.definitions.Add(dusted);
            catalog.definitions.Add(CreateStatusDefinition(
                StatusId.EncryptedSeal, StatusClass.Duration, StatusOrigin.General, "Encrypted Seal"));
            catalog.definitions.Add(CreateStatusDefinition(StatusId.Relay, StatusClass.Consumed, StatusOrigin.General, "Relay"));
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO CreateStatusDefinition(StatusId id, StatusClass statusClass, StatusOrigin origin, string displayName)
        {
            var def = CreateAsset<StatusDefinitionSO>();
            def.statusId = id;
            def.statusClass = statusClass;
            def.origin = origin;
            def.displayName = displayName;
            def.defaultDuration = 1;
            return def;
        }

        private MoveDefinitionSO CreateMoveDefinition(UtilityEffectType effectType)
        {
            var def = CreateAsset<MoveDefinitionSO>();
            def.utilityEffects.Add(new UtilityEffectPayload { effectType = effectType });
            return def;
        }

        private MoveDefinitionSO CreateMoveDefinitionWithTag(string tag)
        {
            var def = CreateAsset<MoveDefinitionSO>();
            def.tags.Add(tag);
            return def;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static ChainlingInstance CreateChainling(
            string id,
            int ap,
            int speed,
            int maxHp = 100,
            ChainlingType primaryType = ChainlingType.Node)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = ap,
                baseMaxHP = maxHp,
                currentHP = maxHp,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = speed,
                primaryType = primaryType,
                maxSlots = 4,
                isAlive = true,
            };
            chainling.SyncDisplayVitals();
            return chainling;
        }

        private static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
        {
            var team = new TeamState(teamIndex, $"Team {teamIndex}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static MoveInstance CreateMove(
            string id,
            MoveCategory category,
            MoveTargeting targeting,
            int power,
            int hitCount = 1,
            ChainlingType baseType = ChainlingType.Node)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                baseType = baseType,
                targeting = targeting,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.None,
                basePower = power,
                apCost = 1,
                baseAccuracy = 100,
                hitCount = hitCount,
                hitCountMax = hitCount,
                rarity = RarityTier.Common,
                resolveProfile = new MoveResolveProfile
                {
                    accuracy = new MoveAccuracyTuning { forceHit = true },
                },
            };
        }
    }
}
