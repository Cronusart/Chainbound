import { assertEquals, assertRejects, assertThrows } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  forwardSolanaRpcRequest,
  prepareSolanaRpcRequest,
  requireSolanaRpcEndpoint,
  resolveSolanaRpcEndpoint,
  SolanaRpcConfigurationError,
  SolanaRpcRequestError,
  SolanaRpcUpstreamError,
} from "./solana_rpc_proxy.ts";

const KEY = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";
const SIGNATURE = "5".repeat(88);

Deno.test("proxy allows only the Unity RPC surface and sanitizes parameters", () => {
  assertEquals(
    prepareSolanaRpcRequest({
      jsonrpc: "2.0",
      id: 7,
      method: "getAccountInfo",
      params: [KEY, { encoding: "jsonParsed", commitment: "processed", extra: true }],
    }),
    {
      jsonrpc: "2.0",
      id: 7,
      method: "getAccountInfo",
      params: [KEY, { encoding: "base64", commitment: "confirmed" }],
    },
  );

  assertEquals(
    prepareSolanaRpcRequest({
      jsonrpc: "2.0",
      id: "confirm",
      method: "getSignatureStatuses",
      params: [[SIGNATURE], { searchTransactionHistory: false }],
    }),
    {
      jsonrpc: "2.0",
      id: "confirm",
      method: "getSignatureStatuses",
      params: [[SIGNATURE], { searchTransactionHistory: true }],
    },
  );

  for (const method of ["sendTransaction", "getBalance", "getProgramAccounts", "requestAirdrop"]) {
    const error = assertThrows(
      () => prepareSolanaRpcRequest({ jsonrpc: "2.0", id: 1, method, params: [] }),
      SolanaRpcRequestError,
    );
    assertEquals(error.code, "rpc_method_not_allowed");
  }
});

Deno.test("proxy validates batches, transaction payloads, and slots", () => {
  assertThrows(
    () => prepareSolanaRpcRequest({ jsonrpc: "2.0", id: 1, method: "getMultipleAccounts", params: [[]] }),
    SolanaRpcRequestError,
  );
  assertThrows(
    () => prepareSolanaRpcRequest({ jsonrpc: "2.0", id: 1, method: "simulateTransaction", params: ["not base64!"] }),
    SolanaRpcRequestError,
  );
  assertThrows(
    () => prepareSolanaRpcRequest({ jsonrpc: "2.0", id: 1, method: "getBlockTime", params: [-1] }),
    SolanaRpcRequestError,
  );
});

Deno.test("credentialed Helius endpoint is pinned to the selected cluster", () => {
  assertEquals(
    requireSolanaRpcEndpoint("https://devnet.helius-rpc.com/?api-key=redacted-test-value", "devnet"),
    "https://devnet.helius-rpc.com/?api-key=redacted-test-value",
  );
  assertEquals(
    requireSolanaRpcEndpoint("https://mainnet.helius-rpc.com/?api-key=redacted-test-value", "mainnet-beta"),
    "https://mainnet.helius-rpc.com/?api-key=redacted-test-value",
  );

  for (
    const [endpoint, cluster] of [
      ["https://devnet.helius-rpc.com/", "devnet"],
      ["http://devnet.helius-rpc.com/?api-key=x", "devnet"],
      ["https://mainnet.helius-rpc.com/?api-key=x", "devnet"],
      ["https://devnet.helius-rpc.com/?api-key=x", "mainnet-beta"],
    ]
  ) {
    assertThrows(() => requireSolanaRpcEndpoint(endpoint, cluster), SolanaRpcConfigurationError);
  }
});

Deno.test("endpoint resolution falls back to the cluster-matched server-only Helius URL", () => {
  const values: Record<string, string> = {
    CHAINBOUND_SOLANA_RPC_ENDPOINT: "https://api.devnet.solana.com",
    HELIUS_DAS_URL: "https://devnet.helius-rpc.com/?api-key=server-only-test-value",
  };

  assertEquals(
    resolveSolanaRpcEndpoint((name) => values[name], "devnet"),
    "https://devnet.helius-rpc.com/?api-key=server-only-test-value",
  );
  assertThrows(
    () => resolveSolanaRpcEndpoint((name) => values[name], "mainnet-beta"),
    SolanaRpcConfigurationError,
  );
});

Deno.test("upstream forwarding preserves JSON-RPC and collapses provider failures", async () => {
  const originalFetch = globalThis.fetch;
  const request = prepareSolanaRpcRequest({ jsonrpc: "2.0", id: 9, method: "getGenesisHash", params: [] });
  globalThis.fetch = (_input, init) => {
    assertEquals(JSON.parse(String(init?.body)), request);
    return Promise.resolve(
      new Response(
        JSON.stringify({
          jsonrpc: "2.0",
          id: 9,
          result: "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG",
        }),
        { status: 200 },
      ),
    );
  };

  try {
    assertEquals(await forwardSolanaRpcRequest("https://devnet.helius-rpc.com/?api-key=never-logged", request), {
      jsonrpc: "2.0",
      id: 9,
      result: "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG",
    });

    globalThis.fetch = () => Promise.resolve(new Response("provider detail", { status: 429 }));
    const error = await assertRejects(
      () => forwardSolanaRpcRequest("https://devnet.helius-rpc.com/?api-key=never-logged", request),
      SolanaRpcUpstreamError,
    );
    assertEquals(error.message, "solana_rpc_upstream");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

Deno.test("Edge handler authenticates and rate-limits before reading the RPC secret", async () => {
  const source = await Deno.readTextFile(new URL("../solana_rpc_proxy/index.ts", import.meta.url));
  const authIndex = source.indexOf("requireAuthenticatedUser(req)");
  const quotaIndex = source.indexOf('supabase.rpc("solana_rpc_consume_rate_limit"');
  const secretIndex = source.indexOf("resolveSolanaRpcEndpoint(");

  assertEquals(authIndex >= 0, true);
  assertEquals(quotaIndex > authIndex, true);
  assertEquals(secretIndex > quotaIndex, true);
  assertEquals(source.includes("error.message"), false);
  assertEquals(source.includes("JSON.stringify(error)"), false);
});
