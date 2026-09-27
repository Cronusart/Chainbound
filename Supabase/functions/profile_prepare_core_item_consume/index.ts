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
import { prepareCoreItemConsume } from "../_shared/onchain_items.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const prepared = await prepareCoreItemConsume(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
      str(body.item_id),
      str(body.asset_id),
      str(body.reason),
    );

    if (!prepared.ok) {
      return jsonResponse(
        { success: false, error: prepared.error ?? "prepare_failed" },
        prepared.status ?? 400,
      );
    }

    return jsonResponse({
      success: true,
      consume_intent_id: prepared.consume_intent_id,
      item_id: prepared.item_id,
      asset_id: prepared.asset_id,
      unsigned_tx_base64: prepared.unsigned_tx_base64,
      cluster: prepared.cluster,
      action_summary: prepared.action_summary,
      estimated_fee_lamports: prepared.estimated_fee_lamports,
    });
  } catch (error) {
    console.error("[profile_prepare_core_item_consume]", error);
    return jsonResponse({ success: false, error: "profile_prepare_core_item_consume_failed" }, 500);
  }
});
