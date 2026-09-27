using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Cross-track narrative signal (lighter-weight than TriggerRegistry; for sync/barriers).
/// </summary>
[Serializable]
public struct NarrativeSignalRecord
{
    public string signalId;
    public string sourceTrackId;
    public string sourceSequenceId;
    public string payload;
    public float time;
}

/// <summary>
/// Runtime bus for narrative signals. DontDestroyOnLoad singleton.
/// </summary>
public class NarrativeSignalBus : MonoBehaviour
{
    private static NarrativeSignalBus instance;

    public static NarrativeSignalBus Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<NarrativeSignalBus>();
                if (instance == null)
                {
                    var go = new GameObject("NarrativeSignalBus");
                    instance = go.AddComponent<NarrativeSignalBus>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [SerializeField] private int recentBufferSize = 32;

    private readonly HashSet<string> activeSignals = new HashSet<string>();
    private readonly Queue<NarrativeSignalRecord> recent = new Queue<NarrativeSignalRecord>();

    public event Action<NarrativeSignalRecord> OnSignalEmitted;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        // A service nested under the authoritative GameServices root inherits
        // that root's persistence. Unity only accepts DontDestroyOnLoad for a
        // root GameObject, so standalone fallback instances persist themselves
        // while prefab-owned instances leave ownership to GameServicesContainer.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void Emit(string signalId, string sourceTrackId = "", string sourceSequenceId = "", string payload = null)
    {
        if (string.IsNullOrEmpty(signalId))
            return;

        activeSignals.Add(signalId);
        var rec = new NarrativeSignalRecord
        {
            signalId = signalId,
            sourceTrackId = sourceTrackId ?? "",
            sourceSequenceId = sourceSequenceId ?? "",
            payload = payload ?? "",
            time = Time.unscaledTime
        };
        recent.Enqueue(rec);
        while (recent.Count > recentBufferSize)
            recent.Dequeue();

        OnSignalEmitted?.Invoke(rec);
    }

    public bool HasSignal(string signalId)
    {
        return !string.IsNullOrEmpty(signalId) && activeSignals.Contains(signalId);
    }

    public void ClearSignal(string signalId)
    {
        if (!string.IsNullOrEmpty(signalId))
            activeSignals.Remove(signalId);
    }

    public void ClearAll()
    {
        activeSignals.Clear();
    }

    public IReadOnlyCollection<NarrativeSignalRecord> GetRecentSignals()
    {
        return recent.ToArray();
    }
}
