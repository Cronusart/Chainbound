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

  const speciesId = str(body.species_id);
  if (!speciesId) {
    return jsonResponse({ success: false, error: "species_id_required" }, 400);
  }

  const idempotencyKey = str(body.idempotency_key);
  if (!idempotencyKey) {
    return jsonResponse({ success: false, error: "idempotency_key_required" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const { data, error } = await supabase.rpc("profile_capture_wild_tx", {
      p_profile_id: profile.player_profile_id,
      p_species_id: speciesId,
      p_idempotency_key: idempotencyKey,
      p_ordinal_move_id: str(body.ordinal_move_id) || str(body.move_id),
      p_current_hp: integer(body.current_hp, 1),
      p_max_hp: integer(body.max_hp, 1),
      p_base_capture_rate: integer(body.base_capture_rate, 45),
      p_status_modifier: number(body.status_modifier, 1),
      p_device_modifier: number(body.device_modifier, 1),
      p_favor_score: integer(body.favor_score, 0),
      p_stars: integer(body.stars, 0),
      p_level: integer(body.level, 1),
      p_ivs: parseIvs(body.ivs),
      p_entangled: body.entangled === true || body.entangled === "true",
      p_mutated: body.mutated === true || body.mutated === "true",
      p_visual_variant_id: str(body.visual_variant_id) || "base",
      p_entangled_type: str(body.entangled_type) || "none",
      p_mutation_id: str(body.mutation_id) || "none",
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
      reason: str(payload.reason),
      probability: number(payload.probability, 0),
      roll: number(payload.roll, 0),
      species_id: str(payload.species_id) || speciesId,
      chainling_id: str(payload.chainling_id),
      is_party_member: payload.is_party_member === true,
      party_slot: payload.party_slot == null ? null : integer(payload.party_slot, 0),
      move_item_id: str(payload.move_item_id),
      move_id: str(payload.move_id),
      error: str(payload.error),
      player_profile_id: profile.player_profile_id,
    });
  } catch (error) {
    console.error("[profile_capture_wild]", error);
    return jsonResponse({ success: false, error: "profile_capture_wild_failed" }, 500);
  }
});

function integer(value: unknown, fallback = 0): number {
  if (typeof value === "number" && Number.isFinite(value)) return Math.trunc(value);
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return Math.trunc(parsed);
  }
  return fallback;
}

function number(value: unknown, fallback = 0): number {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
  }
  return fallback;
}

function parseIvs(value: unknown): number[] {
  if (Array.isArray(value)) {
    const ivs = value.slice(0, 6).map((entry) => integer(entry, 0));
    while (ivs.length < 6) ivs.push(0);
    return ivs;
  }
  return [0, 0, 0, 0, 0, 0];
}

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_capture_wild_failed";
  if (normalized.includes("profile_not_found")) return "profile_not_found";
  if (normalized.includes("species_id_required")) return "species_id_required";
  if (normalized.includes("idempotency_key_required")) return "idempotency_key_required";
  return normalized;
}
