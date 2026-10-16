import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { Connection, PublicKey } from "npm:@solana/web3.js@1.98.4";
import { JsonMap, SOLANA_BASE58_RE, str } from "./account_helpers.ts";
import { BindOwnershipConfig, CORE_PROGRAM_ID } from "./ownership_spine.ts";

const CHAINLING_ACCOUNT_DISCRIMINATOR = Uint8Array.from([126, 210, 27, 85, 136, 74, 53, 142]);
const U64_MAX = 18446744073709551615n;

export interface BindAssetTrust {
  asset_address: string;
  role: "cover" | "stamped_move" | "cosmetic";
  trusted: boolean;
  owner_address: string;
  collection_address: string;
  update_authority: string;
  kind: string;
  schema_version: number;
  reasons: string[];
}

export interface BindChainlingTrust {
  trusted: boolean;
  account_address: string;
  owner_address: string;
  onchain_chainling_id: string;
  is_bound: boolean;
  archive_asset_address: string;
  reasons: string[];
}

export interface BindSourceTrustReport {
  source_id: string;
  inspected_at: string;
  assets_trusted: boolean;
  source_integrity_trusted: boolean;
  selection_trusted: boolean;
  prepare_eligible: boolean;
  source_policy_reasons: string[];
  chainling: BindChainlingTrust;
  cover: BindAssetTrust;
  component: BindAssetTrust;
}

export interface ArchiveBindingProjection {
  chainling_id: string;
  archive_asset_id: string;
  archived_at_unix: number;
  bind_operation_id: string;
  bind_source_id: string;
  cover_asset_address: string;
  component_asset_address: string;
  component_kind: number;
  receipt_address: string;
  signature: string;
  cluster: string;
}

export async function inspectBindSourceTrust(args: {
  config: BindOwnershipConfig;
  source: JsonMap;
  profileChainlingOwned: boolean;
  fetchImpl?: typeof fetch;
}): Promise<BindSourceTrustReport> {
  const fetchImpl = args.fetchImpl ?? fetch;
  const componentKind = Number(args.source.component_kind);
  const [chainling, cover, component, coreProgramOwnership, archiveMetadataReasons] = await Promise.all([
    verifyBindChainlingAccount(args.config, args.source),
    fetchAndVerifyBindAsset(
      args.config,
      str(args.source.cover_asset_address),
      str(args.source.wallet_address),
      "cover",
      fetchImpl,
    ),
    fetchAndVerifyBindAsset(
      args.config,
      str(args.source.component_asset_address),
      str(args.source.wallet_address),
      componentKind === 0 ? "stamped_move" : "cosmetic",
      fetchImpl,
    ),
    verifyCoreAssetProgramOwners(args.config, args.source),
    fetchAndVerifyArchiveMetadataSnapshot(args.config, args.source, fetchImpl),
  ]);
  if (!coreProgramOwnership.cover) cover.reasons.push("core_program_owner_mismatch");
  if (!coreProgramOwnership.component) component.reasons.push("core_program_owner_mismatch");
  cover.reasons = Array.from(new Set(cover.reasons));
  component.reasons = Array.from(new Set(component.reasons));
  cover.trusted = cover.reasons.length === 0;
  component.trusted = component.reasons.length === 0;

  const sourcePolicyReasons: string[] = [];
  sourcePolicyReasons.push(...archiveMetadataReasons);
  if (!args.profileChainlingOwned) sourcePolicyReasons.push("chainling_not_owned_by_profile");
  if (!/^[0-9a-f]{64}$/.test(str(args.source.dossier_snapshot_hash))) {
    sourcePolicyReasons.push("dossier_snapshot_hash_missing");
  }
  if (!/^[0-9a-f]{64}$/.test(str(args.source.archive_metadata_hash))) {
    sourcePolicyReasons.push("archive_metadata_hash_missing");
  }
  const provenance = asRecord(args.source.dossier_provenance);
  if (!str(provenance.authority) || !str(provenance.authority_record_id) || !str(provenance.snapshot_version)) {
    sourcePolicyReasons.push("dossier_provenance_incomplete");
  }
  const expiresAt = Date.parse(str(args.source.expires_at));
  if (!Number.isFinite(expiresAt) || expiresAt <= Date.now()) sourcePolicyReasons.push("source_expired");
  if (args.source.revoked_at) sourcePolicyReasons.push("source_revoked");
  if (args.source.consumed_at) sourcePolicyReasons.push("source_consumed");
  if (str(args.source.cluster) !== args.config.cluster) sourcePolicyReasons.push("source_cluster_mismatch");
  if (componentKind !== 0 && componentKind !== 1) sourcePolicyReasons.push("component_kind_invalid");

  const assetsTrusted = chainling.trusted && cover.trusted && component.trusted;
  const sourceIntegrityTrusted = sourcePolicyReasons.length === 0;
  const selectionTrusted = assetsTrusted && sourceIntegrityTrusted;
  return {
    source_id: str(args.source.source_id),
    inspected_at: new Date().toISOString(),
    assets_trusted: assetsTrusted,
    source_integrity_trusted: sourceIntegrityTrusted,
    selection_trusted: selectionTrusted,
    prepare_eligible: selectionTrusted && args.source.enabled === true,
    source_policy_reasons: sourcePolicyReasons,
    chainling,
    cover,
    component,
  };
}

export async function fetchAndVerifyArchiveMetadataSnapshot(
  config: BindOwnershipConfig,
  source: JsonMap,
  fetchImpl: typeof fetch = fetch,
): Promise<string[]> {
  const uri = str(source.archive_uri);
  const expectedHash = str(source.archive_metadata_hash);
  const base = config.metadataUriBase.replace(/\/+$/, "");
  try {
    const baseUrl = new URL(`${base}/`);
    const targetUrl = new URL(uri);
    if (
      targetUrl.protocol !== "https:" || targetUrl.origin !== baseUrl.origin ||
      !targetUrl.pathname.startsWith(baseUrl.pathname) || targetUrl.username || targetUrl.password
    ) {
      return ["archive_metadata_uri_outside_configured_base"];
    }
  } catch {
    return ["archive_metadata_uri_outside_configured_base"];
  }
  if (!/^[0-9a-f]{64}$/.test(expectedHash)) return ["archive_metadata_hash_missing"];
  try {
    const response = await fetchImpl(uri, { method: "GET", headers: { accept: "application/json" } });
    if (!response.ok) return ["archive_metadata_fetch_failed"];
    const declaredLength = Number(response.headers.get("content-length") ?? 0);
    if (declaredLength > 65_536) return ["archive_metadata_too_large"];
    const bytes = await readResponseBytes(response, 65_536);
    if (!bytes) return ["archive_metadata_too_large"];
    if (bytes.length === 0) return ["archive_metadata_too_large"];
    try {
      const decoded = JSON.parse(new TextDecoder().decode(bytes));
      if (!decoded || typeof decoded !== "object" || Array.isArray(decoded)) {
        return ["archive_metadata_invalid_json"];
      }
    } catch {
      return ["archive_metadata_invalid_json"];
    }
    const hashInput = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength) as ArrayBuffer;
    const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", hashInput));
    const actualHash = Array.from(digest).map((value) => value.toString(16).padStart(2, "0")).join("");
    return actualHash === expectedHash ? [] : ["archive_metadata_hash_mismatch"];
  } catch {
    return ["archive_metadata_fetch_failed"];
  }
}

async function readResponseBytes(response: Response, limit: number): Promise<Uint8Array | null> {
  if (!response.body) return new Uint8Array();
  const reader = response.body.getReader();
  const chunks: Uint8Array[] = [];
  let total = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      total += value.byteLength;
      if (total > limit) {
        await reader.cancel("archive_metadata_too_large");
        return null;
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }
  const result = new Uint8Array(total);
  let offset = 0;
  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return result;
}

export async function inspectionDigest(report: BindSourceTrustReport): Promise<string> {
  const bytes = new TextEncoder().encode(JSON.stringify(report));
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", bytes));
  return Array.from(digest).map((value) => value.toString(16).padStart(2, "0")).join("");
}

export async function fetchAndVerifyBindAsset(
  config: BindOwnershipConfig,
  assetAddress: string,
  expectedOwner: string,
  role: BindAssetTrust["role"],
  fetchImpl: typeof fetch = fetch,
): Promise<BindAssetTrust> {
  const fallback = untrustedAsset(assetAddress, role, "asset_not_found");
  if (!SOLANA_BASE58_RE.test(assetAddress) || !SOLANA_BASE58_RE.test(expectedOwner)) {
    return untrustedAsset(assetAddress, role, "invalid_asset_or_owner_address");
  }
  let response: Response;
  try {
    response = await fetchImpl(config.dasUrl, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: "chainbound-bindery-inspection",
        method: "getAsset",
        params: { id: assetAddress },
      }),
      signal: AbortSignal.timeout(10_000),
    });
  } catch {
    throw new Error("bind_inventory_das_unavailable");
  }
  if (!response.ok) throw new Error("bind_inventory_das_unavailable");
  const payload = await response.json() as JsonMap;
  if (payload.error || !payload.result || typeof payload.result !== "object") return fallback;
  return verifyBindAssetPayload(config, payload.result as JsonMap, assetAddress, expectedOwner, role);
}

export function verifyBindAssetPayload(
  config: BindOwnershipConfig,
  asset: JsonMap,
  assetAddress: string,
  expectedOwner: string,
  role: BindAssetTrust["role"],
): BindAssetTrust {
  const owner = normalizeKey((asset.ownership as JsonMap | undefined)?.owner);
  const collection = findCollection(asset);
  const updateAuthority = findAuthority(asset);
  const attrs = collectAttributes(asset.plugins);
  const interfaceName = str(asset.interface).toLowerCase();
  const content = asRecord(asset.content);
  const metadata = asRecord(content.metadata);
  const core = interfaceName.includes("core") || str(metadata.interface).toLowerCase().includes("core");
  const schemaVersion = Number(attrs["cb.schema_version"] ?? 0);
  const reasons: string[] = [];
  const expectedCollection = role === "cover"
    ? config.coversCollection.toBase58()
    : role === "stamped_move"
    ? config.movesCollection.toBase58()
    : config.cosmeticsCollection.toBase58();
  const expectedKind = role === "cover" ? "book_cover" : role === "stamped_move" ? "stamped_move" : "cosmetic";

  if (!core) reasons.push("not_core_asset");
  if (normalizeKey(asset.id) !== assetAddress) reasons.push("asset_address_mismatch");
  if (owner !== expectedOwner) reasons.push("owner_mismatch");
  if (collection !== expectedCollection) reasons.push("collection_mismatch");
  const authorityTrusted = role === "cosmetic"
    ? config.cosmeticsUpdateAuthorities.has(updateAuthority)
    : updateAuthority === config.movesUpdateAuthority.toBase58();
  if (!authorityTrusted) reasons.push("update_authority_mismatch");
  if (attrs["cb.kind"] !== expectedKind) reasons.push("kind_mismatch");
  if (schemaVersion !== config.schemaVersion) reasons.push("schema_version_mismatch");
  if (role === "cover" && !attrs["cb.cover_id"]) reasons.push("cover_id_missing");
  if (role === "cosmetic" && (!attrs["cb.cosmetic_id"] || !attrs["cb.slot"])) {
    reasons.push("cosmetic_identity_missing");
  }
  if (role === "stamped_move") {
    const modifierCount = Number(attrs["cb.modifier_count"] ?? 0);
    if (attrs["cb.move_tier"] !== "stamped") reasons.push("move_tier_mismatch");
    if (Number(attrs["cb.generator_version"] ?? 0) !== config.generatorVersion) {
      reasons.push("generator_version_mismatch");
    }
    if (attrs["cb.pool_hash"] !== config.modifierPoolHash) reasons.push("pool_hash_mismatch");
    if ((modifierCount !== 1 && modifierCount !== 2) || !attrs["cb.modifier_1"]) {
      reasons.push("modifier_shape_invalid");
    }
    if (modifierCount === 2 && (!attrs["cb.modifier_2"] || attrs["cb.modifier_2"] === attrs["cb.modifier_1"])) {
      reasons.push("modifier_shape_invalid");
    }
  }

  return {
    asset_address: assetAddress,
    role,
    trusted: reasons.length === 0,
    owner_address: owner,
    collection_address: collection,
    update_authority: updateAuthority,
    kind: attrs["cb.kind"] ?? "",
    schema_version: schemaVersion,
    reasons: Array.from(new Set(reasons)),
  };
}

async function verifyCoreAssetProgramOwners(
  config: BindOwnershipConfig,
  source: JsonMap,
): Promise<{ cover: boolean; component: boolean }> {
  try {
    const cover = new PublicKey(str(source.cover_asset_address));
    const component = new PublicKey(str(source.component_asset_address));
    const connection = new Connection(config.rpcUrl, "confirmed");
    const [coverInfo, componentInfo] = await connection.getMultipleAccountsInfo([cover, component], "confirmed");
    return {
      cover: coverInfo?.owner.equals(new PublicKey(CORE_PROGRAM_ID)) === true,
      component: componentInfo?.owner.equals(new PublicKey(CORE_PROGRAM_ID)) === true,
    };
  } catch {
    return { cover: false, component: false };
  }
}

export async function loadArchiveBindingProjection(
  supabase: SupabaseClient,
  playerProfileId: string,
): Promise<Map<string, ArchiveBindingProjection>> {
  const { data, error } = await supabase.rpc("ownership_archive_binding_projection_service", {
    p_player_profile_id: playerProfileId,
  });
  if (error) throw error;
  const result = new Map<string, ArchiveBindingProjection>();
  for (const raw of data ?? []) {
    const row = raw as JsonMap;
    const chainlingId = str(row.chainling_id);
    if (!chainlingId) continue;
    result.set(chainlingId, {
      chainling_id: chainlingId,
      archive_asset_id: str(row.archive_asset_id),
      archived_at_unix: finiteInteger(row.archived_at_unix),
      bind_operation_id: str(row.bind_operation_id),
      bind_source_id: str(row.bind_source_id),
      cover_asset_address: str(row.cover_asset_address),
      component_asset_address: str(row.component_asset_address),
      component_kind: finiteInteger(row.component_kind),
      receipt_address: str(row.receipt_address),
      signature: str(row.signature),
      cluster: str(row.cluster),
    });
  }
  return result;
}

export function archiveBindingFields(binding: ArchiveBindingProjection | undefined): JsonMap {
  return binding
    ? {
      is_bound: true,
      archive_asset_id: binding.archive_asset_id,
      archived_at_unix: binding.archived_at_unix,
      archive_binding: binding,
    }
    : { is_bound: false, archive_asset_id: "", archived_at_unix: 0, archive_binding: null };
}

async function verifyBindChainlingAccount(
  config: BindOwnershipConfig,
  source: JsonMap,
): Promise<BindChainlingTrust> {
  const reasons: string[] = [];
  const wallet = str(source.wallet_address);
  const idText = String(source.onchain_chainling_id ?? "");
  let owner: PublicKey;
  let chainlingId: bigint;
  try {
    owner = new PublicKey(wallet);
    chainlingId = BigInt(idText);
    if (chainlingId < 0n || chainlingId > U64_MAX) throw new Error("range");
  } catch {
    return {
      trusted: false,
      account_address: "",
      owner_address: wallet,
      onchain_chainling_id: idText,
      is_bound: false,
      archive_asset_address: "",
      reasons: ["invalid_chainling_identity"],
    };
  }
  const idBytes = u64(chainlingId);
  const [account] = PublicKey.findProgramAddressSync(
    [new TextEncoder().encode("chainling_account"), owner.toBytes(), idBytes],
    config.programId,
  );
  const [archive] = PublicKey.findProgramAddressSync(
    [new TextEncoder().encode("archive_asset"), owner.toBytes(), idBytes],
    config.programId,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [new TextEncoder().encode("bind_receipt"), owner.toBytes(), idBytes],
    config.programId,
  );
  const connection = new Connection(config.rpcUrl, "confirmed");
  const [info, archiveInfo, receiptInfo] = await connection.getMultipleAccountsInfo(
    [account, archive, receipt],
    "confirmed",
  );
  if (!info) reasons.push("chainling_account_missing");
  let isBound = false;
  let archiveAssetAddress = "";
  if (info) {
    const data = new Uint8Array(info.data);
    if (!info.owner.equals(config.programId)) reasons.push("chainling_program_owner_mismatch");
    if (data.length < 48 || !equalBytes(data.slice(0, 8), CHAINLING_ACCOUNT_DISCRIMINATOR)) {
      reasons.push("chainling_discriminator_mismatch");
    } else {
      if (!equalBytes(data.slice(8, 40), owner.toBytes())) reasons.push("chainling_owner_mismatch");
      if (readU64(data, 40) !== chainlingId) reasons.push("chainling_id_mismatch");
      try {
        const decoded = decodeChainlingTerminalFields(data);
        isBound = decoded.isBound;
        archiveAssetAddress = decoded.archiveAssetAddress;
        if (isBound) reasons.push("chainling_already_bound");
      } catch {
        reasons.push("chainling_data_invalid");
      }
    }
  }
  if (archiveInfo || receiptInfo) reasons.push("bind_output_already_exists");
  return {
    trusted: reasons.length === 0,
    account_address: account.toBase58(),
    owner_address: wallet,
    onchain_chainling_id: chainlingId.toString(),
    is_bound: isBound,
    archive_asset_address: archiveAssetAddress,
    reasons,
  };
}

function decodeChainlingTerminalFields(data: Uint8Array): { isBound: boolean; archiveAssetAddress: string } {
  let offset = 48;
  offset = skipString(data, offset); // species_id
  offset += 2 + 6 + 8 + 1 + 1; // level, IVs, seed, mutation/entangled flags
  offset = skipString(data, offset); // base_visual_variant_id
  offset = skipString(data, offset); // entangled_type
  offset = skipStringVector(data, offset); // mutation_ids
  const knownMoveCount = readU32(data, offset);
  offset += 4 + knownMoveCount * 2;
  requireRange(data, offset, 34);
  return {
    isBound: data[offset] !== 0,
    archiveAssetAddress: new PublicKey(data.slice(offset + 1, offset + 33)).toBase58(),
  };
}

function skipStringVector(data: Uint8Array, offset: number): number {
  const count = readU32(data, offset);
  offset += 4;
  for (let index = 0; index < count; index++) offset = skipString(data, offset);
  return offset;
}

function skipString(data: Uint8Array, offset: number): number {
  const length = readU32(data, offset);
  requireRange(data, offset + 4, length);
  return offset + 4 + length;
}

function readU32(data: Uint8Array, offset: number): number {
  requireRange(data, offset, 4);
  return new DataView(data.buffer, data.byteOffset, data.byteLength).getUint32(offset, true);
}

function readU64(data: Uint8Array, offset: number): bigint {
  requireRange(data, offset, 8);
  return new DataView(data.buffer, data.byteOffset, data.byteLength).getBigUint64(offset, true);
}

function requireRange(data: Uint8Array, offset: number, length: number): void {
  if (offset < 0 || length < 0 || offset + length > data.length) throw new Error("out_of_range");
}

function u64(value: bigint): Uint8Array {
  const out = new Uint8Array(8);
  new DataView(out.buffer).setBigUint64(0, value, true);
  return out;
}

function untrustedAsset(assetAddress: string, role: BindAssetTrust["role"], reason: string): BindAssetTrust {
  return {
    asset_address: assetAddress,
    role,
    trusted: false,
    owner_address: "",
    collection_address: "",
    update_authority: "",
    kind: "",
    schema_version: 0,
    reasons: [reason],
  };
}

function normalizeKey(value: unknown): string {
  const key = str(value);
  return SOLANA_BASE58_RE.test(key) ? key : "";
}

function findCollection(asset: JsonMap): string {
  const grouping = Array.isArray(asset.grouping) ? asset.grouping as JsonMap[] : [];
  for (const item of grouping) {
    if (str(item.group_key).toLowerCase().includes("collection")) {
      const key = normalizeKey(item.group_value);
      if (key) return key;
    }
  }
  return normalizeKey((asset.collection as JsonMap | undefined)?.address);
}

function findAuthority(asset: JsonMap): string {
  const authorities = Array.isArray(asset.authorities) ? asset.authorities as JsonMap[] : [];
  for (const authority of authorities) {
    const key = normalizeKey(authority.address);
    if (key) return key;
  }
  return normalizeKey(asset.update_authority) || normalizeKey(asset.updateAuthority);
}

function collectAttributes(value: unknown, out: Record<string, string> = {}, depth = 0): Record<string, string> {
  if (depth > 8 || value == null) return out;
  if (Array.isArray(value)) {
    for (const item of value) collectAttributes(item, out, depth + 1);
    return out;
  }
  if (typeof value !== "object") return out;
  const record = value as JsonMap;
  const key = str(record.trait_type) || str(record.key);
  const attribute = str(record.value);
  if (key && attribute) out[key] = attribute;
  for (const child of ["attributes", "attributeList", "attribute_list", "metadata", "content", "plugins"]) {
    if (child in record) collectAttributes(record[child], out, depth + 1);
  }
  return out;
}

function asRecord(value: unknown): JsonMap {
  return value !== null && typeof value === "object" && !Array.isArray(value) ? value as JsonMap : {};
}

function equalBytes(a: Uint8Array, b: Uint8Array): boolean {
  return a.length === b.length && a.every((value, index) => value === b[index]);
}

function finiteInteger(value: unknown): number {
  const parsed = typeof value === "number" ? value : Number(value);
  return Number.isFinite(parsed) ? Math.trunc(parsed) : 0;
}

export function coreProgramIdForTrustTests(): string {
  return CORE_PROGRAM_ID;
}
