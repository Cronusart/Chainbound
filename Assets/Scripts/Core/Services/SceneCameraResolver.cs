using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared camera lookup for scene readiness, billboards, shots, and zones.
/// When the overworld rig is registered on GameServices, it is the only live
/// gameplay presenter — leftover scene CameraRigRoots must not win.
/// Interiors and combat hide that registration before they activate and use a
/// scene-local camera.
/// </summary>
public static class SceneCameraResolver
{
    private static readonly Dictionary<SceneHandle, Camera> CamerasBySceneHandle = new Dictionary<SceneHandle, Camera>();
    private static Camera persistentGameplayCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        CamerasBySceneHandle.Clear();
        persistentGameplayCamera = null;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    public static Camera PersistentGameplayCamera => persistentGameplayCamera;

    public static bool HasLivePersistentCamera =>
        IsCameraEnabled(persistentGameplayCamera, includeInactive: false);

    /// <summary>
    /// Registers the DontDestroyOnLoad overworld camera so scene-owned billboards,
    /// zones, and warmup can keep targeting it after the rig leaves its source chunk.
    /// </summary>
    public static void RegisterPersistentCamera(Camera camera)
    {
        persistentGameplayCamera = camera;
        CamerasBySceneHandle.Clear();
    }

    /// <summary>Clears the registered persistent camera when that instance is gone.</summary>
    public static void ClearPersistentCamera(Camera camera)
    {
        if (persistentGameplayCamera != camera)
            return;

        persistentGameplayCamera = null;
        CamerasBySceneHandle.Clear();
    }

    /// <summary>
    /// Gameplay camera for the active presentation: persistent overworld rig when
    /// live, otherwise the active scene's local camera (interiors, combat, menu).
    /// </summary>
    public static Camera ResolveGameplayCamera(bool includeInactive = false)
    {
        return Resolve(SceneManager.GetActiveScene(), includeInactive);
    }

    /// <summary>
    /// Keeps <paramref name="current"/> only when it is still the live presenter
    /// for <paramref name="consumer"/>; otherwise re-resolves.
    /// </summary>
    public static Camera ResolveOrKeep(Camera current, GameObject consumer, bool includeInactive = false)
    {
        if (IsUsableFor(current, consumer, includeInactive))
            return current;

        return consumer != null
            ? ResolveFor(consumer, includeInactive)
            : ResolveGameplayCamera(includeInactive);
    }

    /// <summary>
    /// Camera component owned by a top-down controller (same object or child).
    /// </summary>
    public static Camera GetCamera(TopDownCameraController3D controller)
    {
        return FindCamera(controller);
    }

    /// <summary>
    /// Top-down controller that owns a camera (same object, parent, or child).
    /// </summary>
    public static TopDownCameraController3D GetController(Camera camera, bool includeInactive = false)
    {
        return FindController(camera, includeInactive);
    }

    /// <summary>
    /// Top-down controller on the gameplay camera. Prefers the GameServices rig
    /// so narrative shots and player locomotion do not bind a leftover chunk camera.
    /// </summary>
    public static TopDownCameraController3D ResolveGameplayCameraController(bool includeInactive = false)
    {
        Camera camera = ResolveGameplayCamera(includeInactive);
        TopDownCameraController3D controller = FindController(camera, includeInactive);
        if (controller != null)
            return controller;

        TopDownCameraController3D[] controllers = Object.FindObjectsByType<TopDownCameraController3D>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < controllers.Length; i++)
        {
            TopDownCameraController3D candidate = controllers[i];
            if (candidate == null)
                continue;
            if (!includeInactive && !candidate.isActiveAndEnabled)
                continue;
            if (HasLivePersistentCamera && FindCamera(candidate) != persistentGameplayCamera)
                continue;
            return candidate;
        }

        return null;
    }

    public static Camera Resolve(Scene scene, bool includeInactive = false)
    {
        if (!scene.IsValid())
            return null;

        if (HasLivePersistentCamera && IsCameraEnabled(persistentGameplayCamera, includeInactive))
            return Cache(scene, persistentGameplayCamera);

        if (CamerasBySceneHandle.TryGetValue(scene.handle, out Camera cached) &&
            IsUsable(cached, scene, includeInactive))
        {
            return cached;
        }

        if (IsUsable(persistentGameplayCamera, scene, includeInactive))
            return Cache(scene, persistentGameplayCamera);

        Camera local = FindCameraInScene(scene, includeInactive);
        if (local != null)
            return Cache(scene, local);

        Camera main = Camera.main;
        if (IsUsable(main, scene, includeInactive))
            return Cache(scene, main);

        CamerasBySceneHandle.Remove(scene.handle);
        return null;
    }

    /// <summary>
    /// Resolves the camera that should render a specific consumer. Persistent
    /// objects live in Unity's DontDestroyOnLoad scene, so their own scene does
    /// not contain the active gameplay camera; those consumers fall back to the
    /// active scene after checking the registered presenter.
    /// </summary>
    public static Camera ResolveFor(GameObject consumer, bool includeInactive = false)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (consumer == null)
            return Resolve(activeScene, includeInactive);

        Scene consumerScene = consumer.scene;
        if (consumerScene.IsValid() && !IsPersistentScene(consumerScene))
        {
            Camera colocated = Resolve(consumerScene, includeInactive);
            if (colocated != null)
                return colocated;
        }

        return Resolve(activeScene, includeInactive);
    }

    /// <summary>
    /// True when a camera is still a valid presentation target for a consumer.
    /// A live persistent overworld camera is exclusive: leftover scene rigs are not usable.
    /// </summary>
    public static bool IsUsableFor(Camera camera, GameObject consumer, bool includeInactive = false)
    {
        if (camera == null)
            return false;

        if (HasLivePersistentCamera)
            return camera == persistentGameplayCamera && IsCameraEnabled(camera, includeInactive);

        if (consumer == null)
            return false;

        Scene consumerScene = consumer.scene;
        if (consumerScene.IsValid() && !IsPersistentScene(consumerScene))
            return IsUsable(camera, consumerScene, includeInactive);

        Scene activeScene = SceneManager.GetActiveScene();
        return IsUsable(camera, activeScene, includeInactive);
    }

    /// <summary>
    /// Top-down controller owned by <paramref name="scene"/>, ignoring the
    /// exclusive persistent presenter. Combat and interiors use this after the
    /// overworld rig is hidden.
    /// </summary>
    public static TopDownCameraController3D FindLocalController(Scene scene, bool includeInactive = false)
    {
        Camera camera = FindLocalCamera(scene, includeInactive);
        return FindController(camera, includeInactive);
    }

    public static void Invalidate(Scene scene)
    {
        if (scene.IsValid())
            CamerasBySceneHandle.Remove(scene.handle);
    }

    private static Camera Cache(Scene scene, Camera camera)
    {
        CamerasBySceneHandle[scene.handle] = camera;
        return camera;
    }

    private static Camera FindCameraInScene(Scene scene, bool includeInactive)
    {
        if (HasLivePersistentCamera)
            return null;

        return FindLocalCamera(scene, includeInactive);
    }

    private static Camera FindLocalCamera(Scene scene, bool includeInactive)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        Camera[] cameras = Object.FindObjectsByType<Camera>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null &&
                camera.gameObject.scene == scene &&
                camera != persistentGameplayCamera &&
                IsCameraEnabled(camera, includeInactive))
            {
                return camera;
            }
        }

        return null;
    }

    private static bool IsUsable(Camera camera, Scene scene, bool includeInactive)
    {
        if (camera == null || !scene.IsValid() || !scene.isLoaded)
            return false;

        if (!IsCameraEnabled(camera, includeInactive))
            return false;

        if (camera == persistentGameplayCamera)
            return true;

        if (HasLivePersistentCamera)
            return false;

        return camera.gameObject.scene == scene;
    }

    private static bool IsCameraEnabled(Camera camera, bool includeInactive)
    {
        return camera != null && (includeInactive || camera.isActiveAndEnabled);
    }

    private static TopDownCameraController3D FindController(Camera camera, bool includeInactive)
    {
        if (camera == null)
            return null;

        TopDownCameraController3D controller = camera.GetComponent<TopDownCameraController3D>();
        if (controller == null)
            controller = camera.GetComponentInParent<TopDownCameraController3D>();
        if (controller == null)
            controller = camera.GetComponentInChildren<TopDownCameraController3D>(includeInactive);

        if (controller == null)
            return null;
        if (!includeInactive && !controller.isActiveAndEnabled)
            return null;
        return controller;
    }

    private static Camera FindCamera(TopDownCameraController3D controller)
    {
        if (controller == null)
            return null;

        Camera camera = controller.GetComponent<Camera>();
        if (camera == null)
            camera = controller.GetComponentInChildren<Camera>(true);
        return camera;
    }

    private static bool IsPersistentScene(Scene scene)
    {
        return scene.IsValid() && scene.name == "DontDestroyOnLoad";
    }

    private static void HandleSceneUnloaded(Scene scene)
    {
        CamerasBySceneHandle.Remove(scene.handle);
    }
}
