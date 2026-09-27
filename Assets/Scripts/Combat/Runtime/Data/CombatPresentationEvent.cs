using System;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class CombatPresentationEvent
    {
        public string eventId = string.Empty;
        public CombatChainlingVisual.CombatAnimState animationState = CombatChainlingVisual.CombatAnimState.Idle;
        public int frameIndex;
        public PresentationEventFamily family = PresentationEventFamily.VFX_Sprite;
        public PresentationAnchorTarget anchorTarget = PresentationAnchorTarget.SelfRoot;
        public PresentationPlaybackSpace playbackSpace = PresentationPlaybackSpace.AtAnchor;
        public UnityEngine.Object payload;
        public AudioActionType audioAction = AudioActionType.PlayOneShot;
        public Vector3 offset = Vector3.zero;
        [TextArea(2, 4)] public string note = string.Empty;
    }
}