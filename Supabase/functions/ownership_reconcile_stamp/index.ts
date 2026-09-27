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
  fetchAndVerifyStampReceipt,
  findConfirmedSignatureForAddress,
  FIRST_STAMP_RAM_COST,
  hashMoveId,
  isSolanaSignature,
  loadOwnershipConfig,
  OwnershipConfigurationError,
  signatureIsConfirmed,
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

    const config = loadOwnershipConfig();
    const knownId = str(operation.known_id);
    const { data: knownRow, error: knownError } = await supabase.from("ownership_known_moves")
      .select("move_id,onchain_move_id").eq("known_id", knownId).single();
    if (knownError) throw knownError;
    const modifierIds = Array.isArray(operation.modifier_ids)
      ? operation.modifier_ids.map((value: unknown) => str(value))
      : [];
    const verified = await fetchAndVerifyStampReceipt({
      config,
      walletAddress,
      idempotencyKey: str(operation.idempotency_key),
      expectedKnownOnchainInstanceId: String(operation.onchain_instance_id),
      expectedMoveId: Number(knownRow?.onchain_move_id ?? hashMoveId(str(knownRow?.move_id))),
      expectedModifierIds: modifierIds,
      expectedRarity: Number(operation.rarity),
      expectedTicketAddress: str(operation.ticket_address),
      expectedStampedAssetAddress: str(operation.stamped_asset_address),
    });
    let signature = str(body.signature);
    if (!isSolanaSignature(signature)) signature = str(operation.signature);
    let chainConfirmed = isSolanaSignature(signature) && await signatureIsConfirmed(config.rpcUrl, signature);
    if (!chainConfirmed || !isSolanaSignature(signature)) {
      const discovered = await findConfirmedSignatureForAddress(
        config.rpcUrl,
        str(operation.stamped_asset_address),
      );
      if (discovered) {
        signature = discovered;
        chainConfirmed = true;
      }
    }
    chainConfirmed = chainConfirmed || (verified != null && isSolanaSignature(signature));

    const now = new Date().toISOString();
    if (!chainConfirmed) {
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

    if (!verified) {
      return jsonResponse({
        success: true,
        confirmed: false,
        pending: true,
        operation_status: "chain_confirmed",
        operation_id: str(operation.operation_id),
        known_instance_id: knownId,
        idempotency_key: str(operation.idempotency_key),
        stamped_asset_address: str(operation.stamped_asset_address),
        signature,
        message: "The transaction is confirmed and the on-chain Stamp receipt is still propagating.",
      }, 202);
    }

    const publicAsset: JsonMap = { ...verified, knownInstanceId: knownId, moveId: str(knownRow?.move_id) };
    delete publicAsset.knownOnchainInstanceId;
    delete publicAsset.ticketAddress;
    delete publicAsset.idempotencyHash;
    delete publicAsset.assetUri;
    const { data: settlement, error: settlementError } = await supabase.rpc("ownership_confirm_stamp_atomic", {
      p_player_profile_id: profile.player_profile_id,
      p_idempotency_key: str(operation.idempotency_key),
      p_known_id: knownId,
      p_signature: signature,
      p_verified_asset_json: publicAsset,
      p_onchain_instance_id: String(operation.onchain_instance_id),
      p_ram_cost: FIRST_STAMP_RAM_COST,
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
