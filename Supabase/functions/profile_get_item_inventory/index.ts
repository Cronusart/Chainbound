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
import { buildItemInventoryPayload } from "../_shared/onchain_items.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const { data: profileRow } = await supabase
      .from("player_profiles")
      .select("ram_balance")
      .eq("player_profile_id", profile.player_profile_id)
      .maybeSingle();

    const payload = await buildItemInventoryPayload(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
      Number(profileRow?.ram_balance) || 0,
    );

    if (!payload.ok) {
      return jsonResponse(
        { success: false, error: payload.error ?? "item_inventory_failed" },
        payload.status ?? 400,
      );
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      wallet_public_key: payload.wallet_public_key,
      cluster: payload.cluster,
      ram_balance: payload.ram_balance,
      stacks: payload.stacks,
    });
  } catch (error) {
    console.error("[profile_get_item_inventory]", error);
    return jsonResponse({ success: false, error: "profile_get_item_inventory_failed" }, 500);
  }
});
