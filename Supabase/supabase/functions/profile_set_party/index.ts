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

const ROSTER_SIZE = 3;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const rawIds = body["party_chainling_ids"];
  if (!Array.isArray(rawIds)) {
    return jsonResponse({
      success: false,
      error: "party_chainling_ids_must_be_array",
    }, 400);
  }

  const normalized: string[] = [];
  const seen = new Set<string>();

  for (const entry of rawIds) {
    const id = str(entry);
    if (!id) continue;
    if (seen.has(id)) {
      return jsonResponse({ success: false, error: "duplicate_chainling_id" }, 400);
    }
    seen.add(id);
    normalized.push(id);
  }

  if (normalized.length === 0 || normalized.length > ROSTER_SIZE) {
    return jsonResponse({
      success: false,
      error: "party_must_have_1_to_3_chainlings",
      max_slots: ROSTER_SIZE,
    }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const pid = profile.player_profile_id;

    const { data: ownedRows, error: ownedErr } = await supabase
      .from("chainlings")
      .select("id")
      .eq("player_profile_id", pid)
      .in("id", normalized);

    if (ownedErr) throw ownedErr;

    const owned = ((ownedRows ?? []) as Array<{ id: string }>).map((r) => r.id).filter(Boolean);
    if (owned.length !== normalized.length) {
      return jsonResponse({
        success: false,
        error: "unknown_or_foreign_chainling_id",
      }, 403);
    }

    const clearRes = await supabase
      .from("chainlings")
      // Passive recovery is party-scoped. Reset its anchor whenever membership
      // changes so bench time cannot be converted into recovery on promotion.
      .update({ is_party_member: false, party_slot: null, passive_recovery_at: new Date().toISOString() })
      .eq("player_profile_id", pid);

    if (clearRes.error) throw clearRes.error;

    for (let slot = 0; slot < normalized.length; slot++) {
      const id = normalized[slot];
      const up = await supabase
        .from("chainlings")
        .update({ is_party_member: true, party_slot: slot, passive_recovery_at: new Date().toISOString() })
        .eq("id", id)
        .eq("player_profile_id", pid);

      if (up.error) throw up.error;
    }

    return jsonResponse({
      success: true,
      player_profile_id: pid,
      party_chainling_ids: normalized,
    });
  } catch (error) {
    console.error("[profile_set_party]", error);
    return jsonResponse({ success: false, error: "profile_set_party_failed" }, 500);
  }
});
