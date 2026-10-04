import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";

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
