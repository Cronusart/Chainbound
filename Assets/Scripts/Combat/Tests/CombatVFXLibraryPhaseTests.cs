using System.Reflection;
using NUnit.Framework;
using SignalJam.VisualFx;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatVFXLibraryPhaseTests
    {
        [Test]
        public void PhaseConfig_ResolvesOnlyItsEnabledPrefab()
        {
            var castPrefab = new GameObject("CastPrefab");
            var entry = new CombatVFXLibrary.VFXEntry
            {
                castVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = true,
                    vfxPrefab = castPrefab
                },
                strikeVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = false,
                    vfxPrefab = castPrefab
                }
            };

            Assert.That(entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Cast, out var cast), Is.True);
            Assert.That(cast, Is.SameAs(entry.castVfx));
            Assert.That(entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Strike, out _), Is.False);
            Assert.That(entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Projectile, out _), Is.False);

            Object.DestroyImmediate(castPrefab);
        }

        [Test]
        public void CombatVfxLibrary_ComposesWorldAndPhaseScaleMultipliers()
        {
            var library = ScriptableObject.CreateInstance<CombatVFXLibrary>();
            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                scaleMultiplier = 1.05f
            };

            Assert.That(library.WorldScaleMultiplier, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(library.GetEffectivePhaseScaleMultiplier(config), Is.EqualTo(1.26f).Within(0.0001f));

            Object.DestroyImmediate(library);
        }

        [Test]
        public void PhaseConfig_CloneCopiesPhaseScaleCurve()
        {
            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                phaseScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
            };

            var clone = config.Clone();

            Assert.That(clone.phaseScaleCurve, Is.Not.SameAs(config.phaseScaleCurve));
            Assert.That(clone.phaseScaleCurve.Evaluate(0.5f), Is.EqualTo(config.phaseScaleCurve.Evaluate(0.5f)).Within(0.0001f));
        }

        [Test]
        public void CombatMoveVfx_AppliesPhaseScaleCurveOverConfiguredDuration()
        {
            var go = new GameObject("PhaseScaleVfx");
            go.transform.localScale = Vector3.one * 2f;
            var moveVfx = go.AddComponent<CombatMoveVFX>();
            moveVfx.SetAutoDestroy(false);
            moveVfx.ConfigurePhaseScale(1.5f, 1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.That(go.transform.localScale.x, Is.EqualTo(0f).Within(0.0001f));

            moveVfx.StepPlayback(0.5f);
            Assert.That(go.transform.localScale.x, Is.EqualTo(1.5f).Within(0.0001f));

            moveVfx.StepPlayback(0.5f);
            Assert.That(go.transform.localScale.x, Is.EqualTo(3f).Within(0.0001f));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void TravelTimingResolver_ConstantSpeedScalesWithResolvedPathLength()
        {
            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                vfxPrefab = new GameObject("SpeedPrefab"),
                travel = new CombatVFXLibrary.VFXTravelConfig
                {
                    enabled = true,
                    durationSource = CombatVFXLibrary.VFXTravelDurationSource.ConstantSpeed,
                    speedUnitsPerSecond = 5f,
                    travelProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
                }
            };

            float shortDuration = CombatVfxTravelTimingResolver.ResolveDuration(
                config,
                Vector3.zero,
                new Vector3(10f, 0f, 0f));
            float longDuration = CombatVfxTravelTimingResolver.ResolveDuration(
                config,
                Vector3.zero,
                new Vector3(20f, 0f, 0f));

            Assert.That(shortDuration, Is.EqualTo(2f).Within(0.001f));
            Assert.That(longDuration, Is.EqualTo(4f).Within(0.001f));

            Object.DestroyImmediate(config.vfxPrefab);
        }

        [Test]
        public void TravelTimingResolver_PreservesExistingDurationModes()
        {
            var prefab = new GameObject("DurationModePrefab");
            var moveVfx = prefab.AddComponent<CombatMoveVFX>();
            var textures = new Texture2D[3];
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                textures[i] = new Texture2D(1, 1);
                sprites[i] = Sprite.Create(textures[i], new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            }

            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(sprites));
            SetPrivateField(moveVfx, "spriteFPS", 10f);

            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                vfxPrefab = prefab,
                travel = new CombatVFXLibrary.VFXTravelConfig
                {
                    enabled = true,
                    explicitDurationSeconds = 0.75f
                }
            };

            config.travel.durationSource = CombatVFXLibrary.VFXTravelDurationSource.SpriteFrameSpan;
            Assert.That(
                CombatVfxTravelTimingResolver.ResolveDuration(config, Vector3.zero, new Vector3(10f, 0f, 0f)),
                Is.EqualTo(0.2f).Within(0.0001f));

            config.travel.durationSource = CombatVFXLibrary.VFXTravelDurationSource.EffectLifetime;
            Assert.That(
                CombatVfxTravelTimingResolver.ResolveDuration(config, Vector3.zero, new Vector3(10f, 0f, 0f)),
                Is.EqualTo(0.3f).Within(0.0001f));

            config.travel.durationSource = CombatVFXLibrary.VFXTravelDurationSource.ExplicitSeconds;
            Assert.That(
                CombatVfxTravelTimingResolver.ResolveDuration(config, Vector3.zero, new Vector3(10f, 0f, 0f)),
                Is.EqualTo(0.75f).Within(0.0001f));

            Object.DestroyImmediate(prefab);
            for (int i = 0; i < sprites.Length; i++)
            {
                Object.DestroyImmediate(sprites[i]);
                Object.DestroyImmediate(textures[i]);
            }
        }

        [Test]
        public void TravelTimingResolver_IncludesProgressProfileAndArcInPathLength()
        {
            float straightLength = CombatVfxTravelTimingResolver.EstimatePathLength(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                0f);
            float arcedLength = CombatVfxTravelTimingResolver.EstimatePathLength(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                3f);

            Assert.That(straightLength, Is.EqualTo(10f).Within(0.001f));
            Assert.That(arcedLength, Is.GreaterThan(straightLength));
            Assert.That(
                CombatVfxTravelTimingResolver.EvaluateTravelPosition(
                    Vector3.zero,
                    new Vector3(10f, 0f, 0f),
                    AnimationCurve.Linear(0f, 0f, 1f, 1f),
                    3f,
                    1f),
                Is.EqualTo(new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void Preset_SourceOverrideIsOptIn()
        {
            Shader shader = Shader.Find("SignalJam/VFX/PixelBattleVfx");
            Assert.That(shader, Is.Not.Null);

            var material = new Material(shader);
            var source = new Texture2D(1, 1);
            var replacement = new Texture2D(1, 1);
            var preset = ScriptableObject.CreateInstance<PixelBattleVfxPresetSO>();
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", source);

            SetPrivateField(preset, "sourceTextureOverride", replacement);
            preset.ApplyTo(block, material);
            Assert.That(block.GetTexture("_MainTex"), Is.SameAs(source));

            SetPrivateField(preset, "overrideSourceTexture", true);
            preset.ApplyTo(block, material);
            Assert.That(block.GetTexture("_MainTex"), Is.SameAs(replacement));

            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(replacement);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void Preset_StyleOnlyApplicationPreservesRendererSourceTexture()
        {
            Shader shader = Shader.Find("SignalJam/VFX/PixelBattleVfx");
            Assert.That(shader, Is.Not.Null);

            var material = new Material(shader);
            var rendererTexture = new Texture2D(1, 1);
            var presetTexture = new Texture2D(1, 1);
            var preset = ScriptableObject.CreateInstance<PixelBattleVfxPresetSO>();
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", rendererTexture);

            SetPrivateField(preset, "overrideSourceTexture", true);
            SetPrivateField(preset, "sourceTextureOverride", presetTexture);
            preset.ApplyStyleTo(block, material);

            Assert.That(block.GetTexture("_MainTex"), Is.SameAs(rendererTexture));

            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(rendererTexture);
            Object.DestroyImmediate(presetTexture);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CombatMoveVfx_WritesAnimatedSourceIntoConfiguredMaterialSlot()
        {
            Shader shader = Shader.Find("SignalJam/VFX/PixelBattleVfx");
            Assert.That(shader, Is.Not.Null);

            var root = new GameObject("MoveVfxTest");
            var renderer = root.AddComponent<MeshRenderer>();
            var material = new Material(shader);
            renderer.sharedMaterial = material;

            var frameTexture = new Texture2D(2, 2);
            var frameSprite = Sprite.Create(frameTexture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            var styleBlock = new MaterialPropertyBlock();
            styleBlock.SetFloat("_PixelCells", 23f);
            renderer.SetPropertyBlock(styleBlock, 0);

            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteMaterialSlotIndex", 0);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(new[] { frameSprite }));

            MethodInfo awake = typeof(CombatMoveVFX).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(moveVfx, null);

            var appliedBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(appliedBlock, 0);
            Assert.That(appliedBlock.GetTexture("_MainTex"), Is.SameAs(frameTexture));
            Assert.That(appliedBlock.GetFloat("_PixelCells"), Is.EqualTo(23f));
            Assert.That(appliedBlock.GetVector("_PixelCanvasSize"), Is.EqualTo(new Vector4(2f, 2f, 0f, 0f)));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(frameSprite);
            Object.DestroyImmediate(frameTexture);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CombatMoveVfx_PreserveAuthoredHeightUsesEachSpriteAspect()
        {
            Shader shader = Shader.Find("SignalJam/VFX/PixelBattleVfx");
            Assert.That(shader, Is.Not.Null);

            var root = new GameObject("MoveVfxAspectTest");
            var spriteObject = new GameObject("AnimatedSprite");
            spriteObject.transform.SetParent(root.transform, false);
            spriteObject.transform.localScale = new Vector3(4.8f, 1.65f, 1f);
            var renderer = spriteObject.AddComponent<MeshRenderer>();
            var material = new Material(shader);
            renderer.sharedMaterial = material;

            var frameTexture = new Texture2D(96, 69);
            var frameSprite = Sprite.Create(frameTexture, new Rect(0f, 0f, 96f, 69f), Vector2.one * 0.5f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(new[] { frameSprite }));
            SetPrivateField(moveVfx, "spriteAspectMode", CombatMoveVFX.SpriteAspectMode.PreserveAuthoredHeight);

            MethodInfo awake = typeof(CombatMoveVFX).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(moveVfx, null);

            Vector3 correctedScale = spriteObject.transform.localScale;
            Assert.That(correctedScale.x, Is.EqualTo(1.65f * 96f / 69f).Within(0.0001f));
            Assert.That(correctedScale.y, Is.EqualTo(1.65f).Within(0.0001f));
            Assert.That(correctedScale.z, Is.EqualTo(1f));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(frameSprite);
            Object.DestroyImmediate(frameTexture);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CombatMoveVfx_SpriteSizeDefinesAspectReferenceEnvelope()
        {
            Shader shader = Shader.Find("SignalJam/VFX/PixelBattleVfx");
            Assert.That(shader, Is.Not.Null);

            var root = new GameObject("MoveVfxSpriteSizeTest");
            var spriteObject = new GameObject("AnimatedSprite");
            spriteObject.transform.SetParent(root.transform, false);
            spriteObject.transform.localScale = new Vector3(1f, 1f, 1f);
            var renderer = spriteObject.AddComponent<MeshRenderer>();
            var material = new Material(shader);
            renderer.sharedMaterial = material;

            var frameTexture = new Texture2D(96, 69);
            var frameSprite = Sprite.Create(frameTexture, new Rect(0f, 0f, 96f, 69f), Vector2.one * 0.5f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(new[] { frameSprite }));
            SetPrivateField(moveVfx, "spriteAspectMode", CombatMoveVFX.SpriteAspectMode.PreserveAuthoredHeight);
            SetPrivateField(moveVfx, "spriteSize", new Vector2(6f, 4f));

            MethodInfo awake = typeof(CombatMoveVFX).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(moveVfx, null);

            Vector3 correctedScale = spriteObject.transform.localScale;
            Assert.That(correctedScale.x, Is.EqualTo(4f * 96f / 69f).Within(0.0001f));
            Assert.That(correctedScale.y, Is.EqualTo(4f).Within(0.0001f));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(frameSprite);
            Object.DestroyImmediate(frameTexture);
            Object.DestroyImmediate(material);
        }

        [Test]
        public void CombatMoveVfx_ExplicitCanvasSizeKeepsFrameScaleStable()
        {
            var root = new GameObject("MoveVfxCanvasSizeTest");
            var spriteObject = new GameObject("AnimatedSprite");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<MeshRenderer>();

            var firstTexture = new Texture2D(8, 4);
            var secondTexture = new Texture2D(12, 3);
            var firstSprite = Sprite.Create(firstTexture, new Rect(0f, 0f, 8f, 4f), Vector2.one * 0.5f);
            var secondSprite = Sprite.Create(secondTexture, new Rect(0f, 0f, 12f, 3f), Vector2.one * 0.5f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(new[] { firstSprite, secondSprite }));
            SetPrivateField(moveVfx, "spriteFPS", 10f);
            SetPrivateField(moveVfx, "spriteSize", new Vector2(5f, 5f));
            SetPrivateField(moveVfx, "spriteCanvasSize", new Vector2Int(96, 69));
            SetPrivateField(moveVfx, "spriteAspectMode", CombatMoveVFX.SpriteAspectMode.PreserveAuthoredHeight);

            MethodInfo awake = typeof(CombatMoveVFX).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(moveVfx, null);
            Vector3 firstScale = spriteObject.transform.localScale;

            moveVfx.StepPlayback(0.1f);
            Vector3 secondScale = spriteObject.transform.localScale;

            Assert.That(firstScale.x, Is.EqualTo(secondScale.x).Within(0.0001f));
            Assert.That(firstScale.y, Is.EqualTo(secondScale.y).Within(0.0001f));
            Assert.That(firstScale.x, Is.EqualTo(5f * 96f / 69f).Within(0.0001f));
            Assert.That(firstScale.y, Is.EqualTo(5f).Within(0.0001f));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(firstSprite);
            Object.DestroyImmediate(secondSprite);
            Object.DestroyImmediate(firstTexture);
            Object.DestroyImmediate(secondTexture);
        }

        [Test]
        public void PixelBattleSourceTextureUtility_PreservesExplicitCanvasAndFullFrameUv()
        {
            var texture = new Texture2D(96, 69);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 96f, 69f), Vector2.one * 0.5f);

            Vector2 canvas = PixelBattleVfxSourceTextureUtility.ResolveCanvasSize(sprite, new Vector2Int(96, 69));
            Vector4 textureSt = PixelBattleVfxSourceTextureUtility.ResolveTextureSt(sprite, new Vector2Int(96, 69));

            Assert.That(canvas, Is.EqualTo(new Vector2(96f, 69f)));
            Assert.That(textureSt, Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void CombatMoveVfx_MirrorXFlipsRootWithoutChangingOtherScaleAxes()
        {
            var root = new GameObject("MoveVfxMirrorTest");
            root.transform.localScale = new Vector3(2f, 3f, 4f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();

            moveVfx.SetMirrorX(true);
            Assert.That(moveVfx.MirrorX, Is.True);
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(-2f, 3f, 4f)));

            moveVfx.SetMirrorX(false);
            Assert.That(moveVfx.MirrorX, Is.False);
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(2f, 3f, 4f)));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void CombatMoveVfx_MirrorXSurvivesTravelScaleUpdates()
        {
            var root = new GameObject("TravelMirrorTest");
            root.transform.localScale = new Vector3(2f, 2f, 2f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();
            moveVfx.SetAutoDestroy(false);
            moveVfx.SetMirrorX(true);
            moveVfx.ConfigureTravel(new CombatMoveVFX.TravelPath(
                Vector3.zero,
                new Vector3(4f, 0f, 0f),
                null,
                null,
                Vector3.zero,
                Vector3.zero,
                false,
                false,
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                0f,
                Vector3.one,
                Vector3.one * 2f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps));

            moveVfx.StepPlayback(0.5f);

            Assert.That(root.transform.localScale.x, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(root.transform.localScale.y, Is.EqualTo(3f).Within(0.001f));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void PhaseConfig_CloneCopiesTravelWithoutSharingNestedAuthoringData()
        {
            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                enabled = true,
                spawnOffset = new Vector3(1f, 2f, 3f),
                travel = new CombatVFXLibrary.VFXTravelConfig
                {
                    enabled = true,
                    start = new CombatVFXLibrary.VFXTravelEndpoint
                    {
                        anchor = CombatVFXLibrary.VFXAnchor.AttackerCameraFocus,
                        offset = new Vector3(1f, 0f, 0f),
                        followAnchor = true
                    },
                    end = new CombatVFXLibrary.VFXTravelEndpoint
                    {
                        anchor = CombatVFXLibrary.VFXAnchor.TargetImpact,
                        offset = new Vector3(0f, 2f, 0f)
                    },
                    durationSource = CombatVFXLibrary.VFXTravelDurationSource.ExplicitSeconds,
                    speedUnitsPerSecond = 6f,
                    spriteTimingMode = CombatVFXLibrary.VFXTravelSpriteTimingMode.FitFrameSpanToTravel,
                    explicitDurationSeconds = 0.35f,
                    travelProgressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                    worldYArc = 0.5f,
                    startScaleMultiplier = new Vector3(1f, 2f, 1f),
                    endScaleMultiplier = new Vector3(2f, 1f, 2f)
                }
            };

            CombatVFXLibrary.VFXPhaseConfig clone = config.Clone();
            config.travel.start.offset = Vector3.zero;
            config.travel.travelProgressCurve.keys = new[] { new Keyframe(0f, 1f) };

            Assert.That(clone, Is.Not.SameAs(config));
            Assert.That(clone.travel, Is.Not.SameAs(config.travel));
            Assert.That(clone.travel.start, Is.Not.SameAs(config.travel.start));
            Assert.That(clone.travel.start.offset, Is.EqualTo(new Vector3(1f, 0f, 0f)));
            Assert.That(clone.travel.start.followAnchor, Is.True);
            Assert.That(clone.travel.explicitDurationSeconds, Is.EqualTo(0.35f));
            Assert.That(clone.travel.speedUnitsPerSecond, Is.EqualTo(6f));
            Assert.That(clone.travel.spriteTimingMode, Is.EqualTo(CombatVFXLibrary.VFXTravelSpriteTimingMode.FitFrameSpanToTravel));
            Assert.That(clone.travel.travelProgressCurve.length, Is.EqualTo(2));
        }

        [Test]
        public void CombatMoveVfx_SeparatesSpriteFrameSpanFromEffectLifetime()
        {
            var root = new GameObject("TravelTimingTest");
            var textures = new Texture2D[3];
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                textures[i] = new Texture2D(1, 1);
                sprites[i] = Sprite.Create(textures[i], new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            }

            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(sprites));
            SetPrivateField(moveVfx, "spriteFPS", 10f);

            Assert.That(moveVfx.GetSpriteFrameSpanDuration(), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(moveVfx.GetAuthoredEffectLifetimeDuration(), Is.EqualTo(0.3f).Within(0.0001f));

            Object.DestroyImmediate(root);
            for (int i = 0; i < sprites.Length; i++)
            {
                Object.DestroyImmediate(sprites[i]);
                Object.DestroyImmediate(textures[i]);
            }
        }

        [Test]
        public void CombatMoveVfx_FitFrameSpanToTravelArrivesOnFinalUniqueFrame()
        {
            var root = new GameObject("FitFrameSpanTravelTest");
            var spriteObject = new GameObject("SpriteRenderer");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<MeshRenderer>();
            var textures = new Texture2D[3];
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                textures[i] = new Texture2D(1, 1);
                sprites[i] = Sprite.Create(textures[i], new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            }

            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(sprites));
            SetPrivateField(moveVfx, "spriteFPS", 10f);
            InvokePrivateAwake(moveVfx);
            moveVfx.SetAutoDestroy(false);
            moveVfx.ConfigureTravel(CreateTravelPath(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                1f,
                CombatVFXLibrary.VFXTravelSpriteTimingMode.FitFrameSpanToTravel));

            moveVfx.StepPlayback(0.99f);
            Assert.That(moveVfx.CurrentSpriteFrame, Is.EqualTo(1));

            moveVfx.StepPlayback(0.01f);
            Assert.That(moveVfx.CurrentSpriteFrame, Is.EqualTo(2));
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(moveVfx.Duration, Is.EqualTo(1.5f).Within(0.001f));

            DestroyAnimatedMove(root, sprites, textures);
        }

        [Test]
        public void CombatMoveVfx_FitEffectLifetimeToTravelIncludesFinalFrameInterval()
        {
            var root = new GameObject("FitEffectLifetimeTravelTest");
            var spriteObject = new GameObject("SpriteRenderer");
            spriteObject.transform.SetParent(root.transform, false);
            var renderer = spriteObject.AddComponent<MeshRenderer>();
            var textures = new Texture2D[3];
            var sprites = new Sprite[3];
            for (int i = 0; i < sprites.Length; i++)
            {
                textures[i] = new Texture2D(1, 1);
                sprites[i] = Sprite.Create(textures[i], new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
            }

            var moveVfx = root.AddComponent<CombatMoveVFX>();
            SetPrivateField(moveVfx, "spriteRenderer", renderer);
            SetPrivateField(moveVfx, "spriteSequence", new SpriteAnimationSequence(sprites));
            SetPrivateField(moveVfx, "spriteFPS", 10f);
            InvokePrivateAwake(moveVfx);
            moveVfx.SetAutoDestroy(false);
            moveVfx.ConfigureTravel(CreateTravelPath(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                1f,
                CombatVFXLibrary.VFXTravelSpriteTimingMode.FitEffectLifetimeToTravel));

            moveVfx.StepPlayback(0.99f);
            Assert.That(moveVfx.CurrentSpriteFrame, Is.EqualTo(2));

            moveVfx.StepPlayback(0.01f);
            Assert.That(moveVfx.CurrentSpriteFrame, Is.EqualTo(2));
            Assert.That(root.transform.position, Is.EqualTo(new Vector3(10f, 0f, 0f)));
            Assert.That(moveVfx.Duration, Is.EqualTo(1f).Within(0.001f));

            DestroyAnimatedMove(root, sprites, textures);
        }

        [Test]
        public void CombatMoveVfx_TravelInterpolatesCurveArcAndScaleInWorldSpace()
        {
            var root = new GameObject("TravelInterpolationTest");
            root.transform.localScale = new Vector3(2f, 2f, 2f);
            var moveVfx = root.AddComponent<CombatMoveVFX>();
            moveVfx.SetAutoDestroy(false);

            AnimationCurve travelProgressCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.75f),
                new Keyframe(1f, 1f));
            AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            moveVfx.ConfigureTravel(new CombatMoveVFX.TravelPath(
                Vector3.zero,
                new Vector3(10f, 0f, 0f),
                null,
                null,
                Vector3.zero,
                Vector3.zero,
                false,
                false,
                1f,
                travelProgressCurve,
                2f,
                Vector3.one,
                Vector3.one * 2f,
                scaleCurve,
                CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps));

            moveVfx.StepPlayback(0.5f);

            Assert.That(root.transform.position.x, Is.EqualTo(7.5f).Within(0.001f));
            Assert.That(root.transform.position.y, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(root.transform.localScale, Is.EqualTo(new Vector3(3f, 3f, 3f)));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void CombatMoveVfx_TravelUsesSnapshotOrFollowEndpointAsConfigured()
        {
            var snapshotRoot = new GameObject("SnapshotTravelTest");
            var followRoot = new GameObject("FollowTravelTest");
            var endAnchor = new GameObject("TravelEndAnchor").transform;
            endAnchor.position = new Vector3(10f, 0f, 0f);

            var snapshotVfx = snapshotRoot.AddComponent<CombatMoveVFX>();
            var followVfx = followRoot.AddComponent<CombatMoveVFX>();
            snapshotVfx.SetAutoDestroy(false);
            followVfx.SetAutoDestroy(false);

            CombatMoveVFX.TravelPath snapshotPath = new CombatMoveVFX.TravelPath(
                Vector3.zero,
                endAnchor.position,
                null,
                endAnchor,
                Vector3.zero,
                Vector3.zero,
                false,
                false,
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                0f,
                Vector3.one,
                Vector3.one,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps);
            CombatMoveVFX.TravelPath followPath = new CombatMoveVFX.TravelPath(
                Vector3.zero,
                endAnchor.position,
                null,
                endAnchor,
                Vector3.zero,
                Vector3.zero,
                false,
                true,
                1f,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                0f,
                Vector3.one,
                Vector3.one,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                CombatVFXLibrary.VFXTravelSpriteTimingMode.PreservePrefabFps);
            snapshotVfx.ConfigureTravel(snapshotPath);
            followVfx.ConfigureTravel(followPath);

            endAnchor.position = new Vector3(20f, 0f, 0f);
            snapshotVfx.StepPlayback(0.5f);
            followVfx.StepPlayback(0.5f);

            Assert.That(snapshotRoot.transform.position.x, Is.EqualTo(5f).Within(0.001f));
            Assert.That(followRoot.transform.position.x, Is.EqualTo(10f).Within(0.001f));

            Object.DestroyImmediate(snapshotRoot);
            Object.DestroyImmediate(followRoot);
            Object.DestroyImmediate(endAnchor.gameObject);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing serialized field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static CombatMoveVFX.TravelPath CreateTravelPath(
            Vector3 startPosition,
            Vector3 endPosition,
            float duration,
            CombatVFXLibrary.VFXTravelSpriteTimingMode spriteTimingMode)
        {
            return new CombatMoveVFX.TravelPath(
                startPosition,
                endPosition,
                null,
                null,
                Vector3.zero,
                Vector3.zero,
                false,
                false,
                duration,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                0f,
                Vector3.one,
                Vector3.one,
                AnimationCurve.Linear(0f, 0f, 1f, 1f),
                spriteTimingMode);
        }

        private static void InvokePrivateAwake(CombatMoveVFX moveVfx)
        {
            MethodInfo awake = typeof(CombatMoveVFX).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(moveVfx, null);
        }

        private static void DestroyAnimatedMove(GameObject root, Sprite[] sprites, Texture2D[] textures)
        {
            Object.DestroyImmediate(root);
            for (int i = 0; i < sprites.Length; i++)
            {
                Object.DestroyImmediate(sprites[i]);
                Object.DestroyImmediate(textures[i]);
            }
        }
    }
}
