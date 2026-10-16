import { assert, assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { Keypair, PublicKey } from "npm:@solana/web3.js@1.98.4";
import {
  archiveBindingFields,
  coreProgramIdForTrustTests,
  fetchAndVerifyArchiveMetadataSnapshot,
  verifyBindAssetPayload,
} from "./bindery_foundation.ts";
import type { BindOwnershipConfig } from "./ownership_spine.ts";

const wallet = Keypair.generate().publicKey.toBase58();
const authority = Keypair.generate().publicKey;
const covers = Keypair.generate().publicKey;
const moves = Keypair.generate().publicKey;
const cosmetics = Keypair.generate().publicKey;

const config = {
  cluster: "devnet",
  rpcUrl: "https://devnet.helius-rpc.com/?api-key=test",
  expectedGenesisHash: "test",
  dasUrl: "https://devnet.helius-rpc.com/?api-key=test",
  programId: Keypair.generate().publicKey,
  movesCollection: moves,
  movesUpdateAuthority: authority,
  resultAuthority: Keypair.generate(),
  generatorVersion: 9,
  modifierPoolHash: "a".repeat(64),
  ticketTtlSeconds: 300,
  stampRamCost: 75,
  schemaVersion: 2,
  metadataUriBase: "https://example.invalid",
  coversCollection: covers,
  cosmeticsCollection: cosmetics,
  cosmeticsUpdateAuthorities: new Set([authority.toBase58()]),
  archivesCollection: Keypair.generate().publicKey,
} as BindOwnershipConfig;

function coreAsset(collection: PublicKey, attrs: Record<string, string>) {
  return {
    id: Keypair.generate().publicKey.toBase58(),
    interface: "MplCoreAsset",
    ownership: { owner: wallet },
    grouping: [{ group_key: "collection", group_value: collection.toBase58() }],
    authorities: [{ address: authority.toBase58() }],
    plugins: [{ type: "Attributes", attributes: Object.entries(attrs).map(([key, value]) => ({ key, value })) }],
  };
}

Deno.test("Bind asset inspection accepts only exact Core cover provenance", () => {
  const asset = coreAsset(covers, {
    "cb.kind": "book_cover",
    "cb.schema_version": "2",
    "cb.cover_id": "founder-cover",
  });
  const result = verifyBindAssetPayload(config, asset, asset.id, wallet, "cover");
  assertEquals(result.trusted, true);
  assertEquals(result.reasons, []);

  const wrongKind = verifyBindAssetPayload(
    config,
    { ...asset, plugins: [{ attributes: [{ key: "cb.kind", value: "cosmetic" }] }] },
    asset.id,
    wallet,
    "cover",
  );
  assertEquals(wrongKind.trusted, false);
  assert(wrongKind.reasons.includes("kind_mismatch"));

  const { id: _assetId, ...missingIdentity } = asset;
  const missingIdentityResult = verifyBindAssetPayload(config, missingIdentity, asset.id, wallet, "cover");
  assertEquals(missingIdentityResult.trusted, false);
  assert(missingIdentityResult.reasons.includes("asset_address_mismatch"));
});

Deno.test("Bind asset inspection rejects forged Stamped metadata and accepts exact locks", () => {
  const attrs = {
    "cb.kind": "stamped_move",
    "cb.schema_version": "2",
    "cb.move_tier": "stamped",
    "cb.generator_version": "9",
    "cb.pool_hash": "a".repeat(64),
    "cb.modifier_count": "1",
    "cb.modifier_1": "mod.power",
  };
  const asset = coreAsset(moves, attrs);
  assertEquals(verifyBindAssetPayload(config, asset, asset.id, wallet, "stamped_move").trusted, true);

  const forged = coreAsset(moves, { ...attrs, "cb.pool_hash": "b".repeat(64) });
  const result = verifyBindAssetPayload(config, forged, forged.id, wallet, "stamped_move");
  assertEquals(result.trusted, false);
  assert(result.reasons.includes("pool_hash_mismatch"));
});

Deno.test("Cosmetic inspection requires identity and canonical kind", () => {
  const asset = coreAsset(cosmetics, {
    "cb.kind": "cosmetic",
    "cb.schema_version": "2",
    "cb.cosmetic_id": "skin.neon",
    "cb.slot": "body",
  });
  assertEquals(verifyBindAssetPayload(config, asset, asset.id, wallet, "cosmetic").trusted, true);

  const missingSlot = coreAsset(cosmetics, {
    "cb.kind": "cosmetic",
    "cb.schema_version": "2",
    "cb.cosmetic_id": "skin.neon",
  });
  const result = verifyBindAssetPayload(config, missingSlot, missingSlot.id, wallet, "cosmetic");
  assert(result.reasons.includes("cosmetic_identity_missing"));
});

Deno.test("Archive projection fields are fail-closed for unbound Chainlings", () => {
  assertEquals(archiveBindingFields(undefined), {
    is_bound: false,
    archive_asset_id: "",
    archived_at_unix: 0,
    archive_binding: null,
  });
  assertEquals(coreProgramIdForTrustTests(), "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d");
});

Deno.test("Archive metadata inspection pins the configured origin and exact response hash", async () => {
  const body = new TextEncoder().encode(JSON.stringify({ name: "Phase 1 Archive" }));
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", body));
  const expectedHash = Array.from(digest).map((value) => value.toString(16).padStart(2, "0")).join("");
  const source = {
    archive_uri: "https://example.invalid/archive/phase-1.json",
    archive_metadata_hash: expectedHash,
  };
  const fetchImpl = (_input: string | URL | Request, _init?: RequestInit) =>
    Promise.resolve(new Response(body, { status: 200, headers: { "content-type": "application/json" } }));
  assertEquals(await fetchAndVerifyArchiveMetadataSnapshot(config, source, fetchImpl), []);
  assertEquals(
    await fetchAndVerifyArchiveMetadataSnapshot(
      config,
      { ...source, archive_metadata_hash: "b".repeat(64) },
      fetchImpl,
    ),
    ["archive_metadata_hash_mismatch"],
  );
  assertEquals(
    await fetchAndVerifyArchiveMetadataSnapshot(
      config,
      { ...source, archive_uri: "https://untrusted.invalid/archive.json" },
      fetchImpl,
    ),
    ["archive_metadata_uri_outside_configured_base"],
  );
  assertEquals(
    await fetchAndVerifyArchiveMetadataSnapshot(
      { ...config, metadataUriBase: "https://example.invalid/archive" },
      { ...source, archive_uri: "https://example.invalid/archive/../escape.json" },
      fetchImpl,
    ),
    ["archive_metadata_uri_outside_configured_base"],
  );
});

Deno.test("Phase 1 wiring reuses inspection before preparation and one archive projection", async () => {
  const prepare = await Deno.readTextFile(new URL("../ownership_prepare_bind/index.ts", import.meta.url));
  const inspect = await Deno.readTextFile(new URL("../ownership_inspect_bind_source/index.ts", import.meta.url));
  const roster = await Deno.readTextFile(new URL("../profile_get_chainlings/index.ts", import.meta.url));
  const party = await Deno.readTextFile(new URL("../profile_get_party/index.ts", import.meta.url));
  const inventory = await Deno.readTextFile(new URL("../ownership_get_inventory/index.ts", import.meta.url));
  for (const source of [prepare, inspect]) {
    assert(source.includes("inspectBindSourceTrust"));
    assert(source.includes("ownership_record_bind_source_inspection_service"));
  }
  for (const source of [roster, party, inventory]) {
    assert(source.includes("loadArchiveBindingProjection"));
  }
});

Deno.test("Phase 1 migration is service-only, expiring, immutable, audited, and seed-free", async () => {
  const migration = await Deno.readTextFile(
    new URL("../../migrations/20261016045210_bindery_phase1_foundation.sql", import.meta.url),
  );
  assertEquals(/INSERT\s+INTO\s+public\.ownership_bind_sources\s*\(/i.test(migration), true);
  assertEquals(/VALUES\s*\([\s\S]*FALSE, 1,/i.test(migration), true);
  assert(migration.includes("bind_source_inputs_immutable"));
  assert(migration.includes("bind_source_expiry_out_of_range"));
  assert(migration.includes("p_expires_at IS NULL"));
  assert(migration.includes("p_dossier_provenance IS NULL"));
  assert(migration.includes("ownership_bind_source_audit"));
  assert(migration.includes("last_inspected_at < now() - interval '10 minutes'"));
  assert(migration.includes("FROM PUBLIC, anon, authenticated"));
  assert(migration.includes("TO service_role"));
  assertEquals(migration.includes("'enabled', TRUE"), false);
});
