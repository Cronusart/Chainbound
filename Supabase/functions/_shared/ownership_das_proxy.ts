type JsonMap = Record<string, unknown>;

const SOLANA_BASE58_RE = /^[1-9A-HJ-NP-Za-km-z]{32,44}$/;

export const HELIUS_DAS_URL_ENV = "HELIUS_DAS_URL";
export const SUPPORTED_OWNERSHIP_CLUSTERS = ["devnet", "mainnet-beta"] as const;
export type OwnershipCluster = typeof SUPPORTED_OWNERSHIP_CLUSTERS[number];
export const MAX_DAS_LIMIT = 50;
export const MAX_DAS_PAGE = 1000;
export const DEFAULT_DAS_LIMIT = 20;
export const ALLOWED_DAS_METHODS = ["getAsset", "getAssetsByGroup", "getAssetsByOwner"] as const;

export type AllowedDasMethod = typeof ALLOWED_DAS_METHODS[number];

export interface PreparedDasRequest {
  method: AllowedDasMethod;
  params: JsonMap;
}

export class DasProxyRequestError extends Error {
  constructor(public readonly code: string, public readonly status: number) {
    super(code);
  }
}

export class DasProxyConfigurationError extends Error {
  constructor(public readonly code: string) {
    super(code);
  }
}

export class DasUpstreamError extends Error {
  constructor() {
    super("das_upstream");
  }
}

export function prepareDasRequest(
  body: JsonMap,
  trustedMovesCollection: string,
  linkedWallet = "",
): PreparedDasRequest {
  if (!SOLANA_BASE58_RE.test(trustedMovesCollection)) {
    throw new DasProxyConfigurationError("invalid_env:CHAINBOUND_CORE_MOVES_COLLECTION");
  }

  const method = str(body.method);
  const params = asRecord(body.params);

  if (method === "getAsset") {
    const id = str(params.id);
    if (!SOLANA_BASE58_RE.test(id)) throw new DasProxyRequestError("invalid_asset_id", 400);
    return { method, params: { id } };
  }

  if (method === "getAssetsByGroup") {
    if (str(params.groupKey) !== "collection") {
      throw new DasProxyRequestError("group_key_must_be_collection", 400);
    }
    if (str(params.groupValue) !== trustedMovesCollection) {
      throw new DasProxyRequestError("collection_not_allowed", 400);
    }
    return {
      method,
      params: {
        groupKey: "collection",
        groupValue: trustedMovesCollection,
        page: clampInteger(params.page, 1, 1, MAX_DAS_PAGE),
        limit: clampInteger(params.limit, DEFAULT_DAS_LIMIT, 1, MAX_DAS_LIMIT),
      },
    };
  }

  if (method === "getAssetsByOwner") {
    if (!SOLANA_BASE58_RE.test(linkedWallet)) {
      throw new DasProxyRequestError("wallet_not_linked_to_profile", 403);
    }
    const suppliedOwner = str(params.ownerAddress);
    if (suppliedOwner && suppliedOwner !== linkedWallet) {
      throw new DasProxyRequestError("wallet_not_linked_to_profile", 403);
    }
    return {
      method,
      params: {
        ownerAddress: linkedWallet,
        page: clampInteger(params.page, 1, 1, MAX_DAS_PAGE),
        limit: clampInteger(params.limit, DEFAULT_DAS_LIMIT, 1, MAX_DAS_LIMIT),
        displayOptions: {
          showCollectionMetadata: false,
          showFungible: false,
          showNativeBalance: false,
          showGrandTotal: false,
        },
      },
    };
  }

  throw new DasProxyRequestError("das_method_not_allowed", 400);
}

export function requireOwnershipCluster(value: string | undefined): OwnershipCluster {
  const cluster = str(value);
  if (cluster === "devnet" || cluster === "mainnet-beta") return cluster;
  throw new DasProxyConfigurationError("invalid_env:CHAINBOUND_SOLANA_CLUSTER");
}

export function requireHeliusDasUrl(value: string | undefined, cluster: OwnershipCluster): string {
  const raw = str(value);
  if (!raw) throw new DasProxyConfigurationError(`missing_env:${HELIUS_DAS_URL_ENV}`);

  try {
    const url = new URL(raw);
    const apiKey = str(url.searchParams.get("api-key"));
    const expectedHost = cluster === "mainnet-beta" ? "mainnet.helius-rpc.com" : "devnet.helius-rpc.com";
    if (url.protocol !== "https:" || url.hostname.toLowerCase() !== expectedHost || !apiKey) {
      throw new Error("invalid");
    }
    return url.toString();
  } catch {
    throw new DasProxyConfigurationError(`invalid_env:${HELIUS_DAS_URL_ENV}`);
  }
}

/** Backward-compatible Devnet-only helper for the dedicated smoke-grant path. */
export function requireHeliusDevnetUrl(value: string | undefined): string {
  return requireHeliusDasUrl(value, "devnet");
}

export async function forwardDasRequest(endpoint: string, request: PreparedDasRequest): Promise<unknown> {
  let response: Response;
  try {
    response = await fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: "chainbound-ownership-das",
        method: request.method,
        params: request.params,
      }),
      signal: AbortSignal.timeout(10_000),
    });
  } catch {
    throw new DasUpstreamError();
  }

  if (!response.ok) throw new DasUpstreamError();

  let payload: JsonMap;
  try {
    payload = await response.json() as JsonMap;
  } catch {
    throw new DasUpstreamError();
  }

  if (payload.error || !("result" in payload)) throw new DasUpstreamError();
  return payload.result;
}

function asRecord(value: unknown): JsonMap {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? value as JsonMap : {};
}

function clampInteger(value: unknown, fallback: number, min: number, max: number): number {
  if (typeof value !== "number" || !Number.isFinite(value)) return fallback;
  return Math.min(max, Math.max(min, Math.floor(value)));
}

function str(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}
