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
import { finalizeCoreItemConsume } from "../_shared/onchain_items.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const finalized = await finalizeCoreItemConsume(
      supabase,
      profile.player_profile_id,
      str(body.consume_intent_id),
      str(body.tx_signature),
    );

    if (!finalized.ok) {
      return jsonResponse(
        { success: false, error: finalized.error ?? "finalize_failed" },
        finalized.status ?? 400,
      );
    }

    return jsonResponse({
      success: true,
      consume_intent_id: finalized.consume_intent_id,
      tx_signature: finalized.tx_signature,
    });
  } catch (error) {
    console.error("[profile_finalize_core_item_consume]", error);
    return jsonResponse({ success: false, error: "profile_finalize_core_item_consume_failed" }, 500);
  }
});
