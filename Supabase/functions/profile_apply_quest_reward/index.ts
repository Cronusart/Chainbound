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
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const rewardId = str(body.reward_id);
  if (!rewardId) {
    return jsonResponse({ success: false, error: "reward_id_required" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data, error } = await supabase.rpc("profile_apply_quest_reward_tx", {
      p_profile_id: profile.player_profile_id,
      p_reward_id: rewardId,
    });

    if (error) {
      console.error("[profile_apply_quest_reward] rpc", error);
      return jsonResponse(
        { success: false, ok: false, error: error.message || "quest_reward_failed" },
        400,
      );
    }

    const result = (data ?? {}) as JsonMap;
    if (result.ok === false) {
      return jsonResponse({
        success: false,
        ok: false,
        already_applied: false,
        error: str(result.error) || "not_applied",
        reward_id: rewardId,
        item_id: str(result.item_id),
      }, 409);
    }

    return jsonResponse({
      success: true,
      ok: true,
      already_applied: result.already_applied === true,
      player_profile_id: profile.player_profile_id,
      ram_balance: result.ram_balance ?? 0,
      ram_balance_version: result.ram_balance_version ?? 0,
      // Quest RAM counts toward the 100/UTC-day earn cap (RAM Shop R2 D3).
      requested_ram: result.requestedRam ?? 0,
      applied_ram: result.appliedRam ?? 0,
      reason: str(result.reason),
      error: "",
    });
  } catch (error) {
    console.error("[profile_apply_quest_reward]", error);
    return jsonResponse({ success: false, ok: false, error: "profile_apply_quest_reward_failed" }, 500);
  }
});
