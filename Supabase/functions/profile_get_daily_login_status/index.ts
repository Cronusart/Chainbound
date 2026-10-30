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

// Server clock values as ISO-8601 UTC; empty when absent so Unity's JsonUtility
// reads a string either way.
function isoOrEmpty(value: unknown): string {
  const text = str(value);
  if (!text) return "";
  const parsed = new Date(text);
  return Number.isNaN(parsed.getTime()) ? "" : parsed.toISOString();
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_get_daily_login_status_tx", {
      p_profile_id: profile.player_profile_id,
    });

    if (error) {
      console.error("[profile_get_daily_login_status] rpc", error);
      return jsonResponse(
        { success: false, ok: false, error: error.message || "daily_login_status_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok === false) {
      return jsonResponse({ success: false, ok: false, error: str(result.error) || "not_available" }, 409);
    }

    return jsonResponse({
      success: true,
      ok: true,
      player_profile_id: profile.player_profile_id,
      enabled: result.enabled === true,
      unlocked: result.unlocked === true,
      can_claim: result.canClaim === true,
      utc_day: str(result.utcDay),
      server_now: isoOrEmpty(result.serverNow),
      next_claim_at: isoOrEmpty(result.nextClaimAt),
      current_streak: result.currentStreak ?? 0,
      ram_amount: result.ramAmount ?? 0,
      item_id: str(result.itemId),
      item_quantity: result.itemQuantity ?? 0,
      error: "",
    });
  } catch (error) {
    console.error("[profile_get_daily_login_status]", error);
    return jsonResponse({ success: false, ok: false, error: "profile_get_daily_login_status_failed" }, 500);
  }
});
