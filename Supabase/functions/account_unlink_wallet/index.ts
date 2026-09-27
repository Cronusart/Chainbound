import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  bool,
  JsonMap,
  jsonResponse,
  readJsonBody,
  recordLinkEvent,
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

  const walletPublicKey = str(body.wallet_public_key);

  if (!walletPublicKey) {
    return jsonResponse({ success: false, error: "missing_wallet" }, 400);
  }

  if (!bool(body.explicit_consent)) {
    return jsonResponse({ success: false, error: "explicit_consent_required" }, 400);
  }

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
    const playerProfileId = profile.player_profile_id;

    const { error } = await supabase
      .from("profile_wallets")
      .update({ is_active: false, unlinked_at: new Date().toISOString() })
      .eq("player_profile_id", playerProfileId)
      .eq("wallet_public_key", walletPublicKey)
      .eq("is_active", true);

    if (error) throw error;

    await recordLinkEvent(supabase, playerProfileId, "wallet_unlink_requested", "wallet", "success", {
      wallet_public_key: walletPublicKey,
    });

    return jsonResponse({
      success: true,
      player_profile_id: playerProfileId,
      account_link_state: "unlinked",
      message: "Wallet unlinked.",
    });
  } catch (error) {
    console.error("[account_unlink_wallet]", error);
    return jsonResponse({ success: false, error: "account_unlink_wallet_failed" }, 500);
  }
});
