using UnityEngine;

/// <summary>
/// Shared helpers for resolving player-facing directions from screen-space or world-space targets.
/// Keeps cutscene/dialogue steps data-driven without duplicating camera-basis projection logic.
/// </summary>
public static class PlayerFacingUtility
{
    public static bool TryGetPlayerFacingComponents(
        string playerTag,
        out GameObject playerObject,
        out Transform playerTransform,
        out PlayerAnimationController3D_Mesh animationController,
        out PlayerMovement3D playerMovement,
        out string error)
    {
        playerObject = null;
        playerTransform = null;
        animationController = null;
        playerMovement = null;
        error = null;

        if (string.IsNullOrWhiteSpace(playerTag))
        {
            error = "Player tag is empty.";
            return false;
        }

        GameObject resolvedPlayer;
        try
        {
            resolvedPlayer = PlayerRootResolver.ResolveActivePlayer(playerTag);
        }
        catch (UnityException ex)
        {
            error = ex.Message;
            return false;
        }

        if (resolvedPlayer == null)
        {
            error = $"Player with tag '{playerTag}' was not found.";
            return false;
        }

        return TryGetPlayerFacingComponents(
            resolvedPlayer,
            playerTag,
            out playerObject,
            out playerTransform,
            out animationController,
            out playerMovement,
            out error);
    }

    /// <summary>
    /// Resolves facing components from any object in the player hierarchy. This is useful when
    /// colliders or presentation children share the Player tag with the authoritative root.
    /// </summary>
    public static bool TryGetPlayerFacingComponents(
        GameObject playerCandidate,
        string playerTag,
        out GameObject playerObject,
        out Transform playerTransform,
        out PlayerAnimationController3D_Mesh animationController,
        out PlayerMovement3D playerMovement,
        out string error)
    {
        playerObject = null;
        playerTransform = null;
        animationController = null;
        playerMovement = null;
        error = null;

        if (playerCandidate == null)
        {
            error = "Player candidate is null.";
            return false;
        }

        playerObject = PlayerRootResolver.ResolveCandidate(playerCandidate, playerTag);
        if (playerObject == null)
        {
            error = $"Could not resolve the player root from '{playerCandidate.name}'.";
            return false;
        }

        playerMovement = playerObject.GetComponent<PlayerMovement3D>()
            ?? playerObject.GetComponentInParent<PlayerMovement3D>(true)
            ?? playerObject.GetComponentInChildren<PlayerMovement3D>(true);
        if (playerMovement != null)
            playerObject = playerMovement.gameObject;

        playerTransform = playerObject.transform;
        animationController = playerObject.GetComponent<PlayerAnimationController3D_Mesh>()
            ?? playerObject.GetComponentInChildren<PlayerAnimationController3D_Mesh>(true)
            ?? playerObject.GetComponentInParent<PlayerAnimationController3D_Mesh>(true);

        if (animationController == null)
        {
            error = $"PlayerAnimationController3D_Mesh was not found on the resolved player hierarchy '{playerObject.name}'.";
            return false;
        }

        return true;
    }

    public static MeshRendererPlayerVisual.Direction ToCardinalDirection(PlayerFaceScreenDirection direction)
    {
        return direction switch
        {
            PlayerFaceScreenDirection.Up => MeshRendererPlayerVisual.Direction.Up,
            PlayerFaceScreenDirection.Left => MeshRendererPlayerVisual.Direction.Left,
            PlayerFaceScreenDirection.Right => MeshRendererPlayerVisual.Direction.Right,
            _ => MeshRendererPlayerVisual.Direction.Down
        };
    }

    public static Vector3 GetWorldDirection(PlayerFaceWorldDirection direction)
    {
        return direction switch
        {
            PlayerFaceWorldDirection.North => Vector3.forward,
            PlayerFaceWorldDirection.South => Vector3.back,
            PlayerFaceWorldDirection.East => Vector3.right,
            PlayerFaceWorldDirection.West => Vector3.left,
            _ => Vector3.forward
        };
    }

    public static bool TryResolveFacingDirectionFromWorld(
        Vector3 worldDirection,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        out MeshRendererPlayerVisual.Direction direction)
    {
        direction = MeshRendererPlayerVisual.Direction.Down;

        Vector2 screenDirection = ConvertWorldDirectionToScreen(worldDirection, cameraController, fallbackCamera);
        if (screenDirection.sqrMagnitude <= 0.0001f)
            return false;

        direction = ToCardinalDirection(screenDirection);
        return true;
    }

    public static bool TryResolveFacingDirectionTowardPosition(
        Transform playerTransform,
        Vector3 targetPosition,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        bool faceAway,
        out MeshRendererPlayerVisual.Direction direction)
    {
        direction = MeshRendererPlayerVisual.Direction.Down;
        if (playerTransform == null)
            return false;

        Vector3 worldDirection = targetPosition - playerTransform.position;
        worldDirection.y = 0f;
        if (faceAway)
            worldDirection = -worldDirection;

        return TryResolveFacingDirectionFromWorld(
            worldDirection,
            cameraController,
            fallbackCamera,
            out direction);
    }

    public static MeshRendererPlayerVisual.Direction ToCardinalDirection(Vector2 screenDirection)
    {
        if (Mathf.Abs(screenDirection.x) > Mathf.Abs(screenDirection.y))
            return screenDirection.x >= 0f
                ? MeshRendererPlayerVisual.Direction.Right
                : MeshRendererPlayerVisual.Direction.Left;

        return screenDirection.y >= 0f
            ? MeshRendererPlayerVisual.Direction.Up
            : MeshRendererPlayerVisual.Direction.Down;
    }

    public static Vector2 ConvertWorldDirectionToScreen(
        Vector3 worldDirection,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera)
    {
        Vector2 planar = new Vector2(worldDirection.x, worldDirection.z);
        if (planar.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        planar.Normalize();

        if (cameraController == null && fallbackCamera == null)
            return planar;

        Vector3 forward3 = cameraController != null
            ? cameraController.GetCameraForwardXZ()
            : GetCameraForwardXZ(fallbackCamera.transform);
        Vector3 right3 = cameraController != null
            ? cameraController.GetCameraRightXZ()
            : GetCameraRightXZ(fallbackCamera.transform);

        Vector2 forward = new Vector2(forward3.x, forward3.z).normalized;
        Vector2 right = new Vector2(right3.x, right3.z).normalized;

        float screenX = Vector2.Dot(planar, right);
        float screenY = Vector2.Dot(planar, forward);
        return new Vector2(screenX, screenY);
    }

    public static Vector3 GetDirectionTowardCamera(
        Transform playerTransform,
        TopDownCameraController3D cameraController,
        Camera fallbackCamera,
        bool faceAway)
    {
        if (playerTransform == null)
            return Vector3.zero;

        Transform cameraTransform = cameraController != null
            ? cameraController.transform
            : fallbackCamera != null ? fallbackCamera.transform : null;

        if (cameraTransform != null)
        {
            Vector3 toCamera = cameraTransform.position - playerTransform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = toCamera.normalized;
                return faceAway ? -direction : direction;
            }
        }

        if (cameraController != null)
        {
            Vector3 fallbackDirection = -cameraController.GetCameraForwardXZ();
            return faceAway ? -fallbackDirection : fallbackDirection;
        }

        if (fallbackCamera != null)
        {
            Vector3 fallbackDirection = -GetCameraForwardXZ(fallbackCamera.transform);
            return faceAway ? -fallbackDirection : fallbackDirection;
        }

        return Vector3.zero;
    }

    private static Vector3 GetCameraForwardXZ(Transform cameraTransform)
    {
        if (cameraTransform == null)
            return Vector3.forward;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    private static Vector3 GetCameraRightXZ(Transform cameraTransform)
    {
        if (cameraTransform == null)
            return Vector3.right;

        Vector3 right = cameraTransform.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
    }
}

public enum PlayerFaceTargetType
{
    ScreenDirection,
    WorldDirection,
    Camera,
    NpcId,
    WaypointId,
    GameObjectTag
}

public enum PlayerFaceScreenDirection
{
    Down,
    Up,
    Left,
    Right
}

public enum PlayerFaceWorldDirection
{
    North,
    South,
    East,
    West
}
