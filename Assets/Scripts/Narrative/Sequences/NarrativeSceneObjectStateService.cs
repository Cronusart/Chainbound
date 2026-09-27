using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persists desired GameObject active state across scene loads for narrative-driven barriers, doors, etc.
/// Keys are stable IDs from <see cref="NarrativeSceneObjectBinding"/> or fallback GameObject names.
/// Lives under GameServices (DontDestroyOnLoad).
/// </summary>
[DisallowMultipleComponent]
public sealed class NarrativeSceneObjectStateService : MonoBehaviour
{
    public static NarrativeSceneObjectStateService Instance { get; private set; }

    [SerializeField] private bool enableDebugLogs;

    private readonly Dictionary<string, bool> _desiredActiveByKey = new Dictionary<string, bool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Records desired active state and applies it immediately if the target exists in any loaded scene.
    /// </summary>
    public void RecordDesiredState(string key, bool desiredActive)
    {
        if (string.IsNullOrEmpty(key))
            return;

        _desiredActiveByKey[key] = desiredActive;
        ApplyKey(key);
    }

    /// <summary>
    /// Removes a key from persistence (optional cleanup when narrative clears an object permanently).
    /// </summary>
    public bool ClearDesiredState(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        return _desiredActiveByKey.Remove(key);
    }

    public bool TryGetDesiredState(string key, out bool desiredActive)
    {
        return _desiredActiveByKey.TryGetValue(key, out desiredActive);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReapplyAll();
    }

    private void ReapplyAll()
    {
        foreach (var key in _desiredActiveByKey.Keys)
            ApplyKey(key);
    }

    private void ApplyKey(string key)
    {
        if (!_desiredActiveByKey.TryGetValue(key, out bool desired))
            return;

        GameObject target = ResolveTarget(key);
        if (target == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[NarrativeSceneObjectStateService] No target yet for key '{key}' (will apply when scene loads).");
            return;
        }

        if (target.activeSelf != desired)
            target.SetActive(desired);

        if (enableDebugLogs)
            Debug.Log($"[NarrativeSceneObjectStateService] Applied '{key}' -> active={desired} on '{target.name}'.");
    }

    private GameObject ResolveTarget(string key)
    {
        NarrativeSceneObjectBinding[] bindings = FindObjectsOfType<NarrativeSceneObjectBinding>(true);
        for (int i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (b == null || string.IsNullOrEmpty(b.ObjectId))
                continue;
            if (b.ObjectId == key)
                return b.ResolveTarget();
        }

        GameObject found = GameObject.Find(key);
        if (found != null)
            return found;

        GameObject[] all = FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == key)
                return all[i];
        }

        return null;
    }
}
