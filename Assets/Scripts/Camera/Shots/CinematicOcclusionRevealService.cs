using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Tokenized runtime service that hides marked foreground occluders for cinematic shots,
/// then restores each renderer to its exact pre-reveal enabled state.
/// </summary>
[DisallowMultipleComponent]
public class CinematicOcclusionRevealService : MonoBehaviour
{
    public sealed class RevealToken : IDisposable
    {
        private CinematicOcclusionRevealService owner;
        private int tokenId;

        internal RevealToken(CinematicOcclusionRevealService owner, int tokenId)
        {
            this.owner = owner;
            this.tokenId = tokenId;
        }

        internal int TokenId => tokenId;
        public bool IsValid => owner != null && tokenId > 0;

        public void Dispose()
        {
            if (owner == null)
                return;

            owner.ReleaseReveal(this);
        }

        internal void Invalidate()
        {
            owner = null;
            tokenId = 0;
        }
    }

    private sealed class RendererRevealState
    {
        public bool originalEnabled;
        public readonly HashSet<int> tokenIds = new HashSet<int>();
    }

    private sealed class RevealRecord
    {
        public string reason;
        public readonly List<Renderer> renderers = new List<Renderer>();
        public OcclusionSilhouette.SuppressionHandle silhouetteSuppression;
    }

    private static CinematicOcclusionRevealService instance;

    [Header("Fallbacks")]
    [Tooltip("Fallback camera used when the caller does not provide one.")]
    [SerializeField] private Camera fallbackCamera;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly Dictionary<int, RevealRecord> activeTokens = new Dictionary<int, RevealRecord>();
    private readonly Dictionary<Renderer, RendererRevealState> rendererStates = new Dictionary<Renderer, RendererRevealState>();
    private int nextTokenId = 1;

    public static CinematicOcclusionRevealService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CinematicOcclusionRevealService>();
            }

            return instance;
        }
    }

    public int ActiveTokenCount => activeTokens.Count;
    public int HiddenRendererCount => rendererStates.Count;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[CinematicOcclusionRevealService] Duplicate service found. Destroying {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        RestoreAll("service disabled");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public RevealToken AcquireReveal(
        Camera sourceCamera,
        Transform targetTransform,
        Vector3 targetPosition,
        CinematicOcclusionProfileSO profile,
        string reason = null)
    {
        if (profile == null)
        {
            LogDebug("Acquire skipped: profile is null.");
            return null;
        }

        Camera camera = sourceCamera != null ? sourceCamera : ResolveFallbackCamera();
        if (camera == null)
        {
            LogDebug("Acquire skipped: no camera available.");
            return null;
        }

        if (profile.HideMode != CinematicOcclusionHideMode.DisableRenderer)
        {
            LogDebug($"Acquire skipped: unsupported hide mode {profile.HideMode}.");
            return null;
        }

        var renderersToHide = new HashSet<Renderer>();
        CollectRevealRenderers(camera, targetTransform, targetPosition, profile, renderersToHide);

        OcclusionSilhouette.SuppressionHandle silhouetteSuppression = default;
        if (profile.SuppressTargetSilhouette && targetTransform != null)
        {
            OcclusionSilhouette silhouette = ResolveTargetSilhouette(targetTransform);
            if (silhouette != null)
            {
                silhouetteSuppression = silhouette.AcquireSuppression(reason ?? "cinematic reveal");
            }
        }

        if (renderersToHide.Count == 0 && !silhouetteSuppression.IsValid)
        {
            LogDebug($"Acquire produced no reveal changes ({reason ?? "no reason"}).");
            return null;
        }

        int tokenId = nextTokenId++;
        var record = new RevealRecord
        {
            reason = reason ?? string.Empty,
            silhouetteSuppression = silhouetteSuppression
        };

        foreach (Renderer renderer in renderersToHide)
        {
            if (renderer == null)
                continue;

            ApplyRendererReveal(renderer, tokenId);
            record.renderers.Add(renderer);
        }

        activeTokens[tokenId] = record;

        if (enableDebugLogs || profile.EnableDebugLogs)
        {
            Debug.Log(
                $"[CinematicOcclusionRevealService] Acquired token {tokenId} ({record.reason}) " +
                $"renderers={record.renderers.Count}, suppressSilhouette={silhouetteSuppression.IsValid}, active={activeTokens.Count}");
        }

        return new RevealToken(this, tokenId);
    }

    public void ReleaseReveal(RevealToken token)
    {
        if (token == null)
            return;

        int tokenId = token.TokenId;
        if (tokenId <= 0)
            return;

        ReleaseTokenId(tokenId, "token release");
        token.Invalidate();
    }

    public void RestoreAll(string reason = "restore all")
    {
        foreach (KeyValuePair<int, RevealRecord> kvp in activeTokens)
        {
            if (kvp.Value.silhouetteSuppression.IsValid)
            {
                kvp.Value.silhouetteSuppression.Dispose();
            }
        }

        foreach (KeyValuePair<Renderer, RendererRevealState> kvp in rendererStates)
        {
            Renderer renderer = kvp.Key;
            if (renderer != null)
            {
                renderer.enabled = kvp.Value.originalEnabled;
            }
        }

        int tokenCount = activeTokens.Count;
        int rendererCount = rendererStates.Count;
        activeTokens.Clear();
        rendererStates.Clear();

        if (enableDebugLogs && (tokenCount > 0 || rendererCount > 0))
        {
            Debug.Log(
                $"[CinematicOcclusionRevealService] Restored all reveals ({reason}); " +
                $"tokens={tokenCount}, renderers={rendererCount}.");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RestoreAll($"scene loaded: {scene.name}");
        fallbackCamera = null;
    }

    private Camera ResolveFallbackCamera()
    {
        if (SceneCameraResolver.IsUsableFor(fallbackCamera, gameObject))
            return fallbackCamera;

        fallbackCamera = SceneCameraResolver.ResolveGameplayCamera();
        return fallbackCamera;
    }

    private void CollectRevealRenderers(
        Camera camera,
        Transform targetTransform,
        Vector3 targetPosition,
        CinematicOcclusionProfileSO profile,
        HashSet<Renderer> renderersToHide)
    {
        Vector3[] sampleOffsets = profile.TargetSampleOffsets;
        if (sampleOffsets == null || sampleOffsets.Length == 0)
        {
            sampleOffsets = new[] { Vector3.zero };
        }

        Vector3 cameraPosition = camera.transform.position;
        foreach (Vector3 offset in sampleOffsets)
        {
            Vector3 samplePoint = (targetTransform != null ? targetTransform.position : targetPosition) + offset;
            Vector3 direction = samplePoint - cameraPosition;
            float distance = Mathf.Min(direction.magnitude, profile.MaxDistance);

            if (distance <= 0.001f)
                continue;

            direction.Normalize();

            RaycastHit[] hits = profile.SphereCastRadius > 0f
                ? Physics.SphereCastAll(
                    cameraPosition,
                    profile.SphereCastRadius,
                    direction,
                    distance,
                    profile.OccluderLayerMask,
                    QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(
                    cameraPosition,
                    direction,
                    distance,
                    profile.OccluderLayerMask,
                    QueryTriggerInteraction.Ignore);

            bool hitRevealOccluder = false;
            foreach (RaycastHit hit in hits)
            {
                if (ShouldSkipRevealHit(hit.transform, targetTransform))
                    continue;

                CinematicOccluder occluder = hit.collider.GetComponentInParent<CinematicOccluder>();
                if (occluder == null)
                    continue;

                if (ShouldSkipCharacterTransform(occluder.transform))
                    continue;

                Renderer[] renderers = occluder.ResolveRenderers(profile.IncludeChildren);
                if (renderers == null)
                    continue;

                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null || ShouldSkipCharacterTransform(renderer.transform))
                        continue;

                    renderersToHide.Add(renderer);
                    hitRevealOccluder = true;
                }

                if (enableDebugLogs || profile.EnableDebugLogs)
                {
                    Debug.Log($"[CinematicOcclusionRevealService] Hit cinematic occluder '{occluder.OccluderId}'.");
                }
            }

            if (profile.DrawDebugRays)
            {
                Debug.DrawLine(cameraPosition, samplePoint, hitRevealOccluder ? Color.cyan : Color.gray, 1f);
            }
        }
    }

    private void ApplyRendererReveal(Renderer renderer, int tokenId)
    {
        if (!rendererStates.TryGetValue(renderer, out RendererRevealState state))
        {
            state = new RendererRevealState
            {
                originalEnabled = renderer.enabled
            };
            rendererStates[renderer] = state;
        }

        state.tokenIds.Add(tokenId);
        renderer.enabled = false;
    }

    private void ReleaseTokenId(int tokenId, string releaseReason)
    {
        if (!activeTokens.TryGetValue(tokenId, out RevealRecord record))
            return;

        if (record.silhouetteSuppression.IsValid)
        {
            record.silhouetteSuppression.Dispose();
        }

        int restoredCount = 0;
        foreach (Renderer renderer in record.renderers)
        {
            if (renderer == null)
                continue;

            if (!rendererStates.TryGetValue(renderer, out RendererRevealState state))
                continue;

            state.tokenIds.Remove(tokenId);
            if (state.tokenIds.Count == 0)
            {
                renderer.enabled = state.originalEnabled;
                rendererStates.Remove(renderer);
                restoredCount++;
            }
        }

        activeTokens.Remove(tokenId);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[CinematicOcclusionRevealService] Released token {tokenId} ({releaseReason}); " +
                $"restored={restoredCount}, active={activeTokens.Count}.");
        }
    }

    private static bool ShouldSkipRevealHit(Transform hitTransform, Transform targetTransform)
    {
        if (hitTransform == null)
            return true;

        if (targetTransform != null &&
            (hitTransform == targetTransform || hitTransform.IsChildOf(targetTransform)))
            return true;

        return ShouldSkipCharacterTransform(hitTransform);
    }

    private static bool ShouldSkipCharacterTransform(Transform transform)
    {
        if (transform == null)
            return true;

        if (transform.CompareTag("Player") || transform.root.CompareTag("Player"))
            return true;

        if (transform.GetComponentInParent<MeshRendererPlayerVisual>() != null)
            return true;
        if (transform.GetComponentInParent<MeshRendererNPCVisual>() != null)
            return true;
        if (transform.GetComponentInParent<CombatChainlingVisual>() != null)
            return true;

        return false;
    }

    private OcclusionSilhouette ResolveTargetSilhouette(Transform targetTransform)
    {
        OcclusionSilhouette silhouette = targetTransform.GetComponentInParent<OcclusionSilhouette>();
        if (silhouette != null)
            return silhouette;

        return targetTransform.GetComponentInChildren<OcclusionSilhouette>();
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CinematicOcclusionRevealService] {message}");
        }
    }
}
