using System.Text.Json.Serialization;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class AuthenticatedProfile
{
    public string AuthUserId { get; init; } = "";
    public Guid ProfileId { get; init; }
}

public sealed class StartBattleApiRequest
{
    public string CommandId { get; set; } = "";
    public string ZoneId { get; set; } = "";
    public string NarrativeEncounterId { get; set; } = "";
    public string ClientManifestHash { get; set; } = "";
}

public sealed class TeamCommandApiRequest
{
    public string CommandId { get; set; } = "";
    public int ExpectedVersion { get; set; }
    public string Kind { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string OrbId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ActionId { get; set; } = "";
    public int TurnNumber { get; set; }
    public int StepIndex { get; set; }
    public bool BattleActive { get; set; } = true;
    public int WinnerTeamIndex { get; set; } = -1;
    public string EndReason { get; set; } = "";
    public List<CombatVitalSnapshot> Vitals { get; set; } = new();
    public Dictionary<string, string> Actions { get; set; } = new();
}

public sealed class EquipCaptureGearApiRequest
{
    public string Slot { get; set; } = "";
    public string ItemId { get; set; } = "";
}

public sealed class CaptureStreamInput
{
    public long Sequence { get; set; }
    public bool Held { get; set; }
    public bool Arm { get; set; }
}

public sealed class ApiError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public int? StateVersion { get; init; }
}

public sealed class GameplayManifest
{
    public int ManifestVersion { get; set; } = 1;
    public List<ManifestSpecies> Species { get; set; } = new();
    public List<ManifestMove> Moves { get; set; } = new();
    public List<ManifestStatus> Statuses { get; set; } = new();
    public List<ManifestEncounter> Encounters { get; set; } = new();
}

public sealed class ManifestSpecies
{
    public string SpeciesId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int PrimaryType { get; set; }
    public int SecondaryType { get; set; }
    public int BaseMaxHp { get; set; }
    public int BaseMeleeAttack { get; set; }
    public int BaseMeleeDefense { get; set; }
    public int BaseRangedAttack { get; set; }
    public int BaseRangedDefense { get; set; }
    public int BaseSpeed { get; set; }
    public List<string> InitialMoveIds { get; set; } = new();
}

public sealed class ManifestMove
{
    public string MoveId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int Category { get; set; }
    public int BaseType { get; set; }
    public int Rarity { get; set; }
    public int Targeting { get; set; }
    public int ContactType { get; set; }
    public int Power { get; set; }
    public int ApCost { get; set; }
    public int Accuracy { get; set; }
    public int HitCountMin { get; set; }
    public int HitCountMax { get; set; }
    public int RechargeTurns { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<ManifestMoveStatus> StatusEffects { get; set; } = new();
    public string StageChanges { get; set; } = "";
    public string UtilityEffects { get; set; } = "";
    public float HpCostPercent { get; set; }
    public int HealPercent { get; set; }
    public ManifestResolveProfile ResolveProfile { get; set; } = new();
    public ManifestAccuracyTuning AccuracyTuning { get; set; } = new();
    public bool BossOnly { get; set; }
    public bool SpendsRemainingAp { get; set; }
    public bool MoveEffectsOncePerTarget { get; set; }
}

public sealed class ManifestMoveStatus
{
    public string StatusId { get; set; } = "";
    public int Duration { get; set; }
    public int ChanceBasisPoints { get; set; }
}

public sealed class ManifestResolveProfile
{
    public string PrimaryTiming { get; set; } = "";
    public string UtilityTiming { get; set; } = "";
    public string ConditionalPolicy { get; set; } = "";
    public string Conditions { get; set; } = "";
    public bool RequireAll { get; set; }
    public float DamagePowerMultiplier { get; set; }
    public float DamagePreMitigationMultiplier { get; set; }
    public float DamageFinalMultiplier { get; set; }
    public bool DamageOverrideAttackStat { get; set; }
    public string DamageAttackStat { get; set; } = "";
    public float ConditionalDamageMultiplier { get; set; }
    public string ConditionalDamageConditions { get; set; } = "";
    public bool ConditionalDamageRequireAll { get; set; }
    public string ConditionalDamageScalingCondition { get; set; } = "";
    public float ConditionalDamageBonusPerCount { get; set; }
    public float ConditionalDamageMaxMultiplier { get; set; }
    public string RandomDamageMultipliers { get; set; } = "";
}

public sealed class ManifestAccuracyTuning
{
    public bool ForceHit { get; set; }
    public bool ForceMiss { get; set; }
    public float Multiplier { get; set; }
    public float BonusPercent { get; set; }
    public int IgnoreEvasionStages { get; set; }
    public float MinHitChance { get; set; }
    public float MaxHitChance { get; set; }
}

public sealed class ManifestStatus
{
    public string StatusId { get; set; } = "";
    public int NumericId { get; set; }
    public string DisplayName { get; set; } = "";
    public string EffectSummary { get; set; } = "";
    public int StatusClass { get; set; }
    public int Origin { get; set; }
    public int DefaultDuration { get; set; }
    public int CounterThreshold { get; set; }
    public bool IsBuff { get; set; }
    public bool IsDebuff { get; set; }
    public bool PreventsAction { get; set; }
    public int EndOfTurnDamageBasisPoints { get; set; }
    public int EndOfTurnApChange { get; set; }
}

public sealed class ManifestEncounter
{
    public string EncounterId { get; set; } = "";
    public string ZoneId { get; set; } = "";
    public string TimeSegment { get; set; } = "";
    public AuthoritativeBattleKind BattleKind { get; set; } = AuthoritativeBattleKind.Wild;
    public string RewardTier { get; set; } = BattleRewardTier.Standard;
    public string RewardEligibilityQuestId { get; set; } = "";
    public bool CaptureEnabled { get; set; } = true;
    public EncounterCapturePolicy CapturePolicy { get; set; } = EncounterCapturePolicy.UseEncounterDefault;
    public int Weight { get; set; } = 1;
    public List<ManifestTarget> Targets { get; set; } = new();
}

public sealed class BattleRewardsManifest
{
    public int SchemaVersion { get; set; } = 1;
    public int ConfigVersion { get; set; } = 1;
    public BattleRewardValues Rewards { get; set; } = new();
}

public sealed class BattleRewardValues
{
    public int Standard { get; set; }
    public int Elite { get; set; }
    public int Boss { get; set; }
}

public sealed class ManifestTarget
{
    public string TargetId { get; set; } = "";
    public string SpeciesId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VisualVariantId { get; set; } = "base";
    public string MutationId { get; set; } = "none";
    public bool Mutated { get; set; }
    public bool Entangled { get; set; }
    public string EntangledTypeId { get; set; } = "none";
    public string DifficultyId { get; set; } = "standard";
    public int CurrentHp { get; set; } = 1;
    public int MaxHp { get; set; } = 1;
    public int LegacyBaseCaptureRate { get; set; } = 45;
    public int CaptureProfile { get; set; } = (int)CaptureVolatilityProfile.Standard;
    public int Stars { get; set; }
    public int StarVariance { get; set; }
    public int Level { get; set; } = 1;
    public bool RollIvs { get; set; }
    public int IvMin { get; set; } = -5;
    public int IvMax { get; set; } = 5;
    public int[] Ivs { get; set; } = new int[6];
    public string KnownMoveId { get; set; } = "baseline_jab";
    public bool StartsActive { get; set; } = true;
}

public sealed class CaptureBalanceManifest
{
    public int ConfigVersion { get; set; } = 1;
    public string ConfigHash { get; set; } = "";
    public CaptureBalanceData Balance { get; set; } = CaptureBalanceData.CreateDefault();
    public List<WeightedEntangledType> EntangledTypes { get; set; } = new();
}

public sealed class WeightedEntangledType
{
    public string TypeId { get; set; } = "";
    public int Weight { get; set; } = 1;
}

public sealed class AuthorityLoadoutSnapshot
{
    public string HeadItemId { get; set; } = "";
    public string BodyItemId { get; set; } = "";
    public CaptureEffectVector GearEffects { get; set; } = CaptureEffectVector.None;
    public CaptureEffectVector BoostEffects { get; set; } = CaptureEffectVector.None;
    public List<ActiveCaptureBoost> ActiveBoosts { get; set; } = new();
    public long AuthorityUnixMilliseconds { get; set; }
}

public sealed class ResolvedCaptureProfile
{
    public CaptureMotionProfile Profile { get; set; } = new();
    public int ProfileVersion { get; set; }
    public CaptureEffectVector ProfileEffects { get; set; } = CaptureEffectVector.None;
    public CaptureEffectVector EventEffects { get; set; } = CaptureEffectVector.None;
    public string AssignmentId { get; set; } = "";
    public int AssignmentVersion { get; set; }
    public List<string> EventStamps { get; set; } = new();
}

public sealed class AuthoritativeBattleTarget
{
    public string TargetId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public CaptureTargetContext CaptureContext { get; set; } = new();
    public string KnownMoveId { get; set; } = "baseline_jab";
    public int[] RawIvs { get; set; } = new int[6];
    public int Stars { get; set; }
    public int Level { get; set; } = 1;
    public bool Mutated { get; set; }
    public bool IsActive { get; set; }
    public bool IsRemoved { get; set; }
}

public sealed class AuthoritativeBattleState
{
    public string Phase { get; set; } = "player_turn";
    public int TurnNumber { get; set; } = 1;
    public bool HasActionCheckpoint { get; set; }
    public bool EncounterCaptureEnabled { get; set; }
    public EncounterCapturePolicy CapturePolicy { get; set; }
    public AuthoritativeBattleKind BattleKind { get; set; }
    public List<AuthoritativeBattleTarget> Targets { get; set; } = new();
    public string ActiveAttemptId { get; set; } = "";
    public CaptureChallengeSnapshot? ActiveChallenge { get; set; }
    public CaptureSimulationState? CaptureState { get; set; }
    public string EndReason { get; set; } = "";
    public List<CombatVitalSnapshot> Vitals { get; set; } = new();
}

public sealed class BattleSessionRecord
{
    public Guid BattleId { get; set; }
    public Guid ProfileId { get; set; }
    public string StartCommandId { get; set; } = "";
    public string AuthUserId { get; set; } = "";
    public int StateVersion { get; set; }
    public uint Seed { get; set; }
    public string ManifestHash { get; set; } = "";
    public string ConfigHash { get; set; } = "";
    public string EncounterId { get; set; } = "";
    public string RewardTier { get; set; } = BattleRewardTier.Standard;
    public int RewardConfigVersion { get; set; }
    public string RewardConfigHash { get; set; } = "";
    public string TerminalOutcome { get; set; } = "";
    public string TerminalReason { get; set; } = "";
    public long? TerminalAtUnixMilliseconds { get; set; }
    public string Status { get; set; } = "active";
    public long ExpiresAtUnixMilliseconds { get; set; }
    public AuthorityLoadoutSnapshot LockedLoadout { get; set; } = new();
    public AuthoritativeBattleState State { get; set; } = new();

    [JsonIgnore]
    public BattleRewardSettlement? Settlement { get; set; }

    [JsonIgnore]
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

public sealed class ClientBattleState
{
    public uint Seed { get; set; }
    public string Phase { get; set; } = "";
    public int TurnNumber { get; set; }
    public bool HasActionCheckpoint { get; set; }
    public AuthoritativeBattleKind BattleKind { get; set; }
    public EncounterCapturePolicy CapturePolicy { get; set; }
    public bool EncounterCaptureEnabled { get; set; }
    public string ZoneId { get; set; } = "";
    public string EncounterId { get; set; } = "";
    public string RewardTier { get; set; } = BattleRewardTier.Standard;
    public int RewardConfigVersion { get; set; }
    public string RewardConfigHash { get; set; } = "";
    public string TerminalOutcome { get; set; } = "";
    public int CaptureScannerRating { get; set; }
    public int CaptureLowHpThresholdBasisPoints { get; set; }
    public List<ClientBattleTarget> Targets { get; set; } = new();
    public CaptureChallengeSnapshot? CaptureChallenge { get; set; }
    public string EndReason { get; set; } = "";
    public List<CombatVitalSnapshot> Vitals { get; set; } = new();
}

public sealed class ClientBattleTarget
{
    public string TargetId { get; set; } = "";
    public string SpeciesId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int CurrentHp { get; set; }
    public int MaxHp { get; set; }
    public bool IsActive { get; set; }
    public bool IsRemoved { get; set; }
    public bool CaptureEnabled { get; set; }
    public bool IsEntangled { get; set; }
    public string VisualVariantId { get; set; } = "base";
    public string MutationId { get; set; } = "none";
    public string EntangledTypeId { get; set; } = "none";
    public string DifficultyId { get; set; } = "standard";
    public string KnownMoveId { get; set; } = "baseline_jab";
    public int[] RawIvs { get; set; } = new int[6];
    public int Stars { get; set; }
    public int Level { get; set; } = 1;
    public bool Mutated { get; set; }
}

public sealed class CommandServiceResult
{
    public bool Ok { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public BattleSessionRecord? Session { get; set; }
    public bool AlreadyApplied { get; set; }
}

public sealed class AuthorityException : Exception
{
    public AuthorityException(string code, string message, int statusCode = 503) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }
    public int StatusCode { get; }
}
