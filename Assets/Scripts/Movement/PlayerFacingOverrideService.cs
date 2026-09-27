using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Token-based owner of temporary player-facing overrides. The newest active request wins;
/// releasing it reveals the previous request, so presentation systems do not fight over facing.
/// </summary>
public class PlayerFacingOverrideService : MonoBehaviour
{
    private static PlayerFacingOverrideService instance;

    public static PlayerFacingOverrideService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PlayerFacingOverrideService>(FindObjectsInactive.Include);
                if (instance == null && Application.isPlaying)
                {
                    GameObject host = new GameObject("PlayerFacingOverrideService");
                    instance = host.AddComponent<PlayerFacingOverrideService>();
                    DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private sealed class FacingRequest
    {
        public int id;
        public string reason;
        public string playerTag;
        public string npcId;
        public bool faceAway;
        public UnityEngine.Object owner;
        public bool hasOwner;
        public string lastResolutionError;
    }

    private readonly Dictionary<int, FacingRequest> activeRequests = new Dictionary<int, FacingRequest>();
    private int nextRequestId = 1;

    public int ActiveOverrideCount => activeRequests.Count;

    public PlayerFacingOverrideHandle AcquireNpcOverride(
        string reason,
        string npcId,
        string playerTag = "Player",
        bool faceAway = false,
        UnityEngine.Object owner = null)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[PlayerFacingOverrideService] Cannot acquire an NPC-facing override with an empty NPC ID.");
            return default;
        }

        int id = nextRequestId++;
        FacingRequest request = new FacingRequest
        {
            id = id,
            reason = string.IsNullOrWhiteSpace(reason) ? "Unspecified" : reason,
            playerTag = string.IsNullOrWhiteSpace(playerTag) ? "Player" : playerTag,
            npcId = npcId,
            faceAway = faceAway,
            owner = owner,
            hasOwner = owner != null
        };
        activeRequests[id] = request;

        TryApplyRequest(request, logResolutionFailure: false);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[PlayerFacingOverrideService] Override acquired: '{request.reason}' -> NPC '{npcId}' " +
                $"(id={id}, total={activeRequests.Count}).");
        }

        return new PlayerFacingOverrideHandle(id, request.reason, ReleaseOverride);
    }

    public bool TryFaceNpcOnce(
        string npcId,
        string playerTag,
        bool faceAway,
        out string error)
    {
        FacingRequest request = new FacingRequest
        {
            playerTag = string.IsNullOrWhiteSpace(playerTag) ? "Player" : playerTag,
            npcId = npcId,
            faceAway = faceAway
        };

        return TryApplyRequest(request, logResolutionFailure: false, out error);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[PlayerFacingOverrideService] Duplicate service found; removing duplicate component.");
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        activeRequests.Clear();
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        PruneDestroyedOwners();
        FacingRequest request = GetNewestRequest();
        if (request != null)
            TryApplyRequest(request, logResolutionFailure: true);
    }

    private FacingRequest GetNewestRequest()
    {
        FacingRequest newest = null;
        foreach (FacingRequest request in activeRequests.Values)
        {
            if (newest == null || request.id > newest.id)
                newest = request;
        }

        return newest;
    }

    private void PruneDestroyedOwners()
    {
        if (activeRequests.Count == 0)
            return;

        List<int> staleIds = null;
        foreach (KeyValuePair<int, FacingRequest> pair in activeRequests)
        {
            FacingRequest request = pair.Value;
            if (!request.hasOwner || request.owner != null)
                continue;

            if (staleIds == null)
                staleIds = new List<int>();
            staleIds.Add(pair.Key);
        }

        if (staleIds == null)
            return;

        for (int i = 0; i < staleIds.Count; i++)
            ReleaseOverride(staleIds[i]);
    }

    private bool TryApplyRequest(FacingRequest request, bool logResolutionFailure)
    {
        return TryApplyRequest(request, logResolutionFailure, out _);
    }

    private bool TryApplyRequest(FacingRequest request, bool logResolutionFailure, out string error)
    {
        error = null;
        if (request == null || string.IsNullOrWhiteSpace(request.npcId))
        {
            error = "NPC ID is empty.";
            return false;
        }

        if (!PlayerFacingUtility.TryGetPlayerFacingComponents(
                request.playerTag,
                out _,
                out Transform playerTransform,
                out PlayerAnimationController3D_Mesh animationController,
                out PlayerMovement3D playerMovement,
                out error))
        {
            LogResolutionFailure(request, error, logResolutionFailure);
            return false;
        }

        NPCController npc = NPCRegistry.Instance != null
            ? NPCRegistry.Instance.GetNPC(request.npcId)
            : null;
        if (npc == null)
        {
            error = $"NPC '{request.npcId}' was not found.";
            LogResolutionFailure(request, error, logResolutionFailure);
            return false;
        }

        TopDownCameraController3D cameraController = playerMovement != null
            ? playerMovement.CameraController
            : null;
        if (cameraController == null)
            cameraController = SceneCameraResolver.ResolveGameplayCameraController();

        Camera fallbackCamera = SceneCameraResolver.ResolveGameplayCamera();
        if (!PlayerFacingUtility.TryResolveFacingDirectionTowardPosition(
                playerTransform,
                npc.transform.position,
                cameraController,
                fallbackCamera,
                request.faceAway,
                out MeshRendererPlayerVisual.Direction direction))
        {
            error = $"Could not resolve a non-zero direction toward NPC '{request.npcId}'.";
            LogResolutionFailure(request, error, logResolutionFailure);
            return false;
        }

        request.lastResolutionError = null;
        animationController.ForceFaceDirection(direction);
        return true;
    }

    private void LogResolutionFailure(FacingRequest request, string error, bool logResolutionFailure)
    {
        if (!logResolutionFailure || !enableDebugLogs || request == null || request.lastResolutionError == error)
            return;

        request.lastResolutionError = error;
        Debug.LogWarning($"[PlayerFacingOverrideService] '{request.reason}' waiting to resolve: {error}");
    }

    private void ReleaseOverride(int id)
    {
        if (!activeRequests.TryGetValue(id, out FacingRequest request))
            return;

        activeRequests.Remove(id);
        if (enableDebugLogs)
        {
            Debug.Log(
                $"[PlayerFacingOverrideService] Override released: '{request.reason}' " +
                $"(id={id}, remaining={activeRequests.Count}).");
        }
    }
}

/// <summary>Disposable ownership token for a temporary player-facing override.</summary>
public struct PlayerFacingOverrideHandle : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;

    public int Id => id;
    public string Reason { get; }
    public bool IsValid => releaseCallback != null;
    public bool IsReleased => isReleased;

    internal PlayerFacingOverrideHandle(int id, string reason, Action<int> releaseCallback)
    {
        this.id = id;
        Reason = reason;
        this.releaseCallback = releaseCallback;
        isReleased = false;
    }

    public void Release()
    {
        if (isReleased || releaseCallback == null)
            return;

        isReleased = true;
        releaseCallback(id);
    }

    public void Dispose()
    {
        Release();
    }
}
