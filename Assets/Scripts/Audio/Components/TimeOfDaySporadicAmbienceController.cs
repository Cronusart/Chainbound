using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swaps one or more AmbienceZoneSporadicLayer profiles based on the current time-of-day segment.
/// Sparse mappings fall back to each layer's default profile or inline settings.
/// </summary>
[DisallowMultipleComponent]
public class TimeOfDaySporadicAmbienceController : MonoBehaviour
{
    [Serializable]
    private struct TimeSegmentProfileMapping
    {
        public TimeSegment segment;
        public SporadicAmbienceProfileSO profile;
    }

    [Header("References")]
    [SerializeField] private TimeOfDayManager timeManager;
    [SerializeField] private bool autoFindChildLayers = true;
    [SerializeField] private bool includeInactiveChildLayers = true;
    [SerializeField] private List<AmbienceZoneSporadicLayer> controlledLayers = new List<AmbienceZoneSporadicLayer>();

    [Header("Profile Mapping")]
    [SerializeField] private List<TimeSegmentProfileMapping> segmentProfileMappings = new List<TimeSegmentProfileMapping>();
    [SerializeField] private bool rescheduleOnProfileChange = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool isSubscribed = false;

    private void Awake()
    {
        RefreshControlledLayers();
    }

    private void OnEnable()
    {
        TimeOfDayManager.OnInstanceAvailable += OnTimeManagerAvailable;

        RefreshControlledLayers();
        ResolveTimeManager();
        Subscribe();
        ApplyCurrentSegment();
    }

    private void OnDisable()
    {
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
    }

    private void OnDestroy()
    {
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
    }

    private void OnValidate()
    {
        RefreshControlledLayers();
    }

    private void ResolveTimeManager()
    {
        if (timeManager == null)
            timeManager = TimeOfDayManager.Instance;
    }

    private void Subscribe()
    {
        if (isSubscribed || timeManager == null)
            return;

        timeManager.OnSegmentChanged += OnSegmentChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || timeManager == null)
            return;

        timeManager.OnSegmentChanged -= OnSegmentChanged;
        isSubscribed = false;
    }

    private void OnTimeManagerAvailable(TimeOfDayManager manager)
    {
        if (manager == null)
            return;

        if (timeManager == manager && isSubscribed)
        {
            ApplyCurrentSegment();
            return;
        }

        Unsubscribe();
        timeManager = manager;
        Subscribe();
        ApplyCurrentSegment();
    }

    private void OnSegmentChanged(TimeSegment previousSegment, TimeSegment nextSegment)
    {
        ApplySegment(nextSegment);
    }

    private void ApplyCurrentSegment()
    {
        if (timeManager == null)
            return;

        ApplySegment(timeManager.CurrentSegment);
    }

    private void ApplySegment(TimeSegment segment)
    {
        if (controlledLayers.Count == 0)
            return;

        int updatedLayerCount = 0;

        for (int i = 0; i < controlledLayers.Count; i++)
        {
            AmbienceZoneSporadicLayer layer = controlledLayers[i];
            if (layer == null)
                continue;

            SporadicAmbienceProfileSO resolvedProfile = ResolveProfileForSegment(layer, segment);
            SporadicAmbienceProfileSO overrideProfile = layer.DefaultProfile == resolvedProfile ? null : resolvedProfile;

            if (layer.CurrentProfileOverride == overrideProfile)
                continue;

            layer.SetSporadicProfile(overrideProfile, rescheduleOnProfileChange);
            updatedLayerCount++;
        }

        if (enableDebugLogs && updatedLayerCount > 0)
        {
            Debug.Log($"[TimeOfDaySporadicAmbienceController] {gameObject.name} applied {segment} sporadic ambience to {updatedLayerCount} layer(s).");
        }
    }

    private SporadicAmbienceProfileSO ResolveProfileForSegment(AmbienceZoneSporadicLayer layer, TimeSegment segment)
    {
        for (int i = 0; i < segmentProfileMappings.Count; i++)
        {
            TimeSegmentProfileMapping mapping = segmentProfileMappings[i];
            if (mapping.segment == segment && mapping.profile != null)
                return mapping.profile;
        }

        return layer != null ? layer.DefaultProfile : null;
    }

    private void OnTransformChildrenChanged()
    {
        if (!autoFindChildLayers)
            return;

        RefreshControlledLayers();

        if (isActiveAndEnabled && timeManager != null)
        {
            ApplyCurrentSegment();
        }
    }

    private void RefreshControlledLayers()
    {
        if (autoFindChildLayers)
        {
            controlledLayers.Clear();

            AmbienceZoneSporadicLayer[] discoveredLayers = GetComponentsInChildren<AmbienceZoneSporadicLayer>(includeInactiveChildLayers);
            for (int i = 0; i < discoveredLayers.Length; i++)
            {
                AddControlledLayer(discoveredLayers[i]);
            }
        }

        RemoveNullLayers();
    }

    private void AddControlledLayer(AmbienceZoneSporadicLayer layer)
    {
        if (layer == null || controlledLayers.Contains(layer))
            return;

        controlledLayers.Add(layer);
    }

    private void RemoveNullLayers()
    {
        for (int i = controlledLayers.Count - 1; i >= 0; i--)
        {
            if (controlledLayers[i] == null)
                controlledLayers.RemoveAt(i);
        }
    }
}
