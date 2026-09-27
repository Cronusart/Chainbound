using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns pixel-perfect grass debris (dandelion seeds, leaf bits) when player moves through grass.
/// Uses pooled SpriteRenderers for performance and maintains pixel-perfect rendering.
/// </summary>
public class GrassDebrisSpawner : MonoBehaviour
{
    [Header("Debris Settings")]
    [Tooltip("Sprite to use for debris particles (1x1 or small pixel sprite)")]
    [SerializeField] private Sprite debrisSprite;
    
    [Tooltip("Color tint for debris particles")]
    [SerializeField] private Color debrisColor = new Color(1f, 1f, 0.9f, 0.8f);
    
    [Tooltip("Scale multiplier for debris particles. For 1x1 pixel sprites at 100 PPU, try 5.0 to match x5 scaled objects.")]
    [SerializeField] private float debrisScale = 5f;
    
    [Tooltip("Number of debris to spawn per trigger")]
    [SerializeField] private int debrisPerSpawn = 3;
    
    [Tooltip("Maximum debris particles active at once")]
    [SerializeField] private int maxDebris = 20;
    
    [Header("Movement")]
    [Tooltip("Initial upward velocity range")]
    [SerializeField] private Vector2 upwardVelocityRange = new Vector2(0.5f, 1.5f);
    
    [Tooltip("Horizontal drift range (random per particle)")]
    [SerializeField] private Vector2 horizontalDriftRange = new Vector2(-0.3f, 0.3f);
    
    [Tooltip("Gravity strength (downward acceleration)")]
    [SerializeField] private float gravity = 0.5f;
    
    [Tooltip("Air resistance (velocity damping)")]
    [SerializeField] private float airResistance = 0.98f;
    
    [Tooltip("Sway frequency (side-to-side floating) - higher = faster oscillation")]
    [SerializeField] private float swayFrequency = 3f;
    
    [Tooltip("Sway amplitude (X-axis only for top-down view) - how far debris drifts side-to-side")]
    [SerializeField] private float swayAmplitude = 0.8f;
    
    [Tooltip("Sway ease-in duration (seconds) - feather starts slow, then speeds up")]
    [SerializeField] private float swayEaseInDuration = 0.3f;
    
    [Tooltip("Sway ease-out duration (seconds) - feather slows down near end of life")]
    [SerializeField] private float swayEaseOutDuration = 0.5f;
    
    [Header("Lifetime")]
    [Tooltip("Debris lifetime in seconds")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(1.5f, 3f);
    
    [Tooltip("Fade out duration at end of life")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    
    [Header("Spawn Trigger")]
    [Tooltip("Minimum player speed to spawn debris (no debris when standing still)")]
    [SerializeField] private float minSpawnSpeed = 0.5f;
    
    [Tooltip("Speed at which maximum debris is spawned (running speed)")]
    [SerializeField] private float maxSpawnSpeed = 3f;
    
    [Tooltip("Base cooldown between spawns (seconds) - scales with speed")]
    [SerializeField] private float spawnCooldown = 0.2f;
    
    [Tooltip("Minimum cooldown at max speed (seconds)")]
    [SerializeField] private float minCooldown = 0.08f;
    
    [Tooltip("How much player velocity affects debris velocity (0 = none, 1 = full)")]
    [SerializeField] private float velocityInfluence = 0.5f;
    
    [Tooltip("Enable debug logs for debris spawning")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Header("Rendering")]
    [Tooltip("Reference GameObject to derive all layer settings from (e.g., the grass patch GameObject). If set, this takes priority over auto-detection.")]
    [SerializeField] private GameObject layerReferenceObject;
    
    [Tooltip("Unity Layer name for debris (auto-detected from layerReferenceObject or parent if not set)")]
    [SerializeField] private string gameObjectLayerName = "";
    
    [Tooltip("Sorting layer for debris (auto-detected from layerReferenceObject's renderer if not set)")]
    [SerializeField] private string sortingLayerName = "";
    
    [Tooltip("Sorting order for debris (auto-detected from layerReferenceObject's renderer if not set)")]
    [SerializeField] private int sortingOrder = int.MinValue;
    
    // Debris particle data
    private class DebrisParticle
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public Vector3 velocity;
        public float lifetime;
        public float maxLifetime;
        public float swayOffset;
        public float spawnTime; // Track when particle was spawned for easing
        public bool active;
    }
    
    private List<DebrisParticle> debrisPool = new List<DebrisParticle>();
    private float lastSpawnTime = 0f;
    private Transform playerTransform;
    private Vector3 lastPlayerPosition;
    private bool playerInTrigger = false;
    private string resolvedSortingLayerName;
    private int resolvedSortingOrder;
    private int resolvedGameObjectLayer; // Unity Layer (for camera culling)
    private GrassPatchManager grassManager; // Reference to parent grass manager for shared trigger
    
    private void Awake()
    {
        // Detect Unity Layer from parent GameObject (for camera culling)
        DetectGameObjectLayer();
        
        // Detect sorting layer and order from attached renderer
        DetectRendererSettings();
        
        // Get reference to GrassPatchManager (should be on same GameObject or parent)
        grassManager = GetComponent<GrassPatchManager>();
        if (grassManager == null)
        {
            grassManager = GetComponentInParent<GrassPatchManager>();
        }
        
        // Warn if no sprite assigned
        if (debrisSprite == null && enableDebugLogs)
        {
            Debug.LogWarning($"[GrassDebrisSpawner] No debris sprite assigned! Debris will not render.");
        }
        
        // Pre-create debris pool
        for (int i = 0; i < maxDebris; i++)
        {
            CreateDebrisParticle();
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
                Debug.Log($"[GrassDebrisSpawner] Using layer from reference object '{layerReferenceObject.name}': {refLayerName} (index: {resolvedGameObjectLayer})");
            }
            return;
        }
        
        // Priority 2: If layer name is explicitly set, use it
        if (!string.IsNullOrEmpty(gameObjectLayerName))
        {
            resolvedGameObjectLayer = LayerMask.NameToLayer(gameObjectLayerName);
            if (resolvedGameObjectLayer == -1)
            {
                Debug.LogWarning($"[GrassDebrisSpawner] Layer '{gameObjectLayerName}' not found! Using parent layer.");
                resolvedGameObjectLayer = gameObject.layer;
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Using explicit layer: {gameObjectLayerName} (index: {resolvedGameObjectLayer})");
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
                    Debug.Log($"[GrassDebrisSpawner] Auto-detected layer from parent: {detectedLayerName} (index: {resolvedGameObjectLayer})");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Using Default layer (no parent found)");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[GrassDebrisSpawner] Auto-detected layer from self: {detectedLayerName} (index: {resolvedGameObjectLayer})");
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
                    Debug.Log($"[GrassDebrisSpawner] Using SpriteRenderer from reference object '{layerReferenceObject.name}' - Sorting Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
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
                    Debug.Log($"[GrassDebrisSpawner] Using MeshRenderer from reference object '{layerReferenceObject.name}' - Sorting Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {refMeshRenderer.sortingOrder})");
                }
                return;
            }
            
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[GrassDebrisSpawner] Reference object '{layerReferenceObject.name}' has no renderer! Falling back to auto-detection.");
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
                Debug.Log($"[GrassDebrisSpawner] Detected SpriteRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
            }
            return;
        }
        
        // Priority 3: Try MeshRenderer (for 3D sprites/quads) - this is what grass uses
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            // MeshRenderer uses sortingOrder but not sortingLayerName
            // Use default layer but get order from renderer
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? meshRenderer.sortingOrder + 1 : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Detected MeshRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {meshRenderer.sortingOrder})");
            }
            return;
        }
        
        // Priority 4: Check parent for renderer (most common case - debris spawner is child of grass)
        SpriteRenderer parentSpriteRenderer = GetComponentInParent<SpriteRenderer>();
        if (parentSpriteRenderer != null)
        {
            resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? parentSpriteRenderer.sortingLayerName : sortingLayerName;
            resolvedSortingOrder = sortingOrder == int.MinValue ? parentSpriteRenderer.sortingOrder + 1 : sortingOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Detected parent SpriteRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
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
                Debug.Log($"[GrassDebrisSpawner] Detected parent MeshRenderer - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder} (from {parentMeshRenderer.sortingOrder})");
            }
            return;
        }
        
        // Fallback to defaults
        resolvedSortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
        resolvedSortingOrder = sortingOrder == int.MinValue ? 10 : sortingOrder;
        
        if (enableDebugLogs)
        {
            Debug.LogWarning($"[GrassDebrisSpawner] No renderer found! Using defaults - Layer: {resolvedSortingLayerName}, Order: {resolvedSortingOrder}");
        }
    }
    
    private DebrisParticle CreateDebrisParticle()
    {
        GameObject obj = new GameObject($"GrassDebris_{debrisPool.Count}");
        obj.transform.SetParent(transform, false); // false = keep local transform, don't preserve world position
        obj.SetActive(false);
        
        // Apply scale (important for small pixel sprites!)
        obj.transform.localScale = Vector3.one * debrisScale;
        
        // Set Unity Layer (for camera culling) - CRITICAL for visibility!
        obj.layer = resolvedGameObjectLayer;
        
        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = debrisSprite; // May be null initially, will be set when spawning
        sr.color = debrisColor;
        sr.sortingLayerName = resolvedSortingLayerName;
        sr.sortingOrder = resolvedSortingOrder;
        
        DebrisParticle particle = new DebrisParticle
        {
            gameObject = obj,
            spriteRenderer = sr,
            active = false
        };
        
        debrisPool.Add(particle);
        return particle;
    }
    
    private DebrisParticle GetAvailableParticle()
    {
        foreach (var particle in debrisPool)
        {
            if (!particle.active)
                return particle;
        }
        
        // Pool exhausted, reuse oldest
        if (debrisPool.Count > 0)
        {
            return debrisPool[0];
        }
        
        return CreateDebrisParticle();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Use same player detection logic as GrassPatchManager
        if (IsPlayer(other.gameObject))
        {
            playerTransform = other.transform;
            lastPlayerPosition = playerTransform.position;
            playerInTrigger = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Player entered trigger: {other.gameObject.name}");
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayer(other.gameObject))
            return;
            
        if (!playerInTrigger || playerTransform == null)
        {
            // Re-initialize if needed
            playerTransform = other.transform;
            lastPlayerPosition = playerTransform.position;
            playerInTrigger = true;
        }
        
        // Calculate player speed
        Vector3 currentPos = playerTransform.position;
        Vector3 delta = currentPos - lastPlayerPosition;
        float deltaTime = Time.deltaTime;
        if (deltaTime < 0.001f) return;
        
        float speed = delta.magnitude / deltaTime;
        lastPlayerPosition = currentPos;
        
        // No debris when standing still or moving too slowly
        if (speed < minSpawnSpeed)
            return;
        
        // Calculate dynamic cooldown based on speed (faster = shorter cooldown)
        float speedNormalized = Mathf.InverseLerp(minSpawnSpeed, maxSpawnSpeed, speed);
        float dynamicCooldown = Mathf.Lerp(spawnCooldown, minCooldown, speedNormalized);
        
        // Check if cooldown has passed
        if (Time.time - lastSpawnTime < dynamicCooldown)
            return;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[GrassDebrisSpawner] Spawning debris! Speed: {speed:F2}, Cooldown: {dynamicCooldown:F3}s, Direction: {delta.normalized}");
        }
        
        // Spawn debris with velocity information
        SpawnDebris(currentPos, delta.normalized, speed);
        lastSpawnTime = Time.time;
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            playerInTrigger = false;
            playerTransform = null;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[GrassDebrisSpawner] Player exited trigger: {other.gameObject.name}");
            }
        }
    }
    
    private bool IsPlayer(GameObject obj)
    {
        // Use same logic as GrassPatchManager
        if (obj.CompareTag("Player"))
            return true;
            
        if (obj.GetComponent<PlayerMovement3D>() != null)
            return true;
            
        // Check if it's a child of player root (via reflection or direct access)
        if (grassManager != null)
        {
            // Check if the grass manager has a player object reference
            // We'll use reflection to access private field, or just check tag/component
            // For now, rely on tag and component checks above
        }
        
        return false;
    }
    
    private void SpawnDebris(Vector3 playerPosition, Vector3 moveDirection, float playerSpeed = 1f)
    {
        // Check if sprite is assigned
        if (debrisSprite == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[GrassDebrisSpawner] Cannot spawn debris - no sprite assigned!");
            }
            return;
        }
        
        // Calculate speed factor (0 to 1) for scaling effects
        float speedFactor = Mathf.InverseLerp(minSpawnSpeed, maxSpawnSpeed, playerSpeed);
        
        // Scale debris count based on speed (at least 1, up to debrisPerSpawn at max speed)
        int actualDebrisCount = Mathf.Max(1, Mathf.RoundToInt(debrisPerSpawn * speedFactor));
        
        // Spawn position is offset upward from grass base
        Vector3 spawnBase = transform.position;
        spawnBase.y = playerPosition.y + 2f; // Offset +2 units upward from player position
        
        for (int i = 0; i < actualDebrisCount; i++)
        {
            DebrisParticle particle = GetAvailableParticle();
            if (particle == null) continue;
            
            // Ensure sprite is assigned
            if (particle.spriteRenderer.sprite == null)
            {
                particle.spriteRenderer.sprite = debrisSprite;
            }
            
            // Randomize spawn position around grass (spread increases with speed)
            float spreadFactor = 0.2f + (speedFactor * 0.2f);
            Vector3 spawnPos = spawnBase + new Vector3(
                Random.Range(-0.5f, 0.5f), // X variation for randomness
                Random.Range(0f, 0.3f),     // Small Y variation for natural spread
                Random.Range(-spreadFactor, spreadFactor) // Z spread based on speed
            );
            
            particle.gameObject.transform.position = spawnPos;
            
            // Calculate base velocity - mostly upward with some drift
            float upwardSpeed = Random.Range(upwardVelocityRange.x, upwardVelocityRange.y);
            float horizontalDrift = Random.Range(horizontalDriftRange.x, horizontalDriftRange.y);
            
            // Scale velocity based on player speed
            float velocityScale = 1f + (speedFactor * velocityInfluence);
            upwardSpeed *= velocityScale;
            
            // Add velocity in player's movement direction (more at higher speeds)
            float directionInfluence = 0.2f + (speedFactor * 0.4f * velocityInfluence);
            Vector3 driftDir = new Vector3(
                moveDirection.x * directionInfluence + horizontalDrift,
                0f,
                moveDirection.z * directionInfluence + Random.Range(horizontalDriftRange.x, horizontalDriftRange.y)
            );
            
            particle.velocity = new Vector3(driftDir.x * velocityScale, upwardSpeed, driftDir.z * velocityScale);
            particle.maxLifetime = Random.Range(lifetimeRange.x, lifetimeRange.y);
            particle.lifetime = particle.maxLifetime;
            particle.swayOffset = Random.Range(0f, Mathf.PI * 2f);
            particle.spawnTime = Time.time; // Track spawn time for easing
            particle.active = true;
            
            // Reset color and ensure sprite is set
            particle.spriteRenderer.sprite = debrisSprite;
            particle.spriteRenderer.color = debrisColor;
            particle.spriteRenderer.sortingLayerName = resolvedSortingLayerName;
            particle.spriteRenderer.sortingOrder = resolvedSortingOrder;
            
            // Ensure Unity Layer is set (for camera culling)
            particle.gameObject.layer = resolvedGameObjectLayer;
            
            // Ensure scale is set (critical for small pixel sprites!)
            particle.gameObject.transform.localScale = Vector3.one * debrisScale;
            
            particle.gameObject.SetActive(true);
        }
        
        if (enableDebugLogs)
        {
            string layerName = LayerMask.LayerToName(resolvedGameObjectLayer);
            Debug.Log($"[GrassDebrisSpawner] Spawned {actualDebrisCount} debris (speed factor: {speedFactor:F2}), Layer: {layerName}");
        }
    }
    
    private void Update()
    {
        // Update all active particles
        foreach (var particle in debrisPool)
        {
            if (!particle.active) continue;
            
            // Apply physics
            particle.velocity.y -= gravity * Time.deltaTime;
            particle.velocity *= airResistance;
            
            // Calculate ease-in/ease-out factor for feather-like falling
            float age = Time.time - particle.spawnTime;
            float lifetimeProgress = 1f - (particle.lifetime / particle.maxLifetime);
            
            // Ease-in: start slow, accelerate
            float easeInFactor = 1f;
            if (age < swayEaseInDuration)
            {
                float easeInT = age / swayEaseInDuration;
                easeInFactor = easeInT * easeInT; // Quadratic ease-in
            }
            
            // Ease-out: slow down near end of life
            float easeOutFactor = 1f;
            if (particle.lifetime < swayEaseOutDuration)
            {
                float easeOutT = particle.lifetime / swayEaseOutDuration;
                easeOutFactor = easeOutT * easeOutT; // Quadratic ease-out
            }
            
            // Combine ease factors
            float totalEaseFactor = easeInFactor * easeOutFactor;
            
            // Calculate X-axis sway with easing (feather-like drift)
            float swayPhase = Time.time * swayFrequency + particle.swayOffset;
            float sway = Mathf.Sin(swayPhase) * swayAmplitude * totalEaseFactor;
            
            // Apply sway directly to X position (not multiplied by deltaTime for more visible effect)
            Vector3 currentPos = particle.gameObject.transform.position;
            currentPos.x += sway * Time.deltaTime;
            
            // Update position with physics velocity
            currentPos += particle.velocity * Time.deltaTime;
            
            particle.gameObject.transform.position = currentPos;
            
            // Update lifetime
            particle.lifetime -= Time.deltaTime;
            
            // Fade out near end of life
            if (particle.lifetime <= fadeOutDuration)
            {
                float alpha = particle.lifetime / fadeOutDuration;
                Color c = particle.spriteRenderer.color;
                c.a = debrisColor.a * alpha;
                particle.spriteRenderer.color = c;
            }
            
            // Deactivate when dead
            if (particle.lifetime <= 0f)
            {
                particle.active = false;
                particle.gameObject.SetActive(false);
            }
        }
    }
    
    /// <summary>
    /// Manually spawn debris at a specific position.
    /// </summary>
    /// <param name="position">World position to spawn debris</param>
    /// <param name="direction">Direction of movement (normalized)</param>
    /// <param name="speed">Player speed (affects debris count and velocity)</param>
    public void SpawnDebrisAt(Vector3 position, Vector3 direction, float speed = 2f)
    {
        SpawnDebris(position, direction, speed);
    }
    
    /// <summary>
    /// Set the debris sprite at runtime.
    /// </summary>
    public void SetDebrisSprite(Sprite sprite)
    {
        debrisSprite = sprite;
        foreach (var particle in debrisPool)
        {
            particle.spriteRenderer.sprite = sprite;
        }
    }
    
    /// <summary>
    /// Set the debris color at runtime.
    /// </summary>
    public void SetDebrisColor(Color color)
    {
        debrisColor = color;
    }
    
    /// <summary>
    /// Manually update debris spawner when player moves (called from GrassPatchManager if needed).
    /// </summary>
    public void OnPlayerMovement(Vector3 playerPosition, Vector3 velocity)
    {
        if (!playerInTrigger)
        {
            playerInTrigger = true;
            playerTransform = null; // Will be set by trigger
        }
        
        float speed = velocity.magnitude;
        
        // No debris when standing still
        if (speed < minSpawnSpeed)
            return;
        
        // Calculate dynamic cooldown based on speed
        float speedFactor = Mathf.InverseLerp(minSpawnSpeed, maxSpawnSpeed, speed);
        float dynamicCooldown = Mathf.Lerp(spawnCooldown, minCooldown, speedFactor);
        
        if (Time.time - lastSpawnTime >= dynamicCooldown)
        {
            SpawnDebris(playerPosition, velocity.normalized, speed);
            lastSpawnTime = Time.time;
        }
    }
    
    /// <summary>
    /// Force refresh renderer settings (call if renderer changes at runtime).
    /// </summary>
    public void RefreshRendererSettings()
    {
        DetectGameObjectLayer();
        DetectRendererSettings();
        
        // Update all existing particles
        foreach (var particle in debrisPool)
        {
            if (particle.spriteRenderer != null)
            {
                particle.spriteRenderer.sortingLayerName = resolvedSortingLayerName;
                particle.spriteRenderer.sortingOrder = resolvedSortingOrder;
            }
            if (particle.gameObject != null)
            {
                particle.gameObject.layer = resolvedGameObjectLayer;
            }
        }
    }
}

