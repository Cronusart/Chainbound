import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  ensureRamLedgerAccount,
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  readRamBalanceSnapshot,
  requireBoundWallet,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
  WalletBindingError,
} from "../_shared/account_helpers.ts";
import {
  authoritySecretBytes,
  deriveKnownResult,
  deriveRamOperationId,
  ensureOwnershipRpcCluster,
  isOwnershipWalletAllowed,
  KNOWN_ROLL_RAM_COST,
  loadOwnershipConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
  settleKnownRollOnChain,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const idempotencyKey = str(body.idempotency_key);
  const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
  if (!idempotencyKey || idempotencyKey.length > 128 || !requestedWallet) {
    return jsonResponse({ success: false, error: "invalid_idempotency_key" }, 400);
  }
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const config = loadOwnershipConfig();
    await ensureOwnershipRpcCluster(config);
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = str(activeWallet?.wallet_public_key);
    await requireBoundWallet(supabase, profile.player_profile_id, requestedWallet);
    if (walletAddress && walletAddress !== requestedWallet) throw new WalletBindingError("wallet_profile_mismatch");
    if (!walletAddress) return jsonResponse({ success: false, error: "linked_wallet_required" }, 409);
    await ensureRamLedgerAccount(supabase, profile.player_profile_id, walletAddress, config.cluster);
    if (config.cluster === "mainnet-beta") {
      if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
        return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
      }
    }
    const result = await deriveKnownResult(
      authoritySecretBytes(config.resultAuthority),
      profile.player_profile_id,
      idempotencyKey,
    );
    const operationId = await deriveRamOperationId(profile.player_profile_id, "known_roll", idempotencyKey);
    const expiresAt = new Date(Date.now() + Math.max(config.ticketTtlSeconds, 600) * 1000);
    const { error: reservationError } = await supabase.rpc("ram_ledger_reserve_operation", {
      p_operation_id: operationId,
      p_player_profile_id: profile.player_profile_id,
      p_wallet_address: walletAddress,
      p_cluster: config.cluster,
      p_operation_kind: "known_roll",
      p_amount: KNOWN_ROLL_RAM_COST,
      p_expires_at: expiresAt.toISOString(),
      p_metadata_json: {
        idempotency_key: idempotencyKey,
        move_id: result.moveId,
        onchain_move_id: result.onchainMoveId,
        generator_version: config.generatorVersion,
        pool_hash: config.modifierPoolHash,
      },
    });
    if (reservationError) {
      if (reservationError.message?.includes("insufficient_ram")) {
        return jsonResponse({ success: false, error: "insufficient_ram", ramCost: KNOWN_ROLL_RAM_COST }, 409);
      }
      if (reservationError.message?.includes("ram_ledger_not_active")) {
        return jsonResponse({ success: false, error: "ram_reconciliation_required" }, 409);
      }
      throw reservationError;
    }

    const chain = await settleKnownRollOnChain({
      config,
      walletAddress,
      operationId,
      result,
      expiresAt,
    });
    const { error: submittedError } = await supabase.rpc("ram_ledger_mark_chain_submitted", {
      p_operation_id: operationId,
      p_cluster: config.cluster,
      p_chain_signature: chain.signature,
    });
    if (submittedError) throw submittedError;
    const { error: confirmedError } = await supabase.rpc("ram_ledger_mark_chain_confirmed", {
      p_operation_id: operationId,
      p_cluster: config.cluster,
      p_chain_signature: chain.signature,
      p_receipt_address: chain.receiptAddress,
    });
    if (confirmedError) throw confirmedError;

    const { data, error } = await supabase.rpc("ownership_settle_known_reserved", {
      p_operation_id: operationId,
      p_player_profile_id: profile.player_profile_id,
      p_cluster: config.cluster,
      p_chain_signature: chain.signature,
      p_receipt_address: chain.receiptAddress,
      p_idempotency_key: idempotencyKey,
      p_move_id: result.moveId,
      p_onchain_move_id: result.onchainMoveId,
      p_onchain_instance_id: chain.knownInstanceId,
      p_element: result.element,
      p_base_power: result.basePower,
      p_generator_version: config.generatorVersion,
      p_pool_hash: config.modifierPoolHash,
    });
    if (error) throw error;
    const payload = data as JsonMap;
    const ramSnapshot = await readRamBalanceSnapshot(supabase, profile.player_profile_id);
    return jsonResponse({
      success: true,
      operationId,
      chainSignature: chain.signature,
      receiptAddress: chain.receiptAddress,
      replayed: Boolean(payload.replayed),
      known: knownResponse(payload.known as JsonMap),
      ramCost: Number(payload.ram_cost),
      ramBalance: ramSnapshot.balance,
      ramBalanceVersion: ramSnapshot.version,
    });
  } catch (error) {
    if (error instanceof WalletBindingError) {
      return jsonResponse({ success: false, error: error.code }, error.httpStatus);
    }
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_roll_known]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_roll_known_failed" }, 500);
  }
});

function knownResponse(row: JsonMap): JsonMap {
  return {
    instanceId: str(row.known_id),
    moveId: str(row.move_id),
    modifierCount: 0,
    generatorVersion: Number(row.generator_version),
    poolHash: str(row.pool_hash),
    acquisition: normalizeAcquisition(str(row.acquisition)),
  };
}
