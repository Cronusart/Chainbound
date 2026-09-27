using Chainbound.Combat.Core;

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
    StateKey(left) == "180,2124024666,6705,-593,9986,-121,4847,-20,0,0",
    "golden fixed-point state changed; update only with an intentional manifest/version migration");
Require(challenge.Parameters.TickRate == 30, "tick rate changed");
Require(challenge.Parameters.DurationTicks == 600, "standard duration changed");
Require(challenge.Parameters.BandHeightUnits == 2400, "standard band changed");
Require(challenge.Parameters.LockRateBasisPointsPerSecond == 1250, "standard lock rate changed");

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

PrintScenario("Standard competence", CaptureVolatilityProfile.Standard, 25, 0, CaptureEffectVector.None, 6000);
PrintScenario("Extreme baseline", CaptureVolatilityProfile.Extreme, 25, 0, CaptureEffectVector.None, 6000);
PrintScenario("Extreme stacked rare", CaptureVolatilityProfile.Extreme, 100, 10, CaptureEffectVector.None, 6000);
PrintScenario(
    "Extreme equipped hunter",
    CaptureVolatilityProfile.Extreme,
    25,
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

static string StateKey(CaptureSimulationState state)
{
    return string.Join(",",
        state.Tick,
        state.RngState,
        state.TargetPositionUnits,
        state.TargetVelocityUnitsPerSecond,
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

sealed class OutcomeCounts
{
    public int Locked;
    public int Escaped;
    public int TimedOut;
}
