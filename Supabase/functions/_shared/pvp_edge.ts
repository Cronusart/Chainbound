import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requireAuthenticatedUser,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "./account_helpers.ts";
import { hashMatchmakingConfig, MATCHMAKING_CONFIG, rejectEnqueue } from "./matchmaking_config.ts";
import { loadLockedParty, prepareLockedParty, profileHasActiveWallet, PvpPartyWire } from "./pvp_party.ts";
import { partyLockError, TurnAction, validateTurnActions } from "./pvp_rules.ts";

type PvpOperation = "enqueue" | "cancel" | "matchmaker" | "lock" | "get" | "turn" | "result";

export async function handlePvp(req: Request, operation: PvpOperation): Promise<Response> {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    switch (operation) {
      case "enqueue": {
        const authenticated = await requireAuthenticatedUser(req);
        if (!authenticated?.userId) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
        return await enqueue(supabase, profile.player_profile_id, authenticated.userId, body);
      }
      case "cancel":
        return await rpcSnapshot(supabase, "pvp_cancel_queue_service", { p_profile_id: profile.player_profile_id });
      case "matchmaker":
        return await sweepCreatedMatches(supabase, profile.player_profile_id);
      case "lock":
        return await lockMatch(supabase, profile.player_profile_id, str(body.match_id));
      case "get":
        return await getMatch(supabase, profile.player_profile_id, str(body.match_id));
      case "turn":
        return await submitTurn(supabase, profile.player_profile_id, body);
      case "result":
        return await submitResult(supabase, profile.player_profile_id, body);
      default:
        return jsonResponse({ success: false, error: "unknown_operation" }, 400);
    }
  } catch (error) {
    console.error(`[PvPMatch] ${operation}`, error);
    return jsonResponse({ success: false, error: "pvp_failed" }, 500);
  }
}

async function enqueue(
  supabase: SupabaseClient,
  profileId: string,
  authUserId: string,
  body: JsonMap,
): Promise<Response> {
  const cfg = MATCHMAKING_CONFIG;
  const serverHash = await hashMatchmakingConfig(cfg);
  const walletLinked = cfg.requireWalletForCasual ? await profileHasActiveWallet(supabase, profileId) : false;
  const rejection = rejectEnqueue(cfg, serverHash, {
    queueType: str(body.queue_type) || "casual",
    rulesetId: str(body.ruleset_id),
    configHash: str(body.config_hash),
    appVersion: str(body.app_version),
    resolverHash: str(body.resolver_hash),
    rulesetHash: str(body.ruleset_hash),
    buildStamp: str(body.build_stamp),
    walletLinked,
  });
  if (rejection) return jsonResponse({ success: false, error: rejection, phase: "idle" });

  const ownParty = await loadLockedParty(supabase, profileId);
  if (!ownParty) return jsonResponse({ success: false, error: "empty_party", phase: "idle" });
  const partyProblem = partyLockError(ownParty.chainlings.map((chainling) => ({ currentHp: chainling.currentHp })));
  if (partyProblem) return jsonResponse({ success: false, error: partyProblem, phase: "idle" });

  const queued = await callRpc(supabase, "pvp_enqueue_service", {
    p_profile_id: profileId,
    p_queue_type: "casual",
    p_ruleset_id: cfg.defaultCasualRulesetId,
    p_config_hash: serverHash,
    p_app_version: str(body.app_version),
    p_resolver_hash: str(body.resolver_hash),
    p_ruleset_hash: str(body.ruleset_hash),
    p_build_stamp: str(body.build_stamp),
    p_auth_user_id: authUserId,
    p_queue_timeout_seconds: cfg.queueTimeoutSeconds,
    p_turn_limit_seconds: cfg.turnActionTimeLimitSeconds,
    p_forfeit_misses: cfg.turnForfeitMissedTurns,
    p_rematch_cooldown_seconds: cfg.rematchCooldownSeconds,
    p_lock_deadline_seconds: cfg.lockDeadlineSeconds,
    p_result_window_seconds: cfg.resultWindowSeconds,
  });
  if (!queued.success) return jsonResponse(queued);

  await callRpc(supabase, "pvp_matchmaker_service", {});
  const after = await callRpc(supabase, "pvp_snapshot_service", { p_profile_id: profileId });
  if (str(after.state) === "created" && str(after.match_id)) {
    return await lockMatch(supabase, profileId, str(after.match_id));
  }
  return jsonResponse(after.success === false ? after : { ...after, success: true, error: str(after.error) });
}

async function getMatch(supabase: SupabaseClient, profileId: string, matchId: string): Promise<Response> {
  const snapshot = await callRpc(supabase, "pvp_snapshot_service", { p_profile_id: profileId });
  if (matchId && str(snapshot.match_id) && str(snapshot.match_id) !== matchId) {
    return jsonResponse({ ...snapshot, success: false, error: "match_mismatch" });
  }
  if (str(snapshot.state) === "created" && str(snapshot.match_id)) {
    return await lockMatch(supabase, profileId, str(snapshot.match_id));
  }
  return jsonResponse(snapshot);
}

async function lockMatch(supabase: SupabaseClient, profileId: string, matchId: string): Promise<Response> {
  if (!matchId) return jsonResponse({ success: false, error: "match_required", phase: "idle" });
  const snapshot = await callRpc(supabase, "pvp_snapshot_service", { p_profile_id: profileId });
  if (str(snapshot.match_id) !== matchId) {
    return jsonResponse({ success: false, error: "not_participant", phase: "idle" });
  }
  if (str(snapshot.state) !== "created") return jsonResponse(snapshot);

  const playerA = str(snapshot.player_a);
  const playerB = str(snapshot.player_b);
  const partyA = await loadLockedParty(supabase, playerA);
  const partyB = await loadLockedParty(supabase, playerB);
  const preparedA = partyA ? prepareLockedParty(partyA) : { party: emptyParty(), error: "empty_party" };
  const preparedB = partyB ? prepareLockedParty(partyB) : { party: emptyParty(), error: "empty_party" };
  if (preparedA.error || preparedB.error) {
    await callRpc(supabase, "pvp_lock_parties_service", {
      p_match_id: matchId,
      p_actor_id: profileId,
      p_party_a: emptyParty(),
      p_party_b: emptyParty(),
    });
    return jsonResponse({ success: false, error: preparedA.error || preparedB.error, phase: "idle" });
  }
  return await callRpcResponse(supabase, "pvp_lock_parties_service", {
    p_match_id: matchId,
    p_actor_id: profileId,
    p_party_a: preparedA.party,
    p_party_b: preparedB.party,
  });
}

async function sweepCreatedMatches(supabase: SupabaseClient, profileId: string): Promise<Response> {
  const swept = await callRpc(supabase, "pvp_matchmaker_service", {});
  const created = await callRpc(supabase, "pvp_snapshot_service", { p_profile_id: profileId });
  if (str(created.state) === "created" && str(created.match_id)) {
    return await lockMatch(supabase, profileId, str(created.match_id));
  }
  return jsonResponse({ ...created, swept: swept.paired ?? 0 });
}

async function submitTurn(supabase: SupabaseClient, profileId: string, body: JsonMap): Promise<Response> {
  const matchId = str(body.match_id);
  const turnIndex = numberValue(body.turn_index);
  const seat = str(body.seat);
  const actions = parseActions(body.actions_json);
  if (!matchId || turnIndex < 1 || (seat !== "a" && seat !== "b") || !actions) {
    return jsonResponse({ success: false, error: "invalid_turn", phase: "idle" });
  }

  const snapshot = await callRpc(supabase, "pvp_snapshot_service", { p_profile_id: profileId });
  if (str(snapshot.match_id) !== matchId || str(snapshot.seat) !== seat) {
    return jsonResponse({ success: false, error: "not_participant", phase: "idle" });
  }
  const partyJson = seat === "a" ? str(snapshot.party_a_json) : str(snapshot.party_b_json);
  const party = parseParty(partyJson);
  const chainlings = (party?.chainlings ?? []).map((chainling) => ({
    moveIds: chainling.moveIds ?? [],
    moveApCosts: chainling.moveApCosts ?? [],
    fainted: chainling.fainted === true,
  }));
  const prior = priorActionsForSeat(str(snapshot.turns_json), seat, turnIndex);
  const rejection = validateTurnActions(seat, chainlings, actions, prior);
  if (rejection) return jsonResponse({ ...snapshot, success: false, error: rejection });

  return await callRpcResponse(supabase, "pvp_submit_turn_service", {
    p_match_id: matchId,
    p_actor_id: profileId,
    p_turn_index: turnIndex,
    p_action: { actions },
  });
}

async function submitResult(supabase: SupabaseClient, profileId: string, body: JsonMap): Promise<Response> {
  const matchId = str(body.match_id);
  const forensicHash = str(body.forensic_hash);
  if (!matchId || !forensicHash) return jsonResponse({ success: false, error: "hash_required", phase: "idle" });
  if (body.winner_team === undefined || body.winner_team === null || body.winner_team === "") {
    return jsonResponse({ success: false, error: "winner_illegal", phase: "idle" });
  }
  const winnerTeam = numberValue(body.winner_team);
  if (winnerTeam !== 0 && winnerTeam !== 1 && winnerTeam !== -2) {
    return jsonResponse({ success: false, error: "winner_illegal", phase: "idle" });
  }
  return await callRpcResponse(supabase, "pvp_submit_result_service", {
    p_match_id: matchId,
    p_actor_id: profileId,
    p_forensic_hash: forensicHash,
    p_winner_team: winnerTeam,
  });
}

function parseActions(value: unknown): TurnAction[] | null {
  const text = typeof value === "string" ? value : "";
  if (!text) return null;
  try {
    const parsed = JSON.parse(text) as { actions?: unknown };
    if (!parsed || !Array.isArray(parsed.actions)) return null;
    return parsed.actions.map(whitelistAction);
  } catch {
    return null;
  }
}

function whitelistAction(value: unknown): TurnAction {
  const row = value && typeof value === "object" ? value as Record<string, unknown> : {};
  const action: TurnAction = { actionType: numberValue(row.actionType) };
  if ("moveIndex" in row) action.moveIndex = numberValue(row.moveIndex);
  if ("swapTargetIndex" in row) action.swapTargetIndex = numberValue(row.swapTargetIndex);
  if ("teamIndex" in row) action.teamIndex = numberValue(row.teamIndex);
  if ("targetTeamIndex" in row) action.targetTeamIndex = numberValue(row.targetTeamIndex);
  if ("targetSlotIndex" in row) action.targetSlotIndex = numberValue(row.targetSlotIndex);
  return action;
}

function priorActionsForSeat(turnsJson: string, seat: string, turnIndex: number): TurnAction[][] {
  if (!turnsJson) return [];
  try {
    const parsed = JSON.parse(turnsJson) as {
      turns?: Array<{ turn_index?: number; seat?: string; actions?: unknown[] }>;
    };
    const turns = Array.isArray(parsed?.turns) ? parsed.turns : [];
    return turns
      .filter((turn) =>
        turn && turn.seat === seat && typeof turn.turn_index === "number" && turn.turn_index < turnIndex
      )
      .sort((left, right) => (left.turn_index ?? 0) - (right.turn_index ?? 0))
      .map((turn) => (Array.isArray(turn.actions) ? turn.actions.map(whitelistAction) : []));
  } catch {
    return [];
  }
}

function parseParty(text: string): PvpPartyWire | null {
  if (!text) return null;
  try {
    return JSON.parse(text) as PvpPartyWire;
  } catch {
    return null;
  }
}

function emptyParty(): PvpPartyWire {
  return { teamName: "", chainlings: [] };
}

function numberValue(value: unknown): number {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? Math.trunc(parsed) : 0;
}

async function rpcSnapshot(supabase: SupabaseClient, fn: string, args: JsonMap): Promise<Response> {
  return await callRpcResponse(supabase, fn, args);
}

async function callRpcResponse(supabase: SupabaseClient, fn: string, args: JsonMap): Promise<Response> {
  const payload = await callRpc(supabase, fn, args);
  const status = payload.success === false && payload.error === "profile_not_found" ? 404 : 200;
  return jsonResponse(payload, status);
}

async function callRpc(supabase: SupabaseClient, fn: string, args: JsonMap): Promise<JsonMap> {
  const { data, error } = await supabase.rpc(fn, args);
  if (error) throw error;
  if (!data || typeof data !== "object") return { success: false, error: "empty_rpc" };
  return data as JsonMap;
}
