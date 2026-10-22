import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  readRamBalanceSnapshot,
  requirePost,
  resolveAuthenticatedProfile,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  cancelRamReservationOnChain,
  ensureOwnershipRpcCluster,
  fetchAndVerifyStampReceipt,
  finalizedSignatureOutcome,
  FIRST_STAMP_RAM_COST,
  hashMoveId,
  isOwnershipWalletAllowed,
  isSolanaSignature,
  loadOwnershipConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
  stampEffectFieldsFromOperation,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const knownId = str(body.known_instance_id);
  const idempotencyKey = str(body.idempotency_key);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key);
  const signature = str(body.signature);
  if (!knownId || !idempotencyKey || !SOLANA_BASE58_RE.test(walletAddress) || !isSolanaSignature(signature)) {
    return jsonResponse({ success: false, error: "invalid_confirm_request" }, 400);
  }
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== walletAddress) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    const config = loadOwnershipConfig();
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);
    const { data: operation, error } = await supabase.from("ownership_stamp_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).eq("idempotency_key", idempotencyKey).maybeSingle();
    if (error) throw error;
    if (!operation || str(operation.known_id) !== knownId || str(operation.wallet_address) !== walletAddress) {
      return jsonResponse({ success: false, error: "stamp_operation_not_found" }, 404);
    }
    if (str(operation.status) === "confirmed") {
      const confirmedAsset = operation.verified_asset_json && typeof operation.verified_asset_json === "object"
        ? {
          ...(operation.verified_asset_json as JsonMap),
          acquisition: normalizeAcquisition(str(operation.acquisition)),
        }
        : null;
      return jsonResponse({
        success: true,
        confirmed: true,
        pending: false,
        operation_id: str(operation.operation_id),
        operation_status: "confirmed",
        known_instance_id: knownId,
        idempotency_key: idempotencyKey,
        acquisition: normalizeAcquisition(str(operation.acquisition)),
        stamped_asset_address: str(operation.stamped_asset_address),
        idempotent: true,
        signature: str(operation.signature),
        asset: confirmedAsset,
      });
    }
    if (str(operation.status) === "failed") {
      return jsonResponse({ success: false, error: "stamp_operation_failed" }, 409);
    }

    // Persist the wallet-returned signature before waiting on RPC or DAS. An app
    // interruption after this point is recoverable by ownership_reconcile_stamp.
    const now = new Date().toISOString();
    const { error: submittedError } = await supabase.from("ownership_stamp_operations").update({
      status: "submitted",
      signature,
      submitted_at: operation.submitted_at ?? now,
      last_reconciled_at: now,
      updated_at: now,
    }).eq("operation_id", operation.operation_id).neq("status", "confirmed");
    if (submittedError) throw submittedError;
    const { error: ledgerSubmittedError } = await supabase.rpc("ram_ledger_mark_chain_submitted", {
      p_operation_id: str(operation.operation_id),
      p_cluster: config.cluster,
      p_chain_signature: signature,
    });
    if (ledgerSubmittedError) throw ledgerSubmittedError;

    const { data: knownRow, error: knownError } = await supabase.from("ownership_known_moves")
      .select("move_id,onchain_move_id,acquisition").eq("known_id", knownId).single();
    if (knownError) throw knownError;
    const modifierIds = Array.isArray(operation.modifier_ids)
      ? operation.modifier_ids.map((value: unknown) => str(value))
      : [];
    const verified = await fetchAndVerifyStampReceipt({
      config,
      walletAddress,
      idempotencyKey: str(operation.operation_id),
      expectedKnownOnchainInstanceId: String(operation.onchain_instance_id),
      expectedMoveId: Number(knownRow?.onchain_move_id ?? hashMoveId(str(knownRow?.move_id))),
      expectedModifierIds: modifierIds,
      expectedRarity: Number(operation.rarity),
      expectedTicketAddress: str(operation.ticket_address),
      expectedStampedAssetAddress: str(operation.stamped_asset_address),
    });

    const outcome = verified ? "confirmed" : await finalizedSignatureOutcome(config.rpcUrl, signature);
    if (outcome === "failed") {
      if (str(operation.known_source) !== "profile") {
        await cancelRamReservationOnChain({
          config,
          walletAddress,
          operationId: str(operation.operation_id),
        });
      }
      const { error: releaseError } = await supabase.rpc("ram_ledger_release_operation", {
        p_operation_id: str(operation.operation_id),
        p_cluster: config.cluster,
        p_failure_code: "transaction_failed",
      });
      if (releaseError) throw releaseError;
      const { error: failedUpdateError } = await supabase.from("ownership_stamp_operations").update({
        status: "failed",
        failure_code: "transaction_failed",
        last_reconciled_at: now,
        updated_at: now,
      }).eq("operation_id", operation.operation_id).neq("status", "confirmed");
      if (failedUpdateError) throw failedUpdateError;
      return jsonResponse({ success: false, error: "stamp_transaction_failed" }, 409);
    }

    if (!verified && outcome !== "confirmed") {
      return jsonResponse({
        success: true,
        confirmed: false,
        pending: true,
        operation_id: str(operation.operation_id),
        operation_status: "submitted",
        known_instance_id: knownId,
        idempotency_key: idempotencyKey,
        acquisition: normalizeAcquisition(str(operation.acquisition)),
        stamped_asset_address: str(operation.stamped_asset_address),
        signature,
        message: "Stamp submitted; confirmation will resume automatically.",
      }, 202);
    }

    await supabase.from("ownership_stamp_operations").update({
      status: "chain_confirmed",
      chain_confirmed_at: now,
      last_reconciled_at: now,
      updated_at: now,
    }).eq("operation_id", operation.operation_id).neq("status", "confirmed");

    if (!verified) {
      return jsonResponse({
        success: true,
        confirmed: false,
        pending: true,
        operation_id: str(operation.operation_id),
        operation_status: "chain_confirmed",
        known_instance_id: knownId,
        idempotency_key: idempotencyKey,
        acquisition: normalizeAcquisition(str(operation.acquisition)),
        stamped_asset_address: str(operation.stamped_asset_address),
        signature,
        message: "Transaction confirmed; the on-chain Stamp receipt is still propagating.",
      }, 202);
    }

    const publicAsset: JsonMap = {
      ...verified,
      ...stampEffectFieldsFromOperation(operation),
      knownInstanceId: knownId,
      moveId: str(knownRow?.move_id),
      acquisition: normalizeAcquisition(str(operation.acquisition) || str(knownRow?.acquisition)),
    };
    delete publicAsset.knownOnchainInstanceId;
    delete publicAsset.ticketAddress;
    delete publicAsset.idempotencyHash;
    delete publicAsset.assetUri;
    const { error: confirmedLedgerError } = await supabase.rpc("ram_ledger_mark_chain_confirmed", {
      p_operation_id: str(operation.operation_id),
      p_cluster: config.cluster,
      p_chain_signature: signature,
      p_receipt_address: str(operation.stamped_asset_address),
    });
    if (confirmedLedgerError) throw confirmedLedgerError;
    const { data: settlement, error: settlementError } = await supabase.rpc("ownership_confirm_stamp_reserved", {
      p_operation_id: str(operation.operation_id),
      p_player_profile_id: profile.player_profile_id,
      p_cluster: config.cluster,
      p_idempotency_key: idempotencyKey,
      p_known_id: knownId,
      p_signature: signature,
      p_verified_asset_json: publicAsset,
      p_onchain_instance_id: String(operation.onchain_instance_id),
      p_receipt_address: str(operation.stamped_asset_address),
    });
    if (settlementError) throw settlementError;
    const ramSnapshot = await readRamBalanceSnapshot(supabase, profile.player_profile_id);
    return jsonResponse({
      success: true,
      confirmed: true,
      pending: false,
      operation_id: str(operation.operation_id),
      operation_status: "confirmed",
      known_instance_id: knownId,
      idempotency_key: idempotencyKey,
      acquisition: normalizeAcquisition(str(operation.acquisition)),
      stamped_asset_address: str(operation.stamped_asset_address),
      idempotent: settlement?.replayed === true,
      signature: str(settlement?.signature) || signature,
      asset: settlement?.asset ?? publicAsset,
      ram_cost: FIRST_STAMP_RAM_COST,
      ram_balance: ramSnapshot.balance,
      ram_balance_version: ramSnapshot.version,
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_confirm_stamp]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_confirm_stamp_failed" }, 500);
  }
});
