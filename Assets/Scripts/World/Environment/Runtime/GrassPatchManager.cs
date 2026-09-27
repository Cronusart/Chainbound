using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages grass interaction for a group of grass blade renderers.
/// Uses a single trigger collider for player detection and updates all blades via MaterialPropertyBlock.
/// 
/// TUNING GUIDE:
/// - interactRadius: 1.0-2.0 for natural grass parting. Larger values = wider path.
/// - pushStrengthXZ: 0.15-0.3 for subtle parting, 0.4-0.6 for dramatic parting.
/// - bendStrength: 0.5-0.8 for visible bending in movement direction.
/// - downStrength: 0.05-0.15 for subtle press, 0.2+ for stomping effect.
/// - maxDownOffset: 0.1-0.2 prevents grass from sinking below ground.
/// - heightMaskPower: 1.0 = linear (top moves proportionally), 2.0+ = only tips move.
/// - updateRateHz: 15-30 Hz is usually sufficient for smooth interaction.
/// 
/// SETUP:
/// 1. Add this component to a parent GameObject containing grass blade renderers as children.
/// 2. Add a BoxCollider (or other collider) set as Trigger covering the grass area.
/// 3. Grass blades should use the Environment/GrassBend shader.
/// 4. Tag the player GameObject as "Player" or assign playerObject manually.
/// 
/// PERFORMANCE:
/// - One manager per grass area/patch group.
/// - No per-blade colliders needed - manager handles all interaction.
/// - Updates throttled to updateRateHz for efficiency.
/// 
/// RUNTIME API:
/// - RegenerateBladeMesh() - Regenerate mesh for a specific blade
/// - SetBladeSize() - Change blade size at runtime
/// - SetBladeForwardOffset() - Enable/configure forward offset per blade
/// - SetInteractionStrength() - Adjust interaction parameters at runtime
/// </summary>
[RequireComponent(typeof(Collider))]
public class GrassPatchManager : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Tag of the player GameObject (leave empty to auto-detect PlayerMovement3D)")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Player GameObject reference (auto-found if null)")]
    [SerializeField] private GameObject playerObject;
    
    [Header("Interaction Settings")]
    [Tooltip("Radius of grass interaction around player (world units)")]
    [SerializeField] private float interactRadius = 1.5f;

    [Tooltip("If true, each blade reacts from its visual plant point instead of per-vertex. Required for wide cards such as bushes.")]
    [SerializeField] private bool usePivotFalloff = false;
    
    [Tooltip("Strength of XZ push away from player (radial parting)")]
    [SerializeField] private float pushStrengthXZ = 0.25f;
    
    [Tooltip("Strength of bending in movement direction")]
    [SerializeField] private float bendStrength = 0.6f;
    
    [Tooltip("Strength of downward press when player is on top")]
    [SerializeField] private float downStrength = 0.1f;
    
    [Tooltip("Maximum downward offset to prevent grass sinking below ground")]
    [SerializeField] private float maxDownOffset = 0.15f;
    
    [Tooltip("Controls how much top moves vs base (1.0 = linear, 2.0+ = only tips move)")]
    [SerializeField] private float heightMaskPower = 1.0f;
    
    [Header("Performance")]
    [Tooltip("Update rate in Hz (15-30 recommended)")]
    [SerializeField] private float updateRateHz = 20f;
    
    [Header("Decay Settings")]
    [Tooltip("Time in seconds to decay interaction after player exits")]
    [SerializeField] private float decayTime = 0.4f;
    
    [Tooltip("If true, smoothly decays interaction. If false, immediately resets.")]
    [SerializeField] private bool smoothDecay = true;
    
    [Header("Blade Collection")]
    [Tooltip("If true, auto-collects renderers from children on Awake")]
    [SerializeField] private bool autoCollectBlades = true;

    [Tooltip("Defers hierarchy scans and blade setup to the scene warmup pipeline.")]
    [SerializeField] private bool deferInitializationToSceneWarmup = false;
    
    [Tooltip("Only collect renderers using materials with this shader name")]
    [SerializeField] private string shaderNameFilter = "Environment/GrassBend";
    
    [Header("Forward Offset (Optional)")]
    [Tooltip("Enable forward offset for all blades to prevent camera clipping")]
    [SerializeField] private bool enableForwardOffset = false;
    
    [Tooltip("Forward offset distance toward camera (world units). Set to 0 to disable per-blade.")]
    [SerializeField] private float forwardOffset = 0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawDebugGizmos = false;
    
    // Collected blade renderers
    private List<Renderer> bladeRenderers = new List<Renderer>();
    private List<float> bladeBaseYPositions = new List<float>();
    
    // Forward offset tracking (per blade)
    private Dictionary<Renderer, BladeOffsetData> bladeOffsetData = new Dictionary<Renderer, BladeOffsetData>();
    
    // Blade mesh data (for runtime regeneration)
    private Dictionary<Renderer, BladeMeshData> bladeMeshData = new Dictionary<Renderer, BladeMeshData>();
    
    private struct BladeOffsetData
    {
        public Vector3 originalPosition;
        public SpriteBillboard billboard;
        public float offset;
        
        public BladeOffsetData(Vector3 pos, SpriteBillboard bb, float off)
        {
            originalPosition = pos;
            billboard = bb;
            offset = off;
        }
    }
    
    private struct BladeMeshData
    {
        public Vector2 size;
        public float sizeMultiplier;
        public bool useManualSize;
        public bool usePixelsPerUnit;
        public float pixelsPerUnit;
        public bool useCrossQuad;
        
        public BladeMeshData(Vector2 s, float mult, bool manual, bool ppu, float ppuVal, bool cross)
        {
            size = s;
            sizeMultiplier = mult;
            useManualSize = manual;
            usePixelsPerUnit = ppu;
            pixelsPerUnit = ppuVal;
            useCrossQuad = cross;
        }
    }
    
    // Cached MaterialPropertyBlock (reused for all blades)
    private MaterialPropertyBlock propertyBlock;
    
    // Player tracking
    private bool playerInTrigger = false;
    private Transform playerTransform;
    private Rigidbody playerRigidbody;
    private Vector3 lastPlayerPosition;
    private float lastUpdateTime = 0f;
    
    // Current interaction state
    private Vector3 currentInteractorPos;
    private Vector3 currentInteractorVel;
    private float currentPushStrength;
    private float currentBendStrength;
    private float currentDownStrength;
    private bool isInitialized;
    
    // Decay state
    private bool isDecaying = false;
    private float decayStartTime;
    private Vector3 decayStartPos;
    private Vector3 decayStartVel;
    private float decayStartPush;
    private float decayStartBend;
    private float decayStartDown;
    
    // Shader property IDs (cached for performance)
    private static readonly int InteractorPosWSID = Shader.PropertyToID("_InteractorPosWS");
    private static readonly int InteractorVelWSID = Shader.PropertyToID("_InteractorVelWS");
    private static readonly int InteractRadiusID = Shader.PropertyToID("_InteractRadius");
    private static readonly int PushStrengthID = Shader.PropertyToID("_PushStrength");
    private static readonly int BendStrengthID = Shader.PropertyToID("_BendStrength");
    private static readonly int DownStrengthID = Shader.PropertyToID("_DownStrength");
    private static readonly int MaxDownOffsetID = Shader.PropertyToID("_MaxDownOffset");
    private static readonly int HeightMaskPowerID = Shader.PropertyToID("_HeightMaskPower");
    private static readonly int BladeBaseYID = Shader.PropertyToID("_BladeBaseY");
    private static readonly int BladeHeightID = Shader.PropertyToID("_BladeHeight");
    private static readonly int UsePivotFalloffID = Shader.PropertyToID("_UsePivotFalloff");
    private static readonly int BladePivotWSID = Shader.PropertyToID("_BladePivotWS");
    
    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        
        // Ensure collider is a trigger
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        
        if (!deferInitializationToSceneWarmup)
            InitializeForSceneWarmup();
    }

    /// <summary>Runs idempotent setup, allowing large scenes to stage managers across frames.</summary>
    public void InitializeForSceneWarmup()
    {
        if (isInitialized)
            return;

        // Auto-collect blade renderers
        if (autoCollectBlades)
        {
            CollectBladeRenderers();
        }
        
        // Find player if not assigned
        if (playerObject == null)
        {
            FindPlayer();
        }
        
        // Initialize all blades to zero interaction
        ResetAllBlades();
        
        // Initialize forward offset data if enabled
        if (enableForwardOffset && forwardOffset > 0f)
        {
            InitializeForwardOffsets();
        }

        isInitialized = true;
    }
    
    /// <summary>
    /// Collect all child renderers that use the GrassBend shader.
    /// </summary>
    public void CollectBladeRenderers()
    {
        bladeRenderers.Clear();
        bladeBaseYPositions.Clear();
        
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer rend in allRenderers)
        {
            if (rend == null || rend.sharedMaterial == null)
                continue;

            MeshFilter meshFilter = rend.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;
            
            // Check if shader matches filter
            if (!string.IsNullOrEmpty(shaderNameFilter))
            {
                if (rend.sharedMaterial.shader == null || 
                    !rend.sharedMaterial.shader.name.Contains(shaderNameFilter.Replace("Environment/", "")))
                {
                    continue;
                }
            }
            
            bladeRenderers.Add(rend);
            bladeBaseYPositions.Add(rend.bounds.min.y);
            
            // Initialize mesh data for this blade
            if (!bladeMeshData.ContainsKey(rend))
            {
                MeshFilter mf = rend.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    // Estimate size from mesh bounds
                    Bounds bounds = mf.sharedMesh.bounds;
                    Vector2 estimatedSize = new Vector2(bounds.size.x, bounds.size.y);
                    bladeMeshData[rend] = new BladeMeshData(estimatedSize, 1f, true, false, 16f, false);
                }
                else
                {
                    bladeMeshData[rend] = new BladeMeshData(new Vector2(0.5f, 1f), 1f, true, false, 16f, false);
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[GrassPatchManager] Collected {bladeRenderers.Count} blade renderers in {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Manually add a renderer to the managed blades list.
    /// </summary>
    public void AddBladeRenderer(Renderer rend)
    {
        if (rend == null || bladeRenderers.Contains(rend))
            return;

        MeshFilter meshFilter = rend.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        bladeRenderers.Add(rend);
        bladeBaseYPositions.Add(rend.bounds.min.y);
        
        // Initialize mesh data
        if (!bladeMeshData.ContainsKey(rend))
        {
            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector2 estimatedSize = new Vector2(bounds.size.x, bounds.size.y);
            bladeMeshData[rend] = new BladeMeshData(estimatedSize, 1f, true, false, 16f, false);
        }
        
        // Initialize forward offset if enabled
        if (enableForwardOffset && forwardOffset > 0f)
        {
            InitializeBladeForwardOffset(rend);
        }
    }
    
    /// <summary>
    /// Remove a renderer from the managed blades list.
    /// </summary>
    public void RemoveBladeRenderer(Renderer rend)
    {
        int index = bladeRenderers.IndexOf(rend);
        if (index >= 0)
        {
            bladeRenderers.RemoveAt(index);
            bladeBaseYPositions.RemoveAt(index);
        }
        
        // Clean up data
        bladeMeshData.Remove(rend);
        bladeOffsetData.Remove(rend);
    }
    
    private void FindPlayer()
    {
        // Try to find by tag first
        if (!string.IsNullOrEmpty(playerTag))
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                playerObject = taggedPlayer;
            }
        }
        
        // Fallback: find PlayerMovement3D component
        if (playerObject == null)
        {
            PlayerMovement3D playerMovement = FindObjectOfType<PlayerMovement3D>();
            if (playerMovement != null)
            {
                playerObject = playerMovement.gameObject;
            }
        }
        
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            playerRigidbody = playerObject.GetComponent<Rigidbody>();
            lastPlayerPosition = playerTransform.position;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassPatchManager] Found player: {playerObject.name}");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"[GrassPatchManager] No player found! Grass interaction will not work.");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!isInitialized) return;
        if (IsPlayer(other.gameObject))
        {
            // Ensure player is cached
            if (playerTransform == null)
            {
                if (playerObject != null)
                {
                    playerTransform = playerObject.transform;
                    playerRigidbody = playerObject.GetComponent<Rigidbody>();
                }
                else
                {
                    FindPlayer();
                }
            }
            
            if (playerTransform != null)
            {
                playerInTrigger = true;
                isDecaying = false;
                lastPlayerPosition = playerTransform.position;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[GrassPatchManager] Player entered grass area: {gameObject.name}");
                }
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!isInitialized) return;
        if (!IsPlayer(other.gameObject))
            return;
        
        // Ensure player is cached
        if (playerTransform == null)
        {
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
                playerRigidbody = playerObject.GetComponent<Rigidbody>();
            }
            else
            {
                FindPlayer();
            }
        }
        
        if (playerTransform == null)
            return;
        
        if (!playerInTrigger)
        {
            playerInTrigger = true;
            isDecaying = false;
            lastPlayerPosition = playerTransform.position;
        }
        
        // Throttle updates to updateRateHz
        float updateInterval = 1f / updateRateHz;
        if (Time.time - lastUpdateTime < updateInterval)
            return;
        
        lastUpdateTime = Time.time;
        
        UpdateInteraction();
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!isInitialized) return;
        if (IsPlayer(other.gameObject))
        {
            playerInTrigger = false;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassPatchManager] Player exited grass area: {gameObject.name}");
            }
            
            if (smoothDecay)
            {
                StartDecay();
            }
            else
            {
                ResetAllBlades();
            }
        }
    }
    
    private bool IsPlayer(GameObject obj)
    {
        if (playerObject == null)
        {
            FindPlayer();
        }
        
        if (playerObject == null)
            return false;
        
        return obj == playerObject || obj.transform.IsChildOf(playerObject.transform);
    }
    
    private void UpdateInteraction()
    {
        if (playerTransform == null)
            return;
        
        Vector3 currentPlayerPos = playerTransform.position;
        
        // Calculate velocity from position delta
        float updateInterval = 1f / updateRateHz;
        Vector3 velocity = (currentPlayerPos - lastPlayerPosition) / Mathf.Max(updateInterval, 0.001f);
        lastPlayerPosition = currentPlayerPos;
        
        // Update current interaction state
        currentInteractorPos = currentPlayerPos;
        currentInteractorVel = velocity;
        currentPushStrength = pushStrengthXZ;
        currentBendStrength = bendStrength;
        currentDownStrength = downStrength;
        
        // Apply to all blades
        ApplyInteractionToAllBlades();
    }
    
    private void Update()
    {
        if (!isInitialized) return;
        // Handle decay
        if (isDecaying && smoothDecay)
        {
            float elapsed = Time.time - decayStartTime;
            float t = Mathf.Clamp01(elapsed / decayTime);
            
            // Ease out curve
            t = 1f - (1f - t) * (1f - t);
            
            // Interpolate values toward zero
            currentInteractorPos = Vector3.Lerp(decayStartPos, decayStartPos, 1f); // Keep position stable
            currentInteractorVel = Vector3.Lerp(decayStartVel, Vector3.zero, t);
            currentPushStrength = Mathf.Lerp(decayStartPush, 0f, t);
            currentBendStrength = Mathf.Lerp(decayStartBend, 0f, t);
            currentDownStrength = Mathf.Lerp(decayStartDown, 0f, t);
            
            ApplyInteractionToAllBlades();
            
            if (t >= 1f)
            {
                isDecaying = false;
                ResetAllBlades();
            }
        }
    }
    
    private void LateUpdate()
    {
        // Apply forward offset to all blades if enabled
        if (enableForwardOffset && forwardOffset > 0f)
        {
            ApplyForwardOffsets();
        }
    }
    
    private void StartDecay()
    {
        isDecaying = true;
        decayStartTime = Time.time;
        decayStartPos = currentInteractorPos;
        decayStartVel = currentInteractorVel;
        decayStartPush = currentPushStrength;
        decayStartBend = currentBendStrength;
        decayStartDown = currentDownStrength;
    }
    
    private void ApplyInteractionToAllBlades()
    {
        for (int i = 0; i < bladeRenderers.Count; i++)
        {
            Renderer rend = bladeRenderers[i];
            ApplyBladePropertyBlock(
                rend,
                currentInteractorPos,
                currentInteractorVel,
                currentPushStrength,
                currentBendStrength,
                currentDownStrength);
        }
    }

    private void ApplyBladePropertyBlock(
        Renderer rend,
        Vector3 interactorPos,
        Vector3 interactorVel,
        float pushStr,
        float bendStr,
        float downStr)
    {
        if (rend == null)
            return;
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        float bladeBaseY = rend.bounds.min.y;
        float bladeHeight = Mathf.Max(rend.bounds.size.y, 0.001f);
        MeshFilter meshFilter = rend.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            bladeHeight = Mathf.Max(meshFilter.sharedMesh.bounds.size.y * rend.transform.lossyScale.y, 0.001f);
        }

        Vector3 pivot = rend.bounds.center;
        pivot.y = bladeBaseY;

        rend.GetPropertyBlock(propertyBlock);

        propertyBlock.SetVector(InteractorPosWSID, interactorPos);
        propertyBlock.SetVector(InteractorVelWSID, interactorVel);
        propertyBlock.SetFloat(InteractRadiusID, interactRadius);
        propertyBlock.SetFloat(PushStrengthID, pushStr);
        propertyBlock.SetFloat(BendStrengthID, bendStr);
        propertyBlock.SetFloat(DownStrengthID, downStr);
        propertyBlock.SetFloat(MaxDownOffsetID, maxDownOffset);
        propertyBlock.SetFloat(HeightMaskPowerID, heightMaskPower);
        propertyBlock.SetFloat(BladeBaseYID, bladeBaseY);
        propertyBlock.SetFloat(BladeHeightID, bladeHeight);
        propertyBlock.SetFloat(UsePivotFalloffID, usePivotFalloff ? 1f : 0f);
        propertyBlock.SetVector(BladePivotWSID, pivot);

        rend.SetPropertyBlock(propertyBlock);
    }
    
    /// <summary>
    /// Reset all blades to zero interaction state.
    /// </summary>
    public void ResetAllBlades()
    {
        currentPushStrength = 0f;
        currentBendStrength = 0f;
        currentDownStrength = 0f;
        currentInteractorVel = Vector3.zero;
        
        for (int i = 0; i < bladeRenderers.Count; i++)
        {
            ApplyBladePropertyBlock(
                bladeRenderers[i],
                Vector3.zero,
                Vector3.zero,
                0f,
                0f,
                0f);
        }
    }
    
    private void OnDisable()
    {
        ResetAllBlades();
        isDecaying = false;
    }
    
    /// <summary>
    /// Get the number of managed blade renderers.
    /// </summary>
    public int BladeCount => bladeRenderers.Count;
    
    /// <summary>
    /// Update base Y positions for all blades (call if blades have moved).
    /// </summary>
    public void RefreshBladeBasePositions()
    {
        bladeBaseYPositions.Clear();
        foreach (Renderer rend in bladeRenderers)
        {
            if (rend != null)
            {
                bladeBaseYPositions.Add(rend.bounds.min.y);
            }
            else
            {
                bladeBaseYPositions.Add(0f);
            }
        }
    }
    
    // ============================================
    // FORWARD OFFSET MANAGEMENT
    // ============================================
    
    private void InitializeForwardOffsets()
    {
        foreach (Renderer rend in bladeRenderers)
        {
            if (rend != null)
            {
                InitializeBladeForwardOffset(rend);
            }
        }
    }
    
    private void InitializeBladeForwardOffset(Renderer rend)
    {
        if (rend == null || forwardOffset <= 0f)
            return;
        
        SpriteBillboard billboard = rend.GetComponent<SpriteBillboard>();
        Vector3 originalPos = rend.transform.position;
        
        bladeOffsetData[rend] = new BladeOffsetData(originalPos, billboard, forwardOffset);
    }
    
    private void ApplyForwardOffsets()
    {
        foreach (var kvp in bladeOffsetData)
        {
            Renderer rend = kvp.Key;
            BladeOffsetData data = kvp.Value;
            
            if (rend == null || data.offset <= 0f)
                continue;
            
            SpriteBillboard billboard = data.billboard;
            if (billboard == null)
            {
                billboard = rend.GetComponent<SpriteBillboard>();
                if (billboard == null)
                    continue;
                
                // Update cached billboard
                var updatedData = data;
                updatedData.billboard = billboard;
                bladeOffsetData[rend] = updatedData;
            }
            
            Camera cam = billboard.GetTargetCamera();
            if (cam == null)
                continue;
            
            Vector3 basePos = data.originalPosition;
            Vector3 toCamera = (cam.transform.position - basePos);
            float distance = toCamera.magnitude;
            if (distance < 0.001f)
                continue;
            
            toCamera /= distance;
            Vector3 offsetPosition = basePos + toCamera * data.offset;
            rend.transform.position = offsetPosition;
        }
    }
    
    /// <summary>
    /// Set forward offset for a specific blade (runtime).
    /// </summary>
    public void SetBladeForwardOffset(Renderer rend, float offset)
    {
        if (rend == null || !bladeRenderers.Contains(rend))
            return;
        
        if (offset <= 0f)
        {
            bladeOffsetData.Remove(rend);
            return;
        }
        
        SpriteBillboard billboard = rend.GetComponent<SpriteBillboard>();
        Vector3 originalPos = rend.transform.position;
        
        bladeOffsetData[rend] = new BladeOffsetData(originalPos, billboard, offset);
    }
    
    /// <summary>
    /// Reset original position for a blade's forward offset (call if blade moved).
    /// </summary>
    public void ResetBladeOriginalPosition(Renderer rend)
    {
        if (rend == null || !bladeOffsetData.ContainsKey(rend))
            return;
        
        var data = bladeOffsetData[rend];
        data.originalPosition = rend.transform.position;
        bladeOffsetData[rend] = data;
    }
    
    // ============================================
    // MESH GENERATION API (Runtime)
    // ============================================
    
    /// <summary>
    /// Regenerate mesh for a specific blade at runtime.
    /// </summary>
    public void RegenerateBladeMesh(Renderer rend)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return;
        
        MeshFilter mf = rend.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = rend.gameObject.AddComponent<MeshFilter>();
        }
        
        BladeMeshData data = bladeMeshData[rend];
        Vector2 size = CalculateBladeSpriteSize(rend, data);
        size *= data.sizeMultiplier;
        
        Mesh mesh;
        if (data.useCrossQuad)
        {
            mesh = GrassMeshGenerator.CreateCrossQuadMesh(size, 0f, $"{rend.gameObject.name}_CrossQuad");
        }
        else
        {
            mesh = GrassMeshGenerator.CreateQuadMesh(size, 0f, $"{rend.gameObject.name}_Quad");
        }
        
        mf.mesh = mesh;
        
        // Update collider if present
        BoxCollider col = rend.GetComponent<BoxCollider>();
        if (col != null)
        {
            if (data.useCrossQuad)
            {
                float crossSize = Mathf.Max(size.x, size.y) * 1.414f;
                col.size = new Vector3(crossSize, size.y, 0.1f);
            }
            else
            {
                col.size = new Vector3(size.x, size.y, 0.1f);
            }
            col.center = new Vector3(0f, size.y * 0.5f, 0f);
        }
        
        // Update base Y position
        int index = bladeRenderers.IndexOf(rend);
        if (index >= 0 && index < bladeBaseYPositions.Count)
        {
            bladeBaseYPositions[index] = rend.transform.position.y;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[GrassPatchManager] Regenerated mesh for {rend.gameObject.name} with size: {size}");
        }
    }
    
    /// <summary>
    /// Set size for a specific blade and regenerate its mesh.
    /// </summary>
    public void SetBladeSize(Renderer rend, Vector2 size)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return;
        
        var data = bladeMeshData[rend];
        data.size = size;
        data.useManualSize = true;
        bladeMeshData[rend] = data;
        
        RegenerateBladeMesh(rend);
    }
    
    /// <summary>
    /// Get current size for a blade (before multiplier).
    /// </summary>
    public Vector2 GetBladeSize(Renderer rend)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return Vector2.zero;
        
        return bladeMeshData[rend].size;
    }
    
    /// <summary>
    /// Get actual size for a blade (after multiplier).
    /// </summary>
    public Vector2 GetBladeActualSize(Renderer rend)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return Vector2.zero;
        
        BladeMeshData data = bladeMeshData[rend];
        Vector2 size = CalculateBladeSpriteSize(rend, data);
        return size * data.sizeMultiplier;
    }
    
    /// <summary>
    /// Set size multiplier for a blade and regenerate mesh.
    /// </summary>
    public void SetBladeSizeMultiplier(Renderer rend, float multiplier)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return;
        
        var data = bladeMeshData[rend];
        data.sizeMultiplier = Mathf.Max(0.01f, multiplier);
        bladeMeshData[rend] = data;
        
        RegenerateBladeMesh(rend);
    }
    
    /// <summary>
    /// Configure pixels-per-unit based sizing for a blade.
    /// </summary>
    public void SetBladePixelsPerUnit(Renderer rend, bool enable, float ppu = 16f)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return;
        
        var data = bladeMeshData[rend];
        data.usePixelsPerUnit = enable;
        data.pixelsPerUnit = Mathf.Max(1f, ppu);
        if (enable)
        {
            data.useManualSize = false;
        }
        bladeMeshData[rend] = data;
        
        RegenerateBladeMesh(rend);
    }
    
    /// <summary>
    /// Set whether blade uses cross-quad mesh and regenerate.
    /// </summary>
    public void SetBladeUseCrossQuad(Renderer rend, bool useCrossQuad)
    {
        if (rend == null || !bladeMeshData.ContainsKey(rend))
            return;
        
        var data = bladeMeshData[rend];
        data.useCrossQuad = useCrossQuad;
        bladeMeshData[rend] = data;
        
        RegenerateBladeMesh(rend);
    }
    
    private Vector2 CalculateBladeSpriteSize(Renderer rend, BladeMeshData data)
    {
        if (data.useManualSize)
        {
            return data.size;
        }
        
        if (rend != null && rend.sharedMaterial != null)
        {
            Texture2D tex = rend.sharedMaterial.GetTexture("_MainTex") as Texture2D;
            if (tex != null)
            {
                if (data.usePixelsPerUnit && data.pixelsPerUnit > 0f)
                {
                    float width = tex.width / data.pixelsPerUnit;
                    float height = tex.height / data.pixelsPerUnit;
                    return new Vector2(width, height);
                }
                else
                {
                    float aspectRatio = (float)tex.width / tex.height;
                    return new Vector2(1f * aspectRatio, 1f);
                }
            }
        }
        
        return data.size;
    }
    
    // ============================================
    // RUNTIME INTERACTION TUNING API
    // ============================================
    
    /// <summary>
    /// Set interaction strength parameters at runtime.
    /// </summary>
    public void SetInteractionStrength(float pushStrength, float bendStrength, float downStrength)
    {
        pushStrengthXZ = Mathf.Max(0f, pushStrength);
        this.bendStrength = Mathf.Max(0f, bendStrength);
        this.downStrength = Mathf.Max(0f, downStrength);
        
        // Update immediately if player is in trigger
        if (playerInTrigger)
        {
            currentPushStrength = pushStrengthXZ;
            currentBendStrength = this.bendStrength;
            currentDownStrength = this.downStrength;
            ApplyInteractionToAllBlades();
        }
    }
    
    /// <summary>
    /// Set interaction radius at runtime.
    /// </summary>
    public void SetInteractionRadius(float radius)
    {
        interactRadius = Mathf.Max(0.1f, radius);
        
        // Update immediately if player is in trigger
        if (playerInTrigger)
        {
            ApplyInteractionToAllBlades();
        }
    }
    
    /// <summary>
    /// Set height mask power at runtime.
    /// </summary>
    public void SetHeightMaskPower(float power)
    {
        heightMaskPower = Mathf.Max(0.1f, power);
        
        // Update immediately
        ApplyInteractionToAllBlades();
    }
    
    /// <summary>
    /// Manually set interaction for a specific blade (for testing/external control).
    /// </summary>
    public void SetBladeInteraction(Renderer rend, Vector3 interactorPos, Vector3 interactorVel, float pushStr, float bendStr, float downStr)
    {
        if (rend == null || !bladeRenderers.Contains(rend))
            return;

        ApplyBladePropertyBlock(rend, interactorPos, interactorVel, pushStr, bendStr, downStr);
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;
        
        // Draw interaction radius around player
        if (playerTransform != null && playerInTrigger)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(playerTransform.position, interactRadius);
        }
        
        // Draw blade positions
        Gizmos.color = new Color(0f, 0.5f, 0f, 0.5f);
        foreach (Renderer rend in bladeRenderers)
        {
            if (rend != null)
            {
                Gizmos.DrawWireCube(rend.bounds.center, rend.bounds.size * 0.5f);
            }
        }
    }
#endif
}

