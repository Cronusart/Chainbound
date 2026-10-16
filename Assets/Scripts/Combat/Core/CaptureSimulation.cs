using System;

namespace Chainbound.Combat.Core
{
    public enum CaptureTraceRegime
    {
        Consolidation = 0,
        Impulse = 1,
    }

    [Serializable]
    public sealed class CaptureSimulationState
    {
        public int Tick { get; set; }
        public uint RngState { get; set; }
        public int TargetPositionUnits { get; set; }
        public int TargetVelocityUnitsPerSecond { get; set; }
        public CaptureTraceRegime TraceRegime { get; set; }
        public int TraceRegimeTicksRemaining { get; set; }
        public int TraceConsolidationAnchorUnits { get; set; }
        public int TraceImpulseTargetPositionUnits { get; set; }
        public bool IsTraceRegimeInitialized { get; set; }
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
            int dwellTicks = NextConsolidationDwellTicks(ref rng, parameters);
            bool inside = IsTargetInsideBand(target, band, parameters.BandHeightUnits);
            return new CaptureSimulationState
            {
                Tick = 0,
                RngState = rng.State,
                TargetPositionUnits = target,
                TargetVelocityUnitsPerSecond = 0,
                TraceRegime = CaptureTraceRegime.Consolidation,
                TraceRegimeTicksRemaining = dwellTicks,
                TraceConsolidationAnchorUnits = target,
                TraceImpulseTargetPositionUnits = target,
                IsTraceRegimeInitialized = true,
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
            bool spike = StepTrace(next, parameters, ref rng);

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

        /// <summary>
        /// Re-anchors client prediction after an authoritative position snap. The
        /// authority snapshot deliberately does not carry trace velocity or RNG,
        /// so an in-flight local dart must not continue from stale momentum.
        /// </summary>
        public static void RebuildTraceRegimeAfterSnapshot(
            CaptureSimulationState state,
            CaptureSimulationParameters sourceParameters)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            CaptureSimulationParameters parameters = Normalize(sourceParameters);
            int preservedTicks = state.TraceRegimeTicksRemaining;
            if (preservedTicks <= 0)
                preservedTicks = Math.Max(1, parameters.TickRate / 2);
            preservedTicks = Clamp(preservedTicks, 1, Math.Max(1, parameters.TickRate * 4));

            state.TargetPositionUnits = Clamp(state.TargetPositionUnits, 0, parameters.GraphUnits);
            state.TargetVelocityUnitsPerSecond = 0;
            state.TraceRegime = CaptureTraceRegime.Consolidation;
            state.TraceRegimeTicksRemaining = preservedTicks;
            state.TraceConsolidationAnchorUnits = state.TargetPositionUnits;
            state.TraceImpulseTargetPositionUnits = state.TargetPositionUnits;
            state.IsTraceRegimeInitialized = true;
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

        private static bool StepTrace(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            EnsureTraceRegimeInitialized(state, parameters, ref rng);
            bool spike = rng.RollBasisPoints(parameters.SpikeChanceBasisPointsPerTick);

            if (state.TargetPositionUnits <= 0 || state.TargetPositionUnits >= parameters.GraphUnits)
            {
                RejectTraceEdge(state, parameters, ref rng);
                StepImpulse(state, parameters, ref rng);
                return spike;
            }

            if (state.TraceRegime == CaptureTraceRegime.Consolidation)
            {
                if (spike || state.TraceRegimeTicksRemaining <= 0)
                {
                    BeginImpulse(state, parameters, ref rng, spike, false);
                    StepImpulse(state, parameters, ref rng);
                }
                else
                {
                    StepConsolidation(state, parameters, ref rng);
                }

                return spike;
            }

            if (spike)
            {
                int motionBasisPoints = MotionBasisPoints(parameters);
                int retargetChance = Clamp(2500 + motionBasisPoints / 4, 3500, 7500);
                if (rng.RollBasisPoints(retargetChance))
                {
                    BeginImpulse(state, parameters, ref rng, true, true);
                }
                else
                {
                    SharpenImpulse(state, parameters);
                }
            }

            StepImpulse(state, parameters, ref rng);
            return spike;
        }

        private static void EnsureTraceRegimeInitialized(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            if (state.IsTraceRegimeInitialized)
                return;

            state.TargetPositionUnits = Clamp(state.TargetPositionUnits, 0, parameters.GraphUnits);
            state.TargetVelocityUnitsPerSecond = 0;
            state.TraceRegime = CaptureTraceRegime.Consolidation;
            state.TraceRegimeTicksRemaining = NextConsolidationDwellTicks(ref rng, parameters);
            state.TraceConsolidationAnchorUnits = state.TargetPositionUnits;
            state.TraceImpulseTargetPositionUnits = state.TargetPositionUnits;
            state.IsTraceRegimeInitialized = true;
        }

        private static void StepConsolidation(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            int tickRate = parameters.TickRate;
            int range = Math.Max(
                2,
                Math.Min(
                    Math.Max(2, parameters.GraphUnits / 25),
                    Math.Max(2, parameters.GraphUnits / 100) +
                    parameters.TraceAccelerationUnitsPerSecondSquared / Math.Max(1, tickRate * 4)));
            int step = Math.Max(
                1,
                Math.Min(
                    Math.Max(1, range / 4),
                    parameters.TraceAccelerationUnitsPerSecondSquared / Math.Max(1, tickRate * tickRate)));
            int minimum = Clamp(state.TraceConsolidationAnchorUnits - range, 0, parameters.GraphUnits);
            int maximum = Clamp(state.TraceConsolidationAnchorUnits + range, 0, parameters.GraphUnits);
            int previous = state.TargetPositionUnits;
            int offset = rng.NextInt(-step, step + 1);
            state.TargetPositionUnits = Clamp(SaturatingAdd(previous, offset), minimum, maximum);
            state.TargetVelocityUnitsPerSecond = ClampLongToInt(
                (long)(state.TargetPositionUnits - previous) * tickRate,
                -parameters.TraceMaximumSpeedUnitsPerSecond,
                parameters.TraceMaximumSpeedUnitsPerSecond);
            state.TraceRegimeTicksRemaining--;
        }

        private static void BeginImpulse(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng,
            bool spikeStarted,
            bool forceReverse)
        {
            int graphUnits = parameters.GraphUnits;
            int margin = Math.Max(1, graphUnits / 20);
            int current = Clamp(state.TargetPositionUnits, 0, graphUnits);
            int previousDirection = Math.Sign(state.TargetVelocityUnitsPerSecond);
            int direction = rng.NextInt(0, 2) == 0 ? -1 : 1;
            if (forceReverse && previousDirection != 0)
                direction = -previousDirection;
            if (current <= margin)
                direction = 1;
            else if (current >= graphUnits - margin)
                direction = -1;

            int category = rng.NextInt(0, CaptureBalanceData.BasisPointScale);
            int minimumDistanceBasisPoints;
            int maximumDistanceBasisPoints;
            if (category < 4000)
            {
                minimumDistanceBasisPoints = 400;
                maximumDistanceBasisPoints = 1200;
            }
            else if (category < 9000)
            {
                minimumDistanceBasisPoints = 1500;
                maximumDistanceBasisPoints = 3200;
            }
            else
            {
                minimumDistanceBasisPoints = 5500;
                maximumDistanceBasisPoints = 9000;
            }

            int distanceBasisPoints = rng.NextInt(
                minimumDistanceBasisPoints,
                maximumDistanceBasisPoints + 1);
            int desiredDistance = ClampLongToInt(
                (long)graphUnits * distanceBasisPoints / CaptureBalanceData.BasisPointScale *
                MotionBasisPoints(parameters) / CaptureBalanceData.BasisPointScale,
                1,
                Math.Max(1, graphUnits - margin * 2));
            int negativeRoom = Math.Max(0, current - margin);
            int positiveRoom = Math.Max(0, graphUnits - margin - current);
            int selectedRoom = direction < 0 ? negativeRoom : positiveRoom;
            int oppositeRoom = direction < 0 ? positiveRoom : negativeRoom;
            if (selectedRoom < desiredDistance && oppositeRoom > selectedRoom)
            {
                direction = -direction;
                selectedRoom = oppositeRoom;
            }

            int distance = Math.Max(1, Math.Min(desiredDistance, selectedRoom));
            int target = Clamp(current + direction * distance, margin, graphUnits - margin);
            if (target == current)
            {
                direction = current < graphUnits / 2 ? 1 : -1;
                target = Clamp(current + direction * Math.Max(1, graphUnits / 10), margin, graphUnits - margin);
            }

            int maximumSpeed = parameters.TraceMaximumSpeedUnitsPerSecond;
            int minimumSpeed = Math.Max(1, maximumSpeed * 6500 / CaptureBalanceData.BasisPointScale);
            int speed = rng.NextInt(minimumSpeed, maximumSpeed + 1);
            if (spikeStarted)
            {
                speed = Clamp(
                    SaturatingAdd(speed, parameters.SpikeVelocityImpulseUnitsPerSecond),
                    1,
                    maximumSpeed);
            }

            state.TraceRegime = CaptureTraceRegime.Impulse;
            state.TraceConsolidationAnchorUnits = current;
            state.TraceImpulseTargetPositionUnits = target;
            state.TargetVelocityUnitsPerSecond = direction * speed;
            state.TraceRegimeTicksRemaining = NextImpulseDurationTicks(
                ref rng,
                Math.Abs(target - current),
                speed,
                parameters.TickRate);
        }

        private static void SharpenImpulse(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters)
        {
            int direction = Math.Sign(state.TraceImpulseTargetPositionUnits - state.TargetPositionUnits);
            if (direction == 0)
                direction = Math.Sign(state.TargetVelocityUnitsPerSecond);
            if (direction == 0)
                direction = 1;
            int speed = Clamp(
                SaturatingAdd(
                    Magnitude(state.TargetVelocityUnitsPerSecond),
                    parameters.SpikeVelocityImpulseUnitsPerSecond),
                1,
                parameters.TraceMaximumSpeedUnitsPerSecond);
            state.TargetVelocityUnitsPerSecond = direction * speed;
        }

        private static void StepImpulse(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            int direction = Math.Sign(state.TraceImpulseTargetPositionUnits - state.TargetPositionUnits);
            if (direction == 0)
            {
                EnterConsolidation(state, parameters, ref rng);
                return;
            }

            int speed = Clamp(
                Magnitude(state.TargetVelocityUnitsPerSecond),
                1,
                parameters.TraceMaximumSpeedUnitsPerSecond);
            state.TargetVelocityUnitsPerSecond = direction * speed;
            int previous = state.TargetPositionUnits;
            int nextPosition = SaturatingAdd(previous, state.TargetVelocityUnitsPerSecond / parameters.TickRate);
            bool arrived = direction > 0
                ? nextPosition >= state.TraceImpulseTargetPositionUnits
                : nextPosition <= state.TraceImpulseTargetPositionUnits;
            state.TargetPositionUnits = arrived
                ? state.TraceImpulseTargetPositionUnits
                : nextPosition;
            state.TraceRegimeTicksRemaining--;

            if (state.TargetPositionUnits <= 0 || state.TargetPositionUnits >= parameters.GraphUnits)
            {
                state.TargetPositionUnits = Clamp(state.TargetPositionUnits, 0, parameters.GraphUnits);
                RejectTraceEdge(state, parameters, ref rng);
                return;
            }

            if (arrived || state.TraceRegimeTicksRemaining <= 0)
                EnterConsolidation(state, parameters, ref rng);
        }

        private static void RejectTraceEdge(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            int graphUnits = parameters.GraphUnits;
            int minimum = Math.Max(1, graphUnits / 5);
            int maximum = Math.Max(minimum + 1, graphUnits - minimum);
            int target = rng.NextInt(minimum, maximum);
            int direction = state.TargetPositionUnits <= 0 ? 1 : -1;
            if ((direction > 0 && target <= state.TargetPositionUnits) ||
                (direction < 0 && target >= state.TargetPositionUnits))
            {
                target = direction > 0 ? minimum : maximum - 1;
            }

            int speed = parameters.TraceMaximumSpeedUnitsPerSecond;
            state.TraceRegime = CaptureTraceRegime.Impulse;
            state.TraceConsolidationAnchorUnits = state.TargetPositionUnits;
            state.TraceImpulseTargetPositionUnits = target;
            state.TargetVelocityUnitsPerSecond = direction * speed;
            state.TraceRegimeTicksRemaining = NextImpulseDurationTicks(
                ref rng,
                Math.Abs(target - state.TargetPositionUnits),
                speed,
                parameters.TickRate);
        }

        private static void EnterConsolidation(
            CaptureSimulationState state,
            CaptureSimulationParameters parameters,
            ref DeterministicCaptureRng rng)
        {
            state.TargetPositionUnits = Clamp(state.TargetPositionUnits, 0, parameters.GraphUnits);
            state.TargetVelocityUnitsPerSecond = 0;
            state.TraceRegime = CaptureTraceRegime.Consolidation;
            state.TraceRegimeTicksRemaining = NextConsolidationDwellTicks(ref rng, parameters);
            state.TraceConsolidationAnchorUnits = state.TargetPositionUnits;
            state.TraceImpulseTargetPositionUnits = state.TargetPositionUnits;
        }

        private static int NextConsolidationDwellTicks(
            ref DeterministicCaptureRng rng,
            CaptureSimulationParameters parameters)
        {
            int motionBasisPoints = MotionBasisPoints(parameters);
            long motionSquared = (long)motionBasisPoints * motionBasisPoints;
            const long BasisPointScaleSquared =
                (long)CaptureBalanceData.BasisPointScale * CaptureBalanceData.BasisPointScale;
            int minimum = ClampLongToInt(
                (long)parameters.TickRate * 2 * BasisPointScaleSquared / motionSquared,
                Math.Max(2, parameters.TickRate / 3),
                Math.Max(2, parameters.TickRate * 12));
            int maximum = ClampLongToInt(
                (long)parameters.TickRate * 7 * BasisPointScaleSquared / motionSquared,
                minimum,
                Math.Max(minimum, parameters.TickRate * 12));
            return rng.NextInt(minimum, maximum + 1);
        }

        private static int NextImpulseDurationTicks(
            ref DeterministicCaptureRng rng,
            int distance,
            int speed,
            int tickRate)
        {
            int expected = ClampLongToInt(
                ((long)Math.Max(1, distance) * tickRate + Math.Max(1, speed) - 1) / Math.Max(1, speed),
                1,
                Math.Max(2, tickRate * 4));
            int duration;
            if (rng.RollBasisPoints(2000))
            {
                int completionBasisPoints = rng.NextInt(4500, 8501);
                duration = Math.Max(2, expected * completionBasisPoints / CaptureBalanceData.BasisPointScale);
            }
            else
            {
                duration = expected + rng.NextInt(0, Math.Max(2, tickRate / 5) + 1);
            }

            return Clamp(duration, 2, Math.Max(2, tickRate * 2));
        }

        private static int MotionBasisPoints(CaptureSimulationParameters parameters)
        {
            const int ReferenceAcceleration = 12000;
            return ClampLongToInt(
                (long)Math.Max(1, parameters.TraceAccelerationUnitsPerSecondSquared) *
                CaptureBalanceData.BasisPointScale / ReferenceAcceleration,
                5000,
                18000);
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

        private static int ClampLongToInt(long value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return (int)value;
        }

        private static int Magnitude(int value)
        {
            return value == int.MinValue ? int.MaxValue : Math.Abs(value);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
