using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum TimeSegmentMask
{
    None = 0,
    Night = 1 << 0,
    Dawn = 1 << 1,
    Day = 1 << 2,
    Dusk = 1 << 3,
    All = Night | Dawn | Day | Dusk
}

/// <summary>
/// Shared serializable time-of-day predicate used by narrative and other gameplay systems.
/// Empty configuration passes by default. Segment and range filters combine as AND.
/// </summary>
[Serializable]
public class TimeOfDayPredicate
{
    [Tooltip("Optional segment filter. None means any segment is allowed.")]
    public TimeSegmentMask allowedSegments = TimeSegmentMask.None;

    [Tooltip("When enabled, the current normalized time must also fall within the configured inclusive range.")]
    public bool useNormalizedTimeRange = false;

    [Tooltip("Inclusive lower bound of the normalized time window.")]
    [Range(0f, 1f)]
    public float minNormalizedTime = 0f;

    [Tooltip("Inclusive upper bound of the normalized time window. When min is greater than max, the range wraps across midnight.")]
    [Range(0f, 1f)]
    public float maxNormalizedTime = 1f;
}

public static class TimeOfDayPredicateUtility
{
    public static bool HasConfiguredConstraints(TimeOfDayPredicate predicate)
    {
        return predicate != null &&
               (predicate.allowedSegments != TimeSegmentMask.None || predicate.useNormalizedTimeRange);
    }

    public static bool Matches(TimeOfDayPredicate predicate, ITimeOfDayService timeOfDayService)
    {
        if (!HasConfiguredConstraints(predicate))
            return true;

        if (timeOfDayService == null)
            return false;

        if (!MatchesSegments(predicate.allowedSegments, timeOfDayService.CurrentSegment))
            return false;

        if (!predicate.useNormalizedTimeRange)
            return true;

        return MatchesNormalizedTime(
            timeOfDayService.NormalizedTime,
            predicate.minNormalizedTime,
            predicate.maxNormalizedTime);
    }

    public static bool MatchesSegments(TimeSegmentMask allowedSegments, TimeSegment segment)
    {
        if (allowedSegments == TimeSegmentMask.None)
            return true;

        return (allowedSegments & ToMask(segment)) != 0;
    }

    public static bool MatchesNormalizedTime(float normalizedTime, float minInclusive, float maxInclusive)
    {
        float time = Mathf.Repeat(normalizedTime, 1f);
        float min = Mathf.Repeat(minInclusive, 1f);
        float max = Mathf.Repeat(maxInclusive, 1f);

        if (min <= max)
            return time >= min && time <= max;

        return time >= min || time <= max;
    }

    public static void AppendValidationWarnings(
        TimeOfDayPredicate predicate,
        List<string> warnings,
        TimeSegmentThresholds thresholds)
    {
        if (predicate == null || warnings == null || !HasConfiguredConstraints(predicate))
            return;

        if (predicate.useNormalizedTimeRange)
        {
            if (predicate.minNormalizedTime < 0f || predicate.minNormalizedTime > 1f)
            {
                warnings.Add($"Normalized time minimum {predicate.minNormalizedTime:0.###} is outside the expected [0..1] range.");
            }

            if (predicate.maxNormalizedTime < 0f || predicate.maxNormalizedTime > 1f)
            {
                warnings.Add($"Normalized time maximum {predicate.maxNormalizedTime:0.###} is outside the expected [0..1] range.");
            }
        }

        if (!thresholds.IsValid())
            return;

        if (predicate.allowedSegments == TimeSegmentMask.All)
            return;

        if (predicate.allowedSegments != TimeSegmentMask.None &&
            predicate.useNormalizedTimeRange &&
            !RangeCanIntersectAllowedSegments(predicate, thresholds))
        {
            warnings.Add("Allowed segments and normalized time range do not overlap under the current segment thresholds.");
        }
    }

    public static bool UsesSegment(TimeOfDayPredicate predicate, TimeSegment segment)
    {
        return predicate != null && MatchesSegments(predicate.allowedSegments, segment);
    }

    private static bool RangeCanIntersectAllowedSegments(TimeOfDayPredicate predicate, TimeSegmentThresholds thresholds)
    {
        List<TimeRange> allowedRanges = GetAllowedSegmentRanges(predicate.allowedSegments, thresholds);
        List<TimeRange> predicateRanges = GetNormalizedRanges(predicate.minNormalizedTime, predicate.maxNormalizedTime);

        for (int allowedIndex = 0; allowedIndex < allowedRanges.Count; allowedIndex++)
        {
            TimeRange allowed = allowedRanges[allowedIndex];
            for (int predicateIndex = 0; predicateIndex < predicateRanges.Count; predicateIndex++)
            {
                if (RangesOverlapInclusive(allowed, predicateRanges[predicateIndex]))
                    return true;
            }
        }

        return false;
    }

    private static List<TimeRange> GetAllowedSegmentRanges(TimeSegmentMask mask, TimeSegmentThresholds thresholds)
    {
        List<TimeRange> ranges = new List<TimeRange>();
        AppendSegmentRanges(mask, TimeSegment.Night, thresholds, ranges);
        AppendSegmentRanges(mask, TimeSegment.Dawn, thresholds, ranges);
        AppendSegmentRanges(mask, TimeSegment.Day, thresholds, ranges);
        AppendSegmentRanges(mask, TimeSegment.Dusk, thresholds, ranges);
        return ranges;
    }

    private static void AppendSegmentRanges(
        TimeSegmentMask mask,
        TimeSegment segment,
        TimeSegmentThresholds thresholds,
        List<TimeRange> ranges)
    {
        if ((mask & ToMask(segment)) == 0)
            return;

        switch (segment)
        {
            case TimeSegment.Night:
                ranges.Add(new TimeRange(0f, thresholds.dawnStart));
                ranges.Add(new TimeRange(thresholds.nightStart, 1f));
                break;
            case TimeSegment.Dawn:
                ranges.Add(new TimeRange(thresholds.dawnStart, thresholds.dayStart));
                break;
            case TimeSegment.Day:
                ranges.Add(new TimeRange(thresholds.dayStart, thresholds.duskStart));
                break;
            case TimeSegment.Dusk:
                ranges.Add(new TimeRange(thresholds.duskStart, thresholds.nightStart));
                break;
        }
    }

    private static List<TimeRange> GetNormalizedRanges(float minInclusive, float maxInclusive)
    {
        List<TimeRange> ranges = new List<TimeRange>();
        float min = Mathf.Repeat(minInclusive, 1f);
        float max = Mathf.Repeat(maxInclusive, 1f);

        if (min <= max)
        {
            ranges.Add(new TimeRange(min, max));
            return ranges;
        }

        ranges.Add(new TimeRange(0f, max));
        ranges.Add(new TimeRange(min, 1f));
        return ranges;
    }

    private static bool RangesOverlapInclusive(TimeRange a, TimeRange b)
    {
        return a.Min <= b.Max && b.Min <= a.Max;
    }

    private static TimeSegmentMask ToMask(TimeSegment segment)
    {
        switch (segment)
        {
            case TimeSegment.Night:
                return TimeSegmentMask.Night;
            case TimeSegment.Dawn:
                return TimeSegmentMask.Dawn;
            case TimeSegment.Day:
                return TimeSegmentMask.Day;
            case TimeSegment.Dusk:
                return TimeSegmentMask.Dusk;
            default:
                return TimeSegmentMask.None;
        }
    }

    private readonly struct TimeRange
    {
        public TimeRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Min { get; }
        public float Max { get; }
    }
}
