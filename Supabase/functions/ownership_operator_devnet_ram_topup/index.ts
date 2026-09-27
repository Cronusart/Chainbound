import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { Connection } from "npm:@solana/web3.js@1.98.4";
import {
  getActiveWallet,
  isApprovedBetaWallet,
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  buildOperatorDevnetRamTopupTransaction,
  fetchAndVerifyOperatorDevnetRamTopup,
  findConfirmedSignatureForAddress,
  loadDevnetSmokeGrantConfig,
  OwnershipConfigurationError,
  RAM_DAILY_CAP,
  sha256Hex,
  simulateOperatorDevnetRamTopup,
} from "../_shared/ownership_spine.ts";

// This recovery endpoint is deliberately single-target and temporary. It is
// not a player grant API and cannot be redirected by request-body values.
const OPERATOR_TARGET_PROFILE_ID = "38ac2f9a-5b94-4037-9bc6-fdd205632018";
const OPERATOR_TARGET_WALLET = "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj";
const OPERATOR_IDEMPOTENCY_KEY = "operator-devnet-ram-topup-20260927-seeker-1";
const OPERATOR_TOKEN_ENV = "CHAINBOUND_DEVNET_OPERATOR_TOPUP_TOKEN";

function operatorAuthorized(req: Request): boolean {
  const expected = str(Deno.env.get(OPERATOR_TOKEN_ENV));
  const actual = str(req.headers.get("x-chainbound-operator-token"));
  return expected.length >= 32 && actual.length === expected.length && actual === expected;
}

function requestedMode(body: JsonMap): "review" | "execute" | "reconcile" | null {
  const mode = str(body.mode).toLowerCase();
  return mode === "review" || mode === "execute" || mode === "reconcile" ? mode : null;
}

async function settle(
  supabase: ReturnType<typeof supabaseAdmin>,
  operationId: string,
  signature: string,
  chainBalanceBefore: number,
  verified: {
    balanceAfter: number;
    dailyRamEarned: number;
    lifetimeRamEarned: bigint;
    utcDay: bigint;
  },
): Promise<JsonMap> {
  const { data, error } = await supabase.rpc("ownership_reconcile_operator_devnet_ram_topup_atomic", {
    p_player_profile_id: OPERATOR_TARGET_PROFILE_ID,
    p_operation_id: operationId,
    p_signature: signature,
    p_chain_balance_before: chainBalanceBefore,
    p_balance_after: verified.balanceAfter,
    p_daily_ram_earned: verified.dailyRamEarned,
    p_lifetime_ram_earned: verified.lifetimeRamEarned.toString(),
    p_utc_day: verified.utcDay.toString(),
  });
  if (!error) return (data ?? {}) as JsonMap;

  // The migration is the normal atomic boundary. The linked project's database
  // pooler has intermittently failed authentication during this recovery, so a
  // narrowly idempotent fallback is retained for this one fixed operator target.
  // It can only advance the server mirror to the just-verified chain value;
  // retries recover a completed mirror write without sending another grant.
  if (error.code !== "PGRST202") {
    throw new Error(`operator_topup_settlement_failed:${error.message}`);
  }
  const { data: current, error: currentError } = await supabase
    .from("player_profiles")
    .select("ram_balance")
    .eq("player_profile_id", OPERATOR_TARGET_PROFILE_ID)
    .maybeSingle();
  if (currentError || !current) throw new Error("operator_topup_profile_not_found");
  const currentBalance = Number((current as JsonMap).ram_balance);
  if (currentBalance > verified.balanceAfter) throw new Error("operator_topup_server_ram_exceeds_verified_chain");
  if (currentBalance < verified.balanceAfter) {
    const { data: updatedProfile, error: profileError } = await supabase
      .from("player_profiles")
      .update({ ram_balance: verified.balanceAfter })
      .eq("player_profile_id", OPERATOR_TARGET_PROFILE_ID)
      .lte("ram_balance", chainBalanceBefore)
      .select("ram_balance")
      .maybeSingle();
    if (profileError) throw profileError;
    if (!updatedProfile || Number((updatedProfile as JsonMap).ram_balance) !== verified.balanceAfter) {
      throw new Error("operator_topup_server_ram_changed_during_settlement");
    }
  }
  const { data: confirmedOperation, error: operationError } = await supabase
    .from("ownership_ram_grant_operations")
    .update({
      status: "confirmed",
      signature,
      balance_after: verified.balanceAfter,
      daily_ram_earned: verified.dailyRamEarned,
      lifetime_ram_earned: verified.lifetimeRamEarned.toString(),
      utc_day: verified.utcDay.toString(),
      confirmed_at: new Date().toISOString(),
      updated_at: new Date().toISOString(),
    })
    .eq("operation_id", operationId)
    .eq("status", "prepared")
    .select("signature")
    .maybeSingle();
  if (operationError) throw operationError;
  if (!confirmedOperation) {
    const { data: replay, error: replayError } = await supabase
      .from("ownership_ram_grant_operations")
      .select("status, signature")
      .eq("operation_id", operationId)
      .maybeSingle();
    if (replayError || !replay || str(replay.status) !== "confirmed" || str(replay.signature) !== signature) {
      throw new Error("operator_topup_operation_changed_during_settlement");
    }
    return { replayed: true, ram_balance: verified.balanceAfter, signature };
  }
  return { replayed: false, ram_balance: verified.balanceAfter, signature };
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  if (!operatorAuthorized(req)) return jsonResponse({ success: false, error: "operator_unauthorized" }, 401);
  const body = await readJsonBody<JsonMap>(req);
  const mode = body ? requestedMode(body) : null;
  if (!mode) return jsonResponse({ success: false, error: "invalid_operator_topup_request" }, 400);

  const supabase = supabaseAdmin();
  try {
    const config = loadDevnetSmokeGrantConfig();
    if (!config.smokeWallets.has(OPERATOR_TARGET_WALLET)) {
      return jsonResponse({ success: false, error: "devnet_smoke_wallet_not_allowed" }, 403);
    }
    const activeWallet = await getActiveWallet(supabase, OPERATOR_TARGET_PROFILE_ID);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== OPERATOR_TARGET_WALLET) {
      return jsonResponse({ success: false, error: "operator_target_wallet_link_mismatch" }, 409);
    }
    if (!await isApprovedBetaWallet(supabase, OPERATOR_TARGET_WALLET)) {
      return jsonResponse({ success: false, error: "operator_target_wallet_not_beta_approved" }, 403);
    }
    const { data: profile, error: profileError } = await supabase
      .from("player_profiles")
      .select("ram_balance")
      .eq("player_profile_id", OPERATOR_TARGET_PROFILE_ID)
      .maybeSingle();
    if (profileError || !profile) throw new Error("operator_target_profile_not_found");
    const serverBalance = Number((profile as JsonMap).ram_balance);
    if (!Number.isSafeInteger(serverBalance) || serverBalance < 0) {
      throw new Error("operator_invalid_server_ram_balance");
    }

    const { data: existing, error: existingError } = await supabase
      .from("ownership_ram_grant_operations")
      .select("*")
      .eq("player_profile_id", OPERATOR_TARGET_PROFILE_ID)
      .eq("idempotency_key", OPERATOR_IDEMPOTENCY_KEY)
      .maybeSingle();
    if (existingError) throw existingError;
    if (
      existing && (str(existing.wallet_address) !== OPERATOR_TARGET_WALLET || Number(existing.amount) !== RAM_DAILY_CAP)
    ) {
      return jsonResponse({ success: false, error: "operator_topup_idempotency_conflict" }, 409);
    }
    if (existing && str(existing.status) === "confirmed") {
      return jsonResponse({
        success: true,
        confirmed: true,
        idempotent: true,
        signature: str(existing.signature),
        balance_after: Number(existing.balance_after),
      });
    }

    // A prepared row can only be reconciled; it must never authorize a second
    // grant after a timeout or a worker/database interruption.
    if (existing && str(existing.status) === "prepared") {
      const verified = await fetchAndVerifyOperatorDevnetRamTopup({
        config,
        walletAddress: OPERATOR_TARGET_WALLET,
        idempotencyKey: OPERATOR_IDEMPOTENCY_KEY,
      });
      if (!verified) {
        return jsonResponse({ success: false, error: "operator_topup_prepared_or_pending" }, 409);
      }
      if (mode !== "reconcile") {
        return jsonResponse({ success: false, error: "operator_topup_requires_reconcile" }, 409);
      }
      const signature = await findConfirmedSignatureForAddress(config.rpcUrl, verified.receiptAddress);
      if (!signature) return jsonResponse({ success: false, error: "operator_topup_signature_not_found" }, 409);
      const before = verified.balanceAfter - verified.amount;
      const settled = await settle(supabase, str(existing.operation_id), signature, before, verified);
      return jsonResponse({ success: true, confirmed: true, reconciled: true, signature, ...settled });
    }

    if (mode === "reconcile") return jsonResponse({ success: false, error: "operator_topup_not_prepared" }, 409);
    const built = await buildOperatorDevnetRamTopupTransaction({
      config,
      walletAddress: OPERATOR_TARGET_WALLET,
      idempotencyKey: OPERATOR_IDEMPOTENCY_KEY,
    });
    if (serverBalance > built.balanceBefore) {
      return jsonResponse({ success: false, error: "operator_server_ram_exceeds_chain" }, 409);
    }
    const expectedBalanceAfter = built.balanceBefore + RAM_DAILY_CAP;
    await simulateOperatorDevnetRamTopup(config.rpcUrl, built.transaction);
    if (mode === "review") {
      return jsonResponse({
        success: true,
        reviewed: true,
        cluster: config.cluster,
        player_profile_id: OPERATOR_TARGET_PROFILE_ID,
        wallet: OPERATOR_TARGET_WALLET,
        player_profile_address: built.playerProfileAddress,
        receipt_address: built.receiptAddress,
        chain_ram_before: built.balanceBefore,
        server_ram_before: serverBalance,
        amount: RAM_DAILY_CAP,
        expected_chain_and_server_ram_after: expectedBalanceAfter,
        estimated_network_fee_lamports: built.estimatedNetworkFeeLamports,
        simulation_succeeded: true,
      });
    }

    const operation = {
      player_profile_id: OPERATOR_TARGET_PROFILE_ID,
      wallet_address: OPERATOR_TARGET_WALLET,
      idempotency_key: OPERATOR_IDEMPOTENCY_KEY,
      idempotency_hash: await sha256Hex(OPERATOR_IDEMPOTENCY_KEY),
      amount: RAM_DAILY_CAP,
      player_profile_address: built.playerProfileAddress,
      move_vault_address: built.moveVaultAddress,
      receipt_address: built.receiptAddress,
      transaction_base64: built.transactionBase64,
      recent_blockhash: built.recentBlockhash,
      status: "prepared",
      updated_at: new Date().toISOString(),
    };
    const { data: prepared, error: prepareError } = await supabase
      .from("ownership_ram_grant_operations")
      .insert(operation)
      .select("operation_id")
      .single();
    if (prepareError || !prepared) {
      return jsonResponse({ success: false, error: "operator_topup_already_prepared" }, 409);
    }

    const connection = new Connection(config.rpcUrl, "confirmed");
    const signature = await connection.sendRawTransaction(built.transaction.serialize(), {
      skipPreflight: false,
      preflightCommitment: "confirmed",
      maxRetries: 3,
    });
    const confirmation = await connection.confirmTransaction(
      { signature, blockhash: built.recentBlockhash, lastValidBlockHeight: built.lastValidBlockHeight },
      "confirmed",
    );
    if (confirmation.value.err) throw new Error("operator_topup_transaction_failed");
    const verified = await fetchAndVerifyOperatorDevnetRamTopup({
      config,
      walletAddress: OPERATOR_TARGET_WALLET,
      idempotencyKey: OPERATOR_IDEMPOTENCY_KEY,
      expectedBalanceAfter,
    });
    if (!verified) throw new Error("operator_topup_chain_verification_failed");
    const settled = await settle(
      supabase,
      str((prepared as JsonMap).operation_id),
      signature,
      built.balanceBefore,
      verified,
    );
    return jsonResponse({
      success: true,
      confirmed: true,
      signature,
      receipt_address: verified.receiptAddress,
      chain_ram_after: verified.balanceAfter,
      ...settled,
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    const message = error instanceof Error ? error.message : "unknown_error";
    console.error("[ownership_operator_devnet_ram_topup]", message);
    return jsonResponse({ success: false, error: "operator_devnet_ram_topup_failed" }, 500);
  }
});
