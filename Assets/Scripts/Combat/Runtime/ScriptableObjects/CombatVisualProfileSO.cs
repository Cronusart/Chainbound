using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatVisual_", menuName = "Chainbound/Combat/Presentation/Visual Profiles/Combat Visual Profile")]
    public class CombatVisualProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Species this profile belongs to (informational only).")]
        public string speciesId;

        [Tooltip("Display label shown in the Inspector.")]
        public string displayName;

        [Header("Sprite Sequences")]
        [Tooltip("Idle loop (breathing / blinking). Played continuously when no action.")]
        public SpriteAnimationSequence idleSequence = new SpriteAnimationSequence();
        public float idleFPS = 4f;

        [Tooltip("Melee attack one-shot.")]
        public SpriteAnimationSequence meleeAttackSequence = new SpriteAnimationSequence();
        public float meleeAttackFPS = 12f;

        [Tooltip("Ranged attack one-shot.")]
        public SpriteAnimationSequence rangedAttackSequence = new SpriteAnimationSequence();
        public float rangedAttackFPS = 10f;

        [Tooltip("Hit reaction one-shot.")]
        public SpriteAnimationSequence hitSequence = new SpriteAnimationSequence();
        public float hitFPS = 8f;

        [Tooltip("Default swap-in / intro flipbook played when the Chainling enters.")]
        [FormerlySerializedAs("glitchInSequence")]
        public SpriteAnimationSequence swapInSequence = new SpriteAnimationSequence();
        [FormerlySerializedAs("glitchInFPS")]
        public float swapInFPS = 10f;

        [Tooltip("Default voluntary swap-out flipbook.")]
        [FormerlySerializedAs("glitchOutSequence")]
        public SpriteAnimationSequence swapOutSequence = new SpriteAnimationSequence();
        [FormerlySerializedAs("glitchOutFPS")]
        public float swapOutFPS = 10f;

        [Tooltip("Default KO flipbook. Empty stock profiles fall back to the swap-out sequence.")]
        public SpriteAnimationSequence koSequence = new SpriteAnimationSequence();
        public float koFPS = 10f;

        [Header("Animation Alternates")]
        [Tooltip("Variant-correct alternates keyed by animation slot and equipped style ID.")]
        public List<CombatVisualAnimationAlternate> animationAlternates = new List<CombatVisualAnimationAlternate>();

        [Header("Rendering")]
        [Tooltip("Size of the quad in world units. Default 5x5 matches the scene's x5 pixel scale.")]
        public Vector2 spriteSize = new Vector2(5f, 5f);

        [Tooltip("When true, the combat billboard quad is rebuilt around each sprite's imported pivot so stance pivots align to the slot origin.")]
        public bool useSpritePivot = true;

        [Tooltip("Optional local-space nudge applied after sprite pivot alignment. X/Y are billboard-local world units.")]
        public Vector2 spritePivotWorldOffset = Vector2.zero;

        [Header("Presentation")]
        [Tooltip("Optional per-frame species-level VFX/SFX track.")]
        public CombatPresentationTrackSO presentationTrack;

        [Tooltip("Optional local offsets applied per presentation anchor for this species profile.")]
        public List<CombatPresentationAnchorOffset> anchorOffsets = new List<CombatPresentationAnchorOffset>();

        [Header("Fallback SFX")]
        [Tooltip("Optional SoundEventSO used when spawn/swap-in has no frame-authored audio.")]
        public UnityEngine.Object spawnSound;

        [Tooltip("Optional SoundEventSO used when swap-in has no frame-authored audio. Falls back to spawnSound when unset.")]
        public UnityEngine.Object swapInSound;

        [Tooltip("Optional SoundEventSO used when melee attack has no frame-authored audio.")]
        public UnityEngine.Object meleeAttackFallbackSound;

        [Tooltip("Optional SoundEventSO used when ranged attack has no frame-authored audio.")]
        public UnityEngine.Object rangedAttackFallbackSound;

        [Tooltip("Optional SoundEventSO used when hit reaction has no frame-authored audio.")]
        public UnityEngine.Object hitReactionSound;

        [Tooltip("Optional SoundEventSO used when KO/faint has no frame-authored audio.")]
        public UnityEngine.Object koSound;

        [Header("UI")]
        [Tooltip("Portrait sprite for UI panels (HUD, team select, etc.). Optional.")]
        public Sprite portrait;

        [Header("Action Outline")]
        public bool useCustomActionOutlineColor;
        public Color actionOutlineColor = new Color(0.15f, 0.8f, 1f, 0.8f);

        public Vector3 GetAnchorOffset(PresentationAnchorTarget anchorTarget, bool flipX)
        {
            if (anchorOffsets == null)
                return Vector3.zero;

            for (int i = 0; i < anchorOffsets.Count; i++)
            {
                if (anchorOffsets[i].anchorTarget != anchorTarget)
                    continue;

                Vector3 offset = anchorOffsets[i].localOffset;
                if (flipX)
                    offset.x = -offset.x;

                return offset;
            }

            return Vector3.zero;
        }

        public bool TryGetAnimationAlternate(ChainlingAnimSlot slot, string styleId, out CombatVisualAnimationAlternate alternate)
        {
            string normalizedStyleId = ChainlingVisualIdentity.NormalizeId(styleId, ChainlingVisualIdentity.NoneId);
            if (string.Equals(normalizedStyleId, ChainlingVisualIdentity.NoneId, StringComparison.Ordinal) ||
                string.Equals(normalizedStyleId, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal))
            {
                alternate = default;
                return false;
            }

            if (animationAlternates == null)
            {
                alternate = default;
                return false;
            }

            for (int i = 0; i < animationAlternates.Count; i++)
            {
                CombatVisualAnimationAlternate candidate = animationAlternates[i];
                if (candidate.slot != slot || !candidate.HasSequence)
                    continue;

                if (string.Equals(candidate.StyleId, normalizedStyleId, StringComparison.Ordinal))
                {
                    alternate = candidate;
                    return true;
                }
            }

            alternate = default;
            return false;
        }
    }

    [Serializable]
    public struct CombatPresentationAnchorOffset
    {
        public PresentationAnchorTarget anchorTarget;
        public Vector3 localOffset;
    }

    [Serializable]
    public struct CombatVisualAnimationAlternate
    {
        public ChainlingAnimSlot slot;
        [Tooltip("Equipped cosmetic/style ID this profile-specific alternate satisfies.")]
        public string styleId;
        public SpriteAnimationSequence sequence;
        [Min(0.01f)] public float fps;
        [Tooltip("-1 uses the visual/presenter fallback marker. Non-negative values override attack marker timing.")]
        public int markerFrame;

        public string StyleId => ChainlingVisualIdentity.NormalizeId(styleId, ChainlingVisualIdentity.NoneId);
        public bool HasSequence => sequence != null && sequence.Length > 0;
        public float ResolvedFps => fps > 0f ? fps : 1f;
    }
}
