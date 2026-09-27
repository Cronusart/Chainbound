using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that makes the player face a screen direction or a resolved world target.
/// Compose with input-lock or dialogue steps when you need the facing to persist.
/// </summary>
[CreateAssetMenu(fileName = "Step_Player_FaceTarget", menuName = "Chainbound/Narrative/Steps/Player/Face Target")]
public class Step_Player_FaceTarget : SequenceStep
{
    [Header("Player")]
    [Tooltip("Tag used to find the live player object.")]
    [SerializeField] private string playerTag = "Player";

    [Header("Target")]
    [Tooltip("How to resolve the facing target.")]
    [SerializeField] private PlayerFaceTargetType targetType = PlayerFaceTargetType.ScreenDirection;

    [Tooltip("Used when Target Type is ScreenDirection. Matches the player's normal input-style facing.")]
    [SerializeField] private PlayerFaceScreenDirection screenDirection = PlayerFaceScreenDirection.Down;

    [Tooltip("Used when Target Type is WorldDirection. Gets projected through the active camera so the player visuals still match screen-space facing.")]
    [SerializeField] private PlayerFaceWorldDirection worldDirection = PlayerFaceWorldDirection.North;

    [Tooltip("NPC ID used when Target Type is NpcId.")]
    [SerializeField] private string npcId = "";

    [Tooltip("Waypoint ID used when Target Type is WaypointId.")]
    [SerializeField] private string waypointId = "";

    [Tooltip("Unity tag used when Target Type is GameObjectTag.")]
    [SerializeField] private string targetTag = "";

    [Tooltip("Invert the resolved world direction. Useful for AwayFromCamera / AwayFromNPC / AwayFromObject setups.")]
    [SerializeField] private bool faceAwayFromResolvedTarget = false;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (!PlayerFacingUtility.TryGetPlayerFacingComponents(
                playerTag,
                out GameObject playerObject,
                out Transform playerTransform,
                out PlayerAnimationController3D_Mesh animationController,
                out PlayerMovement3D playerMovement,
                out string playerError))
        {
            Debug.LogError($"[Step_Player_FaceTarget] {playerError}");
            onComplete?.Invoke(StepOutcome.Failed(playerError));
            yield break;
        }

        TopDownCameraController3D cameraController = ResolveCameraController(playerMovement);
        Camera fallbackCamera = SceneCameraResolver.ResolveGameplayCamera();

        if (!TryResolveFacingDirection(
                playerTransform,
                cameraController,
                fallbackCamera,
                out MeshRendererPlayerVisual.Direction resolvedDirection,
                out string resolutionError))
        {
            Debug.LogError($"[Step_Player_FaceTarget] {resolutionError}");
            onComplete?.Invoke(StepOutcome.Failed(resolutionError));
            yield break;
        }

        animationController.ForceFaceDirection(resolvedDirection);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[Step_Player_FaceTarget] Player '{playerObject.name}' facing {resolvedDirection} " +
                $"via {targetType}.");
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            Debug.LogWarning("[Step_Player_FaceTarget] Player tag is not set.");
            return false;
        }

        if (targetType == PlayerFaceTargetType.NpcId && string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[Step_Player_FaceTarget] NPC ID is not set for NpcId target type.");
            return false;
        }

        if (targetType == PlayerFaceTargetType.WaypointId && string.IsNullOrWhiteSpace(waypointId))
        {
            Debug.LogWarning("[Step_Player_FaceTarget] Waypoint ID is not set for WaypointId target type.");
            return false;
        }

        if (targetType == PlayerFaceTargetType.GameObjectTag && string.IsNullOrWhiteSpace(targetTag))
        {
            Debug.LogWarning("[Step_Player_FaceTarget] Target tag is not set for GameObjectTag target type.");
            return false;
        }

        return true;
    }

    private bool TryResolveFacingDirection(
        Transform playerTransform,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        out MeshRendererPlayerVisual.Direction resolvedDirection,
        out string error)
    {
        resolvedDirection = MeshRendererPlayerVisual.Direction.Down;
        error = null;

        switch (targetType)
        {
            case PlayerFaceTargetType.ScreenDirection:
                resolvedDirection = PlayerFacingUtility.ToCardinalDirection(screenDirection);
                return true;

            case PlayerFaceTargetType.WorldDirection:
                return TryResolveWorldDirection(
                    PlayerFacingUtility.GetWorldDirection(worldDirection),
                    cameraController,
                    fallbackCamera,
                    out resolvedDirection,
                    out error);

            case PlayerFaceTargetType.Camera:
                return TryResolveWorldDirection(
                    PlayerFacingUtility.GetDirectionTowardCamera(
                        playerTransform,
                        cameraController,
                        fallbackCamera,
                        faceAwayFromResolvedTarget),
                    cameraController,
                    fallbackCamera,
                    out resolvedDirection,
                    out error);

            case PlayerFaceTargetType.NpcId:
            {
                NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
                if (npc == null)
                {
                    error = $"NPC '{npcId}' was not found.";
                    return false;
                }

                return TryResolveDirectionTowardPosition(
                    playerTransform,
                    npc.transform.position,
                    cameraController,
                    fallbackCamera,
                    out resolvedDirection,
                    out error);
            }

            case PlayerFaceTargetType.WaypointId:
            {
                NPCWaypointMarker waypoint = NPCWaypointRegistry.Instance.GetWaypoint(waypointId);
                if (waypoint == null)
                {
                    error = $"Waypoint '{waypointId}' was not found.";
                    return false;
                }

                return TryResolveDirectionTowardPosition(
                    playerTransform,
                    waypoint.Position,
                    cameraController,
                    fallbackCamera,
                    out resolvedDirection,
                    out error);
            }

            case PlayerFaceTargetType.GameObjectTag:
            {
                GameObject taggedObject;
                try
                {
                    taggedObject = GameObject.FindGameObjectWithTag(targetTag);
                }
                catch (UnityException ex)
                {
                    error = ex.Message;
                    return false;
                }

                if (taggedObject == null)
                {
                    error = $"No GameObject with tag '{targetTag}' was found.";
                    return false;
                }

                return TryResolveDirectionTowardPosition(
                    playerTransform,
                    taggedObject.transform.position,
                    cameraController,
                    fallbackCamera,
                    out resolvedDirection,
                    out error);
            }
        }

        error = $"Unsupported target type: {targetType}";
        return false;
    }

    private bool TryResolveDirectionTowardPosition(
        Transform playerTransform,
        Vector3 targetPosition,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        out MeshRendererPlayerVisual.Direction resolvedDirection,
        out string error)
    {
        if (PlayerFacingUtility.TryResolveFacingDirectionTowardPosition(
            playerTransform,
            targetPosition,
            cameraController,
            fallbackCamera,
            faceAwayFromResolvedTarget,
            out resolvedDirection))
        {
            error = null;
            return true;
        }

        error = "Could not resolve a non-zero direction toward the target position.";
        return false;
    }

    private bool TryResolveWorldDirection(
        Vector3 worldDirection,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        out MeshRendererPlayerVisual.Direction resolvedDirection,
        out string error)
    {
        resolvedDirection = MeshRendererPlayerVisual.Direction.Down;
        error = null;

        if (worldDirection.sqrMagnitude <= 0.0001f)
        {
            error = "Resolved facing direction was zero-length.";
            return false;
        }

        if (!PlayerFacingUtility.TryResolveFacingDirectionFromWorld(
                worldDirection,
                cameraController,
                fallbackCamera,
                out resolvedDirection))
        {
            error = "Could not project the resolved world direction into a player-facing direction.";
            return false;
        }

        return true;
    }

    private TopDownCameraController3D ResolveCameraController(PlayerMovement3D playerMovement)
    {
        if (playerMovement != null && playerMovement.CameraController != null)
            return playerMovement.CameraController;

        return SceneCameraResolver.ResolveGameplayCameraController();
    }
}
