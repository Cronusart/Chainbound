using Chainbound.Combat.Core;
using System.Text.Json;

VerifyGameplayManifest();

CaptureChallengeSnapshot challenge = Resolve(
    seed: 0x12345678u,
    CaptureVolatilityProfile.Standard,
    hp: 25,
    iv: 0,
    alertness: 0,
    CaptureEffectVector.None);
CaptureSimulationState left = CaptureSimulation.CreateInitial(challenge);
CaptureSimulationState right = CaptureSimulation.CreateInitial(challenge);
for (int tick = 0; tick < 180 && left.Outcome == CaptureOutcomeKind.None; tick++)
{
    bool held = (tick / 17) % 2 == 0;
    left = CaptureSimulation.Step(left, challenge.Parameters, held).State;
    right = CaptureSimulation.Step(right, challenge.Parameters, held).State;
    Require(StateKey(left) == StateKey(right), "deterministic streams diverged");
}

Console.WriteLine("GOLDEN=" + StateKey(left));
Require(
    StateKey(left) == "180,3427911885,2732,240,0,62,2791,2791,1,9986,-121,1362,-10,0,0",
    "golden fixed-point state changed; update only with an intentional manifest/version migration");
Require(challenge.Parameters.TickRate == 30, "tick rate changed");
Require(challenge.Parameters.DurationTicks == 517, "standard above-low-HP duration changed");
Require(challenge.Parameters.BandHeightUnits == 2400, "standard band changed");
Require(challenge.Parameters.LockRateBasisPointsPerSecond == 1250, "standard lock rate changed");
VerifyRestAndDartMechanics();

CaptureChallengeSnapshot unityGoldenChallenge = ResolveUnityGoldenChallenge();
CaptureSimulationState unityGolden = CaptureSimulation.CreateInitial(unityGoldenChallenge);
for (int tick = 0; tick < 180 && unityGolden.Outcome == CaptureOutcomeKind.None; tick++)
{
    bool held = (tick / 17) % 2 == 0;
    unityGolden = CaptureSimulation.Step(unityGolden, unityGoldenChallenge.Parameters, held).State;
}
Console.WriteLine("UNITY_GOLDEN=" + StateKey(unityGolden));
Require(
    StateKey(unityGolden) == "180,1961476631,7604,-240,0,101,7584,7584,1,9986,-121,1637,-20,0,0",
    "Unity CaptureCoreTests golden changed");

CaptureBalanceData normalized = new CaptureBalanceData
{
    TickRate = -1,
    StandardTimerMilliseconds = int.MaxValue,
    MaximumAttemptMilliseconds = -1,
    BaseBandHeightBasisPoints = int.MaxValue,
    LockMovementBasisPointsPerSecond = -1,
    EscapeMovementBasisPointsPerSecond = -1,
}.Normalized();
Require(normalized.TickRate == 1, "tick normalization failed");
Require(normalized.MaximumAttemptMilliseconds == 1, "attempt bound normalization failed");
Require(normalized.StandardTimerMilliseconds == 1, "deadline normalization failed");
Require(normalized.BaseBandHeightBasisPoints == 10000, "band normalization failed");
Require(normalized.LockMovementBasisPointsPerSecond == 0, "negative lock rate survived normalization");
Require(normalized.EscapeMovementBasisPointsPerSecond == 0, "negative escape rate survived normalization");

PrintScenario("Standard low-HP competence", CaptureVolatilityProfile.Standard, 15, 0, CaptureEffectVector.None, 6000);
PrintScenario("Extreme low-HP baseline", CaptureVolatilityProfile.Extreme, 15, 0, CaptureEffectVector.None, 6000);
PrintScenario("Extreme stacked rare", CaptureVolatilityProfile.Extreme, 100, 10, CaptureEffectVector.None, 6000);
PrintScenario(
    "Extreme equipped hunter",
    CaptureVolatilityProfile.Extreme,
    15,
    0,
    new CaptureEffectVector(5, 0, 1500, 0, 0),
    6000);

Console.WriteLine("Capture Core verification passed.");

static void PrintScenario(
    string label,
    CaptureVolatilityProfile profile,
    int hp,
    int iv,
    CaptureEffectVector effects,
    int competenceBasisPoints)
{
    CaptureChallengeSnapshot scenario = Resolve(
        0xA1700000u ^ DeterministicCaptureRng.StableSeed(label),
        profile,
        hp,
        iv,
        alertness: 0,
        effects);
    OutcomeCounts counts = SimulateBot(scenario, 1000, competenceBasisPoints);
    Console.WriteLine($"{label}: locked={counts.Locked / 10f:0.0}% escaped={counts.Escaped / 10f:0.0}% timed_out={counts.TimedOut / 10f:0.0}%");
}

static CaptureChallengeSnapshot Resolve(
    uint seed,
    CaptureVolatilityProfile difficulty,
    int hp,
    int iv,
    int alertness,
    CaptureEffectVector effects)
{
    CaptureBalanceData rules = CaptureBalanceData.CreateDefault().Normalized();
    return CaptureChallengeResolver.Resolve(
        "verification-attempt",
        "verification-battle",
        seed,
        0,
        "verification-config",
        new CaptureTargetContext
        {
            TargetId = "verification-target",
            SpeciesId = "verification-species",
            CurrentHp = hp,
            MaxHp = 100,
            LegacyBaseCaptureRate = difficulty switch
            {
                CaptureVolatilityProfile.Calm => 70,
                CaptureVolatilityProfile.Standard => 45,
                CaptureVolatilityProfile.Volatile => 25,
                _ => 10,
            },
            Alertness = alertness,
            Ivs = new CaptureIvValues
            {
                MeleeAttack = iv,
                MeleeDefense = iv,
                RangedAttack = iv,
                RangedDefense = iv,
                Speed = iv,
            },
        },
        rules.ResolveProfile(difficulty),
        new CaptureOrbProfile { OrbId = "signal_orb", Effects = CaptureEffectVector.None },
        effects,
        CaptureEffectVector.None,
        rules);
}

static CaptureChallengeSnapshot ResolveUnityGoldenChallenge()
{
    return CaptureChallengeResolver.Resolve(
        "attempt",
        "battle",
        0x12345678u,
        123456789L,
        "hash",
        new CaptureTargetContext
        {
            TargetId = "target-1",
            SpeciesId = "species-1",
            CurrentHp = 15,
            MaxHp = 100,
            LegacyBaseCaptureRate = 45,
            Ivs = new CaptureIvValues
            {
                MeleeAttack = -10,
                MeleeDefense = -3,
                RangedAttack = 0,
                RangedDefense = 6,
                Speed = 10,
            },
        },
        null,
        new CaptureOrbProfile { OrbId = "signal_orb", Effects = CaptureEffectVector.None },
        CaptureEffectVector.None,
        CaptureEffectVector.None,
        CaptureBalanceData.CreateDefault());
}

static OutcomeCounts SimulateBot(CaptureChallengeSnapshot template, int attempts, int competenceBasisPoints)
{
    var counts = new OutcomeCounts();
    for (int attempt = 0; attempt < attempts; attempt++)
    {
        var challenge = new CaptureChallengeSnapshot
        {
            AttemptId = "bot-" + attempt,
            BattleId = template.BattleId,
            Seed = template.Seed ^ DeterministicCaptureRng.StableSeed("bot", unchecked((uint)attempt)),
            ConfigHash = template.ConfigHash,
            TargetId = template.TargetId,
            SpeciesId = template.SpeciesId,
            ProfileId = template.ProfileId,
            OrbId = template.OrbId,
            Difficulty = template.Difficulty,
            EffectiveEffects = template.EffectiveEffects,
            Parameters = template.Parameters,
            IsLowHp = template.IsLowHp,
        };
        CaptureSimulationState state = CaptureSimulation.CreateInitial(challenge);
        var rng = new DeterministicCaptureRng(challenge.Seed ^ 0xB07C0DEu);
        int reactionTicks = LerpInt(12, 2, competenceBasisPoints);
        int lookAheadTicks = LerpInt(1, 5, competenceBasisPoints);
        int deadZone = LerpInt(900, 140, competenceBasisPoints);
        int noise = LerpInt(900, 30, competenceBasisPoints);
        int observed = state.TargetPositionUnits;
        bool held = false;
        while (state.Outcome == CaptureOutcomeKind.None)
        {
            if (state.Tick % Math.Max(1, reactionTicks) == 0)
            {
                observed = state.TargetPositionUnits + rng.NextInt(-noise, noise + 1);
                int projected = state.BandPositionUnits +
                    state.BandVelocityUnitsPerSecond * lookAheadTicks / Math.Max(1, challenge.Parameters.TickRate);
                if (projected < observed - deadZone) held = true;
                else if (projected > observed + deadZone) held = false;
            }
            state = CaptureSimulation.Step(state, challenge.Parameters, held).State;
        }

        if (state.Outcome == CaptureOutcomeKind.Locked) counts.Locked++;
        else if (state.Outcome == CaptureOutcomeKind.Escaped) counts.Escaped++;
        else counts.TimedOut++;
    }
    return counts;
}

static int LerpInt(int from, int to, int tBasisPoints)
{
    return from + (to - from) * Math.Clamp(tBasisPoints, 0, 10000) / 10000;
}

static void VerifyRestAndDartMechanics()
{
    CaptureSimulationParameters parameters = TraceOnlyParameters();
    var resting = new CaptureSimulationState
    {
        RngState = 123u,
        TargetPositionUnits = 1000,
        TraceRegime = CaptureTraceRegime.Consolidation,
        TraceRegimeTicksRemaining = 60,
        TraceConsolidationAnchorUnits = 1000,
        TraceImpulseTargetPositionUnits = 1000,
        IsTraceRegimeInitialized = true,
        BandPositionUnits = 5000,
        MeterBasisPoints = 5000,
    };
    for (int tick = 0; tick < 45; tick++)
    {
        resting = CaptureSimulation.Step(resting, parameters, false).State;
        Require(resting.TraceRegime == CaptureTraceRegime.Consolidation, "rest ended before its dwell elapsed");
        Require(resting.TargetPositionUnits >= 800 && resting.TargetPositionUnits <= 1200, "rest escaped its consolidation range");
        Require(Math.Abs(resting.TargetVelocityUnitsPerSecond) <= 390, "rest produced impulse-scale velocity");
    }
    Require(resting.TargetPositionUnits < 1500, "rest pulled back toward chart center");

    var dart = new CaptureSimulationState
    {
        RngState = 456u,
        TargetPositionUnits = 1000,
        TargetVelocityUnitsPerSecond = 6500,
        TraceRegime = CaptureTraceRegime.Impulse,
        TraceRegimeTicksRemaining = 60,
        TraceConsolidationAnchorUnits = 1000,
        TraceImpulseTargetPositionUnits = 8000,
        IsTraceRegimeInitialized = true,
        BandPositionUnits = 5000,
        MeterBasisPoints = 5000,
    };
    int previous = dart.TargetPositionUnits;
    int guard = 0;
    while (dart.TraceRegime == CaptureTraceRegime.Impulse && guard++ < 100)
    {
        dart = CaptureSimulation.Step(dart, parameters, false).State;
        Require(dart.TargetPositionUnits >= previous, "dart reversed into a smooth arc");
        Require(dart.TargetPositionUnits <= 8000, "dart overshot its target");
        previous = dart.TargetPositionUnits;
    }
    Require(dart.TraceRegime == CaptureTraceRegime.Consolidation, "dart did not return to rest");
    Require(dart.TargetPositionUnits == 8000 && dart.TargetVelocityUnitsPerSecond == 0, "dart did not settle at its target");

    var edge = new CaptureSimulationState
    {
        RngState = 789u,
        TargetPositionUnits = 0,
        TraceRegime = CaptureTraceRegime.Consolidation,
        TraceRegimeTicksRemaining = 30,
        TraceConsolidationAnchorUnits = 0,
        IsTraceRegimeInitialized = true,
        BandPositionUnits = 5000,
        MeterBasisPoints = 5000,
    };
    edge = CaptureSimulation.Step(edge, parameters, false).State;
    Require(edge.TraceRegime == CaptureTraceRegime.Impulse, "edge contact did not start an inward dart");
    Require(edge.TargetVelocityUnitsPerSecond > 0, "lower edge did not reject upward");
    Require(edge.TraceImpulseTargetPositionUnits >= 2000 && edge.TraceImpulseTargetPositionUnits < 8000, "edge target was not interior");

    uint rngState = edge.RngState;
    edge.TargetPositionUnits = 4200;
    edge.TargetVelocityUnitsPerSecond = 6500;
    edge.TraceRegime = CaptureTraceRegime.Impulse;
    edge.TraceRegimeTicksRemaining = 17;
    edge.TraceImpulseTargetPositionUnits = 9000;
    CaptureSimulation.RebuildTraceRegimeAfterSnapshot(edge, parameters);
    Require(edge.RngState == rngState, "snapshot rebuild consumed RNG");
    Require(edge.TraceRegime == CaptureTraceRegime.Consolidation, "snapshot rebuild kept a stale dart active");
    Require(edge.TargetVelocityUnitsPerSecond == 0, "snapshot rebuild kept stale velocity");
    Require(edge.TraceConsolidationAnchorUnits == 4200, "snapshot rebuild did not use the corrected price");
}

static CaptureSimulationParameters TraceOnlyParameters()
{
    return new CaptureSimulationParameters
    {
        TickRate = 30,
        DurationTicks = 1000,
        GraphUnits = 10000,
        BandHeightUnits = 1,
        BandMaximumSpeedUnitsPerSecond = 1,
        TraceAccelerationUnitsPerSecondSquared = 12000,
        TraceMaximumSpeedUnitsPerSecond = 6500,
        TraceMeanReversionUnitsPerSecondSquared = 8000,
        TraceEdgeBounceBasisPoints = 6500,
        SpikeVelocityImpulseUnitsPerSecond = 2400,
    };
}

static string StateKey(CaptureSimulationState state)
{
    return string.Join(",",
        state.Tick,
        state.RngState,
        state.TargetPositionUnits,
        state.TargetVelocityUnitsPerSecond,
        (int)state.TraceRegime,
        state.TraceRegimeTicksRemaining,
        state.TraceConsolidationAnchorUnits,
        state.TraceImpulseTargetPositionUnits,
        state.IsTraceRegimeInitialized ? 1 : 0,
        state.BandPositionUnits,
        state.BandVelocityUnitsPerSecond,
        state.MeterBasisPoints,
        state.MeterRemainder,
        state.IsInsideBand ? 1 : 0,
        (int)state.Outcome);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void VerifyGameplayManifest()
{
    string current = Directory.GetCurrentDirectory();
    string manifestPath = Path.Combine(current, "backend", "Chainbound.BattleService", "manifests", "gameplay-manifest.json");
    if (!File.Exists(manifestPath))
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(directory.FullName, "backend", "Chainbound.BattleService", "manifests", "gameplay-manifest.json");
            directory = directory.Parent;
        }
    }

    Require(File.Exists(manifestPath), "gameplay manifest was not found");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
    JsonElement root = document.RootElement;
    Require(root.GetProperty("manifestVersion").GetInt32() == 3, "gameplay manifest schema version is not 3");

    JsonElement Move(string id) => root.GetProperty("moves").EnumerateArray()
        .Single(x => x.GetProperty("moveId").GetString() == id);
    JsonElement interest = Move("interest_strike");
    Require(interest.GetProperty("accuracy").GetInt32() == 90, "Interest Strike accuracy drifted");
    Require(
        interest.GetProperty("resolveProfile").GetProperty("conditions").GetString().Contains("StageHigherThanOtherStage", StringComparison.Ordinal),
        "Interest Strike strict stage condition is missing");

    JsonElement precision = Move("precision_strike").GetProperty("accuracyTuning");
    Require(precision.GetProperty("ignoreEvasionStages").GetInt32() == 6, "Precision Strike SureHit evasion bypass is missing");
    Require(precision.GetProperty("minHitChance").GetDouble() >= 0.949, "Precision Strike SureHit floor is missing");

    JsonElement grindTax = Move("grind_tax");
    Require(grindTax.GetProperty("bossOnly").GetBoolean(), "Grind Tax is not boss-only");
    Require(grindTax.GetProperty("moveEffectsOncePerTarget").GetBoolean(), "Grind Tax repeats its stage rider per hit");
    Require(Move("cipher_shot").GetProperty("moveEffectsOncePerTarget").GetBoolean(), "Cipher Shot repeats Cipher Lock per hit");

    Dictionary<int, string> statuses = root.GetProperty("statuses").EnumerateArray()
        .ToDictionary(x => x.GetProperty("numericId").GetInt32(), x => x.GetProperty("statusId").GetString());
    Require(statuses.GetValueOrDefault(310) == "SearField", "status 310 must be SearField");
    Require(statuses.GetValueOrDefault(311) == "Kindled", "status 311 must be Kindled");
    Require(statuses.GetValueOrDefault(312) == "CipherLocked", "status 312 must be CipherLocked");
    Console.WriteLine("Gameplay manifest parity checks passed.");
}

sealed class OutcomeCounts
{
    public int Locked;
    public int Escaped;
    public int TimedOut;
}
