using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Narrow scene-IO boundary. The production backend uses SceneManager; tests
/// and a future Addressables adapter can replace it without changing callers.
/// </summary>
public interface ISceneOperationBackend
{
    bool CanLoad(string sceneId);
    bool TryBeginLoad(string sceneId, out ISceneLoadOperation operation, out string error);
    bool TryBeginLoadAdditive(string sceneId, out ISceneLoadOperation operation, out string error);
    bool TryBeginUnload(Scene scene, out ISceneOperation operation, out string error);
    bool SetActiveScene(Scene scene, out string error);
}

public interface ISceneOperation
{
    float Progress { get; }
    bool IsDone { get; }
}

public interface ISceneLoadOperation : ISceneOperation
{
    bool AllowSceneActivation { get; set; }
}

public sealed class UnitySceneOperationBackend : ISceneOperationBackend
{
    private sealed class UnityLoadOperation : ISceneLoadOperation
    {
        private readonly AsyncOperation operation;

        public UnityLoadOperation(AsyncOperation operation)
        {
            this.operation = operation;
        }

        public float Progress => operation.progress;
        public bool IsDone => operation.isDone;
        public bool AllowSceneActivation
        {
            get => operation.allowSceneActivation;
            set => operation.allowSceneActivation = value;
        }
    }

    private sealed class UnityOperation : ISceneOperation
    {
        private readonly AsyncOperation operation;

        public UnityOperation(AsyncOperation operation)
        {
            this.operation = operation;
        }

        public float Progress => operation.progress;
        public bool IsDone => operation.isDone;
    }

    public bool CanLoad(string sceneId)
    {
        return Application.CanStreamedLevelBeLoaded(sceneId);
    }

    public bool TryBeginLoad(
        string sceneId,
        out ISceneLoadOperation operation,
        out string error)
    {
        return TryBeginLoad(sceneId, LoadSceneMode.Single, out operation, out error);
    }

    public bool TryBeginLoadAdditive(
        string sceneId,
        out ISceneLoadOperation operation,
        out string error)
    {
        return TryBeginLoad(sceneId, LoadSceneMode.Additive, out operation, out error);
    }

    public bool TryBeginUnload(Scene scene, out ISceneOperation operation, out string error)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            operation = null;
            error = "The requested scene is not loaded.";
            return false;
        }

        try
        {
            AsyncOperation unityOperation = SceneManager.UnloadSceneAsync(scene);
            if (unityOperation == null)
            {
                operation = null;
                error = $"Unity did not create an unload operation for '{scene.name}'.";
                return false;
            }

            operation = new UnityOperation(unityOperation);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            operation = null;
            error = $"Unable to start unloading '{scene.name}': {exception.Message}";
            return false;
        }
    }

    public bool SetActiveScene(Scene scene, out string error)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            error = "The requested active scene is not loaded.";
            return false;
        }

        try
        {
            if (!SceneManager.SetActiveScene(scene))
            {
                error = $"Unity rejected '{scene.name}' as the active scene.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Unable to set '{scene.name}' active: {exception.Message}";
            return false;
        }
    }

    private static bool TryBeginLoad(
        string sceneId,
        LoadSceneMode loadMode,
        out ISceneLoadOperation operation,
        out string error)
    {
        try
        {
            AsyncOperation unityOperation = SceneManager.LoadSceneAsync(sceneId, loadMode);
            if (unityOperation == null)
            {
                operation = null;
                error = $"Unity did not create a load operation for '{sceneId}'.";
                return false;
            }

            operation = new UnityLoadOperation(unityOperation);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            operation = null;
            error = $"Unable to start loading '{sceneId}': {exception.Message}";
            return false;
        }
    }
}
