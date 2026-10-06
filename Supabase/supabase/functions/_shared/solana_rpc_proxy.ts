export const SOLANA_RPC_ENDPOINT_ENV = "CHAINBOUND_SOLANA_RPC_ENDPOINT";
export const SOLANA_RPC_DEVNET_ENDPOINT_ENV = "CHAINBOUND_SOLANA_RPC_DEVNET_ENDPOINT";
export const SOLANA_RPC_MAINNET_ENDPOINT_ENV = "CHAINBOUND_SOLANA_RPC_MAINNET_ENDPOINT";
export const HELIUS_DAS_URL_ENV = "HELIUS_DAS_URL";
export const MAX_RPC_BODY_BYTES = 128 * 1024;

const PUBLIC_KEY_RE = /^[1-9A-HJ-NP-Za-km-z]{32,44}$/;
const SIGNATURE_RE = /^[1-9A-HJ-NP-Za-km-z]{80,90}$/;
const BASE64_RE = /^[A-Za-z0-9+/]+={0,2}$/;

export interface SolanaRpcRequest {
  jsonrpc: "2.0";
  id: string | number | null;
  method: string;
  params: unknown[];
}

export class SolanaRpcRequestError extends Error {
  constructor(public readonly code: string, public readonly status = 400) {
    super(code);
  }
}

export class SolanaRpcConfigurationError extends Error {
  constructor(public readonly code: string) {
    super(code);
  }
}

export class SolanaRpcUpstreamError extends Error {
  constructor() {
    super("solana_rpc_upstream");
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function requirePublicKey(value: unknown): string {
  const key = typeof value === "string" ? value.trim() : "";
  if (!PUBLIC_KEY_RE.test(key)) throw new SolanaRpcRequestError("invalid_public_key");
  return key;
}

function requireSignature(value: unknown): string {
  const signature = typeof value === "string" ? value.trim() : "";
  if (!SIGNATURE_RE.test(signature)) throw new SolanaRpcRequestError("invalid_signature");
  return signature;
}

function requireSlot(value: unknown): number {
  if (!Number.isSafeInteger(value) || Number(value) <= 0) {
    throw new SolanaRpcRequestError("invalid_slot");
  }
  return Number(value);
}

function normalizeId(value: unknown): string | number | null {
  if (value === null || value === undefined) return null;
  if (typeof value === "number" && Number.isSafeInteger(value)) return value;
  if (typeof value === "string" && value.length <= 64) return value;
  throw new SolanaRpcRequestError("invalid_rpc_id");
}

export function prepareSolanaRpcRequest(body: unknown): SolanaRpcRequest {
  const request = asRecord(body);
  if (request.jsonrpc !== "2.0") throw new SolanaRpcRequestError("invalid_jsonrpc_version");

  const method = typeof request.method === "string" ? request.method.trim() : "";
  const input = Array.isArray(request.params) ? request.params : [];
  const id = normalizeId(request.id);
  let params: unknown[];

  switch (method) {
    case "getGenesisHash":
      params = [];
      break;

    case "getAccountInfo":
      params = [
        requirePublicKey(input[0]),
        { encoding: "base64", commitment: "confirmed" },
      ];
      break;

    case "getMultipleAccounts": {
      const keys = Array.isArray(input[0]) ? input[0] : [];
      if (keys.length < 1 || keys.length > 100) {
        throw new SolanaRpcRequestError("invalid_account_batch");
      }
      params = [
        keys.map(requirePublicKey),
        { encoding: "base64", commitment: "confirmed" },
      ];
      break;
    }

    case "getLatestBlockhash":
      params = [{ commitment: "finalized" }];
      break;

    case "getSignatureStatuses": {
      const signatures = Array.isArray(input[0]) ? input[0] : [];
      if (signatures.length < 1 || signatures.length > 16) {
        throw new SolanaRpcRequestError("invalid_signature_batch");
      }
      params = [
        signatures.map(requireSignature),
        { searchTransactionHistory: true },
      ];
      break;
    }

    case "simulateTransaction": {
      const transaction = typeof input[0] === "string" ? input[0].trim() : "";
      if (transaction.length < 1 || transaction.length > 16_384 || !BASE64_RE.test(transaction)) {
        throw new SolanaRpcRequestError("invalid_transaction_payload");
      }
      params = [
        transaction,
        {
          encoding: "base64",
          commitment: "confirmed",
          sigVerify: false,
          replaceRecentBlockhash: false,
        },
      ];
      break;
    }

    case "getSlot":
      params = [{ commitment: "finalized" }];
      break;

    case "getBlockTime":
      params = [requireSlot(input[0])];
      break;

    default:
      throw new SolanaRpcRequestError("rpc_method_not_allowed");
  }

  return { jsonrpc: "2.0", id, method, params };
}

export function requireSolanaRpcEndpoint(
  value: string | undefined,
  cluster: string | undefined,
): string {
  const normalizedCluster = (cluster ?? "").trim().toLowerCase();
  const expectedHost = normalizedCluster === "devnet"
    ? "devnet.helius-rpc.com"
    : normalizedCluster === "mainnet-beta"
    ? "mainnet.helius-rpc.com"
    : "";
  if (!expectedHost) throw new SolanaRpcConfigurationError("solana_cluster_not_configured");

  let endpoint: URL;
  try {
    endpoint = new URL((value ?? "").trim());
  } catch {
    throw new SolanaRpcConfigurationError("solana_rpc_not_configured");
  }

  if (
    endpoint.protocol !== "https:" ||
    endpoint.hostname.toLowerCase() !== expectedHost ||
    !endpoint.searchParams.get("api-key")
  ) {
    throw new SolanaRpcConfigurationError("solana_rpc_not_configured");
  }

  return endpoint.toString();
}

export function resolveSolanaRpcEndpoint(
  readEnvironment: (name: string) => string | undefined,
  cluster: string | undefined,
): string {
  const normalizedCluster = (cluster ?? "").trim().toLowerCase();
  const clusterSpecificEnvironment = normalizedCluster === "devnet"
    ? SOLANA_RPC_DEVNET_ENDPOINT_ENV
    : normalizedCluster === "mainnet-beta"
    ? SOLANA_RPC_MAINNET_ENDPOINT_ENV
    : "";
  if (!clusterSpecificEnvironment) {
    throw new SolanaRpcConfigurationError("solana_cluster_not_configured");
  }

  // Prefer a cluster-specific transaction endpoint. The generic endpoint is
  // retained for existing deployments, and the server-only Helius DAS URL is
  // a safe final fallback because Helius serves JSON-RPC and DAS on the same
  // credentialed, cluster-specific host. Every candidate is validated before
  // it can receive a request; no secret value is logged or returned.
  const environmentNames = [
    clusterSpecificEnvironment,
    SOLANA_RPC_ENDPOINT_ENV,
    HELIUS_DAS_URL_ENV,
  ];
  for (const name of environmentNames) {
    const value = readEnvironment(name);
    if (!value?.trim()) continue;
    try {
      return requireSolanaRpcEndpoint(value, normalizedCluster);
    } catch (error) {
      if (!(error instanceof SolanaRpcConfigurationError)) throw error;
    }
  }

  throw new SolanaRpcConfigurationError("solana_rpc_not_configured");
}

export async function forwardSolanaRpcRequest(
  endpoint: string,
  request: SolanaRpcRequest,
): Promise<Record<string, unknown>> {
  let response: Response;
  try {
    response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request),
      signal: AbortSignal.timeout(35_000),
    });
  } catch {
    throw new SolanaRpcUpstreamError();
  }

  if (!response.ok) throw new SolanaRpcUpstreamError();

  let payload: unknown;
  try {
    payload = await response.json();
  } catch {
    throw new SolanaRpcUpstreamError();
  }

  const result = asRecord(payload);
  if (result.jsonrpc !== "2.0" || result.id !== request.id || (!("result" in result) && !("error" in result))) {
    throw new SolanaRpcUpstreamError();
  }
  return result;
}
