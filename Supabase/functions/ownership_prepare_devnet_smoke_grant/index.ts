import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  getActiveWallet,
  isApprovedBetaWallet,
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
  buildDevnetSmokeGrantTransaction,
  CHAINBOUND_PROGRAM_ID,
  loadDevnetSmokeGrantConfig,
  OwnershipConfigurationError,
  RAM_DAILY_CAP,
  sha256Hex,
} from "../_shared/ownership_spine.ts";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;
  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  const walletAddress = str(body.wallet_address) || str(body.wallet_public_key);
  const idempotencyKey = str(body.idempotency_key);
  if (!SOLANA_BASE58_RE.test(walletAddress) || !idempotencyKey || idempotencyKey.length > 128) {
    return jsonResponse({ success: false, error: "invalid_smoke_grant_request" }, 400);
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

    const { data: existing, error: existingError } = await supabase
      .from("ownership_ram_grant_operations")
      .select("*")
      .eq("player_profile_id", profile.player_profile_id)
      .eq("idempotency_key", idempotencyKey)
      .maybeSingle();
    if (existingError) throw existingError;
    if (existing) {
      if (str(existing.wallet_address) !== walletAddress || Number(existing.amount) !== RAM_DAILY_CAP) {
        return jsonResponse({ success: false, error: "idempotency_key_conflict" }, 409);
      }
      if (str(existing.status) === "confirmed") {
        return jsonResponse({
          success: true,
          confirmed: true,
          idempotent: true,
          receipt_address: str(existing.receipt_address),
          amount: Number(existing.amount),
        });
      }
    }

    const built = await buildDevnetSmokeGrantTransaction({ config, walletAddress, idempotencyKey });
    const operation = {
      player_profile_id: profile.player_profile_id,
      wallet_address: walletAddress,
      idempotency_key: idempotencyKey,
      idempotency_hash: await sha256Hex(idempotencyKey),
      amount: built.amount,
      player_profile_address: built.playerProfileAddress,
      move_vault_address: built.moveVaultAddress,
      receipt_address: built.receiptAddress,
      transaction_base64: built.transactionBase64,
      recent_blockhash: built.recentBlockhash,
      status: "prepared",
      updated_at: new Date().toISOString(),
    };
    const { error: upsertError } = await supabase
      .from("ownership_ram_grant_operations")
      .upsert(operation, { onConflict: "player_profile_id,idempotency_key" });
    if (upsertError) throw upsertError;

    return jsonResponse({
      success: true,
      transaction_base64: built.transactionBase64,
      receipt_address: built.receiptAddress,
      player_profile_address: built.playerProfileAddress,
      move_vault_address: built.moveVaultAddress,
      review: {
        cluster: config.cluster,
        feePayer: walletAddress,
        chainboundProgram: CHAINBOUND_PROGRAM_ID,
        amount: built.amount,
        dailyCap: RAM_DAILY_CAP,
        startingBalanceVerified: 0,
        estimatedNetworkFeeLamports: built.estimatedNetworkFeeLamports,
        simulationSucceeded: false,
        simulationMessage: "Not yet simulated by the client RPC.",
      },
    });
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) {
      return jsonResponse({ success: false, error: error.code }, 503);
    }
    const message = error instanceof Error ? error.message : "unknown_error";
    const preconditions = [
      "player_profile_not_initialized",
      "move_vault_not_initialized",
      "invalid_player_profile_account",
      "invalid_move_vault_account",
      "invalid_move_vault_layout",
      "fresh_profile_ram_not_zero",
      "move_vault_not_empty",
      "result_authority_mismatch",
      "unexpected_ram_daily_cap",
      "ownership_flags_must_remain_off",
      "smoke_ram_grant_already_exists",
    ];
    if (preconditions.includes(message)) return jsonResponse({ success: false, error: message }, 412);
    console.error("[ownership_prepare_devnet_smoke_grant]", message);
    return jsonResponse({ success: false, error: "ownership_prepare_devnet_smoke_grant_failed" }, 500);
  }
});
