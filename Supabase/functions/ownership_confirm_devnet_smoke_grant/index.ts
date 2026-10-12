import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  isApprovedBetaWallet,
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
  fetchAndVerifyRamGrant,
  isSolanaSignature,
  loadDevnetSmokeGrantConfig,
  OwnershipConfigurationError,
  RAM_DAILY_CAP,
  signatureIsConfirmed,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key);
  const idempotencyKey = str(body.idempotency_key);
  const signature = str(body.signature);
  if (
    !SOLANA_BASE58_RE.test(walletAddress) || !idempotencyKey || idempotencyKey.length > 128 ||
    !isSolanaSignature(signature)
  ) {
    return jsonResponse({ success: false, error: "invalid_smoke_grant_confirm_request" }, 400);
  }

  const supabase = supabaseAdmin();
  const profile = await resolveAuthenticatedProfile(req, supabase, body);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  try {
    const activeWallet = await getActiveWallet(supabase, profile.player_profile_id);
    if (!activeWallet || str(activeWallet.wallet_public_key) !== walletAddress) {
      return jsonResponse({ success: false, error: "wallet_not_linked_to_profile" }, 403);
    }
    if (!await isApprovedBetaWallet(supabase, walletAddress)) {
      return jsonResponse({ success: false, error: "wallet_not_beta_approved" }, 403);
    }
    const config = loadDevnetSmokeGrantConfig();
    if (!config.smokeWallets.has(walletAddress)) {
      return jsonResponse({ success: false, error: "devnet_smoke_wallet_not_allowed" }, 403);
    }

    const { data: operation, error } = await supabase
      .from("ownership_ram_grant_operations")
      .select("*")
      .eq("player_profile_id", profile.player_profile_id)
      .eq("idempotency_key", idempotencyKey)
      .maybeSingle();
    if (error) throw error;
    if (!operation || str(operation.wallet_address) !== walletAddress || Number(operation.amount) !== RAM_DAILY_CAP) {
      return jsonResponse({ success: false, error: "smoke_grant_operation_not_found" }, 404);
    }
    if (str(operation.status) === "confirmed") {
      const ramSnapshot = await readRamBalanceSnapshot(supabase, profile.player_profile_id);
      return jsonResponse({
        success: true,
        confirmed: true,
        idempotent: true,
        signature: str(operation.signature),
        receipt_address: str(operation.receipt_address),
        amount: Number(operation.amount),
        balance_after: ramSnapshot.balance,
        ram_balance_version: ramSnapshot.version,
      });
    }

    if (!await signatureIsConfirmed(config.rpcUrl, signature)) {
      return jsonResponse({ success: false, error: "transaction_not_confirmed" }, 409);
    }
    const verified = await fetchAndVerifyRamGrant({ config, walletAddress, idempotencyKey });
    if (!verified || verified.receiptAddress !== str(operation.receipt_address) || verified.amount !== RAM_DAILY_CAP) {
      return jsonResponse({ success: false, error: "ram_grant_not_verified" }, 409);
    }

    const { data: settlement, error: settlementError } = await supabase.rpc(
      "ownership_confirm_devnet_smoke_grant_atomic",
      {
        p_player_profile_id: profile.player_profile_id,
        p_operation_id: operation.operation_id,
        p_signature: signature,
        p_balance_after: verified.balanceAfter,
        p_daily_ram_earned: verified.dailyRamEarned,
        p_lifetime_ram_earned: verified.lifetimeRamEarned.toString(),
        p_utc_day: verified.utcDay.toString(),
      },
    );
    if (settlementError) {
      const stableErrors = [
        "invalid_verified_ram_grant_values",
        "smoke_grant_operation_not_found",
        "smoke_grant_operation_not_prepared",
        "unexpected_verified_ram_balance",
        "server_ram_balance_not_fresh",
      ];
      const stable = stableErrors.find((value) => settlementError.message?.includes(value));
      if (stable) return jsonResponse({ success: false, error: stable }, 409);
      throw settlementError;
    }
    const settled = (settlement ?? {}) as JsonMap;
    const ramSnapshot = await readRamBalanceSnapshot(supabase, profile.player_profile_id);

    return jsonResponse({
      success: true,
      confirmed: true,
      idempotent: Boolean(settled.replayed),
      signature,
      receipt_address: verified.receiptAddress,
      amount: verified.amount,
      balance_after: ramSnapshot.balance,
      ram_balance_version: ramSnapshot.version,
      daily_ram_earned: verified.dailyRamEarned,
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    console.error(
      "[ownership_confirm_devnet_smoke_grant]",
      error instanceof Error ? error.message : "unknown_error",
    );
    return jsonResponse({ success: false, error: "ownership_confirm_devnet_smoke_grant_failed" }, 500);
  }
});
