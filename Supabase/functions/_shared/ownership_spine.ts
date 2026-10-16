import {
  Connection,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} from "npm:@solana/web3.js@1.98.4";
import * as bs58Module from "npm:bs58@5.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import { JsonMap, SOLANA_BASE58_RE, str } from "./account_helpers.ts";
import { MODIFIER_EFFECT_BOUNDS, MODIFIER_POOL } from "./modifier_pool.generated.ts";
import {
  HELIUS_DAS_URL_ENV,
  OwnershipCluster,
  requireHeliusDasUrl,
  requireOwnershipCluster,
} from "./ownership_das_proxy.ts";

export {
  EXPECTED_MODIFIER_POOL_HASH,
  EXPECTED_MODIFIER_POOL_ROW_COUNT,
  MODIFIER_EFFECT_BOUNDS,
  MODIFIER_POOL,
} from "./modifier_pool.generated.ts";

export const CHAINBOUND_PROGRAM_ID = "9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ";
export const CORE_PROGRAM_ID = "CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d";
export const CORE_AUTHORITY_PDA = "FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys";
export const MAINNET_SMOKE_ENABLED_ENV = "CHAINBOUND_MAINNET_SMOKE_ENABLED";
export const MAINNET_SMOKE_WALLETS_ENV = "CHAINBOUND_MAINNET_SMOKE_WALLETS";
export const DEVNET_GENESIS_HASH = "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG";
export const MAINNET_GENESIS_HASH = "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d";
export const KNOWN_ROLL_RAM_COST = 5;
export const FIRST_STAMP_RAM_COST = 75;
export const SECOND_STAMP_RAM_COST_PIN = 150;
export const BIND_RAM_COST_PIN = 250;
export const RAM_DAILY_CAP = 100;
export const DEVNET_SMOKE_WALLETS_ENV = "CHAINBOUND_DEVNET_SMOKE_WALLETS";
export const LEGACY_DEVNET_SMOKE_WALLET_ENV = "CHAINBOUND_DEVNET_SMOKE_WALLET";

const ROLL_KNOWN_DISCRIMINATOR = bytes(42, 140, 118, 198, 71, 101, 152, 152);
const CREATE_RAM_RESERVATION_DISCRIMINATOR = bytes(170, 252, 246, 81, 3, 188, 79, 36);
const CANCEL_RAM_RESERVATION_DISCRIMINATOR = bytes(187, 16, 38, 226, 202, 185, 140, 135);
const DEBIT_RESERVED_RAM_DISCRIMINATOR = bytes(33, 31, 171, 238, 31, 94, 205, 157);
const RECONCILE_RAM_DELTA_DISCRIMINATOR = bytes(0, 109, 140, 149, 153, 119, 226, 15);
const GRANT_RAM_DISCRIMINATOR = bytes(178, 154, 17, 238, 34, 122, 206, 163);
const PREPARE_STAMP_DISCRIMINATOR = bytes(244, 254, 92, 75, 125, 49, 21, 158);
const STAMP_KNOWN_DISCRIMINATOR = bytes(169, 191, 199, 46, 62, 34, 188, 181);
const PREPARE_PROFILE_STAMP_DISCRIMINATOR = bytes(14, 195, 234, 21, 25, 158, 57, 144);
const STAMP_PROFILE_KNOWN_DISCRIMINATOR = bytes(158, 107, 90, 125, 109, 128, 171, 210);
const BIND_CHAINLING_DISCRIMINATOR = bytes(181, 19, 166, 249, 245, 197, 66, 176);
const GAME_CONFIG_DISCRIMINATOR = bytes(45, 146, 146, 33, 170, 69, 96, 133);
const PLAYER_PROFILE_DISCRIMINATOR = bytes(82, 226, 99, 87, 164, 130, 181, 80);
const CHAINLING_ACCOUNT_DISCRIMINATOR = bytes(126, 210, 27, 85, 136, 74, 53, 142);
const MOVE_VAULT_DISCRIMINATOR = bytes(30, 75, 22, 221, 63, 126, 196, 29);
const RAM_GRANT_RECEIPT_DISCRIMINATOR = bytes(151, 111, 207, 251, 59, 36, 69, 29);
const KNOWN_ROLL_RECEIPT_DISCRIMINATOR = bytes(168, 108, 149, 105, 64, 120, 128, 147);
const RAM_RESERVATION_DISCRIMINATOR = bytes(171, 186, 0, 107, 1, 255, 131, 148);
const RAM_DEBIT_RECEIPT_DISCRIMINATOR = bytes(158, 73, 117, 135, 247, 44, 34, 129);
const RAM_RECONCILIATION_RECEIPT_DISCRIMINATOR = bytes(242, 112, 40, 70, 115, 180, 87, 215);
const STAMP_TICKET_DISCRIMINATOR = bytes(27, 188, 81, 247, 225, 23, 30, 219);
const BIND_RECEIPT_DISCRIMINATOR = bytes(178, 55, 40, 1, 22, 166, 212, 254);
export const STAMP_METADATA_RENDER_VERSION = 6;

// Server-owned Phase 1 pool. IDs and powers are canonical Moves.csv values;
// the numeric ID is the same FNV-1a truncation used by MoveDatabaseSO.
const KNOWN_POOL = [
  { moveId: "baseline_striker", element: 0, basePower: 40 },
  { moveId: "baseline_slam", element: 0, basePower: 40 },
  { moveId: "baseline_jab", element: 0, basePower: 20 },
  { moveId: "baseline_shot", element: 0, basePower: 35 },
  { moveId: "baseline_snipe", element: 0, basePower: 40 },
  { moveId: "baseline_volley", element: 0, basePower: 20 },
  { moveId: "baseline_blast", element: 0, basePower: 30 },
  { moveId: "baseline_barrage", element: 0, basePower: 13 },
  { moveId: "baseline_thrust", element: 0, basePower: 35 },
  { moveId: "baseline_multi_strike", element: 0, basePower: 10 },
] as const;

/** Event/boss Known grants outside the candy roll pool. Keys must match Moves.csv move ids. */
export const EVENT_KNOWN_GRANT_CATALOG: Readonly<Record<string, { element: number; basePower: number }>> = {
  bean_sip: { element: 9, basePower: 0 },
};

export function resolveEventKnownAuthority(moveId: string): KnownAuthorityResult {
  const normalized = (moveId ?? "").trim();
  const entry = EVENT_KNOWN_GRANT_CATALOG[normalized];
  if (!entry) throw new OwnershipConfigurationError("event_known_move_not_allowed");
  return {
    moveId: normalized,
    onchainMoveId: hashMoveId(normalized),
    element: entry.element,
    basePower: entry.basePower,
  };
}

export interface OwnershipConfig {
  cluster: OwnershipCluster;
  rpcUrl: string;
  expectedGenesisHash: string;
  dasUrl: string;
  programId: PublicKey;
  movesCollection: PublicKey;
  movesUpdateAuthority: PublicKey;
  resultAuthority: Keypair;
  generatorVersion: number;
  modifierPoolHash: string;
  ticketTtlSeconds: number;
  stampRamCost: number;
  schemaVersion: number;
  metadataUriBase: string;
}

export interface BindOwnershipConfig extends OwnershipConfig {
  coversCollection: PublicKey;
  cosmeticsCollection: PublicKey;
  cosmeticsUpdateAuthorities: ReadonlySet<string>;
  archivesCollection: PublicKey;
}

export interface DevnetSmokeGrantConfig {
  cluster: string;
  rpcUrl: string;
  programId: PublicKey;
  resultAuthority: Keypair;
  smokeWallets: ReadonlySet<string>;
}

export interface KnownAuthorityResult {
  moveId: string;
  onchainMoveId: number;
  element: number;
  basePower: number;
}

export interface SettledKnownRoll {
  operationId: string;
  signature: string;
  receiptAddress: string;
  knownInstanceId: string;
}

export interface StampAuthorityResult {
  modifierIds: string[];
  rarity: number;
  modifierEffectValues: number[];
}

export interface BuiltStampTransaction {
  transactionBase64: string;
  recentBlockhash: string;
  ticketAddress: string;
  stampedAssetAddress: string;
  onchainInstanceId: bigint;
  estimatedNetworkFeeLamports: number;
  expiresAt: Date;
  chainRamBalance: number;
  chainRamRequired: number;
  reservationAddress: string;
  knownSource: "legacy_onchain" | "profile";
}

export interface BuiltBindTransaction {
  transactionBase64: string;
  recentBlockhash: string;
  lastValidBlockHeight: number;
  reservationAddress: string;
  chainlingAddress: string;
  archiveAssetAddress: string;
  receiptAddress: string;
  estimatedNetworkFeeLamports: number;
  expiresAt: Date;
}

export interface VerifiedBindReceipt {
  receiptAddress: string;
  ownerAddress: string;
  onchainChainlingId: string;
  operationHash: string;
  coverAssetAddress: string;
  componentAssetAddress: string;
  componentKind: number;
  archiveAssetAddress: string;
}

export interface EnsuredRamReservation {
  address: string;
  signature: string;
  recovered: boolean;
}

export interface SettledRamDebit {
  signature: string;
  receiptAddress: string;
  balanceAfter: number;
}

export interface SettledRamReconciliation {
  signature: string;
  receiptAddress: string;
  balanceBefore: number;
  balanceAfter: number;
  recovered: boolean;
}

export type SignatureOutcome = "missing" | "pending" | "confirmed" | "failed";
export type SubmittedTransactionOutcome = SignatureOutcome | "mismatch";

export interface BuiltRamGrantTransaction {
  transactionBase64: string;
  recentBlockhash: string;
  playerProfileAddress: string;
  moveVaultAddress: string;
  receiptAddress: string;
  amount: number;
  estimatedNetworkFeeLamports: number;
}

/**
 * Server-signed, operator-only recovery transaction. This is deliberately
 * separate from the player-signed fresh-profile smoke grant: it exists solely
 * to reconcile a previously split Devnet test ledger without weakening the
 * normal player-facing grant preconditions.
 */
export interface BuiltOperatorRamTopupTransaction extends BuiltRamGrantTransaction {
  transaction: Transaction;
  lastValidBlockHeight: number;
  balanceBefore: number;
}

export interface VerifiedRamGrant {
  receiptAddress: string;
  ownerAddress: string;
  resultAuthority: string;
  idempotencyHash: string;
  amount: number;
  balanceAfter: number;
  dailyRamEarned: number;
  lifetimeRamEarned: bigint;
  utcDay: bigint;
}

export interface VerifiedStampedAsset {
  assetAddress: string;
  ownerAddress: string;
  collectionAddress: string;
  updateAuthority: string;
  moveId: string;
  knownOnchainInstanceId: string;
  rarity: string;
  modifier1Id: string;
  modifier2Id: string;
  modifierCount: number;
  schemaVersion: number;
  generatorVersion: number;
  poolHash: string;
  isCoreAsset: boolean;
}

export interface VerifiedStampReceipt extends VerifiedStampedAsset {
  ticketAddress: string;
  idempotencyHash: string;
  assetUri: string;
}

export class OwnershipConfigurationError extends Error {
  constructor(public readonly code: string) {
    super(code);
  }
}

export function loadRollConfig(): Pick<
  OwnershipConfig,
  "cluster" | "resultAuthority" | "generatorVersion" | "modifierPoolHash" | "schemaVersion"
> {
  const cluster = loadOwnershipCluster();
  return {
    cluster,
    resultAuthority: readResultAuthority(requiredEnv("CHAINBOUND_RESULT_AUTHORITY_SECRET")),
    generatorVersion: positiveIntEnv("CHAINBOUND_OWNERSHIP_GENERATOR_VERSION"),
    modifierPoolHash: hashEnv("CHAINBOUND_OWNERSHIP_MODIFIER_POOL_HASH"),
    schemaVersion: positiveIntEnv("CHAINBOUND_OWNERSHIP_SCHEMA_VERSION"),
  };
}

export function loadOwnershipConfig(): OwnershipConfig {
  const roll = loadRollConfig();
  const cluster = roll.cluster;
  const programText = requiredEnv("CHAINBOUND_GAME_PROGRAM_ID");
  if (cluster === "devnet" && programText !== CHAINBOUND_PROGRAM_ID) {
    throw new OwnershipConfigurationError("unexpected_chainbound_program_id");
  }
  if (cluster === "mainnet-beta" && programText === CHAINBOUND_PROGRAM_ID) {
    throw new OwnershipConfigurationError("mainnet_program_must_be_isolated");
  }
  const programId = publicKey(programText, "invalid_chainbound_program_id");
  const authorityText = requiredEnv("CHAINBOUND_CORE_MOVES_UPDATE_AUTHORITY");
  const [derivedCoreAuthority] = PublicKey.findProgramAddressSync([utf8("core_authority")], programId);
  if (authorityText !== derivedCoreAuthority.toBase58()) {
    throw new OwnershipConfigurationError("unexpected_moves_update_authority");
  }
  if (cluster === "devnet" && authorityText !== CORE_AUTHORITY_PDA) {
    throw new OwnershipConfigurationError("unexpected_moves_update_authority");
  }
  const metadataUriBase = requiredEnv("CHAINBOUND_OWNERSHIP_METADATA_URI_BASE").replace(/\/+$/, "");
  if (!metadataUriBase.startsWith("https://")) throw new OwnershipConfigurationError("metadata_uri_base_must_be_https");
  const stampRamCost = positiveIntEnv("CHAINBOUND_OWNERSHIP_STAMP_RAM_COST");
  if (stampRamCost !== FIRST_STAMP_RAM_COST) throw new OwnershipConfigurationError("unexpected_stamp_ram_cost");
  const bindRamCost = positiveIntEnv("CHAINBOUND_OWNERSHIP_BIND_RAM_COST");
  if (bindRamCost !== BIND_RAM_COST_PIN) throw new OwnershipConfigurationError("unexpected_bind_ram_cost");
  return {
    ...roll,
    cluster,
    rpcUrl: requireHttpsRpcUrl(requiredEnv("CHAINBOUND_SOLANA_RPC_ENDPOINT")),
    expectedGenesisHash: expectedGenesisHashForCluster(cluster),
    // Stamped-asset confirmation requires DAS, not plain Solana JSON-RPC.
    // Reuse the same validated server-only Devnet Helius endpoint as the
    // authenticated read proxy; the value is never returned to Unity.
    dasUrl: requireHeliusDasUrl(Deno.env.get(HELIUS_DAS_URL_ENV), cluster),
    programId,
    movesCollection: publicKey(requiredEnv("CHAINBOUND_CORE_MOVES_COLLECTION"), "moves_collection_unset_or_invalid"),
    movesUpdateAuthority: publicKey(authorityText, "invalid_moves_update_authority"),
    ticketTtlSeconds: positiveIntEnv("CHAINBOUND_OWNERSHIP_TICKET_TTL_SECONDS"),
    stampRamCost,
    metadataUriBase,
  };
}

export function loadBindConfig(): BindOwnershipConfig {
  return {
    ...loadOwnershipConfig(),
    coversCollection: publicKey(
      requiredEnv("CHAINBOUND_CORE_COVERS_COLLECTION"),
      "covers_collection_unset_or_invalid",
    ),
    cosmeticsCollection: publicKey(
      requiredEnv("CHAINBOUND_CORE_COSMETICS_COLLECTION"),
      "cosmetics_collection_unset_or_invalid",
    ),
    cosmeticsUpdateAuthorities: parseCoreUpdateAuthorityAllowlist(
      requiredEnv("CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES"),
    ),
    archivesCollection: publicKey(
      requiredEnv("CHAINBOUND_CORE_ARCHIVES_COLLECTION"),
      "archives_collection_unset_or_invalid",
    ),
  };
}

export function expectedGenesisHashForCluster(cluster: OwnershipCluster): string {
  return cluster === "mainnet-beta" ? MAINNET_GENESIS_HASH : DEVNET_GENESIS_HASH;
}

export function loadMainnetSmokeWallets(): ReadonlySet<string> {
  return parseWalletAllowlist(
    requiredEnv(MAINNET_SMOKE_WALLETS_ENV),
    "invalid_mainnet_smoke_wallets",
  );
}

export function isOwnershipWalletAllowed(cluster: OwnershipCluster, walletAddress: string): boolean {
  if (cluster !== "mainnet-beta") return true;
  const normalized = publicKey(walletAddress, "invalid_wallet_public_key").toBase58();
  return loadMainnetSmokeWallets().has(normalized);
}

const verifiedRpcGenesis = new Map<string, Promise<void>>();

export async function ensureOwnershipRpcCluster(
  config: Pick<OwnershipConfig, "cluster" | "rpcUrl" | "expectedGenesisHash">,
  fetchImpl: typeof fetch = fetch,
): Promise<void> {
  const cacheKey = `${config.cluster}|${config.rpcUrl}|${config.expectedGenesisHash}`;
  let pending = verifiedRpcGenesis.get(cacheKey);
  if (!pending) {
    pending = verifyRpcGenesis(config, fetchImpl);
    verifiedRpcGenesis.set(cacheKey, pending);
  }
  try {
    await pending;
  } catch (error) {
    verifiedRpcGenesis.delete(cacheKey);
    throw error;
  }
}

async function verifyRpcGenesis(
  config: Pick<OwnershipConfig, "rpcUrl" | "expectedGenesisHash">,
  fetchImpl: typeof fetch,
): Promise<void> {
  let response: Response;
  try {
    response = await fetchImpl(config.rpcUrl, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ jsonrpc: "2.0", id: 1, method: "getGenesisHash" }),
    });
  } catch {
    throw new OwnershipConfigurationError("solana_rpc_genesis_check_failed");
  }
  if (!response.ok) throw new OwnershipConfigurationError("solana_rpc_genesis_check_failed");
  const payload = await response.json() as { error?: unknown; result?: unknown };
  if (payload.error || typeof payload.result !== "string") {
    throw new OwnershipConfigurationError("solana_rpc_genesis_check_failed");
  }
  if (payload.result !== config.expectedGenesisHash) {
    throw new OwnershipConfigurationError("solana_rpc_cluster_mismatch");
  }
}

function requireHttpsRpcUrl(value: string): string {
  try {
    const url = new URL(value);
    if (url.protocol !== "https:") throw new Error("not_https");
    return url.toString();
  } catch {
    throw new OwnershipConfigurationError("invalid_solana_rpc_endpoint");
  }
}

function loadOwnershipCluster(): OwnershipCluster {
  let cluster: OwnershipCluster;
  try {
    cluster = requireOwnershipCluster(Deno.env.get("CHAINBOUND_SOLANA_CLUSTER"));
  } catch {
    throw new OwnershipConfigurationError("unsupported_ownership_cluster");
  }
  if (cluster === "mainnet-beta" && str(Deno.env.get(MAINNET_SMOKE_ENABLED_ENV)).toLowerCase() !== "true") {
    throw new OwnershipConfigurationError("mainnet_ownership_disabled");
  }
  return cluster;
}

export function loadDevnetSmokeGrantConfig(): DevnetSmokeGrantConfig {
  const cluster = requiredEnv("CHAINBOUND_SOLANA_CLUSTER");
  if (cluster !== "devnet") throw new OwnershipConfigurationError("ownership_cluster_must_be_devnet");
  const programText = requiredEnv("CHAINBOUND_GAME_PROGRAM_ID");
  if (programText !== CHAINBOUND_PROGRAM_ID) throw new OwnershipConfigurationError("unexpected_chainbound_program_id");
  const configuredWallets = str(Deno.env.get(DEVNET_SMOKE_WALLETS_ENV));
  return {
    cluster,
    rpcUrl: requiredEnv("CHAINBOUND_SOLANA_RPC_ENDPOINT"),
    programId: publicKey(programText, "invalid_chainbound_program_id"),
    resultAuthority: readResultAuthority(requiredEnv("CHAINBOUND_RESULT_AUTHORITY_SECRET")),
    // Prefer the plural JSON allowlist. The singular value remains a rollout
    // fallback so an already-deployed environment fails closed without downtime.
    smokeWallets: configuredWallets ? parseDevnetSmokeWallets(configuredWallets) : new Set([
      publicKey(
        requiredEnv(LEGACY_DEVNET_SMOKE_WALLET_ENV),
        "invalid_devnet_smoke_wallet",
      ).toBase58(),
    ]),
  };
}

export function parseDevnetSmokeWallets(value: string): ReadonlySet<string> {
  return parseWalletAllowlist(value, "invalid_devnet_smoke_wallets");
}

export function parseMainnetSmokeWallets(value: string): ReadonlySet<string> {
  return parseWalletAllowlist(value, "invalid_mainnet_smoke_wallets");
}

function parseWalletAllowlist(value: string, errorCode: string): ReadonlySet<string> {
  try {
    const parsed = JSON.parse(value) as unknown;
    if (!Array.isArray(parsed) || parsed.length === 0 || parsed.length > 32) {
      throw new Error("invalid_wallet_count");
    }
    const wallets = new Set<string>();
    for (const candidate of parsed) {
      if (typeof candidate !== "string") throw new Error("invalid_wallet_type");
      const normalized = publicKey(candidate.trim(), errorCode).toBase58();
      if (wallets.has(normalized)) throw new Error("duplicate_wallet");
      wallets.add(normalized);
    }
    return wallets;
  } catch (error) {
    if (error instanceof OwnershipConfigurationError) throw error;
    throw new OwnershipConfigurationError(errorCode);
  }
}

export async function deriveKnownResult(
  secret: Uint8Array,
  profileId: string,
  idempotencyKey: string,
): Promise<KnownAuthorityResult> {
  const digest = await authorityDigest(secret, `known:${profileId}:${idempotencyKey}`);
  const selected = KNOWN_POOL[readU32(digest, 0) % KNOWN_POOL.length];
  return { ...selected, onchainMoveId: hashMoveId(selected.moveId) };
}

/** Stable UUID used as the shared Postgres/on-chain operation identifier. */
export async function deriveRamOperationId(profileId: string, kind: string, idempotencyKey: string): Promise<string> {
  const digest = await sha256Bytes(`ram-operation:${profileId}:${kind}:${idempotencyKey}`);
  const value = digest.slice(0, 16);
  value[6] = (value[6] & 0x0f) | 0x50;
  value[8] = (value[8] & 0x3f) | 0x80;
  const hex = bytesToHex(value);
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function parseCoreUpdateAuthorityAllowlist(value: string): ReadonlySet<string> {
  const candidates = value.split(",").map((candidate) => candidate.trim()).filter(Boolean);
  if (candidates.length === 0 || candidates.length > 16) {
    throw new OwnershipConfigurationError("invalid_cosmetics_update_authorities");
  }
  const authorities = new Set<string>();
  for (const candidate of candidates) {
    const normalized = publicKey(candidate, "invalid_cosmetics_update_authorities").toBase58();
    if (authorities.has(normalized)) {
      throw new OwnershipConfigurationError("invalid_cosmetics_update_authorities");
    }
    authorities.add(normalized);
  }
  return authorities;
}

/** Stable non-zero u64 written to the legacy receipt field for profile-native Known moves. */
export async function deriveProfileKnownReference(knownId: string): Promise<bigint> {
  const digest = await sha256Bytes(`profile-known:${knownId}`);
  const value = readU64(digest, 0);
  return value === 0n ? 1n : value;
}

/**
 * Atomically creates and consumes the Known reservation on chain. The result
 * authority is fee payer and signer, so retries can recover from the immutable
 * receipt without exposing a player-signable bypass transaction.
 */
export async function settleKnownRollOnChain(args: {
  config: OwnershipConfig;
  walletAddress: string;
  operationId: string;
  result: KnownAuthorityResult;
  expiresAt: Date;
}): Promise<SettledKnownRoll> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const operationHash = await sha256Bytes(args.operationId);
  const program = args.config.programId;
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [reservation] = PublicKey.findProgramAddressSync(
    [utf8("ram_reservation"), owner.toBytes(), operationHash],
    program,
  );
  const [vault] = PublicKey.findProgramAddressSync([utf8("move_vault"), owner.toBytes()], program);
  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [ownershipConfig] = PublicKey.findProgramAddressSync([utf8("ownership_config")], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("known_roll_receipt"), owner.toBytes(), operationHash],
    program,
  );

  const recovered = await decodeKnownRollReceipt(
    connection,
    receipt,
    owner,
    operationHash,
    args.result,
    args.config,
  );
  if (recovered !== null) {
    const signatures = await connection.getSignaturesForAddress(receipt, { limit: 20 }, "finalized");
    const signature = signatures.find((candidate) => !candidate.err && candidate.confirmationStatus === "finalized")
      ?.signature ?? "";
    if (!signature) throw new Error("known_roll_signature_missing");
    return {
      operationId: args.operationId,
      signature,
      receiptAddress: receipt.toBase58(),
      knownInstanceId: recovered.toString(),
    };
  }

  const expiresAtSeconds = BigInt(Math.floor(args.expiresAt.getTime() / 1000));
  if (expiresAtSeconds <= BigInt(Math.floor(Date.now() / 1000))) throw new Error("ram_reservation_expired");
  const createReservation = new TransactionInstruction({
    programId: program,
    keys: [
      meta(reservation, false, true),
      meta(profile, false, false),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      CREATE_RAM_RESERVATION_DISCRIMINATOR,
      operationHash,
      u32(KNOWN_ROLL_RAM_COST),
      bytes(0),
      i64(expiresAtSeconds),
    )),
  });
  const roll = new TransactionInstruction({
    programId: program,
    keys: [
      meta(reservation, false, true),
      meta(vault, false, true),
      meta(owner, false, false),
      meta(profile, false, true),
      meta(ownershipConfig, false, false),
      meta(receipt, false, true),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      ROLL_KNOWN_DISCRIMINATOR,
      operationHash,
      u16(args.result.onchainMoveId),
      bytes(args.result.element),
      u16(args.result.basePower),
    )),
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const tx = new Transaction({
    feePayer: args.config.resultAuthority.publicKey,
    recentBlockhash: latest.blockhash,
  }).add(createReservation, roll);
  tx.sign(args.config.resultAuthority);
  const signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
  const confirmation = await connection.confirmTransaction({ signature, ...latest }, "finalized");
  if (confirmation.value.err) throw new Error("known_roll_transaction_failed");
  const knownInstanceId = await decodeKnownRollReceipt(
    connection,
    receipt,
    owner,
    operationHash,
    args.result,
    args.config,
  );
  if (knownInstanceId === null) throw new Error("known_roll_receipt_missing");
  return {
    operationId: args.operationId,
    signature,
    receiptAddress: receipt.toBase58(),
    knownInstanceId: knownInstanceId.toString(),
  };
}

async function decodeKnownRollReceipt(
  connection: Connection,
  receipt: PublicKey,
  owner: PublicKey,
  operationHash: Uint8Array,
  expected: KnownAuthorityResult,
  config: OwnershipConfig,
): Promise<bigint | null> {
  const info = await connection.getAccountInfo(receipt, "finalized");
  if (!info) return null;
  const data = new Uint8Array(info.data);
  if (
    !info.owner.equals(config.programId) || data.length < 119 ||
    !equalBytes(data.slice(0, 8), KNOWN_ROLL_RECEIPT_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) ||
    !equalBytes(data.slice(40, 72), operationHash) ||
    readU16(data, 80) !== expected.onchainMoveId ||
    readU32(data, 82) !== config.generatorVersion ||
    bytesToHex(data.slice(86, 118)) !== config.modifierPoolHash
  ) {
    throw new Error("known_roll_receipt_mismatch");
  }
  return readU64(data, 72);
}

export async function ensureRamReservationOnChain(args: {
  config: OwnershipConfig;
  walletAddress: string;
  operationId: string;
  amount: number;
  kind: number;
  expiresAt: Date;
}): Promise<EnsuredRamReservation> {
  if (!Number.isSafeInteger(args.amount) || args.amount <= 0 || args.amount > 0xffffffff) {
    throw new Error("invalid_ram_reservation_amount");
  }
  if (!Number.isSafeInteger(args.kind) || args.kind < 0 || args.kind > 3) {
    throw new Error("invalid_ram_reservation_kind");
  }
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const operationHash = await sha256Bytes(args.operationId);
  const program = args.config.programId;
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [reservation] = PublicKey.findProgramAddressSync(
    [utf8("ram_reservation"), owner.toBytes(), operationHash],
    program,
  );
  const existing = await connection.getAccountInfo(reservation, "finalized");
  if (existing) {
    const data = new Uint8Array(existing.data);
    if (
      !existing.owner.equals(program) || data.length < 119 ||
      !equalBytes(data.slice(0, 8), RAM_RESERVATION_DISCRIMINATOR) ||
      !equalBytes(data.slice(8, 40), owner.toBytes()) ||
      !equalBytes(data.slice(40, 72), operationHash) ||
      readU32(data, 72) !== args.amount || data[76] !== args.kind ||
      !equalBytes(data.slice(86, 118), args.config.resultAuthority.publicKey.toBytes())
    ) {
      throw new Error("ram_reservation_mismatch");
    }
    const expiresAt = readI64(data, 77);
    const consumed = data[85] !== 0;
    if (!consumed && expiresAt <= BigInt(Math.floor(Date.now() / 1000))) {
      throw new Error("ram_reservation_expired");
    }
    const signatures = await connection.getSignaturesForAddress(reservation, { limit: 20 }, "finalized");
    const signature = signatures.find((candidate) => !candidate.err && candidate.confirmationStatus === "finalized")
      ?.signature ?? "";
    if (!signature) throw new Error("ram_reservation_signature_missing");
    return { address: reservation.toBase58(), signature, recovered: true };
  }

  const expiresAtSeconds = BigInt(Math.floor(args.expiresAt.getTime() / 1000));
  if (expiresAtSeconds <= BigInt(Math.floor(Date.now() / 1000))) throw new Error("ram_reservation_expired");
  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(reservation, false, true),
      meta(profile, false, false),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      CREATE_RAM_RESERVATION_DISCRIMINATOR,
      operationHash,
      u32(args.amount),
      bytes(args.kind),
      i64(expiresAtSeconds),
    )),
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const tx = new Transaction({
    feePayer: args.config.resultAuthority.publicKey,
    recentBlockhash: latest.blockhash,
  }).add(ix);
  tx.sign(args.config.resultAuthority);
  const signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
  const confirmation = await connection.confirmTransaction({ signature, ...latest }, "finalized");
  if (confirmation.value.err) throw new Error("ram_reservation_transaction_failed");
  return { address: reservation.toBase58(), signature, recovered: false };
}

export async function cancelRamReservationOnChain(args: {
  config: OwnershipConfig;
  walletAddress: string;
  operationId: string;
}): Promise<string> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const operationHash = await sha256Bytes(args.operationId);
  const program = args.config.programId;
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [reservation] = PublicKey.findProgramAddressSync(
    [utf8("ram_reservation"), owner.toBytes(), operationHash],
    program,
  );
  const info = await connection.getAccountInfo(reservation, "finalized");
  if (!info) return "";
  const data = new Uint8Array(info.data);
  if (
    !info.owner.equals(program) || data.length < 119 ||
    !equalBytes(data.slice(0, 8), RAM_RESERVATION_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) ||
    !equalBytes(data.slice(40, 72), operationHash) ||
    !equalBytes(data.slice(86, 118), args.config.resultAuthority.publicKey.toBytes())
  ) {
    throw new Error("ram_reservation_mismatch");
  }
  if (data[85] !== 0) throw new Error("ram_reservation_already_consumed");
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(reservation, false, true),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
    ],
    data: instructionData(concat(CANCEL_RAM_RESERVATION_DISCRIMINATOR, operationHash)),
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const tx = new Transaction({
    feePayer: args.config.resultAuthority.publicKey,
    recentBlockhash: latest.blockhash,
  }).add(ix);
  tx.sign(args.config.resultAuthority);
  const signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
  const confirmation = await connection.confirmTransaction({ signature, ...latest }, "finalized");
  if (confirmation.value.err) throw new Error("ram_reservation_cancel_failed");
  return signature;
}

export async function debitReservedRamOnChain(args: {
  config: OwnershipConfig;
  walletAddress: string;
  operationId: string;
  amount: number;
  kind?: number;
}): Promise<SettledRamDebit> {
  const kind = args.kind ?? 3;
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const operationHash = await sha256Bytes(args.operationId);
  const program = args.config.programId;
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [reservation] = PublicKey.findProgramAddressSync(
    [utf8("ram_reservation"), owner.toBytes(), operationHash],
    program,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_debit_receipt"), owner.toBytes(), operationHash],
    program,
  );
  const recovered = await decodeRamDebitReceipt(
    connection,
    receipt,
    owner,
    operationHash,
    args.amount,
    kind,
    args.config,
  );
  if (recovered !== null) {
    const signatures = await connection.getSignaturesForAddress(receipt, { limit: 20 }, "finalized");
    const signature = signatures.find((candidate) => !candidate.err && candidate.confirmationStatus === "finalized")
      ?.signature ?? "";
    if (!signature) throw new Error("ram_debit_signature_missing");
    return { signature, receiptAddress: receipt.toBase58(), balanceAfter: recovered };
  }

  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(reservation, false, true),
      meta(receipt, false, true),
      meta(profile, false, true),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      DEBIT_RESERVED_RAM_DISCRIMINATOR,
      operationHash,
      u32(args.amount),
      bytes(kind),
    )),
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const tx = new Transaction({
    feePayer: args.config.resultAuthority.publicKey,
    recentBlockhash: latest.blockhash,
  }).add(ix);
  tx.sign(args.config.resultAuthority);
  const signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
  const confirmation = await connection.confirmTransaction({ signature, ...latest }, "finalized");
  if (confirmation.value.err) throw new Error("ram_debit_transaction_failed");
  const balanceAfter = await decodeRamDebitReceipt(
    connection,
    receipt,
    owner,
    operationHash,
    args.amount,
    kind,
    args.config,
  );
  if (balanceAfter === null) throw new Error("ram_debit_receipt_missing");
  return { signature, receiptAddress: receipt.toBase58(), balanceAfter };
}

async function decodeRamDebitReceipt(
  connection: Connection,
  receipt: PublicKey,
  owner: PublicKey,
  operationHash: Uint8Array,
  amount: number,
  kind: number,
  config: OwnershipConfig,
): Promise<number | null> {
  const info = await connection.getAccountInfo(receipt, "finalized");
  if (!info) return null;
  const data = new Uint8Array(info.data);
  if (
    !info.owner.equals(config.programId) || data.length < 118 ||
    !equalBytes(data.slice(0, 8), RAM_DEBIT_RECEIPT_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) ||
    !equalBytes(data.slice(40, 72), operationHash) ||
    readU32(data, 72) !== amount || data[76] !== kind ||
    !equalBytes(data.slice(85, 117), config.resultAuthority.publicKey.toBytes())
  ) {
    throw new Error("ram_debit_receipt_mismatch");
  }
  return readU32(data, 81);
}

export async function fetchChainRamBalance(args: {
  config: OwnershipConfig;
  walletAddress: string;
}): Promise<number> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const [profile] = PublicKey.findProgramAddressSync(
    [utf8("player_profile"), owner.toBytes()],
    args.config.programId,
  );
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const info = await connection.getAccountInfo(profile, "finalized");
  if (!info || !info.owner.equals(args.config.programId)) throw new Error("player_profile_not_initialized");
  const state = decodePlayerProfile(new Uint8Array(info.data));
  if (state.owner !== owner.toBase58()) throw new Error("player_profile_owner_mismatch");
  return state.ramBalance;
}

/** Operator-only, authority-paid convergence for an approved frozen-ledger case. */
export async function reconcileRamDeltaOnChain(args: {
  config: OwnershipConfig;
  walletAddress: string;
  caseId: string;
  direction: "credit_chain" | "debit_chain";
  amount: number;
}): Promise<SettledRamReconciliation> {
  if (!Number.isSafeInteger(args.amount) || args.amount <= 0 || args.amount > 0xffffffff) {
    throw new Error("invalid_reconciliation_amount");
  }
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const program = args.config.programId;
  const caseHash = await sha256Bytes(args.caseId);
  const direction = args.direction === "credit_chain" ? 0 : 1;
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_reconcile_receipt"), owner.toBytes(), caseHash],
    program,
  );
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const recovered = await decodeRamReconciliationReceipt(
    connection,
    receipt,
    owner,
    caseHash,
    direction,
    args.amount,
    args.config,
  );
  if (recovered) {
    const signature = await findFinalizedSignature(connection, receipt);
    if (!signature) throw new Error("ram_reconciliation_signature_missing");
    return { signature, receiptAddress: receipt.toBase58(), ...recovered, recovered: true };
  }

  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(receipt, false, true),
      meta(profile, false, true),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      RECONCILE_RAM_DELTA_DISCRIMINATOR,
      caseHash,
      bytes(direction),
      u32(args.amount),
    )),
  });
  const latest = await connection.getLatestBlockhash("finalized");
  const tx = new Transaction({
    feePayer: args.config.resultAuthority.publicKey,
    recentBlockhash: latest.blockhash,
  }).add(ix);
  tx.sign(args.config.resultAuthority);
  let signature = "";
  try {
    signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
    const confirmation = await connection.confirmTransaction({ signature, ...latest }, "finalized");
    if (confirmation.value.err) throw new Error("ram_reconciliation_transaction_failed");
  } catch (error) {
    const afterRace = await decodeRamReconciliationReceipt(
      connection,
      receipt,
      owner,
      caseHash,
      direction,
      args.amount,
      args.config,
    );
    if (!afterRace) throw error;
    signature = await findFinalizedSignature(connection, receipt) ?? "";
  }
  const settled = await decodeRamReconciliationReceipt(
    connection,
    receipt,
    owner,
    caseHash,
    direction,
    args.amount,
    args.config,
  );
  if (!settled || !signature) throw new Error("ram_reconciliation_receipt_missing");
  return { signature, receiptAddress: receipt.toBase58(), ...settled, recovered: false };
}

async function decodeRamReconciliationReceipt(
  connection: Connection,
  receipt: PublicKey,
  owner: PublicKey,
  caseHash: Uint8Array,
  direction: number,
  amount: number,
  config: OwnershipConfig,
): Promise<{ balanceBefore: number; balanceAfter: number } | null> {
  const info = await connection.getAccountInfo(receipt, "finalized");
  if (!info) return null;
  const data = new Uint8Array(info.data);
  if (
    !info.owner.equals(config.programId) || data.length < 118 ||
    !equalBytes(data.slice(0, 8), RAM_RECONCILIATION_RECEIPT_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) ||
    !equalBytes(data.slice(40, 72), caseHash) || data[72] !== direction ||
    readU32(data, 73) !== amount ||
    !equalBytes(data.slice(85, 117), config.resultAuthority.publicKey.toBytes())
  ) throw new Error("ram_reconciliation_receipt_mismatch");
  return { balanceBefore: readU32(data, 77), balanceAfter: readU32(data, 81) };
}

async function findFinalizedSignature(connection: Connection, address: PublicKey): Promise<string | null> {
  const signatures = await connection.getSignaturesForAddress(address, { limit: 20 }, "finalized");
  return signatures.find((candidate) => !candidate.err && candidate.confirmationStatus === "finalized")?.signature ??
    null;
}

export async function deriveStampResult(
  secret: Uint8Array,
  profileId: string,
  knownId: string,
  modifierCount: number,
  generatorVersion: number,
  poolHash: string,
): Promise<StampAuthorityResult> {
  if (modifierCount !== 1 && modifierCount !== 2) throw new Error("invalid_modifier_count");
  if (!Number.isSafeInteger(generatorVersion) || generatorVersion <= 0) throw new Error("invalid_generator_version");
  if (!/^[0-9a-f]{64}$/.test(poolHash)) throw new Error("invalid_pool_hash");
  // The client idempotency key is deliberately excluded. A Known move receives
  // one immutable outcome for a given generator/pool, regardless of retries,
  // wallet cancellation, blockhash expiry, or app relaunch.
  const digest = await stampResultDigest(
    secret,
    profileId,
    knownId,
    modifierCount,
    generatorVersion,
    poolHash,
  );
  const first = readU32(digest, 0) % MODIFIER_POOL.length;
  let second = readU32(digest, 4) % (MODIFIER_POOL.length - 1);
  if (second >= first) second++;
  const rarityRoll = readU32(digest, 8) % 10000;
  const rarity = rarityRoll < 4500
    ? 0
    : rarityRoll < 7500
    ? 1
    : rarityRoll < 9200
    ? 2
    : rarityRoll < 9700
    ? 3
    : rarityRoll < 9900
    ? 4
    : 5;
  const modifierIds = modifierCount === 1 ? [MODIFIER_POOL[first]] : [MODIFIER_POOL[first], MODIFIER_POOL[second]];
  return {
    modifierIds,
    rarity,
    modifierEffectValues: lockModifierEffectValues(digest, modifierIds),
  };
}

async function stampResultDigest(
  secret: Uint8Array,
  profileId: string,
  knownId: string,
  modifierCount: number,
  generatorVersion: number,
  poolHash: string,
): Promise<Uint8Array> {
  return await authorityDigest(
    secret,
    `stamp:v2:${generatorVersion}:${poolHash}:${profileId}:${knownId}:mods:${modifierCount}`,
  );
}

export async function deriveStampLockForModifiers(
  secret: Uint8Array,
  profileId: string,
  knownId: string,
  modifierIds: string[],
  generatorVersion: number,
  poolHash: string,
): Promise<number[]> {
  if (modifierIds.length !== 1 && modifierIds.length !== 2) throw new Error("invalid_modifier_count");
  const digest = await stampResultDigest(
    secret,
    profileId,
    knownId,
    modifierIds.length,
    generatorVersion,
    poolHash,
  );
  return lockModifierEffectValues(digest, modifierIds);
}

export function lockModifierEffectValues(digest: Uint8Array, modifierIds: string[]): number[] {
  return modifierIds.map((sectionId, slot) => {
    const bounds = modifierEffectBounds(sectionId);
    return rollLockedEffectValue(bounds.min, bounds.max, readU32(digest, 12 + slot * 4));
  });
}

function modifierEffectBounds(sectionId: string): { min: number; max: number } {
  const entry = (MODIFIER_EFFECT_BOUNDS as Record<string, { min: number; max: number }>)[sectionId];
  return entry ?? { min: 0, max: 0 };
}

export function rollLockedEffectValue(min: number, max: number, raw: number): number {
  let low = min;
  let high = max;
  if (high < low) {
    const swap = low;
    low = high;
    high = swap;
  }
  if (Math.abs(high - low) < 0.0001) return roundAwayFromZero(low, 4);
  const unitInclusive = (raw >>> 0) / 4294967295;
  return roundAwayFromZero(low + ((high - low) * unitInclusive), 4);
}

export function roundAwayFromZero(value: number, digits: number): number {
  if (!Number.isFinite(value)) return 0;
  if (value === 0) return 0;
  const factor = 10 ** digits;
  const sign = value < 0 ? -1 : 1;
  return sign * Math.round(Math.abs(value) * factor) / factor;
}

export function formatLockedNumber(value: number): string {
  const rounded = roundAwayFromZero(value, 4);
  const negative = rounded < 0;
  const [whole, frac = ""] = Math.abs(rounded).toFixed(4).split(".");
  const trimmedFrac = frac.replace(/0+$/, "");
  const abs = trimmedFrac.length > 0 ? `${whole}.${trimmedFrac}` : whole;
  return negative ? `-${abs}` : abs;
}

const LOCKED_NUMERIC_RANGE = /(?<![\d.])[+-]?\d+(?:\.\d+)?\s*(?:-|–|—)\s*[+-]?\d+(?:\.\d+)?/;

export function formatLockedModifierEffect(description: string, lockedValue: number): string {
  const formatted = formatLockedNumber(lockedValue);
  const source = description.trim();
  if (!source) return formatted;
  let next = source.replace(LOCKED_NUMERIC_RANGE, formatted);
  next = next.split("{value}").join(formatted).split("{n}").join(formatted);
  return next;
}

export function parseModifierEffectValues(value: unknown): number[] | null {
  if (!Array.isArray(value) || value.length === 0) return null;
  const parsed = value.map((entry) => {
    const numeric = typeof entry === "number" ? entry : Number(entry);
    return Number.isFinite(numeric) ? numeric : NaN;
  });
  if (parsed.some((entry) => Number.isNaN(entry))) return null;
  return parsed.map((entry) => roundAwayFromZero(entry, 4));
}

export function stampEffectFieldsFromOperation(operation: JsonMap): JsonMap {
  const values = parseModifierEffectValues(operation.modifier_effect_values);
  const count = Math.max(0, Number(operation.modifier_count) || 0);
  if (!values || values.length < 1) {
    return {
      modifier1EffectValue: 0,
      modifier1EffectValueLocked: false,
      modifier2EffectValue: 0,
      modifier2EffectValueLocked: false,
    };
  }
  return {
    modifier1EffectValue: values[0],
    modifier1EffectValueLocked: true,
    modifier2EffectValue: count > 1 ? values[1] ?? 0 : 0,
    modifier2EffectValueLocked: count > 1 && values.length > 1,
  };
}

export async function buildStampTransaction(args: {
  config: OwnershipConfig;
  walletAddress: string;
  knownId: string;
  knownMoveId: number;
  element: number;
  basePower: number;
  existingOnchainInstanceId: string | null;
  operationId: string;
  idempotencyKey: string;
  expiresAt: Date;
  modifierIds: string[];
  rarity: number;
}): Promise<BuiltStampTransaction> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const program = args.config.programId;
  const operationHash = await sha256Bytes(args.operationId);
  const [ownershipConfig] = PublicKey.findProgramAddressSync([utf8("ownership_config")], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const [coreAuthority] = PublicKey.findProgramAddressSync([utf8("core_authority")], program);
  const [ticket] = PublicKey.findProgramAddressSync([utf8("stamp_ticket"), owner.toBytes(), operationHash], program);
  const [stampedAsset] = PublicKey.findProgramAddressSync(
    [utf8("stamped_asset"), owner.toBytes(), operationHash],
    program,
  );
  const instructions: TransactionInstruction[] = [];
  const expiresAt = args.expiresAt;
  if (expiresAt.getTime() <= Date.now()) throw new Error("ram_reservation_expired");
  const assetName = `Chainbound Stamped ${args.knownMoveId}`;
  const assetUri = stampMetadataUri(args.config.metadataUriBase, stampedAsset.toBase58());
  const profileNative = !args.existingOnchainInstanceId;
  let onchainInstanceId: bigint;
  let chainRamBalance = 0;
  let chainRamRequired = 0;
  let reservationAddress = "";

  if (profileNative) {
    onchainInstanceId = await deriveProfileKnownReference(args.knownId);
    instructions.push(
      new TransactionInstruction({
        programId: program,
        keys: [
          meta(ticket, false, true),
          meta(ownershipConfig, false, false),
          meta(owner, false, false),
          meta(gameConfig, false, false),
          meta(args.config.resultAuthority.publicKey, true, true),
          meta(SystemProgram.programId, false, false),
        ],
        data: instructionData(concat(
          PREPARE_PROFILE_STAMP_DISCRIMINATOR,
          operationHash,
          u64(onchainInstanceId),
          u16(args.knownMoveId),
          stringVector(args.modifierIds),
          bytes(args.rarity),
          borshString(assetName),
          borshString(assetUri),
          i64(BigInt(Math.floor(expiresAt.getTime() / 1000))),
        )),
      }),
      new TransactionInstruction({
        programId: program,
        keys: [
          meta(owner, true, true),
          meta(ticket, false, true),
          meta(ownershipConfig, false, false),
          meta(coreAuthority, false, false),
          meta(stampedAsset, false, true),
          meta(args.config.movesCollection, false, true),
          meta(new PublicKey(CORE_PROGRAM_ID), false, false),
          meta(SystemProgram.programId, false, false),
        ],
        data: instructionData(concat(STAMP_PROFILE_KNOWN_DISCRIMINATOR, operationHash)),
      }),
    );
  } else {
    const [vault] = PublicKey.findProgramAddressSync([utf8("move_vault"), owner.toBytes()], program);
    const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
    const [reservation] = PublicKey.findProgramAddressSync(
      [utf8("ram_reservation"), owner.toBytes(), operationHash],
      program,
    );
    const [vaultInfo, profileInfo] = await connection.getMultipleAccountsInfo([vault, profile], "confirmed");
    if (!vaultInfo) throw new Error("move_vault_not_initialized");
    if (!profileInfo) throw new Error("player_profile_not_initialized");
    if (!vaultInfo.owner.equals(program)) throw new Error("move_vault_wrong_program_owner");
    if (!profileInfo.owner.equals(program)) throw new Error("player_profile_wrong_program_owner");
    const vaultState = decodeMoveVault(new Uint8Array(vaultInfo.data));
    const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
    if (profileState.owner !== owner.toBase58()) throw new Error("player_profile_owner_mismatch");
    onchainInstanceId = BigInt(args.existingOnchainInstanceId!);
    if (!vaultState.instanceIds.some((value) => value === onchainInstanceId)) {
      throw new Error("known_onchain_instance_missing");
    }
    chainRamBalance = profileState.ramBalance;
    chainRamRequired = args.config.stampRamCost;
    if (chainRamBalance < chainRamRequired) throw new Error("insufficient_ram");
    reservationAddress = reservation.toBase58();

    instructions.push(
      new TransactionInstruction({
        programId: program,
        keys: [
          meta(ticket, false, true),
          meta(vault, false, false),
          meta(ownershipConfig, false, false),
          meta(owner, false, false),
          meta(gameConfig, false, false),
          meta(args.config.resultAuthority.publicKey, true, true),
          meta(SystemProgram.programId, false, false),
        ],
        data: instructionData(concat(
          PREPARE_STAMP_DISCRIMINATOR,
          operationHash,
          u64(onchainInstanceId),
          stringVector(args.modifierIds),
          bytes(args.rarity),
          borshString(assetName),
          borshString(assetUri),
          i64(BigInt(Math.floor(expiresAt.getTime() / 1000))),
        )),
      }),
      new TransactionInstruction({
        programId: program,
        keys: [
          meta(owner, true, true),
          meta(profile, false, true),
          meta(reservation, false, true),
          meta(vault, false, true),
          meta(ticket, false, true),
          meta(ownershipConfig, false, false),
          meta(coreAuthority, false, false),
          meta(stampedAsset, false, true),
          meta(args.config.movesCollection, false, true),
          meta(new PublicKey(CORE_PROGRAM_ID), false, false),
          meta(SystemProgram.programId, false, false),
        ],
        data: instructionData(concat(STAMP_KNOWN_DISCRIMINATOR, operationHash)),
      }),
    );
  }

  const latest = await connection.getLatestBlockhash("confirmed");
  const tx = new Transaction({ feePayer: owner, recentBlockhash: latest.blockhash });
  tx.add(...instructions);
  tx.partialSign(args.config.resultAuthority);
  const estimated = await connection.getFeeForMessage(tx.compileMessage(), "confirmed");
  return {
    transactionBase64: uint8ToBase64(
      new Uint8Array(tx.serialize({ requireAllSignatures: false, verifySignatures: true })),
    ),
    recentBlockhash: latest.blockhash,
    ticketAddress: ticket.toBase58(),
    stampedAssetAddress: stampedAsset.toBase58(),
    onchainInstanceId,
    estimatedNetworkFeeLamports: estimated.value ?? 0,
    expiresAt,
    chainRamBalance,
    chainRamRequired,
    reservationAddress,
    knownSource: profileNative ? "profile" : "legacy_onchain",
  };
}

/**
 * Server-side preflight for the exact partially signed transaction returned to
 * Unity. The owner signature is intentionally absent, so signature checks stay
 * disabled while every program instruction and account transition is executed.
 */
export async function simulateStampTransaction(rpcUrl: string, transactionBase64: string): Promise<void> {
  const response = await fetch(rpcUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 1,
      method: "simulateTransaction",
      params: [transactionBase64, {
        encoding: "base64",
        commitment: "confirmed",
        sigVerify: false,
        replaceRecentBlockhash: false,
      }],
    }),
  });
  if (!response.ok) throw new Error("stamp_simulation_failed");
  const payload = await response.json() as {
    error?: unknown;
    result?: { value?: { err?: unknown; logs?: string[] } };
  };
  const simulationError = payload.error ?? payload.result?.value?.err;
  if (!simulationError) return;
  const serialized = JSON.stringify(simulationError);
  if (serialized.includes("6001")) throw new Error("insufficient_ram");
  console.error("[simulateStampTransaction]", serialized, payload.result?.value?.logs ?? []);
  throw new Error("stamp_simulation_failed");
}

export async function buildBindTransaction(args: {
  config: BindOwnershipConfig;
  walletAddress: string;
  operationId: string;
  onchainChainlingId: string;
  coverAssetAddress: string;
  componentAssetAddress: string;
  componentKind: number;
  archiveName: string;
  archiveUri: string;
  expiresAt: Date;
}): Promise<BuiltBindTransaction> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const cover = publicKey(args.coverAssetAddress, "invalid_cover_asset");
  const component = publicKey(args.componentAssetAddress, "invalid_component_asset");
  if (cover.equals(component)) throw new Error("duplicate_bind_component");
  if (args.componentKind !== 0 && args.componentKind !== 1) throw new Error("invalid_component_kind");
  const nameBytes = utf8(args.archiveName);
  const uriBytes = utf8(args.archiveUri);
  if (!args.archiveName.trim() || nameBytes.length > 64) throw new Error("invalid_archive_name");
  if (!args.archiveUri.startsWith("https://") || uriBytes.length > 200) throw new Error("invalid_archive_uri");
  const chainlingId = BigInt(args.onchainChainlingId);
  if (chainlingId < 0n || chainlingId > 18446744073709551615n) throw new Error("invalid_onchain_chainling_id");
  if (args.expiresAt.getTime() <= Date.now()) throw new Error("ram_reservation_expired");

  const program = args.config.programId;
  const operationHash = await sha256Bytes(args.operationId);
  const chainlingIdBytes = u64(chainlingId);
  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [reservation] = PublicKey.findProgramAddressSync(
    [utf8("ram_reservation"), owner.toBytes(), operationHash],
    program,
  );
  const [chainling] = PublicKey.findProgramAddressSync(
    [utf8("chainling_account"), owner.toBytes(), chainlingIdBytes],
    program,
  );
  const [ownershipConfig] = PublicKey.findProgramAddressSync([utf8("ownership_config")], program);
  const [coreAuthority] = PublicKey.findProgramAddressSync([utf8("core_authority")], program);
  const [archiveAsset] = PublicKey.findProgramAddressSync(
    [utf8("archive_asset"), owner.toBytes(), chainlingIdBytes],
    program,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("bind_receipt"), owner.toBytes(), chainlingIdBytes],
    program,
  );
  const coreProgram = new PublicKey(CORE_PROGRAM_ID);
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const infos = await connection.getMultipleAccountsInfo([
    profile,
    reservation,
    chainling,
    cover,
    component,
    args.config.coversCollection,
    args.config.movesCollection,
    args.config.cosmeticsCollection,
    args.config.archivesCollection,
    archiveAsset,
    receipt,
  ], "confirmed");
  const [profileInfo, reservationInfo, chainlingInfo, coverInfo, componentInfo, ...collectionAndOutputInfos] = infos;
  if (!profileInfo) throw new Error("player_profile_not_initialized");
  if (!reservationInfo) throw new Error("ram_reservation_missing");
  if (!chainlingInfo) throw new Error("chainling_account_not_initialized");
  requireProgramAccount(
    profileInfo.owner,
    program,
    new Uint8Array(profileInfo.data),
    PLAYER_PROFILE_DISCRIMINATOR,
    "invalid_player_profile_account",
  );
  requireProgramAccount(
    chainlingInfo.owner,
    program,
    new Uint8Array(chainlingInfo.data),
    CHAINLING_ACCOUNT_DISCRIMINATOR,
    "invalid_chainling_account",
  );
  const chainlingData = new Uint8Array(chainlingInfo.data);
  if (
    chainlingData.length < 48 || !equalBytes(chainlingData.slice(8, 40), owner.toBytes()) ||
    readU64(chainlingData, 40) !== chainlingId
  ) throw new Error("chainling_account_mismatch");
  const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
  if (profileState.owner !== owner.toBase58()) throw new Error("player_profile_owner_mismatch");
  if (profileState.ramBalance < BIND_RAM_COST_PIN) throw new Error("insufficient_ram");
  if (!coverInfo || !coverInfo.owner.equals(coreProgram)) throw new Error("invalid_cover_asset");
  if (!componentInfo || !componentInfo.owner.equals(coreProgram)) throw new Error("invalid_component_asset");
  for (const info of collectionAndOutputInfos.slice(0, 4)) {
    if (!info || !info.owner.equals(coreProgram)) throw new Error("invalid_bind_collection");
  }
  if (collectionAndOutputInfos[4] || collectionAndOutputInfos[5]) throw new Error("bind_already_exists");

  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(owner, true, true),
      meta(profile, false, true),
      meta(reservation, false, true),
      meta(chainling, false, true),
      meta(ownershipConfig, false, false),
      meta(coreAuthority, false, false),
      meta(cover, false, true),
      meta(component, false, true),
      meta(args.config.coversCollection, false, true),
      meta(args.config.movesCollection, false, true),
      meta(args.config.cosmeticsCollection, false, true),
      meta(args.config.archivesCollection, false, true),
      meta(archiveAsset, false, true),
      meta(receipt, false, true),
      meta(coreProgram, false, false),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(
      BIND_CHAINLING_DISCRIMINATOR,
      chainlingIdBytes,
      operationHash,
      bytes(args.componentKind),
      borshString(args.archiveName),
      borshString(args.archiveUri),
    )),
  });
  const latest = await connection.getLatestBlockhash("confirmed");
  const tx = new Transaction({ feePayer: owner, recentBlockhash: latest.blockhash }).add(ix);
  const estimated = await connection.getFeeForMessage(tx.compileMessage(), "confirmed");
  return {
    transactionBase64: uint8ToBase64(
      new Uint8Array(tx.serialize({ requireAllSignatures: false, verifySignatures: false })),
    ),
    recentBlockhash: latest.blockhash,
    lastValidBlockHeight: latest.lastValidBlockHeight,
    reservationAddress: reservation.toBase58(),
    chainlingAddress: chainling.toBase58(),
    archiveAssetAddress: archiveAsset.toBase58(),
    receiptAddress: receipt.toBase58(),
    estimatedNetworkFeeLamports: estimated.value ?? 0,
    expiresAt: args.expiresAt,
  };
}

export async function simulateBindTransaction(rpcUrl: string, transactionBase64: string): Promise<void> {
  const response = await fetch(rpcUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 1,
      method: "simulateTransaction",
      params: [transactionBase64, {
        encoding: "base64",
        commitment: "confirmed",
        sigVerify: false,
        replaceRecentBlockhash: false,
      }],
    }),
  });
  if (!response.ok) throw new Error("bind_simulation_failed");
  const payload = await response.json() as {
    error?: unknown;
    result?: { value?: { err?: unknown; logs?: string[] } };
  };
  const simulationError = payload.error ?? payload.result?.value?.err;
  if (!simulationError) return;
  const serialized = JSON.stringify(simulationError);
  if (serialized.includes("6001")) throw new Error("insufficient_ram");
  console.error("[simulateBindTransaction]", serialized, payload.result?.value?.logs ?? []);
  throw new Error("bind_simulation_failed");
}

export async function fetchAndVerifyBindReceipt(args: {
  config: BindOwnershipConfig;
  walletAddress: string;
  operationId: string;
  onchainChainlingId: string;
  coverAssetAddress: string;
  componentAssetAddress: string;
  componentKind: number;
  expectedArchiveAssetAddress?: string;
  expectedReceiptAddress?: string;
}): Promise<VerifiedBindReceipt | null> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const chainlingId = BigInt(args.onchainChainlingId);
  const operationHash = await sha256Bytes(args.operationId);
  const chainlingIdBytes = u64(chainlingId);
  const [archiveAsset] = PublicKey.findProgramAddressSync(
    [utf8("archive_asset"), owner.toBytes(), chainlingIdBytes],
    args.config.programId,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("bind_receipt"), owner.toBytes(), chainlingIdBytes],
    args.config.programId,
  );
  if (args.expectedArchiveAssetAddress && archiveAsset.toBase58() !== args.expectedArchiveAssetAddress) return null;
  if (args.expectedReceiptAddress && receipt.toBase58() !== args.expectedReceiptAddress) return null;
  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [receiptInfo, archiveInfo] = await connection.getMultipleAccountsInfo([receipt, archiveAsset], "finalized");
  if (!receiptInfo || !archiveInfo) return null;
  const data = new Uint8Array(receiptInfo.data);
  if (
    !receiptInfo.owner.equals(args.config.programId) || data.length !== 179 ||
    !equalBytes(data.slice(0, 8), BIND_RECEIPT_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) || readU64(data, 40) !== chainlingId ||
    !equalBytes(data.slice(48, 80), operationHash) ||
    new PublicKey(data.slice(80, 112)).toBase58() !== args.coverAssetAddress ||
    new PublicKey(data.slice(112, 144)).toBase58() !== args.componentAssetAddress ||
    data[144] !== args.componentKind ||
    !equalBytes(data.slice(145, 177), archiveAsset.toBytes()) || data[177] !== 1 ||
    !archiveInfo.owner.equals(new PublicKey(CORE_PROGRAM_ID))
  ) return null;
  return {
    receiptAddress: receipt.toBase58(),
    ownerAddress: owner.toBase58(),
    onchainChainlingId: chainlingId.toString(),
    operationHash: bytesToHex(operationHash),
    coverAssetAddress: args.coverAssetAddress,
    componentAssetAddress: args.componentAssetAddress,
    componentKind: args.componentKind,
    archiveAssetAddress: archiveAsset.toBase58(),
  };
}

export async function buildDevnetSmokeGrantTransaction(args: {
  config: DevnetSmokeGrantConfig;
  walletAddress: string;
  idempotencyKey: string;
}): Promise<BuiltRamGrantTransaction> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  if (!args.config.smokeWallets.has(owner.toBase58())) throw new Error("devnet_smoke_wallet_not_allowed");
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const program = args.config.programId;
  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [vault] = PublicKey.findProgramAddressSync([utf8("move_vault"), owner.toBytes()], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const idempotencyHash = await sha256Bytes(args.idempotencyKey);
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_grant_receipt"), owner.toBytes(), idempotencyHash],
    program,
  );

  const [profileInfo, vaultInfo, gameConfigInfo, receiptInfo] = await connection.getMultipleAccountsInfo(
    [profile, vault, gameConfig, receipt],
    "confirmed",
  );
  if (!profileInfo) throw new Error("player_profile_not_initialized");
  if (!vaultInfo) throw new Error("move_vault_not_initialized");
  requireProgramAccount(
    profileInfo.owner,
    program,
    new Uint8Array(profileInfo.data),
    PLAYER_PROFILE_DISCRIMINATOR,
    "invalid_player_profile_account",
  );
  requireProgramAccount(
    vaultInfo.owner,
    program,
    new Uint8Array(vaultInfo.data),
    MOVE_VAULT_DISCRIMINATOR,
    "invalid_move_vault_account",
  );
  if (!gameConfigInfo) throw new Error("game_config_not_initialized");
  requireProgramAccount(
    gameConfigInfo.owner,
    program,
    new Uint8Array(gameConfigInfo.data),
    GAME_CONFIG_DISCRIMINATOR,
    "invalid_game_config_account",
  );
  if (receiptInfo) throw new Error("smoke_ram_grant_already_exists");

  const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
  const vaultState = decodeMoveVault(new Uint8Array(vaultInfo.data));
  const configState = decodeGameConfig(new Uint8Array(gameConfigInfo.data));
  if (profileState.owner !== owner.toBase58()) throw new Error("player_profile_owner_mismatch");
  if (profileState.ramBalance !== 0 || profileState.dailyRamEarned !== 0 || profileState.lifetimeRamEarned !== 0n) {
    throw new Error("fresh_profile_ram_not_zero");
  }
  if (vaultState.instanceIds.length !== 0 || vaultState.nextInstanceId !== 1n) throw new Error("move_vault_not_empty");
  if (configState.resultAuthority !== args.config.resultAuthority.publicKey.toBase58()) {
    throw new Error("result_authority_mismatch");
  }
  if (configState.ramDailyCap !== RAM_DAILY_CAP) throw new Error("unexpected_ram_daily_cap");
  if (configState.instructionFlags !== 0n) throw new Error("ownership_flags_must_remain_off");

  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(receipt, false, true),
      meta(profile, false, true),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(GRANT_RAM_DISCRIMINATOR, idempotencyHash, u32(RAM_DAILY_CAP))),
  });
  const latest = await connection.getLatestBlockhash("confirmed");
  const tx = new Transaction({ feePayer: owner, recentBlockhash: latest.blockhash }).add(ix);
  tx.partialSign(args.config.resultAuthority);
  const estimated = await connection.getFeeForMessage(tx.compileMessage(), "confirmed");
  return {
    transactionBase64: uint8ToBase64(
      new Uint8Array(tx.serialize({ requireAllSignatures: false, verifySignatures: true })),
    ),
    recentBlockhash: latest.blockhash,
    playerProfileAddress: profile.toBase58(),
    moveVaultAddress: vault.toBase58(),
    receiptAddress: receipt.toBase58(),
    amount: RAM_DAILY_CAP,
    estimatedNetworkFeeLamports: estimated.value ?? 0,
  };
}

export async function buildOperatorDevnetRamTopupTransaction(args: {
  config: DevnetSmokeGrantConfig;
  walletAddress: string;
  idempotencyKey: string;
}): Promise<BuiltOperatorRamTopupTransaction> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  if (!args.config.smokeWallets.has(owner.toBase58())) throw new Error("devnet_smoke_wallet_not_allowed");
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const program = args.config.programId;
  const [profile] = PublicKey.findProgramAddressSync([utf8("player_profile"), owner.toBytes()], program);
  const [vault] = PublicKey.findProgramAddressSync([utf8("move_vault"), owner.toBytes()], program);
  const [gameConfig] = PublicKey.findProgramAddressSync([utf8("game_config")], program);
  const idempotencyHash = await sha256Bytes(args.idempotencyKey);
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_grant_receipt"), owner.toBytes(), idempotencyHash],
    program,
  );
  const [profileInfo, vaultInfo, gameConfigInfo, receiptInfo] = await connection.getMultipleAccountsInfo(
    [profile, vault, gameConfig, receipt],
    "confirmed",
  );
  if (!profileInfo) throw new Error("player_profile_not_initialized");
  if (!vaultInfo) throw new Error("move_vault_not_initialized");
  requireProgramAccount(
    profileInfo.owner,
    program,
    new Uint8Array(profileInfo.data),
    PLAYER_PROFILE_DISCRIMINATOR,
    "invalid_player_profile_account",
  );
  requireProgramAccount(
    vaultInfo.owner,
    program,
    new Uint8Array(vaultInfo.data),
    MOVE_VAULT_DISCRIMINATOR,
    "invalid_move_vault_account",
  );
  if (!gameConfigInfo) throw new Error("game_config_not_initialized");
  requireProgramAccount(
    gameConfigInfo.owner,
    program,
    new Uint8Array(gameConfigInfo.data),
    GAME_CONFIG_DISCRIMINATOR,
    "invalid_game_config_account",
  );
  if (receiptInfo) throw new Error("operator_topup_receipt_already_exists");

  const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
  const configState = decodeGameConfig(new Uint8Array(gameConfigInfo.data));
  if (profileState.owner !== owner.toBase58()) throw new Error("player_profile_owner_mismatch");
  if (configState.resultAuthority !== args.config.resultAuthority.publicKey.toBase58()) {
    throw new Error("result_authority_mismatch");
  }
  if (configState.ramDailyCap !== RAM_DAILY_CAP) throw new Error("unexpected_ram_daily_cap");
  if (configState.instructionFlags !== 0n) throw new Error("ownership_flags_must_remain_off");

  const ix = new TransactionInstruction({
    programId: program,
    keys: [
      meta(receipt, false, true),
      meta(profile, false, true),
      meta(owner, false, false),
      meta(gameConfig, false, false),
      meta(args.config.resultAuthority.publicKey, true, true),
      meta(SystemProgram.programId, false, false),
    ],
    data: instructionData(concat(GRANT_RAM_DISCRIMINATOR, idempotencyHash, u32(RAM_DAILY_CAP))),
  });
  const latest = await connection.getLatestBlockhash("confirmed");
  // The recovery path is authority-paid and never asks the player wallet to
  // sign. The player account remains an explicitly non-signer account meta.
  const tx = new Transaction({ feePayer: args.config.resultAuthority.publicKey, recentBlockhash: latest.blockhash })
    .add(ix);
  tx.sign(args.config.resultAuthority);
  const estimated = await connection.getFeeForMessage(tx.compileMessage(), "confirmed");
  return {
    transaction: tx,
    transactionBase64: uint8ToBase64(new Uint8Array(tx.serialize())),
    recentBlockhash: latest.blockhash,
    lastValidBlockHeight: latest.lastValidBlockHeight,
    playerProfileAddress: profile.toBase58(),
    moveVaultAddress: vault.toBase58(),
    receiptAddress: receipt.toBase58(),
    amount: RAM_DAILY_CAP,
    estimatedNetworkFeeLamports: estimated.value ?? 0,
    balanceBefore: profileState.ramBalance,
  };
}

export async function simulateOperatorDevnetRamTopup(
  rpcUrl: string,
  transaction: Transaction,
): Promise<void> {
  const connection = new Connection(rpcUrl, "confirmed");
  const simulation = await connection.simulateTransaction(transaction);
  if (simulation.value.err) throw new Error("operator_topup_simulation_failed");
}

export async function fetchAndVerifyOperatorDevnetRamTopup(args: {
  config: DevnetSmokeGrantConfig;
  walletAddress: string;
  idempotencyKey: string;
  expectedBalanceAfter?: number;
}): Promise<VerifiedRamGrant | null> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const idempotencyHash = await sha256Bytes(args.idempotencyKey);
  const [profile] = PublicKey.findProgramAddressSync(
    [utf8("player_profile"), owner.toBytes()],
    args.config.programId,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_grant_receipt"), owner.toBytes(), idempotencyHash],
    args.config.programId,
  );
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const [profileInfo, receiptInfo] = await connection.getMultipleAccountsInfo([profile, receipt], "confirmed");
  if (!profileInfo || !receiptInfo) return null;
  requireProgramAccount(
    profileInfo.owner,
    args.config.programId,
    new Uint8Array(profileInfo.data),
    PLAYER_PROFILE_DISCRIMINATOR,
    "invalid_player_profile_account",
  );
  requireProgramAccount(
    receiptInfo.owner,
    args.config.programId,
    new Uint8Array(receiptInfo.data),
    RAM_GRANT_RECEIPT_DISCRIMINATOR,
    "invalid_ram_grant_receipt",
  );
  const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
  const receiptState = decodeRamGrantReceipt(new Uint8Array(receiptInfo.data));
  if (profileState.owner !== owner.toBase58() || receiptState.owner !== owner.toBase58()) return null;
  if (receiptState.idempotencyHash !== bytesToHex(idempotencyHash)) return null;
  if (receiptState.resultAuthority !== args.config.resultAuthority.publicKey.toBase58()) return null;
  if (receiptState.amount !== RAM_DAILY_CAP) return null;
  if (args.expectedBalanceAfter != null && receiptState.balanceAfter !== args.expectedBalanceAfter) return null;
  if (profileState.ramBalance !== receiptState.balanceAfter || profileState.dailyRamEarned > RAM_DAILY_CAP) return null;
  return {
    receiptAddress: receipt.toBase58(),
    ownerAddress: owner.toBase58(),
    resultAuthority: receiptState.resultAuthority,
    idempotencyHash: receiptState.idempotencyHash,
    amount: receiptState.amount,
    balanceAfter: receiptState.balanceAfter,
    dailyRamEarned: profileState.dailyRamEarned,
    lifetimeRamEarned: profileState.lifetimeRamEarned,
    utcDay: receiptState.utcDay,
  };
}

export async function fetchAndVerifyRamGrant(args: {
  config: DevnetSmokeGrantConfig;
  walletAddress: string;
  idempotencyKey: string;
}): Promise<VerifiedRamGrant | null> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const idempotencyHash = await sha256Bytes(args.idempotencyKey);
  const [profile] = PublicKey.findProgramAddressSync(
    [utf8("player_profile"), owner.toBytes()],
    args.config.programId,
  );
  const [receipt] = PublicKey.findProgramAddressSync(
    [utf8("ram_grant_receipt"), owner.toBytes(), idempotencyHash],
    args.config.programId,
  );
  const connection = new Connection(args.config.rpcUrl, "confirmed");
  const [profileInfo, receiptInfo] = await connection.getMultipleAccountsInfo([profile, receipt], "confirmed");
  if (!profileInfo || !receiptInfo) return null;
  requireProgramAccount(
    profileInfo.owner,
    args.config.programId,
    new Uint8Array(profileInfo.data),
    PLAYER_PROFILE_DISCRIMINATOR,
    "invalid_player_profile_account",
  );
  requireProgramAccount(
    receiptInfo.owner,
    args.config.programId,
    new Uint8Array(receiptInfo.data),
    RAM_GRANT_RECEIPT_DISCRIMINATOR,
    "invalid_ram_grant_receipt",
  );
  const profileState = decodePlayerProfile(new Uint8Array(profileInfo.data));
  const receiptState = decodeRamGrantReceipt(new Uint8Array(receiptInfo.data));
  if (profileState.owner !== owner.toBase58() || receiptState.owner !== owner.toBase58()) return null;
  if (receiptState.idempotencyHash !== bytesToHex(idempotencyHash)) return null;
  if (receiptState.resultAuthority !== args.config.resultAuthority.publicKey.toBase58()) return null;
  if (receiptState.amount !== RAM_DAILY_CAP || receiptState.balanceAfter !== RAM_DAILY_CAP) return null;
  if (profileState.ramBalance !== RAM_DAILY_CAP || profileState.dailyRamEarned !== RAM_DAILY_CAP) return null;
  return {
    receiptAddress: receipt.toBase58(),
    ownerAddress: owner.toBase58(),
    resultAuthority: receiptState.resultAuthority,
    idempotencyHash: receiptState.idempotencyHash,
    amount: receiptState.amount,
    balanceAfter: receiptState.balanceAfter,
    dailyRamEarned: profileState.dailyRamEarned,
    lifetimeRamEarned: profileState.lifetimeRamEarned,
    utcDay: receiptState.utcDay,
  };
}

export async function signatureIsConfirmed(rpcUrl: string, signature: string): Promise<boolean> {
  return await signatureOutcome(rpcUrl, signature) === "confirmed";
}

export async function signatureOutcome(rpcUrl: string, signature: string): Promise<SignatureOutcome> {
  const connection = new Connection(rpcUrl, "confirmed");
  const result = await connection.getSignatureStatuses([signature], { searchTransactionHistory: true });
  const status = result.value[0];
  if (!status) return "missing";
  if (status.err) return "failed";
  return status.confirmationStatus === "confirmed" || status.confirmationStatus === "finalized"
    ? "confirmed"
    : "pending";
}

export async function finalizedSignatureOutcome(rpcUrl: string, signature: string): Promise<SignatureOutcome> {
  const connection = new Connection(rpcUrl, "finalized");
  const result = await connection.getSignatureStatuses([signature], { searchTransactionHistory: true });
  const status = result.value[0];
  if (!status) return "missing";
  if (status.err) return "failed";
  return status.confirmationStatus === "finalized" ? "confirmed" : "pending";
}

export async function submittedLegacyTransactionOutcome(
  rpcUrl: string,
  signature: string,
  expectedTransactionBase64: string,
): Promise<SubmittedTransactionOutcome> {
  const response = await fetch(rpcUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: 1,
      method: "getTransaction",
      params: [signature, {
        commitment: "finalized",
        encoding: "base64",
        maxSupportedTransactionVersion: 0,
      }],
    }),
  });
  if (!response.ok) throw new Error("solana_rpc_unavailable");
  const payload = await response.json() as {
    error?: unknown;
    result?: { meta?: { err?: unknown }; transaction?: [string, string] } | null;
  };
  if (payload.error) throw new Error("solana_rpc_error");
  if (!payload.result?.transaction?.[0]) return "pending";
  const actual = Transaction.from(Buffer.from(payload.result.transaction[0], "base64"));
  const expected = Transaction.from(Buffer.from(expectedTransactionBase64, "base64"));
  if (!equalBytes(new Uint8Array(actual.serializeMessage()), new Uint8Array(expected.serializeMessage()))) {
    return "mismatch";
  }
  return payload.result.meta?.err ? "failed" : "confirmed";
}

export async function finalizedBlockHeight(rpcUrl: string): Promise<number> {
  return await new Connection(rpcUrl, "finalized").getBlockHeight("finalized");
}

export function stampMetadataUri(metadataUriBase: string, assetAddress: string): string {
  return `${
    metadataUriBase.replace(/\/+$/, "")
  }/stamped/v${STAMP_METADATA_RENDER_VERSION}/${assetAddress}/metadata.json`;
}

/**
 * Verify the program-owned receipt written by the same atomic instruction that
 * consumes the Known move, debits RAM, and creates the Core asset. This is the
 * settlement authority; DAS is an asynchronous ownership/presentation index.
 */
export async function fetchAndVerifyStampReceipt(args: {
  config: OwnershipConfig;
  walletAddress: string;
  idempotencyKey: string;
  expectedKnownOnchainInstanceId: string;
  expectedMoveId: number;
  expectedModifierIds: string[];
  expectedRarity: number;
  expectedTicketAddress?: string;
  expectedStampedAssetAddress?: string;
}): Promise<VerifiedStampReceipt | null> {
  const owner = publicKey(args.walletAddress, "invalid_wallet_public_key");
  const operationHash = await sha256Bytes(args.idempotencyKey);
  const [ticket] = PublicKey.findProgramAddressSync(
    [utf8("stamp_ticket"), owner.toBytes(), operationHash],
    args.config.programId,
  );
  const [stampedAsset] = PublicKey.findProgramAddressSync(
    [utf8("stamped_asset"), owner.toBytes(), operationHash],
    args.config.programId,
  );
  if (args.expectedTicketAddress && ticket.toBase58() !== args.expectedTicketAddress) return null;
  if (args.expectedStampedAssetAddress && stampedAsset.toBase58() !== args.expectedStampedAssetAddress) return null;

  const connection = new Connection(args.config.rpcUrl, "finalized");
  const [ticketInfo, stampedInfo] = await connection.getMultipleAccountsInfo([ticket, stampedAsset], "finalized");
  if (!ticketInfo || !stampedInfo) return null;
  requireProgramAccount(
    ticketInfo.owner,
    args.config.programId,
    new Uint8Array(ticketInfo.data),
    STAMP_TICKET_DISCRIMINATOR,
    "invalid_stamp_ticket_account",
  );
  if (!stampedInfo.owner.equals(new PublicKey(CORE_PROGRAM_ID))) return null;

  const state = decodeStampTicket(new Uint8Array(ticketInfo.data));
  const expectedModifiers = args.expectedModifierIds.filter(Boolean);
  const baseMetadataUri = args.config.metadataUriBase.replace(/\/+$/, "");
  const acceptedAssetUris = new Set([
    `${baseMetadataUri}/stamped/${stampedAsset.toBase58()}.json`,
    ...Array.from(
      { length: STAMP_METADATA_RENDER_VERSION - 1 },
      (_, index) => `${baseMetadataUri}/stamped/v${index + 2}/${stampedAsset.toBase58()}/metadata.json`,
    ),
  ]);
  if (
    !state.consumed || state.owner !== owner.toBase58() ||
    state.idempotencyHash !== bytesToHex(operationHash) ||
    state.knownInstanceId !== args.expectedKnownOnchainInstanceId ||
    state.moveId !== args.expectedMoveId ||
    state.modifierCount !== expectedModifiers.length ||
    state.modifierIds.some((value, index) => value !== (expectedModifiers[index] ?? "")) ||
    state.rarity !== args.expectedRarity ||
    state.stampedAsset !== stampedAsset.toBase58() ||
    state.generatorVersion !== args.config.generatorVersion ||
    state.poolHash !== args.config.modifierPoolHash ||
    !acceptedAssetUris.has(state.assetUri)
  ) return null;

  return {
    ticketAddress: ticket.toBase58(),
    idempotencyHash: state.idempotencyHash,
    assetUri: state.assetUri,
    assetAddress: stampedAsset.toBase58(),
    ownerAddress: owner.toBase58(),
    collectionAddress: args.config.movesCollection.toBase58(),
    updateAuthority: args.config.movesUpdateAuthority.toBase58(),
    moveId: String(state.moveId),
    knownOnchainInstanceId: state.knownInstanceId,
    rarity: String(state.rarity),
    modifier1Id: state.modifierIds[0] ?? "",
    modifier2Id: state.modifierIds[1] ?? "",
    modifierCount: state.modifierCount,
    schemaVersion: args.config.schemaVersion,
    generatorVersion: state.generatorVersion,
    poolHash: state.poolHash,
    isCoreAsset: true,
  };
}

export async function findConfirmedSignatureForAddress(rpcUrl: string, address: string): Promise<string> {
  const connection = new Connection(rpcUrl, "confirmed");
  const signatures = await connection.getSignaturesForAddress(
    publicKey(address, "invalid_reconciliation_address"),
    { limit: 20 },
    "confirmed",
  );
  for (const candidate of signatures) {
    if (
      !candidate.err &&
      (candidate.confirmationStatus === "confirmed" || candidate.confirmationStatus === "finalized") &&
      isSolanaSignature(candidate.signature)
    ) return candidate.signature;
  }
  return "";
}

export async function findFinalizedSignatureForAddress(rpcUrl: string, address: string): Promise<string> {
  const connection = new Connection(rpcUrl, "finalized");
  const signatures = await connection.getSignaturesForAddress(
    publicKey(address, "invalid_reconciliation_address"),
    { limit: 20 },
    "finalized",
  );
  for (const candidate of signatures) {
    if (!candidate.err && candidate.confirmationStatus === "finalized" && isSolanaSignature(candidate.signature)) {
      return candidate.signature;
    }
  }
  return "";
}

export function isSolanaSignature(value: string): boolean {
  try {
    return bs58Decode(value).length === 64;
  } catch {
    return false;
  }
}

export async function fetchAndVerifyStampedAsset(
  config: OwnershipConfig,
  assetAddress: string,
  expectedOwner: string,
): Promise<VerifiedStampedAsset | null> {
  const response = await fetch(config.dasUrl, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      jsonrpc: "2.0",
      id: "chainbound-ownership",
      method: "getAsset",
      params: { id: assetAddress },
    }),
  });
  if (!response.ok) throw new Error(`das_http_${response.status}`);
  const payload = await response.json() as JsonMap;
  if (payload.error || !payload.result || typeof payload.result !== "object") return null;
  const asset = payload.result as JsonMap;
  const owner = normalizeKey((asset.ownership as JsonMap | undefined)?.owner);
  const collection = findCollection(asset);
  const updateAuthority = findAuthority(asset);
  // Only the on-chain Core Attributes plugin is authoritative. Off-chain JSON
  // metadata is presentation data and must never satisfy ownership checks.
  const attrs = collectAttributes(asset.plugins);
  const modifierCount = Number(attrs["cb.modifier_count"] ?? 0);
  const schemaVersion = Number(attrs["cb.schema_version"] ?? 0);
  const generatorVersion = Number(attrs["cb.generator_version"] ?? 0);
  const interfaceName = str(asset.interface).toLowerCase();
  const content = (asset.content as JsonMap | undefined) ?? {};
  const metadata = (content.metadata as JsonMap | undefined) ?? {};
  const core = interfaceName.includes("core") || str(metadata.interface).toLowerCase().includes("core");
  if (
    !core || owner !== expectedOwner || collection !== config.movesCollection.toBase58() ||
    updateAuthority !== config.movesUpdateAuthority.toBase58()
  ) return null;
  if (attrs["cb.kind"] !== "stamped_move" || attrs["cb.move_tier"] !== "stamped") return null;
  if (
    schemaVersion !== config.schemaVersion || generatorVersion !== config.generatorVersion ||
    attrs["cb.pool_hash"] !== config.modifierPoolHash
  ) return null;
  if (
    (modifierCount !== 1 && modifierCount !== 2) || !attrs["cb.modifier_1"] ||
    (modifierCount === 2 && (!attrs["cb.modifier_2"] || attrs["cb.modifier_2"] === attrs["cb.modifier_1"]))
  ) return null;
  return {
    assetAddress,
    ownerAddress: owner,
    collectionAddress: collection,
    updateAuthority,
    moveId: attrs["cb.move_id"] ?? "",
    knownOnchainInstanceId: attrs["cb.known_instance_id"] ?? "",
    rarity: attrs["cb.rarity"] ?? "",
    modifier1Id: attrs["cb.modifier_1"] ?? "",
    modifier2Id: attrs["cb.modifier_2"] ?? "",
    modifierCount,
    schemaVersion,
    generatorVersion,
    poolHash: attrs["cb.pool_hash"] ?? "",
    isCoreAsset: true,
  };
}

export function verifiedStampedAssetMatchesOperation(
  asset: VerifiedStampedAsset,
  operation: JsonMap,
): boolean {
  const modifierIds = Array.isArray(operation.modifier_ids) ? operation.modifier_ids.map((value) => str(value)) : [];
  const modifierCount = Number(operation.modifier_count);
  return asset.knownOnchainInstanceId === String(operation.onchain_instance_id) &&
    asset.modifierCount === modifierCount &&
    asset.modifier1Id === (modifierIds[0] ?? "") &&
    asset.modifier2Id === (modifierIds[1] ?? "") &&
    asset.rarity === String(operation.rarity) &&
    asset.generatorVersion === Number(operation.generator_version) &&
    asset.poolHash === str(operation.pool_hash);
}

export function authoritySecretBytes(keypair: Keypair): Uint8Array {
  return keypair.secretKey;
}

export async function sha256Hex(value: string): Promise<string> {
  return Array.from(await sha256Bytes(value)).map((value) => value.toString(16).padStart(2, "0")).join("");
}

export function normalizeAcquisition(value: string): string {
  if (value === "progression_learn" || value === "event_grant" || value === "candy_roll") return value;
  return "candy_roll";
}

export function hashMoveId(moveId: string): number {
  let hash = 2166136261 >>> 0;
  for (let i = 0; i < moveId.length; i++) {
    hash ^= moveId.charCodeAt(i);
    hash = Math.imul(hash, 16777619) >>> 0;
  }
  return hash & 0xffff;
}

function requiredEnv(name: string): string {
  const value = str(Deno.env.get(name));
  if (!value || /^REPLACE_/i.test(value)) throw new OwnershipConfigurationError(`missing_env:${name}`);
  return value;
}
function positiveIntEnv(name: string): number {
  const value = Number(requiredEnv(name));
  if (!Number.isSafeInteger(value) || value <= 0) throw new OwnershipConfigurationError(`invalid_env:${name}`);
  return value;
}
function hashEnv(name: string): string {
  const value = requiredEnv(name).toLowerCase();
  if (!/^[0-9a-f]{64}$/.test(value)) throw new OwnershipConfigurationError(`invalid_env:${name}`);
  return value;
}
function publicKey(value: string, code: string): PublicKey {
  if (!SOLANA_BASE58_RE.test(value)) throw new OwnershipConfigurationError(code);
  try {
    return new PublicKey(value);
  } catch {
    throw new OwnershipConfigurationError(code);
  }
}
function readResultAuthority(value: string): Keypair {
  try {
    const raw = value.startsWith("[") ? Uint8Array.from(JSON.parse(value) as number[]) : bs58Decode(value);
    if (raw.length === 64) return Keypair.fromSecretKey(raw);
    if (raw.length === 32) return Keypair.fromSeed(raw);
  } catch { /* normalized below */ }
  throw new OwnershipConfigurationError("invalid_result_authority_secret");
}
function bs58Decode(value: string): Uint8Array {
  const shape = bs58Module as unknown as {
    decode?: (v: string) => Uint8Array;
    default?: { decode?: (v: string) => Uint8Array };
  };
  const decode = shape.decode ?? shape.default?.decode;
  if (!decode) throw new Error("bs58_unavailable");
  return decode(value);
}
async function authorityDigest(secret: Uint8Array, context: string): Promise<Uint8Array> {
  const input = concat(secret, utf8(context));
  return new Uint8Array(await crypto.subtle.digest("SHA-256", input as BufferSource));
}
async function sha256Bytes(value: string): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", utf8(value) as BufferSource));
}
function decodeMoveVault(data: Uint8Array): { nextInstanceId: bigint; instanceIds: bigint[] } {
  if (data.length < 55 || !equalBytes(data.slice(0, 8), MOVE_VAULT_DISCRIMINATOR)) {
    throw new Error("invalid_move_vault_account");
  }
  let offset = 8 + 32;
  const capacity = data[offset++];
  const count = data[offset++];
  const vectorCount = readU32(data, offset);
  offset += 4;
  if (vectorCount !== count || vectorCount > capacity || data.length < offset + vectorCount * 49 + 9) {
    throw new Error("invalid_move_vault_layout");
  }
  const instanceIds: bigint[] = [];
  for (let i = 0; i < vectorCount; i++) {
    instanceIds.push(readU64(data, offset));
    offset += 49;
  }
  return { instanceIds, nextInstanceId: readU64(data, offset) };
}
function decodePlayerProfile(data: Uint8Array): {
  owner: string;
  ramBalance: number;
  dailyRamEarned: number;
  lifetimeRamEarned: bigint;
} {
  if (data.length !== 91 || !equalBytes(data.slice(0, 8), PLAYER_PROFILE_DISCRIMINATOR)) {
    throw new Error("invalid_player_profile_account");
  }
  const owner = new PublicKey(data.slice(8, 40)).toBase58();
  const ramOffset = 68;
  return {
    owner,
    ramBalance: readU32(data, ramOffset),
    dailyRamEarned: readU16(data, ramOffset + 4),
    lifetimeRamEarned: readU64(data, ramOffset + 14),
  };
}
function decodeGameConfig(data: Uint8Array): {
  resultAuthority: string;
  ramDailyCap: number;
  instructionFlags: bigint;
} {
  if (data.length !== 89 || !equalBytes(data.slice(0, 8), GAME_CONFIG_DISCRIMINATOR)) {
    throw new Error("invalid_game_config_account");
  }
  return {
    resultAuthority: new PublicKey(data.slice(40, 72)).toBase58(),
    ramDailyCap: readU16(data, 74),
    instructionFlags: readU64(data, 80),
  };
}
function decodeRamGrantReceipt(data: Uint8Array): {
  owner: string;
  idempotencyHash: string;
  amount: number;
  balanceAfter: number;
  utcDay: bigint;
  resultAuthority: string;
} {
  if (data.length !== 121 || !equalBytes(data.slice(0, 8), RAM_GRANT_RECEIPT_DISCRIMINATOR)) {
    throw new Error("invalid_ram_grant_receipt");
  }
  return {
    owner: new PublicKey(data.slice(8, 40)).toBase58(),
    idempotencyHash: bytesToHex(data.slice(40, 72)),
    amount: readU32(data, 72),
    balanceAfter: readU32(data, 76),
    utcDay: readI64(data, 80),
    resultAuthority: new PublicKey(data.slice(88, 120)).toBase58(),
  };
}

function decodeStampTicket(data: Uint8Array): {
  owner: string;
  idempotencyHash: string;
  knownInstanceId: string;
  moveId: number;
  modifierCount: number;
  modifierIds: string[];
  rarity: number;
  assetUri: string;
  consumed: boolean;
  stampedAsset: string;
  generatorVersion: number;
  poolHash: string;
} {
  if (data.length < 192 || !equalBytes(data.slice(0, 8), STAMP_TICKET_DISCRIMINATOR)) {
    throw new Error("invalid_stamp_ticket_account");
  }
  let offset = 8;
  const owner = new PublicKey(data.slice(offset, offset + 32)).toBase58();
  offset += 32;
  const idempotencyHash = bytesToHex(data.slice(offset, offset + 32));
  offset += 32;
  const knownInstanceId = readU64(data, offset).toString();
  offset += 8;
  const moveId = readU16(data, offset);
  offset += 2;
  const modifierCount = data[offset++];
  const modifierIds: string[] = [];
  for (let i = 0; i < 2; i++) {
    const decoded = readBorshString(data, offset);
    modifierIds.push(decoded.value);
    offset = decoded.offset;
  }
  const rarity = data[offset++];
  const assetName = readBorshString(data, offset);
  offset = assetName.offset;
  const assetUri = readBorshString(data, offset);
  offset = assetUri.offset;
  if (offset + 8 + 1 + 32 + 4 + 32 + 1 > data.length) throw new Error("invalid_stamp_ticket_layout");
  offset += 8; // expires_at
  const consumed = data[offset++] !== 0;
  const stampedAsset = new PublicKey(data.slice(offset, offset + 32)).toBase58();
  offset += 32;
  const generatorVersion = readU32(data, offset);
  offset += 4;
  const poolHash = bytesToHex(data.slice(offset, offset + 32));
  if (modifierCount > 2 || modifierIds.slice(modifierCount).some((value) => value !== "")) {
    throw new Error("invalid_stamp_ticket_modifier_shape");
  }
  return {
    owner,
    idempotencyHash,
    knownInstanceId,
    moveId,
    modifierCount,
    modifierIds: modifierIds.slice(0, modifierCount),
    rarity,
    assetUri: assetUri.value,
    consumed,
    stampedAsset,
    generatorVersion,
    poolHash,
  };
}

function readBorshString(data: Uint8Array, offset: number): { value: string; offset: number } {
  if (offset + 4 > data.length) throw new Error("invalid_borsh_string");
  const length = readU32(data, offset);
  offset += 4;
  if (length > 1024 || offset + length > data.length) throw new Error("invalid_borsh_string");
  return { value: new TextDecoder().decode(data.slice(offset, offset + length)), offset: offset + length };
}
function requireProgramAccount(
  actualOwner: PublicKey,
  expectedOwner: PublicKey,
  data: Uint8Array,
  discriminator: Uint8Array,
  code: string,
): void {
  if (!actualOwner.equals(expectedOwner) || data.length < 8 || !equalBytes(data.slice(0, 8), discriminator)) {
    throw new Error(code);
  }
}
function meta(pubkey: PublicKey, isSigner: boolean, isWritable: boolean) {
  return { pubkey, isSigner, isWritable };
}
type InstructionData = ConstructorParameters<typeof TransactionInstruction>[0]["data"];
function instructionData(value: Uint8Array): InstructionData {
  return Buffer.from(value) as unknown as InstructionData;
}
function utf8(value: string): Uint8Array {
  return new TextEncoder().encode(value);
}
function bytes(...values: number[]): Uint8Array {
  return Uint8Array.from(values);
}
function concat(...values: Uint8Array[]): Uint8Array {
  const out = new Uint8Array(values.reduce((sum, value) => sum + value.length, 0));
  let offset = 0;
  for (const value of values) {
    out.set(value, offset);
    offset += value.length;
  }
  return out;
}
function u16(value: number): Uint8Array {
  const b = new Uint8Array(2);
  new DataView(b.buffer).setUint16(0, value, true);
  return b;
}
function u32(value: number): Uint8Array {
  const b = new Uint8Array(4);
  new DataView(b.buffer).setUint32(0, value, true);
  return b;
}
function u64(value: bigint): Uint8Array {
  const b = new Uint8Array(8);
  new DataView(b.buffer).setBigUint64(0, value, true);
  return b;
}
function i64(value: bigint): Uint8Array {
  const b = new Uint8Array(8);
  new DataView(b.buffer).setBigInt64(0, value, true);
  return b;
}
function borshString(value: string): Uint8Array {
  const data = utf8(value);
  return concat(u32(data.length), data);
}
function stringVector(values: string[]): Uint8Array {
  return concat(u32(values.length), ...values.map(borshString));
}
function readU32(value: Uint8Array, offset: number): number {
  return new DataView(value.buffer, value.byteOffset, value.byteLength).getUint32(offset, true);
}
function readU16(value: Uint8Array, offset: number): number {
  return new DataView(value.buffer, value.byteOffset, value.byteLength).getUint16(offset, true);
}
function readU64(value: Uint8Array, offset: number): bigint {
  return new DataView(value.buffer, value.byteOffset, value.byteLength).getBigUint64(offset, true);
}
function readI64(value: Uint8Array, offset: number): bigint {
  return new DataView(value.buffer, value.byteOffset, value.byteLength).getBigInt64(offset, true);
}
function equalBytes(a: Uint8Array, b: Uint8Array): boolean {
  return a.length === b.length && a.every((value, index) => value === b[index]);
}
function bytesToHex(value: Uint8Array): string {
  return Array.from(value).map((byte) => byte.toString(16).padStart(2, "0")).join("");
}
function uint8ToBase64(value: Uint8Array): string {
  let raw = "";
  for (const byte of value) raw += String.fromCharCode(byte);
  return btoa(raw);
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
  const attr = str(record.value);
  if (key && attr) out[key] = attr;
  for (const child of ["attributes", "attributeList", "attribute_list", "metadata", "content", "plugins"]) {
    if (child in record) collectAttributes(record[child], out, depth + 1);
  }
  return out;
}
