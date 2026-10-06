import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import {
  jsonResponse,
  requireAuthenticatedUser,
  requirePost,
  resolveProfileByAuthUser,
  supabaseAdmin,
} from "../_shared/account_helpers.ts";
import {
  forwardSolanaRpcRequest,
  MAX_RPC_BODY_BYTES,
  prepareSolanaRpcRequest,
  resolveSolanaRpcEndpoint,
  SolanaRpcConfigurationError,
  SolanaRpcRequestError,
  SolanaRpcUpstreamError,
} from "../_shared/solana_rpc_proxy.ts";

const RATE_WINDOW_SECONDS = 10;
const PER_USER_LIMIT = 20;
const GLOBAL_LIMIT = 50;

function rpcJsonResponse(body: Record<string, unknown>, status = 200, extraHeaders?: HeadersInit): Response {
  const headers = new Headers(extraHeaders);
  headers.set("Content-Type", "application/json");
  headers.set("Access-Control-Allow-Origin", "*");
  return new Response(JSON.stringify(body), { status, headers });
}

serve(async (req: Request) => {
  const early = requirePost(req);
  if (early) return early;

  const declaredLength = Number(req.headers.get("content-length") ?? "0");
  if (Number.isFinite(declaredLength) && declaredLength > MAX_RPC_BODY_BYTES) {
    return jsonResponse({ success: false, error: "rpc_body_too_large" }, 413);
  }

  const authenticated = await requireAuthenticatedUser(req);
  if (!authenticated) return jsonResponse({ success: false, error: "authentication_required" }, 401);

  const supabase = supabaseAdmin();
  const profile = await resolveProfileByAuthUser(supabase, authenticated.userId);
  if (!profile) return jsonResponse({ success: false, error: "authenticated_profile_not_found" }, 401);

  let rawBody = "";
  try {
    rawBody = await req.text();
  } catch {
    return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  }
  if (new TextEncoder().encode(rawBody).byteLength > MAX_RPC_BODY_BYTES) {
    return jsonResponse({ success: false, error: "rpc_body_too_large" }, 413);
  }

  let request;
  try {
    request = prepareSolanaRpcRequest(JSON.parse(rawBody));
  } catch (error) {
    if (error instanceof SolanaRpcRequestError) {
      return jsonResponse({ success: false, error: error.code }, error.status);
    }
    return jsonResponse({ success: false, error: "invalid_json_body" }, 400);
  }

  const { data: quotaRows, error: quotaError } = await supabase.rpc("solana_rpc_consume_rate_limit", {
    p_auth_user_id: authenticated.userId,
    p_user_limit: PER_USER_LIMIT,
    p_global_limit: GLOBAL_LIMIT,
    p_window_seconds: RATE_WINDOW_SECONDS,
  });
  const quota = Array.isArray(quotaRows) ? quotaRows[0] : quotaRows;
  if (quotaError || !quota) {
    console.error("[solana_rpc_proxy] rate_limit_unavailable");
    return jsonResponse({ success: false, error: "rpc_proxy_unavailable" }, 503);
  }
  if (quota.allowed !== true) {
    const retryAfter = Math.max(1, Number(quota.retry_after_seconds) || RATE_WINDOW_SECONDS);
    return rpcJsonResponse(
      { success: false, error: "rpc_rate_limited" },
      429,
      { "Retry-After": String(retryAfter) },
    );
  }

  try {
    // Read the credential only after the caller and quota have been accepted.
    const endpoint = resolveSolanaRpcEndpoint(
      (name) => Deno.env.get(name),
      Deno.env.get("CHAINBOUND_SOLANA_CLUSTER"),
    );
    const response = await forwardSolanaRpcRequest(endpoint, request);
    return rpcJsonResponse(response);
  } catch (error) {
    if (error instanceof SolanaRpcConfigurationError) {
      console.error("[solana_rpc_proxy] configuration_unavailable");
      return jsonResponse({ success: false, error: "rpc_proxy_unavailable" }, 503);
    }
    if (error instanceof SolanaRpcUpstreamError) {
      console.error("[solana_rpc_proxy] upstream_unavailable");
      return jsonResponse({ success: false, error: "rpc_upstream_unavailable" }, 502);
    }
    console.error("[solana_rpc_proxy] request_failed");
    return jsonResponse({ success: false, error: "rpc_proxy_failed" }, 500);
  }
});
