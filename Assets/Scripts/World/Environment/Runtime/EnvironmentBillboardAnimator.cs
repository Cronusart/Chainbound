using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager for environment billboard animations.
/// Ticks at fixed intervals (4-6 FPS) instead of per-frame for CPU optimization.
/// Only updates instances within camera distance and optionally only visible ones.
/// 
/// Optimized for Solana PSG1: batches updates, uses shared materials, supports LOD.
/// </summary>
public class EnvironmentBillboardAnimator : MonoBehaviour
{
    [Header("Update Settings")]
    [Tooltip("Update interval in seconds (0.15-0.25s = 4-6 FPS effective animation)")]
    [SerializeField] private float updateInterval = 0.2f; // 5 FPS
    
    [Header("Culling")]
    [Tooltip("Maximum distance from camera to update animations (LOD cutoff)")]
    [SerializeField] private float maxUpdateDistance = 30f;
    
    [Tooltip("Only update instances visible to camera (frustum culling)")]
    [SerializeField] private bool useFrustumCulling = true;
    
    [Tooltip("LOD distance - instances beyond this freeze at frame 0")]
    [SerializeField] private float lodDistance = 25f;
    
    [Header("Camera")]
    [Tooltip("Target camera for distance/frustum calculations. If null, uses Camera.main.")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Shared Material")]
    [Tooltip("Shared material for all environment billboards (alpha-cutout / depth-write).")]
    [SerializeField] private Material sharedMaterial;
    
    [Header("Auto-Registration")]
    [Tooltip("Automatically find and register all EnvironmentBillboardInstance components in scene")]
    [SerializeField] private bool autoRegisterOnStart = true;
    
    [Tooltip("Search for instances in children of this GameObject")]
    [SerializeField] private bool searchInChildren = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showGizmos = false;

    // Instance registry
    private List<EnvironmentBillboardInstance> instances = new List<EnvironmentBillboardInstance>();
    private HashSet<EnvironmentBillboardInstance> registeredInstances = new HashSet<EnvironmentBillboardInstance>();
    
    // Update state
    private Coroutine updateCoroutine;
    private float lastUpdateTime = 0f;
    private int lastUpdatedCount = 0;
    private int lastCulledCount = 0;

    private void Awake()
    {
        FindCamera();
    }

    private void Start()
    {
        if (autoRegisterOnStart)
        {
            RegisterAllInstances();
        }
        
        // Apply shared material to all registered instances
        if (sharedMaterial != null)
        {
            ApplySharedMaterial();
        }
        
        // Start update coroutine
        StartUpdateLoop();
    }

    private void OnDestroy()
    {
        StopUpdateLoop();
    }

    /// <summary>
    /// Find target camera (auto-find if not assigned).
    /// </summary>
    private void FindCamera()
    {
        targetCamera = SceneCameraResolver.ResolveOrKeep(targetCamera, gameObject);
        
        if (targetCamera == null && enableDebugLogs)
        {
            Debug.LogWarning("[EnvironmentBillboardAnimator] No camera found! Distance culling will not work.");
        }
    }

    /// <summary>
    /// Register all EnvironmentBillboardInstance components in scene.
    /// </summary>
    public void RegisterAllInstances()
    {
        instances.Clear();
        registeredInstances.Clear();
        
        if (searchInChildren)
        {
            // Search in children
            EnvironmentBillboardInstance[] found = GetComponentsInChildren<EnvironmentBillboardInstance>();
            foreach (var instance in found)
            {
                RegisterInstance(instance);
            }
        }
        else
        {
            // Search entire scene
            EnvironmentBillboardInstance[] found = FindObjectsOfType<EnvironmentBillboardInstance>();
            foreach (var instance in found)
            {
                RegisterInstance(instance);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[EnvironmentBillboardAnimator] Registered {instances.Count} environment billboard instances.");
        }
    }

    /// <summary>
    /// Register a single instance.
    /// </summary>
    public void RegisterInstance(EnvironmentBillboardInstance instance)
    {
        if (instance == null) return;
        
        if (!registeredInstances.Contains(instance))
        {
            instances.Add(instance);
            registeredInstances.Add(instance);
            
            // Apply shared material if set
            if (sharedMaterial != null)
            {
                instance.SetSharedMaterial(sharedMaterial);
            }
        }
    }

    /// <summary>
    /// Unregister an instance.
    /// </summary>
    public void UnregisterInstance(EnvironmentBillboardInstance instance)
    {
        if (instance == null) return;
        
        if (registeredInstances.Remove(instance))
        {
            instances.Remove(instance);
        }
    }

    /// <summary>
    /// Apply shared material to all registered instances.
    /// </summary>
    public void ApplySharedMaterial()
    {
        if (sharedMaterial == null)
        {
            Debug.LogWarning("[EnvironmentBillboardAnimator] Cannot apply null shared material!");
            return;
        }
        
        foreach (var instance in instances)
        {
            if (instance != null)
            {
                instance.SetSharedMaterial(sharedMaterial);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[EnvironmentBillboardAnimator] Applied shared material '{sharedMaterial.name}' to {instances.Count} instances.");
        }
    }

    /// <summary>
    /// Start the update loop coroutine.
    /// </summary>
    public void StartUpdateLoop()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        
        updateCoroutine = StartCoroutine(UpdateLoopCoroutine());
        
        if (enableDebugLogs)
        {
            Debug.Log($"[EnvironmentBillboardAnimator] Started update loop with interval {updateInterval}s ({1f / updateInterval:F1} FPS)");
        }
    }

    /// <summary>
    /// Stop the update loop coroutine.
    /// </summary>
    public void StopUpdateLoop()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
            updateCoroutine = null;
        }
    }

    /// <summary>
    /// Main update loop coroutine - ticks at fixed intervals.
    /// </summary>
    private IEnumerator UpdateLoopCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            
            UpdateAllInstances();
        }
    }

    /// <summary>
    /// Update all instances (called at fixed intervals).
    /// </summary>
    private void UpdateAllInstances()
    {
        if (!SceneCameraResolver.IsUsableFor(targetCamera, gameObject))
        {
            FindCamera();
            if (targetCamera == null)
            {
                // No camera - update all instances without culling
                UpdateInstancesBatch(instances, updateInterval);
                return;
            }
        }
        
        // Separate instances by distance
        List<EnvironmentBillboardInstance> withinDistance = new List<EnvironmentBillboardInstance>();
        List<EnvironmentBillboardInstance> beyondDistance = new List<EnvironmentBillboardInstance>();
        
        foreach (var instance in instances)
        {
            if (instance == null) continue;
            
            float distance = instance.GetDistanceFromCamera(targetCamera);
            
            // LOD: set freeze state
            bool withinLOD = distance <= lodDistance;
            instance.SetLODState(withinLOD);
            
            // Culling: skip if beyond max update distance
            if (distance > maxUpdateDistance)
            {
                beyondDistance.Add(instance);
                continue;
            }
            
            // Frustum culling: skip if not visible
            if (useFrustumCulling && !instance.IsVisibleToCamera(targetCamera))
            {
                beyondDistance.Add(instance);
                continue;
            }
            
            withinDistance.Add(instance);
        }
        
        // Update only instances within distance and visible
        UpdateInstancesBatch(withinDistance, updateInterval);
        
        // Update stats
        lastUpdatedCount = withinDistance.Count;
        lastCulledCount = instances.Count - withinDistance.Count;
        lastUpdateTime = Time.time;
        
        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[EnvironmentBillboardAnimator] Updated {lastUpdatedCount}/{instances.Count} instances (culled: {lastCulledCount})");
        }
    }

    /// <summary>
    /// Update a batch of instances.
    /// </summary>
    private void UpdateInstancesBatch(List<EnvironmentBillboardInstance> batch, float deltaTime)
    {
        foreach (var instance in batch)
        {
            if (instance != null && instance.HasAnimation())
            {
                instance.UpdateAnimation(deltaTime);
            }
        }
    }

    /// <summary>
    /// Set update interval at runtime.
    /// </summary>
    public void SetUpdateInterval(float interval)
    {
        updateInterval = Mathf.Max(0.05f, interval); // Minimum 20 FPS
        
        // Restart coroutine with new interval
        if (updateCoroutine != null)
        {
            StopUpdateLoop();
            StartUpdateLoop();
        }
    }

    /// <summary>
    /// Set max update distance at runtime.
    /// </summary>
    public void SetMaxUpdateDistance(float distance)
    {
        maxUpdateDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// Set LOD distance at runtime.
    /// </summary>
    public void SetLODDistance(float distance)
    {
        lodDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// Get number of registered instances.
    /// </summary>
    public int GetInstanceCount() => instances.Count;

    /// <summary>
    /// Get number of instances updated last frame.
    /// </summary>
    public int GetLastUpdatedCount() => lastUpdatedCount;

    /// <summary>
    /// Get number of instances culled last frame.
    /// </summary>
    public int GetLastCulledCount() => lastCulledCount;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos || targetCamera == null) return;
        
        // Draw max update distance sphere
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetCamera.transform.position, maxUpdateDistance);
        
        // Draw LOD distance sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(targetCamera.transform.position, lodDistance);
    }
#endif
}

