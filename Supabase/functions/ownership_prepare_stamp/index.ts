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
  CORE_PROGRAM_ID,
  deriveStampLockForModifiers,
  deriveStampResult,
  ensureOwnershipRpcCluster,
  findConfirmedSignatureForAddress,
  isOwnershipWalletAllowed,
  loadOwnershipConfig,
  normalizeAcquisition,
  OwnershipConfigurationError,
  parseModifierEffectValues,
  sha256Hex,
  simulateStampTransaction,
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
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);
    const now = new Date().toISOString();

    // A transaction that was never signed cannot remain an open lock after its
    // ticket/blockhash window. Retire only unsigned prepared rows; submitted
    // transactions must go through reconciliation.
    const { data: expiredPrepared, error: expiredPreparedError } = await supabase
      .from("ownership_stamp_operations")
      .select("operation_id,stamped_asset_address")
      .eq("player_profile_id", profile.player_profile_id)
      .eq("status", "prepared").is("signature", null).lt("expires_at", now)
      .order("created_at", { ascending: true }).limit(1).maybeSingle();
    if (expiredPreparedError) throw expiredPreparedError;
    if (expiredPrepared) {
      const recoveredSignature = await findConfirmedSignatureForAddress(
        config.rpcUrl,
        str(expiredPrepared.stamped_asset_address),
      );
      const recovered = !!recoveredSignature;
      const { error: expiryError } = await supabase.from("ownership_stamp_operations").update({
        status: recovered ? "submitted" : "failed",
        signature: recoveredSignature || null,
        failure_code: recovered ? null : "unsigned_intent_expired",
        submitted_at: recovered ? now : null,
        last_reconciled_at: now,
        updated_at: now,
      }).eq("operation_id", expiredPrepared.operation_id).eq("status", "prepared").is("signature", null);
      if (expiryError) throw expiryError;
    }

    // Fail before result derivation, metadata publication, or intent creation.
    // The on-chain program remains the final authority and repeats this check.
    const { data: profileState, error: profileStateError } = await supabase.from("player_profiles")
      .select("ram_balance").eq("player_profile_id", profile.player_profile_id).single();
    if (profileStateError) throw profileStateError;
    const ramBalance = Math.max(0, Number(profileState?.ram_balance ?? 0));
    if (ramBalance < config.stampRamCost) {
      return jsonResponse({
        success: false,
        error: "insufficient_ram",
        ram_balance: ramBalance,
        ram_cost: config.stampRamCost,
      }, 409);
    }
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
    const derived = await deriveStampResult(
      authoritySecretBytes(config.resultAuthority),
      profile.player_profile_id,
      knownId,
      modifierCount,
      config.generatorVersion,
      config.modifierPoolHash,
    );
    const storedIds = existing && Array.isArray(existing.modifier_ids)
      ? existing.modifier_ids.map((value: unknown) => str(value))
      : derived.modifierIds;
    const storedRarity = existing ? Number(existing.rarity) : derived.rarity;
    const storedValues = existing ? parseModifierEffectValues(existing.modifier_effect_values) : null;
    const modifierEffectValues = storedValues && storedValues.length === storedIds.length
      ? storedValues
      : storedIds.join("\0") === derived.modifierIds.join("\0")
      ? derived.modifierEffectValues
      : await deriveStampLockForModifiers(
        authoritySecretBytes(config.resultAuthority),
        profile.player_profile_id,
        knownId,
        storedIds,
        config.generatorVersion,
        config.modifierPoolHash,
      );
    const result = { modifierIds: storedIds, rarity: storedRarity, modifierEffectValues };
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
    await simulateStampTransaction(config.rpcUrl, built.transactionBase64);
    const acquisition = normalizeAcquisition(str(known.acquisition));
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
      acquisition,
      modifierEffectValues: result.modifierEffectValues,
      cluster: config.cluster,
    });
    if (existing && String(existing.onchain_instance_id) !== built.onchainInstanceId.toString()) {
      return jsonResponse({ success: false, error: "stamp_reconciliation_required" }, 409);
    }

    const operation = {
      player_profile_id: profile.player_profile_id,
      known_id: knownId,
      wallet_address: walletAddress,
      idempotency_key: canonicalIdempotencyKey,
      idempotency_hash: await sha256Hex(canonicalIdempotencyKey),
      modifier_count: 1,
      modifier_ids: result.modifierIds,
      modifier_effect_values: result.modifierEffectValues,
      rarity: result.rarity,
      generator_version: config.generatorVersion,
      pool_hash: config.modifierPoolHash,
      acquisition,
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
      acquisition,
      transaction_base64: built.transactionBase64,
      ticket_address: built.ticketAddress,
      stamped_asset_address: built.stampedAssetAddress,
      expires_at: built.expiresAt.toISOString(),
      review: {
        cluster: config.cluster,
        feePayer: walletAddress,
        chainboundProgram: config.programId.toBase58(),
        coreProgram: CORE_PROGRAM_ID,
        knownInstanceId: knownId,
        ramCost: config.stampRamCost,
        outputCollection: config.movesCollection.toBase58(),
        estimatedNetworkFeeLamports: built.estimatedNetworkFeeLamports,
        simulationSucceeded: true,
        simulationMessage: "Server preflight passed. Wallet signature is required.",
        metadataPublished: true,
        metadataUri: publishedMetadata.metadataUri,
        imageUri: publishedMetadata.imageUri,
      },
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) return jsonResponse({ success: false, error: error.code }, 503);
    const message = error instanceof Error ? error.message : "unknown_error";
    if (message === "insufficient_ram") {
      return jsonResponse({ success: false, error: message }, 409);
    }
    if (message === "stamp_simulation_failed") {
      return jsonResponse({ success: false, error: message }, 412);
    }
    if (message.startsWith("metadata_") || message.startsWith("metadata")) {
      console.error("[ownership_prepare_stamp] metadata publication", message);
      return jsonResponse({ success: false, error: "stamp_metadata_publication_failed" }, 503);
    }
    if (
      [
        "move_vault_not_initialized",
        "player_profile_not_initialized",
        "player_profile_wrong_program_owner",
        "player_profile_owner_mismatch",
        "known_onchain_instance_missing",
        "invalid_move_vault_account",
        "invalid_move_vault_layout",
      ].includes(message)
    ) return jsonResponse({ success: false, error: message }, 412);
    console.error("[ownership_prepare_stamp]", message);
    return jsonResponse({ success: false, error: "ownership_prepare_stamp_failed" }, 500);
  }
});
