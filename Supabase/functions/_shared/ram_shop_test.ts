import { assertEquals, assertRejects } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  ComputeBudgetProgram,
  Keypair,
  PublicKey,
  SystemProgram,
  Transaction,
  TransactionInstruction,
} from "npm:@solana/web3.js@1.98.4";
import bs58 from "npm:bs58@5.0.0";
import { Buffer } from "npm:buffer@6.0.3";
import {
  createTransferCheckedInstruction,
  getAssociatedTokenAddressSync,
  TOKEN_2022_PROGRAM_ID,
  TOKEN_PROGRAM_ID,
} from "npm:@solana/spl-token@0.4.14";
import {
  advancePurchaseSettlement,
  checkPayerTokenAccount,
  checkTenderMintAccount,
  checkTreasuryTokenAccount,
  expectedTreasuryDestination,
  loadCreditMode,
  requirePinnedTenderMint,
  requireTreasuryDestination,
  resolveTreasuryOwner,
  secretsMatch,
  PurchaseOrderRow,
  RAM_SHOP_COMPUTE_UNIT_LIMIT,
  RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS,
  RamShopError,
  requiresManualReview,
  verifyFinalizedPayment,
  verifyPaymentTransaction,
} from "./ram_shop.ts";

Deno.test("purchase credit settlement passes the canonical SQL signature", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const settle = source.slice(source.indexOf('supabase.rpc("ram_shop_settle_purchase"'));
  assertEquals(settle.includes("p_credit_signature: signature"), true);
  assertEquals(settle.includes("p_receipt_address: receipt.toBase58()"), true);
  assertEquals(settle.slice(0, settle.indexOf("});") + 3).includes("p_balance_after"), false);
});

Deno.test("shop prepare ensures the linked ledger account before scope lookup", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const prepare = source.slice(source.indexOf("export async function preparePurchase"));
  const ensure = prepare.indexOf("ensureRamLedgerAccount(");
  const ledgerLookup = prepare.indexOf('.from("ram_ledger_accounts")');
  assertEquals(ensure >= 0, true);
  assertEquals(ledgerLookup >= 0, true);
  assertEquals(ensure < ledgerLookup, true);
});

Deno.test("purchase receipt validation pins ownership, identity, authority, and exact balance delta", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const receipt = source.slice(source.indexOf("async function readPurchaseReceiptBalance"));
  assertEquals(receipt.includes("data.length !== 117"), true);
  assertEquals(receipt.includes("RAM_PURCHASE_RECEIPT_DISCRIMINATOR"), true);
  assertEquals(receipt.includes("!info.owner.equals(programId)"), true);
  assertEquals(receipt.includes("data.slice(8, 40), owner.toBytes()"), true);
  assertEquals(receipt.includes("data.slice(40, 72), orderHash"), true);
  assertEquals(receipt.includes("data.slice(84, 116), resultAuthority.toBytes()"), true);
  assertEquals(receipt.includes("balanceAfter - balanceBefore !== amount"), true);
});

function paymentInstruction(
  payer: PublicKey,
  destination: PublicKey,
  reference: PublicKey,
  amount: bigint,
): TransactionInstruction {
  const instruction = SystemProgram.transfer({ fromPubkey: payer, toPubkey: destination, lamports: amount });
  instruction.keys.push({ pubkey: reference, isSigner: false, isWritable: false });
  return instruction;
}

function paymentTransaction(
  signer: Keypair,
  destination: PublicKey,
  reference: PublicKey,
  amount = 5_000_000n,
  extraInstruction: TransactionInstruction | null = null,
  computeUnitLimit = RAM_SHOP_COMPUTE_UNIT_LIMIT,
): Transaction {
  const transaction = new Transaction({
    feePayer: signer.publicKey,
    recentBlockhash: Keypair.generate().publicKey.toBase58(),
  }).add(
    ComputeBudgetProgram.setComputeUnitLimit({ units: computeUnitLimit }),
    ComputeBudgetProgram.setComputeUnitPrice({ microLamports: RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS }),
    paymentInstruction(signer.publicKey, destination, reference, amount),
  );
  if (extraInstruction) transaction.add(extraInstruction);
  transaction.sign(signer);
  return transaction;
}

async function orderFor(
  transaction: Transaction,
  expectedPayer: PublicKey,
  expectedDestination: PublicKey,
  expectedReference: PublicKey,
): Promise<PurchaseOrderRow> {
  const message = transaction.serializeMessage();
  const messageBytes = Uint8Array.from(message);
  const digest = new Uint8Array(await crypto.subtle.digest("SHA-256", messageBytes));
  return {
    id: 1,
    order_id: crypto.randomUUID(),
    player_profile_id: crypto.randomUUID(),
    wallet_address: expectedPayer.toBase58(),
    cluster: "devnet",
    sku: "ram_5",
    bundle_name: "100 RAM",
    ram_amount: 100,
    tender: "SOL",
    payment_atomic_amount: "5000000",
    mint_address: null,
    decimals: 9,
    token_program: null,
    destination: expectedDestination.toBase58(),
    reference_address: expectedReference.toBase58(),
    expected_message_base64: Buffer.from(messageBytes).toString("base64"),
    expected_message_hash: [...digest].map((value) => value.toString(16).padStart(2, "0")).join(""),
    unsigned_transaction_base64: "",
    recent_blockhash: transaction.recentBlockhash!,
    last_valid_block_height: 1,
    estimated_fee_lamports: "5000",
    status: "quoted",
    payment_signature: null,
    credit_signature: null,
    credit_receipt_address: null,
    balance_after: null,
    failure_code: null,
    quote_expires_at: new Date(Date.now() + 60_000).toISOString(),
  };
}

async function verifyFixture(transaction: Transaction, order: PurchaseOrderRow): Promise<void> {
  const signature = bs58.encode(Uint8Array.from(transaction.signatures[0].signature!));
  await verifyPaymentTransaction(transaction, order, signature);
}

Deno.test("payment verifier accepts exactly CU limit, CU price, and quoted transfer", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference);
  await verifyFixture(transaction, await orderFor(transaction, payer.publicKey, destination, reference));
});

Deno.test("payment verifier rejects an extra instruction", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(
    payer,
    destination,
    reference,
    5_000_000n,
    ComputeBudgetProgram.setComputeUnitLimit({ units: RAM_SHOP_COMPUTE_UNIT_LIMIT }),
  );
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  await assertRejects(() => verifyFixture(transaction, order), RamShopError, "payment_instruction_count_mismatch");
});

Deno.test("payment verifier rejects wrong amount", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference, 5_000_001n);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  await assertRejects(() => verifyFixture(transaction, order), RamShopError, "payment_amount_mismatch");
});

Deno.test("payment verifier rejects wrong recipient", async () => {
  const payer = Keypair.generate();
  const quotedDestination = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, Keypair.generate().publicKey, Keypair.generate().publicKey);
  const actualReference = transaction.instructions[2].keys[2].pubkey;
  const order = await orderFor(transaction, payer.publicKey, quotedDestination, actualReference);
  await assertRejects(() => verifyFixture(transaction, order), RamShopError, "payment_accounts_mismatch");
});

Deno.test("payment verifier rejects another fee payer", async () => {
  const actualPayer = Keypair.generate();
  const quotedPayer = Keypair.generate().publicKey;
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(actualPayer, destination, reference);
  const order = await orderFor(transaction, quotedPayer, destination, reference);
  await assertRejects(() => verifyFixture(transaction, order), RamShopError, "payment_fee_payer_mismatch");
});

Deno.test("payment verifier rejects an out-of-policy compute limit", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference, 5_000_000n, null, 500_000);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  await assertRejects(() => verifyFixture(transaction, order), RamShopError, "payment_compute_limit_mismatch");
});

Deno.test("an already-settled purchase returns its stored balance and version pair", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  order.status = "settled";
  order.payment_signature = bs58.encode(Uint8Array.from(transaction.signatures[0].signature!));
  order.balance_after = 120;
  order.balance_version = 41;
  const readOnlySupabase = {
    from() {
      throw new Error("settled replay must not pair the stored balance with a current profile version");
    },
    rpc() {
      throw new Error("settled replay must not invoke a mutation RPC");
    },
  };
  const first = await advancePurchaseSettlement(readOnlySupabase as never, order);
  const second = await advancePurchaseSettlement(readOnlySupabase as never, order);
  assertEquals(first, second);
  assertEquals(first.state, "settled");
  assertEquals(first.balance_after, "120");
  assertEquals(first.balance_version, 41);
});

// A stand-in for the service-role client: ram_shop_settings returns the given
// credit_mode, and ram_shop_settle_purchase behaves like the SQL function's
// FOR UPDATE + idempotent replay (first caller settles, the rest replay).
function settlementSupabase(creditMode: string) {
  const rpcCalls: Array<{ name: string; args: Record<string, unknown> }> = [];
  let settled = false;
  let balance = 0;
  const client = {
    from(table: string) {
      if (table !== "ram_shop_settings") throw new Error(`unexpected table ${table}`);
      const query = {
        select: () => query,
        eq: () => query,
        single: () => Promise.resolve({ data: { credit_mode: creditMode }, error: null }),
      };
      return query;
    },
    async rpc(name: string, args: Record<string, unknown>) {
      rpcCalls.push({ name, args });
      if (name !== "ram_shop_settle_purchase") throw new Error(`unexpected rpc ${name}`);
      await Promise.resolve();
      const replayed = settled;
      if (!settled) {
        settled = true;
        balance += 100;
      }
      return {
        data: { replayed, status: "settled", balance_after: balance, balance_version: 7 },
        error: null,
      };
    },
  };
  return { client, rpcCalls, balance: () => balance };
}

async function verifiedOrder(): Promise<PurchaseOrderRow> {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  order.status = "payment_verified";
  order.payment_signature = bs58.encode(Uint8Array.from(transaction.signatures[0].signature!));
  return order;
}

Deno.test("postgres_only settles from the verified payment with no chain write", async () => {
  const order = await verifiedOrder();
  const db = settlementSupabase("postgres_only");
  // No CHAINBOUND_* env or result-authority key is readable in this test: if the
  // chain path ran, loadOwnershipConfig would throw before any RPC.
  const result = await advancePurchaseSettlement(db.client as never, order);
  assertEquals(db.rpcCalls.map((call) => call.name), ["ram_shop_settle_purchase"]);
  assertEquals(db.rpcCalls[0].args, {
    p_order_id: order.order_id,
    p_credit_signature: null,
    p_receipt_address: null,
  });
  assertEquals(result.credit_signature, null);
  assertEquals(result.credit_receipt_address, null);
  assertEquals(result.payment_signature, order.payment_signature);
  assertEquals(result.reference, order.reference_address);
});

Deno.test("postgres_only: concurrent confirm, status and reconcile settle exactly once", async () => {
  const order = await verifiedOrder();
  const db = settlementSupabase("postgres_only");
  const results = await Promise.all([
    advancePurchaseSettlement(db.client as never, { ...order }),
    advancePurchaseSettlement(db.client as never, { ...order }),
    advancePurchaseSettlement(db.client as never, { ...order, status: "credit_submitted" }),
  ]);
  assertEquals(db.balance(), 100);
  assertEquals(results.filter((result) => result.replayed === false).length, 1);
  assertEquals(results.every((result) => result.status === "settled"), true);
  assertEquals(db.rpcCalls.every((call) => call.name === "ram_shop_settle_purchase"), true);
});

Deno.test("chain_mirror never settles in Postgres before the on-chain credit", async () => {
  const order = await verifiedOrder();
  const db = settlementSupabase("chain_mirror");
  await assertRejects(() => advancePurchaseSettlement(db.client as never, order));
  assertEquals(db.rpcCalls.length, 0);
});

Deno.test("an unknown credit_mode fails closed", async () => {
  const db = settlementSupabase("memo");
  await assertRejects(() => loadCreditMode(db.client as never), RamShopError, "credit_mode_invalid");
  const order = await verifiedOrder();
  await assertRejects(() => advancePurchaseSettlement(db.client as never, order), RamShopError, "credit_mode_invalid");
  assertEquals(db.rpcCalls.length, 0);
});

// getTransaction stand-in: serves one finalized transaction to verifyFinalizedPayment.
async function withFinalizedTransaction(
  transaction: Transaction,
  err: unknown,
  run: () => Promise<void>,
): Promise<void> {
  const realFetch = globalThis.fetch;
  globalThis.fetch = (() =>
    Promise.resolve(
      new Response(JSON.stringify({
        jsonrpc: "2.0",
        id: 1,
        result: { meta: { err }, transaction: [transaction.serialize().toString("base64"), "base64"] },
      })),
    )) as typeof fetch;
  try {
    await run();
  } finally {
    globalThis.fetch = realFetch;
  }
}

Deno.test("signature poisoning: another wallet's payment is refused before it touches the order", async () => {
  const victim = Keypair.generate();
  const attacker = Keypair.generate().publicKey;
  const destination = Keypair.generate().publicKey;
  const victimTx = paymentTransaction(victim, destination, Keypair.generate().publicKey);
  const attackerOrder = await orderFor(
    paymentTransaction(Keypair.generate(), destination, Keypair.generate().publicKey),
    attacker,
    destination,
    Keypair.generate().publicKey,
  );
  const victimSignature = bs58.encode(Uint8Array.from(victimTx.signatures[0].signature!));
  await withFinalizedTransaction(victimTx, null, async () => {
    await assertRejects(
      () => verifyFinalizedPayment("https://rpc.invalid", attackerOrder, victimSignature),
      RamShopError,
      "payment_not_from_order_wallet",
    );
  });
  // Even a failed victim tx can't be used to fail the attacker's own order.
  await withFinalizedTransaction(victimTx, { InstructionError: [2, "Custom"] }, async () => {
    await assertRejects(
      () => verifyFinalizedPayment("https://rpc.invalid", attackerOrder, victimSignature),
      RamShopError,
      "payment_not_from_order_wallet",
    );
  });
});

Deno.test("signature poisoning: the order owner's own payment still verifies", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  const signature = bs58.encode(Uint8Array.from(transaction.signatures[0].signature!));
  await withFinalizedTransaction(transaction, null, () => verifyFinalizedPayment("https://rpc.invalid", order, signature));
});

Deno.test("signature poisoning: confirm rethrows a foreign payment without recording it", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const confirm = source.slice(source.indexOf("export async function confirmPurchase"));
  const guard = confirm.indexOf('error.code === "payment_not_from_order_wallet") throw error;');
  const terminal = confirm.indexOf("markOrderTerminal(");
  assertEquals(guard >= 0, true);
  assertEquals(guard < terminal, true);
  const status = await Deno.readTextFile(new URL("../ram_shop_status/index.ts", import.meta.url));
  assertEquals(status.includes('.eq("signature", signature).eq("status", "verified")'), true);
});

// R2 Task 6: these document today's policy, not a target. A wallet that edits the
// server-built transaction (priority fee, guard instruction) produces a different
// message, the verifier rejects it, and confirm parks the paid order in
// manual_review. Docs/Specs/Economy/RAM_SHOP_MANUAL_REVIEW_RUNBOOK.md covers the
// operator path. If these start passing verification, the policy changed.
async function walletModified(edit: (original: Transaction, payer: Keypair) => Transaction) {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const quoted = paymentTransaction(payer, destination, reference);
  const order = await orderFor(quoted, payer.publicKey, destination, reference);
  const paid = edit(quoted, payer);
  return { order, paid };
}

Deno.test("wallet-modified tx: a raised compute-unit price is rejected and goes to manual review", async () => {
  const { order, paid } = await walletModified((original, payer) => {
    const edited = new Transaction({ feePayer: payer.publicKey, recentBlockhash: original.recentBlockhash }).add(
      original.instructions[0],
      ComputeBudgetProgram.setComputeUnitPrice({ microLamports: RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS * 5 }),
      original.instructions[2],
    );
    edited.sign(payer);
    return edited;
  });
  await assertRejects(() => verifyFixture(paid, order), RamShopError, "payment_message_mismatch");
  assertEquals(requiresManualReview("payment_message_mismatch"), true);
});

Deno.test("wallet-modified tx: an added guard instruction is rejected and goes to manual review", async () => {
  const { order, paid } = await walletModified((original, payer) => {
    const guard = new TransactionInstruction({
      programId: Keypair.generate().publicKey,
      keys: [{ pubkey: payer.publicKey, isSigner: true, isWritable: false }],
    });
    const edited = new Transaction({ feePayer: payer.publicKey, recentBlockhash: original.recentBlockhash }).add(
      ...original.instructions,
      guard,
    );
    edited.sign(payer);
    return edited;
  });
  await assertRejects(() => verifyFixture(paid, order), RamShopError, "payment_message_mismatch");
  assertEquals(requiresManualReview("payment_message_mismatch"), true);
});

Deno.test("wallet-modified tx: an execution failure is terminal 'failed', not manual review", () => {
  assertEquals(requiresManualReview("payment_execution_failed"), false);
  assertEquals(requiresManualReview("payment_not_finalized"), false);
  assertEquals(requiresManualReview("payment_not_from_order_wallet"), true);
});

// R2 Task 7: config-driven treasury payee.
const USDC_DEVNET = new PublicKey("4zMMC9srt5Ri5X14GAgXhaHii3GnPAEERYPJgZJDncDU");
const TREASURY_VAULT = new PublicKey("Hkn8eVNs11wFKvGKGk3GZqo7WA29bYn3mroWNDj8gdaL");

Deno.test("treasury: a missing pin or an env/DB mismatch closes checkout with a typed 503", () => {
  const owner = Keypair.generate().publicKey.toBase58();
  for (
    const [database, pinned, code] of [
      ["", owner, "treasury_owner_not_configured"],
      [owner, "", "treasury_owner_not_pinned"],
      [owner, Keypair.generate().publicKey.toBase58(), "treasury_owner_mismatch"],
    ]
  ) {
    try {
      resolveTreasuryOwner(database, pinned);
      throw new Error(`expected ${code}`);
    } catch (error) {
      assertEquals(error instanceof RamShopError && error.code, code);
      assertEquals((error as RamShopError).httpStatus, 503);
    }
  }
  assertEquals(resolveTreasuryOwner(owner, owner).toBase58(), owner);
});

Deno.test("treasury: SOL to anything but the treasury owner is refused", () => {
  const owner = Keypair.generate().publicKey;
  assertEquals(requireTreasuryDestination("SOL", owner.toBase58(), owner, null, null).equals(owner), true);
  try {
    requireTreasuryDestination("SOL", Keypair.generate().publicKey.toBase58(), owner, null, null);
    throw new Error("expected treasury_destination_mismatch");
  } catch (error) {
    assertEquals(error instanceof RamShopError && error.code, "treasury_destination_mismatch");
  }
});

Deno.test("treasury: an ATA for the wrong owner is refused", () => {
  const owner = Keypair.generate().publicKey;
  const otherAta = getAssociatedTokenAddressSync(USDC_DEVNET, Keypair.generate().publicKey, false, TOKEN_PROGRAM_ID);
  try {
    requireTreasuryDestination("USDC", otherAta.toBase58(), owner, USDC_DEVNET, TOKEN_PROGRAM_ID);
    throw new Error("expected treasury_destination_mismatch");
  } catch (error) {
    assertEquals(error instanceof RamShopError && error.code, "treasury_destination_mismatch");
  }
});

Deno.test("treasury: an off-curve (Squads vault PDA) owner's ATA is accepted", () => {
  assertEquals(PublicKey.isOnCurve(TREASURY_VAULT.toBytes()), false);
  const ata = getAssociatedTokenAddressSync(USDC_DEVNET, TREASURY_VAULT, true, TOKEN_PROGRAM_ID);
  assertEquals(
    requireTreasuryDestination("USDC", ata.toBase58(), TREASURY_VAULT, USDC_DEVNET, TOKEN_PROGRAM_ID).equals(ata),
    true,
  );
  assertEquals(expectedTreasuryDestination("SOL", TREASURY_VAULT, null, null).equals(TREASURY_VAULT), true);
  // The Token-2022 ATA is a different address, so a program swap can't reuse a row.
  const ata2022 = getAssociatedTokenAddressSync(USDC_DEVNET, TREASURY_VAULT, true, TOKEN_2022_PROGRAM_ID);
  assertEquals(ata2022.equals(ata), false);
});

Deno.test("treasury: the live token account must be the owner's, on the mint, and usable", () => {
  const owner = TREASURY_VAULT;
  const good = { mint: USDC_DEVNET.toBase58(), owner: owner.toBase58(), state: "initialized" };
  checkTreasuryTokenAccount(good, owner, USDC_DEVNET);
  for (
    const [info, code] of [
      [{ ...good, owner: Keypair.generate().publicKey.toBase58() }, "treasury_token_owner_mismatch"],
      [{ ...good, mint: Keypair.generate().publicKey.toBase58() }, "treasury_token_mint_mismatch"],
      [{ ...good, state: "frozen" }, "treasury_token_account_unusable"],
      [{ ...good, state: "uninitialized" }, "treasury_token_account_unusable"],
      [undefined, "treasury_token_mint_mismatch"],
    ] as Array<[Record<string, string> | undefined, string]>
  ) {
    try {
      checkTreasuryTokenAccount(info, owner, USDC_DEVNET);
      throw new Error(`expected ${code}`);
    } catch (error) {
      assertEquals(error instanceof RamShopError && error.code, code);
    }
  }
});

Deno.test("treasury: an order quoted before a treasury change still verifies against its stored destination", async () => {
  const payer = Keypair.generate();
  const oldTreasury = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, oldTreasury, reference);
  const order = await orderFor(transaction, payer.publicKey, oldTreasury, reference);
  // The configured owner has since moved to the vault; settlement never re-reads it.
  assertEquals(resolveTreasuryOwner(TREASURY_VAULT.toBase58(), TREASURY_VAULT.toBase58()).equals(oldTreasury), false);
  await verifyFixture(transaction, order);
});

Deno.test("treasury: prepare and catalog derive the payee from the treasury, never the row alone", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const prepare = source.slice(source.indexOf("export async function preparePurchase"));
  assertEquals(prepare.includes("loadTreasuryOwner(supabase, runtime.cluster)"), true);
  assertEquals(prepare.includes('publicKey(str(price.destination), "invalid_treasury_destination")'), false);
  assertEquals(
    (prepare.match(/requireTreasuryDestination\(tender, str\(price\.destination\), treasuryOwner/g) ?? []).length,
    2,
  );
  const catalog = source.slice(source.indexOf("export async function catalogResponse"));
  assertEquals(catalog.includes(".filter((price) => paysTreasury(price, treasuryOwner!))"), true);
});

// R2 Task 8: SPL safety before token tenders are enabled.
const USDC_PIN = {
  mint: USDC_DEVNET.toBase58(),
  token_program: TOKEN_PROGRAM_ID.toBase58(),
  decimals: 6,
};

function expectCode(run: () => void, code: string): void {
  try {
    run();
  } catch (error) {
    assertEquals(error instanceof RamShopError && error.code, code);
    return;
  }
  throw new Error(`expected ${code}`);
}

Deno.test("spl: the pinned devnet USDC mint on classic Token is accepted", () => {
  requirePinnedTenderMint("devnet", "USDC", USDC_DEVNET, TOKEN_PROGRAM_ID, 6, USDC_PIN);
});

Deno.test("spl: a wrong or lookalike mint is refused even if a DB row names it", () => {
  const lookalike = Keypair.generate().publicKey;
  expectCode(
    () =>
      requirePinnedTenderMint("devnet", "USDC", lookalike, TOKEN_PROGRAM_ID, 6, {
        ...USDC_PIN,
        mint: lookalike.toBase58(),
      }),
    "tender_mint_not_pinned",
  );
  expectCode(
    () =>
      requirePinnedTenderMint("mainnet-beta", "SKR", lookalike, TOKEN_PROGRAM_ID, 6, {
        ...USDC_PIN,
        mint: lookalike.toBase58(),
      }),
    "tender_mint_not_pinned",
  );
});

Deno.test("spl: devnet SKR needs a DB pin, and the price must match it", () => {
  const testMint = Keypair.generate().publicKey;
  expectCode(() => requirePinnedTenderMint("devnet", "SKR", testMint, TOKEN_PROGRAM_ID, 6, null), "tender_mint_not_pinned");
  requirePinnedTenderMint("devnet", "SKR", testMint, TOKEN_PROGRAM_ID, 6, { ...USDC_PIN, mint: testMint.toBase58() });
});

Deno.test("spl: wrong decimals are refused in the price row and on the live mint", () => {
  expectCode(
    () => requirePinnedTenderMint("devnet", "USDC", USDC_DEVNET, TOKEN_PROGRAM_ID, 9, USDC_PIN),
    "tender_mint_not_pinned",
  );
  expectCode(() => checkTenderMintAccount(TOKEN_PROGRAM_ID, { decimals: 9 }, 6), "tender_decimals_mismatch");
  checkTenderMintAccount(TOKEN_PROGRAM_ID, { decimals: 6, isInitialized: true }, 6);
});

Deno.test("spl: Token-2022 is refused for USDC/SKR, so fee and hook extensions can't apply", () => {
  expectCode(
    () =>
      requirePinnedTenderMint("devnet", "USDC", USDC_DEVNET, TOKEN_2022_PROGRAM_ID, 6, {
        ...USDC_PIN,
        token_program: TOKEN_2022_PROGRAM_ID.toBase58(),
      }),
    "unsupported_token_program",
  );
  expectCode(() => checkTenderMintAccount(TOKEN_2022_PROGRAM_ID, { decimals: 6 }, 6), "tender_mint_owner_mismatch");
});

Deno.test("spl: a missing, foreign, frozen or short source ATA is a clean 409", () => {
  const payer = Keypair.generate().publicKey;
  const good = {
    mint: USDC_DEVNET.toBase58(),
    owner: payer.toBase58(),
    state: "initialized",
    tokenAmount: { amount: "1000000" },
  };
  checkPayerTokenAccount(good, payer, USDC_DEVNET, 1_000_000n);
  for (
    const [info, code] of [
      [undefined, "tender_token_account_missing"],
      [{ ...good, owner: Keypair.generate().publicKey.toBase58() }, "tender_token_account_missing"],
      [{ ...good, state: "frozen" }, "tender_token_account_frozen"],
      [{ ...good, tokenAmount: { amount: "999999" } }, "tender_balance_insufficient"],
    ] as Array<[Record<string, unknown> | undefined, string]>
  ) {
    try {
      checkPayerTokenAccount(info, payer, USDC_DEVNET, 1_000_000n);
      throw new Error(`expected ${code}`);
    } catch (error) {
      assertEquals(error instanceof RamShopError && error.code, code);
      assertEquals((error as RamShopError).httpStatus, 409);
    }
  }
});

async function usdcOrder(program = TOKEN_PROGRAM_ID, mint = USDC_DEVNET) {
  const payer = Keypair.generate();
  const reference = Keypair.generate().publicKey;
  const destination = getAssociatedTokenAddressSync(mint, TREASURY_VAULT, true, program);
  const transfer = createTransferCheckedInstruction(
    getAssociatedTokenAddressSync(mint, payer.publicKey, false, program),
    mint,
    destination,
    payer.publicKey,
    500_000n,
    6,
    [],
    program,
  );
  transfer.keys.push({ pubkey: reference, isSigner: false, isWritable: false });
  const transaction = new Transaction({
    feePayer: payer.publicKey,
    recentBlockhash: Keypair.generate().publicKey.toBase58(),
  }).add(
    ComputeBudgetProgram.setComputeUnitLimit({ units: RAM_SHOP_COMPUTE_UNIT_LIMIT }),
    ComputeBudgetProgram.setComputeUnitPrice({ microLamports: RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS }),
    transfer,
  );
  transaction.sign(payer);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  Object.assign(order, {
    tender: "USDC",
    payment_atomic_amount: "500000",
    mint_address: mint.toBase58(),
    decimals: 6,
    token_program: program.toBase58(),
  });
  return { transaction, order };
}

Deno.test("spl: a correct USDC transferChecked to the treasury ATA verifies", async () => {
  const { transaction, order } = await usdcOrder();
  await verifyFixture(transaction, order);
});

Deno.test("spl: the verifier refuses a Token-2022 or unpinned-mint order", async () => {
  const token2022 = await usdcOrder(TOKEN_2022_PROGRAM_ID);
  await assertRejects(
    () => verifyFixture(token2022.transaction, token2022.order),
    RamShopError,
    "payment_token_program_mismatch",
  );
  const lookalike = await usdcOrder(TOKEN_PROGRAM_ID, Keypair.generate().publicKey);
  await assertRejects(
    () => verifyFixture(lookalike.transaction, lookalike.order),
    RamShopError,
    "payment_token_program_mismatch",
  );
});

// R2 Task 9: the reconciler secret is compared in constant time.
Deno.test("reconciler secret: match, mismatch, prefix and empty", async () => {
  assertEquals(await secretsMatch("s3cret-value", "s3cret-value"), true);
  assertEquals(await secretsMatch("s3cret-valuf", "s3cret-value"), false);
  assertEquals(await secretsMatch("s3cret", "s3cret-value"), false);
  assertEquals(await secretsMatch("", "s3cret-value"), false);
  assertEquals(await secretsMatch("", ""), false);
  assertEquals(await secretsMatch("anything", ""), false);
});

Deno.test("reconciler secret: the endpoint uses the constant-time compare", async () => {
  const source = await Deno.readTextFile(new URL("../ram_shop_reconcile/index.ts", import.meta.url));
  assertEquals(source.includes('secretsMatch(req.headers.get("x-reconciler-secret") ?? "", expected)'), true);
  assertEquals(source.includes('req.headers.get("x-reconciler-secret") !== expected'), false);
});
