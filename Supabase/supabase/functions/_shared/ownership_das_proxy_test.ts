import { assertEquals, assertRejects, assertThrows } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  DasProxyConfigurationError,
  DasProxyRequestError,
  DasUpstreamError,
  forwardDasRequest,
  MAX_DAS_LIMIT,
  MAX_DAS_PAGE,
  prepareDasRequest,
  requireHeliusDasUrl,
  requireHeliusDevnetUrl,
  requireOwnershipCluster,
} from "./ownership_das_proxy.ts";

const MOVES = "AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC";
const WALLET = "DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG";

Deno.test("getAsset forwards only a valid asset id", () => {
  assertEquals(prepareDasRequest({ method: "getAsset", params: { id: MOVES, ignored: true } }, MOVES), {
    method: "getAsset",
    params: { id: MOVES },
  });
  const error = assertThrows(
    () => prepareDasRequest({ method: "getAsset", params: { id: "not-a-pubkey" } }, MOVES),
    DasProxyRequestError,
  );
  assertEquals(error.code, "invalid_asset_id");
  assertEquals(error.status, 400);
});

Deno.test("getAssetsByGroup is collection-pinned and clamps pagination", () => {
  assertEquals(
    prepareDasRequest({
      method: "getAssetsByGroup",
      params: { groupKey: "collection", groupValue: MOVES, page: 99_999, limit: 999 },
    }, MOVES),
    {
      method: "getAssetsByGroup",
      params: { groupKey: "collection", groupValue: MOVES, page: MAX_DAS_PAGE, limit: MAX_DAS_LIMIT },
    },
  );

  const wrongCollection = assertThrows(
    () =>
      prepareDasRequest({
        method: "getAssetsByGroup",
        params: { groupKey: "collection", groupValue: WALLET },
      }, MOVES),
    DasProxyRequestError,
  );
  assertEquals(wrongCollection.code, "collection_not_allowed");

  const wrongKey = assertThrows(
    () =>
      prepareDasRequest({
        method: "getAssetsByGroup",
        params: { groupKey: "owner", groupValue: MOVES },
      }, MOVES),
    DasProxyRequestError,
  );
  assertEquals(wrongKey.code, "group_key_must_be_collection");

  assertEquals(
    prepareDasRequest({
      method: "getAssetsByGroup",
      params: { groupKey: "collection", groupValue: MOVES, page: -5, limit: 0 },
    }, MOVES).params,
    {
      groupKey: "collection",
      groupValue: MOVES,
      page: 1,
      limit: 1,
    },
  );
});

Deno.test("getAssetsByOwner is bound to the authenticated profile wallet", () => {
  assertEquals(prepareDasRequest({ method: "getAssetsByOwner", params: { limit: 5 } }, MOVES, WALLET), {
    method: "getAssetsByOwner",
    params: {
      ownerAddress: WALLET,
      page: 1,
      limit: 5,
      displayOptions: {
        showCollectionMetadata: false,
        showFungible: false,
        showNativeBalance: false,
        showGrandTotal: false,
      },
    },
  });

  const spoof = assertThrows(
    () => prepareDasRequest({ method: "getAssetsByOwner", params: { ownerAddress: MOVES } }, MOVES, WALLET),
    DasProxyRequestError,
  );
  assertEquals(spoof.code, "wallet_not_linked_to_profile");
  assertEquals(spoof.status, 403);
});

Deno.test("all non-allowlisted DAS and Solana RPC methods are rejected", () => {
  for (const method of ["searchAssets", "getAssetProof", "sendTransaction", "getBalance"]) {
    const error = assertThrows(
      () => prepareDasRequest({ method, params: {} }, MOVES, WALLET),
      DasProxyRequestError,
    );
    assertEquals(error.code, "das_method_not_allowed");
    assertEquals(error.status, 400);
  }
});

Deno.test("Helius endpoint is credentialed Devnet HTTPS only", () => {
  assertEquals(
    requireHeliusDevnetUrl("https://devnet.helius-rpc.com/?api-key=redacted-test-value"),
    "https://devnet.helius-rpc.com/?api-key=redacted-test-value",
  );

  for (
    const value of [
      undefined,
      "https://devnet.helius-rpc.com/",
      "http://devnet.helius-rpc.com/?api-key=x",
      "https://mainnet.helius-rpc.com/?api-key=x",
    ]
  ) {
    assertThrows(() => requireHeliusDevnetUrl(value), DasProxyConfigurationError);
  }
});

Deno.test("Helius endpoint is pinned to the explicitly selected ownership cluster", () => {
  assertEquals(requireOwnershipCluster("devnet"), "devnet");
  assertEquals(requireOwnershipCluster("mainnet-beta"), "mainnet-beta");
  assertThrows(() => requireOwnershipCluster("mainnet"), DasProxyConfigurationError);

  assertEquals(
    requireHeliusDasUrl("https://mainnet.helius-rpc.com/?api-key=redacted-test-value", "mainnet-beta"),
    "https://mainnet.helius-rpc.com/?api-key=redacted-test-value",
  );
  assertThrows(
    () => requireHeliusDasUrl("https://devnet.helius-rpc.com/?api-key=x", "mainnet-beta"),
    DasProxyConfigurationError,
  );
  assertThrows(
    () => requireHeliusDasUrl("https://mainnet.helius-rpc.com/?api-key=x", "devnet"),
    DasProxyConfigurationError,
  );
});

Deno.test("upstream forwarding emits only the prepared JSON-RPC request", async () => {
  const originalFetch = globalThis.fetch;
  let forwardedBody = "";
  globalThis.fetch = (_input, init) => {
    forwardedBody = String(init?.body ?? "");
    return Promise.resolve(
      new Response(JSON.stringify({ jsonrpc: "2.0", id: "upstream", result: { total: 0 } }), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      }),
    );
  };

  try {
    const request = prepareDasRequest({
      method: "getAssetsByGroup",
      params: { groupKey: "collection", groupValue: MOVES, limit: 500 },
    }, MOVES);
    assertEquals(await forwardDasRequest("https://devnet.helius-rpc.com/?api-key=never-logged", request), {
      total: 0,
    });
    assertEquals(JSON.parse(forwardedBody), {
      jsonrpc: "2.0",
      id: "chainbound-ownership-das",
      method: "getAssetsByGroup",
      params: { groupKey: "collection", groupValue: MOVES, page: 1, limit: 50 },
    });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

Deno.test("upstream failures collapse to a secret-free error", async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = () => Promise.resolve(new Response("provider detail", { status: 429 }));
  try {
    const error = await assertRejects(
      () =>
        forwardDasRequest(
          "https://devnet.helius-rpc.com/?api-key=never-logged",
          { method: "getAsset", params: { id: MOVES } },
        ),
      DasUpstreamError,
    );
    assertEquals(error.message, "das_upstream");
  } finally {
    globalThis.fetch = originalFetch;
  }
});

Deno.test("Edge handler authenticates through the shared profile resolver before reading Helius config", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_das_proxy/index.ts", import.meta.url));
  const authIndex = source.indexOf("resolveAuthenticatedProfile(req, supabase, body)");
  const clusterIndex = source.indexOf("const cluster = requireOwnershipCluster");
  const endpointIndex = source.indexOf("const endpoint = requireHeliusDasUrl");

  assertEquals(authIndex >= 0, true);
  assertEquals(clusterIndex > authIndex, true);
  assertEquals(endpointIndex > authIndex, true);
  assertEquals(source.includes("resolveProfileFromBody"), false);
  assertEquals(source.includes("error.message"), false);
  assertEquals(source.includes("JSON.stringify(error)"), false);
  assertEquals(source.includes('error: "das_upstream"'), true);
});
