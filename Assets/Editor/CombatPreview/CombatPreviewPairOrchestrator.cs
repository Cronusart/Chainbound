using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    internal enum PairPhase
    {
        Idle,
        AttackStarted,
        FireFrame,
        WaitingForImpact,
        ImpactHit,
        Cooldown,
        Done
    }

    internal sealed class CombatPreviewPhasePrefabOverrides
    {
        public GameObject CastPrefab { get; set; }
        public GameObject StrikePrefab { get; set; }
        public GameObject ProjectilePrefab { get; set; }
        public GameObject ImpactPrefab { get; set; }

        public GameObject GetOverride(CombatVFXLibrary.VFXPhase phase)
        {
            switch (phase)
            {
                case CombatVFXLibrary.VFXPhase.Cast:
                    return CastPrefab;
                case CombatVFXLibrary.VFXPhase.Strike:
                    return StrikePrefab;
                case CombatVFXLibrary.VFXPhase.Projectile:
                    return ProjectilePrefab;
                case CombatVFXLibrary.VFXPhase.Impact:
                    return ImpactPrefab;
                default:
                    return null;
            }
        }

        public bool HasOverride(CombatVFXLibrary.VFXPhase phase)
        {
            return GetOverride(phase) != null;
        }

        public CombatPreviewPhasePrefabOverrides Clone()
        {
            return new CombatPreviewPhasePrefabOverrides
            {
                CastPrefab = CastPrefab,
                StrikePrefab = StrikePrefab,
                ProjectilePrefab = ProjectilePrefab,
                ImpactPrefab = ImpactPrefab
            };
        }
    }

    internal readonly struct CombatPreviewPairTiming
    {
        public CombatPreviewPairTiming(
            CombatChainlingVisual.CombatAnimState attackState,
            float attackerDuration,
            float fireTime,
            float impactDelay,
            float defenderHitDuration)
        {
            AttackState = attackState;
            AttackerDuration = attackerDuration;
            FireTime = fireTime;
            ImpactDelay = impactDelay;
            DefenderHitDuration = defenderHitDuration;
            TotalDuration = Mathf.Max(attackerDuration, fireTime + impactDelay + defenderHitDuration);
        }

        public CombatChainlingVisual.CombatAnimState AttackState { get; }
        public float AttackerDuration { get; }
        public float FireTime { get; }
        public float ImpactDelay { get; }
        public float DefenderHitDuration { get; }
        public float TotalDuration { get; }
    }

    internal sealed class CombatPreviewPairOrchestrator : IDisposable
    {
        private const float DefaultRangedTravelDuration = 0.20f;
        private const float PhaseHoldSeconds = 0.06f;

        private readonly CombatAnimationPreviewSubject _attacker;
        private readonly CombatAnimationPreviewSubject _defender;
        private readonly MoveDefinitionSO _moveDefinition;
        private readonly CombatVFXLibrary _vfxLibrary;
        private readonly MoveCategory _moveCategory;
        private readonly CombatVFXLibrary.VFXEntry _entry;
        private readonly CombatPreviewPhasePrefabOverrides _phaseOverrides;
        private readonly Camera _previewCamera;
        private readonly List<ActiveMoveVfxPreview> _activeMoveVfx = new List<ActiveMoveVfxPreview>();
        private readonly List<PendingPhaseVfxPreview> _pendingPhaseVfx = new List<PendingPhaseVfxPreview>();
        private readonly CombatPreviewPairTiming _timing;
        private readonly bool _targetWall;
        private readonly BlockKind _wallKind;
        private readonly BlockTier _wallTier;

        private float _elapsed;
        private bool _fireTriggered;
        private bool _impactTriggered;
        private bool _castTriggered;
        private float _phaseHoldUntil;
        private bool _started;

        public CombatPreviewPairOrchestrator(
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            MoveDefinitionSO moveDefinition,
            CombatVFXLibrary vfxLibrary,
            MoveCategory moveCategory,
            CombatPreviewPhasePrefabOverrides phaseOverrides = null,
            Camera previewCamera = null,
            bool targetWall = false,
            BlockKind wallKind = BlockKind.Wall,
            BlockTier wallTier = BlockTier.Lower)
        {
            _attacker = attacker;
            _defender = defender;
            _moveDefinition = moveDefinition;
            _vfxLibrary = vfxLibrary;
            _moveCategory = moveCategory;
            _phaseOverrides = phaseOverrides != null ? phaseOverrides.Clone() : new CombatPreviewPhasePrefabOverrides();
            _previewCamera = previewCamera;
            _targetWall = targetWall;
            _wallKind = wallKind;
            _wallTier = wallTier;
            _entry = _vfxLibrary != null && _moveDefinition != null
                ? _vfxLibrary.Resolve(VfxMoveKey.From(_moveDefinition)).entry
                : null;
            _timing = ResolveTiming(attacker, defender, _entry, moveCategory, _phaseOverrides, targetWall);
            CurrentPhase = PairPhase.Idle;
        }

        public event Action FireFrameTriggered;
        public event Action ImpactTriggered;

        public PairPhase CurrentPhase { get; private set; }
        public bool IsPlaying => CurrentPhase != PairPhase.Idle && CurrentPhase != PairPhase.Done;
        public bool HasActiveEffects => _activeMoveVfx.Count > 0 || _pendingPhaseVfx.Count > 0;
        public float Progress01 => _timing.TotalDuration > 0f ? Mathf.Clamp01(_elapsed / _timing.TotalDuration) : 1f;
        public CombatPreviewPairTiming Timing => _timing;
        public CombatChainlingVisual.CombatAnimState AttackState => _timing.AttackState;

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            Begin();

            if (_timing.FireTime <= 0f)
            {
                TriggerFireFrame(0f);
                if (_timing.ImpactDelay <= 0f)
                    TriggerImpact(0f);
            }
        }

        public static CombatPreviewPairTiming ResolveTiming(
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            CombatVFXLibrary.VFXEntry entry,
            MoveCategory moveCategory,
            CombatPreviewPhasePrefabOverrides phaseOverrides = null,
            bool targetWall = false)
        {
            CombatChainlingVisual.CombatAnimState attackState = ResolveAttackState(moveCategory);
            float attackerDuration = attacker != null && attacker.Visual != null
                ? Mathf.Max(0f, attacker.Visual.GetAnimationDuration(attackState))
                : 0f;
            float fireTime = attacker != null && attacker.Visual != null
                ? Mathf.Max(0f, attacker.Visual.GetMarkerTimeOrDefault(attackState))
                : attackerDuration * 0.5f;

            float impactDelay = -1f;
            if (entry != null ||
                (phaseOverrides != null &&
                 (phaseOverrides.HasOverride(CombatVFXLibrary.VFXPhase.Strike) ||
                  phaseOverrides.HasOverride(CombatVFXLibrary.VFXPhase.Projectile))))
            {
                CombatVFXLibrary.VFXPhase travelPhase =
                    attackState == CombatChainlingVisual.CombatAnimState.RangedAttack
                        ? CombatVFXLibrary.VFXPhase.Projectile
                        : CombatVFXLibrary.VFXPhase.Strike;
                float travelFallback = ResolvePhaseTravelDuration(
                    entry,
                    phaseOverrides,
                    travelPhase,
                    attacker,
                    defender,
                    attackState == CombatChainlingVisual.CombatAnimState.RangedAttack
                        ? DefaultRangedTravelDuration
                        : -1f,
                    targetWall);
                impactDelay = entry != null ? entry.GetEffectiveImpactDelay(travelFallback) : travelFallback;
            }

            if (impactDelay <= 0f)
                impactDelay = Mathf.Max(0f, attackerDuration - fireTime);

            float defenderHitDuration = defender != null && defender.Visual != null
                ? Mathf.Max(0f, defender.Visual.GetAnimationDuration(CombatChainlingVisual.CombatAnimState.Hit))
                : 0f;

            return new CombatPreviewPairTiming(attackState, attackerDuration, fireTime, impactDelay, defenderHitDuration);
        }

        public bool Update(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            bool changed = StepActiveMoveVfx(deltaTime);
            if (!IsPlaying)
                return changed;

            float previousElapsed = _elapsed;
            _elapsed += deltaTime;

            if (_attacker != null && _attacker.Visual != null)
                _attacker.Visual.StepPlayback(deltaTime);

            if (_impactTriggered && _defender != null && _defender.Visual != null)
                _defender.Visual.StepPlayback(deltaTime);

            if (!_fireTriggered && previousElapsed < _timing.FireTime && _elapsed >= _timing.FireTime)
            {
                TriggerFireFrame(_timing.FireTime);
                changed = true;
            }

            float impactTime = _timing.FireTime + _timing.ImpactDelay;
            if (!_impactTriggered && previousElapsed < impactTime && _elapsed >= impactTime)
            {
                TriggerImpact(impactTime);
                changed = true;
            }

            if (SpawnPendingPhaseVfx())
                changed = true;

            if (_impactTriggered && CurrentPhase == PairPhase.ImpactHit && _elapsed >= _phaseHoldUntil)
            {
                CurrentPhase = PairPhase.Cooldown;
                changed = true;
            }
            else if (_fireTriggered && !_impactTriggered && CurrentPhase == PairPhase.FireFrame && _elapsed >= _phaseHoldUntil)
            {
                CurrentPhase = PairPhase.WaitingForImpact;
                changed = true;
            }

            if (_elapsed >= _timing.TotalDuration && _pendingPhaseVfx.Count == 0)
            {
                CurrentPhase = PairPhase.Done;
                changed = true;
            }

            return changed;
        }

        public void Dispose()
        {
            for (int i = 0; i < _activeMoveVfx.Count; i++)
                _activeMoveVfx[i]?.StopParticles();

            _activeMoveVfx.Clear();
            _pendingPhaseVfx.Clear();
            _started = false;
        }

        private void Begin()
        {
            _elapsed = 0f;
            _fireTriggered = false;
            _impactTriggered = false;
            _castTriggered = false;
            _pendingPhaseVfx.Clear();
            CurrentPhase = PairPhase.AttackStarted;
            _phaseHoldUntil = 0f;

            if (_attacker != null && _attacker.Visual != null)
                _attacker.Visual.PlayOneShot(_timing.AttackState);

            if (_defender != null && _defender.Visual != null)
                _defender.Visual.PlayLoop(CombatChainlingVisual.CombatAnimState.Idle);

            if (HasStartupCast())
            {
                _castTriggered = true;
                TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase.Cast, 0f);
            }

            if (_targetWall && TryResolveWallStateRow(out CombatVFXFallbacks.WallStateRow wallRow))
                TryQueueWallPhaseVfx(wallRow.loopVfx, 0f);
        }

        private void TriggerFireFrame(float triggerTime)
        {
            _fireTriggered = true;
            CurrentPhase = PairPhase.FireFrame;
            _phaseHoldUntil = _elapsed + PhaseHoldSeconds;

            if (_timing.AttackState == CombatChainlingVisual.CombatAnimState.RangedAttack)
            {
                TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase.Projectile, triggerTime);
            }
            else
            {
                TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase.Strike, triggerTime);
            }

            if (!_castTriggered)
            {
                _castTriggered = true;
                TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase.Cast, triggerTime);
            }

            FireFrameTriggered?.Invoke();
        }

        private void TriggerImpact(float triggerTime)
        {
            _impactTriggered = true;
            CurrentPhase = PairPhase.ImpactHit;
            _phaseHoldUntil = _elapsed + PhaseHoldSeconds;

            TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase.Impact, triggerTime);
            IReadOnlyList<CombatWallPresentationBeat> beats = ResolvePreviewWallBeats(_targetWall, _wallKind, _wallTier);
            if (CombatWallPresentationPlanner.Contains(beats, CombatWallPresentationBeat.WallHit) &&
                TryResolveWallStateRow(out CombatVFXFallbacks.WallStateRow wallRow))
                TryQueueWallPhaseVfx(wallRow.hitVfx, triggerTime);
            if (CombatWallPresentationPlanner.Contains(beats, CombatWallPresentationBeat.BodyHitReaction) &&
                _defender != null && _defender.Visual != null)
                _defender.Visual.PlayOneShot(CombatChainlingVisual.CombatAnimState.Hit);

            ImpactTriggered?.Invoke();
        }

        public bool FirePhaseNow(CombatVFXLibrary.VFXPhase phase)
        {
            if (!TryResolvePhaseConfig(phase, out CombatVFXLibrary.VFXPhaseConfig config) || config == null || config.vfxPrefab == null)
                return false;

            SpawnPhaseVfxNow(config);
            return true;
        }

        private void TryQueuePhaseVfx(CombatVFXLibrary.VFXPhase phase, float triggerTime)
        {
            if (!TryResolvePhaseConfig(phase, out CombatVFXLibrary.VFXPhaseConfig config) || config == null || config.vfxPrefab == null)
                return;

            float spawnDelay = Mathf.Max(0f, config.spawnDelay);
            if (spawnDelay <= 0f)
            {
                SpawnPhaseVfxNow(config);
                return;
            }

            _pendingPhaseVfx.Add(new PendingPhaseVfxPreview(config, Mathf.Max(0f, triggerTime) + spawnDelay));
        }

        private bool TryResolveWallStateRow(out CombatVFXFallbacks.WallStateRow row)
        {
            CombatVFXFallbacks fallbacks = _vfxLibrary != null ? _vfxLibrary.Fallbacks : null;
            row = fallbacks != null ? fallbacks.ResolveWallState(_wallKind, _wallTier) : null;
            return row != null;
        }

        private void TryQueueWallPhaseVfx(CombatVFXLibrary.VFXPhaseConfig config, float triggerTime)
        {
            if (config == null || !config.enabled || config.vfxPrefab == null)
                return;

            float spawnDelay = Mathf.Max(0f, config.spawnDelay);
            if (spawnDelay <= 0f)
            {
                SpawnPhaseVfxNow(config);
                return;
            }

            _pendingPhaseVfx.Add(new PendingPhaseVfxPreview(config, Mathf.Max(0f, triggerTime) + spawnDelay));
        }

        private bool SpawnPendingPhaseVfx()
        {
            bool changed = false;
            for (int i = _pendingPhaseVfx.Count - 1; i >= 0; i--)
            {
                PendingPhaseVfxPreview pending = _pendingPhaseVfx[i];
                if (pending == null || pending.SpawnTime > _elapsed)
                    continue;

                _pendingPhaseVfx.RemoveAt(i);
                SpawnPhaseVfxNow(pending.Config);
                changed = true;
            }

            return changed;
        }

        private void SpawnPhaseVfxNow(CombatVFXLibrary.VFXPhaseConfig config)
        {
            if (config == null || config.vfxPrefab == null)
                return;

            bool usesTravel = config.travel != null && config.travel.enabled;
            bool mirrorX = ResolveVfxMirror();
            Transform staticAnchor = ResolveAnchorTransform(config.anchor, _attacker, _defender, _targetWall);
            Vector3 staticRawSpawnPosition = ResolveSpawnPosition(
                config.anchor,
                _attacker,
                _defender,
                _targetWall,
                staticAnchor);
            staticRawSpawnPosition += ApplyVfxFacingToOffset(config.spawnOffset, mirrorX);
            Vector3 staticSpawnPosition = ApplyPreviewCameraDepthOffset(staticRawSpawnPosition, config.cameraDepthOffset);

            Transform travelStartAnchor = null;
            Transform travelEndAnchor = null;
            Vector3 travelStartPosition = staticSpawnPosition;
            Vector3 travelEndPosition = staticSpawnPosition;
            if (usesTravel)
            {
                travelStartPosition = ResolveTravelEndpoint(
                    config,
                    config.travel.start,
                    mirrorX,
                    out travelStartAnchor);
                travelEndPosition = ResolveTravelEndpoint(
                    config,
                    config.travel.end,
                    mirrorX,
                    out travelEndAnchor);
            }

            Vector3 spawnPosition = usesTravel ? travelStartPosition : staticSpawnPosition;
            Transform rotationAnchor = usesTravel ? travelStartAnchor : staticAnchor;
            Quaternion rotation = rotationAnchor != null ? rotationAnchor.rotation : Quaternion.identity;
            Transform attachTarget = ResolveAttachTarget(config.attachPolicy, _attacker, _defender);
            CombatAnimationPreviewSubject host = ResolveHostSubject(
                usesTravel && config.travel.start != null ? config.travel.start.anchor : config.anchor);
            if (host == null)
                host = _attacker ?? _defender;

            Transform parent = usesTravel || config.attachPolicy == CombatVFXLibrary.VFXAttachPolicy.World
                ? host.PreviewVfxRoot
                : attachTarget;
            if (parent == null)
                parent = host.PreviewVfxRoot;

            GameObject instance = host.SpawnPrefab(config.vfxPrefab, spawnPosition, rotation, parent, out ParticleSystem[] previewParticleSystems);
            if (instance == null)
                return;

            ApplyRendererSortingOffset(instance, config.sortingOrderOffset);

            float phaseScaleMultiplier = _vfxLibrary != null
                ? _vfxLibrary.GetEffectivePhaseScaleMultiplier(config)
                : config.scaleMultiplier;

            var moveVfx = instance.GetComponent<CombatMoveVFX>();
            if (moveVfx != null)
            {
                moveVfx.SetMirrorX(mirrorX);
                moveVfx.SetBillboardCamera(_previewCamera);

                float phaseScaleDuration = usesTravel
                    ? CombatVfxTravelTimingResolver.ResolveDuration(config, travelStartPosition, travelEndPosition)
                    : moveVfx.GetAuthoredEffectLifetimeDuration();
                moveVfx.ConfigurePhaseScale(phaseScaleMultiplier, phaseScaleDuration, config.phaseScaleCurve);

                if (config.persistent)
                    moveVfx.SetAutoDestroy(false);

                if (!usesTravel && attachTarget != null && config.attachPolicy != CombatVFXLibrary.VFXAttachPolicy.World)
                    moveVfx.AttachTo(attachTarget, true);

                if (usesTravel)
                {
                    Vector3 startAnchorOffset = travelStartAnchor != null
                        ? travelStartPosition - travelStartAnchor.position
                        : Vector3.zero;
                    Vector3 endAnchorOffset = travelEndAnchor != null
                        ? travelEndPosition - travelEndAnchor.position
                        : Vector3.zero;
                    float duration = CombatVfxTravelTimingResolver.ResolveDuration(
                        config,
                        travelStartPosition,
                        travelEndPosition);
                    moveVfx.ConfigureTravel(new CombatMoveVFX.TravelPath(
                        travelStartPosition,
                        travelEndPosition,
                        travelStartAnchor,
                        travelEndAnchor,
                        startAnchorOffset,
                        endAnchorOffset,
                        config.travel.start != null && config.travel.start.followAnchor,
                        config.travel.end != null && config.travel.end.followAnchor,
                        duration,
                        config.travel.travelProgressCurve,
                        config.travel.worldYArc,
                        config.travel.startScaleMultiplier,
                        config.travel.endScaleMultiplier,
                        config.travel.scaleCurve,
                        config.travel.spriteTimingMode));
                }

                _activeMoveVfx.Add(new ActiveMoveVfxPreview(moveVfx, previewParticleSystems, instance));
            }
            else
            {
                if (!Mathf.Approximately(phaseScaleMultiplier, 1f))
                    instance.transform.localScale *= phaseScaleMultiplier;

                ApplyVfxRootMirror(instance, mirrorX);
                if (usesTravel)
                    return;

                if (attachTarget == null || config.attachPolicy == CombatVFXLibrary.VFXAttachPolicy.World)
                    return;

                instance.transform.SetParent(attachTarget, true);
            }
        }

        private Vector3 ResolveTravelEndpoint(
            CombatVFXLibrary.VFXPhaseConfig config,
            CombatVFXLibrary.VFXTravelEndpoint endpoint,
            bool mirrorX,
            out Transform anchor)
        {
            CombatVFXLibrary.VFXAnchor endpointAnchor = endpoint != null ? endpoint.anchor : config.anchor;
            anchor = ResolveAnchorTransform(endpointAnchor, _attacker, _defender, _targetWall);
            Vector3 position = ResolveSpawnPosition(endpointAnchor, _attacker, _defender, _targetWall, anchor);
            position += ApplyVfxFacingToOffset(config.spawnOffset, mirrorX);
            if (endpoint != null)
                position += ApplyVfxFacingToOffset(endpoint.offset, mirrorX);

            return ApplyPreviewCameraDepthOffset(position, config.cameraDepthOffset);
        }

        private bool StepActiveMoveVfx(float deltaTime)
        {
            bool changed = false;
            for (int i = _activeMoveVfx.Count - 1; i >= 0; i--)
            {
                ActiveMoveVfxPreview activeVfx = _activeMoveVfx[i];
                CombatMoveVFX moveVfx = activeVfx != null ? activeVfx.MoveVfx : null;
                if (moveVfx == null)
                {
                    _activeMoveVfx.RemoveAt(i);
                    changed = true;
                    continue;
                }

                moveVfx.StepPlayback(deltaTime);
                if (moveVfx != null && !moveVfx.IsFinished)
                    activeVfx.StepParticles(deltaTime);

                if (moveVfx == null || moveVfx.IsFinished)
                {
                    activeVfx.StopParticles();
                    _activeMoveVfx.RemoveAt(i);
                }

                changed = true;
            }

            return changed;
        }

        private sealed class ActiveMoveVfxPreview
        {
            private readonly ParticleSystem[] _particleSystems;
            private readonly Renderer[] _renderers;

            public ActiveMoveVfxPreview(CombatMoveVFX moveVfx, ParticleSystem[] particleSystems, GameObject root)
            {
                MoveVfx = moveVfx;
                _particleSystems = particleSystems;
                _renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
            }

            public CombatMoveVFX MoveVfx { get; }

            public void StepParticles(float deltaTime)
            {
                if (!HasVisibleRenderer())
                    return;

                CombatAnimationPreviewSubject.StepPreviewParticleSystems(_particleSystems, deltaTime);
            }

            public void StopParticles()
            {
                CombatAnimationPreviewSubject.StopPreviewParticleSystems(_particleSystems);
            }

            private bool HasVisibleRenderer()
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer != null && renderer.enabled)
                        return true;
                }

                return false;
            }
        }

        private sealed class PendingPhaseVfxPreview
        {
            public PendingPhaseVfxPreview(CombatVFXLibrary.VFXPhaseConfig config, float spawnTime)
            {
                Config = config;
                SpawnTime = spawnTime;
            }

            public CombatVFXLibrary.VFXPhaseConfig Config { get; }
            public float SpawnTime { get; }
        }

        private bool TryResolvePhaseConfig(CombatVFXLibrary.VFXPhase phase, out CombatVFXLibrary.VFXPhaseConfig config)
        {
            return TryResolvePhaseConfig(phase, _entry, _phaseOverrides, out config);
        }

        private bool HasStartupCast()
        {
            return _phaseOverrides.HasOverride(CombatVFXLibrary.VFXPhase.Cast) ||
                   (_entry != null && _entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Cast, out _));
        }

        private CombatAnimationPreviewSubject ResolveHostSubject(CombatVFXLibrary.VFXAnchor anchor)
        {
            switch (anchor)
            {
                case CombatVFXLibrary.VFXAnchor.TargetBarrier:
                case CombatVFXLibrary.VFXAnchor.TargetImpact:
                    return _defender ?? _attacker;
                default:
                    return _attacker ?? _defender;
            }
        }

        private static CombatChainlingVisual.CombatAnimState ResolveAttackState(MoveCategory moveCategory)
        {
            return moveCategory == MoveCategory.DamagingRanged
                ? CombatChainlingVisual.CombatAnimState.RangedAttack
                : CombatChainlingVisual.CombatAnimState.MeleeAttack;
        }

        private static float ResolvePhaseTravelDuration(
            CombatVFXLibrary.VFXEntry entry,
            CombatPreviewPhasePrefabOverrides phaseOverrides,
            CombatVFXLibrary.VFXPhase phase,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            float fallbackDuration,
            bool targetWall)
        {
            if (TryResolvePhaseConfig(phase, entry, phaseOverrides, out CombatVFXLibrary.VFXPhaseConfig config) &&
                config != null &&
                config.vfxPrefab != null &&
                config.travel != null &&
                config.travel.enabled)
            {
                bool mirrorX = attacker != null && attacker.Visual != null && attacker.Visual.FlipX;
                Vector3 startPosition = ResolveTravelEndpointForTiming(
                    config,
                    config.travel.start,
                    attacker,
                    defender,
                    mirrorX,
                    targetWall);
                Vector3 endPosition = ResolveTravelEndpointForTiming(
                    config,
                    config.travel.end,
                    attacker,
                    defender,
                    mirrorX,
                    targetWall);
                float resolvedDuration = CombatVfxTravelTimingResolver.ResolveDuration(
                    config,
                    startPosition,
                    endPosition);
                return Mathf.Max(
                    fallbackDuration,
                    Mathf.Max(0f, config.spawnDelay) + resolvedDuration);
            }

            return fallbackDuration;
        }

        private static Vector3 ResolveTravelEndpointForTiming(
            CombatVFXLibrary.VFXPhaseConfig config,
            CombatVFXLibrary.VFXTravelEndpoint endpoint,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            bool mirrorX,
            bool targetWall)
        {
            CombatVFXLibrary.VFXAnchor endpointAnchor = endpoint != null ? endpoint.anchor : config.anchor;
            Transform anchor = ResolveAnchorTransform(endpointAnchor, attacker, defender, targetWall);
            Vector3 position = ResolveSpawnPosition(endpointAnchor, attacker, defender, targetWall, anchor);
            position += ApplyVfxFacingToOffset(config.spawnOffset, mirrorX);
            if (endpoint != null)
                position += ApplyVfxFacingToOffset(endpoint.offset, mirrorX);

            return ApplyPreviewCameraDepthOffset(position, config.cameraDepthOffset);
        }

        internal static IReadOnlyList<CombatWallPresentationBeat> ResolvePreviewWallBeats(
            bool targetWall,
            BlockKind wallKind = BlockKind.Wall,
            BlockTier wallTier = BlockTier.Lower)
        {
            _ = wallKind;
            _ = wallTier;
            return CombatWallPresentationPlanner.PlanPreview(targetWall);
        }

        private static bool TryResolvePhaseConfig(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXEntry entry,
            CombatPreviewPhasePrefabOverrides phaseOverrides,
            out CombatVFXLibrary.VFXPhaseConfig config)
        {
            GameObject overridePrefab = phaseOverrides != null ? phaseOverrides.GetOverride(phase) : null;
            if (overridePrefab != null)
            {
                CombatVFXLibrary.VFXPhaseConfig baseConfig = null;
                if (entry != null)
                    entry.TryGetPhaseConfig(phase, out baseConfig);

                config = CloneOrDefaultPhaseConfig(phase, baseConfig);
                config.enabled = true;
                config.vfxPrefab = overridePrefab;
                return true;
            }

            config = null;
            return entry != null && entry.TryGetPhaseConfig(phase, out config) && config != null && config.vfxPrefab != null;
        }

        private static CombatVFXLibrary.VFXPhaseConfig CloneOrDefaultPhaseConfig(
            CombatVFXLibrary.VFXPhase phase,
            CombatVFXLibrary.VFXPhaseConfig source)
        {
            if (source == null)
                source = CreateDefaultPhaseConfig(phase);

            CombatVFXLibrary.VFXPhaseConfig clone = source.Clone();
            clone.enabled = true;
            clone.scaleMultiplier = Mathf.Approximately(clone.scaleMultiplier, 0f) ? 1f : clone.scaleMultiplier;
            return clone;
        }

        private static CombatVFXLibrary.VFXPhaseConfig CreateDefaultPhaseConfig(CombatVFXLibrary.VFXPhase phase)
        {
            var config = new CombatVFXLibrary.VFXPhaseConfig
            {
                enabled = true,
                scaleMultiplier = 1f,
                cameraDepthOffset = 0.35f,
                sortingOrderOffset = 100,
                persistent = false,
                spawnDelay = 0f
            };

            switch (phase)
            {
                case CombatVFXLibrary.VFXPhase.Cast:
                    config.anchor = CombatVFXLibrary.VFXAnchor.AttackerCameraFocus;
                    config.attachPolicy = CombatVFXLibrary.VFXAttachPolicy.AttachToAttacker;
                    break;
                case CombatVFXLibrary.VFXPhase.Projectile:
                    config.anchor = CombatVFXLibrary.VFXAnchor.AttackerProjectileOrigin;
                    config.attachPolicy = CombatVFXLibrary.VFXAttachPolicy.World;
                    config.travel.enabled = true;
                    config.travel.start.anchor = CombatVFXLibrary.VFXAnchor.AttackerProjectileOrigin;
                    config.travel.end.anchor = CombatVFXLibrary.VFXAnchor.TargetImpact;
                    config.travel.durationSource = CombatVFXLibrary.VFXTravelDurationSource.ExplicitSeconds;
                    config.travel.explicitDurationSeconds = 0.35f;
                    config.travel.worldYArc = 0.35f;
                    break;
                case CombatVFXLibrary.VFXPhase.Strike:
                case CombatVFXLibrary.VFXPhase.Impact:
                default:
                    config.anchor = CombatVFXLibrary.VFXAnchor.TargetImpact;
                    config.attachPolicy = CombatVFXLibrary.VFXAttachPolicy.World;
                    break;
            }

            return config;
        }

        private static Vector3 ApplyPreviewCameraDepthOffset(Vector3 position, float cameraDepthOffset)
        {
            if (Mathf.Approximately(cameraDepthOffset, 0f))
                return position;

            return position + Vector3.back * cameraDepthOffset;
        }

        private bool ResolveVfxMirror()
        {
            return _attacker != null && _attacker.Visual != null && _attacker.Visual.FlipX;
        }

        private static Vector3 ApplyVfxFacingToOffset(Vector3 authoredOffset, bool mirrorX)
        {
            if (mirrorX)
                authoredOffset.x = -authoredOffset.x;

            return authoredOffset;
        }

        private static void ApplyVfxRootMirror(GameObject root, bool mirrorX)
        {
            if (root == null || !mirrorX)
                return;

            Vector3 scale = root.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            root.transform.localScale = scale;
        }

        private static void ApplyRendererSortingOffset(GameObject root, int sortingOrderOffset)
        {
            if (root == null || sortingOrderOffset == 0)
                return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer != null)
                    renderer.sortingOrder += sortingOrderOffset;
            }
        }

        private static Transform ResolveAnchorTransform(
            CombatVFXLibrary.VFXAnchor anchor,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            bool targetWall)
        {
            switch (anchor)
            {
                case CombatVFXLibrary.VFXAnchor.AttackerCameraFocus:
                    return attacker != null && attacker.View != null ? attacker.View.CameraFocus : null;
                case CombatVFXLibrary.VFXAnchor.AttackerProjectileOrigin:
                    if (attacker == null || attacker.View == null)
                        return null;
                    return attacker.View.ProjectileOriginAnchor != null ? attacker.View.ProjectileOriginAnchor : attacker.View.CameraFocus;
                case CombatVFXLibrary.VFXAnchor.TargetBarrier:
                    if (defender == null || defender.View == null)
                        return null;
                    if (targetWall && defender.View.BarrierAnchor != null)
                        return defender.View.BarrierAnchor;
                    return defender.View.BarrierAnchor != null ? defender.View.BarrierAnchor : defender.View.ImpactAnchor;
                case CombatVFXLibrary.VFXAnchor.TargetImpact:
                    if (defender == null || defender.View == null)
                        return null;
                    if (targetWall && defender.View.BarrierAnchor != null)
                        return defender.View.BarrierAnchor;
                    return defender.View.ImpactAnchor != null ? defender.View.ImpactAnchor : defender.View.CameraFocus;
                default:
                    return null;
            }
        }

        private static Vector3 ResolveSpawnPosition(
            CombatVFXLibrary.VFXAnchor anchor,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender,
            bool targetWall,
            Transform resolvedAnchor)
        {
            if (anchor == CombatVFXLibrary.VFXAnchor.Midpoint)
            {
                Vector3 sourcePosition = attacker != null && attacker.View != null && attacker.View.CameraFocus != null
                    ? attacker.View.CameraFocus.position
                    : Vector3.zero;

                Transform targetAnchor = targetWall
                    ? (defender != null && defender.View != null ? defender.View.BarrierAnchor : null)
                    : (defender != null && defender.View != null ? defender.View.ImpactAnchor : null);
                if (targetAnchor == null && defender != null && defender.View != null)
                    targetAnchor = defender.View.CameraFocus;

                Vector3 targetPosition = targetAnchor != null ? targetAnchor.position : sourcePosition;
                return (sourcePosition + targetPosition) * 0.5f;
            }

            return resolvedAnchor != null ? resolvedAnchor.position : Vector3.zero;
        }

        private static Transform ResolveAttachTarget(
            CombatVFXLibrary.VFXAttachPolicy attachPolicy,
            CombatAnimationPreviewSubject attacker,
            CombatAnimationPreviewSubject defender)
        {
            switch (attachPolicy)
            {
                case CombatVFXLibrary.VFXAttachPolicy.AttachToAttacker:
                    return attacker != null && attacker.View != null ? attacker.View.CameraFocus : null;
                case CombatVFXLibrary.VFXAttachPolicy.AttachToTarget:
                    return defender != null && defender.View != null ? defender.View.ImpactAnchor : null;
                case CombatVFXLibrary.VFXAttachPolicy.AttachToBarrierAnchor:
                    return defender != null && defender.View != null ? defender.View.BarrierAnchor : null;
                default:
                    return null;
            }
        }
    }
}
