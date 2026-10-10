import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { JsonMap, readRamBalanceSnapshot, str } from "./account_helpers.ts";
import {
  BIND_RAM_COST_PIN,
  cancelRamReservationOnChain,
  fetchAndVerifyBindReceipt,
  finalizedBlockHeight,
  findFinalizedSignatureForAddress,
  isSolanaSignature,
  loadBindConfig,
  submittedLegacyTransactionOutcome,
} from "./ownership_spine.ts";

export function bindOperationResponse(operation: JsonMap, extra: JsonMap = {}): JsonMap {
  const status = str(operation.status);
  return {
    success: status !== "failed",
    pending: status === "prepared" || status === "submitted" || status === "chain_confirmed",
    operation_id: str(operation.operation_id),
    operation_status: status,
    idempotency_key: str(operation.idempotency_key),
    source_id: str(operation.source_id),
    chainling_id: str(operation.chainling_id),
    onchain_chainling_id: String(operation.onchain_chainling_id ?? ""),
    wallet: str(operation.wallet_address),
    cluster: str(operation.cluster),
    ram_cost: String(BIND_RAM_COST_PIN),
    cover_asset_address: str(operation.cover_asset_address),
    component_asset_address: str(operation.component_asset_address),
    component_kind: Number(operation.component_kind),
    archive_name: str(operation.archive_name),
    archive_uri: str(operation.archive_uri),
    reservation_address: str(operation.reservation_address),
    chainling_address: str(operation.chainling_address),
    archive_asset_address: str(operation.archive_asset_address),
    receipt_address: str(operation.receipt_address),
    transaction_base64: str(operation.transaction_base64),
    recent_blockhash: str(operation.recent_blockhash),
    last_valid_block_height: String(operation.last_valid_block_height ?? ""),
    estimated_fee_lamports: String(operation.estimated_fee_lamports ?? "0"),
    expires_at: operation.expires_at ?? null,
    signature: str(operation.signature),
    failure_code: str(operation.failure_code),
    ...extra,
  };
}

export async function advanceBindOperation(
  supabase: SupabaseClient,
  playerProfileId: string,
  operation: JsonMap,
  suppliedSignature = "",
): Promise<JsonMap> {
  const config = loadBindConfig();
  if (str(operation.cluster) !== config.cluster) throw new Error("bind_cluster_mismatch");
  if (str(operation.status) === "settled" || str(operation.status) === "failed") {
    const ramSnapshot = await readRamBalanceSnapshot(supabase, playerProfileId);
    return bindOperationResponse(operation, {
      confirmed: str(operation.status) === "settled",
      ram_balance: ramSnapshot.balance,
      ram_balance_version: ramSnapshot.version,
    });
  }

  const verified = await fetchAndVerifyBindReceipt({
    config,
    walletAddress: str(operation.wallet_address),
    operationId: str(operation.operation_id),
    onchainChainlingId: String(operation.onchain_chainling_id),
    coverAssetAddress: str(operation.cover_asset_address),
    componentAssetAddress: str(operation.component_asset_address),
    componentKind: Number(operation.component_kind),
    expectedArchiveAssetAddress: str(operation.archive_asset_address),
    expectedReceiptAddress: str(operation.receipt_address),
  });

  let signature = isSolanaSignature(suppliedSignature) ? suppliedSignature : str(operation.signature);
  if (verified) {
    const discovered = await findFinalizedSignatureForAddress(config.rpcUrl, verified.receiptAddress);
    if (!isSolanaSignature(discovered)) throw new Error("bind_signature_missing");
    signature = discovered;
  }

  if (!verified && isSolanaSignature(signature)) {
    const { error: submittedError } = await supabase.rpc("ownership_mark_bind_submitted", {
      p_operation_id: str(operation.operation_id),
      p_player_profile_id: playerProfileId,
      p_cluster: config.cluster,
      p_signature: signature,
    });
    if (submittedError) throw submittedError;
  }

  if (!verified) {
    const outcome = isSolanaSignature(signature)
      ? await submittedLegacyTransactionOutcome(config.rpcUrl, signature, str(operation.transaction_base64))
      : "missing";
    const blockHeight = await finalizedBlockHeight(config.rpcUrl);
    const conclusivelyExpired = !isSolanaSignature(signature) &&
      blockHeight > Number(operation.last_valid_block_height);
    if (outcome === "mismatch") throw new Error("bind_signature_transaction_mismatch");
    if (outcome === "failed" || conclusivelyExpired) {
      await cancelRamReservationOnChain({
        config,
        walletAddress: str(operation.wallet_address),
        operationId: str(operation.operation_id),
      });
      const failureCode = outcome === "failed" ? "transaction_failed" : "unsigned_blockhash_expired";
      const { data: released, error: releaseError } = await supabase.rpc("ownership_release_bind_operation", {
        p_operation_id: str(operation.operation_id),
        p_player_profile_id: playerProfileId,
        p_cluster: config.cluster,
        p_failure_code: failureCode,
      });
      if (releaseError) throw releaseError;
      return bindOperationResponse(released as JsonMap, { confirmed: false, pending: false });
    }
    return bindOperationResponse({ ...operation, signature }, {
      confirmed: false,
      pending: true,
      message: "Bind remains recoverable while finality or its receipt is pending.",
    });
  }

  const { error: confirmedError } = await supabase.rpc("ram_ledger_mark_chain_confirmed", {
    p_operation_id: str(operation.operation_id),
    p_cluster: config.cluster,
    p_chain_signature: signature,
    p_receipt_address: verified.receiptAddress,
  });
  if (confirmedError) throw confirmedError;
  const { data: settlement, error: settlementError } = await supabase.rpc("ownership_settle_bind_reserved", {
    p_operation_id: str(operation.operation_id),
    p_player_profile_id: playerProfileId,
    p_cluster: config.cluster,
    p_signature: signature,
    p_receipt_address: verified.receiptAddress,
    p_archive_asset_address: verified.archiveAssetAddress,
  });
  if (settlementError) throw settlementError;
  const ramSnapshot = await readRamBalanceSnapshot(supabase, playerProfileId);
  return bindOperationResponse({ ...operation, status: "settled", signature }, {
    confirmed: true,
    pending: false,
    ram_balance: ramSnapshot.balance,
    ram_balance_version: ramSnapshot.version,
    binding: (settlement as JsonMap)?.binding ?? verified,
  });
}
