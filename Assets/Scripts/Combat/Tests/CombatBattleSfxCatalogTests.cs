using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatBattleSfxCatalogTests
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
        public void StatusSpecificAppliedSoundBeatsBuffFallback()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var specific = CreateSound("specific");
            var fallback = CreateSound("buff_fallback");
            SetPrivateField(catalog, "buffApplied", fallback);
            SetPrivateField(catalog, "statusSfxEntries", new List<CombatStatusSfxEntry>
            {
                new CombatStatusSfxEntry
                {
                    statusId = StatusId.Pumped,
                    appliedSound = specific,
                },
            });
            catalog.BuildLookup();

            var evt = new CombatEvent { eventType = CombatEventType.StatusApplied, statusId = StatusId.Pumped };
            var def = CreateStatusDefinition(StatusId.Pumped, isBuff: true, isDebuff: false);

            Assert.AreSame(specific, catalog.ResolveStatusSound(evt, def));
        }

        [Test]
        public void StatusSpecificCleanseSoundBeatsGlobalCleanseFallback()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var specific = CreateSound("cleanse_specific");
            var fallback = CreateSound("cleanse_fallback");
            SetPrivateField(catalog, "statusCleanse", fallback);
            SetPrivateField(catalog, "statusSfxEntries", new List<CombatStatusSfxEntry>
            {
                new CombatStatusSfxEntry
                {
                    statusId = StatusId.Corroded,
                    cleansedSound = specific,
                },
            });
            catalog.BuildLookup();

            var evt = new CombatEvent { eventType = CombatEventType.StatusCleansed, statusId = StatusId.Corroded };
            var def = CreateStatusDefinition(StatusId.Corroded, isBuff: false, isDebuff: true);

            Assert.AreSame(specific, catalog.ResolveStatusSound(evt, def));
        }

        [Test]
        public void MissingStatusEntryFallsBackSafelyByStatusDefinition()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var fallback = CreateSound("debuff_fallback");
            SetPrivateField(catalog, "debuffApplied", fallback);
            catalog.BuildLookup();

            var evt = new CombatEvent { eventType = CombatEventType.StatusApplied, statusId = StatusId.FUD };
            var def = CreateStatusDefinition(StatusId.FUD, isBuff: false, isDebuff: true);

            Assert.AreSame(fallback, catalog.ResolveStatusSound(evt, def));
        }

        [Test]
        public void TurnStartApBatchPlaysOneCatalogSound()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var turnStart = CreateSound("turn_start_ap");
            SetPrivateField(catalog, "turnStartApGrant", turnStart);

            var bridge = new RecordingCombatAudioBridge();
            var orchestrator = CreateGameObject("SfxOrchestrator").AddComponent<CombatSfxOrchestrator>();
            orchestrator.Configure(catalog, bridge);

            orchestrator.PlayTurnStartApGrant(new List<CombatEvent>
            {
                new CombatEvent { eventType = CombatEventType.APGained, targetId = "a" },
                new CombatEvent { eventType = CombatEventType.APGained, targetId = "b" },
            });

            Assert.AreEqual(1, bridge.PlayCount);
            Assert.AreSame(turnStart, bridge.LastSound);
            Assert.AreEqual(catalog.TurnStartApVolumeScale, bridge.LastVolumeScale, 0.001f);
        }

        [Test]
        public void PostBattleStingerResolvesFromResultState()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            var victory = CreateSound("victory");
            var defeat = CreateSound("defeat");
            var capture = CreateSound("capture");
            SetPrivateField(catalog, "victoryStinger", victory);
            SetPrivateField(catalog, "defeatStinger", defeat);
            SetPrivateField(catalog, "captureStinger", capture);

            Assert.AreSame(victory, catalog.ResolvePostBattle(new BattleResult { winnerTeamIndex = 0 }));
            Assert.AreSame(victory, catalog.ResolvePostBattle(new BattleResult { winnerTeamIndex = -2 }));
            Assert.AreSame(defeat, catalog.ResolvePostBattle(new BattleResult { winnerTeamIndex = 1 }));
            Assert.AreSame(capture, catalog.ResolvePostBattle(new BattleResult
            {
                winnerTeamIndex = -1,
                completionReason = BattleCompletionReason.Captured,
                captureAttempted = true,
                captureSucceeded = true,
            }));
            Assert.AreSame(defeat, catalog.ResolvePostBattle(new BattleResult { winnerTeamIndex = 0, wasFled = true }));
        }

        [Test]
        public void ImpactPhaseAccentVolumeScale_DucksWhenOutcomeImpactAuthored()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            SetPrivateField(catalog, "pairedImpactAccentVolumeScale", 0.6f);

            Assert.AreEqual(1f, catalog.ResolveImpactPhaseAccentVolumeScale(hasOutcomeImpactSound: false), 0.001f);
            Assert.AreEqual(0.6f, catalog.ResolveImpactPhaseAccentVolumeScale(hasOutcomeImpactSound: true), 0.001f);
        }

        [Test]
        public void ImpactPhaseAccentVolumeScale_UsesCatalogLeverValue()
        {
            var catalog = CreateAsset<CombatBattleSfxCatalogSO>();
            SetPrivateField(catalog, "pairedImpactAccentVolumeScale", 0.45f);

            Assert.AreEqual(0.45f, catalog.ResolveImpactPhaseAccentVolumeScale(hasOutcomeImpactSound: true), 0.001f);
            Assert.AreEqual(1f, catalog.ResolveImpactPhaseAccentVolumeScale(hasOutcomeImpactSound: false), 0.001f);
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
            public int PlayCount { get; private set; }
            public Object LastSound { get; private set; }
            public float LastVolumeScale { get; private set; }

            public void PlayOneShot(Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
            {
                PlayCount++;
                LastSound = soundEvent;
                LastVolumeScale = volumeScale;
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
        }
    }

    public sealed class CombatSfxDummySoundSO : ScriptableObject
    {
    }
}
