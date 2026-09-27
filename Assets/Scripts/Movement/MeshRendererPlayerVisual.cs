using System;
using UnityEngine;

/// <summary>
/// MeshRenderer-based player visual that supports sprite animations.
/// Uses a flat quad mesh that billboards to the camera, with depth-writing material.
/// Supports Cassette Beasts-style directional animations with per-state FPS.
/// 
/// This solves the SpriteRenderer depth-testing limitation by using MeshRenderer
/// which properly participates in the depth buffer.
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class MeshRendererPlayerVisual : MonoBehaviour
{
    // ============================================
    // DIRECTIONAL SPRITE SEQUENCES
    // Explicit per-direction assignment to avoid index confusion
    // ============================================
    
    [System.Serializable]
    public class DirectionalSpriteSequences
    {
        [Tooltip("Sprites for facing down (south, -Z in world space)")]
        public SpriteAnimationSequence down = new SpriteAnimationSequence();
        
        [Tooltip("Sprites for facing left (west, -X in world space)")]
        public SpriteAnimationSequence left = new SpriteAnimationSequence();
        
        [Tooltip("Sprites for facing right (east, +X in world space)")]
        public SpriteAnimationSequence right = new SpriteAnimationSequence();
        
        [Tooltip("Sprites for facing up (north, +Z in world space)")]
        public SpriteAnimationSequence up = new SpriteAnimationSequence();
        
        /// <summary>
        /// Get sprite sequence for a direction.
        /// </summary>
        public SpriteAnimationSequence GetSequence(Direction dir)
        {
            switch (dir)
            {
                case Direction.Down: return down;
                case Direction.Left: return left;
                case Direction.Right: return right;
                case Direction.Up: return up;
                default: return down;
            }
        }
        
        /// <summary>
        /// Check if any sprites are assigned.
        /// </summary>
        public bool HasSprites()
        {
            return (down != null && down.Length > 0) ||
                   (left != null && left.Length > 0) ||
                   (right != null && right.Length > 0) ||
                   (up != null && up.Length > 0);
        }
    }
    
    [Header("Directional Sprite Sequences")]
    [Tooltip("Idle animation sprites - slow tempo, directional. Can be multi-frame for breathing/blinking animations.")]
    [SerializeField] private DirectionalSpriteSequences idleSequences = new DirectionalSpriteSequences();

    [Tooltip("Alert animation sprites used during encounter transition lead-in. Falls back to idle when left unassigned.")]
    [SerializeField] private DirectionalSpriteSequences alertSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Walk animation sprites - medium tempo, directional. Typically 7-8 frames per direction.")]
    [SerializeField] private DirectionalSpriteSequences walkSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Squat animation sprites (holding jump button)")]
    [SerializeField] private DirectionalSpriteSequences squatSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Jump animation sprites (airborne)")]
    [SerializeField] private DirectionalSpriteSequences jumpSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Landing animation sprites (impact from fall)")]
    [SerializeField] private DirectionalSpriteSequences landingSequences = new DirectionalSpriteSequences();
    
    [Header("Animation Speed Settings")]
    [Tooltip("Frames per second for idle animation (slow tempo for breathing/blinking)")]
    [SerializeField] private float idleAnimationSpeed = 2f;
    
    [Tooltip("Frames per second for walk animation (medium tempo)")]
    [SerializeField] private float walkAnimationSpeed = 8f;

    [Tooltip("Frames per second for sprint. Uses the walk sprites at a faster tempo.")]
    [SerializeField] private float sprintAnimationSpeed = 12f;
    
    [Tooltip("Frames per second for squat animation (can be slow for anticipation)")]
    [SerializeField] private float squatAnimationSpeed = 4f;
    
    [Tooltip("Frames per second for jump animation (airborne)")]
    [SerializeField] private float jumpAnimationSpeed = 8f;
    
    [Tooltip("Frames per second for landing animation (impact)")]
    [SerializeField] private float landingAnimationSpeed = 10f;
    
    [Header("Rendering")]
    [Tooltip("Material with depth-writing shader (e.g., Sprites/DepthWrite)")]
    [SerializeField] private Material depthWriteMaterial;
    
    [Tooltip("Fallback quad size in world units when preserve aspect ratio is disabled.")]
    [SerializeField] private Vector2 spriteSize = new Vector2(1f, 1f);

    [Tooltip("If true, derives quad width from the current sprite aspect ratio. Defaults off so existing prefabs keep their current sizing.")]
    [SerializeField] private bool preserveAspectRatio = false;

    [Tooltip("World-space quad height used when preserve aspect ratio is enabled.")]
    [SerializeField] private float referenceHeight = 1f;
     
    [Tooltip("Forward offset toward camera to prevent clipping (world units). Set to 0 to disable.")]
    [SerializeField] private float forwardOffset = 0f;
    
    [Tooltip("Dynamic forward offset from PlayerVisualClipAvoidance (overrides forwardOffset when > 0)")]
    private float dynamicForwardOffset = 0f;
    
    [Header("Visual Position Offsets")]
    [Tooltip("Y position offset for the visual quad. With bottom-center anchor enabled, this is where the feet sit relative to the parent root.")]
    [SerializeField] private float yPositionOffset = 0f;

    [Tooltip("If true, the quad anchors from bottom-center so the feet sit at local Y = 0. Defaults off so existing prefabs keep their current pivot.")]
    [SerializeField] private bool anchorBottomCenter = false;
    
    [Tooltip("If true, uses per-camera-yaw XZ offsets. If false, uses single XZ offset.")]
    [SerializeField] private bool usePerYawXZOffsets = false;
    
    [Tooltip("Single XZ offset for visual position (used when usePerYawXZOffsets is false)")]
    [SerializeField] private Vector2 xzPositionOffset = Vector2.zero;
    
    [Tooltip("XZ offsets for visual position per camera yaw. Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] xzPositionOffsetsPerYaw = new Vector2[]
    {
        new Vector2(0f, 0f),   // 0°
        new Vector2(0f, 0f),   // 90°
        new Vector2(0f, 0f),   // 180°
        new Vector2(0f, 0f)    // 270°
    };

    [Header("Pitch Y Offset")]
    [Tooltip("Additive Y lift when camera pitch drops below the explore baseline.")]
    [SerializeField] private ExplorationVisualPitchYOffset.PitchYOffsetSettings pitchYOffsetSettings =
        ExplorationVisualPitchYOffset.PitchYOffsetSettings.CreateDefault(0.5f);
    
    // Current camera yaw index for per-yaw offsets
    private int currentCameraYawIndex = 0;
    
    [Header("References")]
    [Tooltip("Billboard script (optional, can use SpriteBillboard)")]
    [SerializeField] private SpriteBillboard billboard;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // ============================================
    // ANIMATION STATE ENUMS
    // ============================================
    
    public enum AnimationState
    {
        Idle,
        Walk,
        Sprint,
        Squat,
        Jump,
        Landing,
        Alert
    }

    public enum Direction
    {
        Down = 0,   // -Z (south) - facing camera in top-down view
        Up = 1,     // +Z (north)
        Left = 2,   // -X (west)
        Right = 3   // +X (east)
    }

    // ============================================
    // PRIVATE STATE
    // ============================================
    
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh quadMesh;
    private MaterialPropertyBlock propertyBlock;
    private float lastAppliedAspect = -1f;

    private const float MIN_QUAD_DIMENSION = 0.0001f;
    
    private AnimationState currentState = AnimationState.Idle;
    private Direction currentDirection = Direction.Down;
    private float animationTimer = 0f;
    private int currentFrame = 0;
    private ExplorationDirectionalSpriteSequences presentationSequences;
    private float presentationFramesPerSecond = 8f;
    private float presentationTimer = 0f;
    private int presentationFrame = 0;
    private bool hasPresentationOverride = false;
    private bool presentationPlaybackAnimating = false;
    private ExplorationPresentationPlaybackMode presentationPlaybackMode = ExplorationPresentationPlaybackMode.OneShot;
    private Direction presentationDirection = Direction.Down;
    private int hiddenLockCount;

    public event Action OnPresentationPlaybackFinished;
    public event Action OnPresentationOneShotComplete;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        propertyBlock = new MaterialPropertyBlock();
        
        RebuildQuadFromCurrentSettings(GetCurrentSprite());
        
        if (depthWriteMaterial == null)
        {
            Debug.LogWarning("[MeshRendererPlayerVisual] No material assigned! Creating default.");
            depthWriteMaterial = new Material(Shader.Find("Sprites/DepthWrite"));
        }
        
        meshRenderer.material = depthWriteMaterial;
        
        if (meshRenderer.material.renderQueue != 2450)
        {
            meshRenderer.material.renderQueue = 2450;
        }
        
        if (billboard == null)
        {
            billboard = GetComponent<SpriteBillboard>();
        }
    }

    private void RebuildQuad(float width, float height, bool bottomCenter)
    {
        if (quadMesh == null)
        {
            quadMesh = new Mesh();
            quadMesh.name = "PlayerQuad";
        }

        float safeWidth = Mathf.Max(MIN_QUAD_DIMENSION, width);
        float safeHeight = Mathf.Max(MIN_QUAD_DIMENSION, height);
        float left = -safeWidth * 0.5f;
        float right = safeWidth * 0.5f;
        float bottom = bottomCenter ? 0f : -safeHeight * 0.5f;
        float top = bottomCenter ? safeHeight : safeHeight * 0.5f;
         
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(left, bottom, 0f),
            new Vector3(right, bottom, 0f),
            new Vector3(left, top, 0f),
            new Vector3(right, top, 0f)
        };
        
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
         
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        quadMesh.Clear();
        quadMesh.vertices = vertices;
        quadMesh.uv = uvs;
        quadMesh.triangles = triangles;
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        
        meshFilter.mesh = quadMesh;
    }

    private void RebuildQuadFromCurrentSettings(Sprite referenceSprite)
    {
        float aspect = GetAspectForQuad(referenceSprite);
        float height = preserveAspectRatio
            ? Mathf.Max(MIN_QUAD_DIMENSION, referenceHeight)
            : Mathf.Max(MIN_QUAD_DIMENSION, spriteSize.y);
        float width = preserveAspectRatio
            ? height * aspect
            : Mathf.Max(MIN_QUAD_DIMENSION, spriteSize.x);

        RebuildQuad(width, height, anchorBottomCenter);
        lastAppliedAspect = preserveAspectRatio ? aspect : -1f;
    }

    private void EnsureQuadMatchesSprite(Sprite currentSprite)
    {
        if (!preserveAspectRatio)
            return;

        float aspect = GetAspectForQuad(currentSprite);
        if (Mathf.Approximately(aspect, lastAppliedAspect))
            return;

        float height = Mathf.Max(MIN_QUAD_DIMENSION, referenceHeight);
        RebuildQuad(height * aspect, height, anchorBottomCenter);
        lastAppliedAspect = aspect;
    }

    private float GetAspectForQuad(Sprite sprite)
    {
        if (TryGetSpriteAspect(sprite, out float spriteAspect))
            return spriteAspect;

        float fallbackHeight = Mathf.Max(MIN_QUAD_DIMENSION, spriteSize.y);
        return Mathf.Max(MIN_QUAD_DIMENSION, spriteSize.x) / fallbackHeight;
    }

    private static bool TryGetSpriteAspect(Sprite sprite, out float aspect)
    {
        aspect = 1f;
        if (sprite == null)
            return false;

        Rect spriteRect = sprite.textureRect;
        if (spriteRect.width <= 0f || spriteRect.height <= 0f)
            return false;

        aspect = spriteRect.width / spriteRect.height;
        return true;
    }

    private Sprite GetCurrentSprite()
    {
        if (hasPresentationOverride)
        {
            SpriteAnimationSequence presentationSequence = GetPresentationSequence();
            if (presentationSequence == null || presentationSequence.Length == 0)
                return null;

            int presentationFrameIndex = Mathf.Clamp(presentationFrame, 0, presentationSequence.Length - 1);
            return presentationSequence[presentationFrameIndex];
        }

        SpriteAnimationSequence sequence = GetCurrentSequence();
        if (sequence == null || sequence.Length == 0)
            return null;

        int frameIndex = currentFrame % sequence.Length;
        return sequence[frameIndex];
    }

    private void Update()
    {
        UpdateAnimationFrame();
        UpdateSpriteTexture();
    }
    
    private void LateUpdate()
    {
        ApplyForwardOffset();
    }
    
    // ============================================
    // ANIMATION LOGIC
    // ============================================
    
    /// <summary>
    /// Update animation frame based on current state and speed.
    /// All states (including idle) can have multi-frame animations.
    /// </summary>
    private void UpdateAnimationFrame()
    {
        if (hasPresentationOverride)
        {
            UpdatePresentationFrame();
            return;
        }

        // Get animation speed for current state
        float animSpeed = GetAnimationSpeed();
        
        // Get current sprite sequence length
        int sequenceLength = GetCurrentSequenceLength();
        
        // Only advance animation if sequence has multiple frames
        if (sequenceLength > 1)
        {
            animationTimer += Time.deltaTime * animSpeed;
            currentFrame = Mathf.FloorToInt(animationTimer) % sequenceLength;
        }
        else
        {
            // Single frame - reset timer and stay on frame 0
            animationTimer = 0f;
            currentFrame = 0;
        }
    }

    private void UpdatePresentationFrame()
    {
        SpriteAnimationSequence sequence = GetPresentationSequence();
        int sequenceLength = sequence != null ? sequence.Length : 0;
        if (sequenceLength <= 0)
        {
            StopPresentationPlayback();
            return;
        }

        if (!presentationPlaybackAnimating)
        {
            return;
        }

        if (sequenceLength == 1)
        {
            presentationFrame = 0;

            if (presentationPlaybackMode == ExplorationPresentationPlaybackMode.OneShot)
            {
                presentationPlaybackAnimating = false;
                NotifyPresentationPlaybackFinished();
            }

            return;
        }

        presentationTimer += Time.deltaTime * Mathf.Max(0.01f, presentationFramesPerSecond);

        if (presentationPlaybackMode == ExplorationPresentationPlaybackMode.OneShot)
        {
            int evaluatedFrame = Mathf.FloorToInt(presentationTimer);
            if (evaluatedFrame >= sequenceLength - 1)
            {
                presentationFrame = sequenceLength - 1;
                presentationPlaybackAnimating = false;
                NotifyPresentationPlaybackFinished();
                return;
            }

            presentationFrame = Mathf.Clamp(evaluatedFrame, 0, sequenceLength - 1);
            return;
        }

        int loopedFrame = Mathf.FloorToInt(presentationTimer) % sequenceLength;
        presentationFrame = Mathf.Clamp(loopedFrame, 0, sequenceLength - 1);
    }
    
    /// <summary>
    /// Get animation speed (FPS) for current state.
    /// </summary>
    private float GetAnimationSpeed()
    {
        return GetAnimationSpeed(currentState);
    }
    
    /// <summary>
    /// Get sprite sequence for current state and direction.
    /// </summary>
    private SpriteAnimationSequence GetCurrentSequence()
    {
        return GetSequenceForState(currentState, currentDirection);
    }
    
    /// <summary>
    /// Get length of current sprite sequence.
    /// </summary>
    private int GetCurrentSequenceLength()
    {
        SpriteAnimationSequence seq = GetCurrentSequence();
        return seq != null ? seq.Length : 1;
    }
    
    /// <summary>
    /// Update the mesh texture based on current animation state, direction, and frame.
    /// </summary>
    private void UpdateSpriteTexture()
    {
        Sprite currentSprite = GetCurrentSprite();
        if (currentSprite == null)
            return;

        EnsureQuadMatchesSprite(currentSprite);
        
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        
        meshRenderer.GetPropertyBlock(propertyBlock);
        
        if (currentSprite.texture != null)
        {
            propertyBlock.SetTexture("_MainTex", currentSprite.texture);
            
            // Set UV offset/scale for sprite atlas support
            Rect spriteRect = currentSprite.textureRect;
            Vector2 textureSize = new Vector2(currentSprite.texture.width, currentSprite.texture.height);
            
            Vector2 offset = new Vector2(spriteRect.x / textureSize.x, spriteRect.y / textureSize.y);
            Vector2 scale = new Vector2(spriteRect.width / textureSize.x, spriteRect.height / textureSize.y);
            
            propertyBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
            
            // Preserve material properties
            if (depthWriteMaterial != null)
            {
                if (depthWriteMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", depthWriteMaterial.GetColor("_Color"));
                }
                if (depthWriteMaterial.HasProperty("_Cutoff"))
                {
                    propertyBlock.SetFloat("_Cutoff", depthWriteMaterial.GetFloat("_Cutoff"));
                }
            }
            
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            int sequenceLength = hasPresentationOverride
                ? (GetPresentationSequence() != null ? GetPresentationSequence().Length : 0)
                : GetCurrentSequenceLength();
            Debug.Log($"[MeshRendererPlayerVisual] State: {currentState}, Dir: {currentDirection}, Frame: {currentFrame}/{sequenceLength}, FPS: {GetAnimationSpeed()}");
        }
    }
    
    /// <summary>
    /// Apply position offsets (XZ, Y, and forward toward camera).
    /// Uses dynamicForwardOffset if > 0, otherwise uses forwardOffset.
    /// </summary>
    private void ApplyForwardOffset()
    {
        Vector3 playerRootPos = transform.parent != null ? transform.parent.position : Vector3.zero;
        
        // Apply XZ offset (either single or per-yaw)
        Vector2 xzOffset = GetCurrentXZOffset();
        float effectiveY = yPositionOffset + GetPitchYOffsetLift();
        Vector3 basePosition = playerRootPos + new Vector3(xzOffset.x, effectiveY, xzOffset.y);
        
        // Use dynamic offset if set (from PlayerVisualClipAvoidance), otherwise use static offset
        float effectiveForwardOffset = dynamicForwardOffset > 0f ? dynamicForwardOffset : forwardOffset;
        
        if (effectiveForwardOffset > 0f)
        {
            if (billboard == null)
            {
                billboard = GetComponent<SpriteBillboard>();
            }
            
            Camera cam = billboard != null ? billboard.GetTargetCamera() : null;
            if (cam != null)
            {
                Vector3 toCamera = (cam.transform.position - basePosition);
                float distance = toCamera.magnitude;
                if (distance > 0.001f)
                {
                    toCamera /= distance;
                    basePosition += toCamera * effectiveForwardOffset;
                }
            }
        }
        
        transform.position = basePosition;
    }
    
    /// <summary>
    /// Get the current XZ offset based on settings.
    /// </summary>
    private Vector2 GetCurrentXZOffset()
    {
        if (usePerYawXZOffsets)
        {
            // Use per-yaw offset
            if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > currentCameraYawIndex)
            {
                return xzPositionOffsetsPerYaw[currentCameraYawIndex];
            }
        }
        
        // Use single offset
        return xzPositionOffset;
    }

    private float GetPitchYOffsetLift()
    {
        return ExplorationVisualPitchYOffset.EvaluateLiftForSubject(
            pitchYOffsetSettings,
            transform.position,
            enableDebugLogs,
            nameof(MeshRendererPlayerVisual));
    }
    
    /// <summary>
    /// Update visual position for a specific camera yaw index.
    /// Called by PlayerMovement3D or CameraZoneBehavior when camera rotates.
    /// </summary>
    /// <param name="yawIndex90">Camera yaw index (0-3)</param>
    public void UpdateVisualForCameraYaw(int yawIndex90)
    {
        // Normalize yaw index to 0-3
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;
        
        // Ensure array is valid
        if (xzPositionOffsetsPerYaw == null || xzPositionOffsetsPerYaw.Length < 4)
        {
            xzPositionOffsetsPerYaw = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            };
        }
        
        if (enableDebugLogs)
        {
            Vector2 xzOffset = GetCurrentXZOffset();
            Debug.Log($"[MeshRendererPlayerVisual] Updated visual for yaw index {yawIndex90}: XZ=({xzOffset.x}, {xzOffset.y}), Y={yPositionOffset}");
        }
    }
    
    /// <summary>
    /// Set dynamic forward offset (called by PlayerVisualClipAvoidance).
    /// This overrides the static forwardOffset when > 0.
    /// </summary>
    public void SetDynamicForwardOffset(float offset)
    {
        dynamicForwardOffset = Mathf.Max(0f, offset);
    }

    // ============================================
    // PUBLIC API
    // ============================================
    
    public void SetTemporarilyHidden(bool hidden)
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            return;

        if (hidden)
        {
            hiddenLockCount++;
            meshRenderer.enabled = false;
            return;
        }

        hiddenLockCount = Mathf.Max(0, hiddenLockCount - 1);
        if (hiddenLockCount == 0)
            meshRenderer.enabled = true;
    }

    public void ForceVisible()
    {
        hiddenLockCount = 0;
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = true;
    }

    /// <summary>
    /// Set animation state. Resets the animation timer unless switching between walk and sprint
    /// (same sprites, faster FPS).
    /// </summary>
    public void SetAnimationState(AnimationState state)
    {
        if (currentState != state)
        {
            bool keepWalkCycle = (currentState == AnimationState.Walk && state == AnimationState.Sprint)
                || (currentState == AnimationState.Sprint && state == AnimationState.Walk);
            currentState = state;
            if (!keepWalkCycle)
            {
                animationTimer = 0f;
                currentFrame = 0;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererPlayerVisual] State changed to: {state}");
            }
        }
    }

    /// <summary>
    /// Set facing direction from a Vector2 input.
    /// Uses Stardew-style diagonals: horizontal takes priority.
    /// </summary>
    public void SetDirection(Vector2 direction)
    {
        Direction newDirection = DirectionFromVector2(direction);
        
        if (currentDirection != newDirection)
        {
            currentDirection = newDirection;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererPlayerVisual] Direction changed to: {newDirection}");
            }
        }
    }
    
    /// <summary>
    /// Set facing direction directly using Direction enum.
    /// </summary>
    public void SetDirection(Direction direction)
    {
        if (currentDirection != direction)
        {
            currentDirection = direction;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererPlayerVisual] Direction changed to: {direction}");
            }
        }
    }

    /// <summary>
    /// Convert Vector2 direction to Direction enum.
    /// Implements Stardew Valley diagonals: if absX > absY, use Left/Right.
    /// </summary>
    private Direction DirectionFromVector2(Vector2 dir)
    {
        float absX = Mathf.Abs(dir.x);
        float absY = Mathf.Abs(dir.y);
        
        // Stardew diagonals: horizontal takes priority
        if (absX > absY)
        {
            return dir.x > 0 ? Direction.Right : Direction.Left;
        }
        else
        {
            return dir.y > 0 ? Direction.Up : Direction.Down;
        }
    }

    /// <summary>
    /// Set sprite size at runtime.
    /// </summary>
    public void SetSpriteSize(Vector2 size)
    {
        spriteSize = size;
        referenceHeight = Mathf.Max(MIN_QUAD_DIMENSION, size.y);
        RebuildQuadFromCurrentSettings(GetCurrentSprite());
    }

    /// <summary>
    /// Get current animation state.
    /// </summary>
    public AnimationState GetAnimationState() => currentState;

    /// <summary>
    /// Get current facing direction.
    /// </summary>
    public Direction GetDirection() => currentDirection;

    /// <summary>
    /// Snapshot the facing direction currently used for presentation playback.
    /// </summary>
    public Direction GetFacingSnapshot() => hasPresentationOverride ? presentationDirection : currentDirection;
    
    /// <summary>
    /// Get current animation frame index.
    /// </summary>
    public int GetCurrentFrame() => currentFrame;

    /// <summary>
    /// True while presentation playback is actively animating.
    /// </summary>
    public bool IsPresentationPlaybackAnimating => hasPresentationOverride && presentationPlaybackAnimating;

    /// <summary>
    /// True while a presentation one-shot is actively animating toward its terminal frame.
    /// </summary>
    public bool IsPresentationOneShotPlaying => hasPresentationOverride &&
                                                presentationPlaybackMode == ExplorationPresentationPlaybackMode.OneShot &&
                                                presentationPlaybackAnimating;

    /// <summary>
    /// Current presentation playback mode.
    /// </summary>
    public ExplorationPresentationPlaybackMode CurrentPresentationPlaybackMode => presentationPlaybackMode;

    /// <summary>
    /// Play a reusable presentation clip on the primary visual without auto-restoring gameplay state.
    /// The caller decides when to release back to normal animation updates.
    /// </summary>
    public void PlayPresentation(
        ExplorationDirectionalSpriteSequences sequences,
        float framesPerSecond,
        ExplorationFacingDirection facingDirection,
        ExplorationPresentationPlaybackMode playbackMode)
    {
        presentationSequences = sequences;
        presentationFramesPerSecond = Mathf.Max(0.01f, framesPerSecond);
        presentationTimer = 0f;
        presentationFrame = 0;
        hasPresentationOverride = sequences != null && sequences.HasSprites();
        presentationPlaybackAnimating = hasPresentationOverride;
        presentationPlaybackMode = playbackMode;
        presentationDirection = ToPlayerDirection(facingDirection);
    }

    /// <summary>
    /// Play a reusable presentation one-shot on the primary visual without auto-restoring gameplay state.
    /// The caller decides when to release back to normal animation updates.
    /// </summary>
    public void PlayPresentationOneShot(
        ExplorationDirectionalSpriteSequences sequences,
        float framesPerSecond,
        ExplorationFacingDirection facingDirection)
    {
        PlayPresentation(
            sequences,
            framesPerSecond,
            facingDirection,
            ExplorationPresentationPlaybackMode.OneShot);
    }

    /// <summary>
    /// Freeze the current presentation frame while keeping the override active.
    /// </summary>
    public void HoldPresentationFrame()
    {
        if (!hasPresentationOverride)
            return;

        presentationPlaybackAnimating = false;
    }

    /// <summary>
    /// Clear any active presentation one-shot and return sprite selection to gameplay-driven state.
    /// </summary>
    public void StopPresentationPlayback()
    {
        presentationSequences = null;
        presentationFramesPerSecond = 8f;
        presentationTimer = 0f;
        presentationFrame = 0;
        presentationPlaybackAnimating = false;
        presentationPlaybackMode = ExplorationPresentationPlaybackMode.OneShot;
        hasPresentationOverride = false;
    }

    /// <summary>
    /// Clear any active presentation one-shot and return sprite selection to gameplay-driven state.
    /// </summary>
    public void StopPresentationOneShot()
    {
        StopPresentationPlayback();
    }

    /// <summary>
    /// Duration in seconds for a presentation clip in the specified facing direction.
    /// </summary>
    public float GetPresentationDuration(
        ExplorationDirectionalSpriteSequences sequences,
        float framesPerSecond,
        ExplorationFacingDirection? facingDirection = null)
    {
        if (sequences == null || !sequences.HasSprites())
            return 0f;

        Direction direction = facingDirection.HasValue ? ToPlayerDirection(facingDirection.Value) : currentDirection;
        SpriteAnimationSequence sequence = GetDirectionalSequence(sequences, direction);
        int frameCount = sequence != null ? sequence.Length : 0;
        if (frameCount <= 1)
            return 0f;

        return (frameCount - 1) / Mathf.Max(0.01f, framesPerSecond);
    }
    
    /// <summary>
    /// Get animation speed for a specific state.
    /// </summary>
    public float GetAnimationSpeed(AnimationState state)
    {
        switch (state)
        {
            case AnimationState.Idle: return idleAnimationSpeed;
            case AnimationState.Alert: return idleAnimationSpeed;
            case AnimationState.Walk: return walkAnimationSpeed;
            case AnimationState.Sprint: return sprintAnimationSpeed;
            case AnimationState.Squat: return squatAnimationSpeed;
            case AnimationState.Jump: return jumpAnimationSpeed;
            case AnimationState.Landing: return landingAnimationSpeed;
            default: return walkAnimationSpeed;
        }
    }
    
    /// <summary>
    /// Get the duration (in seconds) it takes to play a full cycle of a specific animation state.
    /// Returns 0 if the animation has no frames or if state is invalid.
    /// </summary>
    /// <param name="state">The animation state to get duration for</param>
    /// <param name="direction">The direction to check (uses current direction if null)</param>
    /// <returns>Duration in seconds to play one full cycle of the animation</returns>
    public float GetAnimationDuration(AnimationState state, Direction? direction = null)
    {
        Direction dirToUse = direction ?? currentDirection;
        float fps = GetAnimationSpeed(state);
        
        if (fps <= 0f)
            return 0f;
        
        // Get sequence without modifying current state
        SpriteAnimationSequence sequence = GetSequenceForState(state, dirToUse);
        int frameCount = sequence != null ? sequence.Length : 0;
        
        if (frameCount <= 0)
            return 0f;
        
        // Duration = (frameCount - 1) / fps
        // This gives the time to play from first frame to last frame
        // For a single frame, duration is 0 (instant)
        return frameCount > 1 ? (frameCount - 1) / fps : 0f;
    }
    
    /// <summary>
    /// Get sprite sequence for a specific state and direction without modifying current state.
    /// </summary>
    private SpriteAnimationSequence GetSequenceForState(AnimationState state, Direction direction)
    {
        DirectionalSpriteSequences sequences = ResolveSequencesForState(state);
        if (sequences == null || !sequences.HasSprites())
        {
            return null;
        }

        SpriteAnimationSequence seq = sequences.GetSequence(direction);
        if (seq == null || seq.Length == 0)
        {
            seq = sequences.down;
        }

        return seq;
    }

    private DirectionalSpriteSequences ResolveSequencesForState(AnimationState state)
    {
        DirectionalSpriteSequences sequences;

        switch (state)
        {
            case AnimationState.Idle:
                sequences = idleSequences;
                break;
            case AnimationState.Alert:
                sequences = alertSequences;
                break;
            case AnimationState.Walk:
            case AnimationState.Sprint:
                sequences = walkSequences;
                break;
            case AnimationState.Squat:
                sequences = squatSequences;
                break;
            case AnimationState.Jump:
                sequences = jumpSequences;
                break;
            case AnimationState.Landing:
                sequences = landingSequences;
                break;
            default:
                sequences = idleSequences;
                break;
        }

        if (state == AnimationState.Alert && (sequences == null || !sequences.HasSprites()))
        {
            sequences = idleSequences;
        }

        if ((sequences == null || !sequences.HasSprites()) && idleSequences != null && idleSequences.HasSprites())
        {
            sequences = idleSequences;
        }

        return sequences;
    }

    private SpriteAnimationSequence GetPresentationSequence()
    {
        if (presentationSequences == null || !presentationSequences.HasSprites())
            return null;

        return GetDirectionalSequence(presentationSequences, presentationDirection);
    }

    private static SpriteAnimationSequence GetDirectionalSequence(
        ExplorationDirectionalSpriteSequences sequences,
        Direction direction)
    {
        if (sequences == null)
            return null;

        ExplorationFacingDirection explorationDirection = ToExplorationDirection(direction);
        SpriteAnimationSequence sequence = sequences.GetSequence(explorationDirection);
        if (sequence == null || sequence.Length == 0)
        {
            sequence = sequences.down;
        }

        return sequence;
    }

    private void NotifyPresentationPlaybackFinished()
    {
        OnPresentationPlaybackFinished?.Invoke();
        OnPresentationOneShotComplete?.Invoke();
    }

    private static Direction ToPlayerDirection(ExplorationFacingDirection direction)
    {
        switch (direction)
        {
            case ExplorationFacingDirection.Down:
                return Direction.Down;
            case ExplorationFacingDirection.Up:
                return Direction.Up;
            case ExplorationFacingDirection.Left:
                return Direction.Left;
            case ExplorationFacingDirection.Right:
                return Direction.Right;
            default:
                return Direction.Down;
        }
    }

    private static ExplorationFacingDirection ToExplorationDirection(Direction direction)
    {
        switch (direction)
        {
            case Direction.Down:
                return ExplorationFacingDirection.Down;
            case Direction.Up:
                return ExplorationFacingDirection.Up;
            case Direction.Left:
                return ExplorationFacingDirection.Left;
            case Direction.Right:
                return ExplorationFacingDirection.Right;
            default:
                return ExplorationFacingDirection.Down;
        }
    }
    
    // ============================================
    // POSITION OFFSET API
    // ============================================
    
    /// <summary>
    /// Get current camera yaw index being used for position offsets.
    /// </summary>
    public int CurrentCameraYawIndex => currentCameraYawIndex;
    
    /// <summary>
    /// Set the Y position offset.
    /// </summary>
    public void SetYPositionOffset(float offset)
    {
        yPositionOffset = offset;
    }
    
    /// <summary>
    /// Get the Y position offset.
    /// </summary>
    public float GetYPositionOffset() => yPositionOffset;
    
    /// <summary>
    /// Set the single XZ position offset (when not using per-yaw offsets).
    /// </summary>
    public void SetXZPositionOffset(Vector2 offset)
    {
        xzPositionOffset = offset;
    }
    
    /// <summary>
    /// Get the single XZ position offset.
    /// </summary>
    public Vector2 GetXZPositionOffset() => xzPositionOffset;
    
    /// <summary>
    /// Set XZ offset for a specific camera yaw index.
    /// </summary>
    /// <param name="yawIndex">Camera yaw index (0-3)</param>
    /// <param name="offset">XZ offset for this yaw</param>
    public void SetXZOffsetForYaw(int yawIndex, Vector2 offset)
    {
        yawIndex = yawIndex % 4;
        if (yawIndex < 0) yawIndex += 4;
        
        if (xzPositionOffsetsPerYaw == null || xzPositionOffsetsPerYaw.Length < 4)
        {
            xzPositionOffsetsPerYaw = new Vector2[4];
        }
        
        xzPositionOffsetsPerYaw[yawIndex] = offset;
    }
    
    /// <summary>
    /// Get XZ offset for a specific camera yaw index.
    /// </summary>
    public Vector2 GetXZOffsetForYaw(int yawIndex)
    {
        yawIndex = yawIndex % 4;
        if (yawIndex < 0) yawIndex += 4;
        
        if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > yawIndex)
        {
            return xzPositionOffsetsPerYaw[yawIndex];
        }
        
        return Vector2.zero;
    }
    
    /// <summary>
    /// Enable or disable per-yaw XZ offsets.
    /// </summary>
    public void SetUsePerYawXZOffsets(bool enable)
    {
        usePerYawXZOffsets = enable;
    }
    
    /// <summary>
    /// Check if using per-yaw XZ offsets.
    /// </summary>
    public bool IsUsingPerYawXZOffsets => usePerYawXZOffsets;
    
    /// <summary>
    /// Get the forward offset (static).
    /// </summary>
    public float GetForwardOffset() => forwardOffset;
    
    /// <summary>
    /// Set the forward offset (static).
    /// </summary>
    public void SetForwardOffset(float offset)
    {
        forwardOffset = offset;
    }
}
