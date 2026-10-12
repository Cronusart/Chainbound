using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatPresentationAudioTests
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
        public void SoundOnlyPhaseIsUsableWithoutPrefab()
        {
            var sound = CreateAsset<SoundEventSO>();
            var entry = new CombatVFXLibrary.VFXEntry
            {
                castVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = true,
                    phaseStartSound = sound,
                },
            };

            Assert.IsTrue(entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Cast, out var config));
            Assert.IsNull(config.vfxPrefab);
            Assert.AreSame(sound, config.phaseStartSound);
        }

        [UnityTest]
        public IEnumerator SoundOnlyPhaseHonorsSpawnDelay()
        {
            var sound = CreateAsset<SoundEventSO>();
            var entry = new CombatVFXLibrary.VFXEntry
            {
                castVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = true,
                    phaseStartSound = sound,
                    spawnDelay = 0.05f,
                },
            };

            GameObject presenterObject = CreateGameObject("Presenter");
            var presenter = presenterObject.AddComponent<Combat2BattlefieldPresenter>();
            var bridge = presenterObject.AddComponent<RecordingMonoAudioBridge>();
            SetPrivateField(presenter, "combatAudioBridgeBehaviour", bridge);
            SetPrivateField(presenter, "autoDiscoverCombatAudioBridge", false);

            MethodInfo method = null;
            MethodInfo[] methods = typeof(Combat2BattlefieldPresenter).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (methods[i].Name == "SpawnPhaseVfx" && parameters.Length == 5)
                {
                    method = methods[i];
                    break;
                }
            }

            Assert.IsNotNull(method);
            method.Invoke(presenter, new object[]
            {
                CombatVFXLibrary.VFXPhase.Cast,
                entry,
                null,
                null,
                false,
            });

            Assert.AreEqual(0, bridge.PlayCount);
            yield return new WaitForSeconds(0.02f);
            Assert.AreEqual(0, bridge.PlayCount);
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(1, bridge.PlayCount);
            Assert.AreSame(sound, bridge.LastSound);
        }

        [Test]
        public void WallCatalogUsesCaseInsensitiveIdAndDefaultFallback()
        {
            var specific = CreateAsset<SoundEventSO>();
            var fallback = CreateAsset<SoundEventSO>();
            var catalog = CreateAsset<CombatWallSfxCatalogSO>();
            SetPrivateField(catalog, "entries", new List<CombatWallSfxEntry>
            {
                new CombatWallSfxEntry { wallSfxId = "default", blockSound = fallback },
                new CombatWallSfxEntry { wallSfxId = "wall_ice", blockSound = specific },
            });
            catalog.BuildLookup();

            Assert.AreSame(specific, catalog.ResolveSound(" WALL_ICE ", CombatWallSfxCue.Block));
            Assert.AreSame(fallback, catalog.ResolveSound("unknown", CombatWallSfxCue.Block));
        }

        [Test]
        public void WallCatalogFallsBackToDefaultWhenMaterialCueSlotEmpty()
        {
            var defaultRaise = CreateAsset<SoundEventSO>();
            var defaultAbsorb = CreateAsset<SoundEventSO>();
            var catalog = CreateAsset<CombatWallSfxCatalogSO>();
            SetPrivateField(catalog, "entries", new List<CombatWallSfxEntry>
            {
                new CombatWallSfxEntry
                {
                    wallSfxId = "default",
                    raiseSound = defaultRaise,
                    absorbSound = defaultAbsorb,
                },
                new CombatWallSfxEntry
                {
                    wallSfxId = "wall_barrier",
                    // Intentionally empty — should inherit default cue sounds.
                },
            });
            catalog.BuildLookup();

            Assert.AreSame(defaultRaise, catalog.ResolveSound("wall_barrier", CombatWallSfxCue.Raise));
            Assert.AreSame(defaultAbsorb, catalog.ResolveSound("wall_barrier", CombatWallSfxCue.Absorb));
        }

        [Test]
        public void WallCatalogSupportsEveryLifecycleCue()
        {
            var raise = CreateAsset<SoundEventSO>();
            var absorb = CreateAsset<SoundEventSO>();
            var reflect = CreateAsset<SoundEventSO>();
            var breakSound = CreateAsset<SoundEventSO>();
            var block = CreateAsset<SoundEventSO>();
            var catalog = CreateAsset<CombatWallSfxCatalogSO>();
            SetPrivateField(catalog, "entries", new List<CombatWallSfxEntry>
            {
                new CombatWallSfxEntry
                {
                    wallSfxId = "wall_all",
                    raiseSound = raise,
                    absorbSound = absorb,
                    reflectSound = reflect,
                    breakSound = breakSound,
                    blockSound = block,
                },
            });
            catalog.BuildLookup();

            Assert.AreSame(raise, catalog.ResolveSound("wall_all", CombatWallSfxCue.Raise));
            Assert.AreSame(absorb, catalog.ResolveSound("wall_all", CombatWallSfxCue.Absorb));
            Assert.AreSame(reflect, catalog.ResolveSound("wall_all", CombatWallSfxCue.Reflect));
            Assert.AreSame(breakSound, catalog.ResolveSound("wall_all", CombatWallSfxCue.Break));
            Assert.AreSame(block, catalog.ResolveSound("wall_all", CombatWallSfxCue.Block));
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

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private sealed class RecordingMonoAudioBridge : MonoBehaviour, ICombatAudioBridge
        {
            public int PlayCount { get; private set; }
            public Object LastSound { get; private set; }

            public void PlayOneShot(Object soundEvent, Vector3? position = null, Transform follow = null, float volumeScale = 1f)
            {
                PlayCount++;
                LastSound = soundEvent;
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

            public void PlayPostBattleSting(Object soundEvent, float volumeScale = 1f)
            {
            }

            public void FadeOutPostBattleSting(float fadeSeconds)
            {
            }
        }
    }
}
