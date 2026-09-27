import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  JsonMap,
  jsonResponse,
  normalizePlatform,
  readJsonBody,
  requirePost,
  resolveAuthenticatedProfile,
  sha256Hex,
  SOLANA_BASE58_RE,
  str,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";

const DEFAULT_CHALLENGE_TTL_SECONDS = 5 * 60;

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const body = await readJsonBody<JsonMap>(req);
  if (!body) return jsonResponse({ success: false, error: "invalid_json_body" }, 400);

  const walletPublicKey = str(body.wallet_public_key) || str(body.wallet_address);
  if (!SOLANA_BASE58_RE.test(walletPublicKey)) {
    return jsonResponse({ success: false, error: "invalid_wallet_public_key" }, 400);
  }

  const supabase = supabaseAdmin();
  // The proof is still bound to the JWT-owned profile. Permit an existing
  // wallet on a different profile through this issuance step so the verified
  // endpoint can return the explicit restore-or-continue conflict instead of
  // leaking or silently resolving a different profile.
  const profile = await resolveAuthenticatedProfile(req, supabase, body, true);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);
  const requestedProfileId = profile.player_profile_id;
  const walletProvider = normalizeWalletProvider(body.wallet_provider);
  const activePlatform = normalizePlatform(body.active_platform_build);
  const cluster = str(body.cluster) || str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || "devnet";
  const ttlSeconds = positiveInt(
    Deno.env.get("CHAINBOUND_WALLET_CHALLENGE_TTL_SECONDS"),
    DEFAULT_CHALLENGE_TTL_SECONDS,
  );
  const issuedAt = new Date();
  const expiresAt = new Date(issuedAt.getTime() + ttlSeconds * 1000);
  const nonce = randomNonce();
  const profileLine = requestedProfileId || "new";
  const message = [
    "Chainbound wallet login",
    `Wallet: ${walletPublicKey}`,
    `Profile: ${profileLine}`,
    `Cluster: ${cluster}`,
    `Nonce: ${nonce}`,
    `Issued At: ${issuedAt.toISOString()}`,
    `Expires At: ${expiresAt.toISOString()}`,
  ].join("\n");

  try {
    const { data, error } = await supabase
      .from("wallet_auth_challenges")
      .insert({
        player_profile_id: requestedProfileId || null,
        wallet_public_key: walletPublicKey,
        wallet_provider: walletProvider,
        cluster,
        nonce_hash: await sha256Hex(nonce),
        message_hash: await sha256Hex(message),
        message,
        active_platform_build: activePlatform,
        client_version: str(body.client_version) || null,
        device_id_hash: str(body.device_id) ? await sha256Hex(`device:${str(body.device_id)}`) : null,
        expires_at: expiresAt.toISOString(),
        metadata_json: {
          source: "wallet_auth_challenge",
          proof_model: "ed25519_message_signature",
        },
      })
      .select("nonce_id")
      .single();

    if (error) throw error;

    return jsonResponse({
      success: true,
      nonce_id: data.nonce_id,
      wallet_public_key: walletPublicKey,
      wallet_provider: walletProvider,
      cluster,
      message,
      issued_at: issuedAt.toISOString(),
      expires_at: expiresAt.toISOString(),
    });
  } catch (error) {
    console.error("[wallet_auth_challenge]", error);
    return jsonResponse({ success: false, error: "wallet_auth_challenge_failed" }, 500);
  }
});

function randomNonce(): string {
  const bytes = new Uint8Array(24);
  crypto.getRandomValues(bytes);
  let raw = "";
  for (const byte of bytes) raw += String.fromCharCode(byte);
  return btoa(raw).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
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
