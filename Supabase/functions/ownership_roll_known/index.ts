import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  authoritySecretBytes,
  deriveKnownResult,
  deriveRamOperationId,
  KNOWN_ROLL_RAM_COST,
  loadRollConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
} from "../_shared/ownership_spine.ts";
import { deriveKnownBundle, knownBundleRpcPulls, parsePullCount } from "../_shared/known_roll_bundle.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const idempotencyKey = str(body.idempotency_key);
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return jsonResponse({ success: false, error: "invalid_idempotency_key" }, 400);
  }
  const pullCount = parsePullCount(body.pull_count);
  if (pullCount === null) return jsonResponse({ success: false, error: "invalid_pull_count" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    // Known moves are profile inventory. The authenticated Supabase profile is
    // the authority here; a wallet becomes mandatory only for Stamp.
    const config = loadRollConfig();
    if (pullCount > 1) return await rollBundle(supabase, profile.player_profile_id, config, idempotencyKey, pullCount);
    const result = await deriveKnownResult(
      authoritySecretBytes(config.resultAuthority),
      profile.player_profile_id,
      idempotencyKey,
    );
    const operationId = await deriveRamOperationId(profile.player_profile_id, "known_roll", idempotencyKey);
    const { data, error } = await supabase.rpc("ownership_roll_known_profile_atomic", {
      p_operation_id: operationId,
      p_player_profile_id: profile.player_profile_id,
      p_cluster: config.cluster,
      p_idempotency_key: idempotencyKey,
      p_move_id: result.moveId,
      p_onchain_move_id: result.onchainMoveId,
      p_element: result.element,
      p_base_power: result.basePower,
      p_generator_version: config.generatorVersion,
      p_pool_hash: config.modifierPoolHash,
      p_ram_cost: KNOWN_ROLL_RAM_COST,
    });
    if (error) {
      if (error.message?.includes("insufficient_ram")) {
        return jsonResponse({ success: false, error: "insufficient_ram", ramCost: KNOWN_ROLL_RAM_COST }, 409);
      }
      if (
        error.message?.includes("known_roll_idempotency_conflict") ||
        error.message?.includes("known_roll_operation_conflict")
      ) {
        return jsonResponse({ success: false, error: "known_roll_conflict" }, 409);
      }
      throw error;
    }
    const payload = data as JsonMap;
    return jsonResponse({
      success: true,
      operationId,
      replayed: Boolean(payload.replayed),
      pullCount: 1,
      known: knownResponse(payload.known as JsonMap),
      knowns: [knownResponse(payload.known as JsonMap)],
      ramCost: Number(payload.ram_cost),
      ramBalance: Number(payload.ram_balance),
      ramBalanceVersion: Number(payload.ram_balance_version),
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_roll_known]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_roll_known_failed" }, 500);
  }
});

/**
 * One atomic RPC for the whole bundle: every pull's RAM is debited together or
 * the request fails with nothing written. Retrying the same key and pull_count
 * replays the settled bundle; a different pull_count is a conflict.
 */
async function rollBundle(
  supabase: ReturnType<typeof supabaseAdmin>,
  profileId: string,
  config: ReturnType<typeof loadRollConfig>,
  idempotencyKey: string,
  pullCount: number,
): Promise<Response> {
  const pulls = await deriveKnownBundle(
    authoritySecretBytes(config.resultAuthority),
    profileId,
    idempotencyKey,
    pullCount,
  );
  const ramCost = KNOWN_ROLL_RAM_COST * pullCount;
  const { data, error } = await supabase.rpc("ownership_roll_known_bundle_profile_atomic", {
    p_player_profile_id: profileId,
    p_cluster: config.cluster,
    p_idempotency_key: idempotencyKey,
    p_pulls: knownBundleRpcPulls(pulls),
    p_generator_version: config.generatorVersion,
    p_pool_hash: config.modifierPoolHash,
    p_ram_cost_per_pull: KNOWN_ROLL_RAM_COST,
  });
  if (error) {
    const inventoryFull = /inventory_full free_slots=(\d+)/.exec(error.message ?? "");
    if (inventoryFull) {
      return jsonResponse(
        { success: false, error: "inventory_full", freeSlots: Number(inventoryFull[1]), required: pullCount },
        409,
      );
    }
    if (error.message?.includes("insufficient_ram")) {
      return jsonResponse({ success: false, error: "insufficient_ram", ramCost, pullCount }, 409);
    }
    if (
      error.message?.includes("known_roll_bundle_conflict") ||
      error.message?.includes("known_roll_idempotency_conflict") ||
      error.message?.includes("known_roll_operation_conflict")
    ) {
      return jsonResponse({ success: false, error: "known_roll_conflict" }, 409);
    }
    throw error;
  }
  const payload = data as JsonMap;
  const knowns = (payload.knowns as JsonMap[]).map(knownResponse);
  if (knowns.length !== pullCount) throw new Error("known_roll_bundle_size_mismatch");
  return jsonResponse({
    success: true,
    operationId: pulls[0].operationId,
    operationIds: pulls.map((pull) => pull.operationId),
    bundleId: str(payload.bundle_id),
    replayed: Boolean(payload.replayed),
    pullCount,
    known: knowns[0],
    knowns,
    ramCost: Number(payload.ram_cost),
    ramBalance: Number(payload.ram_balance),
    ramBalanceVersion: Number(payload.ram_balance_version),
  });
}

function knownResponse(row: JsonMap): JsonMap {
  return {
    instanceId: str(row.known_id),
    moveId: str(row.move_id),
    modifierCount: 0,
    generatorVersion: Number(row.generator_version),
    poolHash: str(row.pool_hash),
    acquisition: normalizeAcquisition(str(row.acquisition)),
  };
}
