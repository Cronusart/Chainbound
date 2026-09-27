using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatPresentationTrack_", menuName = "Chainbound/Combat/Presentation/Tracks/Presentation Track")]
    public class CombatPresentationTrackSO : ScriptableObject
    {
        public List<CombatPresentationEvent> events = new List<CombatPresentationEvent>();

        public List<CombatPresentationEvent> GetEventsForFrame(CombatChainlingVisual.CombatAnimState state, int frame)
        {
            var matches = new List<CombatPresentationEvent>();
            AppendEventsForFrame(state, frame, matches);
            return matches;
        }

        public void GetEventsForFrame(CombatChainlingVisual.CombatAnimState state, int frame, List<CombatPresentationEvent> results)
        {
            if (results == null)
                return;

            results.Clear();
            AppendEventsForFrame(state, frame, results);
        }

        public void AppendEventsForFrame(CombatChainlingVisual.CombatAnimState state, int frame, List<CombatPresentationEvent> results)
        {
            if (results == null || events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null)
                    continue;

                if (evt.animationState != state || evt.frameIndex != frame)
                    continue;

                results.Add(evt);
            }
        }

        public bool HasAudioEvents()
        {
            if (events == null)
                return false;

            for (int i = 0; i < events.Count; i++)
            {
                if (IsAuthoredAudioEvent(events[i]))
                    return true;
            }

            return false;
        }

        public bool HasAudioEventForState(CombatChainlingVisual.CombatAnimState state)
        {
            if (events == null)
                return false;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (IsAuthoredAudioEvent(evt) && evt.animationState == state)
                    return true;
            }

            return false;
        }

        public bool HasAudioEventForState(CombatChainlingVisual.CombatAnimState state, AudioActionType audioAction)
        {
            if (events == null)
                return false;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (IsAuthoredAudioEvent(evt) &&
                    evt.animationState == state &&
                    evt.audioAction == audioAction)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAuthoredAudioEvent(CombatPresentationEvent evt)
        {
            return evt != null &&
                   evt.family == PresentationEventFamily.Audio &&
                   evt.payload != null;
        }

        private void OnValidate()
        {
            if (events == null)
                return;

            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null)
                    continue;

                if (string.IsNullOrWhiteSpace(evt.eventId))
                    evt.eventId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
