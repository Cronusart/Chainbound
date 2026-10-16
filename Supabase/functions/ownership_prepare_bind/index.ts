import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  ensureRamLedgerAccount,
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requireBoundWallet,
  requirePost,
  resolveAuthenticatedProfile,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
  WalletBindingError,
} from "../_shared/account_helpers.ts";
import { advanceBindOperation, bindOperationResponse } from "../_shared/ownership_bind.ts";
import { inspectBindSourceTrust, inspectionDigest } from "../_shared/bindery_foundation.ts";
import {
  BIND_RAM_COST_PIN,
  buildBindTransaction,
  cancelRamReservationOnChain,
  deriveRamOperationId,
  ensureOwnershipRpcCluster,
  ensureRamReservationOnChain,
  isOwnershipWalletAllowed,
  loadBindConfig,
  OwnershipConfigurationError,
  simulateBindTransaction,
} from "../_shared/ownership_spine.ts";

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const sourceId = str(body.bind_source_id);
  const idempotencyKey = str(body.idempotency_key);
  const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
  if (
    !UUID_RE.test(sourceId) || !idempotencyKey || idempotencyKey.length > 128 ||
    !SOLANA_BASE58_RE.test(requestedWallet)
  ) return jsonResponse({ success: false, error: "invalid_bind_prepare_request" }, 400);

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  let operationId = "";
  let ledgerReserved = false;
  let config: ReturnType<typeof loadBindConfig> | null = null;

  try {
    await requireBoundWallet(supabase, profile.player_profile_id, requestedWallet);
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = activeWallet ? str(activeWallet.wallet_public_key) : "";
    if (!walletAddress || walletAddress !== requestedWallet) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    config = loadBindConfig();
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);
    await ensureRamLedgerAccount(supabase, profile.player_profile_id, walletAddress, config.cluster);

    const { data: existing, error: existingError } = await supabase.from("ownership_bind_operations").select("*")
      .eq("player_profile_id", profile.player_profile_id).eq("idempotency_key", idempotencyKey).maybeSingle();
    if (existingError) throw existingError;
    if (existing) {
      if (str(existing.source_id) !== sourceId || str(existing.wallet_address) !== walletAddress) {
        return jsonResponse({ success: false, error: "bind_operation_conflict" }, 409);
      }
      return jsonResponse(await advanceBindOperation(supabase, profile.player_profile_id, existing as JsonMap));
    }

    const { data: source, error: sourceError } = await supabase.from("ownership_bind_sources").select("*")
      .eq("source_id", sourceId).eq("player_profile_id", profile.player_profile_id)
      .eq("wallet_address", walletAddress).eq("cluster", config.cluster).maybeSingle();
    if (sourceError) throw sourceError;
    if (!source) return jsonResponse({ success: false, error: "bind_not_configured" }, 503);

    // Preparation must reuse the same live Core/DAS and Chainling trust proof
    // exposed by the read-only inspection endpoint. A database allowlist row
    // alone is never sufficient authority for a burn transaction.
    const { data: chainling, error: chainlingError } = await supabase.from("chainlings").select("id")
      .eq("id", str(source.chainling_id)).eq("player_profile_id", profile.player_profile_id).maybeSingle();
    if (chainlingError) throw chainlingError;
    const inspection = await inspectBindSourceTrust({
      config,
      source: source as JsonMap,
      profileChainlingOwned: chainling != null,
    });
    const inspectionHash = await inspectionDigest(inspection);
    const { error: inspectionError } = await supabase.rpc("ownership_record_bind_source_inspection_service", {
      p_source_id: sourceId,
      p_inspection_digest: inspectionHash,
      p_trusted: inspection.selection_trusted,
    });
    if (inspectionError) throw inspectionError;
    if (!inspection.selection_trusted) {
      return jsonResponse({
        success: false,
        error: "bind_source_untrusted",
        inspection_digest: inspectionHash,
        inspection,
      }, 412);
    }
    if (!inspection.prepare_eligible) {
      return jsonResponse({ success: false, error: "bind_source_not_available" }, 409);
    }

    operationId = await deriveRamOperationId(profile.player_profile_id, "bind", idempotencyKey);
    const expiresAt = new Date(Date.now() + config.ticketTtlSeconds * 1000);
    const { error: reserveError } = await supabase.rpc("ram_ledger_reserve_operation", {
      p_operation_id: operationId,
      p_player_profile_id: profile.player_profile_id,
      p_wallet_address: walletAddress,
      p_cluster: config.cluster,
      p_operation_kind: "bind",
      p_amount: BIND_RAM_COST_PIN,
      p_expires_at: expiresAt.toISOString(),
      p_metadata_json: {
        idempotency_key: idempotencyKey,
        source_id: sourceId,
        chainling_id: str(source.chainling_id),
        onchain_chainling_id: String(source.onchain_chainling_id),
        cover_asset_address: str(source.cover_asset_address),
        component_asset_address: str(source.component_asset_address),
        component_kind: Number(source.component_kind),
      },
    });
    if (reserveError) {
      if (reserveError.message?.includes("insufficient_ram")) {
        return jsonResponse({ success: false, error: "insufficient_ram", ram_cost: String(BIND_RAM_COST_PIN) }, 409);
      }
      if (reserveError.message?.includes("ram_ledger_not_active")) {
        return jsonResponse({ success: false, error: "ram_reconciliation_required" }, 409);
      }
      throw reserveError;
    }
    ledgerReserved = true;
    const reservation = await ensureRamReservationOnChain({
      config,
      walletAddress,
      operationId,
      amount: BIND_RAM_COST_PIN,
      kind: 2,
      expiresAt,
    });
    const built = await buildBindTransaction({
      config,
      walletAddress,
      operationId,
      onchainChainlingId: String(source.onchain_chainling_id),
      coverAssetAddress: str(source.cover_asset_address),
      componentAssetAddress: str(source.component_asset_address),
      componentKind: Number(source.component_kind),
      archiveName: str(source.archive_name),
      archiveUri: str(source.archive_uri),
      expiresAt,
    });
    await simulateBindTransaction(config.rpcUrl, built.transactionBase64);
    const { data: prepared, error: prepareError } = await supabase.rpc("ownership_prepare_bind_operation", {
      p_operation_id: operationId,
      p_player_profile_id: profile.player_profile_id,
      p_wallet_address: walletAddress,
      p_cluster: config.cluster,
      p_source_id: sourceId,
      p_idempotency_key: idempotencyKey,
      p_expires_at: expiresAt.toISOString(),
      p_reservation_address: reservation.address,
      p_chainling_address: built.chainlingAddress,
      p_archive_asset_address: built.archiveAssetAddress,
      p_receipt_address: built.receiptAddress,
      p_transaction_base64: built.transactionBase64,
      p_recent_blockhash: built.recentBlockhash,
      p_last_valid_block_height: built.lastValidBlockHeight,
      p_estimated_fee_lamports: built.estimatedNetworkFeeLamports,
    });
    if (prepareError) throw prepareError;
    ledgerReserved = false;
    return jsonResponse(bindOperationResponse(prepared as JsonMap, {
      review: {
        cluster: config.cluster,
        feePayer: walletAddress,
        chainboundProgram: config.programId.toBase58(),
        ramCost: String(BIND_RAM_COST_PIN),
        coverAsset: str(source.cover_asset_address),
        componentAsset: str(source.component_asset_address),
        componentKind: Number(source.component_kind),
        archiveAsset: built.archiveAssetAddress,
        estimatedNetworkFeeLamports: String(built.estimatedNetworkFeeLamports),
        simulationSucceeded: true,
        inspectionDigest: inspectionHash,
      },
    }));
  } catch (error) {
    if (error instanceof WalletBindingError) {
      return jsonResponse({ success: false, error: error.code }, error.httpStatus);
    }
    if (ledgerReserved && config && operationId) {
      try {
        await cancelRamReservationOnChain({ config, walletAddress: requestedWallet, operationId });
      } catch { /* an absent reservation needs no cleanup */ }
      try {
        await supabase.rpc("ram_ledger_release_operation", {
          p_operation_id: operationId,
          p_cluster: config.cluster,
          p_failure_code: "prepare_failed_before_exposure",
        });
      } catch { /* preserve the primary failure */ }
    }
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message === "bind_simulation_failed" || message === "insufficient_ram") {
      return jsonResponse({ success: false, error: message }, 412);
    }
    console.error("[ownership_prepare_bind]", message);
    return jsonResponse({ success: false, error: "ownership_prepare_bind_failed" }, 500);
  }
});
