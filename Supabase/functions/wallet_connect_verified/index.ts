import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import * as bs58Module from "https://esm.sh/bs58@5.0.0";
import * as naclModule from "https://esm.sh/tweetnacl@1.0.3";
import {
  editorMockWalletAllowed,
  ensureRamLedgerAccount,
  getProfileById,
  isApprovedBetaWallet,
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  recordLinkEvent,
  requireAuthenticatedUser,
  requirePost,
  resolveProfileByAuthUser,
  sessionResponse,
  sha256Hex,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

const DEFAULT_PROOF_TTL_SECONDS = 15 * 60;
const EDITOR_MOCK_SIGNATURE_PREFIX = "editor_mock_signature:";

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const walletPublicKey = str(body.wallet_public_key) || str(body.wallet_address);
  const nonceId = str(body.nonce_id);
  const signedMessage = str(body.signed_message) || str(body.message);
  const signature = str(body.signature);
  const mode = normalizeMode(body.mode);

  if (!SOLANA_BASE58_RE.test(walletPublicKey)) {
    return jsonResponse({ success: false, error: "invalid_wallet_public_key" }, 400);
  }
  if (!mode) return jsonResponse({ success: false, error: "invalid_wallet_connect_mode" }, 400);
  if (!nonceId || !signedMessage || !signature) {
    return jsonResponse({ success: false, error: "missing_wallet_proof" }, 400);
  }

  const supabase = supabaseAdmin();
  const authenticated = await requireAuthenticatedUser(req);
  const authenticatedProfile = authenticated ? await resolveProfileByAuthUser(supabase, authenticated.userId) : null;
  if (!authenticated || !authenticatedProfile) {
    return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  }
  if (
    mode !== "resolve" && str(body.player_profile_id) &&
    str(body.player_profile_id) !== authenticatedProfile.player_profile_id
  ) return jsonResponse({ success: false, error: "authenticated_profile_mismatch" }, 401);
  const activePlatform = normalizePlatform(body.active_platform_build);
  const walletProvider = normalizeWalletProvider(body.wallet_provider);
  const now = new Date();
  const nowIso = now.toISOString();
  const proofTtlSeconds = positiveInt(Deno.env.get("CHAINBOUND_WALLET_PROOF_TTL_SECONDS"), DEFAULT_PROOF_TTL_SECONDS);
  const proofExpiresAt = new Date(now.getTime() + proofTtlSeconds * 1000).toISOString();

  try {
    const { data: challenge, error: challengeError } = await supabase
      .from("wallet_auth_challenges")
      .select("*")
      .eq("nonce_id", nonceId)
      .maybeSingle();

    if (challengeError) throw challengeError;
    if (!challenge) return jsonResponse({ success: false, error: "challenge_not_found" }, 404);

    const challengeStatus = str(challenge.verification_status);
    const challengeWallet = str(challenge.wallet_public_key);
    const challengeMessage = str(challenge.message);
    const requestedProfileId = authenticatedProfile.player_profile_id;
    if (str(challenge.player_profile_id) !== requestedProfileId) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_profile_mismatch");
      return jsonResponse({ success: false, error: "challenge_profile_mismatch" }, 401);
    }
    const expiresAt = new Date(str(challenge.expires_at));

    if (challengeStatus !== "issued" || challenge.consumed_at) {
      return jsonResponse({ success: false, error: "challenge_already_used" }, 409);
    }
    if (!Number.isFinite(expiresAt.getTime()) || expiresAt.getTime() < now.getTime()) {
      await markChallenge(supabase, nonceId, "expired", "challenge_expired");
      return jsonResponse({ success: false, error: "challenge_expired" }, 401);
    }
    if (challengeWallet !== walletPublicKey) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_wallet_mismatch");
      return jsonResponse({ success: false, error: "challenge_wallet_mismatch" }, 401);
    }
    if (challengeMessage !== signedMessage || str(challenge.message_hash) !== await sha256Hex(signedMessage)) {
      await markChallenge(supabase, nonceId, "rejected", "challenge_message_mismatch");
      return jsonResponse({ success: false, error: "challenge_message_mismatch" }, 401);
    }

    const verification = verifyEd25519Signature(walletPublicKey, signedMessage, signature);
    const verified = verification.verified;
    const devBypass = editorMockWalletAllowed() && isEditorMockSignature(signature);
    if (!verified && !devBypass) {
      await markChallenge(supabase, nonceId, "rejected", verification.error);
      return jsonResponse({ success: false, error: "invalid_proof" }, 401);
    }

    if (!await isApprovedBetaWallet(supabase, walletPublicKey)) {
      return jsonResponse({ success: false, error: "beta_not_allowlisted" }, 403);
    }

    const { data: consumedChallenge, error: consumeError } = await supabase
      .from("wallet_auth_challenges")
      .update({
        consumed_at: nowIso,
        verification_status: "consumed",
        verification_error: null,
        metadata_json: {
          source: "wallet_connect_verified",
          signature_verified: verified,
          dev_bypass: devBypass,
        },
      })
      .eq("nonce_id", nonceId)
      .eq("verification_status", "issued")
      .is("consumed_at", null)
      .select("nonce_id")
      .maybeSingle();
    if (consumeError) throw consumeError;
    if (!consumedChallenge) {
      return jsonResponse({ success: false, error: "challenge_already_used" }, 409);
    }

    if (mode === "resolve") {
      const { data, error } = await supabase.rpc("wallet_resolve_bound_profile_service", {
        p_wallet_address: walletPublicKey,
      });
      if (error) return jsonResponse({ success: false, error: "wallet_profile_resolution_failed" }, 409);
      const resolution = (data ?? {}) as JsonMap;
      return jsonResponse({
        success: true,
        status: str(resolution.status) || "no_profile",
        player_profile_id: str(resolution.player_profile_id) || null,
        wallet_public_key: walletPublicKey,
        wallet_proof_verified_at: nowIso,
        wallet_proof_expires_at: proofExpiresAt,
      });
    }

    const rpcName = mode === "create" ? "wallet_create_bound_profile_service" : "wallet_bind_profile_service";
    const rpcArgs = mode === "create"
      ? {
        p_auth_user_id: authenticated.userId,
        p_current_profile_id: requestedProfileId,
        p_wallet_address: walletPublicKey,
        p_wallet_provider: walletProvider,
        p_active_platform: activePlatform,
      }
      : {
        p_player_profile_id: requestedProfileId,
        p_wallet_address: walletPublicKey,
        p_wallet_provider: walletProvider,
      };
    const { data: bindingData, error: bindingError } = await supabase.rpc(rpcName, rpcArgs);
    if (bindingError) {
      console.error("[wallet_connect_verified] binding RPC failed", bindingError.code);
      return jsonResponse({ success: false, error: "wallet_binding_conflict" }, 409);
    }
    const binding = (bindingData ?? {}) as JsonMap;
    if (binding.success !== true) {
      const code = stableBindingError(str(binding.error));
      return jsonResponse({ success: false, error: code }, code === "profile_not_found" ? 404 : 409);
    }
    const resolvedProfileId = str(binding.player_profile_id);
    const resolvedProfile = await getProfileById(supabase, resolvedProfileId);
    if (!resolvedProfile) return jsonResponse({ success: false, error: "profile_not_found" }, 404);

    const ledgerCluster = str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || str(Deno.env.get("RAM_SHOP_CLUSTER"));
    try {
      await ensureRamLedgerAccount(supabase, resolvedProfileId, walletPublicKey, ledgerCluster);
    } catch (error) {
      console.warn("[wallet_connect_verified] non-fatal ledger ensure", error instanceof Error ? error.message : error);
    }

    await recordLinkEvent(
      supabase,
      resolvedProfileId,
      mode === "create" ? "wallet_profile_created" : "wallet_connect_requested",
      walletProvider,
      mode === "create" ? "created" : "success",
      {
        wallet_public_key: walletPublicKey,
        challenge_nonce_id: nonceId,
        signature_verified: verified,
        dev_bypass: devBypass,
        connect_mode: mode,
      },
    );

    const session = await sessionResponse(supabase, resolvedProfile, "wallet", activePlatform, mode === "create");
    return jsonResponse({
      ...session,
      status: mode === "create" ? "profile_created" : "profile_found",
      wallet_proof_verified_at: nowIso,
      wallet_proof_expires_at: proofExpiresAt,
      wallet_profile_restored: false,
    });
  } catch (error) {
    console.error("[wallet_connect_verified]", error);
    return jsonResponse({ success: false, error: "wallet_connect_conflict" }, 409);
  }
});

async function markChallenge(
  supabase: ReturnType<typeof supabaseAdmin>,
  nonceId: string,
  status: string,
  error: string,
): Promise<void> {
  await supabase
    .from("wallet_auth_challenges")
    .update({
      verification_status: status,
      verification_error: error,
    })
    .eq("nonce_id", nonceId);
}

type SignatureVerificationResult = {
  verified: boolean;
  error: string;
};

function normalizeMode(value: unknown): "resolve" | "link" | "create" | null {
  const mode = str(value).toLowerCase() || "link";
  return mode === "resolve" || mode === "link" || mode === "create" ? mode : null;
}

function stableBindingError(value: string): string {
  return [
      "wallet_already_bound_to_profile",
      "wallet_bound_to_other_profile",
      "authenticated_profile_mismatch",
      "profile_not_found",
      "invalid_wallet_binding",
    ].includes(value)
    ? value
    : "wallet_binding_conflict";
}

function verifyEd25519Signature(
  walletPublicKey: string,
  message: string,
  signatureText: string,
): SignatureVerificationResult {
  let publicKey: Uint8Array;
  let signature: Uint8Array;

  try {
    publicKey = bs58Decode(walletPublicKey);
  } catch {
    return { verified: false, error: "invalid_wallet_public_key_decode" };
  }

  try {
    signature = decodeSignature(signatureText);
  } catch {
    return { verified: false, error: "invalid_signature_decode" };
  }

  if (publicKey.length !== 32) {
    return { verified: false, error: `invalid_wallet_public_key_length:${publicKey.length}` };
  }
  if (signature.length !== 64) {
    return { verified: false, error: `invalid_signature_length:${signature.length}` };
  }

  const detached = naclDetachedVerify();
  if (!detached) {
    return { verified: false, error: "nacl_detached_verify_unavailable" };
  }

  try {
    const verified = detached(new TextEncoder().encode(message), signature, publicKey);
    return { verified, error: verified ? "" : "invalid_wallet_signature" };
  } catch {
    return { verified: false, error: "nacl_detached_verify_failed" };
  }
}

function decodeSignature(value: string): Uint8Array {
  try {
    const bytes = Uint8Array.from(atob(value), (c) => c.charCodeAt(0));
    if (bytes.length === 64) return bytes;
  } catch {
    // Try base58 below.
  }

  try {
    const bytes = bs58Decode(value);
    if (bytes.length === 64) return bytes;
  } catch {
    // Fall through.
  }

  throw new Error("signature decode unavailable");
}

function bs58Decode(value: string): Uint8Array {
  const moduleShape = bs58Module as unknown as {
    decode?: (input: string) => Uint8Array;
    default?: { decode?: (input: string) => Uint8Array };
  };
  const decode = moduleShape.decode ?? moduleShape.default?.decode;
  if (!decode) throw new Error("bs58 decode unavailable");
  return decode(value);
}

function naclDetachedVerify(): ((message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean) | null {
  const moduleShape = naclModule as unknown as {
    sign?: { detached?: { verify?: (message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean } };
    default?: {
      sign?: { detached?: { verify?: (message: Uint8Array, signature: Uint8Array, publicKey: Uint8Array) => boolean } };
    };
  };
  return moduleShape.sign?.detached?.verify ?? moduleShape.default?.sign?.detached?.verify ?? null;
}

function isEditorMockSignature(value: string): boolean {
  if (value.startsWith(EDITOR_MOCK_SIGNATURE_PREFIX)) return true;

  try {
    const decoded = new TextDecoder().decode(Uint8Array.from(atob(value), (c) => c.charCodeAt(0)));
    return decoded.startsWith(EDITOR_MOCK_SIGNATURE_PREFIX);
  } catch {
    return false;
  }
}

function positiveInt(value: unknown, fallback: number): number {
  const parsed = Number(str(value));
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function normalizeWalletProvider(value: unknown): string {
  const raw = str(value).toLowerCase();
  switch (raw) {
    case "phantom":
    case "solflare":
    case "seed_vault":
    case "other":
      return raw;
    default:
      return "other";
  }
}
