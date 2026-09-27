using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class BattleSessionManager
{
    private readonly ConcurrentDictionary<Guid, BattleSessionRecord> _sessions = new();
    private readonly ConcurrentDictionary<Guid, CaptureAttemptRuntime> _attempts = new();
    private readonly ConcurrentDictionary<string, CombatSessionEnvelope> _commandResults = new(StringComparer.Ordinal);
    private readonly ServiceOptions _options;
    private readonly ManifestCatalog _catalog;
    private readonly SupabaseAuthorityGateway _gateway;
    private readonly SolanaTimeAuthority _time;
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(
        ServiceOptions options,
        ManifestCatalog catalog,
        SupabaseAuthorityGateway gateway,
        SolanaTimeAuthority time,
        ILogger<BattleSessionManager> logger)
    {
        _options = options;
        _catalog = catalog;
        _gateway = gateway;
        _time = time;
        _logger = logger;
    }

    public bool IsReady =>
        _options.AuthoritativePveCaptureEnabled &&
        _options.HasAuthoritySecrets &&
        _catalog.IsReady &&
        _time.TryGetUnixMilliseconds(out _);

    public async Task<CombatSessionEnvelope> StartAsync(AuthenticatedProfile owner, StartBattleApiRequest request, CancellationToken cancellationToken)
    {
        RequireReady();
        if (request == null || string.IsNullOrWhiteSpace(request.CommandId))
            throw new AuthorityException("command_id_required", "A stable command_id is required.", 400);
        if (!string.Equals(request.ClientManifestHash, _catalog.ManifestHash, StringComparison.OrdinalIgnoreCase))
            throw new AuthorityException("upgrade_required", "Client and authority gameplay manifests do not match.", 409);

        long now = _time.RequireUnixMilliseconds();
        uint seed = unchecked((uint)RandomNumberGenerator.GetInt32(1, int.MaxValue));
        ManifestEncounter encounter = _catalog.ResolveEncounter(request.ZoneId, request.NarrativeEncounterId, seed);
        AuthorityLoadoutSnapshot loadout = await _gateway.GetLoadoutAsync(owner.ProfileId, now, cancellationToken);
        Guid battleId = Guid.NewGuid();
        var session = new BattleSessionRecord
        {
            BattleId = battleId,
            ProfileId = owner.ProfileId,
            AuthUserId = owner.AuthUserId,
            StartCommandId = request.CommandId.Trim(),
            StateVersion = 0,
            Seed = seed,
            ManifestHash = _catalog.ManifestHash,
            ConfigHash = _catalog.ConfigHash,
            Status = "active",
            ExpiresAtUnixMilliseconds = now + (long)_options.BattleResumeWindow.TotalMilliseconds,
            LockedLoadout = new AuthorityLoadoutSnapshot
            {
                HeadItemId = loadout.HeadItemId,
                BodyItemId = loadout.BodyItemId,
                GearEffects = loadout.GearEffects,
                AuthorityUnixMilliseconds = now,
            },
            State = BuildInitialState(encounter, request.ZoneId),
        };
        JsonElement result = await _gateway.BeginBattleAsync(session, cancellationToken);
        if (!ReadOk(result))
            throw new AuthorityException(ReadString(result, "code", "battle_start_rejected"), "A PvE battle is already active for this profile.", 409);
        if (result.TryGetProperty("already_applied", out JsonElement alreadyApplied) &&
            alreadyApplied.ValueKind == JsonValueKind.True &&
            Guid.TryParse(ReadString(result, "battle_id", ""), out Guid existingBattleId))
        {
            BattleSessionRecord existing = await _gateway.GetBattleAsync(existingBattleId, owner.ProfileId, cancellationToken)
                ?? throw new AuthorityException("battle_not_found", "The idempotent battle session could not be resumed.", 404);
            existing.AuthUserId = owner.AuthUserId;
            _sessions[existingBattleId] = existing;
            EnsureCaptureRuntime(existing);
            return BuildEnvelope(existing);
        }
        _sessions[battleId] = session;
        return BuildEnvelope(session);
    }

    public async Task<CombatSessionEnvelope> ResumeAsync(AuthenticatedProfile owner, Guid battleId, CancellationToken cancellationToken)
    {
        RequireReady();
        BattleSessionRecord session;
        if (!_sessions.TryGetValue(battleId, out session!))
        {
            session = await _gateway.GetBattleAsync(battleId, owner.ProfileId, cancellationToken)
                ?? throw new AuthorityException("battle_not_found", "No resumable battle was found.", 404);
            session.AuthUserId = owner.AuthUserId;
            _sessions[battleId] = session;
        }
        EnsureOwner(session, owner);
        long now = _time.RequireUnixMilliseconds();
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await ThrowIfExpiredAsync(session, now, cancellationToken);
            EnsureCaptureRuntime(session);
            return BuildEnvelope(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<CommandServiceResult> SubmitAsync(
        AuthenticatedProfile owner,
        Guid battleId,
        TeamCommandApiRequest command,
        CancellationToken cancellationToken)
    {
        RequireReady();
        if (command == null)
            return Rejected("command_required", "A team command body is required.");
        BattleSessionRecord session = await GetOwnedSession(owner, battleId, cancellationToken);
        string commandKey = $"{battleId:D}:{command.CommandId}";

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await ThrowIfExpiredAsync(session, _time.RequireUnixMilliseconds(), cancellationToken);
            if (_commandResults.TryGetValue(commandKey, out CombatSessionEnvelope? existing))
                return new CommandServiceResult { Ok = true, Session = session, AlreadyApplied = true, Message = JsonSerializer.Serialize(existing, JsonDefaults.Options) };
            if (string.IsNullOrWhiteSpace(command.CommandId))
                return Rejected("command_id_required", "A stable command_id is required.");
            if (await _gateway.HasCommandAsync(battleId, owner.ProfileId, command.CommandId, cancellationToken))
            {
                _commandResults[commandKey] = BuildEnvelope(session);
                return new CommandServiceResult { Ok = true, Session = session, AlreadyApplied = true };
            }
            if (command.ExpectedVersion != session.StateVersion)
                return Rejected("state_version_conflict", $"Expected {command.ExpectedVersion}; authority is {session.StateVersion}.");
            if (!string.Equals(command.Kind, "capture", StringComparison.OrdinalIgnoreCase))
                return Rejected("combat_core_migration_required", "This rollout slice accepts capture commands only; authoritative move resolution remains feature-gated.");
            if (!string.Equals(session.State.Phase, "player_turn", StringComparison.Ordinal))
                return Rejected("battle_not_ready", "Capture can only consume an available player team turn.");

            AuthoritativeBattleTarget? target = session.State.Targets.FirstOrDefault(x =>
                string.Equals(x.TargetId, command.TargetId, StringComparison.Ordinal) && x.IsActive && !x.IsRemoved);
            if (target == null)
                return Rejected("invalid_capture_target", "Capture requires a living active target.");
            if (!CaptureChallengeResolver.IsCaptureAllowed(
                    session.State.BattleKind,
                    session.State.CapturePolicy,
                    EncounterCapturePolicy.UseEncounterDefault,
                    session.State.EncounterCaptureEnabled,
                    target.IsActive,
                    target.CaptureContext.CurrentHp > 0))
            {
                return Rejected("capture_disabled", "This encounter or target cannot be captured.");
            }
            if (!TryResolveOrb(command.OrbId, out CaptureOrbProfile orb))
                return Rejected("invalid_orb", "The selected item is not a supported Orb.");
            if (orb.EntanglesOnSuccess && target.CaptureContext.IsEntangled)
                return Rejected("already_entangled", "Entangle Orbs cannot target an already entangled Chainling.");

            long now = _time.RequireUnixMilliseconds();
            AuthorityLoadoutSnapshot attemptLoadout = await _gateway.GetLoadoutAsync(owner.ProfileId, now, cancellationToken);
            ResolvedCaptureProfile resolved = await _gateway.ResolveCaptureProfileAsync(target.CaptureContext, now, cancellationToken);
            CaptureEffectVector attemptBoosts = attemptLoadout.BoostEffects
                .Add(resolved.ProfileEffects)
                .Add(resolved.EventEffects);
            string attemptConfigHash = BuildAttemptConfigHash(session.ConfigHash, resolved);
            Guid attemptId = Guid.NewGuid();
            CaptureChallengeSnapshot challenge = CaptureChallengeResolver.Resolve(
                attemptId.ToString("D"),
                session.BattleId.ToString("D"),
                session.Seed ^ DeterministicCaptureRng.StableSeed(target.TargetId, (uint)session.State.TurnNumber),
                0,
                attemptConfigHash,
                target.CaptureContext,
                resolved.Profile,
                orb,
                session.LockedLoadout.GearEffects,
                attemptBoosts,
                _catalog.Balance);
            challenge.DeadlineUnixMilliseconds = now +
                (long)Math.Ceiling(challenge.Parameters.DurationTicks * 1000d / challenge.Parameters.TickRate);

            session.State.Phase = "capturing";
            session.State.ActiveAttemptId = challenge.AttemptId;
            session.State.ActiveChallenge = challenge;
            session.State.CaptureState = CaptureSimulation.CreateInitial(challenge);
            object targetSnapshot = BuildTargetLedgerSnapshot(target);
            JsonElement begin = await _gateway.BeginCaptureAttemptAsync(
                session,
                command,
                attemptId,
                challenge,
                targetSnapshot,
                _catalog.Balance.LowHpThresholdBasisPoints,
                cancellationToken);
            if (!ReadOk(begin))
            {
                session.State.Phase = "player_turn";
                session.State.ActiveAttemptId = "";
                session.State.ActiveChallenge = null;
                session.State.CaptureState = null;
                return Rejected(ReadString(begin, "code", "capture_rejected"), "The authority rejected the capture before consuming an Orb.");
            }

            session.StateVersion++;
            session.Status = "capturing";
            var runtime = CreateRuntime(session, challenge, session.State.CaptureState);
            _attempts[attemptId] = runtime;
            runtime.Start();
            CombatSessionEnvelope envelope = BuildEnvelope(session);
            _commandResults[commandKey] = envelope;
            return new CommandServiceResult { Ok = true, Session = session };
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<CaptureAttemptRuntime> GetAttemptAsync(AuthenticatedProfile owner, Guid battleId, CancellationToken cancellationToken)
    {
        BattleSessionRecord session = await GetOwnedSession(owner, battleId, cancellationToken);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            await ThrowIfExpiredAsync(session, _time.RequireUnixMilliseconds(), cancellationToken);
            if (!Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId))
                throw new AuthorityException("capture_attempt_not_found", "This battle has no active capture attempt.", 404);
            EnsureCaptureRuntime(session);
            if (!_attempts.TryGetValue(attemptId, out CaptureAttemptRuntime? attempt))
                throw new AuthorityException("capture_attempt_not_found", "The capture attempt is not resumable.", 404);
            return attempt;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public CombatSessionEnvelope BuildEnvelope(BattleSessionRecord session)
    {
        var client = new ClientBattleState
        {
            Phase = session.State.Phase,
            TurnNumber = session.State.TurnNumber,
            CaptureScannerRating = session.LockedLoadout.GearEffects.ScanRating,
            CaptureLowHpThresholdBasisPoints = session.LockedLoadout.GearEffects.ScanRating > 0
                ? _catalog.Balance.LowHpThresholdBasisPoints
                : 0,
            EndReason = session.State.EndReason,
            CaptureChallenge = session.State.ActiveChallenge,
        };
        foreach (AuthoritativeBattleTarget target in session.State.Targets)
        {
            client.Targets.Add(new ClientBattleTarget
            {
                TargetId = target.TargetId,
                SpeciesId = target.CaptureContext.SpeciesId,
                DisplayName = target.DisplayName,
                CurrentHp = target.CaptureContext.CurrentHp,
                MaxHp = target.CaptureContext.MaxHp,
                IsActive = target.IsActive,
                IsRemoved = target.IsRemoved,
                IsEntangled = target.CaptureContext.IsEntangled,
                CaptureEnabled = CaptureChallengeResolver.IsCaptureAllowed(
                    session.State.BattleKind,
                    session.State.CapturePolicy,
                    EncounterCapturePolicy.UseEncounterDefault,
                    session.State.EncounterCaptureEnabled,
                    target.IsActive,
                    target.CaptureContext.CurrentHp > 0),
            });
        }
        return new CombatSessionEnvelope
        {
            BattleId = session.BattleId.ToString("D"),
            Version = session.StateVersion,
            ManifestHash = session.ManifestHash,
            StateJson = JsonSerializer.Serialize(client, JsonDefaults.Options),
            IsResumable = session.Status is "active" or "capturing" or "service_interrupted",
            ExpiresAtUnixMilliseconds = session.ExpiresAtUnixMilliseconds,
        };
    }

    private async Task<BattleSessionRecord> GetOwnedSession(AuthenticatedProfile owner, Guid battleId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(battleId, out BattleSessionRecord? session))
        {
            session = await _gateway.GetBattleAsync(battleId, owner.ProfileId, cancellationToken)
                ?? throw new AuthorityException("battle_not_found", "No owned battle was found.", 404);
            session.AuthUserId = owner.AuthUserId;
            _sessions[battleId] = session;
        }
        EnsureOwner(session, owner);
        return session;
    }

    private CaptureAttemptRuntime CreateRuntime(BattleSessionRecord session, CaptureChallengeSnapshot challenge, CaptureSimulationState? resumeState)
    {
        return new CaptureAttemptRuntime(
            challenge,
            resumeState,
            _options,
            () => _time.RequireUnixMilliseconds(),
            (runtime, state) => FinalizeAttemptAsync(session, runtime, state),
            (runtime, state) => CheckpointAttemptAsync(session, runtime, state));
    }

    private void EnsureCaptureRuntime(BattleSessionRecord session)
    {
        if (!(string.Equals(session.Status, "capturing", StringComparison.Ordinal) ||
              string.Equals(session.Status, "service_interrupted", StringComparison.Ordinal)) ||
            session.State.ActiveChallenge == null ||
            !Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId) ||
            _attempts.ContainsKey(attemptId))
        {
            return;
        }
        var runtime = CreateRuntime(session, session.State.ActiveChallenge, session.State.CaptureState);
        if (_attempts.TryAdd(attemptId, runtime))
            runtime.Start();
        else
            runtime.Dispose();
    }

    private async Task CheckpointAttemptAsync(BattleSessionRecord session, CaptureAttemptRuntime runtime, CaptureSimulationState state)
    {
        await session.Gate.WaitAsync();
        try
        {
            if (!string.Equals(session.State.ActiveAttemptId, runtime.Challenge.AttemptId, StringComparison.Ordinal))
                return;
            session.State.CaptureState = state;
            await _gateway.CheckpointBattleAsync(session, session.StateVersion, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Capture checkpoint failed for {AttemptId}.", runtime.Challenge.AttemptId);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private async Task<CaptureOutcome> FinalizeAttemptAsync(BattleSessionRecord session, CaptureAttemptRuntime runtime, CaptureSimulationState finalState)
    {
        await session.Gate.WaitAsync();
        AuthoritativeBattleState stateBeforeFinalize = CloneBattleState(session.State);
        int versionBeforeFinalize = session.StateVersion;
        try
        {
            AuthoritativeBattleTarget? target = session.State.Targets.FirstOrDefault(x =>
                string.Equals(x.TargetId, runtime.Challenge.TargetId, StringComparison.Ordinal));
            if (target == null)
                throw new InvalidOperationException("Capture target disappeared from authoritative state.");

            CaptureOutcomeKind kind = finalState.Outcome;
            bool battleContinues;
            if (kind is CaptureOutcomeKind.Locked or CaptureOutcomeKind.Escaped)
            {
                target.IsRemoved = true;
                target.IsActive = false;
                AuthoritativeBattleTarget? replacement = session.State.Targets.FirstOrDefault(x => !x.IsRemoved && !x.IsActive && x.CaptureContext.CurrentHp > 0);
                if (replacement != null)
                {
                    replacement.IsActive = true;
                    session.State.Phase = "enemy_turn";
                    battleContinues = true;
                }
                else
                {
                    session.State.Phase = "complete";
                    session.State.EndReason = kind == CaptureOutcomeKind.Locked ? "Captured" : "TargetEscaped";
                    session.Status = "completed";
                    battleContinues = false;
                }
            }
            else
            {
                session.State.Phase = "enemy_turn";
                session.Status = "active";
                battleContinues = true;
            }

            session.State.ActiveAttemptId = "";
            session.State.ActiveChallenge = null;
            session.State.CaptureState = null;
            session.StateVersion++;
            int elapsedMilliseconds = (int)Math.Min(
                int.MaxValue,
                finalState.Tick * 1000L / Math.Max(1, runtime.Challenge.Parameters.TickRate));
            string entangledType = kind == CaptureOutcomeKind.Locked && runtime.Challenge.OrbId == "entangle_orb"
                ? _catalog.SelectEntangledType(runtime.Challenge.Seed)
                : "";
            JsonElement grant = await _gateway.FinalizeCaptureAttemptAsync(
                session,
                runtime.AttemptId,
                kind,
                elapsedMilliseconds,
                finalState.MeterBasisPoints,
                runtime.History,
                runtime.PerfectLock,
                entangledType,
                CancellationToken.None);
            _attempts.TryRemove(runtime.AttemptId, out _);
            return new CaptureOutcome
            {
                AttemptId = runtime.Challenge.AttemptId,
                Kind = kind,
                BattleContinues = battleContinues,
                FinalMeterBasisPoints = finalState.MeterBasisPoints,
                ElapsedTicks = finalState.Tick,
                GrantedChainlingId = ReadString(grant, "chainling_id", ""),
                GrantedEntangledTypeId = ReadString(grant, "entangled_type", ""),
                AddedToParty = grant.TryGetProperty("is_party_member", out JsonElement party) && party.ValueKind == JsonValueKind.True,
                UnlockedAchievementIds = ReadStrings(grant, "unlocked_achievement_ids"),
                Message = kind switch
                {
                    CaptureOutcomeKind.Locked => "SIGNAL LOCKED — CHAINLING CAPTURED",
                    CaptureOutcomeKind.Escaped => "SIGNAL LOST — TARGET FLED",
                    CaptureOutcomeKind.TimedOut => "ORB TIMED OUT — BATTLE CONTINUES",
                    _ => "CAPTURE INTERRUPTED — BATTLE REMAINS RESUMABLE",
                },
            };
        }
        catch (Exception exception)
        {
            session.State = stateBeforeFinalize;
            session.State.CaptureState = finalState.Clone();
            session.Status = "service_interrupted";
            session.State.Phase = "service_interrupted";
            session.StateVersion = versionBeforeFinalize;
            _attempts.TryRemove(runtime.AttemptId, out _);
            _logger.LogError(exception, "Capture finalization failed for {AttemptId}; no battle loss was assessed.", runtime.Challenge.AttemptId);
            return new CaptureOutcome
            {
                AttemptId = runtime.Challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "service_interrupted",
                Message = "Capture authority was interrupted; the battle remains resumable and no loss was assessed.",
                BattleContinues = true,
                FinalMeterBasisPoints = finalState.MeterBasisPoints,
                ElapsedTicks = finalState.Tick,
            };
        }
        finally
        {
            session.Gate.Release();
        }
    }

    private void RequireReady()
    {
        if (!_options.AuthoritativePveCaptureEnabled)
            throw new AuthorityException("feature_disabled", "Authoritative PvE capture is not enabled for this rollout.");
        if (!_catalog.IsReady)
            throw new AuthorityException("manifest_unavailable", "The authoritative gameplay or capture manifest is unavailable.");
        _time.RequireUnixMilliseconds();
    }

    private async Task ThrowIfExpiredAsync(
        BattleSessionRecord session,
        long authorityUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        if (session.ExpiresAtUnixMilliseconds > authorityUnixMilliseconds)
            return;

        JsonElement result = await _gateway.ExpireBattleAsync(
            session,
            authorityUnixMilliseconds,
            _options.AbandonmentRamPenaltyFlat,
            _options.DefeatKoRespawnHpBasisPoints,
            cancellationToken);
        if (!ReadOk(result))
        {
            throw new AuthorityException(
                ReadString(result, "code", "battle_expiry_failed"),
                "The expired battle could not be finalized safely. No additional battle may start until authority recovers.");
        }

        session.Status = "abandoned";
        session.State.Phase = "complete";
        session.State.EndReason = "Abandoned";
        if (result.TryGetProperty("state_version", out JsonElement version) && version.TryGetInt32(out int persistedVersion))
            session.StateVersion = persistedVersion;
        if (Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId) && _attempts.TryRemove(attemptId, out CaptureAttemptRuntime? attempt))
            attempt.Dispose();
        session.State.ActiveAttemptId = "";
        session.State.ActiveChallenge = null;
        session.State.CaptureState = null;
        _sessions.TryRemove(session.BattleId, out _);

        throw new AuthorityException(
            "battle_expired",
            "The battle resume window expired. The battle was recorded as a loss, the configured RAM recovery cost was applied, and KO'd party Chainlings respawned.",
            410);
    }

    private static void EnsureOwner(BattleSessionRecord session, AuthenticatedProfile owner)
    {
        if (session.ProfileId != owner.ProfileId)
            throw new AuthorityException("battle_not_owned", "The battle does not belong to this profile.", 403);
    }

    private static AuthoritativeBattleState BuildInitialState(ManifestEncounter encounter, string zoneId)
    {
        var state = new AuthoritativeBattleState
        {
            BattleKind = encounter.BattleKind,
            EncounterCaptureEnabled = encounter.CaptureEnabled,
            CapturePolicy = encounter.CapturePolicy,
            Phase = "player_turn",
            TurnNumber = 1,
        };
        for (int i = 0; i < encounter.Targets.Count; i++)
        {
            ManifestTarget source = encounter.Targets[i];
            int[] ivs = NormalizeIvs(source.Ivs);
            state.Targets.Add(new AuthoritativeBattleTarget
            {
                TargetId = string.IsNullOrWhiteSpace(source.TargetId) ? $"enemy-{i}" : source.TargetId,
                DisplayName = source.DisplayName,
                KnownMoveId = source.KnownMoveId,
                RawIvs = ivs,
                Stars = source.Stars,
                Level = source.Level,
                Mutated = source.Mutated,
                IsActive = source.StartsActive,
                CaptureContext = new CaptureTargetContext
                {
                    TargetId = string.IsNullOrWhiteSpace(source.TargetId) ? $"enemy-{i}" : source.TargetId,
                    SpeciesId = source.SpeciesId,
                    VisualVariantId = source.VisualVariantId,
                    MutationId = source.MutationId,
                    IsEntangled = source.Entangled,
                    EntangledTypeId = source.EntangledTypeId,
                    AreaId = "",
                    ZoneId = zoneId,
                    DifficultyId = source.DifficultyId,
                    CurrentHp = Math.Clamp(source.CurrentHp, 0, Math.Max(1, source.MaxHp)),
                    MaxHp = Math.Max(1, source.MaxHp),
                    LegacyBaseCaptureRate = source.LegacyBaseCaptureRate,
                    HasSpeciesCaptureProfile = true,
                    SpeciesCaptureProfile = ClampCaptureProfile(source.CaptureProfile),
                    IsMutated = source.Mutated,
                    Ivs = new CaptureIvValues
                    {
                        MeleeAttack = ivs[1], MeleeDefense = ivs[2], RangedAttack = ivs[3],
                        RangedDefense = ivs[4], Speed = ivs[5],
                    },
                },
            });
        }
        if (!state.Targets.Any(x => x.IsActive) && state.Targets.Count > 0)
            state.Targets[0].IsActive = true;
        return state;
    }

    private static object BuildTargetLedgerSnapshot(AuthoritativeBattleTarget target)
    {
        CaptureTargetContext context = target.CaptureContext;
        int hpBasisPoints = context.MaxHp <= 0 ? 0 : (int)((long)context.CurrentHp * 10000 / context.MaxHp);
        return new
        {
            target_id = target.TargetId,
            species_id = context.SpeciesId,
            current_hp = context.CurrentHp,
            max_hp = context.MaxHp,
            hp_basis_points = hpBasisPoints,
            legacy_base_capture_rate = context.LegacyBaseCaptureRate,
            alertness = context.Alertness,
            stars = target.Stars,
            level = target.Level,
            ivs = target.RawIvs,
            entangled = context.IsEntangled,
            entangled_type = context.EntangledTypeId,
            mutated = target.Mutated,
            mutation_id = context.MutationId,
            visual_variant_id = context.VisualVariantId,
            ordinal_move_id = target.KnownMoveId,
        };
    }

    private static bool TryResolveOrb(string orbId, out CaptureOrbProfile profile)
    {
        profile = new CaptureOrbProfile { OrbId = orbId ?? "" };
        switch ((orbId ?? "").Trim().ToLowerInvariant())
        {
            case "signal_orb":
                profile.Effects = CaptureEffectVector.None;
                return true;
            case "lattice_orb":
                profile.Effects = new CaptureEffectVector(0, 0, 2500, -1500, 0);
                return true;
            case "quick_orb":
                profile.Effects = new CaptureEffectVector(0, 0, -2000, 3000, 0);
                return true;
            case "entangle_orb":
                profile.Effects = new CaptureEffectVector(0, 0, -2000, 0, -2500);
                profile.EntanglesOnSuccess = true;
                return true;
            default:
                return false;
        }
    }

    private static CaptureVolatilityProfile ClampCaptureProfile(int value)
    {
        if (value < (int)CaptureVolatilityProfile.Calm)
            return CaptureVolatilityProfile.Standard;
        if (value > (int)CaptureVolatilityProfile.Extreme)
            return CaptureVolatilityProfile.Standard;
        return (CaptureVolatilityProfile)value;
    }

    private static int[] NormalizeIvs(int[]? source)
    {
        var result = new int[6];
        if (source != null)
        {
            for (int i = 0; i < Math.Min(result.Length, source.Length); i++)
                result[i] = Math.Clamp(source[i], -10, 10);
        }
        return result;
    }

    private static CommandServiceResult Rejected(string code, string message)
    {
        return new CommandServiceResult { Ok = false, Code = code, Message = message };
    }

    private static bool ReadOk(JsonElement element)
    {
        return element.TryGetProperty("ok", out JsonElement ok) && ok.ValueKind == JsonValueKind.True;
    }

    private static string ReadString(JsonElement element, string property, string fallback)
    {
        return element.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : fallback;
    }

    private static List<string> ReadStrings(JsonElement element, string property)
    {
        var result = new List<string>();
        if (!element.TryGetProperty(property, out JsonElement values) || values.ValueKind != JsonValueKind.Array)
            return result;
        foreach (JsonElement value in values.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                result.Add(value.GetString()!);
        }
        return result;
    }

    private static string BuildAttemptConfigHash(string baseHash, ResolvedCaptureProfile resolved)
    {
        var stamp = new System.Text.StringBuilder(192);
        stamp.Append(baseHash ?? "")
            .Append("|profile=").Append(resolved.Profile.ProfileId).Append('@').Append(resolved.ProfileVersion)
            .Append("|assignment=").Append(resolved.AssignmentId).Append('@').Append(resolved.AssignmentVersion);
        for (int i = 0; i < resolved.EventStamps.Count; i++)
            stamp.Append("|event=").Append(resolved.EventStamps[i]);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(stamp.ToString());
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static AuthoritativeBattleState CloneBattleState(AuthoritativeBattleState source)
    {
        string json = JsonSerializer.Serialize(source, JsonDefaults.Options);
        return JsonSerializer.Deserialize<AuthoritativeBattleState>(json, JsonDefaults.Options)
            ?? throw new InvalidOperationException("Authoritative battle state could not be cloned for transaction recovery.");
    }
}
