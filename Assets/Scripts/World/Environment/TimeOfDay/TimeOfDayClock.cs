using System;
using UnityEngine;

/// <summary>
/// Shared 24-hour CST (UTC-6) mapping for time-of-day presentation.
/// Solana cluster unix time and the device fallback both convert through here
/// so lighting samples the same 0 = midnight .. 0.5 = noon day fraction.
/// </summary>
public static class TimeOfDayClock
{
    public const int CstOffsetHours = -6;
    public const float SecondsPerDay = 86400f;

    public static float Wrap01(float normalizedTime)
    {
        return normalizedTime - Mathf.Floor(normalizedTime);
    }

    public static float HoursToNormalized(float hours)
    {
        return Wrap01(hours / 24f);
    }

    public static float NormalizedToHours(float normalizedTime)
    {
        return Wrap01(normalizedTime) * 24f;
    }

    public static float FromUnixSeconds(long unixSeconds)
    {
        DateTimeOffset cst = DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            .ToOffset(TimeSpan.FromHours(CstOffsetHours));
        return (float)(cst.TimeOfDay.TotalSeconds / SecondsPerDay);
    }

    /// <summary>
    /// Display-only fallback when Solana block time has not synced yet.
    /// Durable systems must not use this value.
    /// </summary>
    public static float FromDeviceUtcMinusSix()
    {
        DateTime local = DateTime.UtcNow.AddHours(CstOffsetHours);
        return (float)(local.TimeOfDay.TotalSeconds / SecondsPerDay);
    }
}
