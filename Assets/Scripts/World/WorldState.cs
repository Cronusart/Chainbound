using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight global/world flags for narrative and environment systems. Separate from per-sequence variables.
/// </summary>
public class WorldState : MonoBehaviour
{
    private static WorldState instance;

    public static WorldState Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<WorldState>();
                if (instance == null)
                {
                    var go = new GameObject("WorldState");
                    instance = go.AddComponent<WorldState>();
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Optional sync")]
    [Tooltip("When assigned, currentTimePhase can mirror TimeOfDayManager segment name.")]
    [SerializeField] private bool syncTimePhaseFromTimeOfDay = true;

    private readonly Dictionary<string, bool> globalFlags = new Dictionary<string, bool>();
    private readonly Dictionary<string, string> regionStates = new Dictionary<string, string>();

    [SerializeField] private string currentTimePhase = "";

    public event Action OnWorldStateChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        if (transform.parent == null && Application.isPlaying)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (!syncTimePhaseFromTimeOfDay)
            return;
        var tod = TimeOfDayManager.Instance;
        if (tod == null)
            return;
        string phase = tod.IsNight ? "Night" : "Day";
        if (currentTimePhase != phase)
        {
            currentTimePhase = phase;
            OnWorldStateChanged?.Invoke();
        }
    }

    public string CurrentTimePhase
    {
        get => currentTimePhase;
        set
        {
            if (currentTimePhase == value) return;
            currentTimePhase = value ?? "";
            OnWorldStateChanged?.Invoke();
        }
    }

    public void SetGlobalFlag(string key, bool value)
    {
        if (string.IsNullOrEmpty(key)) return;
        globalFlags[key] = value;
        OnWorldStateChanged?.Invoke();
    }

    public bool GetGlobalFlag(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return globalFlags.TryGetValue(key, out bool v) ? v : defaultValue;
    }

    public bool HasGlobalFlag(string key)
    {
        return !string.IsNullOrEmpty(key) && globalFlags.TryGetValue(key, out bool v) && v;
    }

    public void SetRegionState(string regionId, string state)
    {
        if (string.IsNullOrEmpty(regionId)) return;
        regionStates[regionId] = state ?? "";
        OnWorldStateChanged?.Invoke();
    }

    public string GetRegionState(string regionId, string defaultValue = "")
    {
        if (string.IsNullOrEmpty(regionId)) return defaultValue;
        return regionStates.TryGetValue(regionId, out string s) ? s : defaultValue;
    }
}
