using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class CombatPresentationDirector : MonoBehaviour
    {
        [Header("Slot Views")]
        [SerializeField] private Combat2ChainlingView team0Slot0;
        [SerializeField] private Combat2ChainlingView team0Slot1;
        [SerializeField] private Combat2ChainlingView team1Slot0;
        [SerializeField] private Combat2ChainlingView team1Slot1;

        [Header("Bridges")]
        [SerializeField] private MonoBehaviour audioBridgeBehaviour;

        [Header("Post FX")]
        [Tooltip("Combat-owned post-FX controller. Prefer an explicit assignment; child search is scoped to this object only.")]
        [SerializeField] private CombatPostFxVolumeController postFxVolumeController;

        [Header("Discovery")]
        [SerializeField] private bool autoDiscoverSlots = true;
        [SerializeField] private bool autoDiscoverAudioBridge = true;
        [SerializeField] private bool enableDebugLogs;

        private sealed class ActiveMovePresentation
        {
            public string MoveInstanceId;
            public MoveDefinitionSO MoveDefinition;
            public CombatPresentationTrackSO Track;
        }

        private readonly Dictionary<CombatChainlingVisual, Combat2ChainlingView> _viewByVisual = new Dictionary<CombatChainlingVisual, Combat2ChainlingView>();
        private readonly Dictionary<CombatChainlingVisual, Action<CombatChainlingVisual.CombatAnimState, int>> _subscriptions = new Dictionary<CombatChainlingVisual, Action<CombatChainlingVisual.CombatAnimState, int>>();
        private readonly Dictionary<CombatChainlingVisual, ActiveMovePresentation> _activeMovePresentationByVisual = new Dictionary<CombatChainlingVisual, ActiveMovePresentation>();
        private readonly Dictionary<string, AudioLoopToken> _activeLoopTokens = new Dictionary<string, AudioLoopToken>();
        private readonly List<CombatPresentationEvent> _frameEvents = new List<CombatPresentationEvent>();

        public event Action<Combat2ChainlingView, CombatPresentationEvent> GameplayMarkerFired;

        private ICombatAudioBridge AudioBridge => audioBridgeBehaviour as ICombatAudioBridge;

        private void Reset()
        {
            TryAutoAssignReferences();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                TryAutoAssignReferences();
        }

        private void OnEnable()
        {
            Rebind();
        }

        private void OnDisable()
        {
            Unbind();
            StopAllActiveLoops();
            postFxVolumeController?.ForceClear();
        }

        public void SetPostFxVolumeController(CombatPostFxVolumeController controller)
        {
            if (controller != null)
                postFxVolumeController = controller;
        }

        public void Rebind()
        {
            Unbind();
            TryAutoAssignReferences();

            SubscribeSlot(team0Slot0);
            SubscribeSlot(team0Slot1);
            SubscribeSlot(team1Slot0);
            SubscribeSlot(team1Slot1);
        }

        public void BeginMovePresentation(Combat2ChainlingView slot, MoveDefinitionSO moveDefinition, string moveInstanceId = null)
        {
            if (slot == null)
                return;

            BeginMovePresentation(slot.Visual, moveDefinition, moveInstanceId);
        }

        public void BeginMovePresentation(CombatChainlingVisual visual, MoveDefinitionSO moveDefinition, string moveInstanceId = null)
        {
            if (visual == null)
                return;

            if (moveDefinition == null || moveDefinition.presentationTrack == null)
            {
                _activeMovePresentationByVisual.Remove(visual);
                return;
            }

            _activeMovePresentationByVisual[visual] = new ActiveMovePresentation
            {
                MoveInstanceId = moveInstanceId ?? string.Empty,
                MoveDefinition = moveDefinition,
                Track = moveDefinition.presentationTrack,
            };

            if (enableDebugLogs)
                Debug.Log($"[CombatPresentationDirector] Begin move presentation: {moveDefinition.moveId} on {visual.name}.", this);
        }

        public void EndMovePresentation(Combat2ChainlingView slot, string moveInstanceId = null)
        {
            if (slot == null)
                return;

            EndMovePresentation(slot.Visual, moveInstanceId);
        }

        public void EndMovePresentation(CombatChainlingVisual visual, string moveInstanceId = null)
        {
            if (visual == null)
                return;

            if (!_activeMovePresentationByVisual.TryGetValue(visual, out var activeMove))
                return;

            if (!string.IsNullOrEmpty(moveInstanceId) &&
                !string.IsNullOrEmpty(activeMove.MoveInstanceId) &&
                !string.Equals(activeMove.MoveInstanceId, moveInstanceId, StringComparison.Ordinal))
            {
                return;
            }

            _activeMovePresentationByVisual.Remove(visual);
        }

        private void Unbind()
        {
            foreach (var pair in _subscriptions)
            {
                if (pair.Key != null)
                    pair.Key.OnFrameAdvanced -= pair.Value;
            }

            _subscriptions.Clear();
            _viewByVisual.Clear();
            _activeMovePresentationByVisual.Clear();
        }

        private void SubscribeSlot(Combat2ChainlingView slot)
        {
            if (slot == null || slot.Visual == null)
                return;

            var visual = slot.Visual;
            if (_subscriptions.ContainsKey(visual))
                return;

            Action<CombatChainlingVisual.CombatAnimState, int> handler = (state, frame) => HandleFrameAdvanced(visual, state, frame);
            _viewByVisual[visual] = slot;
            _subscriptions[visual] = handler;
            visual.OnFrameAdvanced += handler;
        }

        private void TryAutoAssignReferences()
        {
            if (!gameObject.scene.IsValid())
                return;

            if (autoDiscoverSlots)
                AutoAssignSlots();

            if (autoDiscoverAudioBridge)
                AutoAssignAudioBridge();
        }

        private void AutoAssignSlots()
        {
            var discovered = FindObjectsOfType<Combat2ChainlingView>(true);
            if (discovered == null || discovered.Length == 0)
                return;

            var available = new List<Combat2ChainlingView>();
            for (int i = 0; i < discovered.Length; i++)
            {
                var slot = discovered[i];
                if (slot == null)
                    continue;

                if (slot == team0Slot0 || slot == team0Slot1 || slot == team1Slot0 || slot == team1Slot1)
                    continue;

                available.Add(slot);
            }

            int nextIndex = 0;
            if (team0Slot0 == null && nextIndex < available.Count)
                team0Slot0 = available[nextIndex++];
            if (team0Slot1 == null && nextIndex < available.Count)
                team0Slot1 = available[nextIndex++];
            if (team1Slot0 == null && nextIndex < available.Count)
                team1Slot0 = available[nextIndex++];
            if (team1Slot1 == null && nextIndex < available.Count)
                team1Slot1 = available[nextIndex++];
        }

        private void AutoAssignAudioBridge()
        {
            MonoBehaviour resolvedBehaviour;
            CombatAudioBridgeResolver.Resolve(
                audioBridgeBehaviour,
                autoDiscoverAudioBridge,
                out resolvedBehaviour);
            if (resolvedBehaviour != null)
                audioBridgeBehaviour = resolvedBehaviour;
        }

        private void HandleFrameAdvanced(CombatChainlingVisual visual, CombatChainlingVisual.CombatAnimState state, int frame)
        {
            if (visual == null)
                return;

            if (!_viewByVisual.TryGetValue(visual, out var view) || view == null)
                return;

            _frameEvents.Clear();

            CombatPresentationTrackSO profileTrack = visual.CurrentProfile != null ? visual.CurrentProfile.presentationTrack : null;
            CombatPresentationPoseUtility.AppendEventsForFrame(profileTrack, state, frame, _frameEvents);

            if (_activeMovePresentationByVisual.TryGetValue(visual, out var activeMove) && activeMove.Track != null && activeMove.Track != profileTrack)
                CombatPresentationPoseUtility.AppendEventsForFrame(activeMove.Track, state, frame, _frameEvents);

            for (int i = 0; i < _frameEvents.Count; i++)
            {
                var evt = _frameEvents[i];
                if (evt == null)
                    continue;

                DispatchEvent(view, visual, evt);
            }
        }

        private void DispatchEvent(Combat2ChainlingView view, CombatChainlingVisual visual, CombatPresentationEvent evt)
        {
            switch (evt.family)
            {
                case PresentationEventFamily.VFX_Sprite:
                    DispatchSpriteVfx(view, evt);
                    break;
                case PresentationEventFamily.VFX_Graph:
                    DispatchGraphVfx(view, evt);
                    break;
                case PresentationEventFamily.Audio:
                    DispatchAudio(view, visual, evt);
                    break;
                case PresentationEventFamily.GameplayMarker:
                    GameplayMarkerFired?.Invoke(view, evt);
                    break;
                case PresentationEventFamily.DebugNote:
                    Debug.Log($"[CombatPresentationDirector] {view.name} frame {evt.frameIndex}: {evt.note}", this);
                    break;
                case PresentationEventFamily.PostFx:
                    DispatchPostFx(evt);
                    break;
            }
        }

        private void DispatchPostFx(CombatPresentationEvent evt)
        {
            var cue = evt.payload as CombatPostFxCueSO;
            if (cue == null || !cue.IsPlayable)
            {
                WarnInvalidPayload(evt, "CombatPostFxCueSO");
                return;
            }

            CombatPostFxVolumeController controller = ResolvePostFxVolumeController();
            if (controller == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CombatPresentationDirector] PostFx event skipped — no CombatPostFxVolumeController on this object.", this);
                return;
            }

            controller.Play(cue);
        }

        private CombatPostFxVolumeController ResolvePostFxVolumeController()
        {
            if (postFxVolumeController != null)
                return postFxVolumeController;

            // Scoped to this director only — never FindObjectOfType<Volume>() / scene Global Volume.
            postFxVolumeController = GetComponent<CombatPostFxVolumeController>();
            if (postFxVolumeController == null)
                postFxVolumeController = GetComponentInChildren<CombatPostFxVolumeController>(true);

            return postFxVolumeController;
        }

        private void DispatchSpriteVfx(Combat2ChainlingView view, CombatPresentationEvent evt)
        {
            var definition = evt.payload as SpriteFlipbookVfxDefinitionSO;
            if (definition == null)
            {
                WarnInvalidPayload(evt, "SpriteFlipbookVfxDefinitionSO");
                return;
            }

            CombatPresentationPoseUtility.ResolvePlaybackPose(view, evt, out var position, out var follow);
            Transform parent = evt.playbackSpace == PresentationPlaybackSpace.AtSelf || evt.playbackSpace == PresentationPlaybackSpace.AtAnchor
                ? follow
                : null;

            SpriteFlipbookVfxPlayer.Spawn(definition, position, definition.defaultScale, parent);
        }

        private void DispatchGraphVfx(Combat2ChainlingView view, CombatPresentationEvent evt)
        {
            var definition = evt.payload as GraphVfxDefinitionSO;
            if (definition == null)
            {
                WarnInvalidPayload(evt, "GraphVfxDefinitionSO");
                return;
            }

            if (enableDebugLogs)
                Debug.Log($"[CombatPresentationDirector] Graph VFX event '{evt.eventId}' is a Phase 1 stub on {view.name}.", this);
        }

        private void DispatchAudio(Combat2ChainlingView view, CombatChainlingVisual visual, CombatPresentationEvent evt)
        {
            var audioBridge = AudioBridge;
            if (audioBridge == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[CombatPresentationDirector] Audio event skipped because no ICombatAudioBridge is assigned.", this);
                return;
            }

            if (evt.payload == null)
            {
                WarnInvalidPayload(evt, "SoundEventSO");
                return;
            }

            CombatPresentationPoseUtility.ResolvePlaybackPose(view, evt, out var position, out var follow);
            Vector3? audioPosition = evt.playbackSpace == PresentationPlaybackSpace.Global2D ? (Vector3?)null : position;
            Transform followTarget = evt.playbackSpace == PresentationPlaybackSpace.AtSelf || evt.playbackSpace == PresentationPlaybackSpace.AtAnchor
                ? follow
                : null;

            string loopKey = BuildLoopKey(visual, evt);
            switch (evt.audioAction)
            {
                case AudioActionType.PlayOneShot:
                    audioBridge.PlayOneShot(evt.payload, audioPosition, followTarget);
                    break;
                case AudioActionType.StartLoop:
                    if (_activeLoopTokens.TryGetValue(loopKey, out var existingToken) && existingToken.IsValid)
                        audioBridge.StopLoop(existingToken);

                    var token = audioBridge.StartLoop(evt.payload, audioPosition, followTarget);
                    if (token.IsValid)
                        _activeLoopTokens[loopKey] = token;
                    break;
                case AudioActionType.StopLoop:
                    if (_activeLoopTokens.TryGetValue(loopKey, out var activeToken) && activeToken.IsValid)
                        audioBridge.StopLoop(activeToken);
                    _activeLoopTokens.Remove(loopKey);
                    break;
            }
        }

        private static string BuildLoopKey(CombatChainlingVisual visual, CombatPresentationEvent evt)
        {
            string eventKey = string.IsNullOrWhiteSpace(evt.eventId)
                ? $"{evt.family}:{evt.animationState}:{evt.frameIndex}"
                : evt.eventId;

            return $"{visual.GetInstanceID()}:{eventKey}";
        }

        private void StopAllActiveLoops()
        {
            var audioBridge = AudioBridge;
            if (audioBridge == null)
            {
                _activeLoopTokens.Clear();
                return;
            }

            foreach (var pair in _activeLoopTokens)
            {
                if (pair.Value.IsValid)
                    audioBridge.StopLoop(pair.Value);
            }

            _activeLoopTokens.Clear();
        }

        private void WarnInvalidPayload(CombatPresentationEvent evt, string expectedType)
        {
            Debug.LogWarning($"[CombatPresentationDirector] Event '{evt.eventId}' expected payload type {expectedType} but received '{evt.payload}'.", this);
        }
    }
}
