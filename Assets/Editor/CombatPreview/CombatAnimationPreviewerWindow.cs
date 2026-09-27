using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    public sealed class CombatAnimationPreviewerWindow : EditorWindow
    {
        private static readonly string[] StateLabels = { "Idle", "Melee", "Ranged", "Hit" };
        private static readonly Color CurrentFrameColor = new Color(0.26f, 0.48f, 0.82f, 1f);
        private static readonly Color ProfileEventColor = new Color(0.95f, 0.58f, 0.22f, 1f);
        private static readonly Color MoveEventColor = new Color(0.81f, 0.46f, 0.94f, 1f);
        private static readonly Color PreviewEventColor = new Color(0.27f, 0.76f, 0.56f, 1f);
        private static readonly Color MeleeMarkerColor = new Color(0.86f, 0.31f, 0.31f, 1f);
        private static readonly Color RangedMarkerColor = new Color(0.28f, 0.65f, 0.95f, 1f);
        private const float ZoomHotkeyStep = 0.12f;
        private const float ZoomScrollStep = 0.08f;

        private readonly List<CombatPreviewEventBinding> _frameEventBuffer = new List<CombatPreviewEventBinding>();
        private CombatAnimationPreviewSession _session;
        private CombatVisualProfileSO _profile;
        private CombatVisualProfileSO _defenderProfile;
        private MoveDefinitionSO _moveDefinition;
        private Combat2ChainlingView _previewTemplate;
        private CombatVFXLibrary _vfxLibrary;
        private GameObject _castPhaseOverridePrefab;
        private GameObject _strikePhaseOverridePrefab;
        private GameObject _projectilePhaseOverridePrefab;
        private GameObject _impactPhaseOverridePrefab;
        private CombatChainlingVisual.CombatAnimState _selectedState = CombatChainlingVisual.CombatAnimState.Idle;
        private MoveCategory _moveCategory = MoveCategory.DamagingMelee;
        private CombatPresentationEvent _selectedEvent;
        private CombatPreviewEventSourceKind _selectedEventSourceKind = CombatPreviewEventSourceKind.Preview;
        private bool _loopPreview = true;
        private bool _flipX;
        private bool _includeAuthoredTrack = true;
        private bool _showAnchors = true;
        private bool _deterministicAudio = true;
        private PresentationAnchorTarget _selectedAnchor = PresentationAnchorTarget.Impact;
        private Vector2 _leftPanelScroll;
        private Vector2 _rightPanelScroll;
        private Vector2 _timelineScroll;
        private Vector2 _logScroll;
        private Rect _previewRect;

        [MenuItem("Tools/Chainbound/Combat/Animation Previewer")]
        public static void Open()
        {
            GetWindow<CombatAnimationPreviewerWindow>("Combat Anim Preview");
        }

        public static CombatAnimationPreviewerWindow OpenWithContext(
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame)
        {
            var window = GetWindow<CombatAnimationPreviewerWindow>("Combat Anim Preview");
            window._profile = profile;
            window._moveDefinition = moveDefinition;
            if (moveDefinition != null)
                window._moveCategory = moveDefinition.category;
            window._selectedState = state;
            window._selectedEvent = null;
            window._selectedEventSourceKind = CombatPreviewEventSourceKind.Preview;
            window.EnsureSession();
            window.PushWindowStateToSession();
            if (window._session.FrameCount > 0)
                window._session.ScrubToFrame(frame, false);
            window.Focus();
            window.Repaint();
            return window;
        }

        private void OnEnable()
        {
            _session = new CombatAnimationPreviewSession();
            EditorApplication.update += OnEditorUpdate;
            PushWindowStateToSession();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _session?.Dispose();
            _session = null;
        }

        private void OnEditorUpdate()
        {
            if (_session == null)
                return;

            bool changed = _session.Update();
            if (changed || _session.RequiresContinuousRepaint)
                Repaint();
        }

        private void OnGUI()
        {
            EnsureSession();
            HandleShortcuts(Event.current);
            DrawHeader();

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLeftPanel();
                DrawCenterPanel();
                DrawRightPanel();
            }

            DrawTimeline();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Combat Animation Previewer", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Preview CombatChainlingVisual playback, markers, anchor-driven VFX/SFX timing, paired attacker/defender move presentation, and layered profile + move events without entering Play Mode.",
                MessageType.None);
        }

        private float GetPanelHeight()
        {
            return Mathf.Max(200f, position.height - 220f);
        }

        private void DrawLeftPanel()
        {
            _leftPanelScroll = EditorGUILayout.BeginScrollView(_leftPanelScroll, GUILayout.Width(300f), GUILayout.Height(GetPanelHeight()));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Subjects", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _profile = (CombatVisualProfileSO)EditorGUILayout.ObjectField("Attacker Profile", _profile, typeof(CombatVisualProfileSO), false);
                _defenderProfile = (CombatVisualProfileSO)EditorGUILayout.ObjectField("Defender Profile", _defenderProfile, typeof(CombatVisualProfileSO), false);
                _moveDefinition = (MoveDefinitionSO)EditorGUILayout.ObjectField("Move", _moveDefinition, typeof(MoveDefinitionSO), false);
                _previewTemplate = (Combat2ChainlingView)EditorGUILayout.ObjectField("Preview Template", _previewTemplate, typeof(Combat2ChainlingView), false);
                _vfxLibrary = (CombatVFXLibrary)EditorGUILayout.ObjectField("VFX Library", _vfxLibrary, typeof(CombatVFXLibrary), false);
                _moveCategory = (MoveCategory)EditorGUILayout.EnumPopup("Move Category", _moveCategory);
                if (EditorGUI.EndChangeCheck())
                {
                    if (_moveDefinition != null)
                        _moveCategory = _moveDefinition.category;
                    PushWindowStateToSession();
                    ClearSelectionIfInvalid();
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                int nextStateIndex = GUILayout.Toolbar((int)_selectedState, StateLabels);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedState = (CombatChainlingVisual.CombatAnimState)nextStateIndex;
                    _session.SetState(_selectedState);
                    ClearSelectionIfInvalid();
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _loopPreview = EditorGUILayout.ToggleLeft("Loop Preview", _loopPreview);
                _flipX = EditorGUILayout.ToggleLeft("Flip X", _flipX);
                _includeAuthoredTrack = EditorGUILayout.ToggleLeft("Replay Authored Tracks", _includeAuthoredTrack);
                _showAnchors = EditorGUILayout.ToggleLeft("Show Anchor Overlay", _showAnchors);
                _deterministicAudio = EditorGUILayout.ToggleLeft("Deterministic Audio", _deterministicAudio);
                if (EditorGUI.EndChangeCheck())
                    PushWindowStateToSession();

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Viewport", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                float zoom = EditorGUILayout.Slider("Zoom", _session.Zoom, _session.MinZoom, _session.MaxZoom);
                if (EditorGUI.EndChangeCheck())
                    _session.SetZoom(zoom);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(string.Format("{0:0}%", _session.Zoom * 100f), GUILayout.Width(56f));
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset", GUILayout.Width(68f)))
                        _session.ResetZoom();
                }

                EditorGUILayout.LabelField("Wheel over preview or +/- to zoom", EditorStyles.miniLabel);

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Frames", Mathf.Max(0, _session.FrameCount).ToString());
                EditorGUILayout.LabelField("FPS", _session.FramesPerSecond.ToString("0.##"));
                EditorGUILayout.LabelField("Duration", _session.DurationSeconds.ToString("0.###") + "s");
                EditorGUILayout.LabelField("Sprite Size", _session.SpriteSize.ToString("F2"));
                EditorGUILayout.ObjectField("Profile Track", _session.ProfileTrack, typeof(CombatPresentationTrackSO), false);
                EditorGUILayout.ObjectField("Move Track", _session.MoveTrack, typeof(CombatPresentationTrackSO), false);
                if (_profile != null && _profile.anchorOffsets != null && _profile.anchorOffsets.Count > 0)
                    EditorGUILayout.LabelField("Attacker Anchor Offsets", _profile.anchorOffsets.Count.ToString());
                if (_defenderProfile != null && _defenderProfile.anchorOffsets != null && _defenderProfile.anchorOffsets.Count > 0)
                    EditorGUILayout.LabelField("Defender Anchor Offsets", _defenderProfile.anchorOffsets.Count.ToString());

                DrawPairSection();

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("Marker Jumps", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!_session.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.MeleeAttack, out int meleeFrame)))
                    {
                        if (GUILayout.Button("Melee Marker"))
                            _session.ScrubToFrame(meleeFrame, false);
                    }

                    using (new EditorGUI.DisabledScope(!_session.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.RangedAttack, out int rangedFrame)))
                    {
                        if (GUILayout.Button("Ranged Marker"))
                            _session.ScrubToFrame(rangedFrame, false);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private void DrawPairSection()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Attack Pair", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Phase", _session.CurrentPairPhase.ToString());

            if (_session.TryGetPairTiming(out CombatPreviewPairTiming timing))
            {
                EditorGUILayout.LabelField("Attack State", timing.AttackState.ToString());
                EditorGUILayout.LabelField("Fire Time", timing.FireTime.ToString("0.###") + "s");
                EditorGUILayout.LabelField("Impact Delay", timing.ImpactDelay.ToString("0.###") + "s");
                EditorGUILayout.LabelField("Hit Duration", timing.DefenderHitDuration.ToString("0.###") + "s");
                EditorGUILayout.LabelField("Total", timing.TotalDuration.ToString("0.###") + "s");
            }

            Rect progressRect = GUILayoutUtility.GetRect(10f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, _session.PairProgress01, _session.IsPairPlaying ? "Pair Running" : "Idle");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_session.CanPlayAttackPair))
                {
                    if (GUILayout.Button("Play Attack Pair"))
                    {
                        SetPreviewState(ResolveAttackState(_moveCategory));
                        _session.PlayAttackPair();
                    }
                }

                using (new EditorGUI.DisabledScope(!_session.IsPairPlaying))
                {
                    if (GUILayout.Button("Stop Pair"))
                        _session.StopPair();
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Move VFX Phases", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _castPhaseOverridePrefab = (GameObject)EditorGUILayout.ObjectField("Cast Override", _castPhaseOverridePrefab, typeof(GameObject), false);
            _strikePhaseOverridePrefab = (GameObject)EditorGUILayout.ObjectField("Strike Override", _strikePhaseOverridePrefab, typeof(GameObject), false);
            _projectilePhaseOverridePrefab = (GameObject)EditorGUILayout.ObjectField("Projectile Override", _projectilePhaseOverridePrefab, typeof(GameObject), false);
            _impactPhaseOverridePrefab = (GameObject)EditorGUILayout.ObjectField("Impact Override", _impactPhaseOverridePrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
                PushPhaseOverridesToSession();

            using (new EditorGUI.DisabledScope(!_session.CanPreviewMoveVfxPhase || _session.IsPairPlaying))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fire Cast"))
                        _session.PreviewMoveVfxPhase(CombatVFXLibrary.VFXPhase.Cast);
                    if (GUILayout.Button("Fire Strike"))
                        _session.PreviewMoveVfxPhase(CombatVFXLibrary.VFXPhase.Strike);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Fire Projectile"))
                        _session.PreviewMoveVfxPhase(CombatVFXLibrary.VFXPhase.Projectile);
                    if (GUILayout.Button("Fire Impact"))
                        _session.PreviewMoveVfxPhase(CombatVFXLibrary.VFXPhase.Impact);
                }
            }

            if (GUILayout.Button("Clear Spawned"))
                _session.ClearSpawnedPreview();

            if (!_session.CanPlayAttackPair)
            {
                EditorGUILayout.HelpBox(
                    "Set attacker profile, defender profile, move, and VFX library to enable pair preview.",
                    MessageType.Info);
            }
        }

        private void DrawCenterPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
                Rect previewRect = GUILayoutUtility.GetRect(320f, 320f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                _previewRect = previewRect;
                _session.DrawPreview(previewRect, _showAnchors, _selectedAnchor);
                HandlePreviewViewportInput(previewRect, Event.current);

                using (new EditorGUI.DisabledScope(_session.IsPairPlaying))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(_session.IsPlaying ? "Pause" : "Play", GUILayout.Width(72f)))
                        {
                            if (_session.IsPlaying)
                                _session.Pause();
                            else
                                _session.Play();
                        }

                        if (GUILayout.Button("Stop", GUILayout.Width(60f)))
                            _session.Stop();

                        if (GUILayout.Button("Prev", GUILayout.Width(60f)))
                            _session.StepFrame(-1);

                        if (GUILayout.Button("Next", GUILayout.Width(60f)))
                            _session.StepFrame(1);

                        if (GUILayout.Button("Replay Frame", GUILayout.Width(96f)))
                            _session.ReplayCurrentFrameEvents();

                        if (GUILayout.Button("Clear Spawned", GUILayout.Width(96f)))
                            _session.ClearSpawnedPreview();
                    }

                    using (new EditorGUI.DisabledScope(_session.FrameCount <= 0))
                    {
                        int currentFrame = Mathf.Clamp(_session.CurrentFrame, 0, Mathf.Max(0, _session.FrameCount - 1));
                        int nextFrame = EditorGUILayout.IntSlider("Frame", currentFrame, 0, Mathf.Max(0, _session.FrameCount - 1));
                        if (nextFrame != currentFrame)
                            _session.ScrubToFrame(nextFrame, false);
                    }
                }

                if (_session.IsPairPlaying)
                    EditorGUILayout.HelpBox("Pair preview is running. Use Stop Pair to return to manual scrubbing and solo transport.", MessageType.None);

                EditorGUILayout.LabelField($"Current Frame: {_session.CurrentFrame + 1}/{Mathf.Max(1, _session.FrameCount)}");
            }
        }

        private void DrawRightPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(360f)))
            using (var scroll = new EditorGUILayout.ScrollViewScope(_rightPanelScroll, GUILayout.Height(GetPanelHeight())))
            {
                _rightPanelScroll = scroll.scrollPosition;
                DrawFrameEventsSection();
                EditorGUILayout.Space(8f);
                DrawSelectedEventInspector();
                EditorGUILayout.Space(8f);
                DrawToolingSection();
                EditorGUILayout.Space(8f);
                DrawAnchorSection();
                EditorGUILayout.Space(8f);
                DrawLogSection();
            }
        }

        private void DrawFrameEventsSection()
        {
            EditorGUILayout.LabelField("Frame Events", EditorStyles.boldLabel);
            _session.GetFrameEvents(_session.CurrentFrame, _frameEventBuffer);

            for (int i = 0; i < _frameEventBuffer.Count; i++)
            {
                CombatPreviewEventBinding binding = _frameEventBuffer[i];
                CombatPresentationEvent evt = binding.Event;
                string label = $"[{binding.SourceLabel}] {GetEventLabel(evt)}";
                bool selected = ReferenceEquals(_selectedEvent, evt) && _selectedEventSourceKind == binding.SourceKind;
                if (GUILayout.Toggle(selected, label, "Button"))
                {
                    _selectedEvent = evt;
                    _selectedEventSourceKind = binding.SourceKind;
                }
            }

            if (_frameEventBuffer.Count == 0)
                EditorGUILayout.HelpBox("No profile, move, or preview events on the current frame.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Preview Event"))
                {
                    _selectedEvent = _session.EventSimulator.AddTemporaryEvent(_selectedState, _session.CurrentFrame);
                    _selectedEventSourceKind = CombatPreviewEventSourceKind.Preview;
                }

                using (new EditorGUI.DisabledScope(_selectedEvent == null || _selectedEventSourceKind != CombatPreviewEventSourceKind.Preview))
                {
                    if (GUILayout.Button("Remove Selected"))
                    {
                        _session.EventSimulator.RemoveTemporaryEvent(_selectedEvent);
                        _selectedEvent = null;
                    }
                }
            }
        }

        private void DrawSelectedEventInspector()
        {
            EditorGUILayout.LabelField("Selected Event", EditorStyles.boldLabel);
            if (_selectedEvent == null)
            {
                EditorGUILayout.HelpBox("Select a frame event to inspect it, or add a preview-only event for timing experiments.", MessageType.Info);
                return;
            }

            bool isPreviewEvent = _selectedEventSourceKind == CombatPreviewEventSourceKind.Preview;
            EditorGUILayout.LabelField("Source", _selectedEventSourceKind.ToString());

            using (new EditorGUI.DisabledScope(!isPreviewEvent))
            {
                _selectedEvent.eventId = EditorGUILayout.TextField("Event Id", _selectedEvent.eventId);
                _selectedEvent.animationState = (CombatChainlingVisual.CombatAnimState)EditorGUILayout.EnumPopup("State", _selectedEvent.animationState);
                _selectedEvent.frameIndex = EditorGUILayout.IntField("Frame", _selectedEvent.frameIndex);
                _selectedEvent.family = (PresentationEventFamily)EditorGUILayout.EnumPopup("Family", _selectedEvent.family);
                _selectedEvent.anchorTarget = (PresentationAnchorTarget)EditorGUILayout.EnumPopup("Anchor", _selectedEvent.anchorTarget);
                _selectedEvent.playbackSpace = (PresentationPlaybackSpace)EditorGUILayout.EnumPopup("Playback Space", _selectedEvent.playbackSpace);
                _selectedEvent.payload = EditorGUILayout.ObjectField("Payload", _selectedEvent.payload, typeof(UnityEngine.Object), false);
                if (_selectedEvent.family == PresentationEventFamily.Audio)
                    _selectedEvent.audioAction = (AudioActionType)EditorGUILayout.EnumPopup("Audio Action", _selectedEvent.audioAction);
                _selectedEvent.offset = EditorGUILayout.Vector3Field("Offset", _selectedEvent.offset);
            }

            if (!isPreviewEvent)
                EditorGUILayout.HelpBox("Authored events are read-only here. Edit saved profile or move tracks in the Combat Presentation Authoring Tool.", MessageType.None);

            EditorGUILayout.LabelField("Note");
            using (new EditorGUI.DisabledScope(!isPreviewEvent))
            {
                _selectedEvent.note = EditorGUILayout.TextArea(_selectedEvent.note, GUILayout.MinHeight(52f));
            }

            if (isPreviewEvent)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_profile == null))
                    {
                        if (GUILayout.Button("Promote to Profile Track"))
                            PromoteSelectedPreviewEvent(false);
                    }

                    using (new EditorGUI.DisabledScope(_moveDefinition == null))
                    {
                        if (GUILayout.Button("Promote to Move Track"))
                            PromoteSelectedPreviewEvent(true);
                    }
                }
            }
        }
        private void DrawToolingSection()
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Edit in Authoring Tool"))
                OpenAuthoringTool();
        }

        private void DrawAnchorSection()
        {
            EditorGUILayout.LabelField("Anchors", EditorStyles.boldLabel);
            DrawAnchorButtons("ATK", _session.Anchors);
            if (_session.HasDefender)
                DrawAnchorButtons("DEF", _session.DefenderAnchors);

            Transform attackerAnchor = FindAnchorTransform(_session.Anchors, _selectedAnchor);
            Transform defenderAnchor = FindAnchorTransform(_session.DefenderAnchors, _selectedAnchor);

            using (new EditorGUI.DisabledScope(true))
            {
                if (attackerAnchor != null)
                    EditorGUILayout.Vector3Field("Attacker Position", attackerAnchor.position);
                if (defenderAnchor != null)
                    EditorGUILayout.Vector3Field("Defender Position", defenderAnchor.position);
            }

            DrawProfileAnchorOffset("Attacker Offset", _profile);
            DrawProfileAnchorOffset("Defender Offset", _defenderProfile);
        }

        private void DrawAnchorButtons(string prefix, IReadOnlyList<CombatPreviewAnchorInfo> anchors)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                CombatPreviewAnchorInfo anchor = anchors[i];
                if (anchor.Transform == null)
                    continue;

                bool selected = _selectedAnchor == anchor.Target;
                if (GUILayout.Toggle(selected, prefix + " " + anchor.Label, "Button"))
                    _selectedAnchor = anchor.Target;
            }
        }

        private void DrawProfileAnchorOffset(string label, CombatVisualProfileSO profile)
        {
            if (profile == null || profile.anchorOffsets == null)
                return;

            for (int i = 0; i < profile.anchorOffsets.Count; i++)
            {
                CombatPresentationAnchorOffset anchorOffset = profile.anchorOffsets[i];
                if (anchorOffset.anchorTarget != _selectedAnchor)
                    continue;

                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Vector3Field(label, anchorOffset.localOffset);
                return;
            }
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("Debug Log", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.Height(180f)))
            {
                _logScroll = scroll.scrollPosition;
                for (int i = 0; i < _session.LogEntries.Count; i++)
                    EditorGUILayout.LabelField(_session.LogEntries[i], EditorStyles.miniLabel);
            }
        }

        private void DrawTimeline()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);
                DrawTimelineLegend();
                if (_session.FrameCount <= 0)
                {
                    EditorGUILayout.HelpBox("No frames available for the selected state/profile.", MessageType.Warning);
                    return;
                }

                bool hasMeleeMarker = _session.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.MeleeAttack, out int meleeMarkerFrame);
                bool hasRangedMarker = _session.TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.RangedAttack, out int rangedMarkerFrame);

                _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll, GUILayout.Height(86f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int frame = 0; frame < _session.FrameCount; frame++)
                        DrawTimelineCell(frame, hasMeleeMarker && frame == meleeMarkerFrame, hasRangedMarker && frame == rangedMarkerFrame);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTimelineLegend()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLegendSwatch(ProfileEventColor, "Profile");
                DrawLegendSwatch(MoveEventColor, "Move");
                DrawLegendSwatch(PreviewEventColor, "Preview");
                DrawLegendSwatch(MeleeMarkerColor, "Melee");
                DrawLegendSwatch(RangedMarkerColor, "Ranged");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawTimelineCell(int frame, bool isMeleeMarker, bool isRangedMarker)
        {
            _session.CountFrameEvents(frame, out int profileCount, out int moveCount, out int previewCount);
            Rect cellRect = GUILayoutUtility.GetRect(40f, 56f, GUILayout.Width(40f), GUILayout.Height(56f));
            Color background = frame == _session.CurrentFrame ? CurrentFrameColor : new Color(0.18f, 0.19f, 0.21f, 1f);
            EditorGUI.DrawRect(cellRect, background);

            Rect buttonRect = new Rect(cellRect.x + 2f, cellRect.y + 18f, cellRect.width - 4f, cellRect.height - 20f);
            if (GUI.Button(buttonRect, frame.ToString(), EditorStyles.miniButton))
                _session.ScrubToFrame(frame, false);

            if (isMeleeMarker)
                EditorGUI.DrawRect(new Rect(cellRect.x + 2f, cellRect.y + 2f, 16f, 5f), MeleeMarkerColor);
            if (isRangedMarker)
                EditorGUI.DrawRect(new Rect(cellRect.x + 22f, cellRect.y + 2f, 16f, 5f), RangedMarkerColor);

            float barWidth = (cellRect.width - 6f) / 3f;
            if (profileCount > 0)
                EditorGUI.DrawRect(new Rect(cellRect.x + 2f, cellRect.yMax - 6f, barWidth, 4f), ProfileEventColor);
            if (moveCount > 0)
                EditorGUI.DrawRect(new Rect(cellRect.x + 3f + barWidth, cellRect.yMax - 6f, barWidth, 4f), MoveEventColor);
            if (previewCount > 0)
                EditorGUI.DrawRect(new Rect(cellRect.x + 4f + (barWidth * 2f), cellRect.yMax - 6f, barWidth, 4f), PreviewEventColor);
        }

        private void DrawLegendSwatch(Color color, string label)
        {
            Rect rect = GUILayoutUtility.GetRect(70f, 14f, GUILayout.Width(70f));
            Rect swatchRect = new Rect(rect.x, rect.y + 2f, 12f, 10f);
            EditorGUI.DrawRect(swatchRect, color);
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), label, EditorStyles.miniLabel);
        }

        private void HandlePreviewViewportInput(Rect previewRect, Event evt)
        {
            if (evt == null || evt.type != EventType.ScrollWheel || !previewRect.Contains(evt.mousePosition))
                return;

            float zoomDelta = -evt.delta.y * ZoomScrollStep;
            if (Mathf.Abs(zoomDelta) < 0.0001f)
                return;

            _session.AdjustZoom(zoomDelta);
            evt.Use();
            Repaint();
        }

        private void HandleShortcuts(Event evt)
        {
            if (evt.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
                return;

            switch (evt.keyCode)
            {
                case KeyCode.Equals:
                case KeyCode.KeypadPlus:
                    _session.AdjustZoom(ZoomHotkeyStep);
                    evt.Use();
                    return;
                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    _session.AdjustZoom(-ZoomHotkeyStep);
                    evt.Use();
                    return;
            }

            if (_session.IsPairPlaying)
            {
                if (evt.keyCode == KeyCode.Space)
                {
                    _session.StopPair();
                    evt.Use();
                }
                return;
            }

            switch (evt.keyCode)
            {
                case KeyCode.Space:
                    if (_session.IsPlaying)
                        _session.Pause();
                    else
                        _session.Play();
                    evt.Use();
                    break;
                case KeyCode.LeftArrow:
                    _session.StepFrame(-1);
                    evt.Use();
                    break;
                case KeyCode.RightArrow:
                    _session.StepFrame(1);
                    evt.Use();
                    break;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    SetPreviewState(CombatChainlingVisual.CombatAnimState.Idle);
                    evt.Use();
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    SetPreviewState(CombatChainlingVisual.CombatAnimState.MeleeAttack);
                    evt.Use();
                    break;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    SetPreviewState(CombatChainlingVisual.CombatAnimState.RangedAttack);
                    evt.Use();
                    break;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    SetPreviewState(CombatChainlingVisual.CombatAnimState.Hit);
                    evt.Use();
                    break;
            }
        }

        private void SetPreviewState(CombatChainlingVisual.CombatAnimState state)
        {
            _selectedState = state;
            _session.SetState(_selectedState);
            ClearSelectionIfInvalid();
        }
        private void PromoteSelectedPreviewEvent(bool promoteToMoveTrack)
        {
            if (_selectedEvent == null || _selectedEventSourceKind != CombatPreviewEventSourceKind.Preview)
                return;

            CombatPresentationTrackSO track = EnsurePromotionTrack(promoteToMoveTrack);
            if (track == null)
                return;

            Undo.RecordObject(track, "Promote Combat Preview Event");
            if (track.events == null)
                track.events = new List<CombatPresentationEvent>();

            CombatPresentationEvent clone = CloneEvent(_selectedEvent);
            if (string.IsNullOrWhiteSpace(clone.eventId) || clone.eventId.StartsWith("preview_evt_", StringComparison.Ordinal))
                clone.eventId = Guid.NewGuid().ToString("N");

            track.events.Add(clone);
            EditorUtility.SetDirty(track);
            AssetDatabase.SaveAssets();

            _session.EventSimulator.RemoveTemporaryEvent(_selectedEvent);
            _selectedEvent = clone;
            _selectedEventSourceKind = promoteToMoveTrack ? CombatPreviewEventSourceKind.Move : CombatPreviewEventSourceKind.Profile;
            EditorGUIUtility.PingObject(track);
            ClearSelectionIfInvalid();
            Repaint();
        }

        private CombatPresentationTrackSO EnsurePromotionTrack(bool promoteToMoveTrack)
        {
            if (promoteToMoveTrack)
            {
                if (_moveDefinition == null)
                    return null;
                if (_moveDefinition.presentationTrack != null)
                    return _moveDefinition.presentationTrack;
                return CreateTrackAsset(true);
            }

            if (_profile == null)
                return null;
            if (_profile.presentationTrack != null)
                return _profile.presentationTrack;
            return CreateTrackAsset(false);
        }

        private CombatPresentationTrackSO CreateTrackAsset(bool forMove)
        {
            string targetName = forMove && _moveDefinition != null ? _moveDefinition.name : (_profile != null ? _profile.name : "Presentation");
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Presentation Track",
                targetName + "_PresentationTrack",
                "asset",
                forMove ? "Choose where to save the move presentation track." : "Choose where to save the profile presentation track.");

            if (string.IsNullOrWhiteSpace(path))
                return null;

            var track = CreateInstance<CombatPresentationTrackSO>();
            AssetDatabase.CreateAsset(track, path);
            AssetDatabase.SaveAssets();

            if (forMove && _moveDefinition != null)
            {
                Undo.RecordObject(_moveDefinition, "Assign Move Presentation Track");
                _moveDefinition.presentationTrack = track;
                EditorUtility.SetDirty(_moveDefinition);
            }
            else if (_profile != null)
            {
                Undo.RecordObject(_profile, "Assign Combat Presentation Track");
                _profile.presentationTrack = track;
                EditorUtility.SetDirty(_profile);
            }

            PushWindowStateToSession();
            return track;
        }

        private void OpenAuthoringTool()
        {
            CombatChainlingVisual.CombatAnimState state = _selectedEvent != null ? _selectedEvent.animationState : _selectedState;
            int frame = _selectedEvent != null ? _selectedEvent.frameIndex : _session.CurrentFrame;
            bool preferMoveTrack = _selectedEventSourceKind == CombatPreviewEventSourceKind.Move
                || (_selectedEvent == null && _moveDefinition != null && _selectedEventSourceKind != CombatPreviewEventSourceKind.Profile);
            CombatPresentationAuthoringTool.OpenWithContext(_profile, _moveDefinition, state, frame, preferMoveTrack);
        }

        private void PushWindowStateToSession()
        {
            EnsureSession();
            _session.SetProfile(_profile, _moveDefinition, _previewTemplate);
            _session.SetDefenderProfile(_defenderProfile);
            _session.SetVfxLibrary(_vfxLibrary);
            _session.SetMoveCategory(_moveCategory);
            _session.SetState(_selectedState);
            _session.SetLoop(_loopPreview);
            _session.SetFlipX(_flipX);
            _session.SetIncludeAuthoredTrack(_includeAuthoredTrack);
            _session.SetDeterministicAudio(_deterministicAudio);
            PushPhaseOverridesToSession();
        }

        private void PushPhaseOverridesToSession()
        {
            EnsureSession();
            _session.SetPhaseOverride(CombatVFXLibrary.VFXPhase.Cast, _castPhaseOverridePrefab);
            _session.SetPhaseOverride(CombatVFXLibrary.VFXPhase.Strike, _strikePhaseOverridePrefab);
            _session.SetPhaseOverride(CombatVFXLibrary.VFXPhase.Projectile, _projectilePhaseOverridePrefab);
            _session.SetPhaseOverride(CombatVFXLibrary.VFXPhase.Impact, _impactPhaseOverridePrefab);
        }

        private void EnsureSession()
        {
            if (_session != null)
                return;

            _session = new CombatAnimationPreviewSession();
            PushWindowStateToSession();
        }

        private void ClearSelectionIfInvalid()
        {
            if (_selectedEvent == null)
                return;

            bool selectedEventStillExists;
            switch (_selectedEventSourceKind)
            {
                case CombatPreviewEventSourceKind.Profile:
                    selectedEventStillExists = _profile != null
                        && _profile.presentationTrack != null
                        && _profile.presentationTrack.events != null
                        && _profile.presentationTrack.events.Contains(_selectedEvent);
                    break;
                case CombatPreviewEventSourceKind.Move:
                    selectedEventStillExists = _moveDefinition != null
                        && _moveDefinition.presentationTrack != null
                        && _moveDefinition.presentationTrack.events != null
                        && _moveDefinition.presentationTrack.events.Contains(_selectedEvent);
                    break;
                default:
                    selectedEventStillExists = _session.EventSimulator.IsTemporaryEvent(_selectedEvent);
                    break;
            }

            if (!selectedEventStillExists)
                _selectedEvent = null;
        }

        private static Transform FindAnchorTransform(IReadOnlyList<CombatPreviewAnchorInfo> anchors, PresentationAnchorTarget target)
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                if (anchors[i].Target == target)
                    return anchors[i].Transform;
            }

            return null;
        }

        private static CombatPresentationEvent CloneEvent(CombatPresentationEvent source)
        {
            return new CombatPresentationEvent
            {
                eventId = source.eventId,
                animationState = source.animationState,
                frameIndex = source.frameIndex,
                family = source.family,
                anchorTarget = source.anchorTarget,
                playbackSpace = source.playbackSpace,
                payload = source.payload,
                audioAction = source.audioAction,
                offset = source.offset,
                note = source.note
            };
        }

        private static CombatChainlingVisual.CombatAnimState ResolveAttackState(MoveCategory moveCategory)
        {
            return moveCategory == MoveCategory.DamagingRanged
                ? CombatChainlingVisual.CombatAnimState.RangedAttack
                : CombatChainlingVisual.CombatAnimState.MeleeAttack;
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
