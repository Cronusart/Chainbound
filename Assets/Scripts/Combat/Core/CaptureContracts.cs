#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Chainbound.Combat.Core
{
    public enum CaptureOutcomeKind
    {
        None = 0,
        Locked = 1,
        Escaped = 2,
        TimedOut = 3,
        Rejected = 4,
        Interrupted = 5,
    }

    public enum CaptureVolatilityProfile
    {
        Calm = 0,
        Standard = 1,
        Volatile = 2,
        Extreme = 3,
    }

    public enum CaptureIvStat
    {
        MeleeAttack = 0,
        MeleeDefense = 1,
        RangedAttack = 2,
        RangedDefense = 3,
        Speed = 4,
    }

    public enum EncounterCapturePolicy
    {
        UseEncounterDefault = 0,
        Enabled = 1,
        Disabled = 2,
    }

    public enum AuthoritativeBattleKind
    {
        Wild = 0,
        Boss = 1,
        Trainer = 2,
        Scripted = 3,
        PvP = 4,
    }

    [Serializable]
    public struct CaptureEffectVector : IEquatable<CaptureEffectVector>
    {
        public int ScanRating { get; private set; }
        public int StabilityBasisPoints { get; private set; }
        public int BandWidthBasisPoints { get; private set; }
        public int LockRateBasisPoints { get; private set; }
        public int CaptureTimeBasisPoints { get; private set; }

        public CaptureEffectVector(
            int scanRating,
            int stabilityBasisPoints,
            int bandWidthBasisPoints,
            int lockRateBasisPoints,
            int captureTimeBasisPoints)
        {
            ScanRating = Math.Max(0, scanRating);
            StabilityBasisPoints = stabilityBasisPoints;
            BandWidthBasisPoints = bandWidthBasisPoints;
            LockRateBasisPoints = lockRateBasisPoints;
            CaptureTimeBasisPoints = captureTimeBasisPoints;
        }

        public static CaptureEffectVector None => new CaptureEffectVector(0, 0, 0, 0, 0);

        public CaptureEffectVector Add(CaptureEffectVector other)
        {
            return new CaptureEffectVector(
                Math.Max(ScanRating, other.ScanRating),
                SaturatingAdd(StabilityBasisPoints, other.StabilityBasisPoints),
                SaturatingAdd(BandWidthBasisPoints, other.BandWidthBasisPoints),
                SaturatingAdd(LockRateBasisPoints, other.LockRateBasisPoints),
                SaturatingAdd(CaptureTimeBasisPoints, other.CaptureTimeBasisPoints));
        }

        public bool Equals(CaptureEffectVector other)
        {
            return ScanRating == other.ScanRating &&
                   StabilityBasisPoints == other.StabilityBasisPoints &&
                   BandWidthBasisPoints == other.BandWidthBasisPoints &&
                   LockRateBasisPoints == other.LockRateBasisPoints &&
                   CaptureTimeBasisPoints == other.CaptureTimeBasisPoints;
        }

        public override bool Equals(object? obj)
        {
            return obj is CaptureEffectVector other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + ScanRating;
                hash = (hash * 31) + StabilityBasisPoints;
                hash = (hash * 31) + BandWidthBasisPoints;
                hash = (hash * 31) + LockRateBasisPoints;
                hash = (hash * 31) + CaptureTimeBasisPoints;
                return hash;
            }
        }

        private static int SaturatingAdd(int left, int right)
        {
            long value = (long)left + right;
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }
    }

    [Serializable]
    public sealed class CaptureIvValues
    {
        public int MeleeAttack { get; set; }
        public int MeleeDefense { get; set; }
        public int RangedAttack { get; set; }
        public int RangedDefense { get; set; }
        public int Speed { get; set; }

        public int Get(CaptureIvStat stat)
        {
            switch (stat)
            {
                case CaptureIvStat.MeleeAttack: return MeleeAttack;
                case CaptureIvStat.MeleeDefense: return MeleeDefense;
                case CaptureIvStat.RangedAttack: return RangedAttack;
                case CaptureIvStat.RangedDefense: return RangedDefense;
                case CaptureIvStat.Speed: return Speed;
                default: return 0;
            }
        }
    }

    [Serializable]
    public sealed class CaptureTargetContext
    {
        public string TargetId { get; set; } = "";
        public string SpeciesId { get; set; } = "";
        public string VisualVariantId { get; set; } = "";
        public string MutationId { get; set; } = "";
        public bool IsMutated { get; set; }
        public bool IsEntangled { get; set; }
        public string EntangledTypeId { get; set; } = "";
        public string AreaId { get; set; } = "";
        public string ZoneId { get; set; } = "";
        public string DifficultyId { get; set; } = "";
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; } = 1;
        public int LegacyBaseCaptureRate { get; set; } = 45;
        public bool HasSpeciesCaptureProfile { get; set; }
        public CaptureVolatilityProfile SpeciesCaptureProfile { get; set; } = CaptureVolatilityProfile.Standard;
        public int VariantRarityRank { get; set; }
        public int MutationRarityRank { get; set; }
        public int Alertness { get; set; }
        public CaptureIvValues Ivs { get; set; } = new CaptureIvValues();
    }

    [Serializable]
    public sealed class CaptureOrbProfile
    {
        public string OrbId { get; set; } = "signal_orb";
        public CaptureEffectVector Effects { get; set; } = CaptureEffectVector.None;
        public bool EntanglesOnSuccess { get; set; }
    }

    [Serializable]
    public sealed class CaptureMotionProfile
    {
        public string ProfileId { get; set; } = "standard";
        public CaptureVolatilityProfile Difficulty { get; set; } = CaptureVolatilityProfile.Standard;
        public int MotionMultiplierBasisPoints { get; set; } = 10000;
        public int TimerMultiplierBasisPoints { get; set; } = 10000;
        public int SpikeChanceMultiplierBasisPoints { get; set; } = 10000;
    }

    [Serializable]
    public sealed class CaptureScannerIntel
    {
        public CaptureIvStat Stat { get; set; }
        public int Grade { get; set; }
    }

    [Serializable]
    public sealed class CaptureSimulationParameters
    {
        public int TickRate { get; set; }
        public int DurationTicks { get; set; }
        public int GraphUnits { get; set; }
        public int BandHeightUnits { get; set; }
        public int LockRateBasisPointsPerSecond { get; set; }
        public int EscapeRateBasisPointsPerSecond { get; set; }
        public int BandLiftAccelerationUnitsPerSecondSquared { get; set; }
        public int BandGravityUnitsPerSecondSquared { get; set; }
        public int BandDragBasisPointsPerSecond { get; set; }
        public int BandMaximumSpeedUnitsPerSecond { get; set; }
        public int BandEdgeBounceBasisPoints { get; set; }
        public int TraceAccelerationUnitsPerSecondSquared { get; set; }
        public int TraceMaximumSpeedUnitsPerSecond { get; set; }
        public int TraceMeanReversionUnitsPerSecondSquared { get; set; }
        public int TraceEdgeBounceBasisPoints { get; set; }
        public int SpikeChanceBasisPointsPerTick { get; set; }
        public int SpikeVelocityImpulseUnitsPerSecond { get; set; }
    }

    [Serializable]
    public sealed class CaptureChallengeSnapshot
    {
        public string AttemptId { get; set; } = "";
        public uint Seed { get; set; }
        public long DeadlineUnixMilliseconds { get; set; }
        public string BattleId { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string SpeciesId { get; set; } = "";
        public string ProfileId { get; set; } = "standard";
        public string OrbId { get; set; } = "signal_orb";
        public string ConfigHash { get; set; } = "";
        public CaptureVolatilityProfile Difficulty { get; set; }
        public CaptureEffectVector EffectiveEffects { get; set; }
        public CaptureSimulationParameters Parameters { get; set; } = new CaptureSimulationParameters();
        public bool LowHpMarkerRevealed { get; set; }
        public bool IsLowHp { get; set; }
        public List<CaptureScannerIntel> ScannerIntel { get; set; } = new List<CaptureScannerIntel>();
    }

    [Serializable]
    public sealed class CaptureOutcome
    {
        public CaptureOutcomeKind Kind { get; set; }
        public string AttemptId { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public string Message { get; set; } = "";
        public string GrantedChainlingId { get; set; } = "";
        public string GrantedEntangledTypeId { get; set; } = "";
        public bool AddedToParty { get; set; }
        public bool BattleContinues { get; set; }
        public int FinalMeterBasisPoints { get; set; }
        public int ElapsedTicks { get; set; }
        public float DurationSeconds { get; set; }
        public List<string> UnlockedAchievementIds { get; set; } = new List<string>();

        public bool IsFinal => Kind != CaptureOutcomeKind.None;
        public bool RemovesTargetFromBattle =>
            Kind == CaptureOutcomeKind.Locked ||
            Kind == CaptureOutcomeKind.Escaped;
    }

    [Serializable]
    public sealed class CaptureAuthoritativeState
    {
        public string AttemptId { get; set; } = "";
        public int Version { get; set; }
        public int Tick { get; set; }
        public int TargetPositionUnits { get; set; }
        public int BandPositionUnits { get; set; }
        public int MeterBasisPoints { get; set; }
        public int RemainingTicks { get; set; }
        public bool IsHeld { get; set; }
        public bool IsInsideBand { get; set; }
        public bool SpikeOccurred { get; set; }
        public CaptureOutcomeKind Outcome { get; set; }
    }

    [Serializable]
    public sealed class StartCombatSessionRequest
    {
        public string CommandId { get; set; } = "";
        public string ZoneId { get; set; } = "";
        public string NarrativeEncounterId { get; set; } = "";
        public string ClientManifestHash { get; set; } = "";
    }

    [Serializable]
    public sealed class CombatTeamCommand
    {
        public string CommandId { get; set; } = "";
        public int ExpectedVersion { get; set; }
        public string Kind { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string OrbId { get; set; } = "";
        public Dictionary<string, string> Actions { get; set; } = new Dictionary<string, string>();
    }

    [Serializable]
    public sealed class CombatSessionEnvelope
    {
        public string BattleId { get; set; } = "";
        public int Version { get; set; }
        public string ManifestHash { get; set; } = "";
        public string StateJson { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsResumable { get; set; }
        public long ExpiresAtUnixMilliseconds { get; set; }
    }

    public interface ICaptureSession : IDisposable
    {
        CaptureChallengeSnapshot Challenge { get; }
        CaptureAuthoritativeState LatestState { get; }
        Task<CaptureOutcome> Completion { get; }
        event Action<CaptureAuthoritativeState> SnapshotReceived;
        Task SetHeldAsync(bool held, long inputSequence, CancellationToken cancellationToken = default(CancellationToken));
        void ClearHeldInput();
    }

    public interface ICombatSessionService
    {
        Task<CombatSessionEnvelope> StartEncounterAsync(
            StartCombatSessionRequest request,
            CancellationToken cancellationToken = default(CancellationToken));

        Task<CombatSessionEnvelope> ResumeEncounterAsync(
            string battleId,
            CancellationToken cancellationToken = default(CancellationToken));

        Task<CombatSessionEnvelope> SubmitTeamCommandAsync(
            string battleId,
            CombatTeamCommand command,
            CancellationToken cancellationToken = default(CancellationToken));

        Task<ICaptureSession> OpenCaptureSessionAsync(
            string battleId,
            string attemptId,
            CancellationToken cancellationToken = default(CancellationToken));
    }

    [Serializable]
    public sealed class CaptureLoadoutSnapshot
    {
        public string HeadItemId { get; set; } = "";
        public string BodyItemId { get; set; } = "";
        public List<ActiveCaptureBoost> ActiveBoosts { get; set; } = new List<ActiveCaptureBoost>();
        public CaptureEffectVector ResolvedEffects { get; set; }
        public long AuthorityUnixMilliseconds { get; set; }
        public bool TimeAuthorityFresh { get; set; }
    }

    [Serializable]
    public sealed class ActiveCaptureBoost
    {
        public string ItemId { get; set; } = "";
        public CaptureEffectVector Effects { get; set; }
        public long ExpiresAtUnixMilliseconds { get; set; }
    }

    public interface ICaptureLoadoutService
    {
        Task<CaptureLoadoutSnapshot> FetchAsync(CancellationToken cancellationToken = default(CancellationToken));
        Task<CaptureLoadoutSnapshot> EquipAsync(string slot, string itemId, CancellationToken cancellationToken = default(CancellationToken));
        Task<CaptureLoadoutSnapshot> ActivateBoostAsync(string itemId, CancellationToken cancellationToken = default(CancellationToken));
    }
}
