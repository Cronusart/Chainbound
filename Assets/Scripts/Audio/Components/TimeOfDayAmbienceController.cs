using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swaps one or more AmbienceZone looping beds based on the current time-of-day segment.
/// Sparse mappings fall back to each AmbienceZone's serialized default bed.
/// </summary>
[DisallowMultipleComponent]
public class TimeOfDayAmbienceController : MonoBehaviour
{
    [Serializable]
    private struct TimeSegmentBedMapping
    {
        public TimeSegment segment;
        public SoundEventSO ambienceBed;
    }

    [Header("References")]
    [SerializeField] private TimeOfDayManager timeManager;
    [SerializeField] private bool autoFindChildZones = true;
    [SerializeField] private bool includeInactiveChildZones = true;
    [SerializeField] private List<AmbienceZone> controlledZones = new List<AmbienceZone>();

    [Header("Bed Mapping")]
    [SerializeField] private List<TimeSegmentBedMapping> segmentBedMappings = new List<TimeSegmentBedMapping>();
    [SerializeField] private float bedTransitionSeconds = 1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Preserved so older single-zone setups migrate forward without losing their reference.
    [SerializeField, HideInInspector] private AmbienceZone ambienceZone;

    private bool isSubscribed = false;

    private void Awake()
    {
        RefreshControlledZones();
    }

    private void OnEnable()
    {
        TimeOfDayManager.OnInstanceAvailable += OnTimeManagerAvailable;

        RefreshControlledZones();
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
        bedTransitionSeconds = Mathf.Max(0f, bedTransitionSeconds);
        RefreshControlledZones();
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
        if (controlledZones.Count == 0)
            return;

        int updatedZoneCount = 0;

        for (int i = 0; i < controlledZones.Count; i++)
        {
            AmbienceZone zone = controlledZones[i];
            if (zone == null)
                continue;

            SoundEventSO resolvedBed = ResolveBedForSegment(zone, segment);
            if (zone.CurrentAmbienceSound == resolvedBed)
                continue;

            SoundEventSO overrideBed = zone.DefaultAmbienceSound == resolvedBed ? null : resolvedBed;
            zone.SetAmbienceBed(overrideBed, bedTransitionSeconds);
            updatedZoneCount++;
        }

        if (enableDebugLogs && updatedZoneCount > 0)
        {
            Debug.Log($"[TimeOfDayAmbienceController] {gameObject.name} applied {segment} ambience to {updatedZoneCount} zone(s).");
        }
    }

    private SoundEventSO ResolveBedForSegment(AmbienceZone zone, TimeSegment segment)
    {
        for (int i = 0; i < segmentBedMappings.Count; i++)
        {
            TimeSegmentBedMapping mapping = segmentBedMappings[i];
            if (mapping.segment == segment && mapping.ambienceBed != null)
                return mapping.ambienceBed;
        }

        return zone != null ? zone.DefaultAmbienceSound : null;
    }

    private void OnTransformChildrenChanged()
    {
        if (!autoFindChildZones)
            return;

        RefreshControlledZones();

        if (isActiveAndEnabled && timeManager != null)
        {
            ApplyCurrentSegment();
        }
    }

    private void RefreshControlledZones()
    {
        if (autoFindChildZones)
        {
            controlledZones.Clear();

            AmbienceZone[] discoveredZones = GetComponentsInChildren<AmbienceZone>(includeInactiveChildZones);
            for (int i = 0; i < discoveredZones.Length; i++)
            {
                AddControlledZone(discoveredZones[i]);
            }
        }

        MigrateLegacyZoneReference();
        RemoveNullZones();
    }

    private void MigrateLegacyZoneReference()
    {
        if (ambienceZone != null)
            AddControlledZone(ambienceZone);
    }

    private void AddControlledZone(AmbienceZone zone)
    {
        if (zone == null || controlledZones.Contains(zone))
            return;

        controlledZones.Add(zone);
    }

    private void RemoveNullZones()
    {
        for (int i = controlledZones.Count - 1; i >= 0; i--)
        {
            if (controlledZones[i] == null)
                controlledZones.RemoveAt(i);
        }
    }
}
