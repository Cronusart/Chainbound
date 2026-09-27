using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns pixel-perfect dust cloud particles when player lands from a fall.
/// Particles spawn in a circular pattern around the player, initially moving outward,
/// then slowing down and lifting up, breaking into smaller particles over time.
/// Similar to GrassDebrisSpawner but optimized for landing impact effects.
/// 
/// Particles are spawned in world space and do NOT follow the player after spawning.
/// They billboard to always face the camera.
/// </summary>
public class LandingDustSpawner : MonoBehaviour
{
    [Header("Debris Settings")]
    [Tooltip("Sprite to use for dust particles (1x1 pixel sprite recommended)")]
    [SerializeField] private Sprite dustSprite;
    
    [Tooltip("Color tint for dust particles")]
    [SerializeField] private Color dustColor = new Color(0.7f, 0.65f, 0.6f, 0.9f);
    
    [Tooltip("Scale multiplier for dust particles. For 1x1 pixel sprites at 100 PPU, try 5.0 to match x5 scaled objects.")]
    [SerializeField] private float dustScale = 5f;
    
    [Tooltip("Base number of dust particles to spawn (scales with fall height)")]
    [SerializeField] private int baseDustCount = 8;
    
    [Tooltip("Maximum dust particles active at once")]
    [SerializeField] private int maxDust = 30;
    
    [Header("Billboarding")]
    [Tooltip("Camera to billboard toward (auto-detected from scene, player object, or Camera.main if null)")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Spawn Pattern")]
    [Tooltip("Player collider radius (for spawn positioning)")]
    [SerializeField] private float playerColliderRadius = 1.75f;
    
    [Tooltip("Spawn radius multiplier (spawns at playerColliderRadius * this value)")]
    [SerializeField] private float spawnRadiusMultiplier = 1.2f;
    
    [Tooltip("Minimum fall height to spawn dust")]
    [SerializeField] private float minFallHeightForDust = 2f;
    
    [Tooltip("Fall height at which maximum dust is spawned")]
    [SerializeField] private float maxFallHeightForDust = 10f;
    
    [Header("Movement")]
    [Tooltip("Initial outward velocity range (away from impact point) - fast burst on impact")]
    [SerializeField] private Vector2 outwardVelocityRange = new Vector2(4f, 8f);
    
    [Tooltip("Use dynamic ease duration/power based on fall height (scales with impact intensity)")]
    [SerializeField] private bool useDynamicEase = true;
    
    [Header("Outward Ease Settings")]
    [Tooltip("Minimum fall height for ease scaling (matches minFallHeightForDust)")]
    [SerializeField] private float minFallHeightForEase = 2f;
    
    [Tooltip("Maximum fall height for ease scaling (matches maxFallHeightForDust)")]
    [SerializeField] private float maxFallHeightForEase = 10f;
    
    [Header("Static Ease Settings (if useDynamicEase is false)")]
    [Tooltip("Duration of outward motion ease-out (seconds). Fast at start, slow at end.")]
    [SerializeField] private float staticOutwardEaseDuration = 0.4f;
    
    [Tooltip("Ease-out power for outward motion (higher = faster initial decay). 2 = quadratic, 3 = cubic")]
    [SerializeField] private float staticOutwardEasePower = 2.5f;
    
    [Header("Dynamic Ease Settings (if useDynamicEase is true)")]
    [Tooltip("Minimum ease duration (at minFallHeightForEase) - shorter = faster decay")]
    [SerializeField] private float minEaseDuration = 0.2f;
    
    [Tooltip("Maximum ease duration (at maxFallHeightForEase) - longer = slower decay")]
    [SerializeField] private float maxEaseDuration = 0.6f;
    
    [Tooltip("Minimum ease power (at minFallHeightForEase) - lower = gentler decay")]
    [SerializeField] private float minEasePower = 1.5f;
    
    [Tooltip("Maximum ease power (at maxFallHeightForEase) - higher = sharper decay")]
    [SerializeField] private float maxEasePower = 3.5f;
    
    [Tooltip("Initial upward velocity range")]
    [SerializeField] private Vector2 upwardVelocityRange = new Vector2(0.3f, 0.8f);
    
    [Tooltip("Gravity strength (downward acceleration)")]
    [SerializeField] private float gravity = 0.3f;
    
    [Tooltip("Air resistance for vertical velocity only (horizontal uses ease-out instead)")]
    [SerializeField] private float verticalAirResistance = 0.98f;
    
    [Tooltip("Lift force (upward acceleration over time - creates rising cloud effect)")]
    [SerializeField] private float liftForce = 0.5f;
    
    [Tooltip("Lift force duration (seconds) - how long particles continue to lift")]
    [SerializeField] private float liftDuration = 0.6f;
    
    [Tooltip("Particle breakup time (seconds) - when particles start breaking into smaller pieces")]
    [SerializeField] private float breakupTime = 0.25f;
    
    [Tooltip("Breakup adds random drift velocity (slow wandering motion)")]
    [SerializeField] private float breakupDriftStrength = 0.3f;
    
    [Header("Lifetime & Fade")]
    [Tooltip("Dust lifetime in seconds")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(1f, 2f);
    
    [Tooltip("Time at which fade begins (seconds after spawn). Fade happens during breakup/lift phase.")]
    [SerializeField] private float fadeStartTime = 0.2f;
    
    [Tooltip("Total fade duration (from fadeStartTime to fully transparent)")]
    [SerializeField] private float fadeDuration = 0.8f;
    
    [Header("Rendering")]
    [Tooltip("Reference GameObject to derive all layer settings from (e.g., the player GameObject). If set, this takes priority over auto-detection.")]
    [SerializeField] private GameObject layerReferenceObject;
    
    [Tooltip("Unity Layer name for dust (auto-detected from layerReferenceObject or parent if not set)")]
    [SerializeField] private string gameObjectLayerName = "";
    
    [Tooltip("Sorting layer for dust (auto-detected from layerReferenceObject's renderer if not set)")]
    [SerializeField] private string sortingLayerName = "";
    
    [Tooltip("Sorting order for dust (auto-detected from layerReferenceObject's renderer if not set)")]
    [SerializeField] private int sortingOrder = int.MinValue;
    
    [Header("Debug")]
    [Tooltip("Enable debug logs for dust spawning")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Dust particle data
    private class DustParticle
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public Vector3 initialOutwardVelocity; // XZ velocity at spawn (for easing)
        public float verticalVelocity; // Y velocity (separate for physics)
        public Vector3 driftVelocity; // Additional drift after breakup
        public float lifetime;
        public float maxLifetime;
        public float spawnTime;
        public float liftTimer;
        public bool hasBrokenUp;
        public bool active;
        public float fallHeight; // Fall height for this particle (used for dynamic ease calculation)
        public float easeDuration; // Calculated ease duration for this particle
        public float easePower; // Calculated ease power for this particle
    }
    
    private List<DustParticle> dustPool = new List<DustParticle>();
    private string resolvedSortingLayerName;
    private int resolvedSortingOrder;
    private int resolvedGameObjectLayer; // Unity Layer (for camera culling)
    private Transform dustContainer; // Static container for dust particles (not affected by player movement)
    
    private void Awake()
    {
        // Detect Unity Layer from reference object or parent
        DetectGameObjectLayer();
        
        // Detect sorting layer and order from attached renderer
        DetectRendererSettings();
        
        // Auto-detect camera if not assigned
        DetectCamera();
        
        // Create a static container for dust particles in world space
        // This prevents particles from following the player
        GameObject containerObj = new GameObject("LandingDustContainer");
        containerObj.transform.SetParent(null); // World space - no parent
        dustContainer = containerObj.transform;
        
        // Warn if no sprite assigned
        if (dustSprite == null && enableDebugLogs)
        {
            Debug.LogWarning($"[LandingDustSpawner] No dust sprite assigned! Dust will not render.");
        }
        
        // Pre-create dust pool
        for (int i = 0; i < maxDust; i++)
        {
            CreateDustParticle();
        }
    }
    
    private void OnDestroy()
    {
        // Clean up the container when spawner is destroyed
        if (dustContainer != null)
        {
            Destroy(dustContainer.gameObject);
        }
    }
    
    /// <summary>
    /// Auto-detect camera from multiple sources:
    /// 1. layerReferenceObject (player object) and its children
    /// 2. Camera.main
    /// 3. FindObjectOfType<Camera>() as fallback
    /// </summary>
    private void DetectCamera()
    {
        if (SceneCameraResolver.IsUsableFor(targetCamera, gameObject))
            return;

        targetCamera = null;
        
        // Priority 1: Check layerReferenceObject (player object) and its children
        if (layerReferenceObject != null)
        {
            Camera playerCam = layerReferenceObject.GetComponentInChildren<Camera>();
            if (playerCam != null)
            {
                targetCamera = playerCam;
                if (enableDebugLogs)
                {
                    Debug.Log($"[LandingDustSpawner] Auto-detected camera from layerReferenceObject '{layerReferenceObject.name}': {targetCamera.name}");
                }
                return;
            }
        }
        
        // Priority 2: Check parent hierarchy for camera
        Transform parent = transform.parent;
        while (parent != null)
        {
            Camera parentCam = parent.GetComponentInChildren<Camera>();
            if (parentCam != null)
            {
                targetCamera = parentCam;
                if (enableDebugLogs)
                {
                    Debug.Log($"[LandingDustSpawner] Auto-detected camera from parent '{parent.name}': {targetCamera.name}");
                }
                return;
            }
            parent = parent.parent;
        }
        
        // Priority 3: Gameplay camera (GameServices persistent rig when live)
        Camera gameplayCamera = SceneCameraResolver.ResolveGameplayCamera();
        if (gameplayCamera != null)
        {
            targetCamera = gameplayCamera;
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Auto-detected gameplay camera: {targetCamera.name}");
            }
            return;
        }
        
        // Priority 4: Find any camera in scene as fallback
        Camera[] cameras = FindObjectsOfType<Camera>();
        if (cameras != null && cameras.Length > 0)
        {
            // Prefer cameras that are enabled and active
            foreach (Camera cam in cameras)
            {
                if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
                {
                    targetCamera = cam;
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[LandingDustSpawner] Auto-detected camera from scene: {targetCamera.name}");
                    }
                    return;
                }
            }
            
            // If no enabled camera found, use first available
            targetCamera = cameras[0];
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[LandingDustSpawner] Auto-detected disabled camera from scene: {targetCamera.name}");
            }
        }
        
        if (targetCamera == null && enableDebugLogs)
        {
            Debug.LogWarning("[LandingDustSpawner] No camera found! Billboarding will not work.");
        }
    }
    
    private void DetectGameObjectLayer()
    {
        // Priority 1: Use layerReferenceObject if set
        if (layerReferenceObject != null)
        {
            resolvedGameObjectLayer = layerReferenceObject.layer;
            string refLayerName = LayerMask.LayerToName(resolvedGameObjectLayer);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Using layer from reference object '{layerReferenceObject.name}': {refLayerName} (index: {resolvedGameObjectLayer})");
            }
            return;
        }
        
        // Priority 2: If layer name is explicitly set, use it
        if (!string.IsNullOrEmpty(gameObjectLayerName))
        {
            resolvedGameObjectLayer = LayerMask.NameToLayer(gameObjectLayerName);
            if (resolvedGameObjectLayer == -1)
            {
                Debug.LogWarning($"[LandingDustSpawner] Layer '{gameObjectLayerName}' not found! Using parent layer.");
                resolvedGameObjectLayer = gameObject.layer;
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Using explicit layer: {gameObjectLayerName} (index: {resolvedGameObjectLayer})");
            }
            return;
        }
        
        // Priority 3: Auto-detect from this GameObject's layer
        resolvedGameObjectLayer = gameObject.layer;
        string detectedLayerName = LayerMask.LayerToName(resolvedGameObjectLayer);
        
        // If this GameObject is on Default layer, check parent
        if (resolvedGameObjectLayer == 0 && detectedLayerName == "Default")
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                resolvedGameObjectLayer = parent.gameObject.layer;
                detectedLayerName = LayerMask.LayerToName(resolvedGameObjectLayer);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[LandingDustSpawner] Auto-detected layer from parent: {detectedLayerName} (index: {resolvedGameObjectLayer})");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Using Default layer (no parent found)");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[LandingDustSpawner] Auto-detected layer from self: {detectedLayerName} (index: {resolvedGameObjectLayer})");
        }
    }
    
    private void DetectRendererSettings()
    {
        // Priority 1: Use layerReferenceObject if set
        if (layerReferenceObject != null)
        {
            // Try SpriteRenderer first
            SpriteRenderer refSpriteRenderer = layerReferenceObject.GetComponent<SpriteRenderer>();
            if (refSpriteRenderer != null)
            {
                resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? refSpriteRenderer.sortingLayerName : sortingLayerName;
                resolvedSortingOrder = sortingOrder == int.MinValue ? refSpriteRenderer.sortingOrder : sortingOrder;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[LandingDustSpawner] Using SpriteRenderer from reference object '{layerReferenceObject.name}' - Sorting Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
                }
                return;
            }
            
            // Try MeshRenderer
            MeshRenderer refMeshRenderer = layerReferenceObject.GetComponent<MeshRenderer>();
            if (refMeshRenderer != null)
            {
                resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
                resolvedSortingOrder = sortingOrder == int.MinValue ? refMeshRenderer.sortingOrder + 1 : sortingOrder;
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[LandingDustSpawner] Using MeshRenderer from reference object '{layerReferenceObject.name}' - Sorting Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {refMeshRenderer.sortingOrder})");
                }
                return;
            }
        }
        
        // Priority 2: Try to get sorting settings from SpriteRenderer first
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? spriteRenderer.sortingLayerName : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? spriteRenderer.sortingOrder : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Detected SpriteRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
            }
            return;
        }
        
        // Priority 3: Try MeshRenderer
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? meshRenderer.sortingOrder + 1 : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Detected MeshRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {meshRenderer.sortingOrder})");
            }
            return;
        }
        
        // Priority 4: Check parent for renderer
        SpriteRenderer parentSpriteRenderer = GetComponentInParent<SpriteRenderer>();
        if (parentSpriteRenderer != null)
        {
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? parentSpriteRenderer.sortingLayerName : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? parentSpriteRenderer.sortingOrder + 1 : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Detected parent SpriteRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
            }
            return;
        }
        
        MeshRenderer parentMeshRenderer = GetComponentInParent<MeshRenderer>();
        if (parentMeshRenderer != null)
        {
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? parentMeshRenderer.sortingOrder + 1 : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[LandingDustSpawner] Detected parent MeshRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {parentMeshRenderer.sortingOrder})");
            }
            return;
        }
        
        // Fallback to defaults
        resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
        resolvedSortingOrder = sortingOrder == int.MinValue ? 10 : sortingOrder;
        
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[LandingDustSpawner] No renderer found! Using defaults - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
        }
    }
    
    private DustParticle CreateDustParticle()
    {
        GameObject obj = new GameObject($"LandingDust_{dustPool.Count}");
        // Parent to static container in world space (NOT the player/spawner)
        obj.transform.SetParent(dustContainer, false);
        obj.SetActive(false);
        
        // Apply scale (important for small pixel sprites!)
        obj.transform.localScale = Vector3.one * dustScale;
        
        // Set Unity Layer (for camera culling)
        obj.layer = resolvedGameObjectLayer;
        
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = dustSprite;
        sr.color = dustColor;
        sr.sortingLayerName = resolvedSortingLayerName;
        sr.sortingOrder = resolvedSortingOrder;
        
        DustParticle particle = new DustParticle
        {
            gameObject = obj,
            spriteRenderer = sr,
            active = false
        };
        
        dustPool.Add(particle);
        return particle;
    }
    
    private DustParticle GetAvailableParticle()
    {
        // Clean up destroyed particles first
        CleanupDestroyedParticles();
        
        foreach (var particle in dustPool)
        {
            // Check if particle is valid and not active
            if (particle != null && particle.gameObject != null && !particle.active)
            {
                // Ensure spriteRenderer is valid
                if (particle.spriteRenderer == null)
                {
                    particle.spriteRenderer = particle.gameObject.GetComponent<SpriteRenderer>();
                    if (particle.spriteRenderer == null)
                    {
                        particle.spriteRenderer = particle.gameObject.AddComponent<SpriteRenderer>();
                    }
                }
                return particle;
            }
        }
        
        // Pool exhausted or all particles destroyed, create new one
        if (dustPool.Count < maxDust)
        {
            return CreateDustParticle();
        }
        
        // Pool at max capacity, reuse oldest active particle
        if (dustPool.Count > 0)
        {
            DustParticle oldest = dustPool[0];
            if (oldest != null && oldest.gameObject != null)
            {
                return oldest;
            }
            else
            {
                // Oldest is destroyed, create new one
                return CreateDustParticle();
            }
        }
        
        return CreateDustParticle();
    }
    
    /// <summary>
    /// Clean up destroyed particles from the pool.
    /// Called automatically, but can be called manually after scene transitions.
    /// </summary>
    private void CleanupDestroyedParticles()
    {
        for (int i = dustPool.Count - 1; i >= 0; i--)
        {
            DustParticle particle = dustPool[i];
            if (particle == null || particle.gameObject == null || particle.spriteRenderer == null)
            {
                dustPool.RemoveAt(i);
            }
        }
    }
    
    /// <summary>
    /// Spawn dust cloud at landing position based on fall height.
    /// </summary>
    /// <param name="landingPosition">World position where player landed</param>
    /// <param name="fallHeight">Height of fall (affects particle count)</param>
    public void SpawnDustCloud(Vector3 landingPosition, float fallHeight)
    {
        if (fallHeight < minFallHeightForDust)
            return; // Too small to spawn dust
        
        if (dustSprite == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[LandingDustSpawner] Cannot spawn dust - no sprite assigned!");
            }
            return;
        }
        
        // Calculate particle count based on fall height
        float normalizedFall = Mathf.InverseLerp(minFallHeightForDust, maxFallHeightForDust, fallHeight);
        int dustCount = Mathf.RoundToInt(baseDustCount * (1f + normalizedFall * 2f)); // 1x to 3x base count
        dustCount = Mathf.Clamp(dustCount, baseDustCount, maxDust);
        
        // Spawn position is at landing point (ground level)
        float spawnRadius = playerColliderRadius * spawnRadiusMultiplier;
        
        for (int i = 0; i < dustCount; i++)
        {
            DustParticle particle = GetAvailableParticle();
            if (particle == null || particle.gameObject == null) continue;
            
            // Ensure spriteRenderer exists and is valid
            if (particle.spriteRenderer == null)
            {
                particle.spriteRenderer = particle.gameObject.GetComponent<SpriteRenderer>();
                if (particle.spriteRenderer == null)
                {
                    particle.spriteRenderer = particle.gameObject.AddComponent<SpriteRenderer>();
                }
            }
            
            // Ensure sprite is assigned
            if (particle.spriteRenderer != null && particle.spriteRenderer.sprite == null)
            {
                particle.spriteRenderer.sprite = dustSprite;
            }
            
            // Spawn in circular pattern around landing point
            float angle = (i / (float)dustCount) * Mathf.PI * 2f; // Distribute evenly in circle
            float radius = Random.Range(spawnRadius * 0.5f, spawnRadius); // Random radius within spawn area
            Vector3 spawnOffset = new Vector3(
                Mathf.Cos(angle) * radius,
                0f, // Start at ground level
                Mathf.Sin(angle) * radius
            );
            
            Vector3 spawnPos = landingPosition + spawnOffset;
            particle.gameObject.transform.position = spawnPos;
            
            // Calculate velocity: initially outward (away from center), then upward
            Vector3 outwardDir = spawnOffset.normalized;
            float outwardSpeed = Random.Range(outwardVelocityRange.x, outwardVelocityRange.y);
            float upwardSpeed = Random.Range(upwardVelocityRange.x, upwardVelocityRange.y);
            
            // Scale velocity with fall height (bigger falls = faster initial burst)
            float velocityScale = 1f + (normalizedFall * 0.5f);
            
            // Store initial outward velocity for easing (XZ only)
            particle.initialOutwardVelocity = new Vector3(
                outwardDir.x * outwardSpeed * velocityScale,
                0f,
                outwardDir.z * outwardSpeed * velocityScale
            );
            particle.verticalVelocity = upwardSpeed * velocityScale;
            particle.driftVelocity = Vector3.zero;
            
            // Store fall height and calculate dynamic ease values
            particle.fallHeight = fallHeight;
            if (useDynamicEase)
            {
                // Calculate normalized fall height for ease scaling
                float normalizedFallForEase = Mathf.InverseLerp(minFallHeightForEase, maxFallHeightForEase, fallHeight);
                normalizedFallForEase = Mathf.Clamp01(normalizedFallForEase);
                
                // Interpolate ease duration and power based on fall height
                particle.easeDuration = Mathf.Lerp(minEaseDuration, maxEaseDuration, normalizedFallForEase);
                particle.easePower = Mathf.Lerp(minEasePower, maxEasePower, normalizedFallForEase);
            }
            else
            {
                // Use static values
                particle.easeDuration = staticOutwardEaseDuration;
                particle.easePower = staticOutwardEasePower;
            }
            
            particle.maxLifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
            particle.lifetime = particle.maxLifetime;
            particle.spawnTime = Time.time;
            particle.liftTimer = 0f;
            particle.hasBrokenUp = false;
            particle.active = true;
            
            // Reset color and ensure sprite is set (only if spriteRenderer is valid)
            if (particle.spriteRenderer != null)
            {
                particle.spriteRenderer.sprite = dustSprite;
                particle.spriteRenderer.color = dustColor;
                particle.spriteRenderer.sortingLayerName = resolvedSortingLayerName;
                particle.spriteRenderer.sortingOrder = resolvedSortingOrder;
            }
            else
            {
                // SpriteRenderer was destroyed, skip this particle
                particle.active = false;
                continue;
            }
            
            // Ensure Unity Layer is set
            particle.gameObject.layer = resolvedGameObjectLayer;
            
            // Ensure scale is set
            particle.gameObject.transform.localScale = Vector3.one * dustScale;
            
            particle.gameObject.SetActive(true);
        }
        
        if (enableDebugLogs)
        {
            string layerName = LayerMask.LayerToName(resolvedGameObjectLayer);
            Debug.Log($"[LandingDustSpawner] Spawned {dustCount} dust particles (fall height: {fallHeight:F2}), Layer: {layerName}");
        }
    }
    
    private void Update()
    {
        // Re-detect camera if it was null (in case it wasn't available at Awake time)
        if (!SceneCameraResolver.IsUsableFor(targetCamera, gameObject))
        {
            DetectCamera();
        }
        
        // Get camera for billboarding
        Camera cam = SceneCameraResolver.ResolveOrKeep(targetCamera, gameObject);
        
        // Update all active particles
        foreach (var particle in dustPool)
        {
            if (!particle.active) continue;
            
            // Skip destroyed particles
            if (particle == null || particle.gameObject == null || particle.spriteRenderer == null)
            {
                particle.active = false;
                continue;
            }
            
            float age = Time.time - particle.spawnTime;
            particle.liftTimer += Time.deltaTime;
            
            // === OUTWARD MOTION (XZ) with ease-out ===
            // Fast burst on impact, then slows down dramatically
            // Use per-particle ease duration and power (scaled with fall height)
            float outwardT = Mathf.Clamp01(age / particle.easeDuration);
            // Ease-out: 1 - t^power (starts at 1, ends at 0, fast decay initially)
            float outwardEase = 1f - Mathf.Pow(outwardT, particle.easePower);
            Vector3 currentOutwardVelocity = particle.initialOutwardVelocity * outwardEase;
            
            // === VERTICAL MOTION (Y) with physics ===
            // Apply gravity
            particle.verticalVelocity -= gravity * Time.deltaTime;
            
            // Apply lift force for initial duration (creates rising cloud effect)
            if (particle.liftTimer < liftDuration)
            {
                particle.verticalVelocity += liftForce * Time.deltaTime;
            }
            
            // Apply air resistance to vertical only
            particle.verticalVelocity *= verticalAirResistance;
            
            // === BREAKUP: Add slow random drift ===
            if (age >= breakupTime && !particle.hasBrokenUp)
            {
                particle.hasBrokenUp = true;
                
                // Add random drift velocity (slow wandering motion after breakup)
                particle.driftVelocity = new Vector3(
                    Random.Range(-breakupDriftStrength, breakupDriftStrength),
                    Random.Range(0f, breakupDriftStrength * 0.5f), // Slight upward bias
                    Random.Range(-breakupDriftStrength, breakupDriftStrength)
                );
            }
            
            // === COMBINE VELOCITIES AND UPDATE POSITION ===
            Vector3 totalVelocity = new Vector3(
                currentOutwardVelocity.x + particle.driftVelocity.x,
                particle.verticalVelocity + particle.driftVelocity.y,
                currentOutwardVelocity.z + particle.driftVelocity.z
            );
            
            Vector3 currentPos = particle.gameObject.transform.position;
            currentPos += totalVelocity * Time.deltaTime;
            particle.gameObject.transform.position = currentPos;
            
            // Billboard to face camera
            if (cam != null)
            {
                // Face the camera (Y-axis billboard - keeps sprite upright)
                Vector3 lookDir = cam.transform.forward;
                particle.gameObject.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
            }
            
            // Fade during breakup/lift phase (starts at fadeStartTime, ends at fadeStartTime + fadeDuration)
            if (age >= fadeStartTime)
            {
                float fadeProgress = (age - fadeStartTime) / fadeDuration;
                fadeProgress = Mathf.Clamp01(fadeProgress);
                float alpha = dustColor.a * (1f - fadeProgress);
                Color c = particle.spriteRenderer.color;
                c.a = alpha;
                particle.spriteRenderer.color = c;
            }
            
            // Update lifetime
            particle.lifetime -= Time.deltaTime;
            
            // Deactivate when dead (either lifetime expired or fully faded)
            if (particle.lifetime <= 0f || (age >= fadeStartTime + fadeDuration))
            {
                particle.active = false;
                particle.gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Set the dust sprite at runtime.
    /// </summary>
    public void SetDustSprite(Sprite sprite)
    {
        dustSprite = sprite;
        foreach (var particle in dustPool)
        {
            if (particle != null && particle.spriteRenderer != null)
            {
                particle.spriteRenderer.sprite = sprite;
            }
        }
    }
    
    /// <summary>
    /// Set the dust color at runtime.
    /// </summary>
    public void SetDustColor(Color color)
    {
        dustColor = color;
    }
    
    /// <summary>
    /// Force refresh renderer settings (call if renderer changes at runtime).
    /// </summary>
    public void RefreshRendererSettings()
    {
        DetectGameObjectLayer();
        DetectRendererSettings();
        
        // Clean up destroyed particles first
        CleanupDestroyedParticles();
        
        // Update all existing particles
        foreach (var particle in dustPool)
        {
            if (particle != null && particle.gameObject != null)
            {
                if (particle.spriteRenderer != null)
                {
                    particle.spriteRenderer.sortingLayerName = resolvedSortingLayerName;
                    particle.spriteRenderer.sortingOrder = resolvedSortingOrder;
                }
                particle.gameObject.layer = resolvedGameObjectLayer;
            }
        }
    }
    
    /// <summary>
    /// Refresh the dust spawner after scene transitions.
    /// Cleans up destroyed particles and ensures container exists.
    /// Call this from PlayerMovement3D after scene transitions.
    /// </summary>
    public void RefreshAfterSceneTransition()
    {
        if (enableDebugLogs)
        {
            Debug.Log("[LandingDustSpawner] Refreshing after scene transition");
        }
        
        // Clean up destroyed particles
        CleanupDestroyedParticles();
        
        // Ensure container exists
        if (dustContainer == null)
        {
            GameObject existingContainer = GameObject.Find("LandingDustContainer");
            if (existingContainer != null)
            {
                dustContainer = existingContainer.transform;
            }
            else
            {
                GameObject containerObj = new GameObject("LandingDustContainer");
                containerObj.transform.SetParent(null);
                dustContainer = containerObj.transform;
            }
        }
        
        // Re-parent any valid particles to the container
        foreach (var particle in dustPool)
        {
            if (particle != null && particle.gameObject != null && dustContainer != null)
            {
                particle.gameObject.transform.SetParent(dustContainer, false);
            }
        }
        
        // Refresh renderer settings
        RefreshRendererSettings();
        
        // Re-detect camera
        DetectCamera();
    }
}

