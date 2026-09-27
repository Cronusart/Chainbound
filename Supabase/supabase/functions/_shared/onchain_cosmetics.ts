import { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { getActiveWallet, JsonMap, SOLANA_BASE58_RE, str } from "./account_helpers.ts";

const DEFAULT_CLUSTER = "devnet";
const DEFAULT_DAS_PAGE_LIMIT = 100;
const MAX_DAS_PAGES = 10;
const DEFAULT_CACHE_TTL_MS = 2 * 60 * 1000;

const FREE_COSMETIC_IDS = new Set(["", "none", "default", "base", "entangled"]);

export interface CoreCosmeticRecord {
  asset_id: string;
  cosmetic_id: string;
  slot: string;
  collection: string;
  update_authority: string;
  schema_version: number;
  verified_at: string;
}

export interface CosmeticVerificationResult {
  ok: boolean;
  error?: string;
  status?: number;
  wallet_public_key?: string;
  records?: CoreCosmeticRecord[];
  asset_ids_by_cosmetic_id?: Record<string, string>;
  missing_cosmetic_ids?: string[];
}

interface CatalogEntry {
  cosmetic_id: string;
  slot: string;
  schema_version: number;
  enabled: boolean;
  free_default: boolean;
}

interface ValidatedAsset {
  ok: boolean;
  error?: string;
  record?: CoreCosmeticRecord;
}

export function isSolanaPublicKey(value: unknown): boolean {
  return SOLANA_BASE58_RE.test(str(value));
}

export function normalizePublicKey(value: unknown): string {
  const normalized = str(value);
  return SOLANA_BASE58_RE.test(normalized) ? normalized : "";
}

export function isFreeCosmeticId(value: unknown): boolean {
  return FREE_COSMETIC_IDS.has(str(value));
}

export function collectSelectedCosmeticIds(visualIdentity: Record<string, unknown>): string[] {
  const ids = new Set<string>();
  for (const value of Object.values(visualIdentity)) {
    const id = str(value);
    if (!isFreeCosmeticId(id)) ids.add(id);
  }
  return [...ids];
}

export function buildAssetHintsByCosmeticId(
  visualIdentity: Record<string, unknown>,
  visualIdentityAssetIds: Record<string, unknown>,
): Record<string, string> {
  const hints: Record<string, string> = {};
  for (const [slotKey, cosmeticValue] of Object.entries(visualIdentity)) {
    const cosmeticId = str(cosmeticValue);
    if (isFreeCosmeticId(cosmeticId)) continue;

    const assetId = normalizePublicKey(visualIdentityAssetIds[slotKey]);
    if (assetId) hints[cosmeticId] = assetId;
  }
  return hints;
}

export async function resolveVerifiedWalletForProfile(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletHint: string,
): Promise<CosmeticVerificationResult> {
  const profileId = str(playerProfileId);
  if (!profileId) {
    return { ok: false, error: "player_profile_id_required", status: 400 };
  }

  const hintedWallet = str(walletHint);
  if (hintedWallet) {
    if (!isSolanaPublicKey(hintedWallet)) {
      return { ok: false, error: "wallet_address_invalid", status: 400 };
    }

    const { data, error } = await supabase
      .from("profile_wallets")
      .select("wallet_public_key")
      .eq("player_profile_id", profileId)
      .eq("wallet_public_key", hintedWallet)
      .eq("is_active", true)
      .is("unlinked_at", null)
      .maybeSingle();

    if (error) throw new Error(`wallet_lookup_failed:${error.message}`);
    if (!data?.wallet_public_key) {
      return { ok: false, error: "wallet_not_linked_to_profile", status: 403 };
    }

    return { ok: true, wallet_public_key: hintedWallet };
  }

  const wallet = await getActiveWallet(supabase, profileId);
  const activeWallet = normalizePublicKey(wallet?.wallet_public_key);
  if (!activeWallet) {
    return { ok: false, error: "active_wallet_required", status: 400 };
  }

  return { ok: true, wallet_public_key: activeWallet };
}

export async function refreshCoreCosmeticOwnershipCache(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletPublicKey: string,
): Promise<CosmeticVerificationResult> {
  const profileId = str(playerProfileId);
  const wallet = normalizePublicKey(walletPublicKey);
  const cluster = chainCluster();
  const expectedCollection = normalizePublicKey(Deno.env.get("CHAINBOUND_CORE_COSMETICS_COLLECTION"));
  const trustedAuthorities = trustedUpdateAuthorities();

  if (!profileId) return { ok: false, error: "player_profile_id_required", status: 400 };
  if (!wallet) return { ok: false, error: "wallet_address_invalid", status: 400 };
  if (!expectedCollection) return { ok: false, error: "core_cosmetics_collection_unconfigured", status: 503 };
  if (trustedAuthorities.size === 0) {
    return { ok: false, error: "core_cosmetics_update_authorities_unconfigured", status: 503 };
  }

  const catalog = await loadCatalog(supabase);
  const assets = await fetchAssetsByOwner(wallet);
  const verifiedRows: JsonMap[] = [];
  const verifiedRecords: CoreCosmeticRecord[] = [];
  const nowIso = new Date().toISOString();

  await markCachedCosmeticsStaleForWallet(supabase, profileId, wallet, cluster, nowIso);

  for (const asset of assets) {
    const validation = validateCoreCosmeticAsset(asset, {
      wallet,
      expectedCollection,
      trustedAuthorities,
      catalog,
      verifiedAt: nowIso,
    });

    if (!validation.ok || !validation.record) continue;

    const record = validation.record;
    verifiedRecords.push(record);
    verifiedRows.push({
      player_profile_id: profileId,
      wallet_public_key: wallet,
      cluster,
      asset_id: record.asset_id,
      cosmetic_id: record.cosmetic_id,
      slot: record.slot,
      collection: record.collection,
      update_authority: record.update_authority,
      schema_version: record.schema_version,
      verification_status: "verified",
      verification_error: null,
      last_verified_at: nowIso,
      metadata_json: { source: "das_getAssetsByOwner" },
    });
  }

  if (verifiedRows.length > 0) {
    const { error } = await supabase
      .from("chainbound_core_cosmetic_ownership_cache")
      .upsert(verifiedRows, { onConflict: "cluster,asset_id" });

    if (error) throw new Error(`cosmetic_cache_upsert_failed:${error.message}`);
  }

  return {
    ok: true,
    wallet_public_key: wallet,
    records: verifiedRecords,
    asset_ids_by_cosmetic_id: mapAssetIdsByCosmeticId(verifiedRecords),
  };
}

async function markCachedCosmeticsStaleForWallet(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletPublicKey: string,
  cluster: string,
  verifiedAt: string,
): Promise<void> {
  const { error } = await supabase
    .from("chainbound_core_cosmetic_ownership_cache")
    .update({
      verification_status: "stale",
      verification_error: "not_seen_in_latest_das_refresh",
      last_verified_at: verifiedAt,
    })
    .eq("player_profile_id", playerProfileId)
    .eq("wallet_public_key", walletPublicKey)
    .eq("cluster", cluster)
    .eq("verification_status", "verified");

  if (error) throw new Error(`cosmetic_cache_stale_mark_failed:${error.message}`);
}

export async function verifyCosmeticSelectionOwnership(
  supabase: SupabaseClient,
  playerProfileId: string,
  walletHint: string,
  selectedCosmeticIds: string[],
  assetHintsByCosmeticId: Record<string, string> = {},
): Promise<CosmeticVerificationResult> {
  const required = [...new Set((selectedCosmeticIds ?? []).map(str).filter((id) => !isFreeCosmeticId(id)))];
  if (required.length === 0) return { ok: true, records: [], asset_ids_by_cosmetic_id: {} };

  const wallet = await resolveVerifiedWalletForProfile(supabase, playerProfileId, walletHint);
  if (!wallet.ok || !wallet.wallet_public_key) return wallet;

  const refresh = await refreshCoreCosmeticOwnershipCache(supabase, playerProfileId, wallet.wallet_public_key);
  if (!refresh.ok) return refresh;

  const cluster = chainCluster();
  const freshAfter = new Date(Date.now() - DEFAULT_CACHE_TTL_MS).toISOString();
  const { data, error } = await supabase
    .from("chainbound_core_cosmetic_ownership_cache")
    .select("asset_id, cosmetic_id, slot, collection, update_authority, schema_version, last_verified_at")
    .eq("player_profile_id", playerProfileId)
    .eq("wallet_public_key", wallet.wallet_public_key)
    .eq("cluster", cluster)
    .eq("verification_status", "verified")
    .in("cosmetic_id", required)
    .gte("last_verified_at", freshAfter);

  if (error) throw new Error(`cosmetic_cache_select_failed:${error.message}`);

  const records = ((data ?? []) as JsonMap[]).map(cacheRowToRecord).filter((r): r is CoreCosmeticRecord => r != null);
  const missing: string[] = [];
  const accepted: CoreCosmeticRecord[] = [];

  for (const id of required) {
    const hintedAsset = normalizePublicKey(assetHintsByCosmeticId[id]);
    const match = records.find((r) => r.cosmetic_id === id && (!hintedAsset || r.asset_id === hintedAsset));
    if (match) accepted.push(match);
    else missing.push(id);
  }

  if (missing.length > 0) {
    return {
      ok: false,
      error: `cosmetic_not_owned:${missing.join(",")}`,
      status: 403,
      wallet_public_key: wallet.wallet_public_key,
      records: accepted,
      missing_cosmetic_ids: missing,
    };
  }

  return {
    ok: true,
    wallet_public_key: wallet.wallet_public_key,
    records: accepted,
    asset_ids_by_cosmetic_id: mapAssetIdsByCosmeticId(accepted),
  };
}

function chainCluster(): string {
  return str(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER")) || DEFAULT_CLUSTER;
}

function dasRpcUrl(): string {
  return str(Deno.env.get("CHAINBOUND_DAS_RPC_URL")) ||
    str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT")) ||
    "https://api.devnet.solana.com";
}

function trustedUpdateAuthorities(): Set<string> {
  const raw = str(Deno.env.get("CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES"));
  const values = raw.split(",").map((v) => normalizePublicKey(v)).filter(Boolean);
  return new Set(values);
}

async function loadCatalog(supabase: SupabaseClient): Promise<Map<string, CatalogEntry>> {
  const { data, error } = await supabase
    .from("chainbound_cosmetic_catalog")
    .select("cosmetic_id, slot, schema_version, enabled, free_default")
    .eq("enabled", true);

  if (error) throw new Error(`cosmetic_catalog_load_failed:${error.message}`);

  const catalog = new Map<string, CatalogEntry>();
  for (const row of (data ?? []) as JsonMap[]) {
    const cosmeticId = str(row.cosmetic_id);
    if (!cosmeticId) continue;
    catalog.set(cosmeticId, {
      cosmetic_id: cosmeticId,
      slot: str(row.slot),
      schema_version: Number(row.schema_version ?? 1),
      enabled: row.enabled === true,
      free_default: row.free_default === true,
    });
  }
  return catalog;
}

async function fetchAssetsByOwner(ownerAddress: string): Promise<JsonMap[]> {
  const all: JsonMap[] = [];
  for (let page = 1; page <= MAX_DAS_PAGES; page++) {
    const result = await dasRequest("getAssetsByOwner", {
      ownerAddress,
      page,
      limit: DEFAULT_DAS_PAGE_LIMIT,
      displayOptions: {
        showCollectionMetadata: false,
        showFungible: false,
        showNativeBalance: false,
        showGrandTotal: false,
      },
    });

    const items = Array.isArray(result?.items) ? result.items as JsonMap[] : [];
    all.push(...items);
    if (items.length < DEFAULT_DAS_PAGE_LIMIT) break;
  }
  return all;
}

async function dasRequest(method: string, params: JsonMap): Promise<JsonMap> {
  const response = await fetch(dasRpcUrl(), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: `chainbound-${method}`,
      method,
      params,
    }),
  });

  if (!response.ok) {
    throw new Error(`das_http_${response.status}`);
  }

  const payload = await response.json() as JsonMap;
  if (payload.error) {
    throw new Error(`das_rpc_error:${JSON.stringify(payload.error)}`);
  }

  return asRecord(payload.result);
}

function validateCoreCosmeticAsset(
  rawAsset: JsonMap,
  context: {
    wallet: string;
    expectedCollection: string;
    trustedAuthorities: Set<string>;
    catalog: Map<string, CatalogEntry>;
    verifiedAt: string;
  },
): ValidatedAsset {
  const asset = asRecord(rawAsset);
  const assetId = normalizePublicKey(asset.id);
  if (!assetId) return { ok: false, error: "asset_id_invalid" };

  const owner = normalizePublicKey(getPath(asset, ["ownership", "owner"]));
  if (owner !== context.wallet) return { ok: false, error: "asset_owner_mismatch" };

  const iface = str(asset.interface);
  if (iface !== "MplCoreAsset" && iface !== "CoreAsset") {
    return { ok: false, error: "asset_interface_not_core" };
  }

  const collection = findCollection(asset);
  if (collection !== context.expectedCollection) return { ok: false, error: "asset_collection_mismatch" };

  const updateAuthority = findUpdateAuthority(asset);
  if (!updateAuthority || !context.trustedAuthorities.has(updateAuthority)) {
    return { ok: false, error: "asset_update_authority_untrusted" };
  }

  const attributes = collectAttributes(asset);
  if (str(attributes["cb.kind"]) !== "cosmetic") return { ok: false, error: "asset_kind_not_cosmetic" };

  const cosmeticId = str(attributes["cb.cosmetic_id"]);
  const slot = str(attributes["cb.slot"]);
  const schemaVersion = Number(str(attributes["cb.schema_version"]) || "0");
  if (!cosmeticId || isFreeCosmeticId(cosmeticId)) return { ok: false, error: "asset_cosmetic_id_invalid" };
  if (!slot) return { ok: false, error: "asset_slot_missing" };
  if (!Number.isInteger(schemaVersion) || schemaVersion <= 0) return { ok: false, error: "asset_schema_invalid" };

  const catalogEntry = context.catalog.get(cosmeticId);
  if (!catalogEntry || !catalogEntry.enabled) return { ok: false, error: "asset_cosmetic_not_in_catalog" };
  if (catalogEntry.slot !== slot) return { ok: false, error: "asset_slot_catalog_mismatch" };
  if (catalogEntry.schema_version !== schemaVersion) return { ok: false, error: "asset_schema_catalog_mismatch" };

  return {
    ok: true,
    record: {
      asset_id: assetId,
      cosmetic_id: cosmeticId,
      slot,
      collection,
      update_authority: updateAuthority,
      schema_version: schemaVersion,
      verified_at: context.verifiedAt,
    },
  };
}

function cacheRowToRecord(row: JsonMap): CoreCosmeticRecord | null {
  const assetId = normalizePublicKey(row.asset_id);
  const cosmeticId = str(row.cosmetic_id);
  if (!assetId || !cosmeticId) return null;
  return {
    asset_id: assetId,
    cosmetic_id: cosmeticId,
    slot: str(row.slot),
    collection: normalizePublicKey(row.collection),
    update_authority: normalizePublicKey(row.update_authority),
    schema_version: Number(row.schema_version ?? 1),
    verified_at: str(row.last_verified_at),
  };
}

function mapAssetIdsByCosmeticId(records: CoreCosmeticRecord[]): Record<string, string> {
  const map: Record<string, string> = {};
  for (const record of records) {
    if (!map[record.cosmetic_id]) map[record.cosmetic_id] = record.asset_id;
  }
  return map;
}

function findCollection(asset: JsonMap): string {
  const grouping = Array.isArray(asset.grouping) ? asset.grouping as JsonMap[] : [];
  for (const group of grouping) {
    if (str(group.group_key) === "collection") {
      const value = normalizePublicKey(group.group_value);
      if (value) return value;
    }
  }
  return normalizePublicKey(getPath(asset, ["grouping", "collection"]));
}

function findUpdateAuthority(asset: JsonMap): string {
  const authorities = Array.isArray(asset.authorities) ? asset.authorities as JsonMap[] : [];
  for (const authority of authorities) {
    const address = normalizePublicKey(authority.address);
    if (address) return address;
  }
  return normalizePublicKey(asset.updateAuthority) ||
    normalizePublicKey(asset.update_authority);
}

function collectAttributes(root: unknown): Record<string, string> {
  const output: Record<string, string> = {};
  collectAttributesRecursive(root, output, 0);
  return output;
}

function collectAttributesRecursive(value: unknown, output: Record<string, string>, depth: number): void {
  if (depth > 8 || value == null) return;

  if (Array.isArray(value)) {
    for (const item of value) collectAttributesRecursive(item, output, depth + 1);
    return;
  }

  if (typeof value !== "object") return;
  const record = value as JsonMap;
  const key = str(record.trait_type) || str(record.key);
  const attrValue = str(record.value);
  if (key && attrValue) output[key] = attrValue;

  for (const childKey of ["attributes", "attributeList", "attribute_list", "metadata", "content", "plugins"]) {
    if (childKey in record) collectAttributesRecursive(record[childKey], output, depth + 1);
  }
}

function getPath(value: unknown, path: string[]): unknown {
  let current = value;
  for (const part of path) {
    if (!current || typeof current !== "object") return undefined;
    current = (current as JsonMap)[part];
  }
  return current;
}

function asRecord(value: unknown): JsonMap {
  return value && typeof value === "object" && !Array.isArray(value) ? value as JsonMap : {};
}
