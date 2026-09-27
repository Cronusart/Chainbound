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
import { refreshCoreCosmeticOwnershipCache, resolveVerifiedWalletForProfile } from "../_shared/onchain_cosmetics.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const wallet = await resolveVerifiedWalletForProfile(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
    );
    if (!wallet.ok || !wallet.wallet_public_key) {
      return jsonResponse(
        { success: false, error: wallet.error ?? "wallet_verification_failed" },
        wallet.status ?? 400,
      );
    }

    const refresh = await refreshCoreCosmeticOwnershipCache(
      supabase,
      profile.player_profile_id,
      wallet.wallet_public_key,
    );

    if (!refresh.ok) {
      return jsonResponse(
        { success: false, error: refresh.error ?? "cosmetic_inventory_refresh_failed" },
        refresh.status ?? 503,
      );
    }

    return jsonResponse({
      success: true,
      player_profile_id: profile.player_profile_id,
      wallet_public_key: wallet.wallet_public_key,
      cluster: str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || "devnet",
      cosmetics: refresh.records ?? [],
    });
  } catch (error) {
    console.error("[profile_get_cosmetic_inventory]", error);
    return jsonResponse({ success: false, error: "profile_get_cosmetic_inventory_failed" }, 500);
  }
});
