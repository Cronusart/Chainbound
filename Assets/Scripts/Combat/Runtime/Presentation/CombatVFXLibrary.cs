using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatVFXLibrary", menuName = "Chainbound/Combat/Presentation/VFX/VFX Library")]
    public class CombatVFXLibrary : ScriptableObject
    {
        public enum VFXPhase
        {
            Cast = 0,
            Strike = 3,
            Projectile = 1,
            Impact = 2
        }

        public enum VFXAnchor
        {
            AttackerCameraFocus,
            AttackerProjectileOrigin,
            TargetImpact,
            TargetBarrier,
            Midpoint
        }

        public enum VFXAttachPolicy
        {
            World,
            AttachToAttacker,
            AttachToTarget,
            AttachToBarrierAnchor
        }

        public enum VFXTravelDurationSource
        {
            [Tooltip("Use the sprite frame span: (frame count - 1) / FPS. The last frame arrives at the end anchor.")]
            SpriteFrameSpan,
            [Tooltip("Use the full authored effect lifetime, including the final sprite-frame display interval.")]
            EffectLifetime,
            [Tooltip("Use the explicit seconds value below.")]
            ExplicitSeconds,
            [Tooltip("Derive duration from the resolved world-space path length and Speed Units Per Second.")]
            ConstantSpeed
        }

        public enum VFXTravelSpriteTimingMode
        {
            [Tooltip("Keep the prefab's authored sprite FPS.")]
            PreservePrefabFps,
            [Tooltip("Retimes the sequence so the final unique frame arrives at the travel endpoint.")]
            FitFrameSpanToTravel,
            [Tooltip("Retimes the full sequence lifetime, including the final-frame interval, to the travel duration.")]
            FitEffectLifetimeToTravel
        }

        [Serializable]
        public class VFXTravelEndpoint
        {
            [Tooltip("Anchor used for this endpoint. Midpoint is always evaluated as a snapshot because it has no single live transform.")]
            public VFXAnchor anchor = VFXAnchor.TargetImpact;
            [Tooltip("Move-specific adjustment applied after the phase-wide spawn offset.")]
            public Vector3 offset = Vector3.zero;
            [Tooltip("When enabled, this endpoint keeps following its resolved combat anchor after the VFX has spawned.")]
            public bool followAnchor = false;

            public VFXTravelEndpoint Clone()
            {
                return new VFXTravelEndpoint
                {
                    anchor = anchor,
                    offset = offset,
                    followAnchor = followAnchor
                };
            }
        }

        [Serializable]
        public class VFXTravelConfig
        {
            [Tooltip("Moves this phase through world space. Disabled phases use their static placement settings.")]
            public bool enabled = false;
            public VFXTravelEndpoint start = new VFXTravelEndpoint
            {
                anchor = VFXAnchor.AttackerCameraFocus
            };
            public VFXTravelEndpoint end = new VFXTravelEndpoint
            {
                anchor = VFXAnchor.TargetImpact
            };
            public VFXTravelDurationSource durationSource = VFXTravelDurationSource.SpriteFrameSpan;
            [Min(0.01f)]
            [Tooltip("Average world-space travel speed used only when Duration Source is Constant Speed.")]
            public float speedUnitsPerSecond = 10f;
            [Tooltip("Controls whether the prefab keeps its authored FPS or fits its frame span/lifetime to Travel.")]
            public VFXTravelSpriteTimingMode spriteTimingMode = VFXTravelSpriteTimingMode.PreservePrefabFps;
            [Min(0.01f)]
            [Tooltip("Used only when Duration Source is Explicit Seconds.")]
            public float explicitDurationSeconds = 0.2f;
            [FormerlySerializedAs("positionCurve")]
            [Tooltip("Cumulative normalized travel progress / velocity profile. The curve controls acceleration and deceleration while ending at the destination.")]
            public AnimationCurve travelProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [Tooltip("World-space Y arc at the midpoint. Zero produces a straight path.")]
            public float worldYArc = 0f;
            [Tooltip("Multiplied by the prefab's authored root scale at travel start.")]
            public Vector3 startScaleMultiplier = Vector3.one;
            [Tooltip("Multiplied by the prefab's authored root scale at travel end.")]
            public Vector3 endScaleMultiplier = Vector3.one;
            [Tooltip("Maps normalized travel time to scale interpolation.")]
            public AnimationCurve scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            public void EnsureDefaults()
            {
                if (start == null)
                {
                    start = new VFXTravelEndpoint
                    {
                        anchor = VFXAnchor.AttackerCameraFocus
                    };
                }

                if (end == null)
                {
                    end = new VFXTravelEndpoint
                    {
                        anchor = VFXAnchor.TargetImpact
                    };
                }

                if (travelProgressCurve == null || travelProgressCurve.length == 0)
                    travelProgressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                if (scaleCurve == null || scaleCurve.length == 0)
                    scaleCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                if (speedUnitsPerSecond <= 0f)
                    speedUnitsPerSecond = 10f;
                else
                    speedUnitsPerSecond = Mathf.Max(0.01f, speedUnitsPerSecond);
            }

            public VFXTravelConfig Clone()
            {
                EnsureDefaults();
                return new VFXTravelConfig
                {
                    enabled = enabled,
                    start = start != null ? start.Clone() : null,
                    end = end != null ? end.Clone() : null,
                    durationSource = durationSource,
                    speedUnitsPerSecond = speedUnitsPerSecond,
                    spriteTimingMode = spriteTimingMode,
                    explicitDurationSeconds = explicitDurationSeconds,
                    travelProgressCurve = CloneCurve(travelProgressCurve),
                    worldYArc = worldYArc,
                    startScaleMultiplier = startScaleMultiplier,
                    endScaleMultiplier = endScaleMultiplier,
                    scaleCurve = CloneCurve(scaleCurve)
                };
            }

            private static AnimationCurve CloneCurve(AnimationCurve source)
            {
                if (source == null)
                    return null;

                return new AnimationCurve(source.keys)
                {
                    preWrapMode = source.preWrapMode,
                    postWrapMode = source.postWrapMode
                };
            }
        }

        [Serializable]
        public class VFXPhaseConfig
        {
            public bool enabled = false;
            public GameObject vfxPrefab;
            [Tooltip("Optional phase-level SoundEventSO played when this phase starts. Impact may intentionally overlap the entry outcome cue; outcome timing remains orchestrator-owned.")]
            public SoundEventSO phaseStartSound;
            [Tooltip("Optional combat-only post-FX cue (weight-blended on CombatPostFxVolumeController). Does not touch TimeOfDay / GameServices Global Volume.")]
            public CombatPostFxCueSO postFxCue;
            public VFXAnchor anchor = VFXAnchor.TargetImpact;
            public VFXAttachPolicy attachPolicy = VFXAttachPolicy.World;
            public float spawnDelay = 0f;
            [Tooltip("Base world scale for this phase. The Phase Scale Curve animates this value over the effect lifetime.")]
            public float scaleMultiplier = 1f;
            [Tooltip("Normalized phase-lifetime curve multiplied by Scale Multiplier. Use a constant value of 1 to keep the authored size unchanged.")]
            public AnimationCurve phaseScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            [Tooltip("World-space offset applied after resolving the phase anchor. Y is the battlefield vertical offset; use this to compensate for billboarded Chainling visual origins.")]
            public Vector3 spawnOffset = Vector3.zero;
            [Tooltip("World units to move this phase toward the active combat camera after anchor resolution. Small positive values help transparent VFX render in front of depth-writing Chainling quads.")]
            public float cameraDepthOffset = 0f;
            [Tooltip("Sorting order added to every renderer on the spawned VFX instance. Useful for transparent VFX that should layer above Chainling bodies without changing the prefab.")]
            public int sortingOrderOffset = 0;
            public bool persistent = false;
            [Tooltip("Optional world-space movement for this phase. Travel never attaches the spawned VFX to a combatant.")]
            public VFXTravelConfig travel = new VFXTravelConfig();

            public void EnsureDefaults()
            {
                if (phaseScaleCurve == null || phaseScaleCurve.length == 0)
                    phaseScaleCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

                if (travel == null)
                    travel = new VFXTravelConfig();

                travel.EnsureDefaults();
            }

            public VFXPhaseConfig Clone()
            {
                EnsureDefaults();

                return new VFXPhaseConfig
                {
                    enabled = enabled,
                    vfxPrefab = vfxPrefab,
                    phaseStartSound = phaseStartSound,
                    postFxCue = postFxCue,
                    anchor = anchor,
                    attachPolicy = attachPolicy,
                    spawnDelay = spawnDelay,
                    scaleMultiplier = scaleMultiplier,
                    phaseScaleCurve = CloneCurve(phaseScaleCurve),
                    spawnOffset = spawnOffset,
                    cameraDepthOffset = cameraDepthOffset,
                    sortingOrderOffset = sortingOrderOffset,
                    persistent = persistent,
                    travel = travel != null ? travel.Clone() : null
                };
            }

            private static AnimationCurve CloneCurve(AnimationCurve source)
            {
                if (source == null)
                    return null;

                return new AnimationCurve(source.keys)
                {
                    preWrapMode = source.preWrapMode,
                    postWrapMode = source.postWrapMode
                };
            }
        }

        [Serializable]
        public class VFXEntry
        {
            [Header("Matching")]
            public string moveDefinitionId;
            public bool isCategoryFallback;
            public MoveCategory fallbackCategory;
            [Tooltip("When set on the winning base entry, missing phases stay missing instead of being filled from later fallback slots.")]
            public bool suppressPhaseFill;

            [Header("Impact Timing")]
            [Tooltip("Seconds after the attacker fire frame before the move impact lands. " +
                     "Drives when the defender hit reaction, impact VFX, and damage popup appear. " +
                     "Falls back to the configured Strike or Projectile travel duration when <= 0. " +
                     "For melee moves, <= 0 uses the remaining attack animation duration (legacy behavior).")]
            public float impactDelay = -1f;

            /// <summary>
            /// Resolve the effective impact delay for this move entry.
            /// Returns a positive value when impact timing is explicitly configured,
            /// or -1 when the presenter should fall back to legacy behavior.
            /// </summary>
            public float GetEffectiveImpactDelay(float travelDurationFallback = -1f)
            {
                if (impactDelay > 0f)
                    return impactDelay;

                if (travelDurationFallback > 0f)
                    return travelDurationFallback;

                return impactDelay;
            }

            [Header("Phase-Aware Config")]
            public VFXPhaseConfig castVfx = new VFXPhaseConfig();
            public VFXPhaseConfig strikeVfx = new VFXPhaseConfig();
            public VFXPhaseConfig projectileVfx = new VFXPhaseConfig();
            public VFXPhaseConfig impactVfx = new VFXPhaseConfig();

            [Header("Outcome Audio")]
            [Tooltip("Optional authored impact SoundEventSO. The CombatSfxOrchestrator plays this at the resolved outcome beat before catalog hit/crit fallbacks.")]
            public SoundEventSO impactSound;
            [Tooltip("Optional authored miss/dodge SoundEventSO. Used only for Miss and Dodge outcomes.")]
            public SoundEventSO missSound;

            public bool TryGetPhaseConfig(VFXPhase phase, out VFXPhaseConfig config)
            {
                config = GetPhaseConfig(phase);
                if (config != null)
                    config.EnsureDefaults();

                return IsUsablePhaseConfig(config);
            }

            public VFXEntry Clone()
            {
                return new VFXEntry
                {
                    moveDefinitionId = moveDefinitionId,
                    isCategoryFallback = isCategoryFallback,
                    fallbackCategory = fallbackCategory,
                    suppressPhaseFill = suppressPhaseFill,
                    impactDelay = impactDelay,
                    castVfx = castVfx != null ? castVfx.Clone() : null,
                    strikeVfx = strikeVfx != null ? strikeVfx.Clone() : null,
                    projectileVfx = projectileVfx != null ? projectileVfx.Clone() : null,
                    impactVfx = impactVfx != null ? impactVfx.Clone() : null,
                    impactSound = impactSound,
                    missSound = missSound
                };
            }

            private VFXPhaseConfig GetPhaseConfig(VFXPhase phase)
            {
                switch (phase)
                {
                    case VFXPhase.Cast:
                        return castVfx;
                    case VFXPhase.Strike:
                        return strikeVfx;
                    case VFXPhase.Projectile:
                        return projectileVfx;
                    case VFXPhase.Impact:
                        return impactVfx;
                    default:
                        return null;
                }
            }

            private static bool IsUsablePhaseConfig(VFXPhaseConfig config)
            {
                return config != null && config.enabled &&
                       (config.vfxPrefab != null ||
                        config.phaseStartSound != null ||
                        (config.postFxCue != null && config.postFxCue.IsPlayable));
            }
        }

        [Header("World Calibration")]
        [Tooltip("Shared world-space scale applied to every registered combat VFX phase. 1.2 matches the Chainling Billboard scale in Combat2Battlefield. Phase scaleMultiplier remains an individual adjustment.")]
        [SerializeField, Min(0.01f)] private float worldScaleMultiplier = 1.2f;

        [SerializeField] private VFXEntry[] entries = new VFXEntry[0];

        [Header("Fallback Resolution")]
        [SerializeField] private CombatVFXFallbacks fallbacks;
        [SerializeField] private bool logResolution;

        [NonSerialized] private Dictionary<VfxMoveKey, VfxResolution> resolutionCache;
        [NonSerialized] private int cachedFallbacksVersion = int.MinValue;

        public float WorldScaleMultiplier => Mathf.Max(0.01f, worldScaleMultiplier);
        public IReadOnlyList<VFXEntry> Entries => entries;
        public CombatVFXFallbacks Fallbacks => fallbacks;
        public bool LogResolution => logResolution;

        public float GetEffectivePhaseScaleMultiplier(VFXPhaseConfig config)
        {
            float phaseScale = config != null && !Mathf.Approximately(config.scaleMultiplier, 0f)
                ? config.scaleMultiplier
                : 1f;
            return WorldScaleMultiplier * phaseScale;
        }

        public VFXEntry GetEntry(string moveDefinitionId)
        {
            if (entries == null)
                return null;

            if (!string.IsNullOrEmpty(moveDefinitionId))
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    VFXEntry entry = entries[i];
                    if (entry != null && !entry.isCategoryFallback &&
                        entry.moveDefinitionId == moveDefinitionId)
                        return entry;
                }
            }

            return null;
        }

        public VFXEntry GetEntryWithFallback(string moveDefinitionId, MoveCategory category)
        {
            MoveVfxRole role = category == MoveCategory.DamagingMelee || category == MoveCategory.DamagingRanged
                ? MoveVfxRole.Damage
                : category == MoveCategory.Passive
                    ? MoveVfxRole.Passive
                    : category == MoveCategory.StatusUtility
                        ? MoveVfxRole.Debuff
                        : MoveVfxRole.Buff;
            return Resolve(new VfxMoveKey(moveDefinitionId, category, ChainlingType.None, role)).entry;
        }

        public VfxResolution Resolve(VfxMoveKey key)
        {
            int version = fallbacks != null ? fallbacks.Version : 0;
            if (resolutionCache == null || cachedFallbacksVersion != version)
            {
                resolutionCache = new Dictionary<VfxMoveKey, VfxResolution>();
                cachedFallbacksVersion = version;
            }

            if (!resolutionCache.TryGetValue(key, out VfxResolution resolution))
            {
                resolution = CombatVfxResolver.Resolve(key, entries, fallbacks);
                resolutionCache[key] = resolution;
            }

            if (logResolution)
            {
                Debug.Log(
                    $"[VFX] {key.definitionId} -> {resolution.debugTrace}",
                    this);
            }
            return resolution;
        }

        private void OnValidate()
        {
            resolutionCache = null;
            cachedFallbacksVersion = int.MinValue;
            worldScaleMultiplier = Mathf.Max(0.01f, worldScaleMultiplier);

            if (entries == null)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                VFXEntry entry = entries[i];
                if (entry == null)
                    continue;

                entry.castVfx?.EnsureDefaults();
                entry.strikeVfx?.EnsureDefaults();
                entry.projectileVfx?.EnsureDefaults();
                entry.impactVfx?.EnsureDefaults();

                if (entry.impactSound != null && entry.impactVfx != null && entry.impactVfx.phaseStartSound != null)
                {
                    Debug.LogWarning(
                        $"[CombatVFXLibrary] '{entry.moveDefinitionId}' authors both Impact phaseStartSound and impactSound. " +
                        "Both are intentional lifecycle/outcome cues and will play independently.",
                        this);
                }
            }
        }
    }
}
