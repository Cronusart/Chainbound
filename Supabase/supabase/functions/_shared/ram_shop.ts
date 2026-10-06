import { Connection, PublicKey, SystemProgram, Transaction, TransactionInstruction } from "npm:@solana/web3.js@1.98.4";
import {
  createTransferCheckedInstruction,
  getAssociatedTokenAddressSync,
  TOKEN_2022_PROGRAM_ID,
  TOKEN_PROGRAM_ID,
} from "npm:@solana/spl-token@0.4.14";
import bs58 from "npm:bs58@5.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import type { SupabaseClient } from "https://esm.sh/@supabase/supabase-js@2";
import { getActiveWallet, JsonMap, str } from "./account_helpers.ts";
import {
  CHAINBOUND_PROGRAM_ID,
  DEVNET_GENESIS_HASH,
  ensureOwnershipRpcCluster,
  fetchChainRamBalance,
  loadOwnershipConfig,
  MAINNET_GENESIS_HASH,
} from "./ownership_spine.ts";

const RAM_PURCHASE_RECEIPT_DISCRIMINATOR = Uint8Array.from([37, 105, 212, 100, 110, 164, 29, 144]);

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
  quote_expires_at: string;
}

export class RamShopError extends Error {
  constructor(public readonly code: string, public readonly httpStatus = 400) {
    super(code);
  }
}

export function loadRamShopRuntimeConfig(): RamShopRuntimeConfig {
  const cluster = str(Deno.env.get("RAM_SHOP_CLUSTER"));
  if (cluster !== "devnet" && cluster !== "mainnet-beta") {
    throw new RamShopError("ram_shop_cluster_missing", 503);
  }
  const rpcUrl = str(Deno.env.get("CHAINBOUND_SOLANA_RPC_ENDPOINT"));
  if (!rpcUrl.startsWith("https://")) throw new RamShopError("ram_shop_rpc_missing", 503);
  const configuredGenesis = str(Deno.env.get("RAM_SHOP_RPC_GENESIS_HASH"));
  const expectedGenesisHash = cluster === "devnet" ? DEVNET_GENESIS_HASH : MAINNET_GENESIS_HASH;
  if (configuredGenesis !== expectedGenesisHash) throw new RamShopError("ram_shop_genesis_not_pinned", 503);
  const programVersion = str(Deno.env.get("RAM_SHOP_PROGRAM_VERSION"));
  if (!programVersion) throw new RamShopError("ram_shop_program_version_missing", 503);
  const programText = str(Deno.env.get("CHAINBOUND_PROGRAM_ID"));
  if (programText !== CHAINBOUND_PROGRAM_ID) throw new RamShopError("ram_shop_program_not_pinned", 503);
  return { cluster, rpcUrl, expectedGenesisHash, programId: new PublicKey(programText), programVersion };
}

async function tryAuditLinkedLedger(
  supabase: SupabaseClient,
  profileId: string,
): Promise<{ status: string; reason: string }> {
  try {
    const wallet = await getActiveWallet(supabase, profileId);
    const walletAddress = str(wallet?.wallet_public_key);
    if (!walletAddress) return { status: "pending_audit", reason: "linked_wallet_required" };
    const config = loadOwnershipConfig();
    await ensureOwnershipRpcCluster(config);
    const chainBalance = await fetchChainRamBalance({ config, walletAddress });
    const { data, error } = await supabase.rpc("ram_ledger_register_audit", {
      p_player_profile_id: profileId,
      p_wallet_address: walletAddress,
      p_cluster: config.cluster,
      p_chain_balance: chainBalance,
    });
    if (error) throw error;
    const payload = (typeof data === "string" ? JSON.parse(data) : data) as JsonMap;
    const status = str(payload?.status) || "pending_audit";
    return {
      status,
      reason: status === "active" ? "" : "ram_reconciliation_required",
    };
  } catch (error) {
    const message = error instanceof Error ? error.message : "";
    return {
      status: "pending_audit",
      reason: message === "player_profile_not_initialized" ? message : "ram_ledger_audit_pending",
    };
  }
}

function closedCatalog(authoritativeBalance: string, ledgerStatus: string, reason: string): JsonMap {
  return {
    success: true,
    checkout_available: false,
    maintenance_reason: reason,
    authoritative_balance: authoritativeBalance,
    ledger_status: ledgerStatus,
    bundles: [],
    tenders: [],
  };
}

export async function catalogResponse(supabase: SupabaseClient, profileId: string): Promise<JsonMap> {
  const [{ data: profile, error: profileError }, { data: ledger, error: ledgerError }] = await Promise.all([
    supabase.from("player_profiles").select("ram_balance").eq("player_profile_id", profileId).single(),
    supabase.from("ram_ledger_accounts").select("status").eq("player_profile_id", profileId).maybeSingle(),
  ]);
  if (profileError) throw profileError;
  if (ledgerError) throw ledgerError;
  const authoritativeBalance = String(profile.ram_balance ?? 0);
  let ledgerStatus = str(ledger?.status) || "pending_audit";
  if (ledgerStatus === "pending_audit") {
    const audited = await tryAuditLinkedLedger(supabase, profileId);
    ledgerStatus = audited.status;
    if (ledgerStatus !== "active") {
      return closedCatalog(authoritativeBalance, ledgerStatus, audited.reason);
    }
  }

  const { data: settings, error: settingsError } = await supabase
    .from("ram_shop_settings")
    .select("checkout_enabled, quote_ttl_seconds, maintenance_reason")
    .eq("singleton", true)
    .maybeSingle();
  if (settingsError) throw settingsError;

  let runtime: RamShopRuntimeConfig | null = null;
  let reason = str(settings?.maintenance_reason) || "RAM checkout is unavailable.";
  try {
    runtime = loadRamShopRuntimeConfig();
    await ensureOwnershipRpcCluster(runtime);
  } catch (error) {
    reason = error instanceof RamShopError ? error.code : "ram_shop_configuration_invalid";
  }

  const enabledByConfig = settings?.checkout_enabled === true && Number(settings?.quote_ttl_seconds ?? 0) > 0;
  if (ledgerStatus !== "active") {
    return {
      success: true,
      checkout_available: false,
      maintenance_reason: ledgerStatus === "frozen_reconciliation"
        ? "ram_reconciliation_required"
        : "ram_ledger_audit_pending",
      authoritative_balance: authoritativeBalance,
      ledger_status: ledgerStatus,
      bundles: [],
      tenders: [],
    };
  }
  if (!runtime || !enabledByConfig) {
    return {
      success: true,
      checkout_available: false,
      maintenance_reason: reason,
      authoritative_balance: authoritativeBalance,
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
    prices: ((row.ram_shop_tender_prices ?? []) as JsonMap[]).map((price) => ({
      tender: str(price.tender),
      atomic_amount: String(price.atomic_amount),
      mint: str(price.mint_address) || null,
      decimals: Number(price.decimals),
    })),
  }));
  const tenders = [...new Set(bundles.flatMap((bundle) => bundle.prices.map((price) => price.tender)))];
  const available = bundles.length > 0 && tenders.length > 0;
  return {
    success: true,
    checkout_available: available,
    maintenance_reason: available ? null : "catalog_not_configured",
    authoritative_balance: authoritativeBalance,
    ledger_status: ledgerStatus,
    catalog_version: catalog.version,
    cluster: runtime.cluster,
    bundles,
    tenders,
  };
}

export async function preparePurchase(
  supabase: SupabaseClient,
  profileId: string,
  sku: string,
  tenderText: string,
): Promise<JsonMap> {
  const tender = normalizeTender(tenderText);
  const runtime = loadRamShopRuntimeConfig();
  await ensureOwnershipRpcCluster(runtime);

  const { data: settings, error: settingsError } = await supabase
    .from("ram_shop_settings")
    .select("checkout_enabled, quote_ttl_seconds, maintenance_reason")
    .eq("singleton", true)
    .single();
  if (settingsError) throw settingsError;
  if (settings.checkout_enabled !== true) {
    throw new RamShopError(str(settings.maintenance_reason) || "checkout_disabled", 503);
  }
  const ttlSeconds = Number(settings.quote_ttl_seconds ?? 0);
  if (!Number.isSafeInteger(ttlSeconds) || ttlSeconds <= 0) throw new RamShopError("quote_ttl_missing", 503);

  const wallet = await getActiveWallet(supabase, profileId);
  const walletAddress = str(wallet?.wallet_public_key);
  if (!walletAddress) throw new RamShopError("linked_wallet_required", 409);
  const owner = publicKey(walletAddress, "invalid_linked_wallet");

  const { data: ledger, error: ledgerError } = await supabase
    .from("ram_ledger_accounts")
    .select("status, cluster, wallet_address")
    .eq("player_profile_id", profileId)
    .maybeSingle();
  if (ledgerError) throw ledgerError;
  if (!ledger || ledger.status !== "active") throw new RamShopError("ram_reconciliation_required", 409);
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
  const destination = publicKey(str(price.destination), "invalid_treasury_destination");
  let instruction: TransactionInstruction;
  let mintAddress: string | null = null;
  let tokenProgram: string | null = null;

  if (tender === "SOL") {
    if (price.mint_address || price.token_program || Number(price.decimals) !== 9) {
      throw new RamShopError("invalid_sol_price", 503);
    }
    instruction = SystemProgram.transfer({ fromPubkey: owner, toPubkey: destination, lamports: amount });
  } else {
    const mint = publicKey(str(price.mint_address), "invalid_tender_mint");
    const program = publicKey(str(price.token_program), "invalid_token_program");
    if (!program.equals(TOKEN_PROGRAM_ID) && !program.equals(TOKEN_2022_PROGRAM_ID)) {
      throw new RamShopError("unsupported_token_program", 503);
    }
    if (
      tender === "SKR" && runtime.cluster === "mainnet-beta" &&
      mint.toBase58() !== "SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3"
    ) {
      throw new RamShopError("skr_mint_mismatch", 503);
    }
    await verifyMintAndDestination(connection, mint, program, destination, Number(price.decimals));
    const source = getAssociatedTokenAddressSync(mint, owner, false, program);
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
  const transaction = new Transaction({ feePayer: owner, recentBlockhash: latest.blockhash }).add(instruction);
  const messageBytes = transaction.serializeMessage();
  const messageBase64 = Buffer.from(messageBytes).toString("base64");
  const messageHash = await sha256HexBytes(messageBytes);
  const unsignedBase64 = Buffer.from(transaction.serialize({ requireAllSignatures: false, verifySignatures: false }))
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
  return orderResponse(inserted as PurchaseOrderRow);
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
): Promise<JsonMap> {
  if (!isSignature(signature)) throw new RamShopError("invalid_signature");
  if (["settled", "payment_verified", "credit_submitted"].includes(order.status)) {
    if (order.payment_signature && order.payment_signature !== signature) {
      throw new RamShopError("order_signature_mismatch", 409);
    }
    return await advancePurchaseSettlement(supabase, order);
  }
  if (order.status !== "quoted" && order.status !== "submitted") throw new RamShopError("order_not_confirmable", 409);
  if (order.payment_signature && order.payment_signature !== signature) {
    throw new RamShopError("order_signature_mismatch", 409);
  }

  const { error: submittedError } = await supabase.rpc("ram_shop_record_submission", {
    p_order_id: order.order_id,
    p_signature: signature,
  });
  if (submittedError) throw new RamShopError(normalizeDatabaseError(submittedError.message), 409);

  const runtime = loadRamShopRuntimeConfig();
  if (runtime.cluster !== order.cluster) throw new RamShopError("order_cluster_mismatch", 503);
  await verifyFinalizedPayment(runtime.rpcUrl, order, signature);
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

export async function advancePurchaseSettlement(
  supabase: SupabaseClient,
  order: PurchaseOrderRow,
): Promise<JsonMap> {
  if (order.status === "settled") return orderResponse(order);
  if (order.status === "quoted" || order.status === "submitted") return orderResponse(order);
  if (order.status !== "payment_verified" && order.status !== "credit_submitted") return orderResponse(order);

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
  const meta = payload.meta as JsonMap | null;
  if (!meta || meta.err !== null) throw new RamShopError("payment_execution_failed", 409);
  const encoded = Array.isArray(payload.transaction) ? str(payload.transaction[0]) : "";
  if (!encoded) throw new RamShopError("payment_transaction_missing", 503);
  const transaction = Transaction.from(Buffer.from(encoded, "base64"));
  if (transaction.signatures.length !== 1 || !transaction.signatures[0].signature) {
    throw new RamShopError("payment_signature_shape_mismatch", 409);
  }
  if (bs58.encode(transaction.signatures[0].signature) !== signature) {
    throw new RamShopError("payment_signature_mismatch", 409);
  }
  const messageBytes = transaction.serializeMessage();
  if (Buffer.from(messageBytes).toString("base64") !== order.expected_message_base64) {
    throw new RamShopError("payment_message_mismatch", 409);
  }
  if (await sha256HexBytes(messageBytes) !== order.expected_message_hash) {
    throw new RamShopError("payment_message_hash_mismatch", 409);
  }
  if (transaction.instructions.length !== 1) throw new RamShopError("payment_instruction_count_mismatch", 409);
  if (!transaction.feePayer?.equals(publicKey(order.wallet_address, "invalid_order_wallet"))) {
    throw new RamShopError("payment_fee_payer_mismatch", 409);
  }
  verifyPaymentInstruction(transaction.instructions[0], order);
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
    ix.data.length !== 10 || ix.data[0] !== 12 || Buffer.from(ix.data).readBigUInt64LE(1) !== amount ||
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
  decimals: number,
): Promise<void> {
  const parsedAccounts = await connection.getMultipleParsedAccounts([mint, destination], { commitment: "finalized" });
  const mintValue = parsedAccounts.value[0];
  const destinationValue = parsedAccounts.value[1];
  if (!mintValue || !mintValue.owner.equals(tokenProgram)) throw new RamShopError("tender_mint_owner_mismatch", 503);
  const mintParsed = (mintValue.data as { parsed?: JsonMap }).parsed;
  const actualDecimals = Number((mintParsed?.info as JsonMap | undefined)?.decimals ?? -1);
  if (actualDecimals !== decimals) throw new RamShopError("tender_decimals_mismatch", 503);
  if (!destinationValue || !destinationValue.owner.equals(tokenProgram)) {
    throw new RamShopError("treasury_token_program_mismatch", 503);
  }
  const destinationParsed = (destinationValue.data as { parsed?: JsonMap }).parsed;
  if (str((destinationParsed?.info as JsonMap | undefined)?.mint) !== mint.toBase58()) {
    throw new RamShopError("treasury_token_mint_mismatch", 503);
  }
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
  };
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
