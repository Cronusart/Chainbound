using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    internal sealed class CombatAnimationPreviewSession : IDisposable
    {
        private const int MaxLogEntries = 64;
        private const float StepEpsilon = 0.0001f;
        private const float DefaultPreviewZoom = 1f;
        private const float MinPreviewZoom = 0.05f;
        private const float MaxPreviewZoom = 2.4f;
        private static readonly Vector3 SoloPreviewPosition = Vector3.zero;
        private static readonly Vector3 AttackerPreviewPosition = new Vector3(-4f, 0f, 0f);
        private static readonly Vector3 DefenderPreviewPosition = new Vector3(4f, 0f, 0f);

        private readonly CombatPreviewEventSimulator _eventSimulator = new CombatPreviewEventSimulator();
        private readonly CombatPreviewAudioPlayer _audioPlayer = new CombatPreviewAudioPlayer();
        private readonly List<string> _logEntries = new List<string>();
        private readonly List<CombatPreviewEventBinding> _frameEvents = new List<CombatPreviewEventBinding>();
        private readonly List<CombatPreviewOverlayMarker> _overlayMarkers = new List<CombatPreviewOverlayMarker>();
        private readonly List<SpriteFlipbookVfxPlayer> _activeSpriteVfx = new List<SpriteFlipbookVfxPlayer>();
        private readonly List<GameObject> _activeGraphVfx = new List<GameObject>();
        private readonly CombatPreviewPhasePrefabOverrides _phaseOverrides = new CombatPreviewPhasePrefabOverrides();

        private PreviewRenderUtility _previewUtility;
        private CombatAnimationPreviewSubject _subject;
        private CombatAnimationPreviewSubject _defenderSubject;
        private CombatVisualProfileSO _profile;
        private CombatVisualProfileSO _defenderProfile;
        private MoveDefinitionSO _moveDefinition;
        private Combat2ChainlingView _previewTemplate;
        private CombatVFXLibrary _vfxLibrary;
        private CombatPreviewPairOrchestrator _pairOrchestrator;
        private CombatChainlingVisual.CombatAnimState _selectedState = CombatChainlingVisual.CombatAnimState.Idle;
        private MoveCategory _moveCategory = MoveCategory.DamagingMelee;
        private bool _isPlaying;
        private bool _loop = true;
        private bool _flipX;
        private bool _includeAuthoredTrack = true;
        private bool _deterministicAudio = true;
        private bool _restartRequested;
        private double _lastUpdateTime;
        private float _zoom = DefaultPreviewZoom;

        public IReadOnlyList<string> LogEntries => _logEntries;
        public IReadOnlyList<CombatPreviewAnchorInfo> Anchors => _subject != null ? _subject.Anchors : Array.Empty<CombatPreviewAnchorInfo>();
        public IReadOnlyList<CombatPreviewAnchorInfo> DefenderAnchors => _defenderSubject != null ? _defenderSubject.Anchors : Array.Empty<CombatPreviewAnchorInfo>();
        public CombatPreviewEventSimulator EventSimulator => _eventSimulator;
        public CombatVisualProfileSO Profile => _profile;
        public CombatVisualProfileSO DefenderProfile => _defenderProfile;
        public MoveDefinitionSO MoveDefinition => _moveDefinition;
        public CombatVFXLibrary VfxLibrary => _vfxLibrary;
        public MoveCategory MoveCategory => _moveCategory;
        public CombatPresentationTrackSO ProfileTrack => _profile != null ? _profile.presentationTrack : null;
        public CombatPresentationTrackSO MoveTrack => _moveDefinition != null ? _moveDefinition.presentationTrack : null;
        public bool IsPlaying => _isPlaying;
        public bool HasDefender => _defenderSubject != null;
        public bool CanPlayAttackPair => _subject != null
            && _subject.Visual != null
            && _defenderSubject != null
            && _defenderSubject.Visual != null
            && _moveDefinition != null
            && _vfxLibrary != null;
        public bool CanPreviewMoveVfxPhase => CanPlayAttackPair;
        public bool IsPairPlaying => _pairOrchestrator != null && (_pairOrchestrator.IsPlaying || _pairOrchestrator.HasActiveEffects);
        public PairPhase CurrentPairPhase => _pairOrchestrator != null ? _pairOrchestrator.CurrentPhase : PairPhase.Idle;
        public float PairProgress01 => _pairOrchestrator != null ? _pairOrchestrator.Progress01 : 0f;
        public bool RequiresContinuousRepaint => _isPlaying || IsPairPlaying || _activeSpriteVfx.Count > 0 || _activeGraphVfx.Count > 0 || _overlayMarkers.Count > 0;
        public int CurrentFrame => _subject != null && _subject.Visual != null ? _subject.Visual.CurrentFrame : 0;
        public int FrameCount => _subject != null && _subject.Visual != null ? _subject.Visual.GetFrameCount(_selectedState) : 0;
        public float FramesPerSecond => _subject != null && _subject.Visual != null ? _subject.Visual.GetFramesPerSecond(_selectedState) : 0f;
        public float DurationSeconds => _subject != null && _subject.Visual != null ? _subject.Visual.GetAnimationDuration(_selectedState) : 0f;
        public Vector2 SpriteSize => _subject != null && _subject.Visual != null ? _subject.Visual.SpriteSize : Vector2.zero;
        public float Zoom => _zoom;
        public float MinZoom => MinPreviewZoom;
        public float MaxZoom => MaxPreviewZoom;
        public float DefaultZoom => DefaultPreviewZoom;

        public void Dispose()
        {
            DisposePreview();
            _audioPlayer.Dispose();
        }

        public void SetProfile(CombatVisualProfileSO profile, MoveDefinitionSO moveDefinition, Combat2ChainlingView previewTemplate)
        {
            bool needsRebuild = _previewUtility == null || _profile != profile || _previewTemplate != previewTemplate;
            bool moveChanged = _moveDefinition != moveDefinition;
            if (moveChanged)
                StopPairInternal(false, false);

            _profile = profile;
            _moveDefinition = moveDefinition;
            _previewTemplate = previewTemplate;

            if (_profile == null)
            {
                DisposePreview();
                return;
            }

            if (needsRebuild)
                CreatePreview();
            else
                ApplyProfiles();
        }

        public void SetDefenderProfile(CombatVisualProfileSO defenderProfile)
        {
            bool presenceChanged = (_defenderProfile == null) != (defenderProfile == null);
            if (_defenderProfile == defenderProfile && !presenceChanged)
                return;

            StopPairInternal(false, false);
            _defenderProfile = defenderProfile;

            if (_profile == null)
                return;

            if (_previewUtility == null || presenceChanged)
                CreatePreview();
            else
                ApplyProfiles();
        }

        public void SetVfxLibrary(CombatVFXLibrary vfxLibrary)
        {
            if (_vfxLibrary == vfxLibrary)
                return;

            _vfxLibrary = vfxLibrary;
            ClearSpawnedPreview();
        }

        public void SetMoveCategory(MoveCategory moveCategory)
        {
            if (_moveCategory == moveCategory)
                return;

            _moveCategory = moveCategory;
            ClearSpawnedPreview();
        }

        public void SetPhaseOverride(CombatVFXLibrary.VFXPhase phase, GameObject prefab)
        {
            switch (phase)
            {
                case CombatVFXLibrary.VFXPhase.Cast:
                    if (_phaseOverrides.CastPrefab == prefab)
                        return;
                    _phaseOverrides.CastPrefab = prefab;
                    break;
                case CombatVFXLibrary.VFXPhase.Strike:
                    if (_phaseOverrides.StrikePrefab == prefab)
                        return;
                    _phaseOverrides.StrikePrefab = prefab;
                    break;
                case CombatVFXLibrary.VFXPhase.Projectile:
                    if (_phaseOverrides.ProjectilePrefab == prefab)
                        return;
                    _phaseOverrides.ProjectilePrefab = prefab;
                    break;
                case CombatVFXLibrary.VFXPhase.Impact:
                    if (_phaseOverrides.ImpactPrefab == prefab)
                        return;
                    _phaseOverrides.ImpactPrefab = prefab;
                    break;
                default:
                    return;
            }

            ClearSpawnedPreview();
        }

        public void SetState(CombatChainlingVisual.CombatAnimState state)
        {
            if (_selectedState == state)
                return;

            StopPairInternal(false, false);
            _selectedState = state;
            _isPlaying = false;
            ClearSpawnedPreview();
            ResetPlaybackToStart();
            AddLog($"State -> {_selectedState}");
        }

        public void SetLoop(bool loop)
        {
            _loop = loop;
        }

        public void SetZoom(float zoom)
        {
            _zoom = Mathf.Clamp(zoom, MinPreviewZoom, MaxPreviewZoom);
        }

        public void AdjustZoom(float delta)
        {
            SetZoom(_zoom + delta);
        }

        public void ResetZoom()
        {
            _zoom = DefaultPreviewZoom;
        }

        public void SetFlipX(bool flipX)
        {
            _flipX = flipX;
            ApplySubjectLayout();

            if (_subject != null && _subject.Visual != null)
                _subject.Visual.SampleFrame(_selectedState, CurrentFrame, IsPreviewOneShotState());
        }

        public void SetIncludeAuthoredTrack(bool includeAuthoredTrack)
        {
            _includeAuthoredTrack = includeAuthoredTrack;
        }

        public void SetDeterministicAudio(bool deterministicAudio)
        {
            _deterministicAudio = deterministicAudio;
        }

        public void Play()
        {
            if (_subject == null || _subject.Visual == null || FrameCount <= 0)
                return;

            if (_pairOrchestrator != null)
                StopPairInternal(false, true);

            if (IsPreviewOneShotState() && CurrentFrame >= FrameCount - 1)
                ResetPlaybackToStart();

            _isPlaying = true;
            _restartRequested = false;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        public void PlayAttackPair()
        {
            if (!CanPlayAttackPair)
                return;

            StopPairInternal(false, false);
            _isPlaying = false;
            _restartRequested = false;
            _selectedState = ResolveAttackState(_moveCategory);
            ClearSpawnedPreview();
            ResetPlaybackToStart();

            _pairOrchestrator = CreatePairOrchestrator();
            _pairOrchestrator.Start();
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            AddLog($"Pair preview started: {_selectedState} -> {_moveDefinition.name}.");
        }

        public void PreviewMoveVfxPhase(CombatVFXLibrary.VFXPhase phase)
        {
            if (!CanPreviewMoveVfxPhase)
                return;

            if (_pairOrchestrator != null && _pairOrchestrator.IsPlaying)
                StopPairInternal(false, true);

            if (_pairOrchestrator == null)
                _pairOrchestrator = CreatePairOrchestrator();

            _isPlaying = false;
            _restartRequested = false;
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            bool spawned = _pairOrchestrator.FirePhaseNow(phase);
            AddLog(spawned
                ? $"Move VFX phase preview fired: {phase}."
                : $"Move VFX phase preview skipped: no {phase} prefab is configured.");
        }

        public void Pause()
        {
            _isPlaying = false;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
        }

        public void Stop()
        {
            if (_pairOrchestrator != null)
            {
                StopPair();
                return;
            }

            _isPlaying = false;
            _audioPlayer.StopAll();
            ClearSpawnedPreview();
            ResetPlaybackToStart();
            AddLog("Playback reset.");
        }

        public void StopPair()
        {
            StopPairInternal(true, true);
        }

        public bool TryGetPairTiming(out CombatPreviewPairTiming timing)
        {
            if (_pairOrchestrator != null)
            {
                timing = _pairOrchestrator.Timing;
                return true;
            }

            if (CanPlayAttackPair)
            {
                CombatVFXLibrary.VFXEntry entry = _vfxLibrary.GetEntryWithFallback(_moveDefinition.moveId, _moveCategory);
                timing = CombatPreviewPairOrchestrator.ResolveTiming(_subject, _defenderSubject, entry, _moveCategory);
                return true;
            }

            timing = default;
            return false;
        }

        public void StepFrame(int delta)
        {
            if (_subject == null || _subject.Visual == null || FrameCount <= 0 || delta == 0)
                return;

            if (_pairOrchestrator != null)
            {
                StopPairInternal(false, false);
                ClearSpawnedPreview();
            }

            _isPlaying = false;
            _lastUpdateTime = EditorApplication.timeSinceStartup;

            if (delta > 0)
            {
                for (int i = 0; i < delta; i++)
                    AdvanceOneFrame();
                return;
            }

            int targetFrame = CurrentFrame + delta;
            while (targetFrame < 0)
                targetFrame += FrameCount;

            if (!IsPreviewOneShotState() || _loop)
                targetFrame %= FrameCount;
            else
                targetFrame = Mathf.Clamp(targetFrame, 0, FrameCount - 1);

            _subject.Visual.SampleFrame(_selectedState, targetFrame, IsPreviewOneShotState());
        }

        public void ScrubToFrame(int frame, bool replayCurrentFrameEvents)
        {
            if (_subject == null || _subject.Visual == null || FrameCount <= 0)
                return;

            if (_pairOrchestrator != null)
            {
                StopPairInternal(false, false);
                ClearSpawnedPreview();
            }

            frame = Mathf.Clamp(frame, 0, FrameCount - 1);
            _subject.Visual.SampleFrame(_selectedState, frame, IsPreviewOneShotState());

            if (replayCurrentFrameEvents)
                ReplayCurrentFrameEvents();
        }

        public void ReplayCurrentFrameEvents()
        {
            if (_subject == null || _subject.Visual == null || FrameCount <= 0)
                return;

            if (_pairOrchestrator != null)
            {
                StopPairInternal(false, false);
                ClearSpawnedPreview();
            }

            if (IsPreviewOneShotState())
                _subject.Visual.EmitCurrentFrameSignals();

            DispatchFrameEvents(_subject, _profile, _moveDefinition, _selectedState, CurrentFrame, true, true, true, "ATK");
        }
        public void ClearSpawnedPreview()
        {
            StopPairInternal(false, false);
            _audioPlayer.StopAll();
            _activeSpriteVfx.Clear();
            _activeGraphVfx.Clear();
            _overlayMarkers.Clear();
            _subject?.ClearSpawnedPreviewObjects();
            _defenderSubject?.ClearSpawnedPreviewObjects();
        }

        public bool TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState state, out int frame)
        {
            frame = -1;
            return _subject != null && _subject.Visual != null && _subject.Visual.TryGetMarkerFrame(state, out frame);
        }

        public void GetFrameEvents(int frame, List<CombatPreviewEventBinding> results)
        {
            _eventSimulator.GetFrameEvents(_profile, _moveDefinition, _selectedState, frame, _includeAuthoredTrack, results);
        }

        public void CountFrameEvents(int frame, out int profileCount, out int moveCount, out int previewCount)
        {
            _eventSimulator.CountFrameEvents(_profile, _moveDefinition, _selectedState, frame, _includeAuthoredTrack, out profileCount, out moveCount, out previewCount);
        }

        public bool Update()
        {
            double now = EditorApplication.timeSinceStartup;
            float deltaTime = _lastUpdateTime > 0d ? (float)(now - _lastUpdateTime) : 0f;
            _lastUpdateTime = now;

            bool changed = PruneOverlayMarkers(now);

            if (_pairOrchestrator != null)
            {
                if (deltaTime > 0f && UpdatePairPlayback(deltaTime))
                    changed = true;
            }
            else if (_subject != null && _subject.Visual != null && _isPlaying && deltaTime > 0f)
            {
                int previousFrame = CurrentFrame;
                _subject.Visual.StepPlayback(deltaTime);
                int nextFrame = CurrentFrame;
                if (nextFrame != previousFrame)
                {
                    DispatchFramesCrossed(_subject, _profile, _moveDefinition, _selectedState, previousFrame, nextFrame, true, true, true, "ATK");
                    AddLog($"Frame {nextFrame + 1}/{Mathf.Max(1, FrameCount)}");
                    changed = true;
                }

                if (_restartRequested)
                {
                    ResetPlaybackToStart();
                    changed = true;
                }
            }

            if (deltaTime > 0f && StepActiveEffects(deltaTime))
                changed = true;

            return changed;
        }

        public void DrawPreview(Rect rect, bool showAnchors, PresentationAnchorTarget selectedAnchor)
        {
            if (_subject == null || _previewUtility == null)
            {
                EditorGUI.HelpBox(rect, "Select a CombatVisualProfileSO to build the animation preview subject.", MessageType.Info);
                return;
            }

            if (Event.current.type != EventType.Repaint)
                return;

            ConfigureCamera();
            _previewUtility.BeginPreview(rect, GUIStyle.none);
            _previewUtility.camera.Render();
            Texture texture = _previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            CombatPreviewGizmos.DrawOverlay(rect, _previewUtility.camera, _subject, _defenderSubject, showAnchors, selectedAnchor, _overlayMarkers);
        }

        private void CreatePreview()
        {
            DisposePreview();

            _previewUtility = new PreviewRenderUtility();
            ConfigureLighting();

            _subject = new CombatAnimationPreviewSubject(_previewTemplate);
            _previewUtility.AddSingleGO(_subject.Root);
            SubscribeToVisual(_subject.Visual);

            if (_defenderProfile != null)
            {
                _defenderSubject = new CombatAnimationPreviewSubject(_previewTemplate);
                _previewUtility.AddSingleGO(_defenderSubject.Root);
            }

            ApplyProfiles();
            AddLog(_defenderSubject != null
                ? $"Pair preview subjects ready for '{_profile.name}' vs '{_defenderProfile.name}'."
                : $"Preview subject ready for '{_profile.name}'.");
        }

        private void ApplyProfiles()
        {
            if (_subject == null || _subject.Visual == null || _profile == null)
                return;

            _subject.Visual.ApplyProfile(_profile);

            if (_defenderSubject != null && _defenderSubject.Visual != null && _defenderProfile != null)
                _defenderSubject.Visual.ApplyProfile(_defenderProfile);

            ApplySubjectLayout();
            ResetPlaybackToStart();
        }

        private void ApplySubjectLayout()
        {
            if (_subject == null)
                return;

            if (_defenderSubject != null)
            {
                _subject.SetPosition(AttackerPreviewPosition);
                _subject.SetFlipX(false);
                _defenderSubject.SetPosition(DefenderPreviewPosition);
                _defenderSubject.SetFlipX(true);
                return;
            }

            _subject.SetPosition(SoloPreviewPosition);
            _subject.SetFlipX(_flipX);
        }

        private void ResetPlaybackToStart()
        {
            _restartRequested = false;

            if (_subject != null && _subject.Visual != null)
            {
                if (IsPreviewOneShotState())
                    _subject.Visual.PlayOneShot(_selectedState);
                else
                    _subject.Visual.PlayLoop(_selectedState);

                _subject.Visual.StepPlayback(0f);
            }

            if (_defenderSubject != null && _defenderSubject.Visual != null)
            {
                _defenderSubject.Visual.PlayLoop(CombatChainlingVisual.CombatAnimState.Idle);
                _defenderSubject.Visual.StepPlayback(0f);
            }
        }

        private void DisposePreview()
        {
            StopPairInternal(false, false);

            if (_subject != null && _subject.Visual != null)
                UnsubscribeFromVisual(_subject.Visual);

            ClearSpawnedPreview();

            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }

            _subject?.Dispose();
            _subject = null;
            _defenderSubject?.Dispose();
            _defenderSubject = null;
            _isPlaying = false;
            _restartRequested = false;
        }

        private void ConfigureLighting()
        {
            if (_previewUtility == null)
                return;

            _previewUtility.lights[0].intensity = 1.2f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 140f, 0f);
            _previewUtility.lights[1].intensity = 0.8f;
            _previewUtility.lights[1].transform.rotation = Quaternion.Euler(340f, 220f, 0f);
        }

        private void ConfigureCamera()
        {
            if (_previewUtility == null || _subject == null)
                return;

            Camera camera = _previewUtility.camera;
            Vector3 attackerFocus = GetCameraFocus(_subject);
            Vector3 defenderFocus = _defenderSubject != null ? GetCameraFocus(_defenderSubject) : attackerFocus;
            bool isPair = _defenderSubject != null;
            Vector3 focus = isPair ? (attackerFocus + defenderFocus) * 0.5f : attackerFocus;

            float attackerHeight = _subject != null && _subject.Visual != null && _subject.Visual.SpriteSize.y > 0f ? _subject.Visual.SpriteSize.y : 2f;
            float defenderHeight = _defenderSubject != null && _defenderSubject.Visual != null && _defenderSubject.Visual.SpriteSize.y > 0f ? _defenderSubject.Visual.SpriteSize.y : attackerHeight;
            float spriteHeight = Mathf.Max(1.5f, Mathf.Max(attackerHeight, defenderHeight));

            Vector3 baseOffset;
            if (isPair)
            {
                float focusWidth = Mathf.Abs(defenderFocus.x - attackerFocus.x);
                baseOffset = new Vector3(0f, spriteHeight * 0.10f, -Mathf.Max(9f, (focusWidth * 1.15f) + 3f, spriteHeight * 1.75f));
                camera.fieldOfView = 28f;
            }
            else
            {
                baseOffset = new Vector3(0.35f, spriteHeight * 0.08f, -Mathf.Max(3.5f, spriteHeight * 1.35f));
                camera.fieldOfView = 26f;
            }

            Vector3 cameraOffset = baseOffset / Mathf.Max(0.01f, _zoom);
            camera.transform.position = focus + cameraOffset;
            camera.transform.rotation = Quaternion.LookRotation((focus - camera.transform.position).normalized, Vector3.up);
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 80f;
        }
        private void SubscribeToVisual(CombatChainlingVisual visual)
        {
            if (visual == null)
                return;

            visual.OnMeleeContact += HandleMeleeContact;
            visual.OnRangedRelease += HandleRangedRelease;
            visual.OnAnimationComplete += HandleAnimationComplete;
        }

        private void UnsubscribeFromVisual(CombatChainlingVisual visual)
        {
            if (visual == null)
                return;

            visual.OnMeleeContact -= HandleMeleeContact;
            visual.OnRangedRelease -= HandleRangedRelease;
            visual.OnAnimationComplete -= HandleAnimationComplete;
        }

        private void HandleMeleeContact()
        {
            Vector3 position = _subject != null && _subject.GetAnchor(PresentationAnchorTarget.Impact) != null
                ? _subject.GetAnchor(PresentationAnchorTarget.Impact).position
                : Vector3.zero;
            AddOverlayMarker("Melee", position, new Color(0.90f, 0.35f, 0.35f, 1f));
            AddLog($"Marker fired: MeleeContact on frame {CurrentFrame}.");
        }

        private void HandleRangedRelease()
        {
            Vector3 position = _subject != null && _subject.GetAnchor(PresentationAnchorTarget.ProjectileOrigin) != null
                ? _subject.GetAnchor(PresentationAnchorTarget.ProjectileOrigin).position
                : Vector3.zero;
            AddOverlayMarker("Release", position, new Color(0.25f, 0.65f, 0.95f, 1f));
            AddLog($"Marker fired: RangedRelease on frame {CurrentFrame}.");
        }

        private void HandleAnimationComplete()
        {
            AddLog($"Animation complete: {_selectedState}.");
            if (!_isPlaying)
                return;

            if (IsPreviewOneShotState() && _loop)
                _restartRequested = true;
            else
                _isPlaying = false;
        }

        private void HandlePairFireFrame()
        {
            if (_subject == null)
                return;

            PresentationAnchorTarget anchor = _selectedState == CombatChainlingVisual.CombatAnimState.RangedAttack
                ? PresentationAnchorTarget.ProjectileOrigin
                : PresentationAnchorTarget.Impact;
            Vector3 position = _subject.GetAnchor(anchor) != null ? _subject.GetAnchor(anchor).position : _subject.Root.transform.position;
            AddOverlayMarker("Fire", position, new Color(0.96f, 0.63f, 0.20f, 1f));
        }

        private void HandlePairImpact()
        {
            if (_defenderSubject == null)
                return;

            Vector3 position = _defenderSubject.GetAnchor(PresentationAnchorTarget.Impact) != null
                ? _defenderSubject.GetAnchor(PresentationAnchorTarget.Impact).position
                : _defenderSubject.Root.transform.position;
            AddOverlayMarker("Impact", position, new Color(0.96f, 0.32f, 0.32f, 1f));
        }

        private bool UpdatePairPlayback(float deltaTime)
        {
            if (_pairOrchestrator == null)
                return false;

            PairPhase previousPhase = _pairOrchestrator.CurrentPhase;
            int attackerPreviousFrame = _subject != null && _subject.Visual != null ? _subject.Visual.CurrentFrame : -1;
            int defenderPreviousFrame = _defenderSubject != null && _defenderSubject.Visual != null ? _defenderSubject.Visual.CurrentFrame : -1;
            CombatChainlingVisual.CombatAnimState defenderPreviousState = _defenderSubject != null && _defenderSubject.Visual != null
                ? _defenderSubject.Visual.CurrentState
                : CombatChainlingVisual.CombatAnimState.Idle;

            bool changed = _pairOrchestrator.Update(deltaTime);

            if (_subject != null && _subject.Visual != null)
            {
                int attackerNextFrame = _subject.Visual.CurrentFrame;
                if (attackerNextFrame != attackerPreviousFrame)
                {
                    DispatchFramesCrossed(_subject, _profile, _moveDefinition, _pairOrchestrator.AttackState, attackerPreviousFrame, attackerNextFrame, true, true, true, "ATK");
                    AddLog($"ATK frame {attackerNextFrame + 1}/{Mathf.Max(1, _subject.Visual.GetFrameCount(_pairOrchestrator.AttackState))}");
                    changed = true;
                }
            }

            if (_defenderSubject != null && _defenderSubject.Visual != null)
            {
                CombatChainlingVisual.CombatAnimState defenderState = _defenderSubject.Visual.CurrentState;
                int defenderNextFrame = _defenderSubject.Visual.CurrentFrame;
                if (defenderPreviousState != CombatChainlingVisual.CombatAnimState.Hit && defenderState == CombatChainlingVisual.CombatAnimState.Hit)
                {
                    DispatchFrameEvents(_defenderSubject, _defenderProfile, null, CombatChainlingVisual.CombatAnimState.Hit, defenderNextFrame, true, false, false, "DEF");
                    changed = true;
                }
                else if (defenderState == CombatChainlingVisual.CombatAnimState.Hit && defenderNextFrame != defenderPreviousFrame)
                {
                    DispatchFramesCrossed(_defenderSubject, _defenderProfile, null, CombatChainlingVisual.CombatAnimState.Hit, defenderPreviousFrame, defenderNextFrame, true, false, false, "DEF");
                    changed = true;
                }
            }

            if (previousPhase != _pairOrchestrator.CurrentPhase)
            {
                AddLog($"Pair phase -> {_pairOrchestrator.CurrentPhase}.");
                changed = true;
            }

            return changed;
        }

        private void AdvanceOneFrame()
        {
            if (_subject == null || _subject.Visual == null || FrameCount <= 0)
                return;

            if (IsPreviewOneShotState() && CurrentFrame >= FrameCount - 1)
            {
                if (_loop)
                {
                    ResetPlaybackToStart();
                    DispatchFrameEvents(_subject, _profile, _moveDefinition, _selectedState, CurrentFrame, true, true, true, "ATK");
                }

                return;
            }

            int previousFrame = CurrentFrame;
            float deltaTime = (1f / Mathf.Max(0.01f, FramesPerSecond)) + StepEpsilon;
            _subject.Visual.StepPlayback(deltaTime);
            DispatchFramesCrossed(_subject, _profile, _moveDefinition, _selectedState, previousFrame, CurrentFrame, true, true, true, "ATK");

            if (_restartRequested)
                ResetPlaybackToStart();
        }

        private void DispatchFramesCrossed(
            CombatAnimationPreviewSubject subject,
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int previousFrame,
            int nextFrame,
            bool allowProfileTrack,
            bool allowMoveTrack,
            bool allowPreviewEvents,
            string roleLabel)
        {
            int frameCount = subject != null && subject.Visual != null ? subject.Visual.GetFrameCount(state) : 0;
            if (frameCount <= 0)
                return;

            if (previousFrame < 0)
            {
                DispatchFrameEvents(subject, profile, moveDefinition, state, nextFrame, allowProfileTrack, allowMoveTrack, allowPreviewEvents, roleLabel);
                return;
            }

            if (nextFrame > previousFrame)
            {
                for (int frame = previousFrame + 1; frame <= nextFrame; frame++)
                    DispatchFrameEvents(subject, profile, moveDefinition, state, frame, allowProfileTrack, allowMoveTrack, allowPreviewEvents, roleLabel);
                return;
            }

            for (int frame = previousFrame + 1; frame < frameCount; frame++)
                DispatchFrameEvents(subject, profile, moveDefinition, state, frame, allowProfileTrack, allowMoveTrack, allowPreviewEvents, roleLabel);

            for (int frame = 0; frame <= nextFrame; frame++)
                DispatchFrameEvents(subject, profile, moveDefinition, state, frame, allowProfileTrack, allowMoveTrack, allowPreviewEvents, roleLabel);
        }

        private void DispatchFrameEvents(
            CombatAnimationPreviewSubject subject,
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            bool allowProfileTrack,
            bool allowMoveTrack,
            bool allowPreviewEvents,
            string roleLabel)
        {
            _eventSimulator.GetFrameEvents(profile, moveDefinition, state, frame, _includeAuthoredTrack && (allowProfileTrack || allowMoveTrack), _frameEvents);
            for (int i = 0; i < _frameEvents.Count; i++)
            {
                CombatPreviewEventBinding binding = _frameEvents[i];
                switch (binding.SourceKind)
                {
                    case CombatPreviewEventSourceKind.Profile:
                        if (!allowProfileTrack)
                            continue;
                        break;
                    case CombatPreviewEventSourceKind.Move:
                        if (!allowMoveTrack)
                            continue;
                        break;
                    case CombatPreviewEventSourceKind.Preview:
                        if (!allowPreviewEvents)
                            continue;
                        break;
                }

                DispatchEvent(subject, binding, roleLabel);
            }
        }
        private void DispatchEvent(CombatAnimationPreviewSubject subject, CombatPreviewEventBinding binding, string roleLabel)
        {
            CombatPresentationEvent evt = binding.Event;
            if (evt == null)
                return;

            switch (evt.family)
            {
                case PresentationEventFamily.VFX_Sprite:
                    DispatchSpriteVfx(subject, binding, roleLabel);
                    break;
                case PresentationEventFamily.VFX_Graph:
                    DispatchGraphVfx(subject, binding, roleLabel);
                    break;
                case PresentationEventFamily.Audio:
                    DispatchAudio(subject, binding, roleLabel);
                    break;
                case PresentationEventFamily.GameplayMarker:
                    AddLog($"{roleLabel} {binding.SourceLabel} marker '{GetEventLabel(evt)}' fired on frame {evt.frameIndex}.");
                    break;
                case PresentationEventFamily.DebugNote:
                    AddLog($"{roleLabel} {binding.SourceLabel} debug note on frame {evt.frameIndex}: {evt.note}");
                    break;
            }
        }

        private void DispatchSpriteVfx(CombatAnimationPreviewSubject subject, CombatPreviewEventBinding binding, string roleLabel)
        {
            CombatPresentationEvent evt = binding.Event;
            var definition = evt.payload as SpriteFlipbookVfxDefinitionSO;
            if (definition == null)
            {
                WarnInvalidPayload(binding, roleLabel, "SpriteFlipbookVfxDefinitionSO");
                return;
            }

            ResolvePlaybackPose(subject, evt, out Vector3 position, out Transform follow);
            Transform parent = evt.playbackSpace == PresentationPlaybackSpace.AtSelf || evt.playbackSpace == PresentationPlaybackSpace.AtAnchor
                ? follow
                : null;
            var player = subject != null ? subject.SpawnSpriteVfx(definition, position, parent) : null;
            if (player != null)
                _activeSpriteVfx.Add(player);

            AddOverlayMarker("VFX", position, new Color(0.35f, 0.78f, 0.92f, 1f));
            AddLog($"{roleLabel} {binding.SourceLabel} sprite VFX '{GetEventLabel(evt)}' at {DescribeAnchor(evt)} frame {evt.frameIndex}.");
        }

        private void DispatchGraphVfx(CombatAnimationPreviewSubject subject, CombatPreviewEventBinding binding, string roleLabel)
        {
            CombatPresentationEvent evt = binding.Event;
            var definition = evt.payload as GraphVfxDefinitionSO;
            if (definition == null)
            {
                WarnInvalidPayload(binding, roleLabel, "GraphVfxDefinitionSO");
                return;
            }

            ResolvePlaybackPose(subject, evt, out Vector3 position, out Transform follow);
            Transform parent = evt.playbackSpace == PresentationPlaybackSpace.AtSelf || evt.playbackSpace == PresentationPlaybackSpace.AtAnchor
                ? follow
                : null;
            var instance = subject != null ? subject.SpawnGraphVfx(definition, position, parent) : null;
            if (instance != null)
                _activeGraphVfx.Add(instance);

            AddOverlayMarker("Graph", position, new Color(0.82f, 0.60f, 0.96f, 1f));
            AddLog($"{roleLabel} {binding.SourceLabel} graph VFX stub '{GetEventLabel(evt)}' at {DescribeAnchor(evt)} frame {evt.frameIndex}.");
        }

        private void DispatchAudio(CombatAnimationPreviewSubject subject, CombatPreviewEventBinding binding, string roleLabel)
        {
            CombatPresentationEvent evt = binding.Event;
            var soundEvent = evt.payload as SoundEventSO;
            if (soundEvent == null)
            {
                WarnInvalidPayload(binding, roleLabel, "SoundEventSO");
                return;
            }

            ResolvePlaybackPose(subject, evt, out Vector3 position, out _);
            string originLabel = evt.playbackSpace == PresentationPlaybackSpace.Global2D ? "Global2D" : DescribeAnchor(evt);
            string loopKey = BuildLoopKey(evt, roleLabel);

            switch (evt.audioAction)
            {
                case AudioActionType.PlayOneShot:
                {
                    CombatPreviewAudioResult result = _audioPlayer.PlayOneShot(soundEvent, _deterministicAudio);
                    AddAudioLog(binding, roleLabel, evt, result, originLabel);
                    break;
                }
                case AudioActionType.StartLoop:
                {
                    CombatPreviewAudioResult result = _audioPlayer.StartLoop(soundEvent, _deterministicAudio, loopKey);
                    AddAudioLog(binding, roleLabel, evt, result, originLabel);
                    if (!result.LoopSupported)
                        AddLog("Loop playback fell back to one-shot preview because the editor preview API does not expose reliable looping.");
                    break;
                }
                case AudioActionType.StopLoop:
                    _audioPlayer.StopLoop(loopKey);
                    AddLog($"{roleLabel} {binding.SourceLabel} audio stop '{GetEventLabel(evt)}' at {originLabel} frame {evt.frameIndex}.");
                    break;
            }

            if (evt.playbackSpace != PresentationPlaybackSpace.Global2D)
                AddOverlayMarker("SFX", position, new Color(0.96f, 0.74f, 0.24f, 1f));
        }

        private void AddAudioLog(CombatPreviewEventBinding binding, string roleLabel, CombatPresentationEvent evt, CombatPreviewAudioResult result, string originLabel)
        {
            if (!result.Success || result.Clip == null)
            {
                AddLog($"{roleLabel} {binding.SourceLabel} audio '{GetEventLabel(evt)}' could not be previewed.");
                return;
            }

            string loopLabel = evt.audioAction == AudioActionType.PlayOneShot ? "one-shot" : "loop";
            AddLog($"{roleLabel} {binding.SourceLabel} {loopLabel} audio '{result.Clip.name}' at {originLabel} frame {evt.frameIndex} (vol {result.Volume:0.00}, pitch {result.Pitch:0.00}).");
        }

        private void ResolvePlaybackPose(CombatAnimationPreviewSubject subject, CombatPresentationEvent evt, out Vector3 position, out Transform follow)
        {
            if (evt == null)
            {
                position = Vector3.zero;
                follow = null;
                return;
            }

            if (subject != null && subject.View != null)
            {
                CombatPresentationPoseUtility.ResolvePlaybackPose(subject.View, evt, out position, out follow);
                return;
            }

            position = evt.playbackSpace == PresentationPlaybackSpace.Global2D ? Vector3.zero : evt.offset;
            follow = null;
        }

        private bool StepActiveEffects(float deltaTime)
        {
            bool changed = false;

            for (int i = _activeSpriteVfx.Count - 1; i >= 0; i--)
            {
                var player = _activeSpriteVfx[i];
                if (player == null)
                {
                    _activeSpriteVfx.RemoveAt(i);
                    changed = true;
                    continue;
                }

                player.StepPlayback(deltaTime);
                if (player == null)
                    _activeSpriteVfx.RemoveAt(i);
                changed = true;
            }

            for (int i = _activeGraphVfx.Count - 1; i >= 0; i--)
            {
                if (_activeGraphVfx[i] == null)
                {
                    _activeGraphVfx.RemoveAt(i);
                    changed = true;
                }
            }

            return changed;
        }

        private bool PruneOverlayMarkers(double now)
        {
            bool changed = false;
            for (int i = _overlayMarkers.Count - 1; i >= 0; i--)
            {
                if (_overlayMarkers[i].ExpiresAt > now)
                    continue;

                _overlayMarkers.RemoveAt(i);
                changed = true;
            }

            return changed;
        }

        private void StopPairInternal(bool logMessage, bool resetPlayback)
        {
            if (_pairOrchestrator != null)
            {
                _pairOrchestrator.FireFrameTriggered -= HandlePairFireFrame;
                _pairOrchestrator.ImpactTriggered -= HandlePairImpact;
                _pairOrchestrator.Dispose();
                _pairOrchestrator = null;
            }

            if (resetPlayback)
            {
                ClearSpawnedPreview();
                ResetPlaybackToStart();
            }

            if (logMessage)
                AddLog("Pair preview stopped.");
        }

        private CombatPreviewPairOrchestrator CreatePairOrchestrator()
        {
            Camera previewCamera = _previewUtility != null ? _previewUtility.camera : null;
            var orchestrator = new CombatPreviewPairOrchestrator(
                _subject,
                _defenderSubject,
                _moveDefinition,
                _vfxLibrary,
                _moveCategory,
                _phaseOverrides,
                previewCamera);
            orchestrator.FireFrameTriggered += HandlePairFireFrame;
            orchestrator.ImpactTriggered += HandlePairImpact;
            return orchestrator;
        }
        private void AddOverlayMarker(string label, Vector3 position, Color color, float lifetimeSeconds = 0.85f)
        {
            _overlayMarkers.Add(new CombatPreviewOverlayMarker(label, position, color, EditorApplication.timeSinceStartup + lifetimeSeconds));
        }

        private bool IsPreviewOneShotState()
        {
            return _selectedState != CombatChainlingVisual.CombatAnimState.Idle;
        }

        private static CombatChainlingVisual.CombatAnimState ResolveAttackState(MoveCategory moveCategory)
        {
            return moveCategory == MoveCategory.DamagingRanged
                ? CombatChainlingVisual.CombatAnimState.RangedAttack
                : CombatChainlingVisual.CombatAnimState.MeleeAttack;
        }

        private static Vector3 GetCameraFocus(CombatAnimationPreviewSubject subject)
        {
            if (subject == null)
                return Vector3.zero;

            Transform focus = subject.GetAnchor(PresentationAnchorTarget.CameraFocus);
            return focus != null ? focus.position : subject.Root.transform.position;
        }

        private string DescribeAnchor(CombatPresentationEvent evt)
        {
            if (evt.playbackSpace == PresentationPlaybackSpace.AtSelf)
                return "SelfRoot";

            if (evt.playbackSpace == PresentationPlaybackSpace.Global2D)
                return "Global2D";

            return evt.anchorTarget.ToString();
        }

        private string BuildLoopKey(CombatPresentationEvent evt, string roleLabel)
        {
            string eventKey = string.IsNullOrWhiteSpace(evt.eventId)
                ? $"{evt.family}:{evt.animationState}:{evt.frameIndex}"
                : evt.eventId;
            return $"{roleLabel}:{eventKey}";
        }

        private void WarnInvalidPayload(CombatPreviewEventBinding binding, string roleLabel, string expectedType)
        {
            AddLog($"{roleLabel} {binding.SourceLabel} event '{GetEventLabel(binding.Event)}' expected {expectedType} but payload was '{binding.Event.payload}'.");
        }

        private void AddLog(string message)
        {
            string stamped = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logEntries.Add(stamped);
            if (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveAt(0);
        }

        private static string GetEventLabel(CombatPresentationEvent evt)
        {
            if (evt == null)
                return "(null)";

            return string.IsNullOrWhiteSpace(evt.eventId)
                ? $"{evt.family}_{evt.frameIndex}"
                : evt.eventId;
        }
    }
}
