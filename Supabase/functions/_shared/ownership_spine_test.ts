import { assertEquals, assertNotEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  BIND_RAM_COST_PIN,
  buildOperatorDevnetRamTopupTransaction,
  deriveKnownResult,
  deriveStampResult,
  EXPECTED_MODIFIER_POOL_HASH,
  EXPECTED_MODIFIER_POOL_ROW_COUNT,
  FIRST_STAMP_RAM_COST,
  hashMoveId,
  isSolanaSignature,
  KNOWN_ROLL_RAM_COST,
  MODIFIER_POOL,
  parseDevnetSmokeWallets,
  RAM_DAILY_CAP,
  SECOND_STAMP_RAM_COST_PIN,
} from "./ownership_spine.ts";

Deno.test("transaction signatures use 64-byte base58 validation, not the public-key regex", () => {
  const confirmedDevnetSignature =
    "4FbUZ5hGuqW1QZkUCRhcQa5weSsEs1rqSjUb4iD8RDQWQz76LpMAVZBtTkbsiDn196vCtzmjBjnpD7ggfxWYrBUH";
  assertEquals(isSolanaSignature(confirmedDevnetSignature), true);
  assertEquals(isSolanaSignature("H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq"), false);
  assertEquals(isSolanaSignature(`${confirmedDevnetSignature}0`), false);
});

Deno.test("both ownership confirmations apply transaction-signature validation", async () => {
  for (
    const relativePath of [
      "../ownership_confirm_devnet_smoke_grant/index.ts",
      "../ownership_confirm_stamp/index.ts",
    ]
  ) {
    const source = await Deno.readTextFile(new URL(relativePath, import.meta.url));
    assertEquals(source.includes("isSolanaSignature(signature)"), true);
    assertEquals(source.includes("SOLANA_BASE58_RE.test(signature)"), false);
  }
});

Deno.test("move IDs match Unity FNV-1a truncation", () => {
  assertEquals(hashMoveId("baseline_jab"), 47232);
});

Deno.test("authority-derived Known results are deterministic", async () => {
  const secret = Uint8Array.from({ length: 64 }, (_, index) => index);
  const first = await deriveKnownResult(secret, "profile-a", "roll-a");
  const replay = await deriveKnownResult(secret, "profile-a", "roll-a");
  assertEquals(replay, first);
  assertEquals(first.onchainMoveId, hashMoveId(first.moveId));
});

Deno.test("Phase 1 Known roll cost remains pinned in the authority endpoint", async () => {
  assertEquals(KNOWN_ROLL_RAM_COST, 5);
  const source = await Deno.readTextFile(new URL("../ownership_roll_known/index.ts", import.meta.url));
  assertEquals(source.includes("KNOWN_ROLL_RAM_COST"), true);
  assertEquals(source.includes('supabase.rpc("ownership_roll_known_atomic"'), true);
});

Deno.test("Phase 1 RAM policy pins remain distinct", () => {
  assertEquals(KNOWN_ROLL_RAM_COST, 5);
  assertEquals(FIRST_STAMP_RAM_COST, 75);
  assertEquals(SECOND_STAMP_RAM_COST_PIN, 150);
  assertEquals(BIND_RAM_COST_PIN, 250);
  assertEquals(RAM_DAILY_CAP, 100);
});

Deno.test("first Stamp confirmation uses one atomic 75-RAM settlement RPC", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_confirm_stamp/index.ts", import.meta.url));
  assertEquals(source.includes('supabase.rpc("ownership_confirm_stamp_atomic"'), true);
  assertEquals(source.includes("FIRST_STAMP_RAM_COST"), true);
  assertEquals(source.includes('.from("ownership_known_moves").update('), false);
});

Deno.test("Stamped Core creation uses collection authority without a conflicting update authority", async () => {
  const source = await Deno.readTextFile(
    new URL("../../../backend/chainbound_game/programs/chainbound_game/src/lib.rs", import.meta.url),
  );
  const start = source.indexOf("pub fn stamp_known_move");
  const end = source.indexOf("pub fn bind_chainling", start);
  assertNotEquals(start, -1);
  assertNotEquals(end, -1);
  const stampInstruction = source.slice(start, end);
  assertEquals(stampInstruction.includes(".collection(Some(&moves_collection_info))"), true);
  assertEquals(stampInstruction.includes(".update_authority(None)"), true);
  assertEquals(stampInstruction.includes(".update_authority(Some"), false);
});

Deno.test("Devnet smoke grant is explicit-wallet, beta, and fresh-zero gated", async () => {
  const shared = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  const prepare = await Deno.readTextFile(
    new URL("../ownership_prepare_devnet_smoke_grant/index.ts", import.meta.url),
  );
  const confirm = await Deno.readTextFile(
    new URL("../ownership_confirm_devnet_smoke_grant/index.ts", import.meta.url),
  );
  assertEquals(shared.includes('DEVNET_SMOKE_WALLETS_ENV = "CHAINBOUND_DEVNET_SMOKE_WALLETS"'), true);
  assertEquals(shared.includes('LEGACY_DEVNET_SMOKE_WALLET_ENV = "CHAINBOUND_DEVNET_SMOKE_WALLET"'), true);
  assertEquals(shared.includes("smokeWallets.has(owner.toBase58())"), true);
  assertEquals(shared.includes("fresh_profile_ram_not_zero"), true);
  assertEquals(shared.includes("ownership_flags_must_remain_off"), true);
  assertEquals(prepare.includes("isApprovedBetaWallet"), true);
  assertEquals(prepare.includes("config.smokeWallets.has(walletAddress)"), true);
  assertEquals(confirm.includes("isApprovedBetaWallet"), true);
  assertEquals(confirm.includes("config.smokeWallets.has(walletAddress)"), true);
  assertEquals(prepare.includes("RAM_DAILY_CAP"), true);
});

Deno.test("Devnet smoke wallet configuration accepts two distinct validated wallets", () => {
  const desktop = "H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq";
  const seeker = "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj";
  const wallets = parseDevnetSmokeWallets(JSON.stringify([desktop, seeker]));
  assertEquals(wallets.size, 2);
  assertEquals(wallets.has(desktop), true);
  assertEquals(wallets.has(seeker), true);
});

Deno.test("Devnet smoke wallet configuration rejects malformed or duplicate entries", () => {
  const wallet = "H68eGMwT3UCigd9qf4u1VLyqQh6jq57HD4wLWaQrbBWq";
  for (const value of ["[]", JSON.stringify([wallet, wallet]), `${wallet},${wallet}`]) {
    let code = "";
    try {
      parseDevnetSmokeWallets(value);
    } catch (error) {
      code = error instanceof Error ? error.message : "unknown";
    }
    assertEquals(code, "invalid_devnet_smoke_wallets");
  }
});

Deno.test("Devnet smoke confirmation atomically mirrors verified RAM", async () => {
  const confirm = await Deno.readTextFile(
    new URL("../ownership_confirm_devnet_smoke_grant/index.ts", import.meta.url),
  );
  assertEquals(
    confirm.includes('supabase.rpc(\n      "ownership_confirm_devnet_smoke_grant_atomic"'),
    true,
  );
  assertEquals(confirm.includes('.from("ownership_ram_grant_operations")\n      .update('), false);
  assertEquals(confirm.includes("p_balance_after: verified.balanceAfter"), true);
});

Deno.test("operator RAM reconciliation remains a server-signed recovery path", async () => {
  const source = await Deno.readTextFile(
    new URL("../ownership_operator_devnet_ram_topup/index.ts", import.meta.url),
  );
  assertEquals(source.includes("OPERATOR_TARGET_PROFILE_ID"), true);
  assertEquals(source.includes("simulateOperatorDevnetRamTopup"), true);
  assertEquals(source.includes("ownership_reconcile_operator_devnet_ram_topup_atomic"), true);
  assertEquals(source.includes("sendRawTransaction"), true);
  assertEquals(source.indexOf("simulateOperatorDevnetRamTopup") < source.indexOf("sendRawTransaction"), true);
  assertEquals(source.includes("str(body.wallet_address)"), false);
  assertEquals(typeof buildOperatorDevnetRamTopupTransaction, "function");
});

Deno.test("inventory hydration includes the canonical RAM mirror", async () => {
  const inventory = await Deno.readTextFile(
    new URL("../ownership_get_inventory/index.ts", import.meta.url),
  );
  assertEquals(inventory.includes('.select("ram_balance")'), true);
  assertEquals(inventory.includes("{ success: true, ramBalance, known, stamped"), true);
});

Deno.test("two-modifier Stamp never repeats a modifier", async () => {
  const secret = Uint8Array.from({ length: 64 }, (_, index) => 255 - index);
  const result = await deriveStampResult(
    secret,
    "profile-a",
    "known-a",
    2,
    1,
    EXPECTED_MODIFIER_POOL_HASH,
  );
  assertEquals(result.modifierIds.length, 2);
  assertNotEquals(result.modifierIds[0], result.modifierIds[1]);
});

Deno.test("Stamp entropy is bound to the Known and excludes client idempotency", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  const start = source.indexOf("export async function deriveStampResult");
  const end = source.indexOf("export async function buildStampTransaction", start);
  const deriveSource = source.slice(start, end);
  assertEquals(deriveSource.includes("idempotencyKey"), false);
  assertEquals(deriveSource.includes("stamp:v2:"), true);

  const secret = Uint8Array.from({ length: 64 }, (_, index) => index);
  const first = await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH);
  const retry = await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH);
  assertEquals(retry, first);
});

Deno.test("Phase 1 Stamp preparation enforces one modifier and resumes a canonical intent", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_prepare_stamp/index.ts", import.meta.url));
  assertEquals(source.includes("modifierCount !== 1"), true);
  assertEquals(source.includes("canonicalIdempotencyKey"), true);
  assertEquals(source.includes("stamp_intent_in_progress"), true);
  assertEquals(source.includes(".upsert(operation"), false);
});

Deno.test("Stamp confirmation persists signature before RPC and settles from the consumed program receipt", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_confirm_stamp/index.ts", import.meta.url));
  const submitted = source.indexOf('status: "submitted"');
  const rpcCheck = source.indexOf("signatureIsConfirmed(config.rpcUrl, signature)");
  assertNotEquals(submitted, -1);
  assertNotEquals(rpcCheck, -1);
  assertEquals(submitted < rpcCheck, true);
  assertEquals(source.includes("fetchAndVerifyStampReceipt"), true);
  assertEquals(source.includes('status: "das_pending"'), false);
  assertEquals(source.includes("expires_at"), false);
});

Deno.test("Stamp reconciliation can recover a signature from the deterministic asset", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_reconcile_stamp/index.ts", import.meta.url));
  assertEquals(source.includes("findConfirmedSignatureForAddress"), true);
  assertEquals(source.includes("fetchAndVerifyStampReceipt"), true);
  assertEquals(source.includes('supabase.rpc("ownership_confirm_stamp_atomic"'), true);
});

Deno.test("Stamped ownership attributes are read only from the on-chain Core plugin", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  assertEquals(source.includes("const attrs = collectAttributes(asset.plugins);"), true);
  assertEquals(source.includes("verifiedStampedAssetMatchesOperation"), true);
});

Deno.test("Stamp pool is the canonical 104-row Pass-A catalog", () => {
  assertEquals(MODIFIER_POOL.length, EXPECTED_MODIFIER_POOL_ROW_COUNT);
  assertEquals(EXPECTED_MODIFIER_POOL_HASH, "6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4");
  assertEquals(new Set(MODIFIER_POOL).size, MODIFIER_POOL.length);
  for (const id of ["1.3-a", "1.3-b", "2.7-a", "4.8-a", "6.8-a", "6.8-g", "7.3-a"] as const) {
    assertEquals(MODIFIER_POOL.includes(id), true);
  }
});

Deno.test("Stamp verification uses the validated server-only Helius Devnet endpoint", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  assertEquals(source.includes("requireHeliusDevnetUrl(Deno.env.get(HELIUS_DAS_URL_ENV))"), true);
  assertEquals(source.includes('dasUrl: requiredEnv("CHAINBOUND_DAS_RPC_URL")'), false);
});
