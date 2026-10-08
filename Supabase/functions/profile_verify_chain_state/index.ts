import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import { normalizePublicKey, resolveVerifiedWalletForProfile } from "../_shared/onchain_cosmetics.ts";
import { derivePlayerProfilePda } from "../_shared/solana_pda.ts";

const PLAYER_PROFILE_ACCOUNT = "PlayerProfile";
const PLAYER_PROFILE_DATA_LENGTH = 8 + 50;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const supabase = supabaseAdmin();
  const cluster = str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || "devnet";
  const gameProgramId = normalizePublicKey(Deno.env.get("CHAINBOUND_GAME_PROGRAM_ID"));

  try {
    const profile = await resolveAuthenticatedProfile(req, supabase, body);
    if (!profile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);
    if (!gameProgramId) {
      return jsonResponse({ success: false, status: "mismatch", error: "game_program_id_unconfigured" }, 503);
    }

    const wallet = await resolveVerifiedWalletForProfile(
      supabase,
      profile.player_profile_id,
      str(body.wallet_address) || str(body.wallet_public_key),
    );
    if (!wallet.ok || !wallet.wallet_public_key) {
      return jsonResponse(
        { success: false, error: wallet.error ?? "wallet_verification_failed" },
        wallet.status ?? 400,
      );
    }

    let pda: string;
    try {
      pda = derivePlayerProfilePda(wallet.wallet_public_key, gameProgramId);
    } catch (error) {
      if (error instanceof Error && error.message === "invalid_wallet") {
        return jsonResponse({ success: false, error: "invalid_wallet" }, 400);
      }
      throw error;
    }

    const status = await verifyAccount(pda, gameProgramId);
    await upsertChainState(supabase, {
      player_profile_id: profile.player_profile_id,
      wallet_public_key: wallet.wallet_public_key,
      cluster,
      game_program_id: gameProgramId,
      player_profile_pda: pda,
      verification_status: status.status,
      verification_error: status.error,
      metadata_json: status.metadata,
    });

    return jsonResponse({
      success: status.status === "verified",
      status: status.status,
      error: status.error ?? "",
      player_profile_id: profile.player_profile_id,
      wallet_public_key: wallet.wallet_public_key,
      cluster,
      game_program_id: gameProgramId,
      player_profile_pda: pda,
    }, status.httpStatus);
  } catch (error) {
    console.error("[profile_verify_chain_state]", error);
    return jsonResponse({ success: false, status: "rpc_unavailable", error: "profile_verify_chain_state_failed" }, 500);
  }
});

async function verifyAccount(
  playerProfilePda: string,
  expectedOwner: string,
): Promise<{ status: string; error: string | null; httpStatus: number; metadata: JsonMap }> {
  const rpcEndpoint = str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT")) || "https://api.devnet.solana.com";
  let payload: JsonMap;
  try {
    const response = await fetch(rpcEndpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: "chainbound-profile-verify",
        method: "getAccountInfo",
        params: [playerProfilePda, { encoding: "base64", commitment: "confirmed" }],
      }),
    });

    if (!response.ok) {
      return { status: "rpc_unavailable", error: `rpc_http_${response.status}`, httpStatus: 503, metadata: {} };
    }
    payload = await response.json() as JsonMap;
  } catch (error) {
    return { status: "rpc_unavailable", error: String(error), httpStatus: 503, metadata: {} };
  }

  if (payload.error) {
    return { status: "rpc_unavailable", error: JSON.stringify(payload.error), httpStatus: 503, metadata: {} };
  }

  const account = ((payload.result as JsonMap | undefined)?.value ?? null) as JsonMap | null;
  if (!account) {
    return { status: "missing_on_chain", error: "player_profile_pda_missing", httpStatus: 404, metadata: {} };
  }

  const owner = normalizePublicKey(account.owner);
  if (owner !== expectedOwner) {
    return { status: "mismatch", error: "player_profile_owner_mismatch", httpStatus: 409, metadata: { owner } };
  }

  const data = Array.isArray(account.data) ? account.data : [];
  const base64 = typeof data[0] === "string" ? data[0] : "";
  const bytes = base64 ? Uint8Array.from(atob(base64), (c) => c.charCodeAt(0)) : new Uint8Array();
  if (bytes.length < PLAYER_PROFILE_DATA_LENGTH) {
    return {
      status: "mismatch",
      error: "player_profile_data_too_short",
      httpStatus: 409,
      metadata: { data_length: bytes.length },
    };
  }

  const expectedDiscriminator = await anchorAccountDiscriminator(PLAYER_PROFILE_ACCOUNT);
  for (let i = 0; i < expectedDiscriminator.length; i++) {
    if (bytes[i] !== expectedDiscriminator[i]) {
      return {
        status: "mismatch",
        error: "player_profile_discriminator_mismatch",
        httpStatus: 409,
        metadata: { data_length: bytes.length },
      };
    }
  }

  return { status: "verified", error: null, httpStatus: 200, metadata: { data_length: bytes.length } };
}

async function anchorAccountDiscriminator(name: string): Promise<Uint8Array> {
  const bytes = new TextEncoder().encode(`account:${name}`);
  const hash = await crypto.subtle.digest("SHA-256", bytes);
  return new Uint8Array(hash).slice(0, 8);
}

async function upsertChainState(supabase: ReturnType<typeof supabaseAdmin>, row: JsonMap): Promise<void> {
  const { error } = await supabase
    .from("chainbound_profile_chain_state")
    .upsert(row, {
      onConflict: "player_profile_id,wallet_public_key,cluster,game_program_id",
    });
  if (error) throw new Error(`chain_state_upsert_failed:${error.message}`);
}
