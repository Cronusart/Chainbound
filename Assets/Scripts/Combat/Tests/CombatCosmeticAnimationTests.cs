using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatCosmeticAnimationTests
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
        public void VisualIdentityCloneAndNormalizePreservesAnimationStyleIds()
        {
            var identity = new ChainlingVisualIdentity
            {
                meleeAttackStyleId = " slash ",
                rangedAttackStyleId = "burst",
                hitReactionStyleId = "",
                attackerKoStyleId = null
            };

            var clone = ChainlingVisualIdentity.CloneOrDefault(identity);

            Assert.AreEqual("slash", clone.meleeAttackStyleId);
            Assert.AreEqual("burst", clone.rangedAttackStyleId);
            Assert.AreEqual(ChainlingVisualIdentity.NoneId, clone.hitReactionStyleId);
            Assert.AreEqual(ChainlingVisualIdentity.NoneId, clone.attackerKoStyleId);
        }

        [Test]
        public void VisualIdentitySaveClonePreservesVariantCosmeticIdsAndUsesHudCardFallback()
        {
            var identity = new ChainlingVisualIdentity
            {
                visualVariantId = " chrome ",
                hudCardVariantId = "",
                koFxId = "ko_flash",
                idleStyleId = "idle_bounce"
            };

            var save = ChainlingVisualIdentity.CloneForPlayerSave(identity);

            Assert.AreEqual("chrome", save.visualVariantId);
            Assert.AreEqual("ko_flash", save.koFxId);
            Assert.AreEqual("idle_bounce", save.idleStyleId);
            Assert.AreEqual(ChainlingVisualIdentity.DefaultId, save.hudCardVariantId);
        }

        [Test]
        public void ResolverFindsCosmeticAnimationStylesFromIdentityDatabase()
        {
            var database = CreateAsset<ChainlingVisualIdentityDatabaseSO>();
            var melee = CreateAsset<ChainlingMeleeAttackStyleDefinitionSO>();
            var attackerKo = CreateAsset<ChainlingAttackerKoStyleDefinitionSO>();
            SetDefinitionId(melee, "slash");
            SetDefinitionId(attackerKo, "taunt");
            SetPrivateField(database, "meleeAttackStyles", new List<ChainlingMeleeAttackStyleDefinitionSO> { melee });
            SetPrivateField(database, "attackerKoStyles", new List<ChainlingAttackerKoStyleDefinitionSO> { attackerKo });

            var identity = new ChainlingVisualIdentity
            {
                meleeAttackStyleId = "slash",
                attackerKoStyleId = "taunt"
            };

            var resolved = ChainlingVisualIdentityResolver.Resolve("sparkpup", identity, null, database);

            Assert.AreSame(melee, resolved.meleeAttackStyle);
            Assert.AreSame(attackerKo, resolved.attackerKoStyle);
            Assert.IsNull(resolved.rangedAttackStyle);
            Assert.IsNull(resolved.hitReactionStyle);
        }

        [Test]
        public void ResolverUsesVisualVariantAndVariantScopedCosmetics()
        {
            var visualDatabase = CreateAsset<CombatVisualDatabaseSO>();
            var baseProfile = CreateAsset<CombatVisualProfileSO>();
            var chromeProfile = CreateAsset<CombatVisualProfileSO>();
            SetPrivateField(visualDatabase, "entries", new List<CombatVisualDatabaseSO.Entry>
            {
                new CombatVisualDatabaseSO.Entry { speciesId = "sparkpup", visualVariantId = "base", profile = baseProfile },
                new CombatVisualDatabaseSO.Entry { speciesId = "sparkpup", visualVariantId = "chrome", profile = chromeProfile },
            });
            visualDatabase.RebuildLookup();

            var identityDatabase = CreateAsset<ChainlingVisualIdentityDatabaseSO>();
            var aura = CreateAsset<ChainlingAuraDefinitionSO>();
            var hudCard = CreateAsset<ChainlingHudCardVariantDefinitionSO>();
            var idleStyle = CreateAsset<ChainlingIdleStyleDefinitionSO>();
            SetDefinitionId(aura, "spark_aura");
            SetDefinitionId(hudCard, "retro_card");
            SetDefinitionId(idleStyle, "chrome_idle");
            SetPrivateField(idleStyle, "visualVariantScope", "chrome", typeof(ChainlingVisualDefinitionSO));
            SetPrivateField(identityDatabase, "auras", new List<ChainlingAuraDefinitionSO> { aura });
            SetPrivateField(identityDatabase, "hudCardVariants", new List<ChainlingHudCardVariantDefinitionSO> { hudCard });
            SetPrivateField(identityDatabase, "idleStyles", new List<ChainlingIdleStyleDefinitionSO> { idleStyle });

            var identity = new ChainlingVisualIdentity
            {
                visualVariantId = "chrome",
                auraId = "spark_aura",
                hudCardVariantId = "retro_card",
                idleStyleId = "chrome_idle"
            };

            var resolved = ChainlingVisualIdentityResolver.Resolve("sparkpup", identity, visualDatabase, identityDatabase);

            Assert.AreSame(chromeProfile, resolved.baseProfile);
            Assert.AreSame(aura, resolved.aura);
            Assert.AreSame(hudCard, resolved.hudCardVariant);
            Assert.AreSame(idleStyle, resolved.idleStyle);
        }

        [Test]
        public void HudCardVariantResetsOnKoHiddenAndEmpty()
        {
            var root = new GameObject("hud_card", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(CanvasGroup), typeof(HudCardView));
            _createdObjects.Add(root);
            var image = root.GetComponent<UnityEngine.UI.Image>();
            var card = root.GetComponent<HudCardView>();
            var defaultSprite = CreateSprite();
            var customSprite = CreateSprite();
            var secondCustomSprite = CreateSprite();

            card.ConfigureBackgroundSprites(defaultSprite, null, null);

            var variant = CreateAsset<ChainlingHudCardVariantDefinitionSO>();
            variant.normalSprite = customSprite;
            card.ApplyHudCardVariant(variant);
            Assert.AreSame(customSprite, image.sprite);

            card.SetKO(true);
            Assert.AreSame(defaultSprite, image.sprite);

            variant.normalSprite = customSprite;
            card.ApplyHudCardVariant(variant);
            card.SetHiddenInPlace(true);
            Assert.AreSame(defaultSprite, image.sprite);

            variant.normalSprite = customSprite;
            card.SetHiddenInPlace(false);
            card.ApplyHudCardVariant(variant);
            card.SetEmpty(true);
            Assert.AreSame(defaultSprite, image.sprite);

            variant.normalSprite = secondCustomSprite;
            card.ApplyHudCardVariant(variant);
            Assert.AreSame(secondCustomSprite, image.sprite);
        }

        [Test]
        public void VisualIdentityMutationPatchesRosterSnapshotCache()
        {
            var roster = new PlayerRosterSnapshot
            {
                chainlings = new List<PlayerRosterEntry>
                {
                    new PlayerRosterEntry
                    {
                        snapshot = new ChainlingSnapshot
                        {
                            backendChainlingId = "chainling-1",
                            visualIdentity = ChainlingVisualIdentity.CreateDefault()
                        }
                    }
                }
            };

            var identity = new ChainlingVisualIdentity
            {
                visualVariantId = "chrome",
                hudCardVariantId = "retro_card",
                koFxId = "ko_flash",
                idleStyleId = "idle_bounce"
            };

            bool patched = SupabaseVisualIdentityMutation.PatchRosterCache(roster, "chainling-1", identity);
            ChainlingVisualIdentity patchedIdentity = roster.chainlings[0].snapshot.visualIdentity;

            Assert.IsTrue(patched);
            Assert.AreEqual("chrome", patchedIdentity.visualVariantId);
            Assert.AreEqual("ko_flash", patchedIdentity.koFxId);
            Assert.AreEqual("idle_bounce", patchedIdentity.idleStyleId);
            Assert.AreEqual("retro_card", patchedIdentity.hudCardVariantId);
        }

        [Test]
        public void SpawnAndKoFxCanCarryOptionalBodyClips()
        {
            var swapInFx = CreateAsset<ChainlingSwapInFxDefinitionSO>();
            swapInFx.durationSeconds = 0.8f;
            swapInFx.bodySequence = CreateSequence(3);
            swapInFx.bodyFps = 10f;

            Assert.IsTrue(swapInFx.HasBodySequence);
            Assert.AreEqual(0.2f, swapInFx.ResolveBodyDurationSeconds(), 0.001f);
            Assert.AreEqual(0.8f, swapInFx.ResolveDurationSeconds(), 0.001f);
        }

        [Test]
        public void SequenceResolverUsesKoFallbackToProfileSwapOut()
        {
            var profile = CreateAsset<CombatVisualProfileSO>();
            profile.swapOutSequence = CreateSequence(2);
            profile.swapOutFPS = 8f;

            var identity = new ResolvedChainlingVisualIdentity
            {
                baseProfile = profile,
            };

            ResolvedChainlingVisualClip clip = ChainlingVisualSequenceResolver.Resolve(identity, ChainlingAnimSlot.Ko);

            Assert.AreSame(profile.swapOutSequence, clip.sequence);
            Assert.AreEqual(8f, clip.fps);
        }

        [Test]
        public void SequenceResolverPrefersProfileAlternateForEquippedStyle()
        {
            var profile = CreateAsset<CombatVisualProfileSO>();
            SpriteAnimationSequence defaultSequence = CreateSequence(2);
            SpriteAnimationSequence alternateSequence = CreateSequence(4);
            profile.meleeAttackSequence = defaultSequence;
            profile.meleeAttackFPS = 8f;
            profile.animationAlternates.Add(new CombatVisualAnimationAlternate
            {
                slot = ChainlingAnimSlot.Melee,
                styleId = "nuclear_dash",
                sequence = alternateSequence,
                fps = 18f,
                markerFrame = 2
            });

            var resolved = new ResolvedChainlingVisualIdentity
            {
                baseProfile = profile,
                identity = new ChainlingVisualIdentity { meleeAttackStyleId = "nuclear_dash" }
            };

            ResolvedChainlingVisualClip clip = ChainlingVisualSequenceResolver.Resolve(resolved, ChainlingAnimSlot.Melee);

            Assert.AreSame(alternateSequence, clip.sequence);
            Assert.AreEqual(18f, clip.fps);
            Assert.AreEqual(2, clip.markerFrame);
        }

        [Test]
        public void CombatVisualUsesBodyOverrideForDurationMarkerAndClear()
        {
            var root = CreateGameObject("visual");
            var visual = root.AddComponent<CombatChainlingVisual>();
            var sequence = CreateSequence(4);

            visual.SetBodyOverride(CombatChainlingVisual.CombatAnimState.MeleeAttack, sequence, 20f, markerFrame: 2);

            Assert.AreEqual(4, visual.GetFrameCount(CombatChainlingVisual.CombatAnimState.MeleeAttack));
            Assert.AreEqual(0.15f, visual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.MeleeAttack), 0.001f);
            Assert.IsTrue(visual.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.MeleeAttack, out int markerFrame));
            Assert.AreEqual(2, markerFrame);

            visual.ClearBodyOverrides();

            Assert.IsFalse(visual.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.MeleeAttack, out _));
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

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2);
            _createdObjects.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            _createdObjects.Add(sprite);
            return sprite;
        }

        private SpriteAnimationSequence CreateSequence(int frameCount)
        {
            var sprites = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                var texture = new Texture2D(2, 2);
                _createdObjects.Add(texture);
                sprites[i] = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
                _createdObjects.Add(sprites[i]);
            }

            return new SpriteAnimationSequence(sprites);
        }

        private static void SetDefinitionId(ChainlingVisualDefinitionSO definition, string id)
        {
            SetPrivateField(definition, "id", id, typeof(ChainlingVisualDefinitionSO));
        }

        private static void SetPrivateField(object target, string fieldName, object value, System.Type ownerType = null)
        {
            ownerType ??= target.GetType();
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing private field {fieldName} on {ownerType.Name}.");
            field.SetValue(target, value);
        }
    }
}
