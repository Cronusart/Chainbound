import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requireBoundWallet,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
  WalletBindingError,
} from "../_shared/account_helpers.ts";
import { inspectBindSourceTrust, inspectionDigest } from "../_shared/bindery_foundation.ts";
import {
  ensureOwnershipRpcCluster,
  isOwnershipWalletAllowed,
  loadBindConfig,
  OwnershipConfigurationError,
} from "../_shared/ownership_spine.ts";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const sourceId = str(body.bind_source_id);
  const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
  if (!UUID_RE.test(sourceId) || !requestedWallet) {
    return jsonResponse({ success: false, error: "invalid_bind_inspection_request" }, 400);
  }

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  try {
    await requireBoundWallet(supabase, profile.player_profile_id, requestedWallet);
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = activeWallet ? str(activeWallet.wallet_public_key) : "";
    if (!walletAddress || walletAddress !== requestedWallet) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    const config = loadBindConfig();
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);

    const { data: source, error: sourceError } = await supabase.from("ownership_bind_sources").select("*")
      .eq("source_id", sourceId).eq("player_profile_id", profile.player_profile_id)
      .eq("wallet_address", walletAddress).eq("cluster", config.cluster).maybeSingle();
    if (sourceError) throw sourceError;
    if (!source) return jsonResponse({ success: false, error: "bind_source_not_found" }, 404);

    const { data: chainling, error: chainlingError } = await supabase.from("chainlings").select("id")
      .eq("id", str(source.chainling_id)).eq("player_profile_id", profile.player_profile_id).maybeSingle();
    if (chainlingError) throw chainlingError;
    const report = await inspectBindSourceTrust({
      config,
      source: source as JsonMap,
      profileChainlingOwned: chainling != null,
    });
    const digest = await inspectionDigest(report);
    const { error: recordError } = await supabase.rpc("ownership_record_bind_source_inspection_service", {
      p_source_id: sourceId,
      p_inspection_digest: digest,
      p_trusted: report.selection_trusted,
    });
    if (recordError) throw recordError;
    return jsonResponse({ success: true, inspection_digest: digest, ...report });
  } catch (error) {
    if (error instanceof WalletBindingError) {
      return jsonResponse({ success: false, error: error.code }, error.httpStatus);
    }
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    console.error("[ownership_inspect_bind_source]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_inspect_bind_source_failed" }, 500);
  }
});
