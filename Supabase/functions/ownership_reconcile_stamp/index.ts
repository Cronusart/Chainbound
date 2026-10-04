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
  cancelRamReservationOnChain,
  ensureOwnershipRpcCluster,
  fetchAndVerifyStampReceipt,
  finalizedSignatureOutcome,
  findFinalizedSignatureForAddress,
  FIRST_STAMP_RAM_COST,
  hashMoveId,
  isOwnershipWalletAllowed,
  isSolanaSignature,
  loadOwnershipConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
  stampEffectFieldsFromOperation,
} from "../_shared/ownership_spine.ts";

const OPEN_INTENT_STATUSES = ["prepared", "submitted", "chain_confirmed", "das_pending"];

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = activeWallet ? str(activeWallet.wallet_public_key) : "";
    const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
    if (!walletAddress || (requestedWallet && requestedWallet !== walletAddress)) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }

    const config = loadOwnershipConfig();
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);

    let query = supabase.from("ownership_stamp_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).in("status", OPEN_INTENT_STATUSES);
    const requestedKey = str(body.idempotency_key);
    if (requestedKey) query = query.eq("idempotency_key", requestedKey);
    const { data: operation, error: operationError } = await query
      .order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (operationError) throw operationError;
    if (!operation) return jsonResponse({ success: true, pending: false, operation_status: "none" });
    if (str(operation.wallet_address) !== walletAddress) {
      return jsonResponse({ success: false, error: "stamp_operation_wallet_mismatch" }, 409);
    }

    const knownId = str(operation.known_id);
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
    let signature = str(body.signature);
    if (!isSolanaSignature(signature)) signature = str(operation.signature);
    let outcome = isSolanaSignature(signature) ? await finalizedSignatureOutcome(config.rpcUrl, signature) : "missing";
    let chainConfirmed = outcome === "confirmed";
    if (!chainConfirmed || !isSolanaSignature(signature)) {
      const discovered = await findFinalizedSignatureForAddress(
        config.rpcUrl,
        str(operation.stamped_asset_address),
      );
      if (discovered) {
        signature = discovered;
        outcome = "confirmed";
        chainConfirmed = true;
      }
    }
    chainConfirmed = chainConfirmed || (verified != null && isSolanaSignature(signature));

    const now = new Date().toISOString();
    if (!chainConfirmed) {
      const expiredUnsignedPrepare = str(operation.status) === "prepared" &&
        !isSolanaSignature(signature) &&
        !!operation.expires_at &&
        new Date(String(operation.expires_at)).getTime() <= Date.now();
      if (outcome === "failed" || expiredUnsignedPrepare) {
        const failureCode = outcome === "failed" ? "transaction_failed" : "unsigned_intent_expired";
        await cancelRamReservationOnChain({
          config,
          walletAddress,
          operationId: str(operation.operation_id),
        });
        const { error: releaseError } = await supabase.rpc("ram_ledger_release_operation", {
          p_operation_id: str(operation.operation_id),
          p_cluster: config.cluster,
          p_failure_code: failureCode,
        });
        if (releaseError) throw releaseError;
        const { error: failedUpdateError } = await supabase.from("ownership_stamp_operations").update({
          status: "failed",
          failure_code: failureCode,
          last_reconciled_at: now,
          updated_at: now,
        }).eq("operation_id", operation.operation_id).neq("status", "confirmed");
        if (failedUpdateError) throw failedUpdateError;
        return jsonResponse({
          success: true,
          confirmed: false,
          pending: false,
          operation_status: "failed",
          operation_id: str(operation.operation_id),
          known_instance_id: str(operation.known_id),
          idempotency_key: str(operation.idempotency_key),
          acquisition: normalizeAcquisition(str(operation.acquisition)),
          stamped_asset_address: str(operation.stamped_asset_address),
          signature,
          message: outcome === "failed"
            ? "The Stamp transaction failed. No RAM or Known move was consumed."
            : "The unsigned Stamp review expired. No RAM or Known move was consumed.",
        });
      }
      await supabase.from("ownership_stamp_operations").update({
        last_reconciled_at: now,
        updated_at: now,
      }).eq("operation_id", operation.operation_id);
      return jsonResponse({
        success: true,
        confirmed: false,
        pending: true,
        operation_status: str(operation.status),
        operation_id: str(operation.operation_id),
        known_instance_id: str(operation.known_id),
        idempotency_key: str(operation.idempotency_key),
        acquisition: normalizeAcquisition(str(operation.acquisition)),
        stamped_asset_address: str(operation.stamped_asset_address),
        signature,
        message: "The same Stamp intent is still awaiting a confirmed transaction.",
      }, 202);
    }

    const { error: chainUpdateError } = await supabase.from("ownership_stamp_operations").update({
      status: "chain_confirmed",
      signature,
      submitted_at: operation.submitted_at ?? now,
      chain_confirmed_at: operation.chain_confirmed_at ?? now,
      last_reconciled_at: now,
      updated_at: now,
    }).eq("operation_id", operation.operation_id).neq("status", "confirmed");
    if (chainUpdateError) throw chainUpdateError;
    const { error: ledgerSubmittedError } = await supabase.rpc("ram_ledger_mark_chain_submitted", {
      p_operation_id: str(operation.operation_id),
      p_cluster: config.cluster,
      p_chain_signature: signature,
    });
    if (ledgerSubmittedError) throw ledgerSubmittedError;

    if (!verified) {
      return jsonResponse({
        success: true,
        confirmed: false,
        pending: true,
        operation_status: "chain_confirmed",
        operation_id: str(operation.operation_id),
        known_instance_id: knownId,
        idempotency_key: str(operation.idempotency_key),
        acquisition: normalizeAcquisition(str(operation.acquisition)),
        stamped_asset_address: str(operation.stamped_asset_address),
        signature,
        message: "The transaction is confirmed and the on-chain Stamp receipt is still propagating.",
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
      p_idempotency_key: str(operation.idempotency_key),
      p_known_id: knownId,
      p_signature: signature,
      p_verified_asset_json: publicAsset,
      p_onchain_instance_id: String(operation.onchain_instance_id),
      p_receipt_address: str(operation.stamped_asset_address),
    });
    if (settlementError) throw settlementError;
    return jsonResponse({
      success: true,
      confirmed: true,
      pending: false,
      operation_status: "confirmed",
      operation_id: str(operation.operation_id),
      known_instance_id: knownId,
      idempotency_key: str(operation.idempotency_key),
      acquisition: normalizeAcquisition(str(operation.acquisition)),
      stamped_asset_address: str(operation.stamped_asset_address),
      idempotent: settlement?.replayed === true,
      signature: str(settlement?.signature) || signature,
      asset: settlement?.asset ?? publicAsset,
      ram_cost: FIRST_STAMP_RAM_COST,
      ram_balance: Number(settlement?.ram_balance ?? 0),
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_reconcile_stamp]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_reconcile_stamp_failed" }, 500);
  }
});
