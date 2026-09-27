using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public static class CombatPresentationPoseUtility
    {
        public static void AppendEventsForFrame(
            CombatPresentationTrackSO track,
            CombatChainlingVisual.CombatAnimState state,
            int frame,
            List<CombatPresentationEvent> results)
        {
            if (track == null || results == null)
                return;

            track.AppendEventsForFrame(state, frame, results);
        }

        public static void ResolvePlaybackPose(Combat2ChainlingView view, CombatPresentationEvent evt, out Vector3 position, out Transform follow)
        {
            Transform self = view != null && view.Visual != null ? view.Visual.transform : (view != null ? view.transform : null);
            PresentationAnchorTarget effectiveAnchorTarget = evt.playbackSpace == PresentationPlaybackSpace.AtSelf
                ? PresentationAnchorTarget.SelfRoot
                : evt.anchorTarget;

            Transform anchor = evt.playbackSpace == PresentationPlaybackSpace.AtSelf
                ? self
                : ResolveAnchorTransform(view, effectiveAnchorTarget);

            if (anchor == null)
                anchor = self;

            Vector3 profileOffset = evt.playbackSpace == PresentationPlaybackSpace.Global2D
                ? Vector3.zero
                : ResolveProfileAnchorWorldOffset(view, effectiveAnchorTarget);

            switch (evt.playbackSpace)
            {
                case PresentationPlaybackSpace.Global2D:
                    position = Vector3.zero;
                    follow = null;
                    break;
                case PresentationPlaybackSpace.World:
                    position = (anchor != null ? anchor.position : Vector3.zero) + profileOffset + evt.offset;
                    follow = null;
                    break;
                case PresentationPlaybackSpace.AtSelf:
                    follow = self;
                    position = (self != null ? self.position : Vector3.zero) + profileOffset + evt.offset;
                    break;
                default:
                    follow = anchor;
                    position = (anchor != null ? anchor.position : Vector3.zero) + profileOffset + evt.offset;
                    break;
            }
        }

        public static Transform ResolveAnchorTransform(Combat2ChainlingView view, PresentationAnchorTarget anchorTarget)
        {
            if (view == null)
                return null;

            switch (anchorTarget)
            {
                case PresentationAnchorTarget.CameraFocus:
                    return view.CameraFocus;
                case PresentationAnchorTarget.MeleeStop:
                    return view.MeleeStopAnchor;
                case PresentationAnchorTarget.Impact:
                    return view.ImpactAnchor;
                case PresentationAnchorTarget.ProjectileOrigin:
                    return view.ProjectileOriginAnchor;
                case PresentationAnchorTarget.Barrier:
                    return view.BarrierAnchor;
                case PresentationAnchorTarget.CustomOffset:
                case PresentationAnchorTarget.SelfRoot:
                default:
                    return view.Visual != null ? view.Visual.transform : view.transform;
            }
        }

        public static Vector3 ResolveProfileAnchorWorldOffset(Combat2ChainlingView view, PresentationAnchorTarget anchorTarget)
        {
            CombatChainlingVisual visual = view != null ? view.Visual : null;
            CombatVisualProfileSO profile = visual != null ? visual.CurrentProfile : null;
            if (profile == null)
                return Vector3.zero;

            Vector3 localOffset = profile.GetAnchorOffset(anchorTarget, visual != null && visual.FlipX);
            if (localOffset == Vector3.zero)
                return Vector3.zero;

            Transform basis = ResolveOffsetBasisTransform(view, visual);
            return basis != null ? basis.TransformVector(localOffset) : localOffset;
        }

        private static Transform ResolveOffsetBasisTransform(Combat2ChainlingView view, CombatChainlingVisual visual)
        {
            if (view != null && view.VisualRootTransform != null)
                return view.VisualRootTransform;

            if (visual != null)
                return visual.transform;

            return view != null ? view.transform : null;
        }
    }
}