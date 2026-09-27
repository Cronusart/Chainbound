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

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const moveId = str(body.move_id);
  if (!chainlingId) return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  if (!moveId) return jsonResponse({ success: false, error: "move_id_required" }, 400);

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const idempotencyKey = str(body.idempotency_key) ||
      buildIdempotencyKey(profile.player_profile_id, chainlingId, body);

    const { data, error } = await supabase.rpc("profile_grant_progression_ordinal_tx", {
      p_profile_id: profile.player_profile_id,
      p_chainling_id: chainlingId,
      p_move_id: moveId,
      p_source_event_id: str(body.source_event_id),
      p_idempotency_key: idempotencyKey,
      p_event_type: str(body.event_type),
      p_star_index: integer(body.star_index),
      p_mastery_cycle_index: integer(body.mastery_cycle_index),
      p_battle_seed: integer(body.battle_seed),
    });

    if (error) {
      return jsonResponse({
        success: false,
        error: normalizeRpcError(error.message),
      }, 400);
    }

    const payload = (data ?? {}) as Record<string, unknown>;
    return jsonResponse({
      success: payload.success === true,
      already_applied: payload.already_applied === true,
      move_item_id: str(payload.move_item_id),
      move_id: str(payload.move_id) || moveId,
      owned_copy_count: integer(payload.owned_copy_count),
      error: str(payload.error),
    });
  } catch (error) {
    console.error("[profile_grant_progression_ordinal]", error);
    return jsonResponse({ success: false, error: "profile_grant_progression_ordinal_failed" }, 500);
  }
});

function buildIdempotencyKey(
  profileId: string,
  chainlingId: string,
  body: JsonMap,
): string {
  const eventType = str(body.event_type) || "progression";
  const starIndex = integer(body.star_index);
  const masteryCycle = integer(body.mastery_cycle_index);
  const battleSeed = integer(body.battle_seed);
  const eventPart = eventType.toLowerCase().includes("mastery") ? `mastery_${masteryCycle}` : `star_${starIndex}`;
  return `progression_grant:seed_${battleSeed}:${profileId}:${chainlingId}:${eventPart}`;
}

function integer(value: unknown): number {
  if (typeof value === "number" && Number.isFinite(value)) return Math.trunc(value);
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return Math.trunc(parsed);
  }
  return 0;
}

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_grant_progression_ordinal_failed";
  if (normalized.includes("profile_not_found")) return "profile_not_found";
  if (normalized.includes("chainling_not_found_or_not_owned")) return "chainling_not_found_or_not_owned";
  if (normalized.includes("move_id_required")) return "move_id_required";
  if (normalized.includes("idempotency_key_required")) return "idempotency_key_required";
  if (normalized.includes("idempotency_key_conflict")) return "idempotency_key_conflict";
  return normalized;
}
