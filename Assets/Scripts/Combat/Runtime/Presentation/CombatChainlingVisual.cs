using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chainbound.Combat
{
    /// <summary>
    /// Combat-specific sprite renderer for Chainling battle visuals.
    /// Uses a flat quad mesh with depth-writing material, same rendering technique
    /// as MeshRendererPlayerVisual but stripped down for the combat scene.
    ///
    /// Combat Chainlings:
    /// - Face a fixed direction (mirrored via UV flip for opposing team)
    /// - Play simple combat animations: Idle, MeleeAttack, RangedAttack, Hit
    /// - Show damage flash (white tint) on hit
    /// - Report animation completion so the presenter can drive timing
    ///
    /// No directional sprites, no camera-relative positioning, no legacy mode.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class CombatChainlingVisual : MonoBehaviour
    {
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Animation States
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public enum CombatAnimState
        {
            Idle,
            MeleeAttack,
            RangedAttack,
            Hit,
            SwapIn,
            SwapOut,
            Temporary,
            [Obsolete("Use SwapIn.")]
            GlitchIn = SwapIn,
            [Obsolete("Use SwapOut.")]
            GlitchOut = SwapOut,
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Inspector Ã¢â‚¬â€ Sprite Sequences (one per state, no directions)
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Sprite Sequences")]
        [Tooltip("Idle loop (breathing/blinking). Played continuously when no action.")]
        [SerializeField] private SpriteAnimationSequence idleSequence = new SpriteAnimationSequence();

        [Tooltip("Melee attack one-shot. Fires OnAnimationComplete at end.")]
        [SerializeField] private SpriteAnimationSequence meleeAttackSequence = new SpriteAnimationSequence();

        [Tooltip("Ranged attack one-shot. Fires OnAnimationComplete at end.")]
        [SerializeField] private SpriteAnimationSequence rangedAttackSequence = new SpriteAnimationSequence();

        [Tooltip("Hit reaction one-shot. Fires OnAnimationComplete at end.")]
        [SerializeField] private SpriteAnimationSequence hitSequence = new SpriteAnimationSequence();

        [Tooltip("Optional swap-in one-shot used during entry.")]
        [FormerlySerializedAs("glitchInSequence")]
        [SerializeField] private SpriteAnimationSequence swapInSequence = new SpriteAnimationSequence();

        [Tooltip("Optional swap-out one-shot used during voluntary exit or KO fallback.")]
        [FormerlySerializedAs("glitchOutSequence")]
        [SerializeField] private SpriteAnimationSequence swapOutSequence = new SpriteAnimationSequence();

        [Header("Animation Speed (FPS)")]
        [SerializeField] private float idleFPS = 4f;
        [SerializeField] private float meleeAttackFPS = 12f;
        [SerializeField] private float rangedAttackFPS = 10f;
        [SerializeField] private float hitFPS = 8f;
        [FormerlySerializedAs("glitchInFPS")]
        [SerializeField] private float swapInFPS = 10f;
        [FormerlySerializedAs("glitchOutFPS")]
        [SerializeField] private float swapOutFPS = 10f;

        [Header("Combat Timing Markers")]
        [Tooltip("Frame index in MeleeAttack to signal contact. -1 disables marker.")]
        [SerializeField] private int meleeContactFrame = -1;
        [Tooltip("Frame index in RangedAttack to signal projectile release. -1 disables marker.")]
        [SerializeField] private int rangedReleaseFrame = -1;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Inspector Ã¢â‚¬â€ Rendering
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        [Header("Rendering")]
        [Tooltip("Material with depth-writing shader (e.g. Sprites/DepthWrite).")]
        [SerializeField] private Material depthWriteMaterial;

        [Tooltip("Size of the quad in world units. Default 5x5 matches the scene's x5 pixel scale.")]
        [SerializeField] private Vector2 spriteSize = new Vector2(5f, 5f);

        // The profile owns the baseline quad size. Encounter presentation may
        // temporarily scale that baseline without mutating the shared profile.
        private Vector2 _profileSpriteSize = new Vector2(5f, 5f);
        private float _presentationScaleMultiplier = 1f;

        [Tooltip("When true, rebuilds the quad around each sprite's imported pivot so stance pivots align to the slot origin.")]
        [SerializeField] private bool useSpritePivot = true;

        [Tooltip("Optional local-space nudge applied after sprite pivot alignment. X/Y are billboard-local world units.")]
        [SerializeField] private Vector2 spritePivotWorldOffset = Vector2.zero;

        [Header("Team Mirroring")]
        [Tooltip("Flip the sprite horizontally (set true for the team facing left).")]
        [SerializeField] private bool flipX = false;

        [Header("Damage Flash")]
        [Tooltip("Color to tint the sprite during a damage flash.")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("Duration of the damage flash in seconds.")]
        [SerializeField] private float flashDuration = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Runtime State
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Mesh _quadMesh;
        private readonly Vector3[] _quadVertices = new Vector3[4];
        private MaterialPropertyBlock _propBlock;
        private Sprite _lastQuadSprite;
        private Vector2 _lastQuadSize;
        private Vector2 _lastQuadOffset;
        private bool _lastQuadUseSpritePivot;
        private bool _lastQuadFlipX;

        private CombatActionOutlineTuningSO _outlineTuning;
        private Color _outlineColor = new Color(0.15f, 0.8f, 1f, 0.8f);
        private float _outlineWidthPixels;
        private float _outlineSoftness = 0.5f;
        private float _outlineIntensity;
        private float _outlineTargetIntensity;
        private float _outlineFadeSpeed;
        private bool _outlineSelectionHeld;
        private bool _restoreOutlineDefaultsAfterFade;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int MainTexTexelSizeId = Shader.PropertyToID("_MainTex_TexelSize");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineSoftnessId = Shader.PropertyToID("_OutlineSoftness");
        private static readonly int OutlineIntensityId = Shader.PropertyToID("_OutlineIntensity");

        private CombatAnimState _currentState = CombatAnimState.Idle;
        private float _animTimer;
        private int _currentFrame;
        private int _lastEvaluatedFrame;
        private bool _isOneShot;
        private bool _oneShotComplete;
        private bool _meleeContactMarkerFired;
        private bool _rangedReleaseMarkerFired;
        private int _lastFrameEventFrame = -1;
        private int _lastLoopIndex;
        private CombatVisualProfileSO _currentProfile;
        private SpriteAnimationSequence _temporarySequence;
        private float _temporaryFPS = 8f;
        private bool _returnToIdleAfterTemporary;
        private BodyAnimationOverride _idleOverride;
        private BodyAnimationOverride _meleeAttackOverride;
        private BodyAnimationOverride _rangedAttackOverride;
        private BodyAnimationOverride _hitOverride;
        private BodyAnimationOverride _swapInOverride;
        private BodyAnimationOverride _swapOutOverride;

        private float _flashTimer;
        private bool _isFlashing;
        private bool _frozen;

        private sealed class BodyAnimationOverride
        {
            public SpriteAnimationSequence sequence;
            public float fps;
            public int markerFrame;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Events
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        /// <summary>
        /// Fired when a one-shot animation (attack, hit) reaches its last frame.
        /// The caller is responsible for transitioning back to Idle if desired.
        /// </summary>
        public event Action OnAnimationComplete;

        /// <summary>Fired when the melee contact marker frame is reached.</summary>
        public event Action OnMeleeContact;

        /// <summary>Fired when the ranged release marker frame is reached.</summary>
        public event Action OnRangedRelease;

        /// <summary>Fired when a one-shot animation advances across one or more frames.</summary>
        public event Action<CombatAnimState, int> OnFrameAdvanced;

        /// <summary>Fired whenever a looping animation wraps back to its first frame.</summary>
        public event Action<CombatAnimState> OnLoopCompleted;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Public Accessors
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public CombatAnimState CurrentState => _currentState;
        public int CurrentFrame => _currentFrame;

        /// <summary>True while a one-shot animation is still playing.</summary>
        public bool IsAnimating => _isOneShot && !_oneShotComplete;

        public bool FlipX => flipX;
        public Vector2 SpriteSize => spriteSize;
        public float PresentationScaleMultiplier => _presentationScaleMultiplier;
        public CombatVisualProfileSO CurrentProfile => _currentProfile;
        public bool HasSwapInSequence => HasConfiguredSequence(CombatAnimState.SwapIn);
        public bool HasSwapOutSequence => HasConfiguredSequence(CombatAnimState.SwapOut);
        public bool CanPlayTemporarySequence => !_frozen && (!_isOneShot || _oneShotComplete) && _currentState == CombatAnimState.Idle;

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Unity Lifecycle
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void Awake()
        {
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            _propBlock = new MaterialPropertyBlock();

            RebuildQuadMesh(null);
            ApplyDefaultOutlineSettings();

            if (depthWriteMaterial == null)
            {
                Debug.LogWarning("[CombatChainlingVisual] No material assigned — creating default.");
                depthWriteMaterial = new Material(Shader.Find("Sprites/DepthWrite"));
            }

            _meshRenderer.material = depthWriteMaterial;

            if (_meshRenderer.material.renderQueue != 2450)
                _meshRenderer.material.renderQueue = 2450;
        }

        private void OnEnable()
        {
            ApplyTexture();
        }

        private void OnDisable()
        {
            StopOutlineFadeRoutine();
            _outlineTargetIntensity = 0f;
            _outlineIntensity = 0f;
            _outlineWidthPixels = 0f;
            ApplyTexture();
        }

        private void ApplyDefaultOutlineSettings()
        {
            _outlineColor = new Color(0.15f, 0.8f, 1f, 0.8f);
            _outlineWidthPixels = 0f;
            _outlineSoftness = 0.5f;
            _outlineIntensity = 0f;
            _outlineTargetIntensity = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void StepPlayback(float deltaTime)
        {
            Tick(deltaTime);
        }

        public void SampleFrame(CombatAnimState state, int frame, bool oneShot)
        {
            _currentState = state;
            _isOneShot = oneShot;
            _oneShotComplete = false;
            _meleeContactMarkerFired = false;
            _rangedReleaseMarkerFired = false;

            int frameCount = GetFrameCount(state);
            _currentFrame = frameCount > 0 ? Mathf.Clamp(frame, 0, frameCount - 1) : 0;
            float fps = Mathf.Max(0.01f, GetFramesPerSecond(state));
            _animTimer = _currentFrame / fps;
            _lastEvaluatedFrame = _currentFrame;
            _lastFrameEventFrame = _currentFrame;
            _lastLoopIndex = 0;
            ApplyTexture();
        }

        public void EmitCurrentFrameSignals()
        {
            if (!_isOneShot)
                return;

            _lastEvaluatedFrame = _currentFrame - 1;
            _lastFrameEventFrame = _currentFrame - 1;
            TryEmitAnimationMarkers(forceCurrentFrame: false);
            TryEmitFrameAdvanced();

            if (_currentFrame >= GetFrameCount(_currentState) - 1 && !_oneShotComplete)
            {
                _oneShotComplete = true;

                if (enableDebugLogs)
                    Debug.Log($"[CombatChainlingVisual] Animation complete: {_currentState}");

                OnAnimationComplete?.Invoke();
                FinishTemporarySequenceIfNeeded();
            }
        }

        private void FinishTemporarySequenceIfNeeded()
        {
            if (_currentState == CombatAnimState.Temporary && _returnToIdleAfterTemporary)
                PlayLoop(CombatAnimState.Idle);
        }

        private void Tick(float deltaTime)
        {
            UpdateAnimation(deltaTime);
            UpdateFlash(deltaTime);
            UpdateOutlineFade(deltaTime);
            ApplyTexture();
        }

        private void UpdateOutlineFade(float deltaTime)
        {
            if (Mathf.Approximately(_outlineIntensity, _outlineTargetIntensity))
                return;

            if (_outlineFadeSpeed <= 0f)
            {
                _outlineIntensity = _outlineTargetIntensity;
                return;
            }

            _outlineIntensity = Mathf.MoveTowards(_outlineIntensity, _outlineTargetIntensity, _outlineFadeSpeed * deltaTime);

            if (_outlineTargetIntensity <= 0f && _outlineIntensity <= 0f)
            {
                _outlineWidthPixels = 0f;
                if (_restoreOutlineDefaultsAfterFade)
                {
                    _restoreOutlineDefaultsAfterFade = false;
                    ApplyOutlineColorFromProfile();
                }
            }
            else if (_outlineTargetIntensity > 0f && _outlineWidthPixels <= 0f)
                _outlineWidthPixels = ResolveOutlineWidthPixels();
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Quad Mesh
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void RebuildQuadMesh(Sprite pivotSprite)
        {
            if (_quadMesh == null)
                _quadMesh = new Mesh { name = "CombatChainlingQuad" };
            else
                _quadMesh.Clear();

            ResolveQuadExtents(pivotSprite, out float left, out float right, out float bottom, out float top);

            _quadVertices[0] = new Vector3(left, bottom, 0f);
            _quadVertices[1] = new Vector3(right, bottom, 0f);
            _quadVertices[2] = new Vector3(left, top, 0f);
            _quadVertices[3] = new Vector3(right, top, 0f);

            _quadMesh.vertices = _quadVertices;

            // Standard 0-1 UVs Ã¢â‚¬â€ flipping is handled in _MainTex_ST
            _quadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };

            _quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quadMesh.RecalculateNormals();
            _quadMesh.RecalculateBounds();

            if (_meshFilter != null)
                _meshFilter.mesh = _quadMesh;

            _lastQuadSprite = pivotSprite;
            _lastQuadSize = spriteSize;
            _lastQuadOffset = spritePivotWorldOffset;
            _lastQuadUseSpritePivot = useSpritePivot;
            _lastQuadFlipX = flipX;
        }

        private void ResolveQuadExtents(Sprite pivotSprite, out float left, out float right, out float bottom, out float top)
        {
            float width = Mathf.Max(0.0001f, spriteSize.x);
            float height = Mathf.Max(0.0001f, spriteSize.y);
            Vector2 normalizedPivot = new Vector2(0.5f, 0.5f);

            if (useSpritePivot && TryGetNormalizedPivot(pivotSprite, out Vector2 spritePivot))
                normalizedPivot = spritePivot;

            left = -normalizedPivot.x * width + spritePivotWorldOffset.x;
            right = (1f - normalizedPivot.x) * width + spritePivotWorldOffset.x;
            bottom = -normalizedPivot.y * height + spritePivotWorldOffset.y;
            top = (1f - normalizedPivot.y) * height + spritePivotWorldOffset.y;
        }

        private bool TryGetNormalizedPivot(Sprite sprite, out Vector2 normalizedPivot)
        {
            normalizedPivot = new Vector2(0.5f, 0.5f);
            if (sprite == null)
                return false;

            Rect rect = sprite.rect;
            float width = rect.width;
            float height = rect.height;
            if (width <= 0f || height <= 0f)
            {
                Rect textureRect = sprite.textureRect;
                width = textureRect.width;
                height = textureRect.height;
            }

            if (width <= 0f || height <= 0f)
                return false;

            normalizedPivot = new Vector2(
                Mathf.Clamp01(sprite.pivot.x / width),
                Mathf.Clamp01(sprite.pivot.y / height));

            if (flipX)
                normalizedPivot.x = 1f - normalizedPivot.x;

            return true;
        }

        private void EnsureQuadMeshForSprite(Sprite sprite)
        {
            if (_quadMesh == null ||
                _lastQuadSprite != sprite ||
                _lastQuadSize != spriteSize ||
                _lastQuadOffset != spritePivotWorldOffset ||
                _lastQuadUseSpritePivot != useSpritePivot ||
                _lastQuadFlipX != flipX)
            {
                RebuildQuadMesh(sprite);
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Animation Tick
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void UpdateAnimation(float deltaTime)
        {
            var seq = GetSequence(_currentState);
            int len = seq != null ? seq.Length : 0;

            if (_frozen)
            {
                if (len <= 0)
                {
                    _currentFrame = 0;
                    _animTimer = 0f;
                }

                return;
            }

            if (len <= 1)
            {
                _currentFrame = 0;
                _animTimer = 0f;
                TryEmitAnimationMarkers(forceCurrentFrame: true);
                TryEmitFrameAdvanced();

                if (_isOneShot && !_oneShotComplete)
                {
                    _oneShotComplete = true;

                    if (enableDebugLogs)
                        Debug.Log($"[CombatChainlingVisual] Animation complete immediately: {_currentState}");

                    OnAnimationComplete?.Invoke();
                    FinishTemporarySequenceIfNeeded();
                }

                return;
            }

            float fps = Mathf.Max(0.01f, GetFPS(_currentState));
            _animTimer += deltaTime;
            int frame = Mathf.FloorToInt(_animTimer * fps);

            if (_isOneShot)
            {
                if (frame >= len - 1)
                {
                    _currentFrame = len - 1;
                    TryEmitAnimationMarkers(forceCurrentFrame: false);
                    TryEmitFrameAdvanced();

                    if (!_oneShotComplete)
                    {
                        _oneShotComplete = true;

                        if (enableDebugLogs)
                            Debug.Log($"[CombatChainlingVisual] Animation complete: {_currentState}");

                        OnAnimationComplete?.Invoke();
                        FinishTemporarySequenceIfNeeded();
                    }
                }
                else
                {
                    _currentFrame = frame;
                    TryEmitAnimationMarkers(forceCurrentFrame: false);
                    TryEmitFrameAdvanced();
                }
            }
            else
            {
                int loopIndex = frame / len;
                _currentFrame = frame % len;
                if (loopIndex > _lastLoopIndex)
                    OnLoopCompleted?.Invoke(_currentState);
                _lastLoopIndex = loopIndex;
            }
        }

        private void TryEmitAnimationMarkers(bool forceCurrentFrame)
        {
            if (!_isOneShot)
                return;

            int meleeMarkerFrame = GetMarkerFrame(CombatAnimState.MeleeAttack);
            if (_currentState == CombatAnimState.MeleeAttack &&
                !_meleeContactMarkerFired &&
                meleeMarkerFrame >= 0 &&
                HasReachedMarkerFrame(meleeMarkerFrame, forceCurrentFrame))
            {
                _meleeContactMarkerFired = true;
                OnMeleeContact?.Invoke();
            }

            int rangedMarkerFrame = GetMarkerFrame(CombatAnimState.RangedAttack);
            if (_currentState == CombatAnimState.RangedAttack &&
                !_rangedReleaseMarkerFired &&
                rangedMarkerFrame >= 0 &&
                HasReachedMarkerFrame(rangedMarkerFrame, forceCurrentFrame))
            {
                _rangedReleaseMarkerFired = true;
                OnRangedRelease?.Invoke();
            }

            _lastEvaluatedFrame = _currentFrame;
        }

        private void TryEmitFrameAdvanced()
        {
            if (!_isOneShot)
                return;

            int startFrame = Mathf.Max(0, _lastFrameEventFrame + 1);
            for (int frame = startFrame; frame <= _currentFrame; frame++)
            {
                _lastFrameEventFrame = frame;
                OnFrameAdvanced?.Invoke(_currentState, frame);
            }
        }

        private bool HasReachedMarkerFrame(int markerFrame, bool forceCurrentFrame)
        {
            if (forceCurrentFrame)
                return _currentFrame >= markerFrame;

            if (_lastEvaluatedFrame < 0)
                return _currentFrame >= markerFrame;

            return _lastEvaluatedFrame < markerFrame && _currentFrame >= markerFrame;
        }

        private void UpdateFlash(float deltaTime)
        {
            if (!_isFlashing)
                return;

            _flashTimer -= deltaTime;
            if (_flashTimer <= 0f)
                _isFlashing = false;
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Texture Application
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private void ApplyTexture()
        {
            var seq = GetSequence(_currentState);
            if (seq == null || seq.Length == 0)
                return;

            int frameIdx = Mathf.Clamp(_currentFrame, 0, seq.Length - 1);
            Sprite sprite = seq[frameIdx];
            if (sprite == null || sprite.texture == null)
                return;

            EnsureQuadMeshForSprite(sprite);

            _meshRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(MainTexId, sprite.texture);

            // Sprite atlas sub-rect
            Rect r = sprite.textureRect;
            float tw = sprite.texture.width;
            float th = sprite.texture.height;

            float uOffset = r.x / tw;
            float vOffset = r.y / th;
            float uScale = r.width / tw;
            float vScale = r.height / th;

            // X-flip via UV: shift offset to the right edge and negate scale
            if (flipX)
            {
                uOffset = uOffset + uScale;
                uScale = -uScale;
            }

            _propBlock.SetVector(MainTexStId, new Vector4(uScale, vScale, uOffset, vOffset));
            _propBlock.SetVector(MainTexTexelSizeId, new Vector4(1f / tw, 1f / th, tw, th));

            // Tint: flash color or material default
            if (depthWriteMaterial != null && depthWriteMaterial.HasProperty("_Color"))
            {
                _propBlock.SetColor(ColorId,
                    _isFlashing ? flashColor : depthWriteMaterial.GetColor("_Color"));
            }

            if (depthWriteMaterial != null && depthWriteMaterial.HasProperty("_Cutoff"))
                _propBlock.SetFloat(CutoffId, depthWriteMaterial.GetFloat("_Cutoff"));

            ApplyOutlineToPropertyBlock();

            _meshRenderer.SetPropertyBlock(_propBlock);
        }

        private void ApplyOutlineToPropertyBlock()
        {
            bool outlineActive = _outlineIntensity > 0.001f && _outlineWidthPixels > 0.001f;
            _propBlock.SetFloat(OutlineWidthId, outlineActive ? _outlineWidthPixels : 0f);
            _propBlock.SetColor(OutlineColorId, _outlineColor);
            _propBlock.SetFloat(OutlineSoftnessId, _outlineSoftness);
            _propBlock.SetFloat(OutlineIntensityId, outlineActive ? _outlineIntensity : 0f);
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Sequence / FPS Lookup
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        private SpriteAnimationSequence GetSequence(CombatAnimState state)
        {
            SpriteAnimationSequence seq = GetConfiguredSequence(state);

            // Fallback to idle if the requested state has no sprites
            if ((seq == null || seq.Length == 0) && state != CombatAnimState.Idle)
                seq = GetConfiguredSequence(CombatAnimState.Idle);

            return seq;
        }

        private SpriteAnimationSequence GetConfiguredSequence(CombatAnimState state)
        {
            BodyAnimationOverride bodyOverride = GetBodyOverride(state);
            if (bodyOverride != null && bodyOverride.sequence != null && bodyOverride.sequence.Length > 0)
                return bodyOverride.sequence;

            switch (state)
            {
                case CombatAnimState.Idle:         return idleSequence;
                case CombatAnimState.MeleeAttack:  return meleeAttackSequence;
                case CombatAnimState.RangedAttack: return rangedAttackSequence;
                case CombatAnimState.Hit:          return hitSequence;
                case CombatAnimState.SwapIn:       return swapInSequence;
                case CombatAnimState.SwapOut:      return swapOutSequence;
                case CombatAnimState.Temporary:    return _temporarySequence;
                default:                            return idleSequence;
            }
        }

        private float GetFPS(CombatAnimState state)
        {
            BodyAnimationOverride bodyOverride = GetBodyOverride(state);
            if (bodyOverride != null)
                return Mathf.Max(0.01f, bodyOverride.fps);

            switch (state)
            {
                case CombatAnimState.Idle:         return idleFPS;
                case CombatAnimState.MeleeAttack:  return meleeAttackFPS;
                case CombatAnimState.RangedAttack: return rangedAttackFPS;
                case CombatAnimState.Hit:          return hitFPS;
                case CombatAnimState.SwapIn:       return swapInFPS;
                case CombatAnimState.SwapOut:      return swapOutFPS;
                case CombatAnimState.Temporary:    return _temporaryFPS;
                default:                            return idleFPS;
            }
        }

        private bool HasConfiguredSequence(CombatAnimState state)
        {
            var seq = GetConfiguredSequence(state);
            return seq != null && seq.Length > 0;
        }

        private BodyAnimationOverride GetBodyOverride(CombatAnimState state)
        {
            switch (state)
            {
                case CombatAnimState.Idle:         return _idleOverride;
                case CombatAnimState.MeleeAttack:  return _meleeAttackOverride;
                case CombatAnimState.RangedAttack: return _rangedAttackOverride;
                case CombatAnimState.Hit:          return _hitOverride;
                case CombatAnimState.SwapIn:       return _swapInOverride;
                case CombatAnimState.SwapOut:      return _swapOutOverride;
                default:                            return null;
            }
        }

        private void SetBodyOverrideInternal(CombatAnimState state, BodyAnimationOverride bodyOverride)
        {
            switch (state)
            {
                case CombatAnimState.Idle:
                    _idleOverride = bodyOverride;
                    break;
                case CombatAnimState.MeleeAttack:
                    _meleeAttackOverride = bodyOverride;
                    break;
                case CombatAnimState.RangedAttack:
                    _rangedAttackOverride = bodyOverride;
                    break;
                case CombatAnimState.Hit:
                    _hitOverride = bodyOverride;
                    break;
                case CombatAnimState.SwapIn:
                    _swapInOverride = bodyOverride;
                    break;
                case CombatAnimState.SwapOut:
                    _swapOutOverride = bodyOverride;
                    break;
            }
        }

        private int GetMarkerFrame(CombatAnimState state)
        {
            BodyAnimationOverride bodyOverride = GetBodyOverride(state);
            if (bodyOverride != null)
                return bodyOverride.markerFrame;

            switch (state)
            {
                case CombatAnimState.MeleeAttack:  return meleeContactFrame;
                case CombatAnimState.RangedAttack: return rangedReleaseFrame;
                default:                            return -1;
            }
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Public API
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        public int GetFrameCount(CombatAnimState state)
        {
            var seq = GetSequence(state);
            return seq != null ? Mathf.Max(0, seq.Length) : 0;
        }

        public float GetFramesPerSecond(CombatAnimState state)
        {
            return GetFPS(state);
        }

        public Sprite GetFrameSprite(CombatAnimState state, int frame)
        {
            var seq = GetSequence(state);
            if (seq == null || seq.Length == 0)
                return null;

            frame = Mathf.Clamp(frame, 0, seq.Length - 1);
            return seq[frame];
        }

        public bool TryGetMarkerFrame(CombatAnimState state, out int frame)
        {
            frame = GetMarkerFrame(state);

            int frameCount = GetFrameCount(state);
            return frame >= 0 && frame < frameCount;
        }

        /// <summary>
        /// Play a looping animation (typically Idle).
        /// Does not fire OnAnimationComplete.
        /// </summary>
        public void PlayLoop(CombatAnimState state)
        {
            if (_currentState == state && !_isOneShot)
                return;

            if (state != CombatAnimState.Temporary)
                ClearTemporarySequence();

            _currentState = state;
            _animTimer = 0f;
            _currentFrame = 0;
            _lastEvaluatedFrame = -1;
            _lastFrameEventFrame = -1;
            _lastLoopIndex = 0;
            _isOneShot = false;
            _oneShotComplete = false;
            _meleeContactMarkerFired = false;
            _rangedReleaseMarkerFired = false;

            if (enableDebugLogs)
                Debug.Log($"[CombatChainlingVisual] PlayLoop: {state}");
        }

        public void SetBodyOverride(CombatAnimState state, SpriteAnimationSequence sequence, float fps, int markerFrame = -1)
        {
            if (!CanOverrideState(state) || sequence == null || sequence.Length == 0)
            {
                ClearBodyOverride(state);
                return;
            }

            SetBodyOverrideInternal(state, new BodyAnimationOverride
            {
                sequence = sequence,
                fps = Mathf.Max(0.01f, fps),
                markerFrame = markerFrame
            });
        }

        public void ClearBodyOverride(CombatAnimState state)
        {
            if (CanOverrideState(state))
                SetBodyOverrideInternal(state, null);
        }

        public void ClearBodyOverrides()
        {
            _meleeAttackOverride = null;
            _rangedAttackOverride = null;
            _hitOverride = null;
            _swapInOverride = null;
            _swapOutOverride = null;
            _idleOverride = null;
        }

        private static bool CanOverrideState(CombatAnimState state)
        {
            return state == CombatAnimState.Idle ||
                   state == CombatAnimState.MeleeAttack ||
                   state == CombatAnimState.RangedAttack ||
                   state == CombatAnimState.Hit ||
                   state == CombatAnimState.SwapIn ||
                   state == CombatAnimState.SwapOut;
        }

        /// <summary>
        /// Play a one-shot animation (MeleeAttack, RangedAttack, Hit).
        /// Fires OnAnimationComplete when the last frame is reached.
        /// Does NOT auto-return to Idle Ã¢â‚¬â€ the caller decides.
        /// </summary>
        public void PlayOneShot(CombatAnimState state)
        {
            if (state != CombatAnimState.Temporary)
                ClearTemporarySequence();

            _currentState = state;
            _animTimer = 0f;
            _currentFrame = 0;
            _lastEvaluatedFrame = -1;
            _lastFrameEventFrame = -1;
            _lastLoopIndex = 0;
            _isOneShot = true;
            _oneShotComplete = false;
            _meleeContactMarkerFired = false;
            _rangedReleaseMarkerFired = false;

            if (enableDebugLogs)
                Debug.Log($"[CombatChainlingVisual] PlayOneShot: {state}");
        }

        public float PlayTemporarySequence(SpriteAnimationSequence sequence, float fps, bool returnToIdle = true)
        {
            if (sequence == null || sequence.Length == 0)
                return 0f;

            _temporarySequence = sequence;
            _temporaryFPS = Mathf.Max(0.01f, fps);
            _returnToIdleAfterTemporary = returnToIdle;

            _currentState = CombatAnimState.Temporary;
            _animTimer = 0f;
            _currentFrame = 0;
            _lastEvaluatedFrame = -1;
            _lastFrameEventFrame = -1;
            _lastLoopIndex = 0;
            _isOneShot = true;
            _oneShotComplete = false;
            _meleeContactMarkerFired = false;
            _rangedReleaseMarkerFired = false;

            return GetAnimationDuration(CombatAnimState.Temporary);
        }

        private void ClearTemporarySequence()
        {
            _temporarySequence = null;
            _temporaryFPS = 8f;
            _returnToIdleAfterTemporary = false;
        }

        /// <summary>
        /// Duration (seconds) of a one-shot animation from first to last frame.
        /// Returns 0 for empty/single-frame sequences.
        /// </summary>
        public float GetAnimationDuration(CombatAnimState state)
        {
            var seq = GetSequence(state);
            int len = seq != null ? seq.Length : 0;
            if (len <= 1) return 0f;

            float fps = GetFPS(state);
            return fps > 0f ? (float)(len - 1) / fps : 0f;
        }

        /// <summary>
        /// Returns marker time in seconds for the requested one-shot animation.
        /// If marker is disabled or invalid, returns a conservative midpoint fallback.
        /// </summary>
        public float GetMarkerTimeOrDefault(CombatAnimState state)
        {
            int marker = GetMarkerFrame(state);

            var seq = GetSequence(state);
            int len = seq != null ? seq.Length : 0;
            float fps = GetFPS(state);
            if (len <= 0 || fps <= 0f)
                return 0f;

            if (marker < 0 || marker >= len)
                return GetAnimationDuration(state) * 0.5f;

            return marker / fps;
        }

        /// <summary>
        /// Trigger a damage flash (brief tint to flashColor).
        /// </summary>
        public void Flash()
        {
            _isFlashing = true;
            _flashTimer = flashDuration;
        }

        /// <summary>
        /// Set team-side mirroring at runtime.
        /// </summary>
        public void SetFlipX(bool flip)
        {
            flipX = flip;
            EnsureQuadMeshForSprite(GetFrameSprite(_currentState, _currentFrame));
        }

        /// <summary>
        /// Set sprite size at runtime and rebuild the quad.
        /// </summary>
        public void SetSpriteSize(Vector2 size)
        {
            spriteSize = size;
            RebuildQuadMesh(GetFrameSprite(_currentState, _currentFrame));
        }

        /// <summary>
        /// Applies an encounter-scoped presentation multiplier to the current
        /// profile's baseline quad size. This changes only this visual instance;
        /// it never writes back to the shared CombatVisualProfileSO.
        /// </summary>
        public void SetPresentationScaleMultiplier(float multiplier)
        {
            multiplier = Mathf.Max(0.01f, multiplier);
            if (Mathf.Approximately(_presentationScaleMultiplier, multiplier))
                return;

            _presentationScaleMultiplier = multiplier;
            SetSpriteSize(_profileSpriteSize * multiplier);
        }

        public void ConfigureOutlineTuning(CombatActionOutlineTuningSO tuning)
        {
            _outlineTuning = tuning;
            ApplyOutlineColorFromProfile();
        }

        public void SetOutlineEnabled(bool enabled)
        {
            StopOutlineFadeRoutine();
            _outlineSelectionHeld = enabled;

            if (enabled)
            {
                _outlineWidthPixels = ResolveOutlineWidthPixels();
                _outlineTargetIntensity = 1f;
                float fadeIn = _outlineTuning != null ? _outlineTuning.fadeInSeconds : 0.08f;
                _outlineFadeSpeed = fadeIn > 0f ? 1f / fadeIn : float.MaxValue;
            }
            else
            {
                _outlineTargetIntensity = 0f;
                float fadeOut = _outlineTuning != null ? _outlineTuning.fadeOutSeconds : 0.11f;
                _outlineFadeSpeed = fadeOut > 0f ? 1f / fadeOut : float.MaxValue;
            }
        }

        /// <summary>
        /// Temporarily overrides outline color/width for AP/status feedback, then restores profile defaults.
        /// </summary>
        public void BeginTemporaryOutlineFlash(Color color, float widthPixels, float softness)
        {
            _outlineColor = color;
            _outlineSoftness = softness;
            _outlineWidthPixels = Mathf.Max(0f, widthPixels);
            SetOutlineEnabled(true);
        }

        public void RestoreOutlineAfterFlash(bool keepSelectionOutline)
        {
            if (keepSelectionOutline)
            {
                _restoreOutlineDefaultsAfterFade = false;
                ApplyOutlineColorFromProfile();
                SetOutlineEnabled(true);
                return;
            }

            // Fade out in the flash color; restore selection defaults once invisible.
            _restoreOutlineDefaultsAfterFade = true;
            SetOutlineEnabled(false);
        }

        public void ApplySelectionOutlineDefaults()
        {
            _restoreOutlineDefaultsAfterFade = false;
            ApplyOutlineColorFromProfile();
        }

        public bool IsOutlineSelectionHeld => _outlineSelectionHeld;

        private void StopOutlineFadeRoutine()
        {
            _outlineFadeSpeed = 0f;
        }

        private float ResolveOutlineWidthPixels()
        {
            if (_outlineTuning != null)
                return Mathf.Max(0f, _outlineTuning.defaultOutlineWidthPixels);

            return 4f;
        }

        private void ApplyOutlineColorFromProfile()
        {
            if (_currentProfile != null && _currentProfile.useCustomActionOutlineColor)
            {
                _outlineColor = _currentProfile.actionOutlineColor;
                return;
            }

            _outlineColor = _outlineTuning != null
                ? _outlineTuning.defaultOutlineColor
                : new Color(0.15f, 0.8f, 1f, 0.8f);

            _outlineSoftness = _outlineTuning != null
                ? _outlineTuning.defaultOutlineSoftness
                : 0.5f;
        }

        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;

            if (enableDebugLogs)
                Debug.Log($"[CombatChainlingVisual] SetFrozen: {frozen}");
        }

        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â
        // Profile Application
        // Ã¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢ÂÃ¢â€¢Â

        /// <summary>
        /// Apply a <see cref="CombatVisualProfileSO"/> at runtime, swapping all
        /// sprite sequences, FPS values, and quad size to match a new species.
        /// Automatically restarts the Idle loop with the new sprites.
        /// </summary>
        public void ApplyProfile(CombatVisualProfileSO profile)
        {
            if (profile == null)
                return;

            _currentProfile = profile;
            ClearBodyOverrides();

            _profileSpriteSize = profile.spriteSize;
            _presentationScaleMultiplier = 1f;

            // Sprite sequences
            idleSequence = profile.idleSequence ?? new SpriteAnimationSequence();
            meleeAttackSequence = profile.meleeAttackSequence ?? new SpriteAnimationSequence();
            rangedAttackSequence = profile.rangedAttackSequence ?? new SpriteAnimationSequence();
            hitSequence = profile.hitSequence ?? new SpriteAnimationSequence();
            swapInSequence = profile.swapInSequence ?? new SpriteAnimationSequence();
            swapOutSequence = profile.swapOutSequence ?? new SpriteAnimationSequence();

            // FPS
            idleFPS = profile.idleFPS;
            meleeAttackFPS = profile.meleeAttackFPS;
            rangedAttackFPS = profile.rangedAttackFPS;
            hitFPS = profile.hitFPS;
            swapInFPS = profile.swapInFPS;
            swapOutFPS = profile.swapOutFPS;

            // Quad size (rebuilds mesh if different)
            if (spriteSize != _profileSpriteSize)
                SetSpriteSize(_profileSpriteSize);

            useSpritePivot = profile.useSpritePivot;
            spritePivotWorldOffset = profile.spritePivotWorldOffset;
            EnsureQuadMeshForSprite(GetFrameSprite(_currentState, _currentFrame));

            ApplyOutlineColorFromProfile();
            SetOutlineEnabled(false);

            _frozen = false;

            // Reset to idle with the new sprites
            PlayLoop(CombatAnimState.Idle);

            if (enableDebugLogs)
                Debug.Log($"[CombatChainlingVisual] ApplyProfile: {profile.displayName} ({profile.speciesId})");
        }
    }
}
