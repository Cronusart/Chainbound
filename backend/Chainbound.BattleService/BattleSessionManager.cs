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
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _rewardGates = new();
    private readonly ConcurrentDictionary<Guid, long> _rewardRetryAfter = new();
    private readonly ServiceOptions _options;
    private readonly ManifestCatalog _catalog;
    private readonly SupabaseAuthorityGateway _gateway;
    private readonly SolanaTimeAuthority _time;
    private readonly BattleRewardClient _rewards;
    private readonly BattlePlausibilityValidator _plausibility;
    private readonly ILogger<BattleSessionManager> _logger;

    public BattleSessionManager(
        ServiceOptions options,
        ManifestCatalog catalog,
        SupabaseAuthorityGateway gateway,
        SolanaTimeAuthority time,
        BattleRewardClient rewards,
        BattlePlausibilityValidator plausibility,
        ILogger<BattleSessionManager> logger)
    {
        _options = options;
        _catalog = catalog;
        _gateway = gateway;
        _time = time;
        _rewards = rewards;
        _plausibility = plausibility;
        _logger = logger;
    }

    public bool IsReady =>
        _options.AuthoritativePveCaptureEnabled &&
        _options.HasAuthoritySecrets &&
        (string.Equals(_options.BattleRewardMode, "off", StringComparison.Ordinal) || _rewards.IsReady) &&
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
        BattleSessionRecord? current = await _gateway.GetCurrentBattleAsync(owner.ProfileId, cancellationToken);
        if (current != null)
        {
            current.AuthUserId = owner.AuthUserId;
            _sessions[current.BattleId] = current;
            await current.Gate.WaitAsync(cancellationToken);
            try
            {
                if (await TryDismissScriptedBattleAsync(current, now, cancellationToken))
                {
                    // The cinematic row is closed. Start the requested battle below.
                }
                else if (current.ExpiresAtUnixMilliseconds <= now)
                {
                    return await SettleBattleAsync(current, now, "Abandoned", true, cancellationToken);
                }
                else
                {
                    EnsureCaptureRuntime(current);
                    CombatSessionEnvelope existing = BuildEnvelope(current);
                    existing.RequiresResumeChoice = !string.Equals(
                        current.StartCommandId,
                        request.CommandId?.Trim(),
                        StringComparison.Ordinal);
                    existing.Message = existing.RequiresResumeChoice
                        ? "A battle is still waiting."
                        : existing.Message;
                    return existing;
                }
            }
            finally
            {
                current.Gate.Release();
            }
        }

        uint seed = unchecked((uint)RandomNumberGenerator.GetInt32(1, int.MaxValue));
        ManifestEncounter encounter = _catalog.ResolveEncounter(
            request.ZoneId,
            request.NarrativeEncounterId,
            seed,
            now);
        bool rewardsDisabled = string.Equals(_options.BattleRewardMode, "off", StringComparison.Ordinal);
        bool rewardEligible = !rewardsDisabled &&
            await _gateway.IsNarrativeRewardEligibleAsync(owner.ProfileId, encounter, cancellationToken);
        AuthorityLoadoutSnapshot loadout = await _gateway.GetLoadoutAsync(owner.ProfileId, now, cancellationToken);
        List<CombatVitalSnapshot> partyVitals = await _gateway.GetPartyVitalsAsync(owner.ProfileId, cancellationToken);
        Guid battleId = Guid.NewGuid();
        string rewardTier = rewardEligible ? encounter.RewardTier : BattleRewardTier.None;
        bool rewardsDisabledForBattle = string.Equals(rewardTier, BattleRewardTier.None, StringComparison.Ordinal);
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
            EncounterId = encounter.EncounterId,
            RewardTier = rewardTier,
            RewardConfigVersion = rewardsDisabledForBattle ? 0 : _catalog.RewardConfigVersion,
            RewardConfigHash = rewardsDisabledForBattle ? new string('0', 64) : _catalog.RewardConfigHash,
            Status = "active",
            ExpiresAtUnixMilliseconds = now + (long)_options.BattleResumeWindow.TotalMilliseconds,
            LockedLoadout = new AuthorityLoadoutSnapshot
            {
                HeadItemId = loadout.HeadItemId,
                BodyItemId = loadout.BodyItemId,
                GearEffects = loadout.GearEffects,
                AuthorityUnixMilliseconds = now,
            },
            State = BuildInitialState(encounter, request.ZoneId, partyVitals, seed),
        };
        JsonElement result = await _gateway.BeginBattleAsync(session, cancellationToken);
        if (!ReadOk(result))
        {
            if (string.Equals(ReadString(result, "code", ""), "battle_already_active", StringComparison.Ordinal) &&
                Guid.TryParse(ReadString(result, "battle_id", ""), out Guid racedBattleId))
            {
                BattleSessionRecord raced = await _gateway.GetBattleAsync(racedBattleId, owner.ProfileId, cancellationToken)
                    ?? throw new AuthorityException("battle_not_found", "The active battle could not be resumed.", 404);
                raced.AuthUserId = owner.AuthUserId;
                _sessions[racedBattleId] = raced;
                EnsureCaptureRuntime(raced);
                CombatSessionEnvelope racedEnvelope = BuildEnvelope(raced);
                racedEnvelope.RequiresResumeChoice = true;
                racedEnvelope.Message = "A battle is still waiting.";
                return racedEnvelope;
            }
            throw new AuthorityException(ReadString(result, "code", "battle_start_rejected"), "A PvE battle is already active for this profile.", 409);
        }
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
        CaptureAttemptRuntime? finalizingAttempt = null;
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            if (IsTerminal(session))
            {
                await EnsureRewardSettlementAsync(session, cancellationToken);
                return BuildEnvelope(session);
            }
            if (await TryDismissScriptedBattleAsync(session, now, cancellationToken))
                throw new AuthorityException("battle_not_found", "The scripted battle was closed without a party loss.", 404);
            if (session.ExpiresAtUnixMilliseconds <= now)
                return await SettleBattleAsync(session, now, "Abandoned", true, cancellationToken);

            // A capture runtime can continue in an already-warm service instance. It owns its
            // own finalization gate, so do not wait for it while holding the session gate.
            // A cold instance has no runtime and must deterministically settle an elapsed
            // challenge before this endpoint returns a resumable envelope.
            if (TryGetElapsedCaptureRuntime(session, now, out CaptureAttemptRuntime? runningAttempt))
            {
                finalizingAttempt = runningAttempt;
            }
            else
            {
                await ReconcileCaptureForResumeAsync(session, now);
                return BuildEnvelope(session);
            }
        }
        finally
        {
            session.Gate.Release();
        }

        // The runtime was already active before this resume request. Waiting outside the
        // session gate prevents a deadlock with its finalizer and guarantees the client does
        // not receive a stale `capturing` state after the deadline has passed.
        await finalizingAttempt!.Completion.WaitAsync(cancellationToken);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            return BuildEnvelope(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<CombatSessionEnvelope?> GetCurrentAsync(
        AuthenticatedProfile owner,
        CancellationToken cancellationToken)
    {
        RequireReady();
        BattleSessionRecord? session = await _gateway.GetCurrentBattleAsync(owner.ProfileId, cancellationToken);
        if (session == null)
            return null;
        session.AuthUserId = owner.AuthUserId;
        _sessions[session.BattleId] = session;
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            long now = _time.RequireUnixMilliseconds();
            if (await TryDismissScriptedBattleAsync(session, now, cancellationToken))
                return null;
            if (session.ExpiresAtUnixMilliseconds <= now)
                return await SettleBattleAsync(session, now, "Abandoned", true, cancellationToken);
            EnsureCaptureRuntime(session);
            CombatSessionEnvelope envelope = BuildEnvelope(session);
            envelope.RequiresResumeChoice = true;
            envelope.Message = "A battle is still waiting.";
            return envelope;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<BattleRewardSettlement> GetRewardAsync(
        AuthenticatedProfile owner,
        Guid battleId,
        CancellationToken cancellationToken)
    {
        BattleSessionRecord session = await GetOwnedSession(owner, battleId, cancellationToken);
        if (session.Status is not ("completed" or "lost" or "abandoned"))
            throw new AuthorityException("battle_not_terminal", "This battle has not ended.", 409);
        if (RewardsDisabledFor(session))
        {
            session.Settlement ??= BattleRewardSettlement.Unavailable(
                battleId.ToString("D"), "rewards_disabled");
            return session.Settlement;
        }
        BattleRewardSettlement? stored = await _gateway.GetBattleRewardAsync(battleId, cancellationToken);
        if (stored != null)
        {
            session.Settlement = stored;
            return stored;
        }
        if (string.IsNullOrWhiteSpace(session.TerminalOutcome))
            return BattleRewardSettlement.Unavailable(battleId.ToString("D"), "terminal_outcome_missing");
        await EnsureRewardSettlementAsync(session, cancellationToken);
        return session.Settlement ?? BattleRewardSettlement.Unavailable(battleId.ToString("D"));
    }

    public async Task<CombatSessionEnvelope> ForfeitAsync(
        AuthenticatedProfile owner,
        Guid battleId,
        CancellationToken cancellationToken)
    {
        RequireReady();
        BattleSessionRecord session = await GetOwnedSession(owner, battleId, cancellationToken);
        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            return await SettleBattleAsync(
                session,
                _time.RequireUnixMilliseconds(),
                "Forfeited",
                false,
                cancellationToken);
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
            if (string.IsNullOrWhiteSpace(command.CommandId))
                return Rejected("command_id_required", "A stable command_id is required.");
            if (IsTerminal(session))
            {
                bool alreadyApplied = _commandResults.ContainsKey(commandKey) ||
                                      await _gateway.HasCommandAsync(battleId, owner.ProfileId, command.CommandId, cancellationToken);
                if (!alreadyApplied)
                    return Rejected("battle_not_ready", "This battle has already ended.");
                await EnsureRewardSettlementAsync(session, cancellationToken);
                _commandResults[commandKey] = BuildEnvelope(session);
                return new CommandServiceResult { Ok = true, Session = session, AlreadyApplied = true };
            }
            await ThrowIfExpiredAsync(session, _time.RequireUnixMilliseconds(), cancellationToken);
            if (_commandResults.TryGetValue(commandKey, out CombatSessionEnvelope? existing))
                return new CommandServiceResult { Ok = true, Session = session, AlreadyApplied = true, Message = JsonSerializer.Serialize(existing, JsonDefaults.Options) };
            if (await _gateway.HasCommandAsync(battleId, owner.ProfileId, command.CommandId, cancellationToken))
            {
                _commandResults[commandKey] = BuildEnvelope(session);
                return new CommandServiceResult { Ok = true, Session = session, AlreadyApplied = true };
            }
            if (command.ExpectedVersion != session.StateVersion)
                return Rejected("state_version_conflict", $"Expected {command.ExpectedVersion}; authority is {session.StateVersion}.");
            if (string.Equals(command.Kind, "action_step", StringComparison.OrdinalIgnoreCase))
                return await CommitActionStepAsync(session, command, commandKey, cancellationToken);
            if (!string.Equals(command.Kind, "capture", StringComparison.OrdinalIgnoreCase))
                return Rejected("unsupported_command", "The battle authority does not support this command kind.");
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
            challenge.DeadlineUnixMilliseconds = 0;

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
            Seed = session.Seed,
            Phase = session.State.Phase,
            TurnNumber = session.State.TurnNumber,
            HasActionCheckpoint = session.State.HasActionCheckpoint,
            BattleKind = session.State.BattleKind,
            CapturePolicy = session.State.CapturePolicy,
            EncounterCaptureEnabled = session.State.EncounterCaptureEnabled,
            ZoneId = session.State.Targets.Count > 0 ? session.State.Targets[0].CaptureContext.ZoneId : "",
            EncounterId = session.EncounterId,
            RewardTier = session.RewardTier,
            RewardConfigVersion = session.RewardConfigVersion,
            RewardConfigHash = session.RewardConfigHash,
            TerminalOutcome = session.TerminalOutcome,
            CaptureScannerRating = session.LockedLoadout.GearEffects.ScanRating,
            CaptureLowHpThresholdBasisPoints = session.LockedLoadout.GearEffects.ScanRating > 0
                ? _catalog.Balance.LowHpThresholdBasisPoints
                : 0,
            EndReason = session.State.EndReason,
            CaptureChallenge = session.State.ActiveChallenge,
            Vitals = session.State.Vitals != null
                ? session.State.Vitals.Select(CloneVital).ToList()
                : new List<CombatVitalSnapshot>(),
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
                VisualVariantId = target.CaptureContext.VisualVariantId,
                MutationId = target.CaptureContext.MutationId,
                EntangledTypeId = target.CaptureContext.EntangledTypeId,
                DifficultyId = target.CaptureContext.DifficultyId,
                KnownMoveId = target.KnownMoveId,
                RawIvs = target.RawIvs,
                Stars = target.Stars,
                Level = target.Level,
                Mutated = target.Mutated,
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
            EndReason = session.State.EndReason,
            Settlement = session.Status is "completed" or "lost" or "abandoned"
                ? session.Settlement ?? BattleRewardSettlement.Unavailable(
                    session.BattleId.ToString("D"), "settlement_not_loaded")
                : null,
        };
    }

    private async Task<CommandServiceResult> CommitActionStepAsync(
        BattleSessionRecord session,
        TeamCommandApiRequest command,
        string commandKey,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(session.Status, "active", StringComparison.Ordinal) ||
            string.Equals(session.State.Phase, "capturing", StringComparison.Ordinal))
        {
            return Rejected("battle_not_ready", "A move cannot rewind or replace an in-progress capture.");
        }
        if (command.Vitals == null || command.Vitals.Count == 0)
            return Rejected("vitals_required", "An action step must include the resulting battle vitals.");

        AuthoritativeBattleState nextState = CloneBattleState(session.State);
        if (!TryApplyVitals(nextState, command.Vitals, out string validationError))
            return Rejected("invalid_vitals", validationError);

        nextState.TurnNumber = Math.Max(1, command.TurnNumber);
        nextState.HasActionCheckpoint = true;
        string nextStatus = command.BattleActive
            ? "active"
            : command.WinnerTeamIndex == 0 ? "completed" : "lost";
        nextState.Phase = command.BattleActive ? "player_turn" : "complete";
        if (!command.BattleActive)
        {
            nextState.EndReason = string.IsNullOrWhiteSpace(command.EndReason)
                ? (command.WinnerTeamIndex == 0 ? "Victory" : "Defeat")
                : command.EndReason.Trim();
        }
        if (!_plausibility.TryClassifyTerminal(
                command,
                nextState,
                out string terminalOutcome,
                out string terminalReason,
                out string plausibilityError))
        {
            return Rejected("implausible_terminal_claim", plausibilityError);
        }

        int nextVersion = session.StateVersion + 1;
        long nextExpiry = _time.RequireUnixMilliseconds() + (long)_options.BattleResumeWindow.TotalMilliseconds;
        JsonElement persisted = await _gateway.CommitActionStepAsync(
            session,
            command,
            nextState,
            nextStatus,
            nextVersion,
            nextExpiry,
            terminalOutcome,
            terminalReason,
            cancellationToken);
        if (!ReadOk(persisted))
            return Rejected(ReadString(persisted, "code", "action_step_rejected"), "The action step could not be committed.");

        session.State = nextState;
        session.Status = nextStatus;
        session.StateVersion = nextVersion;
        session.ExpiresAtUnixMilliseconds = nextExpiry;
        if (!command.BattleActive)
        {
            session.TerminalOutcome = terminalOutcome;
            session.TerminalReason = terminalReason;
            session.TerminalAtUnixMilliseconds = _time.RequireUnixMilliseconds();
            await EnsureRewardSettlementAsync(session, cancellationToken);
        }
        CombatSessionEnvelope envelope = BuildEnvelope(session);
        _commandResults[commandKey] = envelope;
        return new CommandServiceResult { Ok = true, Session = session };
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
        {
            if (session.State.ActiveChallenge.DeadlineUnixMilliseconds > 0)
                runtime.Start();
        }
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
        try
        {
            return await FinalizeAttemptWhileLockedAsync(
                session,
                runtime.AttemptId,
                runtime.Challenge,
                finalState,
                runtime.History,
                runtime.PerfectLock);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    /// <summary>
    /// Finishes an attempt while the caller owns <see cref="BattleSessionRecord.Gate"/>.
    /// This is shared by the live capture runtime and resume reconciliation so an elapsed
    /// capture has one authoritative finalization path.
    /// </summary>
    private async Task<CaptureOutcome> FinalizeAttemptWhileLockedAsync(
        BattleSessionRecord session,
        Guid attemptId,
        CaptureChallengeSnapshot challenge,
        CaptureSimulationState finalState,
        IReadOnlyList<CaptureHistoryPoint> history,
        bool perfectLock)
    {
        AuthoritativeBattleState stateBeforeFinalize = CloneBattleState(session.State);
        int versionBeforeFinalize = session.StateVersion;
        try
        {
            AuthoritativeBattleTarget? target = session.State.Targets.FirstOrDefault(x =>
                string.Equals(x.TargetId, challenge.TargetId, StringComparison.Ordinal));
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
                    session.Status = kind == CaptureOutcomeKind.Locked ? "completed" : "lost";
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
                finalState.Tick * 1000L / Math.Max(1, challenge.Parameters.TickRate));
            string entangledType = kind == CaptureOutcomeKind.Locked && challenge.OrbId == "entangle_orb"
                ? _catalog.SelectEntangledType(challenge.Seed)
                : "";
            string? terminalOutcome = !battleContinues
                ? kind == CaptureOutcomeKind.Locked
                    ? BattleTerminalOutcome.Capture
                    : BattleTerminalOutcome.Defeat
                : null;
            JsonElement grant = await _gateway.FinalizeCaptureAttemptAsync(
                session,
                attemptId,
                kind,
                elapsedMilliseconds,
                finalState.MeterBasisPoints,
                history,
                perfectLock,
                entangledType,
                terminalOutcome,
                !battleContinues ? session.State.EndReason : null,
                CancellationToken.None);
            if (!battleContinues)
            {
                session.TerminalOutcome = terminalOutcome ?? "";
                session.TerminalReason = session.State.EndReason;
                session.TerminalAtUnixMilliseconds = _time.RequireUnixMilliseconds();
                await EnsureRewardSettlementAsync(session, CancellationToken.None);
            }
            _attempts.TryRemove(attemptId, out _);
            return new CaptureOutcome
            {
                AttemptId = challenge.AttemptId,
                Kind = kind,
                BattleContinues = battleContinues,
                FinalMeterBasisPoints = finalState.MeterBasisPoints,
                ElapsedTicks = finalState.Tick,
                GrantedChainlingId = ReadString(grant, "chainling_id", ""),
                GrantedEntangledTypeId = ReadString(grant, "entangled_type", ""),
                AddedToParty = grant.TryGetProperty("is_party_member", out JsonElement party) && party.ValueKind == JsonValueKind.True,
                UnlockedAchievementIds = ReadStrings(grant, "unlocked_achievement_ids"),
                Settlement = battleContinues ? null : session.Settlement,
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
            _attempts.TryRemove(attemptId, out _);
            _logger.LogError(exception, "Capture finalization failed for {AttemptId}; no battle loss was assessed.", challenge.AttemptId);
            return new CaptureOutcome
            {
                AttemptId = challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "service_interrupted",
                Message = "Capture authority was interrupted; the battle remains resumable and no loss was assessed.",
                BattleContinues = true,
                FinalMeterBasisPoints = finalState.MeterBasisPoints,
                ElapsedTicks = finalState.Tick,
            };
        }
    }

    private bool TryGetElapsedCaptureRuntime(
        BattleSessionRecord session,
        long authorityUnixMilliseconds,
        out CaptureAttemptRuntime? runtime)
    {
        runtime = null;
        if (!IsCaptureInProgress(session) ||
            session.State.ActiveChallenge == null ||
            session.State.ActiveChallenge.DeadlineUnixMilliseconds <= 0 ||
            session.State.ActiveChallenge.DeadlineUnixMilliseconds > authorityUnixMilliseconds ||
            !Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId))
        {
            return false;
        }

        return _attempts.TryGetValue(attemptId, out runtime);
    }

    /// <summary>
    /// Resume is a synchronization boundary: an elapsed capture must be finalized before
    /// returning a battle envelope. Otherwise the Unity client can rebuild a player turn
    /// from a still-capturing snapshot and have its next action rejected.
    /// </summary>
    private async Task ReconcileCaptureForResumeAsync(
        BattleSessionRecord session,
        long authorityUnixMilliseconds)
    {
        if (!IsCaptureInProgress(session) || session.State.ActiveChallenge == null)
            return;

        CaptureChallengeSnapshot challenge = session.State.ActiveChallenge;
        if (!Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId))
            throw new AuthorityException("capture_attempt_not_found", "The active capture attempt could not be resumed.", 409);

        if (challenge.DeadlineUnixMilliseconds <= 0 || challenge.DeadlineUnixMilliseconds > authorityUnixMilliseconds)
        {
            EnsureCaptureRuntime(session);
            return;
        }

        CaptureSimulationState state = session.State.CaptureState?.Clone() ?? CaptureSimulation.CreateInitial(challenge);
        var history = new List<CaptureHistoryPoint>();
        while (state.Outcome == CaptureOutcomeKind.None)
        {
            CaptureTickResult tick = CaptureSimulation.Step(state, challenge.Parameters, held: false);
            state = tick.State;
            if (state.Tick % Math.Max(1, challenge.Parameters.TickRate / _options.CorrectionRateHz) == 0 ||
                state.Outcome != CaptureOutcomeKind.None ||
                tick.SpikeOccurred)
            {
                history.Add(new CaptureHistoryPoint
                {
                    Tick = state.Tick,
                    MeterBasisPoints = state.MeterBasisPoints,
                    InsideBand = state.IsInsideBand,
                    TargetPositionUnits = state.TargetPositionUnits,
                    BandPositionUnits = state.BandPositionUnits,
                });
            }
        }

        // Input history before the service restarted cannot prove a perfect lock. A conservative
        // false avoids awarding a perfect-lock bonus from an incomplete checkpoint.
        await FinalizeAttemptWhileLockedAsync(session, attemptId, challenge, state, history, perfectLock: false);
    }

    private static bool IsCaptureInProgress(BattleSessionRecord session)
    {
        return (string.Equals(session.Status, "capturing", StringComparison.Ordinal) ||
                string.Equals(session.Status, "service_interrupted", StringComparison.Ordinal)) &&
               string.Equals(session.State.Phase, "capturing", StringComparison.Ordinal);
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

        await SettleBattleAsync(session, authorityUnixMilliseconds, "Abandoned", true, cancellationToken);

        throw new AuthorityException(
            "battle_expired",
            "The battle resume window expired. The battle was recorded as a loss and the party respawned.",
            410);
    }

    private bool IsScriptedBattle(BattleSessionRecord session)
    {
        return _catalog.IsScriptedEncounter(session.EncounterId) ||
            session.State.BattleKind == AuthoritativeBattleKind.Scripted;
    }

    /// <summary>
    /// Closes a cinematic battle that never used the player's party.
    /// Party HP, RAM, and rewards stay untouched.
    /// </summary>
    private async Task<bool> TryDismissScriptedBattleAsync(
        BattleSessionRecord session,
        long authorityUnixMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!IsScriptedBattle(session))
            return false;

        JsonElement result = await _gateway.DismissScriptedBattleAsync(
            session,
            authorityUnixMilliseconds,
            cancellationToken);
        if (!ReadOk(result))
        {
            throw new AuthorityException(
                ReadString(result, "code", "scripted_battle_dismiss_failed"),
                "The scripted battle could not be closed. No additional battle may start until authority recovers.");
        }

        session.Status = "abandoned";
        session.State.Phase = "complete";
        session.State.EndReason = "ScriptedDismiss";
        if (result.TryGetProperty("state_version", out JsonElement version) &&
            version.ValueKind == JsonValueKind.Number &&
            version.TryGetInt32(out int persistedVersion))
            session.StateVersion = persistedVersion;
        if (Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId) &&
            _attempts.TryRemove(attemptId, out CaptureAttemptRuntime? attempt))
        {
            attempt.Dispose();
        }
        session.State.ActiveAttemptId = "";
        session.State.ActiveChallenge = null;
        session.State.CaptureState = null;
        _sessions.TryRemove(session.BattleId, out _);
        return true;
    }

    private async Task<CombatSessionEnvelope> SettleBattleAsync(
        BattleSessionRecord session,
        long authorityUnixMilliseconds,
        string endReason,
        bool requireExpired,
        CancellationToken cancellationToken)
    {
        string terminalOutcome = string.Equals(endReason, "Forfeited", StringComparison.Ordinal)
            ? BattleTerminalOutcome.Forfeit
            : BattleTerminalOutcome.Abandon;
        JsonElement result = await _gateway.SettleBattleAsync(
            session,
            authorityUnixMilliseconds,
            _options.AbandonmentRamPenaltyFlat,
            _options.DefeatKoRespawnHpBasisPoints,
            endReason,
            requireExpired,
            terminalOutcome,
            endReason,
            cancellationToken);
        if (!ReadOk(result))
        {
            throw new AuthorityException(
                ReadString(result, "code", "battle_settlement_failed"),
                "The battle could not be settled safely. No additional battle may start until authority recovers.");
        }

        session.Status = "abandoned";
        session.State.Phase = "complete";
        session.State.EndReason = endReason;
        if (result.TryGetProperty("state_version", out JsonElement version) &&
            version.ValueKind == JsonValueKind.Number &&
            version.TryGetInt32(out int persistedVersion))
            session.StateVersion = persistedVersion;
        if (Guid.TryParse(session.State.ActiveAttemptId, out Guid attemptId) && _attempts.TryRemove(attemptId, out CaptureAttemptRuntime? attempt))
            attempt.Dispose();
        session.State.ActiveAttemptId = "";
        session.State.ActiveChallenge = null;
        session.State.CaptureState = null;
        session.TerminalOutcome = terminalOutcome;
        session.TerminalReason = endReason;
        session.TerminalAtUnixMilliseconds = authorityUnixMilliseconds;
        await EnsureRewardSettlementAsync(session, cancellationToken);
        _sessions.TryRemove(session.BattleId, out _);

        CombatSessionEnvelope envelope = BuildEnvelope(session);
        envelope.RequiresWipe = true;
        envelope.EndReason = endReason;
        envelope.Message = string.Equals(endReason, "Forfeited", StringComparison.Ordinal)
            ? "The battle was forfeited. Your party was knocked out and revived at the sanctuary."
            : "That battle ran out of time. Your party was knocked out and revived at the sanctuary.";
        return envelope;
    }

    private async Task EnsureRewardSettlementAsync(
        BattleSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (RewardsDisabledFor(session))
        {
            session.Settlement ??= BattleRewardSettlement.Unavailable(
                session.BattleId.ToString("D"), "rewards_disabled");
            return;
        }
        if (session.Settlement?.IsTerminal == true) return;
        SemaphoreSlim rewardGate = _rewardGates.GetOrAdd(session.BattleId, _ => new SemaphoreSlim(1, 1));
        await rewardGate.WaitAsync(cancellationToken);
        try
        {
            if (session.Settlement?.IsTerminal == true) return;
            BattleRewardSettlement? stored = await _gateway.GetBattleRewardAsync(session.BattleId, cancellationToken);
            if (stored != null)
            {
                session.Settlement = stored;
                _rewardRetryAfter.TryRemove(session.BattleId, out _);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_rewardRetryAfter.TryGetValue(session.BattleId, out long retryAfter) && retryAfter > now)
            {
                session.Settlement = BattleRewardSettlement.Unavailable(
                    session.BattleId.ToString("D"), "reward_retry_throttled");
                return;
            }
            _rewardRetryAfter[session.BattleId] = now + (long)_options.BattleRewardRetryCooldown.TotalMilliseconds;
            session.Settlement = await _rewards.SettleAsync(session.BattleId, cancellationToken);
            if (session.Settlement.IsTerminal)
                _rewardRetryAfter.TryRemove(session.BattleId, out _);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Immediate RAM settlement failed for battle {BattleId}.", session.BattleId);
            session.Settlement = BattleRewardSettlement.Unavailable(
                session.BattleId.ToString("D"), "reward_unavailable");
        }
        finally
        {
            rewardGate.Release();
        }
    }

    private bool RewardsDisabledFor(BattleSessionRecord session)
    {
        return string.Equals(_options.BattleRewardMode, "off", StringComparison.Ordinal) ||
            string.Equals(session.RewardTier, BattleRewardTier.None, StringComparison.Ordinal);
    }

    private static void EnsureOwner(BattleSessionRecord session, AuthenticatedProfile owner)
    {
        if (session.ProfileId != owner.ProfileId)
            throw new AuthorityException("battle_not_owned", "The battle does not belong to this profile.", 403);
    }

    private static bool IsTerminal(BattleSessionRecord session)
    {
        return session != null && session.Status is "completed" or "lost" or "abandoned";
    }

    private static AuthoritativeBattleState BuildInitialState(
        ManifestEncounter encounter,
        string zoneId,
        List<CombatVitalSnapshot> partyVitals,
        uint seed)
    {
        var state = new AuthoritativeBattleState
        {
            BattleKind = encounter.BattleKind,
            EncounterCaptureEnabled = encounter.CaptureEnabled,
            CapturePolicy = encounter.CapturePolicy,
            Phase = "player_turn",
            TurnNumber = 1,
            Vitals = partyVitals != null
                ? partyVitals.Select(CloneVital).ToList()
                : new List<CombatVitalSnapshot>(),
        };
        for (int i = 0; i < encounter.Targets.Count; i++)
        {
            ManifestTarget source = encounter.Targets[i];
            int[] ivs = source.RollIvs
                ? RollTargetIvs(source, seed, i)
                : NormalizeIvs(source.Ivs);
            int stars = RollTargetStars(source, seed, i);
            int maxHp = Math.Max(1, source.MaxHp + (source.RollIvs ? ivs[0] : 0));
            int currentHp = source.RollIvs && source.CurrentHp >= source.MaxHp
                ? maxHp
                : Math.Clamp(source.CurrentHp, 0, maxHp);
            state.Targets.Add(new AuthoritativeBattleTarget
            {
                TargetId = string.IsNullOrWhiteSpace(source.TargetId) ? $"enemy-{i}" : source.TargetId,
                DisplayName = source.DisplayName,
                KnownMoveId = source.KnownMoveId,
                RawIvs = ivs,
                Stars = stars,
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
                    CurrentHp = currentHp,
                    MaxHp = maxHp,
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
            state.Vitals.Add(new CombatVitalSnapshot
            {
                EntityId = string.IsNullOrWhiteSpace(source.TargetId) ? $"enemy-{i}" : source.TargetId,
                TeamIndex = 1,
                RosterIndex = i,
                CurrentHp = currentHp,
                CurrentAp = 0,
                IsActive = source.StartsActive,
            });
        }
        if (!state.Targets.Any(x => x.IsActive) && state.Targets.Count > 0)
            state.Targets[0].IsActive = true;
        return state;
    }

    private static int[] RollTargetIvs(ManifestTarget source, uint seed, int targetIndex)
    {
        int minimum = Math.Clamp(source.IvMin, -100, 100);
        int maximum = Math.Clamp(source.IvMax, minimum, 100);
        var rng = new DeterministicCaptureRng(
            seed ^ DeterministicCaptureRng.StableSeed(source.TargetId, (uint)(targetIndex + 1)));
        var result = new int[6];
        for (int i = 0; i < result.Length; i++)
            result[i] = rng.NextInt(minimum, maximum + 1);
        return result;
    }

    private static int RollTargetStars(ManifestTarget source, uint seed, int targetIndex)
    {
        int variance = Math.Clamp(source.StarVariance, 0, 5);
        if (variance == 0)
            return Math.Clamp(source.Stars, 0, 5);
        var rng = new DeterministicCaptureRng(
            seed ^ DeterministicCaptureRng.StableSeed(source.TargetId, (uint)(0x51A7 + targetIndex)));
        return Math.Clamp(source.Stars + rng.NextInt(-variance, variance + 1), 0, 5);
    }

    private static bool TryApplyVitals(
        AuthoritativeBattleState state,
        List<CombatVitalSnapshot> submitted,
        out string error)
    {
        error = "";
        var normalized = new List<CombatVitalSnapshot>(submitted.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (CombatVitalSnapshot source in submitted)
        {
            if (source == null || source.TeamIndex is < 0 or > 1 || source.RosterIndex < 0 ||
                source.CurrentHp < 0 || source.CurrentAp < 0)
            {
                error = "Battle vitals contained an invalid team, roster index, HP, or AP value.";
                return false;
            }
            string key = $"{source.TeamIndex}:{source.RosterIndex}";
            if (!seen.Add(key))
            {
                error = "Battle vitals contained a duplicate roster slot.";
                return false;
            }
            normalized.Add(CloneVital(source));
        }

        for (int i = 0; i < state.Targets.Count; i++)
        {
            CombatVitalSnapshot? targetVital = normalized.FirstOrDefault(v =>
                v.TeamIndex == 1 &&
                (v.RosterIndex == i || string.Equals(v.EntityId, state.Targets[i].TargetId, StringComparison.Ordinal)));
            if (targetVital == null)
                continue;
            state.Targets[i].CaptureContext.CurrentHp = Math.Clamp(
                targetVital.CurrentHp,
                0,
                Math.Max(1, state.Targets[i].CaptureContext.MaxHp));
            state.Targets[i].IsActive = targetVital.IsActive;
            state.Targets[i].IsRemoved = targetVital.CurrentHp <= 0;
        }
        state.Vitals = normalized;
        return true;
    }

    private static CombatVitalSnapshot CloneVital(CombatVitalSnapshot? source)
    {
        return new CombatVitalSnapshot
        {
            EntityId = source?.EntityId ?? "",
            TeamIndex = source?.TeamIndex ?? 0,
            RosterIndex = source?.RosterIndex ?? 0,
            CurrentHp = source?.CurrentHp ?? 0,
            CurrentAp = source?.CurrentAp ?? 0,
            IsActive = source?.IsActive ?? false,
        };
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
