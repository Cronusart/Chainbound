using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    public class CombatPresentationAuthoringTool : EditorWindow
    {
        private static readonly Color AudioColor = new Color(0.88f, 0.34f, 0.34f, 1f);
        private static readonly Color SpriteVfxColor = new Color(0.29f, 0.56f, 0.95f, 1f);
        private static readonly Color MarkerColor = new Color(0.30f, 0.78f, 0.44f, 1f);
        private static readonly Color DebugColor = new Color(0.82f, 0.72f, 0.18f, 1f);
        private static readonly Color MeleeMarkerColor = new Color(0.86f, 0.31f, 0.31f, 1f);
        private static readonly Color RangedMarkerColor = new Color(0.28f, 0.65f, 0.95f, 1f);

        private enum TrackTarget
        {
            Profile,
            Move
        }

        private CombatVisualProfileSO _profile;
        private MoveDefinitionSO _moveDefinition;
        private CombatPresentationTrackSO _track;
        private TrackTarget _trackTarget = TrackTarget.Profile;
        private CombatChainlingVisual.CombatAnimState _selectedState = CombatChainlingVisual.CombatAnimState.Idle;
        private CombatPresentationEvent _selectedEvent;
        private Vector2 _timelineScroll;
        private Vector2 _detailsScroll;
        private int _selectedFrame;
        private bool _previewPlaying;
        private bool _previewLoop = true;
        private double _previewStartTime;
        private int _previewFrame;

        [MenuItem("Tools/Chainbound/Combat/Presentation Authoring Tool")]
        public static void Open()
        {
            GetWindow<CombatPresentationAuthoringTool>("Combat Presentation");
        }

        public static CombatPresentationAuthoringTool OpenWithContext(
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            bool preferMoveTrack = false)
        {
            var window = GetWindow<CombatPresentationAuthoringTool>("Combat Presentation");
            window._profile = profile;
            window._moveDefinition = moveDefinition;
            window._trackTarget = preferMoveTrack && moveDefinition != null ? TrackTarget.Move : TrackTarget.Profile;
            window._selectedState = state;
            window.SyncTrackSelection(resetSelection: false);
            window._selectedFrame = Mathf.Max(0, frame);
            window._selectedEvent = null;
            window._previewPlaying = false;
            window.ClampSelection();
            window.RestartPreview();
            window.Focus();
            window.Repaint();
            return window;
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_previewPlaying)
                return;

            int frameCount = GetFrameCount(_selectedState);
            if (frameCount <= 0)
            {
                _previewPlaying = false;
                return;
            }

            float fps = Mathf.Max(0.01f, GetFps(_selectedState));
            double elapsed = EditorApplication.timeSinceStartup - _previewStartTime;
            int nextFrame = Mathf.FloorToInt((float)(elapsed * fps));

            if (_previewLoop)
            {
                nextFrame %= frameCount;
            }
            else if (nextFrame >= frameCount)
            {
                nextFrame = frameCount - 1;
                _previewPlaying = false;
            }

            if (nextFrame != _previewFrame)
            {
                _previewFrame = nextFrame;
                _selectedFrame = nextFrame;
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawProfileSection();

            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Select a CombatVisualProfileSO to begin authoring presentation events.", MessageType.Info);
                return;
            }

            if (!HasValidTrackTarget())
            {
                EditorGUILayout.HelpBox("Select a MoveDefinitionSO to author move-specific presentation layered on top of the selected species profile.", MessageType.Info);
                return;
            }

            if (_track == null)
            {
                EditorGUILayout.HelpBox($"The selected {GetCurrentTrackTargetLabel().ToLowerInvariant()} does not have a presentation track assigned yet.", MessageType.Warning);
                DrawTrackActions();
                return;
            }

            ClampSelection();
            DrawValidationPanel();
            DrawPreviewSection();
            DrawTimeline();
            DrawDetailsPanel();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Combat Presentation Authoring Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Author frame-based VFX, SFX, and gameplay markers on top of species animation profiles or individual move tracks.", MessageType.None);
        }

        private void DrawProfileSection()
        {
            EditorGUI.BeginChangeCheck();
            var newProfile = (CombatVisualProfileSO)EditorGUILayout.ObjectField("Profile", _profile, typeof(CombatVisualProfileSO), false);
            var newMoveDefinition = (MoveDefinitionSO)EditorGUILayout.ObjectField("Move", _moveDefinition, typeof(MoveDefinitionSO), false);
            var newTrackTarget = (TrackTarget)EditorGUILayout.EnumPopup("Track Target", _trackTarget);
            if (EditorGUI.EndChangeCheck())
            {
                _profile = newProfile;
                _moveDefinition = newMoveDefinition;
                _trackTarget = newTrackTarget;
                SyncTrackSelection(resetSelection: true);
            }

            using (new EditorGUI.DisabledScope(_profile == null))
            {
                EditorGUI.BeginChangeCheck();
                var newState = (CombatChainlingVisual.CombatAnimState)EditorGUILayout.EnumPopup("Animation State", _selectedState);
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedState = newState;
                    _selectedFrame = 0;
                    _selectedEvent = null;
                    RestartPreview();
                }

                using (new EditorGUI.DisabledScope(!HasValidTrackTarget()))
                {
                    var newTrack = (CombatPresentationTrackSO)EditorGUILayout.ObjectField(GetTrackFieldLabel(), _track, typeof(CombatPresentationTrackSO), false);
                    if (newTrack != _track)
                    {
                        _track = newTrack;
                        AssignTrackToCurrentTarget(_track);
                        _selectedEvent = null;
                    }
                }
            }

            DrawTrackActions();
        }

        private void DrawTrackActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!HasValidTrackTarget()))
                {
                    if (GUILayout.Button("Create Track", GUILayout.Width(120f)))
                        CreateTrackAsset();
                }

                using (new EditorGUI.DisabledScope(_track == null))
                {
                    if (GUILayout.Button("Ping Track", GUILayout.Width(120f)))
                        EditorGUIUtility.PingObject(_track);
                }

                using (new EditorGUI.DisabledScope(_profile == null))
                {
                    if (GUILayout.Button("Preview in Previewer", GUILayout.Width(150f)))
                        OpenPreviewerWithContext(_profile, _moveDefinition, _selectedState, _selectedFrame);
                }
            }
        }

        private void DrawValidationPanel()
        {
            List<string> warnings = BuildWarnings();
            if (warnings.Count == 0)
            {
                EditorGUILayout.HelpBox($"Validation passed for the current {GetCurrentTrackTargetLabel().ToLowerInvariant()} track.", MessageType.Info);
                return;
            }

            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        private void DrawPreviewSection()
        {
            Sprite sprite = GetSprite(_selectedState, _selectedFrame);
            Texture preview = GetPreviewTexture(sprite);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

                Rect previewRect = GUILayoutUtility.GetRect(220f, 120f, GUILayout.ExpandWidth(true));
                if (preview != null)
                {
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    EditorGUI.HelpBox(previewRect, "No sprite on this frame.", MessageType.None);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(_previewPlaying ? "Pause" : "Play", GUILayout.Width(80f)))
                    {
                        if (_previewPlaying)
                        {
                            _previewPlaying = false;
                        }
                        else
                        {
                            RestartPreview();
                            _previewPlaying = true;
                        }
                    }

                    if (GUILayout.Button("Prev", GUILayout.Width(60f)))
                        StepFrame(-1);

                    if (GUILayout.Button("Next", GUILayout.Width(60f)))
                        StepFrame(1);

                    _previewLoop = GUILayout.Toggle(_previewLoop, "Loop");
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"Frame {_selectedFrame + 1}/{Mathf.Max(1, GetFrameCount(_selectedState))}", GUILayout.Width(110f));
                }
            }
        }

        private void DrawTimeline()
        {
            int frameCount = GetFrameCount(_selectedState);
            if (frameCount <= 0)
            {
                EditorGUILayout.HelpBox("The selected animation state has no frames.", MessageType.Warning);
                return;
            }

            bool hasMeleeMarker = TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.MeleeAttack, out int meleeMarkerFrame);
            bool hasRangedMarker = TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState.RangedAttack, out int rangedMarkerFrame);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Timeline ({GetFps(_selectedState):0.##} fps)", EditorStyles.boldLabel);
                DrawTimelineLegend();
                _timelineScroll = EditorGUILayout.BeginScrollView(_timelineScroll, GUILayout.Height(180f));
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int frame = 0; frame < frameCount; frame++)
                        DrawFrameCell(frame, hasMeleeMarker && frame == meleeMarkerFrame, hasRangedMarker && frame == rangedMarkerFrame);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawTimelineLegend()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawLegendSwatch(AudioColor, "Audio");
                DrawLegendSwatch(SpriteVfxColor, "Sprite VFX");
                DrawLegendSwatch(MarkerColor, "Marker");
                DrawLegendSwatch(MeleeMarkerColor, "Melee");
                DrawLegendSwatch(RangedMarkerColor, "Ranged");
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawLegendSwatch(Color color, string label)
        {
            Rect rect = GUILayoutUtility.GetRect(74f, 14f, GUILayout.Width(74f));
            Rect swatchRect = new Rect(rect.x, rect.y + 2f, 12f, 10f);
            EditorGUI.DrawRect(swatchRect, color);
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), label, EditorStyles.miniLabel);
        }

        private void DrawFrameCell(int frame, bool isMeleeMarker, bool isRangedMarker)
        {
            Sprite sprite = GetSprite(_selectedState, frame);
            Texture preview = GetPreviewTexture(sprite);
            List<CombatPresentationEvent> frameEvents = GetEventsForFrame(_selectedState, frame);

            Color previousColor = GUI.backgroundColor;
            if (frame == _selectedFrame)
                GUI.backgroundColor = new Color(0.78f, 0.89f, 1f, 1f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(96f)))
            {
                Rect markerRect = GUILayoutUtility.GetRect(80f, 6f, GUILayout.Width(80f));
                EditorGUI.DrawRect(markerRect, new Color(0.17f, 0.17f, 0.17f, 1f));
                if (isMeleeMarker)
                    EditorGUI.DrawRect(new Rect(markerRect.x + 2f, markerRect.y + 1f, 36f, 4f), MeleeMarkerColor);
                if (isRangedMarker)
                    EditorGUI.DrawRect(new Rect(markerRect.x + 42f, markerRect.y + 1f, 36f, 4f), RangedMarkerColor);

                Rect previewRect = GUILayoutUtility.GetRect(80f, 56f, GUILayout.Width(80f));
                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit, true);
                else
                    EditorGUI.HelpBox(previewRect, "Empty", MessageType.None);

                DrawMarkerStrip(frameEvents);

                if (GUILayout.Button($"Frame {frame}", GUILayout.Height(24f)))
                {
                    _selectedFrame = frame;
                    _previewFrame = frame;
                    if (_previewPlaying)
                        RestartPreview();
                    if (_selectedEvent != null && !frameEvents.Contains(_selectedEvent))
                        _selectedEvent = null;
                }
            }

            GUI.backgroundColor = previousColor;
        }

        private void DrawMarkerStrip(List<CombatPresentationEvent> frameEvents)
        {
            Rect stripRect = GUILayoutUtility.GetRect(80f, 14f, GUILayout.Width(80f));
            EditorGUI.DrawRect(stripRect, new Color(0.17f, 0.17f, 0.17f, 1f));

            if (frameEvents.Count == 0)
                return;

            float width = stripRect.width / Mathf.Max(1, frameEvents.Count);
            for (int i = 0; i < frameEvents.Count; i++)
            {
                Rect markerRect = new Rect(stripRect.x + (i * width), stripRect.y, Mathf.Max(3f, width - 1f), stripRect.height);
                EditorGUI.DrawRect(markerRect, GetColor(frameEvents[i].family));
            }
        }

        private void DrawDetailsPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Frame {_selectedFrame} Details", EditorStyles.boldLabel);
                _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll, GUILayout.MinHeight(220f));

                List<CombatPresentationEvent> frameEvents = GetEventsForFrame(_selectedState, _selectedFrame);
                for (int i = 0; i < frameEvents.Count; i++)
                {
                    var evt = frameEvents[i];
                    string label = string.IsNullOrWhiteSpace(evt.eventId) ? $"Event {i + 1}" : evt.eventId;
                    if (GUILayout.Toggle(ReferenceEquals(_selectedEvent, evt), label, "Button"))
                        _selectedEvent = evt;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Add Event", GUILayout.Width(120f)))
                        AddEventToSelectedFrame();

                    using (new EditorGUI.DisabledScope(_selectedEvent == null))
                    {
                        if (GUILayout.Button("Remove Selected", GUILayout.Width(140f)))
                            RemoveSelectedEvent();
                    }
                }

                EditorGUILayout.Space(6f);
                DrawSelectedEventInspector();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawSelectedEventInspector()
        {
            if (_selectedEvent == null)
            {
                EditorGUILayout.HelpBox("Select an event to edit its payload, anchor, and playback settings.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Selected Event", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Track Target", GetCurrentTrackTargetLabel());
            EditorGUILayout.LabelField("State", _selectedState.ToString());
            EditorGUILayout.LabelField("Frame", _selectedFrame.ToString());

            Undo.RecordObject(_track, "Edit Combat Presentation Event");
            EditorGUI.BeginChangeCheck();

            if (string.IsNullOrWhiteSpace(_selectedEvent.eventId))
                _selectedEvent.eventId = Guid.NewGuid().ToString("N");

            _selectedEvent.eventId = EditorGUILayout.TextField("Event Id", _selectedEvent.eventId);
            _selectedEvent.family = (PresentationEventFamily)EditorGUILayout.EnumPopup("Family", _selectedEvent.family);
            _selectedEvent.anchorTarget = (PresentationAnchorTarget)EditorGUILayout.EnumPopup("Anchor", _selectedEvent.anchorTarget);
            _selectedEvent.playbackSpace = (PresentationPlaybackSpace)EditorGUILayout.EnumPopup("Playback Space", _selectedEvent.playbackSpace);
            _selectedEvent.payload = EditorGUILayout.ObjectField("Payload", _selectedEvent.payload, typeof(UnityEngine.Object), false);

            if (_selectedEvent.family == PresentationEventFamily.Audio)
                _selectedEvent.audioAction = (AudioActionType)EditorGUILayout.EnumPopup("Audio Action", _selectedEvent.audioAction);

            _selectedEvent.offset = EditorGUILayout.Vector3Field("Offset", _selectedEvent.offset);
            EditorGUILayout.LabelField("Note");
            _selectedEvent.note = EditorGUILayout.TextArea(_selectedEvent.note, GUILayout.MinHeight(60f));

            _selectedEvent.animationState = _selectedState;
            _selectedEvent.frameIndex = _selectedFrame;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_track);
                Repaint();
            }
        }

        private void AddEventToSelectedFrame()
        {
            if (_track == null)
                return;

            Undo.RecordObject(_track, "Add Combat Presentation Event");
            var evt = new CombatPresentationEvent
            {
                eventId = Guid.NewGuid().ToString("N"),
                animationState = _selectedState,
                frameIndex = _selectedFrame,
                family = PresentationEventFamily.VFX_Sprite,
                anchorTarget = PresentationAnchorTarget.SelfRoot,
                playbackSpace = PresentationPlaybackSpace.AtAnchor,
                audioAction = AudioActionType.PlayOneShot,
                offset = Vector3.zero,
                note = string.Empty
            };
            _track.events.Add(evt);
            _selectedEvent = evt;
            EditorUtility.SetDirty(_track);
        }

        private void RemoveSelectedEvent()
        {
            if (_track == null || _selectedEvent == null)
                return;

            Undo.RecordObject(_track, "Remove Combat Presentation Event");
            _track.events.Remove(_selectedEvent);
            _selectedEvent = null;
            EditorUtility.SetDirty(_track);
        }

        private void CreateTrackAsset()
        {
            if (!HasValidTrackTarget())
                return;

            string targetName = _trackTarget == TrackTarget.Move && _moveDefinition != null
                ? _moveDefinition.name
                : _profile.name;

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Presentation Track",
                $"{targetName}_PresentationTrack",
                "asset",
                $"Choose where to save the new {GetCurrentTrackTargetLabel().ToLowerInvariant()} presentation track.");

            if (string.IsNullOrWhiteSpace(path))
                return;

            var track = CreateInstance<CombatPresentationTrackSO>();
            AssetDatabase.CreateAsset(track, path);
            AssetDatabase.SaveAssets();
            _track = track;
            AssignTrackToCurrentTarget(track);
            EditorGUIUtility.PingObject(track);
        }

        private void AssignTrackToCurrentTarget(CombatPresentationTrackSO track)
        {
            if (!HasValidTrackTarget())
                return;

            if (_trackTarget == TrackTarget.Move)
            {
                Undo.RecordObject(_moveDefinition, "Assign Move Presentation Track");
                _moveDefinition.presentationTrack = track;
                EditorUtility.SetDirty(_moveDefinition);
                return;
            }

            Undo.RecordObject(_profile, "Assign Combat Presentation Track");
            _profile.presentationTrack = track;
            EditorUtility.SetDirty(_profile);
        }

        private void StepFrame(int delta)
        {
            int frameCount = Mathf.Max(1, GetFrameCount(_selectedState));
            _selectedFrame = (_selectedFrame + delta + frameCount) % frameCount;
            _previewFrame = _selectedFrame;
            if (_previewPlaying)
                RestartPreview();
            Repaint();
        }

        private void RestartPreview()
        {
            _previewStartTime = EditorApplication.timeSinceStartup - (_selectedFrame / Mathf.Max(0.01f, GetFps(_selectedState)));
            _previewFrame = _selectedFrame;
        }

        private void ClampSelection()
        {
            int frameCount = Mathf.Max(1, GetFrameCount(_selectedState));
            _selectedFrame = Mathf.Clamp(_selectedFrame, 0, frameCount - 1);
            _previewFrame = Mathf.Clamp(_previewFrame, 0, frameCount - 1);

            if (_selectedEvent != null)
            {
                bool stillExists = false;
                if (_track != null && _track.events != null)
                {
                    for (int i = 0; i < _track.events.Count; i++)
                    {
                        if (ReferenceEquals(_track.events[i], _selectedEvent))
                        {
                            stillExists = true;
                            break;
                        }
                    }
                }

                if (!stillExists)
                    _selectedEvent = null;
            }
        }

        private void SyncTrackSelection(bool resetSelection)
        {
            _track = ResolveSelectedTrack();
            if (!resetSelection)
                return;

            _selectedFrame = 0;
            _selectedEvent = null;
            _previewFrame = 0;
            _previewPlaying = false;
        }

        private bool TryGetMarkerFrame(CombatChainlingVisual.CombatAnimState state, out int frame)
        {
            frame = -1;
            if (_profile == null)
                return false;

            GameObject probe = new GameObject("CombatMarkerProbe")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                probe.AddComponent<MeshFilter>();
                probe.AddComponent<MeshRenderer>();
                CombatChainlingVisual visual = probe.AddComponent<CombatChainlingVisual>();
                visual.ApplyProfile(_profile);
                return visual.TryGetMarkerFrame(state, out frame);
            }
            finally
            {
                DestroyImmediate(probe);
            }
        }

        private static void OpenPreviewerWithContext(
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame)
        {
            Type previewerType = FindEditorType("Chainbound.Combat.Editor.CombatAnimationPreviewerWindow");
            MethodInfo openMethod = previewerType != null
                ? previewerType.GetMethod(
                    "OpenWithContext",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(CombatVisualProfileSO), typeof(MoveDefinitionSO), typeof(CombatChainlingVisual.CombatAnimState), typeof(int) },
                    null)
                : null;

            if (openMethod == null)
            {
                Debug.LogWarning("[CombatPresentationAuthoringTool] Could not find CombatAnimationPreviewerWindow.OpenWithContext.");
                return;
            }

            openMethod.Invoke(null, new object[] { profile, moveDefinition, state, frame });
        }

        private static Type FindEditorType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName, false);
                if (type != null)
                    return type;
            }

            return null;
        }

        private CombatPresentationTrackSO ResolveSelectedTrack()
        {
            if (_trackTarget == TrackTarget.Move)
                return _moveDefinition != null ? _moveDefinition.presentationTrack : null;

            return _profile != null ? _profile.presentationTrack : null;
        }

        private bool HasValidTrackTarget()
        {
            if (_profile == null)
                return false;

            return _trackTarget != TrackTarget.Move || _moveDefinition != null;
        }

        private string GetCurrentTrackTargetLabel()
        {
            return _trackTarget == TrackTarget.Move ? "Move" : "Profile";
        }

        private string GetTrackFieldLabel()
        {
            return _trackTarget == TrackTarget.Move ? "Move Track" : "Profile Track";
        }

        private List<string> BuildWarnings()
        {
            var warnings = new List<string>();
            if (_profile == null || _track == null)
                return warnings;

            if (GetFps(_selectedState) <= 0f)
                warnings.Add($"{_selectedState} FPS is zero or negative.");

            for (int i = 0; i < _track.events.Count; i++)
            {
                var evt = _track.events[i];
                if (evt == null)
                    continue;

                int frameCount = GetFrameCount(evt.animationState);
                if (evt.frameIndex < 0 || evt.frameIndex >= frameCount)
                    warnings.Add($"Event '{GetEventLabel(evt, i)}' is on invalid frame {evt.frameIndex} for {evt.animationState} ({frameCount} frames).");

                bool payloadRequired = evt.family == PresentationEventFamily.Audio ||
                                       evt.family == PresentationEventFamily.VFX_Sprite ||
                                       evt.family == PresentationEventFamily.VFX_Graph ||
                                       evt.family == PresentationEventFamily.PostFx;
                if (payloadRequired && evt.payload == null)
                    warnings.Add($"Event '{GetEventLabel(evt, i)}' is missing a payload.");

                if (evt.family == PresentationEventFamily.PostFx &&
                    evt.payload != null &&
                    !(evt.payload is CombatPostFxCueSO))
                {
                    warnings.Add($"Event '{GetEventLabel(evt, i)}' PostFx payload must be a CombatPostFxCueSO.");
                }

                if (evt.family == PresentationEventFamily.Audio && (evt.audioAction == AudioActionType.StartLoop || evt.audioAction == AudioActionType.StopLoop) && string.IsNullOrWhiteSpace(evt.eventId))
                    warnings.Add($"Audio loop event {i + 1} needs a stable eventId so start/stop pairs can match.");
            }

            for (int i = 0; i < _track.events.Count; i++)
            {
                var evt = _track.events[i];
                if (evt == null || evt.family != PresentationEventFamily.Audio || evt.audioAction != AudioActionType.StartLoop || string.IsNullOrWhiteSpace(evt.eventId))
                    continue;

                bool hasStop = false;
                for (int j = 0; j < _track.events.Count; j++)
                {
                    var other = _track.events[j];
                    if (other == null)
                        continue;

                    if (other.family == PresentationEventFamily.Audio && other.audioAction == AudioActionType.StopLoop && other.eventId == evt.eventId)
                    {
                        hasStop = true;
                        break;
                    }
                }

                if (!hasStop)
                    warnings.Add($"Loop event '{evt.eventId}' has no matching StopLoop event.");
            }

            return warnings;
        }

        private List<CombatPresentationEvent> GetEventsForFrame(CombatChainlingVisual.CombatAnimState state, int frame)
        {
            var results = new List<CombatPresentationEvent>();
            if (_track == null || _track.events == null)
                return results;

            _track.AppendEventsForFrame(state, frame, results);
            return results;
        }

        private int GetFrameCount(CombatChainlingVisual.CombatAnimState state)
        {
            var sequence = GetSequence(state);
            return sequence != null ? Mathf.Max(0, sequence.Length) : 0;
        }

        private float GetFps(CombatChainlingVisual.CombatAnimState state)
        {
            if (_profile == null)
                return 0f;

            switch (state)
            {
                case CombatChainlingVisual.CombatAnimState.MeleeAttack:
                    return _profile.meleeAttackFPS;
                case CombatChainlingVisual.CombatAnimState.RangedAttack:
                    return _profile.rangedAttackFPS;
                case CombatChainlingVisual.CombatAnimState.Hit:
                    return _profile.hitFPS;
                default:
                    return _profile.idleFPS;
            }
        }

        private SpriteAnimationSequence GetSequence(CombatChainlingVisual.CombatAnimState state)
        {
            if (_profile == null)
                return null;

            switch (state)
            {
                case CombatChainlingVisual.CombatAnimState.MeleeAttack:
                    return _profile.meleeAttackSequence;
                case CombatChainlingVisual.CombatAnimState.RangedAttack:
                    return _profile.rangedAttackSequence;
                case CombatChainlingVisual.CombatAnimState.Hit:
                    return _profile.hitSequence;
                default:
                    return _profile.idleSequence;
            }
        }

        private Sprite GetSprite(CombatChainlingVisual.CombatAnimState state, int frame)
        {
            var sequence = GetSequence(state);
            if (sequence == null || sequence.Length == 0)
                return null;

            frame = Mathf.Clamp(frame, 0, sequence.Length - 1);
            return sequence[frame];
        }

        private static Texture GetPreviewTexture(Sprite sprite)
        {
            if (sprite == null)
                return null;

            Texture preview = AssetPreview.GetAssetPreview(sprite);
            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(sprite);
            return preview;
        }

        private static string GetEventLabel(CombatPresentationEvent evt, int index)
        {
            return string.IsNullOrWhiteSpace(evt.eventId) ? $"Event {index + 1}" : evt.eventId;
        }

        private static Color GetColor(PresentationEventFamily family)
        {
            switch (family)
            {
                case PresentationEventFamily.Audio:
                    return AudioColor;
                case PresentationEventFamily.VFX_Sprite:
                    return SpriteVfxColor;
                case PresentationEventFamily.GameplayMarker:
                    return MarkerColor;
                case PresentationEventFamily.PostFx:
                    return new Color(0.55f, 0.35f, 0.95f, 1f);
                default:
                    return DebugColor;
            }
        }
    }
}
