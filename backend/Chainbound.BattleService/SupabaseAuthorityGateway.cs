using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class SupabaseAuthorityGateway
{
    private readonly HttpClient _http;
    private readonly ServiceOptions _options;

    public SupabaseAuthorityGateway(HttpClient http, ServiceOptions options)
    {
        _http = http;
        _options = options;
    }

    public async Task<AuthenticatedProfile> AuthenticateAsync(string authorizationHeader, CancellationToken cancellationToken)
    {
        if (!_options.HasAuthoritySecrets)
            throw new AuthorityException("authority_not_configured", "Battle authority secrets are not configured.");

        string token = ReadBearer(authorizationHeader);
        using var authRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.SupabaseUrl}/auth/v1/user");
        authRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        authRequest.Headers.Add("apikey", _options.SupabaseAnonKey);
        using HttpResponseMessage authResponse = await _http.SendAsync(authRequest, cancellationToken);
        if (!authResponse.IsSuccessStatusCode)
            throw new AuthorityException("invalid_access_token", "The Supabase access token is invalid or expired.", 401);

        using JsonDocument authJson = JsonDocument.Parse(await authResponse.Content.ReadAsStringAsync(cancellationToken));
        string authUserId = authJson.RootElement.TryGetProperty("id", out JsonElement idElement)
            ? idElement.GetString() ?? ""
            : "";
        if (string.IsNullOrWhiteSpace(authUserId))
            throw new AuthorityException("invalid_access_token", "The Supabase access token has no user subject.", 401);

        string filter = Uri.EscapeDataString(authUserId);
        string url = $"{_options.SupabaseUrl}/rest/v1/profile_identities?auth_user_id=eq.{filter}&revoked_at=is.null&select=player_profile_id&limit=1";
        using var profileRequest = CreateServiceRequest(HttpMethod.Get, url);
        using HttpResponseMessage profileResponse = await _http.SendAsync(profileRequest, cancellationToken);
        await EnsureSuccess(profileResponse, "profile_lookup_failed", cancellationToken);
        using JsonDocument profileJson = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(cancellationToken));
        if (profileJson.RootElement.ValueKind != JsonValueKind.Array || profileJson.RootElement.GetArrayLength() == 0)
            throw new AuthorityException("profile_not_found", "No owned gameplay profile is linked to this account.", 404);
        string profileIdText = profileJson.RootElement[0].GetProperty("player_profile_id").GetString() ?? "";
        if (!Guid.TryParse(profileIdText, out Guid profileId))
            throw new AuthorityException("profile_not_found", "The linked gameplay profile is invalid.", 404);
        return new AuthenticatedProfile { AuthUserId = authUserId, ProfileId = profileId };
    }

    public async Task<AuthorityLoadoutSnapshot> GetLoadoutAsync(Guid profileId, long authorityUnixMilliseconds, CancellationToken cancellationToken)
    {
        JsonElement json = await RpcAsync("capture_get_loadout_service", new
        {
            p_profile_id = profileId,
            p_authority_unix_milliseconds = authorityUnixMilliseconds,
        }, cancellationToken);
        return ParseLoadout(json);
    }

    public async Task<AuthorityLoadoutSnapshot> EquipAsync(Guid profileId, string slot, string itemId, long authorityUnixMilliseconds, CancellationToken cancellationToken)
    {
        JsonElement json = await RpcAsync("capture_equip_utility_gear_service", new
        {
            p_profile_id = profileId,
            p_slot = slot,
            p_item_id = itemId,
            p_authority_unix_milliseconds = authorityUnixMilliseconds,
        }, cancellationToken);
        return ParseLoadout(json);
    }

    public async Task<AuthorityLoadoutSnapshot> ActivateBoostAsync(Guid profileId, string itemId, long authorityUnixMilliseconds, CancellationToken cancellationToken)
    {
        JsonElement json = await RpcAsync("capture_activate_boost_service", new
        {
            p_profile_id = profileId,
            p_item_id = itemId,
            p_authority_unix_milliseconds = authorityUnixMilliseconds,
        }, cancellationToken);
        return ParseLoadout(json);
    }

    public async Task<ResolvedCaptureProfile> ResolveCaptureProfileAsync(CaptureTargetContext target, long authorityUnixMilliseconds, CancellationToken cancellationToken)
    {
        JsonElement json = await RpcAsync("capture_resolve_profile_service", new
        {
            p_target = new
            {
                target_id = target.TargetId,
                species_id = target.SpeciesId,
                visual_variant_id = target.VisualVariantId,
                mutation_id = target.MutationId,
                entangled = target.IsEntangled,
                entangled_type = target.EntangledTypeId,
                area_id = target.AreaId,
                zone_id = target.ZoneId,
                difficulty_id = target.DifficultyId,
                legacy_base_capture_rate = target.LegacyBaseCaptureRate,
            },
            p_authority_unix_milliseconds = authorityUnixMilliseconds,
        }, cancellationToken);

        JsonElement profile = json.GetProperty("profile");
        var result = new ResolvedCaptureProfile
        {
            Profile = new CaptureMotionProfile
            {
                ProfileId = ReadString(profile, "profile_id"),
                Difficulty = ParseDifficulty(ReadString(profile, "difficulty")),
                MotionMultiplierBasisPoints = ReadInt(profile, "motion_multiplier_bp"),
                TimerMultiplierBasisPoints = ReadInt(profile, "timer_multiplier_bp"),
                SpikeChanceMultiplierBasisPoints = ReadInt(profile, "spike_chance_multiplier_bp"),
            },
            ProfileVersion = ReadInt(profile, "config_version"),
            ProfileEffects = ReadNestedEffect(profile, "typed_delta"),
            EventEffects = ReadEffect(json, "event_effects"),
            AssignmentId = ReadString(json, "assignment_id"),
            AssignmentVersion = ReadInt(json, "assignment_version"),
        };
        if (json.TryGetProperty("events", out JsonElement events) && events.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in events.EnumerateArray())
                result.EventStamps.Add($"{ReadString(entry, "event_id")}@{ReadInt(entry, "config_version")}");
        }
        return result;
    }

    public async Task<JsonElement> BeginBattleAsync(BattleSessionRecord session, CancellationToken cancellationToken)
    {
        bool noReward = string.Equals(session.RewardTier, BattleRewardTier.None, StringComparison.Ordinal);
        return await RpcAsync("authoritative_begin_battle_service", new
        {
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_start_command_id = session.StartCommandId,
            p_battle_kind = ToBattleKind(session.State.BattleKind),
            p_seed = (long)session.Seed,
            p_manifest_hash = session.ManifestHash,
            p_config_hash = session.ConfigHash,
            p_encounter_id = session.EncounterId,
            p_reward_tier = session.RewardTier,
            p_reward_config_version = noReward ? 0 : session.RewardConfigVersion,
            p_reward_config_hash = noReward ? new string('0', 64) : session.RewardConfigHash,
            p_state_json = session.State,
            p_equipment_snapshot = session.LockedLoadout,
            p_expires_at_unix_milliseconds = session.ExpiresAtUnixMilliseconds,
        }, cancellationToken);
    }

    public async Task<bool> IsNarrativeRewardEligibleAsync(
        Guid profileId,
        ManifestEncounter encounter,
        CancellationToken cancellationToken)
    {
        if (encounter.BattleKind == AuthoritativeBattleKind.Wild)
            return true;
        if (string.IsNullOrWhiteSpace(encounter.RewardEligibilityQuestId))
            return false;

        string questId = Uri.EscapeDataString(encounter.RewardEligibilityQuestId.Trim());
        string questUrl = $"{_options.SupabaseUrl}/rest/v1/profile_quest_states" +
            $"?player_profile_id=eq.{profileId:D}&quest_id=eq.{questId}" +
            "&lifecycle=in.(available,active)&select=quest_id&limit=1";
        using var questRequest = CreateServiceRequest(HttpMethod.Get, questUrl);
        using HttpResponseMessage questResponse = await _http.SendAsync(questRequest, cancellationToken);
        await EnsureSuccess(questResponse, "narrative_reward_progress_lookup_failed", cancellationToken);
        using JsonDocument questJson = JsonDocument.Parse(await questResponse.Content.ReadAsStringAsync(cancellationToken));
        if (questJson.RootElement.ValueKind != JsonValueKind.Array || questJson.RootElement.GetArrayLength() == 0)
            return false;

        string encounterId = Uri.EscapeDataString(encounter.EncounterId.Trim());
        string receiptUrl = $"{_options.SupabaseUrl}/rest/v1/battle_reward_settlements" +
            $"?player_profile_id=eq.{profileId:D}&encounter_id=eq.{encounterId}" +
            "&status=eq.settled&applied_amount=gt.0&select=settlement_id&limit=1";
        using var receiptRequest = CreateServiceRequest(HttpMethod.Get, receiptUrl);
        using HttpResponseMessage receiptResponse = await _http.SendAsync(receiptRequest, cancellationToken);
        await EnsureSuccess(receiptResponse, "narrative_reward_history_lookup_failed", cancellationToken);
        using JsonDocument receiptJson = JsonDocument.Parse(await receiptResponse.Content.ReadAsStringAsync(cancellationToken));
        return receiptJson.RootElement.ValueKind == JsonValueKind.Array && receiptJson.RootElement.GetArrayLength() == 0;
    }

    public async Task<JsonElement> CheckpointBattleAsync(BattleSessionRecord session, int expectedVersion, CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_checkpoint_battle_service", new
        {
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_expected_version = expectedVersion,
            p_new_version = session.StateVersion,
            p_status = session.Status,
            p_state_json = session.State,
            p_expires_at_unix_milliseconds = session.ExpiresAtUnixMilliseconds,
        }, cancellationToken);
    }

    public async Task<JsonElement> SettleBattleAsync(
        BattleSessionRecord session,
        long authorityUnixMilliseconds,
        int ramPenalty,
        int respawnHpBasisPoints,
        string endReason,
        bool requireExpired,
        string terminalOutcome,
        string terminalReason,
        CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_settle_battle_service", new
        {
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_authority_unix_milliseconds = authorityUnixMilliseconds,
            p_ram_penalty = ramPenalty,
            p_respawn_hp_basis_points = respawnHpBasisPoints,
            p_end_reason = endReason,
            p_require_expired = requireExpired,
            p_terminal_outcome = terminalOutcome,
            p_terminal_reason = terminalReason,
        }, cancellationToken);
    }

    public async Task<JsonElement> CommitActionStepAsync(
        BattleSessionRecord session,
        TeamCommandApiRequest command,
        AuthoritativeBattleState nextState,
        string nextStatus,
        int nextVersion,
        long expiresAtUnixMilliseconds,
        string terminalOutcome,
        string terminalReason,
        CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_commit_action_step_service", new
        {
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_command_id = command.CommandId,
            p_expected_version = command.ExpectedVersion,
            p_new_state_version = nextVersion,
            p_new_status = nextStatus,
            p_new_state_json = nextState,
            p_request_json = command,
            p_expires_at_unix_milliseconds = expiresAtUnixMilliseconds,
            p_terminal_outcome = string.IsNullOrWhiteSpace(terminalOutcome) ? null : terminalOutcome,
            p_terminal_reason = string.IsNullOrWhiteSpace(terminalReason) ? null : terminalReason,
        }, cancellationToken);
    }

    public async Task<BattleRewardSettlement?> GetBattleRewardAsync(
        Guid battleId,
        CancellationToken cancellationToken)
    {
        JsonElement result = await RpcAsync("battle_reward_get_service", new
        {
            p_battle_id = battleId,
        }, cancellationToken);
        if (result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        BattleRewardSettlement? settlement = JsonSerializer.Deserialize<BattleRewardSettlement>(result.GetRawText(), JsonDefaults.Options);
        if (settlement == null || !BattleRewardReceiptValidator.IsValid(settlement, battleId))
            throw new AuthorityException("stored_reward_receipt_invalid", "The stored reward receipt failed integrity validation.", 503);
        return settlement;
    }

    public async Task<JsonElement> RecordCommandAsync(
        BattleSessionRecord session,
        TeamCommandApiRequest command,
        int resultVersion,
        object result,
        CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_record_command_service", new
        {
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_command_id = command.CommandId,
            p_expected_version = command.ExpectedVersion,
            p_result_version = resultVersion,
            p_command_kind = command.Kind,
            p_request_json = command,
            p_result_json = result,
        }, cancellationToken);
    }

    public async Task<bool> HasCommandAsync(
        Guid battleId,
        Guid profileId,
        string commandId,
        CancellationToken cancellationToken)
    {
        string escapedCommand = Uri.EscapeDataString(commandId ?? "");
        string url = $"{_options.SupabaseUrl}/rest/v1/authoritative_pve_commands" +
            $"?battle_id=eq.{battleId:D}&player_profile_id=eq.{profileId:D}" +
            $"&command_id=eq.{escapedCommand}&select=command_id&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "command_lookup_failed", cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() > 0;
    }

    public async Task<JsonElement> BeginCaptureAttemptAsync(
        BattleSessionRecord session,
        TeamCommandApiRequest command,
        Guid attemptId,
        CaptureChallengeSnapshot challenge,
        object targetSnapshot,
        int lowHpThresholdBasisPoints,
        CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_begin_capture_attempt_service", new
        {
            p_attempt_id = attemptId,
            p_battle_id = session.BattleId,
            p_profile_id = session.ProfileId,
            p_command_id = command.CommandId,
            p_expected_version = command.ExpectedVersion,
            p_orb_id = command.OrbId,
            p_capture_profile_id = challenge.ProfileId,
            p_battle_config_hash = session.ConfigHash,
            p_attempt_config_hash = challenge.ConfigHash,
            p_target_snapshot = targetSnapshot,
            p_low_hp_threshold_bp = lowHpThresholdBasisPoints,
            p_new_state_json = session.State,
            p_expires_at_unix_milliseconds = session.ExpiresAtUnixMilliseconds,
        }, cancellationToken);
    }

    public async Task<JsonElement> FinalizeCaptureAttemptAsync(
        BattleSessionRecord session,
        Guid attemptId,
        CaptureOutcomeKind outcome,
        int elapsedMilliseconds,
        int finalMeterBasisPoints,
        object history,
        bool perfectLock,
        string entangledType,
        string? terminalOutcome,
        string? terminalReason,
        CancellationToken cancellationToken)
    {
        return await RpcAsync("authoritative_finalize_capture_attempt_service", new
        {
            p_attempt_id = attemptId,
            p_profile_id = session.ProfileId,
            p_outcome = ToOutcome(outcome),
            p_elapsed_milliseconds = elapsedMilliseconds,
            p_final_meter_bp = finalMeterBasisPoints,
            p_history = history,
            p_perfect_lock = perfectLock,
            p_entangled_type = entangledType,
            p_new_battle_status = session.Status,
            p_new_state_version = session.StateVersion,
            p_new_state_json = session.State,
            p_terminal_outcome = terminalOutcome,
            p_terminal_reason = terminalReason,
        }, cancellationToken);
    }

    public async Task<BattleSessionRecord?> GetBattleAsync(Guid battleId, Guid profileId, CancellationToken cancellationToken)
    {
        string url = $"{_options.SupabaseUrl}/rest/v1/authoritative_pve_battles?battle_id=eq.{battleId:D}&player_profile_id=eq.{profileId:D}&select=*&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "battle_resume_failed", cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (json.RootElement.GetArrayLength() == 0)
            return null;
        JsonElement row = json.RootElement[0];
        AuthoritativeBattleState? state = JsonSerializer.Deserialize<AuthoritativeBattleState>(row.GetProperty("state_json").GetRawText(), JsonDefaults.Options);
        AuthorityLoadoutSnapshot? loadout = JsonSerializer.Deserialize<AuthorityLoadoutSnapshot>(row.GetProperty("equipment_snapshot").GetRawText(), JsonDefaults.Options);
        return new BattleSessionRecord
        {
            BattleId = battleId,
            ProfileId = profileId,
            StartCommandId = ReadString(row, "start_command_id"),
            StateVersion = ReadInt(row, "state_version"),
            Seed = checked((uint)ReadLong(row, "seed")),
            ManifestHash = ReadString(row, "manifest_hash"),
            ConfigHash = ReadString(row, "config_hash"),
            EncounterId = ReadString(row, "encounter_id"),
            RewardTier = ReadString(row, "reward_tier"),
            RewardConfigVersion = ReadInt(row, "reward_config_version"),
            RewardConfigHash = ReadString(row, "reward_config_hash"),
            TerminalOutcome = ReadString(row, "terminal_outcome"),
            TerminalReason = ReadString(row, "terminal_reason"),
            TerminalAtUnixMilliseconds = ReadOptionalTimestampMilliseconds(row, "terminal_at"),
            Status = ReadString(row, "status"),
            ExpiresAtUnixMilliseconds = DateTimeOffset.Parse(ReadString(row, "expires_at")).ToUnixTimeMilliseconds(),
            State = state ?? new AuthoritativeBattleState(),
            LockedLoadout = loadout ?? new AuthorityLoadoutSnapshot(),
        };
    }

    public async Task<BattleSessionRecord?> GetCurrentBattleAsync(Guid profileId, CancellationToken cancellationToken)
    {
        string url = $"{_options.SupabaseUrl}/rest/v1/authoritative_pve_battles" +
            $"?player_profile_id=eq.{profileId:D}" +
            "&status=in.(active,capturing,service_interrupted)" +
            "&select=*&order=started_at.desc&limit=1";
        using var request = CreateServiceRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "current_battle_lookup_failed", cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
            return null;
        return ParseBattle(json.RootElement[0], profileId);
    }

    public async Task<List<CombatVitalSnapshot>> GetPartyVitalsAsync(Guid profileId, CancellationToken cancellationToken)
    {
        string url = $"{_options.SupabaseUrl}/rest/v1/chainlings" +
            $"?player_profile_id=eq.{profileId:D}&is_party_member=eq.true" +
            "&select=id,current_hp,party_slot&order=party_slot.asc";
        using var request = CreateServiceRequest(HttpMethod.Get, url);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "party_vitals_lookup_failed", cancellationToken);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var result = new List<CombatVitalSnapshot>();
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            return result;
        int fallbackIndex = 0;
        foreach (JsonElement row in json.RootElement.EnumerateArray())
        {
            int rosterIndex = row.TryGetProperty("party_slot", out JsonElement slot) && slot.TryGetInt32(out int parsedSlot)
                ? parsedSlot
                : fallbackIndex;
            int currentHp = row.TryGetProperty("current_hp", out JsonElement hp) && hp.TryGetInt32(out int parsedHp)
                ? Math.Max(0, parsedHp)
                : -1;
            result.Add(new CombatVitalSnapshot
            {
                EntityId = ReadString(row, "id"),
                TeamIndex = 0,
                RosterIndex = Math.Max(0, rosterIndex),
                CurrentHp = currentHp,
                CurrentAp = 0,
                IsActive = rosterIndex >= 0 && rosterIndex < 2,
            });
            fallbackIndex++;
        }
        return result;
    }

    private static BattleSessionRecord ParseBattle(JsonElement row, Guid profileId)
    {
        Guid battleId = Guid.Parse(ReadString(row, "battle_id"));
        AuthoritativeBattleState? state = JsonSerializer.Deserialize<AuthoritativeBattleState>(row.GetProperty("state_json").GetRawText(), JsonDefaults.Options);
        AuthorityLoadoutSnapshot? loadout = JsonSerializer.Deserialize<AuthorityLoadoutSnapshot>(row.GetProperty("equipment_snapshot").GetRawText(), JsonDefaults.Options);
        return new BattleSessionRecord
        {
            BattleId = battleId,
            ProfileId = profileId,
            StartCommandId = ReadString(row, "start_command_id"),
            StateVersion = ReadInt(row, "state_version"),
            Seed = checked((uint)ReadLong(row, "seed")),
            ManifestHash = ReadString(row, "manifest_hash"),
            ConfigHash = ReadString(row, "config_hash"),
            EncounterId = ReadString(row, "encounter_id"),
            RewardTier = ReadString(row, "reward_tier"),
            RewardConfigVersion = ReadInt(row, "reward_config_version"),
            RewardConfigHash = ReadString(row, "reward_config_hash"),
            TerminalOutcome = ReadString(row, "terminal_outcome"),
            TerminalReason = ReadString(row, "terminal_reason"),
            TerminalAtUnixMilliseconds = ReadOptionalTimestampMilliseconds(row, "terminal_at"),
            Status = ReadString(row, "status"),
            ExpiresAtUnixMilliseconds = DateTimeOffset.Parse(ReadString(row, "expires_at")).ToUnixTimeMilliseconds(),
            State = state ?? new AuthoritativeBattleState(),
            LockedLoadout = loadout ?? new AuthorityLoadoutSnapshot(),
        };
    }

    private async Task<JsonElement> RpcAsync(string functionName, object body, CancellationToken cancellationToken)
    {
        string url = $"{_options.SupabaseUrl}/rest/v1/rpc/{functionName}";
        using var request = CreateServiceRequest(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonDefaults.Options), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, functionName, cancellationToken);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        using JsonDocument document = JsonDocument.Parse(string.IsNullOrWhiteSpace(responseBody) ? "{}" : responseBody);
        return document.RootElement.Clone();
    }

    private HttpRequestMessage CreateServiceRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SupabaseServiceRoleKey);
        request.Headers.Add("apikey", _options.SupabaseServiceRoleKey);
        return request;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new AuthorityException(
            "authority_persistence_failed",
            $"{operation} failed with {(int)response.StatusCode}: {body}",
            503);
    }

    private static string ReadBearer(string header)
    {
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new AuthorityException("access_token_required", "A Supabase bearer access token is required.", 401);
        string token = header[7..].Trim();
        if (token.Length == 0)
            throw new AuthorityException("access_token_required", "A Supabase bearer access token is required.", 401);
        return token;
    }

    private static AuthorityLoadoutSnapshot ParseLoadout(JsonElement json)
    {
        var snapshot = new AuthorityLoadoutSnapshot
        {
            HeadItemId = ReadString(json, "head_item_id"),
            BodyItemId = ReadString(json, "body_item_id"),
            GearEffects = ReadEffect(json, "gear_effects"),
            BoostEffects = ReadEffect(json, "boost_effects"),
            AuthorityUnixMilliseconds = ReadLong(json, "authority_unix_milliseconds"),
        };
        if (json.TryGetProperty("active_boosts", out JsonElement boosts) && boosts.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement boost in boosts.EnumerateArray())
            {
                snapshot.ActiveBoosts.Add(new ActiveCaptureBoost
                {
                    ItemId = ReadString(boost, "item_id"),
                    ExpiresAtUnixMilliseconds = ReadLong(boost, "expires_at_unix_milliseconds"),
                    Effects = ReadEffect(boost, "effects"),
                });
            }
        }
        return snapshot;
    }

    private static CaptureEffectVector ReadEffect(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement effect) || effect.ValueKind != JsonValueKind.Object)
            return CaptureEffectVector.None;
        return new CaptureEffectVector(
            ReadInt(effect, "scan"),
            ReadInt(effect, "stability_bp"),
            ReadInt(effect, "band_width_bp"),
            ReadInt(effect, "lock_rate_bp"),
            ReadInt(effect, "capture_time_bp"));
    }

    private static CaptureEffectVector ReadNestedEffect(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement typed) || typed.ValueKind != JsonValueKind.Object)
            return CaptureEffectVector.None;
        if (typed.TryGetProperty("capture_effect", out JsonElement nested) && nested.ValueKind == JsonValueKind.Object)
            return ReadEffectValue(nested);
        return ReadEffectValue(typed);
    }

    private static CaptureEffectVector ReadEffectValue(JsonElement effect)
    {
        return new CaptureEffectVector(
            ReadInt(effect, "scan"),
            ReadInt(effect, "stability_bp"),
            ReadInt(effect, "band_width_bp"),
            ReadInt(effect, "lock_rate_bp"),
            ReadInt(effect, "capture_time_bp"));
    }

    private static string ReadString(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : "";
    }

    private static int ReadInt(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : 0;
    }

    private static long ReadLong(JsonElement parent, string property)
    {
        return parent.TryGetProperty(property, out JsonElement value) && value.TryGetInt64(out long parsed) ? parsed : 0L;
    }

    private static long? ReadOptionalTimestampMilliseconds(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out JsonElement value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.ToString(), out DateTimeOffset parsed)
            ? parsed.ToUnixTimeMilliseconds()
            : null;
    }

    private static CaptureVolatilityProfile ParseDifficulty(string value)
    {
        return Enum.TryParse(value, true, out CaptureVolatilityProfile parsed)
            ? parsed
            : CaptureVolatilityProfile.Standard;
    }

    private static string ToBattleKind(AuthoritativeBattleKind kind) => kind switch
    {
        AuthoritativeBattleKind.Boss => "boss",
        AuthoritativeBattleKind.Trainer => "trainer",
        AuthoritativeBattleKind.Scripted => "scripted",
        _ => "wild",
    };

    private static string ToOutcome(CaptureOutcomeKind outcome) => outcome switch
    {
        CaptureOutcomeKind.Locked => "locked",
        CaptureOutcomeKind.Escaped => "escaped",
        CaptureOutcomeKind.TimedOut => "timed_out",
        _ => "interrupted",
    };
}
