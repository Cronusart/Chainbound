import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  authoritySecretBytes,
  buildStampTransaction,
  CHAINBOUND_PROGRAM_ID,
  CORE_PROGRAM_ID,
  deriveStampResult,
  loadOwnershipConfig,
  OwnershipConfigurationError,
  sha256Hex,
} from "../_shared/ownership_spine.ts";
import { publishStampedMetadata } from "../_shared/ownership_metadata.ts";

const OPEN_INTENT_STATUSES = ["prepared", "submitted", "chain_confirmed", "das_pending"];
const KNOWN_INTENT_STATUSES = [...OPEN_INTENT_STATUSES, "confirmed"];

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const knownId = str(body.known_instance_id);
  const requestedIdempotencyKey = str(body.idempotency_key);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key);
  const modifierCount = Number(body.modifier_count);
  // Phase 1 is intentionally one-modifier-only. The result shape is server policy,
  // not a client-selectable rarity/grinding parameter.
  if (
    !knownId || !requestedIdempotencyKey || requestedIdempotencyKey.length > 128 ||
    !SOLANA_BASE58_RE.test(walletAddress) || modifierCount !== 1
  ) return jsonResponse({ success: false, error: "invalid_prepare_request" }, 400);
  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== walletAddress) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    const config = loadOwnershipConfig();
    const { data: known, error: knownError } = await supabase.from("ownership_known_moves").select("*")
      .eq("known_id", knownId).eq("player_profile_id", profile.player_profile_id).maybeSingle();
    if (knownError) throw knownError;
    if (!known) return jsonResponse({ success: false, error: "known_not_found" }, 404);

    const { data: knownIntent, error: knownIntentError } = await supabase.from("ownership_stamp_operations")
      .select("*").eq("player_profile_id", profile.player_profile_id).eq("known_id", knownId)
      .in("status", KNOWN_INTENT_STATUSES).order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (knownIntentError) throw knownIntentError;
    if (knownIntent && str(knownIntent.status) === "confirmed") {
      return jsonResponse({
        success: false,
        error: "stamp_already_confirmed",
        operation_id: str(knownIntent.operation_id),
        stamped_asset_address: str(knownIntent.stamped_asset_address),
      }, 409);
    }
    if (known.consumed_at != null) return jsonResponse({ success: false, error: "known_already_consumed" }, 409);

    const { data: profileIntent, error: profileIntentError } = await supabase.from("ownership_stamp_operations")
      .select("operation_id,known_id,idempotency_key,status,stamped_asset_address")
      .eq("player_profile_id", profile.player_profile_id).in("status", OPEN_INTENT_STATUSES)
      .order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (profileIntentError) throw profileIntentError;
    if (profileIntent && str(profileIntent.known_id) !== knownId) {
      return jsonResponse({
        success: false,
        error: "stamp_intent_in_progress",
        operation_id: str(profileIntent.operation_id),
        known_instance_id: str(profileIntent.known_id),
        idempotency_key: str(profileIntent.idempotency_key),
        operation_status: str(profileIntent.status),
        stamped_asset_address: str(profileIntent.stamped_asset_address),
      }, 409);
    }

    const existing = knownIntent ?? null;
    if (existing) {
      if (str(existing.wallet_address) !== walletAddress || Number(existing.modifier_count) !== 1) {
        return jsonResponse({ success: false, error: "stamp_intent_conflict" }, 409);
      }
      if (str(existing.status) !== "prepared" || str(existing.signature)) {
        return jsonResponse({
          success: false,
          error: "stamp_reconciliation_required",
          operation_id: str(existing.operation_id),
          known_instance_id: knownId,
          idempotency_key: str(existing.idempotency_key),
          operation_status: str(existing.status),
          stamped_asset_address: str(existing.stamped_asset_address),
        }, 409);
      }
    }

    const canonicalIdempotencyKey = existing ? str(existing.idempotency_key) : requestedIdempotencyKey;
    const result = existing
      ? { modifierIds: existing.modifier_ids as string[], rarity: Number(existing.rarity) }
      : await deriveStampResult(
        authoritySecretBytes(config.resultAuthority),
        profile.player_profile_id,
        knownId,
        modifierCount,
        config.generatorVersion,
        config.modifierPoolHash,
      );
    const built = await buildStampTransaction({
      config,
      walletAddress,
      knownId,
      knownMoveId: Number(known.onchain_move_id),
      element: Number(known.element),
      basePower: Number(known.base_power),
      existingOnchainInstanceId: known.onchain_instance_id == null ? null : String(known.onchain_instance_id),
      idempotencyKey: canonicalIdempotencyKey,
      modifierIds: result.modifierIds,
      rarity: result.rarity,
    });
    // The Core URI is immutable once signed. Publish and publicly verify its
    // asset-specific card + JSON before the transaction is exposed to Unity.
    const publishedMetadata = await publishStampedMetadata(supabase, config.metadataUriBase, {
      assetAddress: built.stampedAssetAddress,
      walletAddress,
      knownId,
      onchainInstanceId: built.onchainInstanceId.toString(),
      moveId: str(known.move_id),
      basePower: Number(known.base_power),
      modifierIds: result.modifierIds,
      rarity: result.rarity,
      generatorVersion: config.generatorVersion,
      poolHash: config.modifierPoolHash,
    });
    if (existing && String(existing.onchain_instance_id) !== built.onchainInstanceId.toString()) {
      return jsonResponse({ success: false, error: "stamp_reconciliation_required" }, 409);
    }

    const now = new Date().toISOString();
    const operation = {
      player_profile_id: profile.player_profile_id,
      known_id: knownId,
      wallet_address: walletAddress,
      idempotency_key: canonicalIdempotencyKey,
      idempotency_hash: await sha256Hex(canonicalIdempotencyKey),
      modifier_count: 1,
      modifier_ids: result.modifierIds,
      rarity: result.rarity,
      generator_version: config.generatorVersion,
      pool_hash: config.modifierPoolHash,
      onchain_instance_id: built.onchainInstanceId.toString(),
      ticket_address: built.ticketAddress,
      stamped_asset_address: built.stampedAssetAddress,
      transaction_base64: built.transactionBase64,
      recent_blockhash: built.recentBlockhash,
      expires_at: built.expiresAt.toISOString(),
      status: "prepared",
      failure_code: null,
      updated_at: now,
    };
    let operationId = existing ? str(existing.operation_id) : "";
    if (existing) {
      const { error: updateError } = await supabase.from("ownership_stamp_operations").update(operation)
        .eq("operation_id", operationId).eq("status", "prepared");
      if (updateError) throw updateError;
    } else {
      const { data: inserted, error: insertError } = await supabase.from("ownership_stamp_operations")
        .insert(operation).select("operation_id").single();
      if (insertError) {
        if (str((insertError as { code?: string }).code) === "23505") {
          return jsonResponse({ success: false, error: "stamp_intent_race_retry" }, 409);
        }
        throw insertError;
      }
      operationId = str(inserted.operation_id);
    }

    return jsonResponse({
      success: true,
      operation_id: operationId,
      operation_status: "prepared",
      idempotency_key: canonicalIdempotencyKey,
      known_instance_id: knownId,
      transaction_base64: built.transactionBase64,
      ticket_address: built.ticketAddress,
      stamped_asset_address: built.stampedAssetAddress,
      expires_at: built.expiresAt.toISOString(),
      review: {
        cluster: config.cluster,
        feePayer: walletAddress,
        chainboundProgram: CHAINBOUND_PROGRAM_ID,
        coreProgram: CORE_PROGRAM_ID,
        knownInstanceId: knownId,
        ramCost: config.stampRamCost,
        outputCollection: config.movesCollection.toBase58(),
        estimatedNetworkFeeLamports: built.estimatedNetworkFeeLamports,
        simulationSucceeded: false,
        simulationMessage: "Not yet simulated by the client RPC.",
        metadataPublished: true,
        metadataUri: publishedMetadata.metadataUri,
        imageUri: publishedMetadata.imageUri,
      },
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message.startsWith("metadata_") || message.startsWith("metadata")) {
      console.error("[ownership_prepare_stamp] metadata publication", message);
      return jsonResponse({ success: false, error: "stamp_metadata_publication_failed" }, 503);
    }
    if (
      [
        "move_vault_not_initialized",
        "known_onchain_instance_missing",
        "invalid_move_vault_account",
        "invalid_move_vault_layout",
      ].includes(message)
    ) return jsonResponse({ success: false, error: message }, 412);
    console.error("[ownership_prepare_stamp]", message);
    return jsonResponse({ success: false, error: "ownership_prepare_stamp_failed" }, 500);
  }
});
