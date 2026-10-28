import {
  ComputeBudgetProgram,
  Connection,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} from "npm:@solana/web3.js@1.98.4";
import {
  createTransferCheckedInstruction,
  getAssociatedTokenAddressSync,
  TOKEN_PROGRAM_ID,
} from "npm:@solana/spl-token@0.4.14";
import bs58 from "npm:bs58@5.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import {
  ensureRamLedgerAccount,
  getActiveWallet,
  JsonMap,
  requireBoundWallet,
  str,
  WalletBindingError,
} from "./account_helpers.ts";
import {
  CHAINBOUND_PROGRAM_ID,
  DEVNET_GENESIS_HASH,
  ensureOwnershipRpcCluster,
  loadOwnershipConfig,
  MAINNET_GENESIS_HASH,
} from "./ownership_spine.ts";

const RAM_PURCHASE_RECEIPT_DISCRIMINATOR = Uint8Array.from([37, 105, 212, 100, 110, 164, 29, 144]);
export const RAM_SHOP_COMPUTE_UNIT_LIMIT = 200_000;
export const RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS = 1_000;
const MIN_COMPUTE_UNIT_LIMIT = 50_000;
const MAX_COMPUTE_UNIT_LIMIT = 400_000;
const MAX_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS = 100_000n;

export type RamTender = "SOL" | "USDC" | "SKR";
export type RamPurchaseState =
  | "quoted"
  | "submitted"
  | "payment_verified"
  | "credit_submitted"
  | "settled"
  | "expired_unpaid"
  | "rejected"
  | "failed"
  | "manual_review";

export interface RamShopRuntimeConfig {
  cluster: "devnet" | "mainnet-beta";
  rpcUrl: string;
  expectedGenesisHash: string;
  programId: PublicKey;
  programVersion: string;
}

export interface PurchaseOrderRow extends JsonMap {
  id: number;
  order_id: string;
  player_profile_id: string;
  wallet_address: string;
  cluster: "devnet" | "mainnet-beta";
  sku: string;
  bundle_name: string;
  ram_amount: number;
  tender: RamTender;
  payment_atomic_amount: string;
  mint_address: string | null;
  decimals: number;
  token_program: string | null;
  destination: string;
  reference_address: string;
  expected_message_base64: string;
  expected_message_hash: string;
  unsigned_transaction_base64: string;
  recent_blockhash: string;
  last_valid_block_height: number;
  estimated_fee_lamports: string;
  status: RamPurchaseState;
  payment_signature: string | null;
  credit_signature: string | null;
  credit_receipt_address: string | null;
  balance_after: number | null;
  balance_version?: number | null;
  failure_code: string | null;
  quote_expires_at: string;
}

export class RamShopError extends Error {
  constructor(public readonly code: string, public readonly httpStatus = 400) {
    super(code);
  }
}

export function loadRamShopRuntimeConfig(): RamShopRuntimeConfig {
  const cluster = str(Deno.env.get("RAM_SHOP_CLUSTER"));
  if (cluster !== "devnet") throw new RamShopError("ram_shop_devnet_only", 503);
  const rpcUrl = str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT"));
  if (!rpcUrl.startsWith("https://")) throw new RamShopError("ram_shop_rpc_missing", 503);
  const configuredGenesis = str(Deno.env.get("RAM_SHOP_RPC_GENESIS_HASH"));
  const expectedGenesisHash = cluster === "devnet" ? DEVNET_GENESIS_HASH : MAINNET_GENESIS_HASH;
  if (configuredGenesis !== expectedGenesisHash) throw new RamShopError("ram_shop_genesis_not_pinned", 503);
  const programVersion = str(Deno.env.get("RAM_SHOP_PROGRAM_VERSION"));
  if (!programVersion) throw new RamShopError("ram_shop_program_version_missing", 503);
  const programText = str(Deno.env.get("CHAINBOUND_PROGRAM_ID"));
  const gameProgramText = str(Deno.env.get("CHAINBOUND_GAME_PROGRAM_ID"));
  if (programText !== CHAINBOUND_PROGRAM_ID || gameProgramText !== programText) {
    throw new RamShopError("ram_shop_program_not_pinned", 503);
  }
  return { cluster, rpcUrl, expectedGenesisHash, programId: new PublicKey(programText), programVersion };
}

// R2 Task 8: token tenders are classic SPL Token with pinned mints. USDC is pinned
// on both clusters and SKR on mainnet, here and in the DB CHECKs; the devnet SKR
// test mint exists only as a ram_shop_tender_mints row Cronus adds (SKR has no
// devnet mint). Every token price row must match a ram_shop_tender_mints pin.
export const PINNED_TENDER_MINTS: Record<string, Partial<Record<RamTender, string>>> = {
  "devnet": { USDC: "4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU" },
  "mainnet-beta": {
    USDC: "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v",
    SKR: "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3",
  },
};
export const TOKEN_TENDER_DECIMALS = 6;

export interface TenderMintPin {
  mint: string;
  token_program: string;
  decimals: number;
}

// Checks a token price against the code pin and the DB pin row.
export function requirePinnedTenderMint(
  cluster: string,
  tender: RamTender,
  mint: PublicKey,
  tokenProgram: PublicKey,
  decimals: number,
  databasePin: TenderMintPin | null,
): void {
  if (tender === "SOL") throw new RamShopError("invalid_tender_mint", 503);
  if (!tokenProgram.equals(TOKEN_PROGRAM_ID)) throw new RamShopError("unsupported_token_program", 503);
  const codePin = PINNED_TENDER_MINTS[cluster]?.[tender];
  if (codePin !== undefined && mint.toBase58() !== codePin) throw new RamShopError("tender_mint_not_pinned", 503);
  if (
    !databasePin || databasePin.mint !== mint.toBase58() || databasePin.token_program !== tokenProgram.toBase58() ||
    Number(databasePin.decimals) !== decimals
  ) {
    throw new RamShopError("tender_mint_not_pinned", 503);
  }
  if (decimals !== TOKEN_TENDER_DECIMALS) throw new RamShopError("tender_decimals_mismatch", 503);
}

// Parsed mint info: a classic SPL Token mint with the pinned decimals.
export function checkTenderMintAccount(owner: PublicKey | null, info: JsonMap | undefined, decimals: number): void {
  if (!owner || !owner.equals(TOKEN_PROGRAM_ID)) throw new RamShopError("tender_mint_owner_mismatch", 503);
  if (info?.isInitialized === false) throw new RamShopError("tender_mint_owner_mismatch", 503);
  if (Number(info?.decimals ?? -1) !== decimals) throw new RamShopError("tender_decimals_mismatch", 503);
}

// Parsed info for the payer's source ATA. A missing account or a short balance is
// the player's to fix, so these are 409s, not 500s.
export function checkPayerTokenAccount(
  info: JsonMap | undefined,
  owner: PublicKey,
  mint: PublicKey,
  amount: bigint,
): void {
  if (!info) throw new RamShopError("tender_token_account_missing", 409);
  if (str(info.mint) !== mint.toBase58() || str(info.owner) !== owner.toBase58()) {
    throw new RamShopError("tender_token_account_missing", 409);
  }
  if (str(info.state) !== "initialized") throw new RamShopError("tender_token_account_frozen", 409);
  const raw = str((info.tokenAmount as JsonMap | undefined)?.amount);
  if (!/^[0-9]+$/.test(raw) || BigInt(raw) < amount) throw new RamShopError("tender_balance_insufficient", 409);
}

// R2 Task 7: the payee is the per-cluster treasury owner in ram_shop_treasuries,
// pinned by the RAM_SHOP_TREASURY_OWNER Edge secret. Missing or different values
// close checkout; a price row whose destination doesn't match disables that tender.
export const RAM_SHOP_TREASURY_OWNER_ENV = "RAM_SHOP_TREASURY_OWNER";

export function resolveTreasuryOwner(databaseOwner: string, pinnedOwner: string): PublicKey {
  if (!databaseOwner) throw new RamShopError("treasury_owner_not_configured", 503);
  if (!pinnedOwner) throw new RamShopError("treasury_owner_not_pinned", 503);
  if (databaseOwner !== pinnedOwner) throw new RamShopError("treasury_owner_mismatch", 503);
  return publicKey(databaseOwner, "invalid_treasury_owner");
}

export async function loadTreasuryOwner(supabase: SupabaseClient, cluster: string): Promise<PublicKey> {
  const { data, error } = await supabase.from("ram_shop_treasuries").select("owner")
    .eq("cluster", cluster).maybeSingle();
  if (error) throw error;
  return resolveTreasuryOwner(str(data?.owner), str(Deno.env.get(RAM_SHOP_TREASURY_OWNER_ENV)));
}

// SOL goes to the owner itself (a Squads vault PDA is a system account). Tokens go
// to the owner's ATA; allowOwnerOffCurve because a vault owner is a PDA.
export function expectedTreasuryDestination(
  tender: RamTender,
  owner: PublicKey,
  mint: PublicKey | null,
  tokenProgram: PublicKey | null,
): PublicKey {
  if (tender === "SOL") return owner;
  if (!mint || !tokenProgram) throw new RamShopError("invalid_tender_mint", 503);
  return getAssociatedTokenAddressSync(mint, owner, true, tokenProgram);
}

export function requireTreasuryDestination(
  tender: RamTender,
  configuredDestination: string,
  owner: PublicKey,
  mint: PublicKey | null,
  tokenProgram: PublicKey | null,
): PublicKey {
  const expected = expectedTreasuryDestination(tender, owner, mint, tokenProgram);
  if (configuredDestination !== expected.toBase58()) throw new RamShopError("treasury_destination_mismatch", 503);
  return expected;
}

// Parsed SPL token account info for the treasury destination.
export function checkTreasuryTokenAccount(info: JsonMap | undefined, owner: PublicKey, mint: PublicKey): void {
  if (str(info?.mint) !== mint.toBase58()) throw new RamShopError("treasury_token_mint_mismatch", 503);
  if (str(info?.owner) !== owner.toBase58()) throw new RamShopError("treasury_token_owner_mismatch", 503);
  if (str(info?.state) !== "initialized") throw new RamShopError("treasury_token_account_unusable", 503);
}

// Compares secrets in time that doesn't depend on where they differ: both sides
// are hashed to 32 bytes first, so length doesn't leak either.
export async function secretsMatch(provided: string, expected: string): Promise<boolean> {
  if (!expected) return false;
  const encoder = new TextEncoder();
  const [a, b] = await Promise.all([
    crypto.subtle.digest("SHA-256", encoder.encode(provided)),
    crypto.subtle.digest("SHA-256", encoder.encode(expected)),
  ]);
  const left = new Uint8Array(a);
  const right = new Uint8Array(b);
  let diff = 0;
  for (let index = 0; index < left.length; index++) diff |= left[index] ^ right[index];
  return diff === 0;
}

export async function catalogResponse(supabase: SupabaseClient, profileId: string): Promise<JsonMap> {
  const { data: profile, error: profileError } = await supabase
    .from("player_profiles").select("ram_balance, ram_balance_version").eq("player_profile_id", profileId).single();
  if (profileError) throw profileError;
  const authoritativeBalance = String(profile.ram_balance ?? 0);
  const authoritativeBalanceVersion = Number(profile.ram_balance_version ?? 0);
  const ledgerStatus = "postgres_canonical";

  const { data: settings, error: settingsError } = await supabase
    .from("ram_shop_settings")
    .select("checkout_enabled, checkout_cluster, quote_ttl_seconds, maintenance_reason")
    .eq("singleton", true)
    .maybeSingle();
  if (settingsError) throw settingsError;

  let runtime: RamShopRuntimeConfig | null = null;
  let treasuryOwner: PublicKey | null = null;
  let reason = str(settings?.maintenance_reason) || "RAM checkout is unavailable.";
  try {
    runtime = loadRamShopRuntimeConfig();
    await ensureOwnershipRpcCluster(runtime);
    treasuryOwner = await loadTreasuryOwner(supabase, runtime.cluster);
  } catch (error) {
    runtime = null;
    reason = error instanceof RamShopError ? error.code : "ram_shop_configuration_invalid";
  }

  const enabledByConfig = settings?.checkout_enabled === true && settings?.checkout_cluster === runtime?.cluster &&
    Number(settings?.quote_ttl_seconds ?? 0) > 0;
  if (!runtime || !treasuryOwner || !enabledByConfig) {
    return {
      success: true,
      checkout_available: false,
      maintenance_reason: reason,
      authoritative_balance: authoritativeBalance,
      authoritative_balance_version: authoritativeBalanceVersion,
      ledger_status: ledgerStatus,
      bundles: [],
      tenders: [],
    };
  }

  const { data: catalog, error: catalogError } = await supabase
    .from("ram_shop_catalog_versions")
    .select("id, version")
    .eq("enabled", true)
    .maybeSingle();
  if (catalogError) throw catalogError;
  if (!catalog) {
    return {
      success: true,
      checkout_available: false,
      maintenance_reason: "catalog_not_configured",
      authoritative_balance: authoritativeBalance,
      authoritative_balance_version: authoritativeBalanceVersion,
      ledger_status: ledgerStatus,
      bundles: [],
      tenders: [],
    };
  }

  const { data: rows, error: rowError } = await supabase
    .from("ram_shop_bundles")
    .select(
      "id, sku, display_name, ram_amount, sort_order, approved_badge, ram_shop_tender_prices!inner(tender, atomic_amount, mint_address, decimals, token_program, destination, verified_at)",
    )
    .eq("catalog_version_id", catalog.id)
    .eq("enabled", true)
    .eq("ram_shop_tender_prices.cluster", runtime.cluster)
    .eq("ram_shop_tender_prices.enabled", true)
    .not("ram_shop_tender_prices.verified_at", "is", null)
    .order("sort_order", { ascending: true });
  if (rowError) throw rowError;

  const bundles = (rows ?? []).map((row: JsonMap) => ({
    sku: str(row.sku),
    display_name: str(row.display_name),
    ram_amount: String(row.ram_amount),
    approved_badge: str(row.approved_badge) || null,
    prices: ((row.ram_shop_tender_prices ?? []) as JsonMap[])
      .filter((price) => paysTreasury(price, treasuryOwner!))
      .map((price) => ({
        tender: str(price.tender),
        atomic_amount: String(price.atomic_amount),
        mint: str(price.mint_address) || null,
        decimals: Number(price.decimals),
      })),
  })).filter((bundle) => bundle.prices.length > 0);
  const tenders = [...new Set(bundles.flatMap((bundle) => bundle.prices.map((price) => price.tender)))];
  const available = bundles.length > 0 && tenders.length > 0;
  return {
    success: true,
    checkout_available: available,
    maintenance_reason: available ? null : "catalog_not_configured",
    authoritative_balance: authoritativeBalance,
    authoritative_balance_version: authoritativeBalanceVersion,
    ledger_status: ledgerStatus,
    catalog_version: catalog.version,
    cluster: runtime.cluster,
    bundles,
    tenders,
  };
}

// Catalog-side check (no RPC): a price row is listed only if it pays the treasury.
function paysTreasury(price: JsonMap, owner: PublicKey): boolean {
  try {
    const tender = normalizeTender(str(price.tender));
    const mint = tender === "SOL" ? null : publicKey(str(price.mint_address), "invalid_tender_mint");
    const program = tender === "SOL" ? null : publicKey(str(price.token_program), "invalid_token_program");
    requireTreasuryDestination(tender, str(price.destination), owner, mint, program);
    return true;
  } catch {
    return false;
  }
}

export async function preparePurchase(
  supabase: SupabaseClient,
  profileId: string,
  requestedWallet: string,
  sku: string,
  tenderText: string,
): Promise<JsonMap> {
  const tender = normalizeTender(tenderText);
  const runtime = loadRamShopRuntimeConfig();
  await ensureOwnershipRpcCluster(runtime);

  const { data: settings, error: settingsError } = await supabase
    .from("ram_shop_settings")
    .select("checkout_enabled, checkout_cluster, quote_ttl_seconds, maintenance_reason")
    .eq("singleton", true)
    .single();
  if (settingsError) throw settingsError;
  if (settings.checkout_enabled !== true) {
    throw new RamShopError(str(settings.maintenance_reason) || "checkout_disabled", 503);
  }
  if (settings.checkout_cluster !== runtime.cluster) throw new RamShopError("catalog_cluster_mismatch", 503);
  const ttlSeconds = Number(settings.quote_ttl_seconds ?? 0);
  if (!Number.isSafeInteger(ttlSeconds) || ttlSeconds <= 0) throw new RamShopError("quote_ttl_missing", 503);
  const treasuryOwner = await loadTreasuryOwner(supabase, runtime.cluster);

  const wallet = await getActiveWallet(supabase, profileId);
  const walletAddress = str(wallet?.wallet_public_key);
  try {
    await requireBoundWallet(supabase, profileId, requestedWallet);
  } catch (error) {
    if (error instanceof WalletBindingError) throw new RamShopError(error.code, error.httpStatus);
    throw error;
  }
  if (walletAddress && walletAddress !== requestedWallet) throw new RamShopError("wallet_profile_mismatch", 409);
  if (!walletAddress) throw new RamShopError("linked_wallet_required", 409);
  const owner = publicKey(walletAddress, "invalid_linked_wallet");
  await ensureRamLedgerAccount(supabase, profileId, walletAddress, runtime.cluster);

  const { data: ledger, error: ledgerError } = await supabase
    .from("ram_ledger_accounts")
    .select("cluster, wallet_address")
    .eq("player_profile_id", profileId)
    .maybeSingle();
  if (ledgerError) throw ledgerError;
  if (!ledger) throw new RamShopError("ram_ledger_scope_missing", 409);
  if (ledger.cluster !== runtime.cluster || ledger.wallet_address !== walletAddress) {
    throw new RamShopError("ram_ledger_scope_mismatch", 409);
  }

  const { data: catalog, error: catalogError } = await supabase
    .from("ram_shop_catalog_versions").select("id, version").eq("enabled", true).single();
  if (catalogError) throw new RamShopError("catalog_not_configured", 503);
  const { data: bundle, error: bundleError } = await supabase
    .from("ram_shop_bundles")
    .select("id, sku, display_name, ram_amount")
    .eq("catalog_version_id", catalog.id).eq("sku", sku).eq("enabled", true).single();
  if (bundleError) throw new RamShopError("bundle_unavailable", 404);
  const { data: price, error: priceError } = await supabase
    .from("ram_shop_tender_prices")
    .select("tender, atomic_amount, mint_address, decimals, token_program, destination, verified_at")
    .eq("bundle_id", bundle.id).eq("cluster", runtime.cluster).eq("tender", tender).eq("enabled", true).single();
  if (priceError || !price?.verified_at) throw new RamShopError("tender_unavailable", 404);

  const amount = parseAtomicAmount(price.atomic_amount);
  const connection = new Connection(runtime.rpcUrl, "finalized");
  const orderId = crypto.randomUUID();
  const orderHash = await sha256Bytes(`ram-purchase:${orderId}`);
  const [reference] = PublicKey.findProgramAddressSync(
    [new TextEncoder().encode("ram_order_reference"), orderHash],
    runtime.programId,
  );
  let destination: PublicKey;
  let instruction: TransactionInstruction;
  let mintAddress: string | null = null;
  let tokenProgram: string | null = null;

  if (tender === "SOL") {
    if (price.mint_address || price.token_program || Number(price.decimals) !== 9) {
      throw new RamShopError("invalid_sol_price", 503);
    }
    destination = requireTreasuryDestination(tender, str(price.destination), treasuryOwner, null, null);
    instruction = SystemProgram.transfer({ fromPubkey: owner, toPubkey: destination, lamports: amount });
  } else {
    const mint = publicKey(str(price.mint_address), "invalid_tender_mint");
    const program = publicKey(str(price.token_program), "invalid_token_program");
    const { data: pin, error: pinError } = await supabase.from("ram_shop_tender_mints")
      .select("mint, token_program, decimals").eq("cluster", runtime.cluster).eq("tender", tender).maybeSingle();
    if (pinError) throw pinError;
    requirePinnedTenderMint(runtime.cluster, tender, mint, program, Number(price.decimals), pin as TenderMintPin | null);
    destination = requireTreasuryDestination(tender, str(price.destination), treasuryOwner, mint, program);
    const source = getAssociatedTokenAddressSync(mint, owner, false, program);
    await verifyMintAndDestination(
      connection,
      mint,
      program,
      destination,
      treasuryOwner,
      Number(price.decimals),
      source,
      owner,
      amount,
    );
    instruction = createTransferCheckedInstruction(
      source,
      mint,
      destination,
      owner,
      amount,
      Number(price.decimals),
      [],
      program,
    );
    mintAddress = mint.toBase58();
    tokenProgram = program.toBase58();
  }
  instruction.keys.push({ pubkey: reference, isSigner: false, isWritable: false });

  const latest = await connection.getLatestBlockhash("finalized");
  const transaction = new Transaction({ feePayer: owner, recentBlockhash: latest.blockhash }).add(
    ComputeBudgetProgram.setComputeUnitLimit({ units: RAM_SHOP_COMPUTE_UNIT_LIMIT }),
    ComputeBudgetProgram.setComputeUnitPrice({ microLamports: RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS }),
    instruction,
  );
  const messageBytes = transaction.serializeMessage();
  const messageBytesArray = Uint8Array.from(messageBytes);
  const messageBase64 = Buffer.from(messageBytesArray).toString("base64");
  const messageHash = await sha256HexBytes(messageBytesArray);
  const unsignedBase64 = Buffer.from(
    Uint8Array.from(transaction.serialize({ requireAllSignatures: false, verifySignatures: false })),
  )
    .toString("base64");
  const estimatedFee = (await connection.getFeeForMessage(transaction.compileMessage(), "finalized")).value;
  if (estimatedFee === null) throw new RamShopError("fee_estimate_unavailable", 503);
  const quoteExpiresAt = new Date(Date.now() + ttlSeconds * 1000).toISOString();

  const { data: inserted, error: insertError } = await supabase.from("ram_purchase_orders").insert({
    order_id: orderId,
    player_profile_id: profileId,
    wallet_address: walletAddress,
    cluster: runtime.cluster,
    catalog_version: catalog.version,
    sku: bundle.sku,
    bundle_name: bundle.display_name,
    ram_amount: bundle.ram_amount,
    tender,
    payment_atomic_amount: amount.toString(),
    mint_address: mintAddress,
    decimals: Number(price.decimals),
    token_program: tokenProgram,
    destination: destination.toBase58(),
    reference_address: reference.toBase58(),
    expected_message_base64: messageBase64,
    expected_message_hash: messageHash,
    unsigned_transaction_base64: unsignedBase64,
    recent_blockhash: latest.blockhash,
    last_valid_block_height: latest.lastValidBlockHeight,
    estimated_fee_lamports: String(estimatedFee),
    quote_expires_at: quoteExpiresAt,
    reconcile_after: new Date().toISOString(),
  }).select("*").single();
  if (insertError) throw insertError;
  return await orderResponseWithBalanceVersion(supabase, inserted as PurchaseOrderRow);
}

export async function loadOwnedOrder(
  supabase: SupabaseClient,
  profileId: string,
  orderId: string,
): Promise<PurchaseOrderRow> {
  const { data, error } = await supabase.from("ram_purchase_orders").select("*")
    .eq("order_id", orderId).eq("player_profile_id", profileId).maybeSingle();
  if (error) throw error;
  if (!data) throw new RamShopError("order_not_found", 404);
  return data as PurchaseOrderRow;
}

export async function confirmPurchase(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
  signature: string,
  manualReviewOnMismatch = true,
): Promise<JsonMap> {
  if (!isSignature(signature)) throw new RamShopError("invalid_signature");
  if (["settled", "payment_verified", "credit_submitted"].includes(order.status)) {
    if (order.payment_signature && order.payment_signature !== signature) {
      throw new RamShopError("order_signature_mismatch", 409);
    }
    return await advancePurchaseSettlement(supabase, order);
  }
  if (order.status !== "quoted" && order.status !== "submitted") throw new RamShopError("order_not_confirmable", 409);

  const runtime = loadRamShopRuntimeConfig();
  if (runtime.cluster !== order.cluster) throw new RamShopError("order_cluster_mismatch", 503);
  try {
    await verifyFinalizedPayment(runtime.rpcUrl, order, signature);
  } catch (error) {
    // Someone else's transaction says nothing about this order: refuse it
    // without recording the signature or moving the order (R2 Task 5).
    if (error instanceof RamShopError && error.code === "payment_not_from_order_wallet") throw error;
    if (error instanceof RamShopError && error.code === "payment_execution_failed") {
      return await markOrderTerminal(supabase, order, signature, "failed", error.code);
    }
    if (manualReviewOnMismatch && error instanceof RamShopError && isPaymentMismatch(error.code)) {
      return await markOrderTerminal(supabase, order, signature, "manual_review", error.code);
    }
    throw error;
  }
  const { error: verifiedError } = await supabase.rpc("ram_shop_mark_payment_verified", {
    p_order_id: order.order_id,
    p_signature: signature,
  });
  if (verifiedError) throw new RamShopError(normalizeDatabaseError(verifiedError.message), 409);
  return await advancePurchaseSettlement(
    supabase,
    await loadOwnedOrder(supabase, order.player_profile_id, order.order_id),
  );
}

async function markOrderTerminal(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
  signature: string | null,
  state: "expired_unpaid" | "failed" | "manual_review",
  failureCode: string,
): Promise<JsonMap> {
  const { error } = await supabase.rpc("ram_shop_mark_order_terminal", {
    p_order_id: order.order_id,
    p_state: state,
    p_failure_code: failureCode,
    p_observed_signature: signature,
  });
  if (error) throw new RamShopError(normalizeDatabaseError(error.message), 409);
  return await orderResponseWithBalanceVersion(
    supabase,
    await loadOwnedOrder(supabase, order.player_profile_id, order.order_id),
  );
}

export async function reconcileQuotedPurchase(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
  findSignature: (rpcUrl: string, reference: string) => Promise<string | null>,
): Promise<JsonMap> {
  const runtime = loadRamShopRuntimeConfig();
  if (runtime.cluster !== order.cluster) throw new RamShopError("order_cluster_mismatch", 503);

  // Older R0 rows may contain an unverified callback signature. Try it without
  // escalating, then fall back to the immutable reference address.
  if (order.payment_signature) {
    try {
      return await confirmPurchase(supabase, order, order.payment_signature, false);
    } catch (error) {
      if (
        !(error instanceof RamShopError) ||
        (!isRecoverablePaymentLookupError(error.code) && !isPaymentMismatch(error.code) &&
          error.code !== "payment_execution_failed")
      ) throw error;
    }
  }
  const recovered = await findSignature(runtime.rpcUrl, order.reference_address);
  if (recovered && recovered !== order.payment_signature) return await confirmPurchase(supabase, order, recovered);

  const expiredByTime = Date.parse(order.quote_expires_at) <= Date.now();
  if (!expiredByTime) return await orderResponseWithBalanceVersion(supabase, order);
  const currentBlockHeight = await rpcCall<number>(runtime.rpcUrl, "getBlockHeight", [{ commitment: "finalized" }]);
  if (currentBlockHeight !== null && currentBlockHeight > Number(order.last_valid_block_height)) {
    return await markOrderTerminal(supabase, order, null, "expired_unpaid", "quote_expired_unpaid");
  }
  return await orderResponseWithBalanceVersion(supabase, order);
}

export async function advancePurchaseSettlement(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
): Promise<JsonMap> {
  if (order.status === "settled") return await orderResponseWithBalanceVersion(supabase, order);
  if (order.status === "quoted" || order.status === "submitted") {
    return await orderResponseWithBalanceVersion(supabase, order);
  }
  if (order.status !== "payment_verified" && order.status !== "credit_submitted") {
    return await orderResponseWithBalanceVersion(supabase, order);
  }
  if (await loadCreditMode(supabase) === "postgres_only") return await settleInPostgres(supabase, order);

  const config = loadOwnershipConfig();
  if (config.cluster !== order.cluster) throw new RamShopError("settlement_cluster_mismatch", 503);
  const owner = publicKey(order.wallet_address, "invalid_order_wallet");
  const orderHash = await sha256Bytes(`ram-purchase:${order.order_id}`);
  const [receipt] = PublicKey.findProgramAddressSync([
    new TextEncoder().encode("ram_purchase_receipt"),
    owner.toBytes(),
    orderHash,
  ], config.programId);
  const [profile] = PublicKey.findProgramAddressSync(
    [new TextEncoder().encode("player_profile"), owner.toBytes()],
    config.programId,
  );
  const [gameConfig] = PublicKey.findProgramAddressSync([new TextEncoder().encode("game_config")], config.programId);
  const connection = new Connection(config.rpcUrl, "finalized");

  let signature = order.credit_signature;
  let balanceAfter = await readPurchaseReceiptBalance(
    connection,
    receipt,
    owner,
    orderHash,
    Number(order.ram_amount),
    config.programId,
    config.resultAuthority.publicKey,
  );
  if (balanceAfter === null) {
    const discriminator = (await sha256Bytes("global:credit_purchased_ram")).slice(0, 8);
    const data = Buffer.alloc(8 + 32 + 4);
    Buffer.from(discriminator).copy(data, 0);
    Buffer.from(orderHash).copy(data, 8);
    data.writeUInt32LE(Number(order.ram_amount), 40);
    const ix = new TransactionInstruction({
      programId: config.programId,
      keys: [
        { pubkey: receipt, isSigner: false, isWritable: true },
        { pubkey: profile, isSigner: false, isWritable: true },
        { pubkey: owner, isSigner: false, isWritable: false },
        { pubkey: gameConfig, isSigner: false, isWritable: false },
        { pubkey: config.resultAuthority.publicKey, isSigner: true, isWritable: true },
        { pubkey: SystemProgram.programId, isSigner: false, isWritable: false },
      ],
      // web3.js and Deno currently resolve distinct structural Buffer typings;
      // the runtime value is the Node-compatible Buffer required by web3.js.
      // deno-lint-ignore no-explicit-any
      data: data as any,
    });
    const latest = await connection.getLatestBlockhash("finalized");
    const tx = new Transaction({ feePayer: config.resultAuthority.publicKey, recentBlockhash: latest.blockhash }).add(
      ix,
    );
    tx.sign(config.resultAuthority);
    signature = await connection.sendRawTransaction(tx.serialize(), { skipPreflight: false, maxRetries: 3 });
    const confirmed = await connection.confirmTransaction({ signature, ...latest }, "finalized");
    if (confirmed.value.err) throw new RamShopError("credit_transaction_failed", 503);
    balanceAfter = await readPurchaseReceiptBalance(
      connection,
      receipt,
      owner,
      orderHash,
      Number(order.ram_amount),
      config.programId,
      config.resultAuthority.publicKey,
    );
    if (balanceAfter === null) throw new RamShopError("credit_receipt_missing", 503);
  }

  if (!signature) {
    const signatures = await connection.getSignaturesForAddress(receipt, { limit: 20 }, "finalized");
    signature = signatures.find((candidate) => !candidate.err && candidate.confirmationStatus === "finalized")
      ?.signature ?? null;
    if (!signature) throw new RamShopError("credit_signature_missing", 503);
  }
  if (order.status === "payment_verified") {
    const { error: creditError } = await supabase.rpc("ram_shop_mark_credit_submitted", {
      p_order_id: order.order_id,
      p_credit_signature: signature,
    });
    if (creditError) throw new RamShopError(normalizeDatabaseError(creditError.message), 409);
  }

  const { data: settled, error: settleError } = await supabase.rpc("ram_shop_settle_purchase", {
    p_order_id: order.order_id,
    p_credit_signature: signature,
    p_receipt_address: receipt.toBase58(),
  });
  if (settleError) throw new RamShopError(normalizeDatabaseError(settleError.message), 409);
  return {
    success: true,
    ...(settled as JsonMap),
    credit_signature: signature,
    credit_receipt_address: receipt.toBase58(),
  };
}

export type RamShopCreditMode = "chain_mirror" | "postgres_only";

export async function loadCreditMode(supabase: SupabaseClient): Promise<RamShopCreditMode> {
  const { data, error } = await supabase.from("ram_shop_settings").select("credit_mode")
    .eq("singleton", true).single();
  if (error) throw error;
  const mode = str(data?.credit_mode);
  if (mode !== "chain_mirror" && mode !== "postgres_only") throw new RamShopError("credit_mode_invalid", 503);
  return mode;
}

// Proposal D1: the verified payment tx (signature + per-order reference, both on
// the order) is the proof. No result-authority signing, no receipt PDA, no RPC.
async function settleInPostgres(supabase: SupabaseClient, order: PurchaseOrderRow): Promise<JsonMap> {
  const { data: settled, error } = await supabase.rpc("ram_shop_settle_purchase", {
    p_order_id: order.order_id,
    p_credit_signature: null,
    p_receipt_address: null,
  });
  if (error) throw new RamShopError(normalizeDatabaseError(error.message), 409);
  return {
    success: true,
    ...(settled as JsonMap),
    credit_signature: null,
    credit_receipt_address: null,
    payment_signature: order.payment_signature,
    reference: order.reference_address,
  };
}

export async function verifyFinalizedPayment(
  rpcUrl: string,
  order: PurchaseOrderRow,
  signature: string,
): Promise<void> {
  const payload = await rpcCall<JsonMap>(rpcUrl, "getTransaction", [signature, {
    commitment: "finalized",
    encoding: "base64",
    maxSupportedTransactionVersion: 0,
  }]);
  if (!payload) throw new RamShopError("payment_not_finalized", 202);
  const encoded = Array.isArray(payload.transaction) ? str(payload.transaction[0]) : "";
  if (!encoded) throw new RamShopError("payment_transaction_missing", 503);
  const transaction = Transaction.from(Buffer.from(encoded, "base64"));
  requireOrderWalletFeePayer(transaction, order);
  const meta = payload.meta as JsonMap | null;
  if (!meta || meta.err !== null) throw new RamShopError("payment_execution_failed", 409);
  await verifyPaymentTransaction(transaction, order, signature);
}

// Checked before anything is recorded against the order: a signature paid by
// another wallet can't fail, review or consume this order.
export function requireOrderWalletFeePayer(transaction: Transaction, order: PurchaseOrderRow): void {
  if (!transaction.feePayer?.equals(publicKey(order.wallet_address, "invalid_order_wallet"))) {
    throw new RamShopError("payment_not_from_order_wallet", 409);
  }
}

// Policy (R2 Task 6): exact match. The signed message must equal the bytes the
// server quoted, so any wallet edit (priority fee, Lighthouse-style guard,
// reordered ixs) fails with payment_message_mismatch and confirm parks the paid
// order in manual_review for the operator runbook
// (Docs/Specs/Economy/RAM_SHOP_MANUAL_REVIEW_RUNBOOK.md).
//
// Proposed alternative, not built (needs a Seeker device test first): verify
// semantically instead of byte-for-byte.
//   - exactly one signature, fee payer = order wallet (as today);
//   - the transfer (SystemProgram.transfer, or transferChecked for SPL) to the
//     order destination for the exact atomic amount appears exactly once, and
//     carries the order's reference key exactly once;
//   - every other instruction is ComputeBudget (limit <= MAX_COMPUTE_UNIT_LIMIT,
//     price <= MAX_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS) or on an explicit
//     allow-list of guard programs that move no funds;
//   - no other instruction names the order destination or the reference key.
// The finalized meta (pre/post balances) would then confirm the destination
// actually received the amount.
export async function verifyPaymentTransaction(
  transaction: Transaction,
  order: PurchaseOrderRow,
  signature: string,
): Promise<void> {
  if (transaction.signatures.length !== 1 || !transaction.signatures[0].signature) {
    throw new RamShopError("payment_signature_shape_mismatch", 409);
  }
  if (bs58.encode(Uint8Array.from(transaction.signatures[0].signature)) !== signature) {
    throw new RamShopError("payment_signature_mismatch", 409);
  }
  const messageBytes = transaction.serializeMessage();
  const messageBytesArray = Uint8Array.from(messageBytes);
  if (Buffer.from(messageBytesArray).toString("base64") !== order.expected_message_base64) {
    throw new RamShopError("payment_message_mismatch", 409);
  }
  if (await sha256HexBytes(messageBytesArray) !== order.expected_message_hash) {
    throw new RamShopError("payment_message_hash_mismatch", 409);
  }
  if (transaction.instructions.length !== 3) throw new RamShopError("payment_instruction_count_mismatch", 409);
  if (!transaction.feePayer?.equals(publicKey(order.wallet_address, "invalid_order_wallet"))) {
    throw new RamShopError("payment_fee_payer_mismatch", 409);
  }
  verifyComputeUnitLimitInstruction(transaction.instructions[0]);
  verifyComputeUnitPriceInstruction(transaction.instructions[1]);
  verifyPaymentInstruction(transaction.instructions[2], order);
}

function verifyComputeUnitLimitInstruction(ix: TransactionInstruction): void {
  if (
    !ix.programId.equals(ComputeBudgetProgram.programId) || ix.keys.length !== 0 || ix.data.length !== 5 ||
    ix.data[0] !== 2
  ) {
    throw new RamShopError("payment_compute_limit_mismatch", 409);
  }
  const units = ix.data.readUInt32LE(1);
  if (units < MIN_COMPUTE_UNIT_LIMIT || units > MAX_COMPUTE_UNIT_LIMIT || units !== RAM_SHOP_COMPUTE_UNIT_LIMIT) {
    throw new RamShopError("payment_compute_limit_mismatch", 409);
  }
}

function verifyComputeUnitPriceInstruction(ix: TransactionInstruction): void {
  if (
    !ix.programId.equals(ComputeBudgetProgram.programId) || ix.keys.length !== 0 || ix.data.length !== 9 ||
    ix.data[0] !== 3
  ) {
    throw new RamShopError("payment_compute_price_mismatch", 409);
  }
  const price = ix.data.readBigUInt64LE(1);
  if (price > MAX_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS || price !== BigInt(RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS)) {
    throw new RamShopError("payment_compute_price_mismatch", 409);
  }
}

function verifyPaymentInstruction(ix: TransactionInstruction, order: PurchaseOrderRow): void {
  const owner = publicKey(order.wallet_address, "invalid_order_wallet");
  const destination = publicKey(order.destination, "invalid_order_destination");
  const reference = publicKey(order.reference_address, "invalid_order_reference");
  const amount = parseAtomicAmount(order.payment_atomic_amount);
  const last = ix.keys[ix.keys.length - 1];
  if (!last || !last.pubkey.equals(reference) || last.isSigner || last.isWritable) {
    throw new RamShopError("payment_reference_mismatch", 409);
  }
  if (order.tender === "SOL") {
    if (!ix.programId.equals(SystemProgram.programId) || ix.keys.length !== 3) {
      throw new RamShopError("payment_program_mismatch", 409);
    }
    if (
      !ix.keys[0].pubkey.equals(owner) || !ix.keys[0].isSigner || !ix.keys[0].isWritable ||
      !ix.keys[1].pubkey.equals(destination) || ix.keys[1].isSigner || !ix.keys[1].isWritable
    ) {
      throw new RamShopError("payment_accounts_mismatch", 409);
    }
    if (ix.data.length !== 12 || ix.data.readUInt32LE(0) !== 2 || ix.data.readBigUInt64LE(4) !== amount) {
      throw new RamShopError("payment_amount_mismatch", 409);
    }
    return;
  }

  const program = publicKey(order.token_program, "invalid_order_token_program");
  const mint = publicKey(order.mint_address, "invalid_order_mint");
  const codePin = PINNED_TENDER_MINTS[order.cluster]?.[order.tender];
  if (!program.equals(TOKEN_PROGRAM_ID) || (codePin !== undefined && mint.toBase58() !== codePin)) {
    throw new RamShopError("payment_token_program_mismatch", 409);
  }
  const source = getAssociatedTokenAddressSync(mint, owner, false, program);
  if (!ix.programId.equals(program) || ix.keys.length !== 5) {
    throw new RamShopError("payment_token_program_mismatch", 409);
  }
  if (
    !ix.keys[0].pubkey.equals(source) || !ix.keys[1].pubkey.equals(mint) ||
    !ix.keys[2].pubkey.equals(destination) || !ix.keys[3].pubkey.equals(owner) || !ix.keys[3].isSigner
  ) {
    throw new RamShopError("payment_token_accounts_mismatch", 409);
  }
  if (
    ix.data.length !== 10 || ix.data[0] !== 12 || Buffer.from(Uint8Array.from(ix.data)).readBigUInt64LE(1) !== amount ||
    ix.data[9] !== order.decimals
  ) {
    throw new RamShopError("payment_token_amount_mismatch", 409);
  }
}

async function verifyMintAndDestination(
  connection: Connection,
  mint: PublicKey,
  tokenProgram: PublicKey,
  destination: PublicKey,
  treasuryOwner: PublicKey,
  decimals: number,
  source: PublicKey,
  payer: PublicKey,
  amount: bigint,
): Promise<void> {
  const parsedAccounts = await connection.getMultipleParsedAccounts([mint, destination, source], {
    commitment: "finalized",
  });
  const mintValue = parsedAccounts.value[0];
  const destinationValue = parsedAccounts.value[1];
  const sourceValue = parsedAccounts.value[2];
  if (!mintValue || !mintValue.owner.equals(tokenProgram)) throw new RamShopError("tender_mint_owner_mismatch", 503);
  const mintParsed = (mintValue.data as { parsed?: JsonMap }).parsed;
  checkTenderMintAccount(mintValue.owner, mintParsed?.info as JsonMap | undefined, decimals);
  if (sourceValue && !sourceValue.owner.equals(tokenProgram)) throw new RamShopError("tender_token_account_missing", 409);
  checkPayerTokenAccount(
    sourceValue ? ((sourceValue.data as { parsed?: JsonMap }).parsed?.info as JsonMap | undefined) : undefined,
    payer,
    mint,
    amount,
  );
  if (!destinationValue || !destinationValue.owner.equals(tokenProgram)) {
    throw new RamShopError("treasury_token_program_mismatch", 503);
  }
  const destinationParsed = (destinationValue.data as { parsed?: JsonMap }).parsed;
  checkTreasuryTokenAccount(destinationParsed?.info as JsonMap | undefined, treasuryOwner, mint);
}

async function readPurchaseReceiptBalance(
  connection: Connection,
  receipt: PublicKey,
  owner: PublicKey,
  orderHash: Uint8Array,
  amount: number,
  programId: PublicKey,
  resultAuthority: PublicKey,
): Promise<number | null> {
  const info = await connection.getAccountInfo(receipt, "finalized");
  if (!info) return null;
  const data = new Uint8Array(info.data);
  if (
    !info.owner.equals(programId) || data.length !== 117 ||
    !equalBytes(data.slice(0, 8), RAM_PURCHASE_RECEIPT_DISCRIMINATOR) ||
    !equalBytes(data.slice(8, 40), owner.toBytes()) ||
    !equalBytes(data.slice(40, 72), orderHash) ||
    readU32(data, 72) !== amount ||
    !equalBytes(data.slice(84, 116), resultAuthority.toBytes())
  ) throw new RamShopError("credit_receipt_invalid", 503);
  const balanceBefore = readU32(data, 76);
  const balanceAfter = readU32(data, 80);
  if (balanceAfter - balanceBefore !== amount) throw new RamShopError("credit_receipt_invalid", 503);
  return balanceAfter;
}

export function orderResponse(order: PurchaseOrderRow): JsonMap {
  return {
    success: true,
    order_id: order.order_id,
    state: order.status,
    cluster: order.cluster,
    wallet: order.wallet_address,
    sku: order.sku,
    bundle_name: order.bundle_name,
    ram_amount: String(order.ram_amount),
    tender: order.tender,
    payment_atomic_amount: String(order.payment_atomic_amount),
    mint: order.mint_address,
    decimals: order.decimals,
    destination: order.destination,
    program_id: CHAINBOUND_PROGRAM_ID,
    payment_program: SystemProgram.programId.toBase58(),
    reference: order.reference_address,
    unsigned_transaction_base64: order.unsigned_transaction_base64,
    message_hash: order.expected_message_hash,
    recent_blockhash: order.recent_blockhash,
    last_valid_block_height: String(order.last_valid_block_height),
    estimated_fee_lamports: String(order.estimated_fee_lamports),
    quote_expires_at: order.quote_expires_at,
    payment_signature: order.payment_signature,
    credit_signature: order.credit_signature,
    credit_receipt_address: order.credit_receipt_address,
    balance_after: order.balance_after === null ? null : String(order.balance_after),
    balance_version: Number(order.balance_version ?? 0),
    failure_code: order.failure_code,
  };
}

export async function orderResponseWithBalanceVersion(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
): Promise<JsonMap> {
  if (order.balance_after !== null && Number(order.balance_version ?? 0) > 0) {
    return orderResponse(order);
  }
  return orderResponse({
    ...order,
    balance_version: await readRamBalanceVersion(supabase, order.player_profile_id),
  } as PurchaseOrderRow);
}

async function readRamBalanceVersion(supabase: SupabaseClient, profileId: string): Promise<number> {
  const { data, error } = await supabase.from("player_profiles")
    .select("ram_balance_version")
    .eq("player_profile_id", profileId)
    .single();
  if (error) throw error;
  const version = Number(data.ram_balance_version ?? 0);
  if (!Number.isSafeInteger(version) || version <= 0) throw new RamShopError("ram_balance_version_missing", 503);
  return version;
}

// Exported for tests: which verifier codes park a paid order in manual_review.
export function requiresManualReview(code: string): boolean {
  return isPaymentMismatch(code);
}

function isPaymentMismatch(code: string): boolean {
  return code.startsWith("payment_") && !isRecoverablePaymentLookupError(code) &&
    code !== "payment_execution_failed" && code !== "payment_transaction_missing";
}

function isRecoverablePaymentLookupError(code: string): boolean {
  return code === "payment_not_finalized" || code === "payment_transaction_missing" ||
    code === "solana_rpc_unavailable" || code === "solana_rpc_error";
}

function normalizeTender(value: string): RamTender {
  const normalized = value.trim().toUpperCase();
  if (normalized !== "SOL" && normalized !== "USDC" && normalized !== "SKR") throw new RamShopError("invalid_tender");
  return normalized;
}

function parseAtomicAmount(value: unknown): bigint {
  const raw = String(value ?? "");
  if (!/^[1-9][0-9]*$/.test(raw)) throw new RamShopError("invalid_atomic_amount", 503);
  const amount = BigInt(raw);
  if (amount > 18446744073709551615n) throw new RamShopError("atomic_amount_overflow", 503);
  return amount;
}

function publicKey(value: unknown, code: string): PublicKey {
  try {
    return new PublicKey(str(value));
  } catch {
    throw new RamShopError(code, 503);
  }
}

function isSignature(value: string): boolean {
  try {
    return bs58.decode(value).length === 64;
  } catch {
    return false;
  }
}

async function sha256Bytes(value: string): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(value)));
}

function readU32(value: Uint8Array, offset: number): number {
  return new DataView(value.buffer, value.byteOffset, value.byteLength).getUint32(offset, true);
}

function equalBytes(left: Uint8Array, right: Uint8Array): boolean {
  return left.length === right.length && left.every((value, index) => value === right[index]);
}

async function sha256HexBytes(value: Uint8Array): Promise<string> {
  const input = new Uint8Array(value.byteLength);
  input.set(value);
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", input.buffer));
  return [...digest].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function rpcCall<T>(rpcUrl: string, method: string, params: unknown[]): Promise<T | null> {
  const response = await fetch(rpcUrl, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ jsonrpc: "2.0", id: 1, method, params }),
  });
  if (!response.ok) throw new RamShopError("solana_rpc_unavailable", 503);
  const payload = await response.json() as { result?: T | null; error?: { message?: string } };
  if (payload.error) throw new RamShopError("solana_rpc_error", 503);
  return payload.result ?? null;
}

function normalizeDatabaseError(message: string): string {
  const value = (message ?? "").trim();
  const known = [
    "order_not_found",
    "invalid_order_state",
    "signature_already_consumed",
    "reference_already_consumed",
    "credit_receipt_already_consumed",
    "ram_reconciliation_required",
    "insufficient_ram",
    "operation_not_found",
  ];
  return known.find((code) => value.includes(code)) ?? "ram_shop_state_conflict";
}
