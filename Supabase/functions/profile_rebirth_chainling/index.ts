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
  debitReservedRamOnChain,
  deriveRamOperationId,
  ensureOwnershipRpcCluster,
  ensureRamReservationOnChain,
  isOwnershipWalletAllowed,
  loadOwnershipConfig,
  OwnershipConfigurationError,
} from "../_shared/ownership_spine.ts";
import { getRebirthRamCost, PROGRESSION_CONFIG } from "../_shared/vending_config.ts";

const REBIRTH_RAM_COSTS = PROGRESSION_CONFIG.rebirthRamCosts;
const STAT_INDEX: Record<string, number> = {
  max_hp: 0,
  melee_atk: 1,
  melee_def: 2,
  ranged_atk: 3,
  ranged_def: 4,
  speed: 5,
};

interface ChainlingProgressionRow {
  rebirth_count: number | null;
}

interface ExistingOperationRow {
  amount: number | string;
  metadata_json: Record<string, unknown> | null;
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ error: "Invalid JSON body" }, 400);

  const chainlingId = str(body.chainling_id);
  const stat = str(body.stat).toLowerCase();
  const statIndex = STAT_INDEX[stat];
  const idempotencyKey = str(body.idempotency_key);

  if (!chainlingId) {
    return jsonResponse({ success: false, error: "chainling_id_required" }, 400);
  }

  if (!Number.isInteger(statIndex)) {
    return jsonResponse({ success: false, error: "invalid_rebirth_stat" }, 400);
  }
  if (!idempotencyKey || idempotencyKey.length > 128) {
    return jsonResponse({ success: false, error: "idempotency_key_required" }, 400);
  }

  const supabase = supabaseAdmin();

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) {
      return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    }

    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    const walletAddress = str(activeWallet?.wallet_public_key);
    if (!walletAddress) return jsonResponse({ success: false, error: "linked_wallet_required" }, 409);
    const requestedWallet = str(body.wallet_address) || str(body.wallet_public_key);
    if (requestedWallet && requestedWallet !== walletAddress) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    const config = loadOwnershipConfig();
    if (!isOwnershipWalletAllowed(config.cluster, walletAddress)) {
      return jsonResponse({ success: false, error: "mainnet_smoke_wallet_not_allowed" }, 403);
    }
    await ensureOwnershipRpcCluster(config);

    const operationId = await deriveRamOperationId(profile.player_profile_id, "rebirth", idempotencyKey);
    const { data: existingOperation, error: operationError } = await supabase
      .from("ram_ledger_operations")
      .select("amount,metadata_json")
      .eq("operation_id", operationId)
      .eq("cluster", config.cluster)
      .maybeSingle();
    if (operationError) throw operationError;

    let cost: number;
    if (existingOperation) {
      const existing = existingOperation as ExistingOperationRow;
      const existingMetadata = existing.metadata_json ?? {};
      if (
        str(existingMetadata.idempotency_key) !== idempotencyKey ||
        str(existingMetadata.chainling_id) !== chainlingId ||
        Number(existingMetadata.stat_index) !== statIndex
      ) {
        return jsonResponse({ success: false, error: "operation_id_conflict" }, 409);
      }
      cost = Number(existing.amount);
      if (!Number.isSafeInteger(cost) || cost <= 0) {
        throw new Error("invalid persisted rebirth amount");
      }
    } else {
      const { data: chainling, error: chainlingError } = await supabase
        .from("chainlings")
        .select("rebirth_count")
        .eq("id", chainlingId)
        .eq("player_profile_id", profile.player_profile_id)
        .maybeSingle();

      if (chainlingError) throw chainlingError;
      if (!chainling) {
        return jsonResponse({ success: false, error: "chainling_not_found_or_not_owned" }, 404);
      }

      const rebirthCount = Math.max(0, Number((chainling as ChainlingProgressionRow).rebirth_count ?? 0));
      if (rebirthCount >= REBIRTH_RAM_COSTS.length) {
        return jsonResponse({ success: false, error: "rebirth_max_reached" }, 400);
      }
      cost = getRebirthRamCost(rebirthCount);
    }

    const expiresAt = new Date(Date.now() + 15 * 60 * 1000);
    const metadata = {
      idempotency_key: idempotencyKey,
      chainling_id: chainlingId,
      stat_index: statIndex,
    };
    const { error: reserveError } = await supabase.rpc("profile_prepare_rebirth_operation", {
      p_operation_id: operationId,
      p_profile_id: profile.player_profile_id,
      p_wallet_address: walletAddress,
      p_cluster: config.cluster,
      p_chainling_id: chainlingId,
      p_stat_index: statIndex,
      p_cost: cost,
      p_expires_at: expiresAt.toISOString(),
      p_metadata_json: metadata,
    });
    if (reserveError) {
      return jsonResponse({ success: false, error: normalizeRpcError(reserveError.message) }, 409);
    }
    await ensureRamReservationOnChain({
      config,
      walletAddress,
      operationId,
      amount: cost,
      kind: 3,
      expiresAt,
    });
    const debit = await debitReservedRamOnChain({
      config,
      walletAddress,
      operationId,
      amount: cost,
      kind: 3,
    });
    const { error: submittedError } = await supabase.rpc("ram_ledger_mark_chain_submitted", {
      p_operation_id: operationId,
      p_cluster: config.cluster,
      p_chain_signature: debit.signature,
    });
    if (submittedError) throw submittedError;
    const { error: confirmedError } = await supabase.rpc("ram_ledger_mark_chain_confirmed", {
      p_operation_id: operationId,
      p_cluster: config.cluster,
      p_chain_signature: debit.signature,
      p_receipt_address: debit.receiptAddress,
    });
    if (confirmedError) throw confirmedError;
    const { data, error } = await supabase.rpc("profile_rebirth_chainling_reserved_tx", {
      p_operation_id: operationId,
      p_profile_id: profile.player_profile_id,
      p_cluster: config.cluster,
      p_chainling_id: chainlingId,
      p_stat_index: statIndex,
      p_cost: cost,
      p_chain_signature: debit.signature,
      p_receipt_address: debit.receiptAddress,
    });

    if (error) {
      return jsonResponse({
        success: false,
        error: normalizeRpcError(error.message),
      }, 400);
    }

    const payload = (data ?? {}) as Record<string, unknown>;
    return jsonResponse({
      success: true,
      operation_id: operationId,
      player_profile_id: profile.player_profile_id,
      ram_balance: Number(payload.ram_balance ?? 0),
      chainling: payload.chainling ?? {},
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    console.error("[profile_rebirth_chainling]", error);
    return jsonResponse({ success: false, error: "profile_rebirth_chainling_failed" }, 500);
  }
});

function normalizeRpcError(message: string): string {
  const normalized = (message ?? "").trim();
  if (!normalized) return "profile_rebirth_chainling_failed";
  if (normalized.includes("chainling_not_found_or_not_owned")) return "chainling_not_found_or_not_owned";
  if (normalized.includes("rebirth_requires_star_5")) return "rebirth_requires_star_5";
  if (normalized.includes("rebirth_max_reached")) return "rebirth_max_reached";
  if (normalized.includes("insufficient_ram")) return "insufficient_ram";
  if (normalized.includes("invalid_stat")) return "invalid_rebirth_stat";
  return normalized;
}
