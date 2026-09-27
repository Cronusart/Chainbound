using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades occluding objects between camera and player to maintain player visibility.
/// Uses raycasts/spherecasts from camera to player to detect occluders.
/// Optimized for mobile: uses MaterialPropertyBlock, caches renderers, updates at configurable interval.
/// </summary>
public class FadeOccludersBetweenCameraAndPlayer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector3 playerVisibilityOffset = new Vector3(0f, 1f, 0f); // Height offset for visibility point
    
    [Header("Occlusion Detection")]
    [SerializeField] private LayerMask occluderLayerMask = -1; // Which layers can occlude
    [SerializeField] private float sphereCastRadius = 0.2f; // Radius for spherecast (more stable than raycast)
    [SerializeField] private float maxDistance = 50f; // Max distance to check
    
    [Header("Fade Settings")]
    [SerializeField] private float fadeAlpha = 0.2f; // Target alpha when occluding
    [SerializeField] private float fadeDuration = 0.25f; // Fade in/out duration
    
    [Header("Performance")]
    [SerializeField] private float updateInterval = 0.033f; // ~30 Hz (update every 33ms)
    [SerializeField] private int maxOccluders = 20; // Limit number of occluders to track
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawDebugRays = false;

    // Cached components and state
    private Dictionary<Renderer, FadeableRenderer> occluderFadeables = new Dictionary<Renderer, FadeableRenderer>();
    private Dictionary<Renderer, Coroutine> activeFadeCoroutines = new Dictionary<Renderer, Coroutine>();
    private HashSet<Renderer> currentlyOccluding = new HashSet<Renderer>();
    private HashSet<Renderer> previouslyOccluding = new HashSet<Renderer>();
    private float lastUpdateTime = 0f;

    private void Awake()
    {
        // Auto-find player if not assigned
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
            else
                Debug.LogWarning("[FadeOccludersBetweenCameraAndPlayer] Player transform not assigned and no GameObject with 'Player' tag found.");
        }

        // Auto-find camera if not assigned
        if (targetCamera == null || !SceneCameraResolver.IsUsableFor(targetCamera, gameObject))
        {
            targetCamera = SceneCameraResolver.ResolveGameplayCamera();
            
            if (targetCamera == null)
                Debug.LogWarning("[FadeOccludersBetweenCameraAndPlayer] Camera not assigned and no Camera found in scene.");
        }
    }

    private void LateUpdate()
    {
        // Update at configurable interval to reduce cost
        if (Time.time - lastUpdateTime < updateInterval)
            return;

        lastUpdateTime = Time.time;
        CheckOcclusion();
    }

    private void CheckOcclusion()
    {
        if (playerTransform == null || targetCamera == null)
            return;

        // Calculate visibility point on player
        Vector3 playerVisibilityPoint = playerTransform.position + playerVisibilityOffset;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 direction = playerVisibilityPoint - cameraPosition;
        float distance = direction.magnitude;

        if (distance > maxDistance)
            return;

        direction.Normalize();

        // Use spherecast for more stable occlusion detection
        RaycastHit[] hits = Physics.SphereCastAll(
            cameraPosition,
            sphereCastRadius,
            direction,
            distance,
            occluderLayerMask,
            QueryTriggerInteraction.Ignore
        );

        // Track which renderers are currently occluding
        currentlyOccluding.Clear();

        int occluderCount = 0;
        foreach (RaycastHit hit in hits)
        {
            if (occluderCount >= maxOccluders)
                break;

            // Skip if hitting the player itself
            if (hit.collider.transform == playerTransform || hit.collider.transform.IsChildOf(playerTransform))
                continue;

            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                // Try to find renderer on parent (for compound colliders)
                renderer = hit.collider.GetComponentInParent<Renderer>();
            }

            if (renderer != null)
            {
                currentlyOccluding.Add(renderer);
                occluderCount++;
            }
        }

        // Fade in newly occluding objects
        foreach (Renderer renderer in currentlyOccluding)
        {
            if (!previouslyOccluding.Contains(renderer))
            {
                FadeRenderer(renderer, fadeAlpha);
            }
        }

        // Fade out objects that are no longer occluding
        foreach (Renderer renderer in previouslyOccluding)
        {
            if (!currentlyOccluding.Contains(renderer))
            {
                FadeRenderer(renderer, 1f);
            }
        }

        // Update previous set
        previouslyOccluding.Clear();
        foreach (Renderer renderer in currentlyOccluding)
        {
            previouslyOccluding.Add(renderer);
        }

        // Debug visualization
        if (drawDebugRays && playerTransform != null && targetCamera != null)
        {
            Debug.DrawLine(cameraPosition, playerTransform.position + playerVisibilityOffset, 
                currentlyOccluding.Count > 0 ? Color.red : Color.green);
        }
    }

    private void FadeRenderer(Renderer renderer, float targetAlpha)
    {
        // Get or create FadeableRenderer component
        FadeableRenderer fadeable = null;
        
        if (!occluderFadeables.TryGetValue(renderer, out fadeable))
        {
            fadeable = renderer.GetComponent<FadeableRenderer>();
            if (fadeable == null)
            {
                fadeable = renderer.gameObject.AddComponent<FadeableRenderer>();
            }
            occluderFadeables[renderer] = fadeable;
        }

        // Check if material supports fading
        if (!fadeable.SupportsAlpha())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[FadeOccludersBetweenCameraAndPlayer] Renderer on {renderer.gameObject.name} doesn't support alpha fading. Consider assigning a fade-capable material.");
            }
            return;
        }

        // Cancel existing coroutine for this renderer if one exists
        if (activeFadeCoroutines.TryGetValue(renderer, out Coroutine existing))
        {
            if (existing != null)
            {
                StopCoroutine(existing);
            }
        }

        // Start new coroutine and track it
        Coroutine newCoroutine = StartCoroutine(FadeToAlpha(fadeable, targetAlpha, renderer));
        activeFadeCoroutines[renderer] = newCoroutine;
    }

    private IEnumerator FadeToAlpha(FadeableRenderer fadeable, float targetAlpha, Renderer renderer)
    {
        float startAlpha = fadeable.GetAlpha();
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            fadeable.SetAlpha(currentAlpha);
            yield return null;
        }

        fadeable.SetAlpha(targetAlpha);

        // Clear tracking when done
        activeFadeCoroutines.Remove(renderer);
    }

    private void OnDisable()
    {
        // Reset all occluders when disabled
        ResetAllOccluders();
    }

    private void OnDestroy()
    {
        ResetAllOccluders();
    }

    private void ResetAllOccluders()
    {
        // Stop all active fade coroutines
        foreach (var kvp in activeFadeCoroutines)
        {
            if (kvp.Value != null)
            {
                StopCoroutine(kvp.Value);
            }
        }
        activeFadeCoroutines.Clear();

        // Reset all fadeables
        foreach (var kvp in occluderFadeables)
        {
            if (kvp.Value != null)
            {
                kvp.Value.ResetAlpha();
            }
        }
        occluderFadeables.Clear();
        currentlyOccluding.Clear();
        previouslyOccluding.Clear();
    }
}

