using System;

namespace Chainbound.Combat.Core
{
    [Serializable]
    public sealed class CaptureSimulationState
    {
        public int Tick { get; set; }
        public uint RngState { get; set; }
        public int TargetPositionUnits { get; set; }
        public int TargetVelocityUnitsPerSecond { get; set; }
        public int BandPositionUnits { get; set; }
        public int BandVelocityUnitsPerSecond { get; set; }
        public int MeterBasisPoints { get; set; } = 5000;
        public int MeterRemainder { get; set; }
        public bool IsInsideBand { get; set; }
        public bool IsHeld { get; set; }
        public CaptureOutcomeKind Outcome { get; set; }

        public CaptureSimulationState Clone()
        {
            return (CaptureSimulationState)MemberwiseClone();
        }
    }

    [Serializable]
    public sealed class CaptureTickResult
    {
        public CaptureSimulationState State { get; set; } = new CaptureSimulationState();
        public bool EnteredBand { get; set; }
        public bool LeftBand { get; set; }
        public bool SpikeOccurred { get; set; }
    }

    public static class CaptureSimulation
    {
        public static CaptureSimulationState CreateInitial(CaptureChallengeSnapshot challenge)
        {
            if (challenge == null) throw new ArgumentNullException(nameof(challenge));
            CaptureSimulationParameters parameters = Normalize(challenge.Parameters);
            var rng = new DeterministicCaptureRng(challenge.Seed);
            int margin = Math.Max(1, parameters.GraphUnits / 5);
            int target = rng.NextInt(margin, Math.Max(margin + 1, parameters.GraphUnits - margin));
            int band = parameters.GraphUnits / 2;
            int velocity = rng.NextInt(-parameters.TraceMaximumSpeedUnitsPerSecond / 4, parameters.TraceMaximumSpeedUnitsPerSecond / 4 + 1);
            bool inside = IsTargetInsideBand(target, band, parameters.BandHeightUnits);
            return new CaptureSimulationState
            {
                Tick = 0,
                RngState = rng.State,
                TargetPositionUnits = target,
                TargetVelocityUnitsPerSecond = velocity,
                BandPositionUnits = band,
                BandVelocityUnitsPerSecond = 0,
                MeterBasisPoints = 5000,
                MeterRemainder = 0,
                IsInsideBand = inside,
                IsHeld = false,
                Outcome = CaptureOutcomeKind.None,
            };
        }

        public static CaptureTickResult Step(
            CaptureSimulationState current,
            CaptureSimulationParameters sourceParameters,
            bool held)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            CaptureSimulationParameters parameters = Normalize(sourceParameters);
            if (current.Outcome != CaptureOutcomeKind.None)
            {
                return new CaptureTickResult { State = current.Clone() };
            }

            CaptureSimulationState next = current.Clone();
            var rng = new DeterministicCaptureRng(current.RngState);
            int tickRate = parameters.TickRate;

            int randomAcceleration = rng.NextInt(
                -parameters.TraceAccelerationUnitsPerSecondSquared,
                parameters.TraceAccelerationUnitsPerSecondSquared + 1);
            int centerOffset = next.TargetPositionUnits - (parameters.GraphUnits / 2);
            int meanReversion = -(int)((long)centerOffset * parameters.TraceMeanReversionUnitsPerSecondSquared /
                                      Math.Max(1, parameters.GraphUnits));
            int traceAcceleration = SaturatingAdd(randomAcceleration, meanReversion);
            next.TargetVelocityUnitsPerSecond = Clamp(
                SaturatingAdd(next.TargetVelocityUnitsPerSecond, traceAcceleration / tickRate),
                -parameters.TraceMaximumSpeedUnitsPerSecond,
                parameters.TraceMaximumSpeedUnitsPerSecond);

            bool spike = rng.RollBasisPoints(parameters.SpikeChanceBasisPointsPerTick);
            if (spike && parameters.SpikeVelocityImpulseUnitsPerSecond > 0)
            {
                int direction = rng.NextInt(0, 2) == 0 ? -1 : 1;
                next.TargetVelocityUnitsPerSecond = Clamp(
                    SaturatingAdd(next.TargetVelocityUnitsPerSecond, direction * parameters.SpikeVelocityImpulseUnitsPerSecond),
                    -parameters.TraceMaximumSpeedUnitsPerSecond,
                    parameters.TraceMaximumSpeedUnitsPerSecond);
            }

            next.TargetPositionUnits = SaturatingAdd(
                next.TargetPositionUnits,
                next.TargetVelocityUnitsPerSecond / tickRate);
            int targetPosition = next.TargetPositionUnits;
            int targetVelocity = next.TargetVelocityUnitsPerSecond;
            BounceAtEdges(
                ref targetPosition,
                ref targetVelocity,
                parameters.GraphUnits,
                parameters.TraceEdgeBounceBasisPoints);
            next.TargetPositionUnits = targetPosition;
            next.TargetVelocityUnitsPerSecond = targetVelocity;

            int bandAcceleration = held
                ? parameters.BandLiftAccelerationUnitsPerSecondSquared
                : -parameters.BandGravityUnitsPerSecondSquared;
            next.BandVelocityUnitsPerSecond = SaturatingAdd(
                next.BandVelocityUnitsPerSecond,
                bandAcceleration / tickRate);
            int dragPerTick = parameters.BandDragBasisPointsPerSecond / tickRate;
            next.BandVelocityUnitsPerSecond = (int)((long)next.BandVelocityUnitsPerSecond *
                                                    Math.Max(0, CaptureBalanceData.BasisPointScale - dragPerTick) /
                                                    CaptureBalanceData.BasisPointScale);
            next.BandVelocityUnitsPerSecond = Clamp(
                next.BandVelocityUnitsPerSecond,
                -parameters.BandMaximumSpeedUnitsPerSecond,
                parameters.BandMaximumSpeedUnitsPerSecond);
            next.BandPositionUnits = SaturatingAdd(
                next.BandPositionUnits,
                next.BandVelocityUnitsPerSecond / tickRate);
            int bandPosition = next.BandPositionUnits;
            int bandVelocity = next.BandVelocityUnitsPerSecond;
            BounceAtEdges(
                ref bandPosition,
                ref bandVelocity,
                parameters.GraphUnits,
                parameters.BandEdgeBounceBasisPoints);
            next.BandPositionUnits = bandPosition;
            next.BandVelocityUnitsPerSecond = bandVelocity;

            bool wasInside = current.IsInsideBand;
            bool isInside = IsTargetInsideBand(next.TargetPositionUnits, next.BandPositionUnits, parameters.BandHeightUnits);
            if (isInside != wasInside)
                next.MeterRemainder = 0;
            int rate = isInside
                ? parameters.LockRateBasisPointsPerSecond
                : -parameters.EscapeRateBasisPointsPerSecond;
            int meterNumerator = SaturatingAdd(rate, next.MeterRemainder);
            int signedDelta = meterNumerator / tickRate;
            next.MeterRemainder = meterNumerator % tickRate;
            next.MeterBasisPoints = Clamp(
                SaturatingAdd(next.MeterBasisPoints, signedDelta),
                0,
                CaptureBalanceData.BasisPointScale);
            next.Tick++;
            next.RngState = rng.State;
            next.IsHeld = held;
            next.IsInsideBand = isInside;

            if (next.MeterBasisPoints >= CaptureBalanceData.BasisPointScale)
                next.Outcome = CaptureOutcomeKind.Locked;
            else if (next.MeterBasisPoints <= 0)
                next.Outcome = CaptureOutcomeKind.Escaped;
            else if (next.Tick >= parameters.DurationTicks)
                next.Outcome = CaptureOutcomeKind.TimedOut;

            return new CaptureTickResult
            {
                State = next,
                EnteredBand = !wasInside && isInside,
                LeftBand = wasInside && !isInside,
                SpikeOccurred = spike,
            };
        }

        public static CaptureAuthoritativeState ToSnapshot(string attemptId, int version, CaptureSimulationState state, CaptureSimulationParameters parameters, bool spikeOccurred)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            CaptureSimulationParameters normalized = Normalize(parameters);
            return new CaptureAuthoritativeState
            {
                AttemptId = attemptId ?? string.Empty,
                Version = version,
                Tick = state.Tick,
                TargetPositionUnits = state.TargetPositionUnits,
                BandPositionUnits = state.BandPositionUnits,
                MeterBasisPoints = state.MeterBasisPoints,
                RemainingTicks = Math.Max(0, normalized.DurationTicks - state.Tick),
                IsHeld = state.IsHeld,
                IsInsideBand = state.IsInsideBand,
                SpikeOccurred = spikeOccurred,
                Outcome = state.Outcome,
            };
        }

        private static CaptureSimulationParameters Normalize(CaptureSimulationParameters source)
        {
            CaptureSimulationParameters value = source ?? new CaptureSimulationParameters();
            return new CaptureSimulationParameters
            {
                TickRate = Clamp(value.TickRate, 1, 120),
                DurationTicks = Clamp(value.DurationTicks, 1, 120 * 600),
                GraphUnits = Clamp(value.GraphUnits, 1, 1000000),
                BandHeightUnits = Clamp(value.BandHeightUnits, 1, Math.Max(1, value.GraphUnits)),
                LockRateBasisPointsPerSecond = Math.Max(0, value.LockRateBasisPointsPerSecond),
                EscapeRateBasisPointsPerSecond = Math.Max(0, value.EscapeRateBasisPointsPerSecond),
                BandLiftAccelerationUnitsPerSecondSquared = Math.Max(0, value.BandLiftAccelerationUnitsPerSecondSquared),
                BandGravityUnitsPerSecondSquared = Math.Max(0, value.BandGravityUnitsPerSecondSquared),
                BandDragBasisPointsPerSecond = Math.Max(0, value.BandDragBasisPointsPerSecond),
                BandMaximumSpeedUnitsPerSecond = Math.Max(1, value.BandMaximumSpeedUnitsPerSecond),
                BandEdgeBounceBasisPoints = Clamp(value.BandEdgeBounceBasisPoints, 0, CaptureBalanceData.BasisPointScale),
                TraceAccelerationUnitsPerSecondSquared = Math.Max(0, value.TraceAccelerationUnitsPerSecondSquared),
                TraceMaximumSpeedUnitsPerSecond = Math.Max(1, value.TraceMaximumSpeedUnitsPerSecond),
                TraceMeanReversionUnitsPerSecondSquared = Math.Max(0, value.TraceMeanReversionUnitsPerSecondSquared),
                TraceEdgeBounceBasisPoints = Clamp(value.TraceEdgeBounceBasisPoints, 0, CaptureBalanceData.BasisPointScale),
                SpikeChanceBasisPointsPerTick = Clamp(value.SpikeChanceBasisPointsPerTick, 0, CaptureBalanceData.BasisPointScale),
                SpikeVelocityImpulseUnitsPerSecond = Math.Max(0, value.SpikeVelocityImpulseUnitsPerSecond),
            };
        }

        public static bool IsTargetInsideBand(int targetPosition, int bandPosition, int bandHeight)
        {
            return Math.Abs((long)targetPosition - bandPosition) * 2L <= bandHeight;
        }

        private static void BounceAtEdges(ref int position, ref int velocity, int maximum, int bounceBasisPoints)
        {
            if (position < 0)
            {
                position = -position;
                velocity = Math.Abs((int)((long)velocity * bounceBasisPoints / CaptureBalanceData.BasisPointScale));
            }
            else if (position > maximum)
            {
                position = maximum - (position - maximum);
                velocity = -Math.Abs((int)((long)velocity * bounceBasisPoints / CaptureBalanceData.BasisPointScale));
            }

            position = Clamp(position, 0, maximum);
        }

        private static int SaturatingAdd(int left, int right)
        {
            long value = (long)left + right;
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
