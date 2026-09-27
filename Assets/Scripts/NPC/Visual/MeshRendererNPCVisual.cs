using System;
using UnityEngine;

/// <summary>
/// MeshRenderer-based NPC visual that supports sprite animations.
/// Uses a flat quad mesh that billboards to the camera, with depth-writing material.
/// Supports Idle/Walk/Run animations with directional sprites.
/// Implements blink overlay system: blink triggers only on Idle frame 0 (Down/Left/Right only).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class MeshRendererNPCVisual : MonoBehaviour
{
    // ============================================
    // DIRECTIONAL SPRITE SEQUENCES
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
        
        public bool HasSprites()
        {
            return (down != null && down.Length > 0) ||
                   (left != null && left.Length > 0) ||
                   (right != null && right.Length > 0) ||
                   (up != null && up.Length > 0);
        }
    }
    
    [Header("Directional Sprite Sequences")]
    [Tooltip("Idle animation sprites - 8-frame breathing loop, directional")]
    [SerializeField] private DirectionalSpriteSequences idleSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Walk animation sprites - medium tempo, directional")]
    [SerializeField] private DirectionalSpriteSequences walkSequences = new DirectionalSpriteSequences();
    
    [Tooltip("Run animation sprites - fast tempo, directional")]
    [SerializeField] private DirectionalSpriteSequences runSequences = new DirectionalSpriteSequences();
    
    [Header("Blink Overlay")]
    [Tooltip("Blink sprites - Down, Left, Right only (no Up blink)")]
    [SerializeField] private Sprite blinkDown;
    [SerializeField] private Sprite blinkLeft;
    [SerializeField] private Sprite blinkRight;
    
    [Tooltip("Blink chance per idle-frame-0 occurrence (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float blinkChance = 0.1f;
    
    [Tooltip("Blink duration (seconds)")]
    [SerializeField] private float blinkDuration = 0.1f;
    
    [Tooltip("Blink rate multiplier from route schedule (set by NPCController)")]
    [SerializeField] private float blinkRateMultiplier = 1f;
    
    [Header("Animation Speed Settings")]
    [Tooltip("Frames per second for idle animation (slow tempo for breathing)")]
    [SerializeField] private float idleAnimationSpeed = 2f;
    
    [Tooltip("Frames per second for walk animation (medium tempo)")]
    [SerializeField] private float walkAnimationSpeed = 8f;
    
    [Tooltip("Frames per second for run animation (fast tempo)")]
    [SerializeField] private float runAnimationSpeed = 12f;
    
    [Header("Rendering")]
    [Tooltip("Material with depth-writing shader (e.g., Sprites/DepthWrite)")]
    [SerializeField] private Material depthWriteMaterial;
    
    [Tooltip("Fallback quad size in world units when preserve aspect ratio is disabled.")]
    [SerializeField] private Vector2 spriteSize = new Vector2(1f, 1f);

    [Tooltip("If true, derives quad width from the current sprite aspect ratio. Defaults off so existing prefabs keep their current sizing.")]
    [SerializeField] private bool preserveAspectRatio = false;

    [Tooltip("World-space quad height used when preserve aspect ratio is enabled.")]
    [SerializeField] private float referenceHeight = 1f;
     
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
        ExplorationVisualPitchYOffset.PitchYOffsetSettings.CreateDefault(0.7f);
    
    [Header("References")]
    [Tooltip("Billboard script (optional, can use SpriteBillboard)")]
    [SerializeField] private SpriteBillboard billboard;
    
    [Tooltip("NPCController for checking interrupt policy")]
    [SerializeField] private NPCController npcController;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // ============================================
    // ANIMATION STATE ENUMS
    // ============================================
    
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
    
    private NPCAnimationState currentState = NPCAnimationState.Idle;
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
    
    // Blink state
    private bool isBlinking = false;
    private float blinkTimer = 0f;
    private bool wasOnFrame0 = false;
    
    // Current camera yaw index for per-yaw offsets
    private int currentCameraYawIndex = 0;

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
            Debug.LogWarning("[MeshRendererNPCVisual] No material assigned! Creating default.");
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
        
        if (npcController == null)
        {
            npcController = GetComponentInParent<NPCController>();
        }
    }
    
    private void RebuildQuad(float width, float height, bool bottomCenter)
    {
        if (quadMesh == null)
        {
            quadMesh = new Mesh();
            quadMesh.name = "NPCQuad";
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
        UpdateBlink();
        UpdateSpriteTexture();
    }
    
    private void LateUpdate()
    {
        ApplyPositionOffsets();
    }
    
    // ============================================
    // ANIMATION LOGIC
    // ============================================
    
    private void UpdateAnimationFrame()
    {
        if (hasPresentationOverride)
        {
            UpdatePresentationFrame();
            return;
        }

        float animSpeed = GetAnimationSpeed();
        int sequenceLength = GetCurrentSequenceLength();
        
        if (sequenceLength > 1)
        {
            animationTimer += Time.deltaTime * animSpeed;
            int newFrame = Mathf.FloorToInt(animationTimer) % sequenceLength;
            
            // Check if we just entered frame 0 (for blink trigger)
            bool nowOnFrame0 = (newFrame == 0);
            if (nowOnFrame0 && !wasOnFrame0 && currentState == NPCAnimationState.Idle)
            {
                // Just entered frame 0, check for blink
                TryTriggerBlink();
            }
            wasOnFrame0 = nowOnFrame0;
            
            currentFrame = newFrame;
        }
        else
        {
            animationTimer = 0f;
            currentFrame = 0;
            wasOnFrame0 = false;
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
    
    private void TryTriggerBlink()
    {
        // Blink constraints:
        // 1. Must be Idle state
        // 2. Must be on frame 0
        // 3. Must not already be blinking
        // 4. Must not be facing Up
        // 5. Interrupt policy must be Normal
        
        if (currentState != NPCAnimationState.Idle)
            return;

        if (hasPresentationOverride)
            return;
        
        if (isBlinking)
            return;
        
        if (currentDirection == Direction.Up)
            return;
        
        // Check interrupt policy
        if (npcController != null && npcController.InterruptPolicy != NPCInterruptPolicy.Normal)
            return;
        
        // Stochastic trigger
        float effectiveChance = blinkChance * blinkRateMultiplier;
        if (UnityEngine.Random.value > effectiveChance)
            return;
        
        // Trigger blink
        isBlinking = true;
        blinkTimer = 0f;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[MeshRendererNPCVisual] {gameObject.name} triggered blink (direction: {currentDirection})");
        }
    }
    
    private void UpdateBlink()
    {
        if (hasPresentationOverride)
        {
            isBlinking = false;
            blinkTimer = 0f;
            return;
        }

        if (!isBlinking)
            return;
        
        blinkTimer += Time.deltaTime;
        
        if (blinkTimer >= blinkDuration)
        {
            // Blink complete, resume normal animation
            isBlinking = false;
            blinkTimer = 0f;
        }
    }
    
    private float GetAnimationSpeed()
    {
        switch (currentState)
        {
            case NPCAnimationState.Idle:
                return idleAnimationSpeed;
            case NPCAnimationState.Walk:
                return walkAnimationSpeed;
            case NPCAnimationState.Run:
                return runAnimationSpeed;
            default:
                return walkAnimationSpeed;
        }
    }
    
    private SpriteAnimationSequence GetCurrentSequence()
    {
        DirectionalSpriteSequences sequences;
        
        switch (currentState)
        {
            case NPCAnimationState.Idle:
                sequences = idleSequences;
                break;
            case NPCAnimationState.Walk:
                sequences = walkSequences;
                break;
            case NPCAnimationState.Run:
                sequences = runSequences;
                break;
            default:
                sequences = idleSequences;
                break;
        }
        
        if (sequences == null || !sequences.HasSprites())
        {
            if (idleSequences != null && idleSequences.HasSprites())
            {
                sequences = idleSequences;
            }
            else
            {
                return null;
            }
        }
        
        SpriteAnimationSequence seq = sequences.GetSequence(currentDirection);
        
        if (seq == null || seq.Length == 0)
        {
            seq = sequences.down;
        }
        
        return seq;
    }
    
    private int GetCurrentSequenceLength()
    {
        SpriteAnimationSequence seq = GetCurrentSequence();
        return seq != null ? seq.Length : 1;
    }
    
    private void UpdateSpriteTexture()
    {
        Sprite currentSprite = GetCurrentSprite();
        int frameIndex = currentFrame;
        
        // Apply blink overlay if active
        if (!hasPresentationOverride && isBlinking && currentState == NPCAnimationState.Idle && frameIndex == 0)
        {
            // Replace frame 0 with blink sprite based on direction
            switch (currentDirection)
            {
                case Direction.Down:
                    if (blinkDown != null)
                        currentSprite = blinkDown;
                    break;
                case Direction.Left:
                    if (blinkLeft != null)
                        currentSprite = blinkLeft;
                    break;
                case Direction.Right:
                    if (blinkRight != null)
                        currentSprite = blinkRight;
                    break;
                case Direction.Up:
                    // No blink for Up direction
                    break;
            }
        }
        
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
    }
    
    private void ApplyPositionOffsets()
    {
        Vector3 parentPos = transform.parent != null ? transform.parent.position : Vector3.zero;
        
        Vector2 xzOffset = GetCurrentXZOffset();
        float effectiveY = yPositionOffset + GetPitchYOffsetLift();
        Vector3 basePosition = parentPos + new Vector3(xzOffset.x, effectiveY, xzOffset.y);
        
        transform.position = basePosition;
    }
    
    private Vector2 GetCurrentXZOffset()
    {
        if (usePerYawXZOffsets)
        {
            if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > currentCameraYawIndex)
            {
                return xzPositionOffsetsPerYaw[currentCameraYawIndex];
            }
        }
        
        return xzPositionOffset;
    }

    private float GetPitchYOffsetLift()
    {
        return ExplorationVisualPitchYOffset.EvaluateLiftForSubject(
            pitchYOffsetSettings,
            transform.position,
            enableDebugLogs,
            nameof(MeshRendererNPCVisual));
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Set animation state. Resets animation timer when state changes.
    /// </summary>
    public void SetAnimationState(NPCAnimationState state)
    {
        if (currentState != state)
        {
            currentState = state;
            animationTimer = 0f;
            currentFrame = 0;
            
            // Cancel blink if not idle
            if (state != NPCAnimationState.Idle)
            {
                isBlinking = false;
                blinkTimer = 0f;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererNPCVisual] {gameObject.name} state changed to: {state}");
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
            
            // Cancel blink if direction changed to Up
            if (newDirection == Direction.Up && isBlinking)
            {
                isBlinking = false;
                blinkTimer = 0f;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererNPCVisual] {gameObject.name} direction changed to: {newDirection}");
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
            
            // Cancel blink if direction changed to Up
            if (direction == Direction.Up && isBlinking)
            {
                isBlinking = false;
                blinkTimer = 0f;
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[MeshRendererNPCVisual] {gameObject.name} direction changed to: {direction}");
            }
        }
    }
    
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
    /// Set blink rate multiplier (from route schedule).
    /// </summary>
    public void SetBlinkRateMultiplier(float multiplier)
    {
        blinkRateMultiplier = Mathf.Clamp(multiplier, 0f, 2f);
    }
    
    /// <summary>
    /// Update visual position for a specific camera yaw index.
    /// </summary>
    public void UpdateVisualForCameraYaw(int yawIndex90)
    {
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;
    }
    
    /// <summary>
    /// Get current animation state.
    /// </summary>
    public NPCAnimationState GetAnimationState() => currentState;
    
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
    /// Play a reusable presentation clip on the primary NPC visual without auto-restoring gameplay state.
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
        presentationDirection = ToNpcDirection(facingDirection);
        isBlinking = false;
        blinkTimer = 0f;
        wasOnFrame0 = false;
    }

    /// <summary>
    /// Play a reusable presentation one-shot on the primary NPC visual without auto-restoring gameplay state.
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

        Direction direction = facingDirection.HasValue ? ToNpcDirection(facingDirection.Value) : currentDirection;
        SpriteAnimationSequence sequence = GetDirectionalSequence(sequences, direction);
        int frameCount = sequence != null ? sequence.Length : 0;
        if (frameCount <= 1)
            return 0f;

        return (frameCount - 1) / Mathf.Max(0.01f, framesPerSecond);
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

    private static Direction ToNpcDirection(ExplorationFacingDirection direction)
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
}

