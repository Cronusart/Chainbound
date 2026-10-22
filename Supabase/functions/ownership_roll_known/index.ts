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

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const idempotencyKey = str(body.idempotency_key);
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return jsonResponse({ success: false, error: "invalid_idempotency_key" }, 400);
  }
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    // Known moves are profile inventory. The authenticated Supabase profile is
    // the authority here; a wallet becomes mandatory only for Stamp.
    const config = loadRollConfig();
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
      known: knownResponse(payload.known as JsonMap),
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
