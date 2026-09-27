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
  loadRollConfig,
  OwnershipConfigurationError,
  resolveEventKnownAuthority,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const moveId = str(body.move_id);
  const idempotencyKey = str(body.idempotency_key);
  if (!moveId) return jsonResponse({ success: false, error: "move_id_required" }, 400);
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return jsonResponse({ success: false, error: "invalid_idempotency_key" }, 400);
  }

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const config = loadRollConfig();
    const authority = resolveEventKnownAuthority(moveId);
    const { data, error } = await supabase.rpc("ownership_grant_event_known_atomic", {
      p_player_profile_id: profile.player_profile_id,
      p_idempotency_key: idempotencyKey,
      p_move_id: authority.moveId,
      p_onchain_move_id: authority.onchainMoveId,
      p_element: authority.element,
      p_base_power: authority.basePower,
      p_generator_version: config.generatorVersion,
      p_pool_hash: config.modifierPoolHash,
      p_source: "boss_victory",
    });
    if (error) throw error;

    const payload = data as JsonMap;
    const known = (payload.known ?? {}) as JsonMap;
    return jsonResponse({
      success: true,
      already_granted: Boolean(payload.replayed),
      move_id: str(known.move_id) || authority.moveId,
      known_instance_id: str(known.known_id),
      move_item_id: str(payload.move_item_id),
      encounter_id: str(body.encounter_id),
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message.includes("event_known_move_not_allowed")) {
      return jsonResponse({ success: false, error: "event_known_move_not_allowed" }, 400);
    }
    console.error("[ownership_grant_event_known]", message);
    return jsonResponse({ success: false, error: "ownership_grant_event_known_failed" }, 500);
  }
});
