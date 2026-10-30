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
    // The JWT profile is the claimant. The gift amount and item come from the
    // server settings row, never from the request.
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_claim_daily_login_tx", {
      p_profile_id: profile.player_profile_id,
    });

    if (error) {
      console.error("[profile_claim_daily_login] rpc", error);
      return jsonResponse(
        { success: false, ok: false, error: error.message || "daily_login_claim_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok === false) {
      // stack_full, not_unlocked, disabled, cluster_unavailable.
      return jsonResponse({
        success: false,
        ok: false,
        already_claimed: false,
        error: str(result.error) || "not_claimed",
        item_id: str(result.item_id),
      }, 409);
    }

    return jsonResponse({
      success: true,
      ok: true,
      already_claimed: result.already_claimed === true,
      player_profile_id: profile.player_profile_id,
      utc_day: str(result.utc_day),
      server_now: isoOrEmpty(result.server_now),
      next_claim_at: isoOrEmpty(result.next_claim_at),
      ram_balance: result.ram_balance ?? 0,
      ram_balance_version: result.ram_balance_version ?? 0,
      ram_amount: result.ram_amount ?? 0,
      item_id: str(result.item_id),
      item_quantity: result.item_quantity ?? 0,
      streak_after: result.streak_after ?? 0,
      error: "",
    });
  } catch (error) {
    console.error("[profile_claim_daily_login]", error);
    return jsonResponse({ success: false, ok: false, error: "profile_claim_daily_login_failed" }, 500);
  }
});
