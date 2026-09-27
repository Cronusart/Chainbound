using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines spawning rules based on time-of-day.
/// Used by creature spawners to determine when entities can spawn.
/// </summary>
[Serializable]
public class SpawnRule
{
    [Tooltip("Segments during which this spawn is allowed")]
    [SerializeField] private List<TimeSegment> allowedSegments = new List<TimeSegment> { TimeSegment.Day };
    
    [Tooltip("Minimum normalized time for spawning (0-1). -1 = no minimum.")]
    [Range(-1f, 1f)]
    [SerializeField] private float minNormalizedTime = -1f;
    
    [Tooltip("Maximum normalized time for spawning (0-1). -1 = no maximum.")]
    [Range(-1f, 1f)]
    [SerializeField] private float maxNormalizedTime = -1f;
    
    [Tooltip("Weight multiplier per segment (affects spawn probability)")]
    [SerializeField] private SegmentWeight[] segmentWeights = new SegmentWeight[]
    {
        new SegmentWeight { segment = TimeSegment.Day, weight = 1f }
    };
    
    [Tooltip("If true, can spawn during held time states")]
    [SerializeField] private bool allowDuringHold = false;
    
    [Tooltip("If true, can spawn during paused time")]
    [SerializeField] private bool allowDuringPause = false;
    
    [Serializable]
    public struct SegmentWeight
    {
        public TimeSegment segment;
        [Range(0f, 5f)]
        public float weight;
    }
    
    /// <summary>Segments during which spawning is allowed.</summary>
    public IReadOnlyList<TimeSegment> AllowedSegments => allowedSegments;
    
    /// <summary>Check if spawning is currently allowed based on time state.</summary>
    public bool CanSpawn(ITimeOfDayService timeService)
    {
        if (timeService == null) return true; // No time service = always allow
        
        // Check pause/hold states
        if (timeService.IsPaused && !allowDuringPause) return false;
        if (timeService.IsHeld && !allowDuringHold) return false;
        
        // Check segment
        if (!IsSegmentAllowed(timeService.CurrentSegment)) return false;
        
        // Check time range
        float t = timeService.NormalizedTime;
        if (minNormalizedTime >= 0f && t < minNormalizedTime) return false;
        if (maxNormalizedTime >= 0f && t > maxNormalizedTime) return false;
        
        return true;
    }
    
    /// <summary>Check if a specific segment is allowed.</summary>
    public bool IsSegmentAllowed(TimeSegment segment)
    {
        return allowedSegments.Contains(segment);
    }
    
    /// <summary>Get spawn weight for current time state.</summary>
    public float GetSpawnWeight(ITimeOfDayService timeService)
    {
        if (timeService == null) return 1f;
        if (!CanSpawn(timeService)) return 0f;
        
        return GetSegmentWeight(timeService.CurrentSegment);
    }
    
    /// <summary>Get weight multiplier for a specific segment.</summary>
    public float GetSegmentWeight(TimeSegment segment)
    {
        foreach (var sw in segmentWeights)
        {
            if (sw.segment == segment) return sw.weight;
        }
        return IsSegmentAllowed(segment) ? 1f : 0f;
    }
    
    // ============================================
    // STATIC FACTORY METHODS
    // ============================================
    
    /// <summary>Create a rule allowing spawning only during day.</summary>
    public static SpawnRule DayOnly()
    {
        return new SpawnRule
        {
            allowedSegments = new List<TimeSegment> { TimeSegment.Day },
            segmentWeights = new SegmentWeight[] { new SegmentWeight { segment = TimeSegment.Day, weight = 1f } }
        };
    }
    
    /// <summary>Create a rule allowing spawning only during night.</summary>
    public static SpawnRule NightOnly()
    {
        return new SpawnRule
        {
            allowedSegments = new List<TimeSegment> { TimeSegment.Night },
            segmentWeights = new SegmentWeight[] { new SegmentWeight { segment = TimeSegment.Night, weight = 1f } }
        };
    }
    
    /// <summary>Create a rule allowing spawning at any time.</summary>
    public static SpawnRule Always()
    {
        return new SpawnRule
        {
            allowedSegments = new List<TimeSegment> { TimeSegment.Night, TimeSegment.Dawn, TimeSegment.Day, TimeSegment.Dusk },
            allowDuringHold = true,
            allowDuringPause = true
        };
    }
    
    /// <summary>Create a rule for twilight creatures (dawn and dusk only).</summary>
    public static SpawnRule TwilightOnly()
    {
        return new SpawnRule
        {
            allowedSegments = new List<TimeSegment> { TimeSegment.Dawn, TimeSegment.Dusk },
            segmentWeights = new SegmentWeight[]
            {
                new SegmentWeight { segment = TimeSegment.Dawn, weight = 1f },
                new SegmentWeight { segment = TimeSegment.Dusk, weight = 1f }
            }
        };
    }
    
    /// <summary>Create a nocturnal rule (night, dawn, dusk - not midday).</summary>
    public static SpawnRule Nocturnal()
    {
        return new SpawnRule
        {
            allowedSegments = new List<TimeSegment> { TimeSegment.Night, TimeSegment.Dawn, TimeSegment.Dusk },
            segmentWeights = new SegmentWeight[]
            {
                new SegmentWeight { segment = TimeSegment.Night, weight = 1.5f },
                new SegmentWeight { segment = TimeSegment.Dawn, weight = 0.5f },
                new SegmentWeight { segment = TimeSegment.Dusk, weight = 0.8f }
            }
        };
    }
}

/// <summary>
/// ScriptableObject wrapper for SpawnRule for easy asset creation.
/// </summary>
[CreateAssetMenu(fileName = "SpawnRule", menuName = "Chainbound/World/Time of Day/Spawn Rule")]
public class SpawnRuleSO : ScriptableObject
{
    [SerializeField] private SpawnRule rule = SpawnRule.DayOnly();
    
    /// <summary>The spawn rule data.</summary>
    public SpawnRule Rule => rule;
    
    /// <summary>Check if spawning is allowed.</summary>
    public bool CanSpawn(ITimeOfDayService timeService) => rule.CanSpawn(timeService);
    
    /// <summary>Get spawn weight.</summary>
    public float GetSpawnWeight(ITimeOfDayService timeService) => rule.GetSpawnWeight(timeService);
}

