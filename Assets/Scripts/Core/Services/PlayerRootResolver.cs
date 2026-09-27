using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolves the authoritative player root when the Player tag is also present on
/// child colliders or presentation objects. Camera, spawn, and trigger systems
/// should use this instead of assuming FindGameObjectWithTag returns the root.
/// </summary>
public static class PlayerRootResolver
{
    public static GameObject ResolveActivePlayer(string playerTag = "Player")
    {
        SpawnService spawnService = Object.FindFirstObjectByType<SpawnService>();
        if (spawnService != null)
        {
            GameObject spawnRoot = spawnService.GetResolvedPlayerRoot();
            if (spawnRoot != null)
                return spawnRoot;
        }

        PlayerMovement3D[] movements = Object.FindObjectsByType<PlayerMovement3D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        GameObject movementRoot = ChoosePreferredRoot(movements);
        if (movementRoot != null)
            return movementRoot;

        if (string.IsNullOrEmpty(playerTag))
            return null;

        GameObject[] taggedObjects = GameObject.FindGameObjectsWithTag(playerTag);
        GameObject fallback = null;
        for (int i = 0; i < taggedObjects.Length; i++)
        {
            GameObject candidate = ResolveCandidate(taggedObjects[i], playerTag);
            if (candidate == null)
                continue;

            if (candidate.GetComponent<PlayerMovement3D>() != null)
                return candidate;

            fallback ??= candidate;
        }

        return fallback;
    }

    public static GameObject ResolveCandidate(GameObject candidate, string playerTag = "Player")
    {
        if (candidate == null)
            return null;

        PlayerMovement3D movement = candidate.GetComponentInParent<PlayerMovement3D>(true);
        if (movement != null)
            return movement.gameObject;

        GameObject topmostTagged = null;
        Transform current = candidate.transform;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(playerTag) && current.CompareTag(playerTag))
                topmostTagged = current.gameObject;

            current = current.parent;
        }

        return topmostTagged ?? candidate.transform.root.gameObject;
    }

    private static GameObject ChoosePreferredRoot(PlayerMovement3D[] movements)
    {
        if (movements == null || movements.Length == 0)
            return null;

        Scene activeScene = SceneManager.GetActiveScene();
        GameObject activeSceneRoot = null;
        GameObject firstRoot = null;

        for (int i = 0; i < movements.Length; i++)
        {
            PlayerMovement3D movement = movements[i];
            if (movement == null)
                continue;

            GameObject root = movement.gameObject;
            firstRoot ??= root;

            if (root.scene.name == "DontDestroyOnLoad")
                return root;

            if (root.scene == activeScene)
                activeSceneRoot ??= root;
        }

        return activeSceneRoot ?? firstRoot;
    }
}
