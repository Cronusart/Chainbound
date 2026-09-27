using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    internal enum CombatPreviewEventSourceKind
    {
        Profile,
        Move,
        Preview
    }

    internal readonly struct CombatPreviewEventBinding
    {
        public CombatPreviewEventBinding(CombatPresentationEvent evt, CombatPreviewEventSourceKind sourceKind)
        {
            Event = evt;
            SourceKind = sourceKind;
        }

        public CombatPresentationEvent Event { get; }
        public CombatPreviewEventSourceKind SourceKind { get; }
        public bool IsAuthored => SourceKind != CombatPreviewEventSourceKind.Preview;

        public string SourceLabel
        {
            get
            {
                switch (SourceKind)
                {
                    case CombatPreviewEventSourceKind.Profile:
                        return "Profile";
                    case CombatPreviewEventSourceKind.Move:
                        return "Move";
                    default:
                        return "Preview";
                }
            }
        }
    }

    internal sealed class CombatPreviewEventSimulator
    {
        private readonly List<CombatPresentationEvent> _temporaryEvents = new List<CombatPresentationEvent>();
        private int _nextPreviewEventId = 1;

        public IReadOnlyList<CombatPresentationEvent> TemporaryEvents => _temporaryEvents;

        public CombatPresentationEvent AddTemporaryEvent(CombatChainlingVisual.CombatAnimState state, int frame)
        {
            var evt = new CombatPresentationEvent
            {
                eventId = $"preview_evt_{_nextPreviewEventId++}",
                animationState = state,
                frameIndex = frame,
                family = PresentationEventFamily.VFX_Sprite,
                anchorTarget = PresentationAnchorTarget.Impact,
                playbackSpace = PresentationPlaybackSpace.AtAnchor,
                audioAction = AudioActionType.PlayOneShot,
                offset = Vector3.zero,
                note = string.Empty,
                payload = null
            };

            _temporaryEvents.Add(evt);
            return evt;
        }

        public bool RemoveTemporaryEvent(CombatPresentationEvent evt)
        {
            return evt != null && _temporaryEvents.Remove(evt);
        }

        public bool IsTemporaryEvent(CombatPresentationEvent evt)
        {
            return evt != null && _temporaryEvents.Contains(evt);
        }

        public void GetFrameEvents(
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            bool includeAuthoredTrack,
            List<CombatPreviewEventBinding> results)
        {
            if (results == null)
                return;

            results.Clear();

            if (includeAuthoredTrack)
            {
                CombatPresentationTrackSO profileTrack = profile != null ? profile.presentationTrack : null;
                CombatPresentationTrackSO moveTrack = moveDefinition != null ? moveDefinition.presentationTrack : null;

                AppendTrackEvents(profileTrack, state, frame, CombatPreviewEventSourceKind.Profile, results);
                if (moveTrack != null && !ReferenceEquals(moveTrack, profileTrack))
                    AppendTrackEvents(moveTrack, state, frame, CombatPreviewEventSourceKind.Move, results);
            }

            AppendTemporaryEvents(state, frame, results);
        }

        public void CountFrameEvents(
            CombatVisualProfileSO profile,
            MoveDefinitionSO moveDefinition,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            bool includeAuthoredTrack,
            out int profileCount,
            out int moveCount,
            out int previewCount)
        {
            profileCount = 0;
            moveCount = 0;
            previewCount = 0;

            if (includeAuthoredTrack)
            {
                CombatPresentationTrackSO profileTrack = profile != null ? profile.presentationTrack : null;
                CombatPresentationTrackSO moveTrack = moveDefinition != null ? moveDefinition.presentationTrack : null;

                profileCount = CountTrackEvents(profileTrack, state, frame);
                if (moveTrack != null && !ReferenceEquals(moveTrack, profileTrack))
                    moveCount = CountTrackEvents(moveTrack, state, frame);
            }

            for (int i = 0; i < _temporaryEvents.Count; i++)
            {
                var evt = _temporaryEvents[i];
                if (evt != null && evt.animationState == state && evt.frameIndex == frame)
                    previewCount++;
            }
        }

        private static void AppendTrackEvents(
            CombatPresentationTrackSO track,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            CombatPreviewEventSourceKind sourceKind,
            List<CombatPreviewEventBinding> results)
        {
            if (track == null || track.events == null)
                return;

            for (int i = 0; i < track.events.Count; i++)
            {
                var evt = track.events[i];
                if (evt == null || evt.animationState != state || evt.frameIndex != frame)
                    continue;

                results.Add(new CombatPreviewEventBinding(evt, sourceKind));
            }
        }

        private void AppendTemporaryEvents(
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            List<CombatPreviewEventBinding> results)
        {
            for (int i = 0; i < _temporaryEvents.Count; i++)
            {
                var evt = _temporaryEvents[i];
                if (evt == null || evt.animationState != state || evt.frameIndex != frame)
                    continue;

                results.Add(new CombatPreviewEventBinding(evt, CombatPreviewEventSourceKind.Preview));
            }
        }

        private static int CountTrackEvents(CombatPresentationTrackSO track, CombatChainlingVisual.CombatAnimState state, int frame)
        {
            if (track == null || track.events == null)
                return 0;

            int count = 0;
            for (int i = 0; i < track.events.Count; i++)
            {
                var evt = track.events[i];
                if (evt != null && evt.animationState == state && evt.frameIndex == frame)
                    count++;
            }

            return count;
        }
    }
}
