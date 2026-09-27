import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet, JsonMap, jsonResponse, readJsonBody, requirePost, resolveAuthenticatedProfile,
  SOLANA_BASE58_RE, str, supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  authoritySecretBytes, buildStampTransaction, CHAINBOUND_PROGRAM_ID, CORE_PROGRAM_ID,
  deriveStampResult, loadOwnershipConfig, OwnershipConfigurationError, sha256Hex,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req); if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const knownId = str(body.known_instance_id); const idempotencyKey = str(body.idempotency_key);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key);
  const modifierCount = Number(body.modifier_count);
  if (!knownId || !idempotencyKey || idempotencyKey.length > 128 || !SOLANA_BASE58_RE.test(walletAddress) || ![1, 2].includes(modifierCount))
    return jsonResponse({ success: false, error: "invalid_prepare_request" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== walletAddress)
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    const config = loadOwnershipConfig();
    const { data: known, error: knownError } = await supabase.from("ownership_known_moves").select("*")
      .eq("known_id", knownId).eq("player_profile_id", profile.player_profile_id).is("consumed_at", null).maybeSingle();
    if (knownError) throw knownError;
    if (!known) return jsonResponse({ success: false, error: "known_not_found_or_consumed" }, 404);

    const { data: existing, error: existingError } = await supabase.from("ownership_stamp_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).eq("idempotency_key", idempotencyKey).maybeSingle();
    if (existingError) throw existingError;
    if (existing) {
      if (str(existing.known_id) !== knownId || Number(existing.modifier_count) !== modifierCount || str(existing.wallet_address) !== walletAddress)
        return jsonResponse({ success: false, error: "idempotency_key_conflict" }, 409);
      if (str(existing.status) === "confirmed") return jsonResponse({ success: false, error: "stamp_already_confirmed" }, 409);
    }

    const result = await deriveStampResult(authoritySecretBytes(config.resultAuthority), profile.player_profile_id, knownId, idempotencyKey, modifierCount);
    const built = await buildStampTransaction({ config, walletAddress, knownId,
      knownMoveId: Number(known.onchain_move_id), element: Number(known.element), basePower: Number(known.base_power),
      existingOnchainInstanceId: known.onchain_instance_id == null ? null : String(known.onchain_instance_id),
      idempotencyKey, modifierIds: result.modifierIds, rarity: result.rarity });
    const operation = {
      player_profile_id: profile.player_profile_id, known_id: knownId, wallet_address: walletAddress,
      idempotency_key: idempotencyKey, idempotency_hash: await sha256Hex(idempotencyKey),
      modifier_count: modifierCount, modifier_ids: result.modifierIds, rarity: result.rarity,
      generator_version: config.generatorVersion, pool_hash: config.modifierPoolHash,
      onchain_instance_id: built.onchainInstanceId.toString(), ticket_address: built.ticketAddress,
      stamped_asset_address: built.stampedAssetAddress, transaction_base64: built.transactionBase64,
      recent_blockhash: built.recentBlockhash, expires_at: built.expiresAt.toISOString(), status: "prepared",
      updated_at: new Date().toISOString(),
    };
    const { error: upsertError } = await supabase.from("ownership_stamp_operations").upsert(operation,
      { onConflict: "player_profile_id,idempotency_key" });
    if (upsertError) throw upsertError;
    return jsonResponse({ success: true, transaction_base64: built.transactionBase64,
      ticket_address: built.ticketAddress, stamped_asset_address: built.stampedAssetAddress,
      expires_at: built.expiresAt.toISOString(), review: {
        cluster: config.cluster, feePayer: walletAddress, chainboundProgram: CHAINBOUND_PROGRAM_ID,
        coreProgram: CORE_PROGRAM_ID, knownInstanceId: knownId, ramCost: config.stampRamCost,
        outputCollection: config.movesCollection.toBase58(), estimatedNetworkFeeLamports: built.estimatedNetworkFeeLamports,
        simulationSucceeded: false, simulationMessage: "Not yet simulated by the client RPC.",
      } });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (["move_vault_not_initialized", "known_onchain_instance_missing", "invalid_move_vault_account", "invalid_move_vault_layout"].includes(message))
      return jsonResponse({ success: false, error: message }, 412);
    console.error("[ownership_prepare_stamp]", message);
    return jsonResponse({ success: false, error: "ownership_prepare_stamp_failed" }, 500);
  }
});

