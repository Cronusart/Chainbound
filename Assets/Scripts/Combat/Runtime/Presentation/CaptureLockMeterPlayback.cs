using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Maps capture lock-meter basis points onto synth playback.
    /// 0 = escaping (low/slow), 10000 = locking (high/fast).
    /// </summary>
    public static class CaptureLockMeterPlayback
    {
        public static float PitchForMeter(int meterBasisPoints, float escapePitch, float lockPitch)
        {
            float t = Mathf.Clamp01(meterBasisPoints / 10000f);
            return Mathf.Lerp(escapePitch, lockPitch, t);
        }
    }
}
