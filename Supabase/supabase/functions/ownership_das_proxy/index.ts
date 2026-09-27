import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  DasProxyConfigurationError,
  DasProxyRequestError,
  DasUpstreamError,
  forwardDasRequest,
  HELIUS_DAS_URL_ENV,
  prepareDasRequest,
  requireHeliusDevnetUrl,
} from "../_shared/ownership_das_proxy.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    let linkedWallet = "";
    if (str(body.method) === "getAssetsByOwner") {
      const wallet = await getActiveWallet(supabase, profile.player_profile_id);
      linkedWallet = str(wallet?.wallet_public_key);
    }

    const trustedMovesCollection = str(Deno.env.get("CHAINBOUND_CORE_MOVES_COLLECTION"));
    const prepared = prepareDasRequest(body, trustedMovesCollection, linkedWallet);
    const endpoint = requireHeliusDevnetUrl(Deno.env.get(HELIUS_DAS_URL_ENV));
    const result = await forwardDasRequest(endpoint, prepared);
    return jsonResponse({ success: true, result });
  } catch (error) {
    if (error instanceof DasProxyRequestError) {
      return jsonResponse({ success: false, error: error.code }, error.status);
    }
    if (error instanceof DasProxyConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    if (error instanceof DasUpstreamError) {
      console.error("[ownership_das_proxy] das_upstream");
      return jsonResponse({ success: false, error: "das_upstream" }, 502);
    }
    console.error("[ownership_das_proxy] ownership_das_proxy_failed");
    return jsonResponse({ success: false, error: "ownership_das_proxy_failed" }, 500);
  }
});
