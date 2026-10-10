import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  readRamBalanceSnapshot,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  ensureOwnershipRpcCluster,
  fetchAndVerifyStampedAsset,
  isOwnershipWalletAllowed,
  loadOwnershipConfig,
  loadRollConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
  stampEffectFieldsFromOperation,
  verifiedStampedAssetMatchesOperation,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  try {
    loadRollConfig();
    const config = loadOwnershipConfig();
    const ramSnapshot = await readRamBalanceSnapshot(supabase, profile.player_profile_id);
    const ramBalance = ramSnapshot.balance;
    const { data: knownRows, error: knownError } = await supabase.from("ownership_known_moves").select("*")
      .eq("player_profile_id", profile.player_profile_id).is("consumed_at", null).order("created_at", {
        ascending: true,
      });
    if (knownError) throw knownError;
    const known = (knownRows ?? []).map((row: JsonMap) => ({
      instanceId: str(row.known_id),
      moveId: str(row.move_id),
      modifierCount: 0,
      generatorVersion: Number(row.generator_version),
      poolHash: str(row.pool_hash),
      acquisition: normalizeAcquisition(str(row.acquisition)),
    }));
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
    const wallet = activeWallet ? str(activeWallet.wallet_public_key) : "";
    if (requestedWallet && requestedWallet !== wallet) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    if (!wallet) {
      return jsonResponse({
        success: true,
        ramBalance,
        ramBalanceVersion: ramSnapshot.version,
        stampRamCost: config.stampRamCost,
        known,
        stamped: [],
        pendingStamp: null,
      });
    }
    if (!isOwnershipWalletAllowed(config.cluster, wallet)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);

    const { data: pendingOperation, error: pendingError } = await supabase.from("ownership_stamp_operations")
      .select("operation_id,known_id,idempotency_key,status,signature,stamped_asset_address,acquisition")
      .eq("player_profile_id", profile.player_profile_id)
      .eq("wallet_address", wallet)
      .in("status", ["prepared", "submitted", "chain_confirmed", "das_pending"])
      .order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (pendingError) throw pendingError;
    const pendingStamp = pendingOperation
      ? {
        operationId: str(pendingOperation.operation_id),
        knownInstanceId: str(pendingOperation.known_id),
        idempotencyKey: str(pendingOperation.idempotency_key),
        operationStatus: str(pendingOperation.status),
        signature: str(pendingOperation.signature),
        assetAddress: str(pendingOperation.stamped_asset_address),
        acquisition: normalizeAcquisition(str(pendingOperation.acquisition)),
        message: str(pendingOperation.status) === "prepared" && !str(pendingOperation.signature)
          ? "A prepared Stamp is awaiting wallet review and signature."
          : str(pendingOperation.status) === "das_pending"
          ? "Transaction confirmed; trusted asset indexing is pending."
          : "A Stamp operation is in progress and will resume automatically.",
      }
      : null;

    const { data: operations, error: operationError } = await supabase.from("ownership_stamp_operations")
      .select("*").eq("player_profile_id", profile.player_profile_id).eq("wallet_address", wallet).eq(
        "status",
        "confirmed",
      );
    if (operationError) throw operationError;
    // Settlement stores the program-receipt-verified asset atomically. Return
    // that authority immediately and opportunistically refresh from DAS with a
    // short, concurrent budget; indexer latency must not freeze gameplay.
    const stamped = (await Promise.all((operations ?? []).map(async (operation: JsonMap) => {
      const cached = operation.verified_asset_json as JsonMap | null;
      if (!cached || !str(cached.assetAddress)) return null;
      let indexed = null;
      try {
        indexed = await Promise.race([
          fetchAndVerifyStampedAsset(config, str(operation.stamped_asset_address), wallet),
          new Promise<null>((resolve) => setTimeout(() => resolve(null), 1200)),
        ]);
      } catch (error) {
        console.warn(
          "[ownership_get_inventory] DAS refresh deferred",
          error instanceof Error ? error.message : "unknown",
        );
      }
      const verified = indexed && verifiedStampedAssetMatchesOperation(indexed, operation) ? indexed : cached;
      const item: JsonMap = {
        ...verified,
        ...stampEffectFieldsFromOperation(operation),
        knownInstanceId: str(operation.known_id),
        moveId: str(cached.moveId) || str(verified.moveId),
        acquisition: normalizeAcquisition(str(operation.acquisition) || str(cached.acquisition)),
      };
      delete item.knownOnchainInstanceId;
      return item;
    }))).filter((item): item is JsonMap => item != null);
    return jsonResponse({
      success: true,
      ramBalance,
      ramBalanceVersion: ramSnapshot.version,
      stampRamCost: config.stampRamCost,
      known,
      stamped,
      pendingStamp,
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    console.error("[ownership_get_inventory]", error instanceof Error ? error.message : "unknown_error");
    return jsonResponse({ success: false, error: "ownership_get_inventory_failed" }, 500);
  }
});
