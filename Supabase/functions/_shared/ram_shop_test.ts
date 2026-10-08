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
  advancePurchaseSettlement,
  PurchaseOrderRow,
  RAM_SHOP_COMPUTE_UNIT_LIMIT,
  RAM_SHOP_COMPUTE_UNIT_PRICE_MICRO_LAMPORTS,
  RamShopError,
  verifyPaymentTransaction,
} from "./ram_shop.ts";

Deno.test("purchase credit settlement passes the canonical SQL signature", async () => {
  const source = await Deno.readTextFile(new URL("./ram_shop.ts", import.meta.url));
  const settle = source.slice(source.indexOf('supabase.rpc("ram_shop_settle_purchase"'));
  assertEquals(settle.includes("p_credit_signature: signature"), true);
  assertEquals(settle.includes("p_receipt_address: receipt.toBase58()"), true);
  assertEquals(settle.slice(0, settle.indexOf("});") + 3).includes("p_balance_after"), false);
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

Deno.test("an already-settled purchase is idempotent and performs no further RPC", async () => {
  const payer = Keypair.generate();
  const destination = Keypair.generate().publicKey;
  const reference = Keypair.generate().publicKey;
  const transaction = paymentTransaction(payer, destination, reference);
  const order = await orderFor(transaction, payer.publicKey, destination, reference);
  order.status = "settled";
  order.payment_signature = bs58.encode(Uint8Array.from(transaction.signatures[0].signature!));
  order.balance_after = 120;
  const rejectingSupabase = new Proxy({}, {
    get() {
      throw new Error("settled replay must not access Supabase");
    },
  });
  const first = await advancePurchaseSettlement(rejectingSupabase as never, order);
  const second = await advancePurchaseSettlement(rejectingSupabase as never, order);
  assertEquals(first, second);
  assertEquals(first.state, "settled");
  assertEquals(first.balance_after, "120");
});
