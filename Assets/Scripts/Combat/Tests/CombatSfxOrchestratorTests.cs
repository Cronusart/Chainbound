using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chainbound.Combat.Tests
{
    public class CombatSfxOrchestratorTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdObjects.Count; i++)
            {
                if (_createdObjects[i] != null)
                    Object.DestroyImmediate(_createdObjects[i]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void MixedHitMissBatchResolvesEachTargetOutcome()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            var miss = CreateSound("miss");
            SetPrivateField(catalog, "hit", hit);
            SetPrivateField(catalog, "miss", miss);
            SetPrivateField(catalog, "defaultImpactStaggerSeconds", 0f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "a", "move_1"),
                    Context(CombatEventType.Miss, "actor", "b", "move_1"),
                },
            });

            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(hit, bridge.Sounds[0]);
            Assert.AreSame(miss, bridge.Sounds[1]);
        }

        [Test]
        public void BlockReplacesHitForSameTarget()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            var block = CreateSound("block");
            SetPrivateField(catalog, "hit", hit);
            SetPrivateField(catalog, "block", block);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "target", "move_1"),
                    Context(CombatEventType.BarrierBlocked, "actor", "target", "move_1"),
                },
            });

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(block, bridge.Sounds[0]);
        }

        [Test]
        public void CritUsesCritSoundAndFallsBackToHit()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            var crit = CreateSound("crit");
            SetPrivateField(catalog, "hit", hit);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "target", "move_1", isCrit: true),
                },
            });

            Assert.AreSame(hit, bridge.Sounds[0]);

            bridge.Clear();
            SetPrivateField(catalog, "critHit", crit);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "target", "move_1", isCrit: true),
                },
            });

            Assert.AreSame(crit, bridge.Sounds[0]);
        }

        [Test]
        public void MultiTargetBatchAppliesVolumeScale()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            SetPrivateField(catalog, "hit", hit);
            SetPrivateField(catalog, "defaultImpactStaggerSeconds", 0f);
            SetPrivateField(catalog, "multiTargetVolumeScale", 0.5f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "a", "move_1"),
                    Context(CombatEventType.DamageDealt, "actor", "b", "move_1"),
                },
            });

            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreEqual(0.5f, bridge.VolumeScales[0], 0.001f);
            Assert.AreEqual(0.5f, bridge.VolumeScales[1], 0.001f);
        }

        [UnityTest]
        public IEnumerator KoPlaysAfterImpactNotBefore()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            var ko = CreateSound("ko");
            SetPrivateField(catalog, "hit", hit);
            SetPrivateField(catalog, "ko", ko);
            SetPrivateField(catalog, "koDelayAfterImpactSeconds", 0.05f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "target", "move_1"),
                    Context(CombatEventType.ChainlingKO, string.Empty, "target", string.Empty, isKo: true),
                },
            });

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(hit, bridge.Sounds[0]);

            yield return new WaitForSeconds(0.07f);

            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(ko, bridge.Sounds[1]);
        }

        [UnityTest]
        public IEnumerator StatusAfterDamageIsDelayedAfterImpact()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var hit = CreateSound("hit");
            var debuff = CreateSound("debuff");
            SetPrivateField(catalog, "hit", hit);
            SetPrivateField(catalog, "debuffApplied", debuff);
            SetPrivateField(catalog, "statusAfterImpactDelaySeconds", 0.05f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            var statusDefinition = CreateStatusDefinition(StatusId.FUD, isBuff: false, isDebuff: true);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.DamageDealt, "actor", "target", "move_1"),
                    Context(CombatEventType.StatusApplied, "move_1", "target", string.Empty, statusId: StatusId.FUD, statusDefinition: statusDefinition),
                },
            });

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(hit, bridge.Sounds[0]);

            yield return new WaitForSeconds(0.07f);

            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(debuff, bridge.Sounds[1]);
        }

        [Test]
        public void StatusImmuneReplacesAppliedForSameTarget()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var applied = CreateSound("applied");
            var immune = CreateSound("immune");
            SetPrivateField(catalog, "debuffApplied", applied);
            SetPrivateField(catalog, "statusImmune", immune);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            var statusDefinition = CreateStatusDefinition(StatusId.FUD, isBuff: false, isDebuff: true);

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.StatusApplied, "move_1", "target", string.Empty, statusId: StatusId.FUD, statusDefinition: statusDefinition),
                    Context(CombatEventType.StatusImmune, "move_1", "target", string.Empty, statusId: StatusId.FUD, statusDefinition: statusDefinition),
                },
            });

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(immune, bridge.Sounds[0]);
        }

        [Test]
        public void MissingFallbackFieldsNeverThrow()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            var moveDefinition = CreateAsset<MoveDefinitionSO>();

            Assert.DoesNotThrow(() =>
            {
                orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
                {
                    outcomes = new List<CombatSfxOutcomeContext>
                    {
                        Context(CombatEventType.DamageDealt, "actor", "target", "move_1", moveDefinition: moveDefinition),
                    },
                });
            });
        }

        [Test]
        public void AuthoredImpactAndMissTakePrecedenceWithoutCatalog()
        {
            var authoredImpact = CreateSoundEvent("authored_impact");
            var authoredMiss = CreateSoundEvent("authored_miss");
            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(null, bridge);

            CombatSfxOutcomeContext impact = Context(CombatEventType.DamageDealt, "actor", "target", "move_1");
            impact.authoredImpactSound = authoredImpact;
            CombatSfxOutcomeContext miss = Context(CombatEventType.Miss, "actor", "target_2", "move_1");
            miss.authoredMissSound = authoredMiss;

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext> { impact, miss },
            });

            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(authoredImpact, bridge.Sounds[0]);
            Assert.AreSame(authoredMiss, bridge.Sounds[1]);
        }

        [UnityTest]
        public IEnumerator CritWindupPrecedesDelayedImpact()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var impact = CreateSound("impact");
            var windup = CreateSoundEvent("crit_windup");
            SetPrivateField(catalog, "hit", impact);
            SetPrivateField(catalog, "critWindupSound", windup);
            SetPrivateField(catalog, "critWindupLeadSeconds", 0.02f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            CombatSfxOutcomeContext context = Context(
                CombatEventType.DamageDealt,
                "actor",
                "target",
                "move_1",
                isCrit: true);
            context.targetDelaySeconds = 0.05f;

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext> { context },
            });

            Assert.AreEqual(0, bridge.Sounds.Count);
            yield return new WaitForSeconds(0.04f);
            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(windup, bridge.Sounds[0]);

            yield return new WaitForSeconds(0.03f);
            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(impact, bridge.Sounds[1]);
        }

        [UnityTest]
        public IEnumerator CritWindupClampsAtZeroForZeroAndShortDelays()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var impact = CreateSound("impact");
            var windup = CreateSoundEvent("crit_windup");
            SetPrivateField(catalog, "hit", impact);
            SetPrivateField(catalog, "critWindupSound", windup);
            SetPrivateField(catalog, "critWindupLeadSeconds", 0.05f);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            CombatSfxOutcomeContext immediate = Context(CombatEventType.DamageDealt, "actor", "zero", "move_1", isCrit: true);
            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext> { immediate },
            });
            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(windup, bridge.Sounds[0]);
            Assert.AreSame(impact, bridge.Sounds[1]);

            bridge.Clear();
            CombatSfxOutcomeContext shortDelay = Context(CombatEventType.DamageDealt, "actor", "short", "move_1", isCrit: true);
            shortDelay.targetDelaySeconds = 0.02f;
            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext> { shortDelay },
            });
            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(windup, bridge.Sounds[0]);
            yield return new WaitForSeconds(0.04f);
            Assert.AreEqual(2, bridge.Sounds.Count);
            Assert.AreSame(impact, bridge.Sounds[1]);
        }

        [Test]
        public void MissDodgeAndBlockDoNotPlayCritWindupOrLegacyAccent()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var miss = CreateSound("miss");
            var block = CreateSound("block");
            var windup = CreateSoundEvent("crit_windup");
            SetPrivateField(catalog, "miss", miss);
            SetPrivateField(catalog, "block", block);
            SetPrivateField(catalog, "critWindupSound", windup);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            var moveDefinition = CreateAsset<MoveDefinitionSO>();
            moveDefinition.impactAccentSound = CreateSound("legacy_accent");

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext>
                {
                    Context(CombatEventType.Miss, "actor", "miss_target", "move_1", moveDefinition: moveDefinition),
                    Context(CombatEventType.Dodge, "actor", "dodge_target", "move_1", moveDefinition: moveDefinition),
                    Context(CombatEventType.BarrierBlocked, "actor", "block_target", "move_1", moveDefinition: moveDefinition),
                },
            });

            Assert.AreEqual(3, bridge.Sounds.Count);
            Assert.AreSame(miss, bridge.Sounds[0]);
            Assert.AreSame(miss, bridge.Sounds[1]);
            Assert.AreSame(block, bridge.Sounds[2]);
        }

        [Test]
        public void MaterialWallBlockOverridesGenericBlock()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var genericBlock = CreateSound("generic_block");
            var materialBlock = CreateSoundEvent("ice_block");
            var wallCatalog = CreateAsset<CombatWallSfxCatalogSO>();
            SetPrivateField(wallCatalog, "entries", new List<CombatWallSfxEntry>
            {
                new CombatWallSfxEntry { wallSfxId = "default", blockSound = CreateSoundEvent("default_block") },
                new CombatWallSfxEntry { wallSfxId = "wall_ice", blockSound = materialBlock },
            });
            wallCatalog.BuildLookup();
            SetPrivateField(catalog, "block", genericBlock);
            SetPrivateField(catalog, "wallSfxCatalog", wallCatalog);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            CombatSfxOutcomeContext context = Context(CombatEventType.BarrierBlocked, "actor", "target", "move_1");
            context.wallSfxId = "wall_ice";

            orchestrator.PlayOutcomeBatch(new CombatSfxOutcomeBatch("move_1", "actor", 2)
            {
                outcomes = new List<CombatSfxOutcomeContext> { context },
            });

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(materialBlock, bridge.Sounds[0]);
        }

        [Test]
        public void WallCueDeduplicatesByEventTargetSourceAndCue()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var raise = CreateSoundEvent("raise");
            var wallCatalog = CreateAsset<CombatWallSfxCatalogSO>();
            SetPrivateField(wallCatalog, "entries", new List<CombatWallSfxEntry>
            {
                new CombatWallSfxEntry { wallSfxId = "default", raiseSound = raise },
            });
            wallCatalog.BuildLookup();
            SetPrivateField(catalog, "wallSfxCatalog", wallCatalog);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);
            var request = new CombatWallSfxRequest
            {
                cue = CombatWallSfxCue.Raise,
                wallSfxId = "missing_id",
                targetId = "target",
                sourceId = "actor",
                eventTimestamp = 42,
            };

            orchestrator.PlayWallCue(request);
            orchestrator.PlayWallCue(request);
            Assert.AreEqual(1, bridge.Sounds.Count);
        }

        [Test]
        public void PlaySpawnUsesGlitchInCatalogFallback()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var spawn = CreateSound("spawn");
            SetPrivateField(catalog, "glitchIn", spawn);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateOrchestrator(catalog, bridge);

            orchestrator.PlaySpawn(null);

            Assert.AreEqual(1, bridge.Sounds.Count);
            Assert.AreSame(spawn, bridge.Sounds[0]);
        }

        private CombatSfxOrchestrator CreateOrchestrator(CombatBattleSfxCatalogSO catalog, RecordingCombatAudioBridge bridge)
        {
            var orchestrator = CreateGameObject("SfxOrchestrator").AddComponent<CombatSfxOrchestrator>();
            orchestrator.Configure(catalog, bridge);
            return orchestrator;
        }

        private CombatSfxOutcomeContext Context(
            CombatEventType eventType,
            string sourceId,
            string targetId,
            string moveId,
            bool isCrit = false,
            bool isKo = false,
            StatusId statusId = StatusId.None,
            StatusDefinitionSO statusDefinition = null,
            MoveDefinitionSO moveDefinition = null)
        {
            return new CombatSfxOutcomeContext
            {
                evt = new CombatEvent
                {
                    eventType = eventType,
                    sourceId = sourceId,
                    targetId = targetId,
                    moveId = moveId,
                    boolValue = isCrit,
                    statusId = statusId,
                },
                isCrit = isCrit,
                isKo = isKo,
                statusDefinition = statusDefinition,
                moveDefinition = moveDefinition,
                volumeScale = 1f,
            };
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(asset);
            return asset;
        }

        private GameObject CreateGameObject(string name)
        {
            var go = new GameObject(name);
            _createdObjects.Add(go);
            return go;
        }

        private CombatSfxDummySoundSO CreateSound(string name)
        {
            var sound = CreateAsset<CombatSfxDummySoundSO>();
            sound.name = name;
            return sound;
        }

        private SoundEventSO CreateSoundEvent(string name)
        {
            var sound = CreateAsset<SoundEventSO>();
            sound.name = name;
            return sound;
        }

        private StatusDefinitionSO CreateStatusDefinition(StatusId id, bool isBuff, bool isDebuff)
        {
            var def = CreateAsset<StatusDefinitionSO>();
            def.statusId = id;
            def.isBuff = isBuff;
            def.isDebuff = isDebuff;
            return def;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private sealed class RecordingCombatAudioBridge : ICombatAudioBridge
        {
            public readonly List<Object> Sounds = new List<Object>();
            public readonly List<float> VolumeScales = new List<float>();

            public void PlayOneShot(Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
            {
                Sounds.Add(soundEvent);
                VolumeScales.Add(volumeScale);
            }

            public AudioLoopToken StartLoop(Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
            {
                return default;
            }

            public void StopLoop(AudioLoopToken token, float fadeOut = 0f)
            {
            }

            public void SetLoopPitch(AudioLoopToken token, float pitch)
            {
            }

            public void SetLoopVolume(AudioLoopToken token, float volume)
            {
            }

            public AudioLoopToken StartCaptureLockLoop(float volumeScale = 1f)
            {
                return default;
            }

            public CombatMusicDuckToken AcquireMusicDuck(
                string reason,
                float targetMultiplier,
                float attackSeconds,
                float releaseSeconds)
            {
                return default;
            }

            public void ReleaseMusicDuck(CombatMusicDuckToken token)
            {
            }

            public void StopSceneMusic(float fadeSeconds = 0f)
            {
            }

            public void Clear()
            {
                Sounds.Clear();
                VolumeScales.Clear();
            }
        }
    }
}
