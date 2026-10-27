import { assertEquals, assertNotEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  BIND_RAM_COST_PIN,
  deriveKnownResult,
  deriveProfileKnownReference,
  deriveRamOperationId,
  deriveStampResult,
  DEVNET_GENESIS_HASH,
  ensureOwnershipRpcCluster,
  EXPECTED_MODIFIER_POOL_HASH,
  EXPECTED_MODIFIER_POOL_ROW_COUNT,
  FIRST_STAMP_RAM_COST,
  formatLockedModifierEffect,
  hashMoveId,
  isSolanaSignature,
  KNOWN_ROLL_RAM_COST,
  MAINNET_GENESIS_HASH,
  MODIFIER_EFFECT_BOUNDS,
  MODIFIER_POOL,
  parseDevnetSmokeWallets,
  parseMainnetSmokeWallets,
  RAM_DAILY_CAP,
  reconcileRamDeltaOnChain,
  SECOND_STAMP_RAM_COST_PIN,
} from "./ownership_spine.ts";
import { allowedHostCategoriesForModifier, eligibleModifierPoolForMove } from "./modifier_host_compatibility.ts";

const repoPrefix = import.meta.url.includes("/Supabase/supabase/functions/_shared/") ? "../../../../" : "../../../";
const functionPrefix = import.meta.url.includes("/Supabase/supabase/functions/_shared/")
  ? "../../../functions/"
  : "../";

Deno.test("stamp modifier pool is filtered by the canonical host move category", async () => {
  const eligible = eligibleModifierPoolForMove("baseline_striker");
  assertEquals(eligible.includes("6.1-a"), true);
  assertEquals(eligible.includes("6.4-d"), false);
  assertEquals(eligible.includes("6.8-b"), false);
  assertEquals(eligible.includes("8.1-a"), false);
  for (const sectionId of eligible) {
    const allowed = allowedHostCategoriesForModifier(sectionId);
    assertEquals(allowed == null || allowed.includes("DamagingMelee"), true, sectionId);
  }

  const secret = new TextEncoder().encode("host-compatible-stamp-test");
  const result = await deriveStampResult(
    secret,
    "profile-a",
    "known-a",
    1,
    1,
    EXPECTED_MODIFIER_POOL_HASH,
    "baseline_striker",
  );
  assertEquals(eligible.includes(result.modifierIds[0]), true);
});

function functionSourceUrl(relativePath: string): URL {
  const leaf = relativePath.startsWith("../") ? relativePath.slice(3) : relativePath;
  return new URL(`${functionPrefix}${leaf}`, import.meta.url);
}

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
    const source = await Deno.readTextFile(functionSourceUrl(relativePath));
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

Deno.test("RAM operation IDs are stable UUIDs scoped by profile, kind, and idempotency", async () => {
  const first = await deriveRamOperationId("profile-a", "stamp", "intent-a");
  assertEquals(await deriveRamOperationId("profile-a", "stamp", "intent-a"), first);
  assertNotEquals(await deriveRamOperationId("profile-a", "known_roll", "intent-a"), first);
  assertEquals(/^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/.test(first), true);
});

Deno.test("profile-owned Known references are stable non-zero u64 values", async () => {
  const first = await deriveProfileKnownReference("39000000-0000-4000-8000-000000000001");
  assertEquals(await deriveProfileKnownReference("39000000-0000-4000-8000-000000000001"), first);
  assertNotEquals(await deriveProfileKnownReference("39000000-0000-4000-8000-000000000002"), first);
  assertEquals(first > 0n && first <= 0xffffffffffffffffn, true);
});

Deno.test("Known rolls are profile-native atomic spends with no wallet or Solana dependency", async () => {
  assertEquals(KNOWN_ROLL_RAM_COST, 5);
  const source = await Deno.readTextFile(functionSourceUrl("ownership_roll_known/index.ts"));
  assertEquals(source.includes("KNOWN_ROLL_RAM_COST"), true);
  assertEquals(source.includes('supabase.rpc("ownership_roll_known_profile_atomic"'), true);
  assertEquals(source.includes("settleKnownRollOnChain"), false);
  assertEquals(source.includes("requireBoundWallet"), false);
  assertEquals(source.includes("wallet_address"), false);
  assertEquals(source.includes("isOwnershipWalletAllowed"), false);
  assertEquals(source.includes("ensureOwnershipRpcCluster"), false);
});

Deno.test("Phase 1 RAM policy pins remain distinct", () => {
  assertEquals(KNOWN_ROLL_RAM_COST, 5);
  assertEquals(FIRST_STAMP_RAM_COST, 75);
  assertEquals(SECOND_STAMP_RAM_COST_PIN, 150);
  assertEquals(BIND_RAM_COST_PIN, 250);
  assertEquals(RAM_DAILY_CAP, 100);
});

Deno.test("first Stamp confirmation uses one atomic 75-RAM settlement RPC", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_confirm_stamp/index.ts"));
  assertEquals(source.includes('supabase.rpc("ownership_confirm_stamp_reserved"'), true);
  assertEquals(source.includes("FIRST_STAMP_RAM_COST"), true);
  assertEquals(source.includes('.from("ownership_known_moves").update('), false);
});

Deno.test("Stamped Core creation uses collection authority without a conflicting update authority", async () => {
  const source = await Deno.readTextFile(
    new URL(`${repoPrefix}backend/chainbound_game/programs/chainbound_game/src/lib.rs`, import.meta.url),
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
    functionSourceUrl("ownership_prepare_devnet_smoke_grant/index.ts"),
  );
  const confirm = await Deno.readTextFile(
    functionSourceUrl("ownership_confirm_devnet_smoke_grant/index.ts"),
  ).then((source) => source.replaceAll("\r\n", "\n"));
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

Deno.test("Mainnet smoke wallet allowlist uses the same strict address validation", () => {
  const wallet = "9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj";
  const wallets = parseMainnetSmokeWallets(JSON.stringify([wallet]));
  assertEquals(wallets.size, 1);
  assertEquals(wallets.has(wallet), true);
  for (const value of ["[]", JSON.stringify([wallet, wallet]), "not-json"]) {
    let code = "";
    try {
      parseMainnetSmokeWallets(value);
    } catch (error) {
      code = error instanceof Error ? error.message : "unknown";
    }
    assertEquals(code, "invalid_mainnet_smoke_wallets");
  }
});

Deno.test("ownership RPC genesis guard accepts the selected cluster and fails closed on mismatch", async () => {
  assertEquals(DEVNET_GENESIS_HASH, "EtWTRABZaYq6iMfeYKouRu166VU2xqa1wcaWoxPkrZBG");
  assertEquals(MAINNET_GENESIS_HASH, "5eykt4UsFv8P8NJdTREpY1vzqKqZKvdpKuc147dw2N9d");
  const okFetch = (() =>
    Promise.resolve(
      new Response(JSON.stringify({ jsonrpc: "2.0", id: 1, result: MAINNET_GENESIS_HASH }), {
        status: 200,
      }),
    )) as typeof fetch;
  await ensureOwnershipRpcCluster({
    cluster: "mainnet-beta",
    rpcUrl: "https://mainnet-rpc.example.invalid/ok",
    expectedGenesisHash: MAINNET_GENESIS_HASH,
  }, okFetch);

  const wrongFetch = (() =>
    Promise.resolve(
      new Response(JSON.stringify({ jsonrpc: "2.0", id: 1, result: DEVNET_GENESIS_HASH }), {
        status: 200,
      }),
    )) as typeof fetch;
  let code = "";
  try {
    await ensureOwnershipRpcCluster({
      cluster: "mainnet-beta",
      rpcUrl: "https://mainnet-rpc.example.invalid/wrong",
      expectedGenesisHash: MAINNET_GENESIS_HASH,
    }, wrongFetch);
  } catch (error) {
    code = error instanceof Error ? error.message : "unknown";
  }
  assertEquals(code, "solana_rpc_cluster_mismatch");
});

Deno.test("Mainnet ownership mutation endpoints enforce the smoke-wallet boundary", async () => {
  for (
    const relativePath of [
      "../ownership_grant_event_known/index.ts",
      "../profile_grant_progression_ordinal/index.ts",
      "../ownership_prepare_stamp/index.ts",
      "../ownership_confirm_stamp/index.ts",
      "../ownership_reconcile_stamp/index.ts",
    ]
  ) {
    const source = await Deno.readTextFile(functionSourceUrl(relativePath));
    assertEquals(source.includes("isOwnershipWalletAllowed"), true, relativePath);
    assertEquals(source.includes("mainnet_smoke_wallet_not_allowed"), true, relativePath);
  }
});

Deno.test("walletless Known rolls are not constrained by the Stamp wallet allowlist", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_roll_known/index.ts"));
  assertEquals(source.includes("isOwnershipWalletAllowed"), false);
  assertEquals(source.includes("mainnet_smoke_wallet_not_allowed"), false);
});

Deno.test("Devnet smoke confirmation atomically mirrors verified RAM", async () => {
  const confirm = await Deno.readTextFile(
    functionSourceUrl("ownership_confirm_devnet_smoke_grant/index.ts"),
  ).then((source) => source.replaceAll("\r\n", "\n"));
  assertEquals(
    confirm.includes('supabase.rpc(\n      "ownership_confirm_devnet_smoke_grant_atomic"'),
    true,
  );
  assertEquals(confirm.includes('.from("ownership_ram_grant_operations")\n      .update('), false);
  assertEquals(confirm.includes("p_balance_after: verified.balanceAfter"), true);
});

Deno.test("operator RAM reconciliation is receipt-backed and retires the legacy top-up", async () => {
  const source = await Deno.readTextFile(
    functionSourceUrl("ram_operator_reconcile/index.ts"),
  );
  const legacy = await Deno.readTextFile(
    functionSourceUrl("ownership_operator_devnet_ram_topup/index.ts"),
  );
  assertEquals(source.includes("reconcileRamDeltaOnChain"), true);
  assertEquals(source.includes("ram_reconciliation_approve"), true);
  assertEquals(source.includes("ram_reconciliation_mark_submitted"), true);
  assertEquals(source.includes("ram_reconciliation_settle"), true);
  assertEquals(source.includes("CHAINBOUND_OPERATOR_ID"), true);
  assertEquals(source.includes(".update({ ram_balance"), false);
  assertEquals(legacy.includes("legacy_operator_topup_retired_use_ram_operator_reconcile"), true);
  assertEquals(typeof reconcileRamDeltaOnChain, "function");
});

Deno.test("inventory hydration includes the canonical RAM mirror", async () => {
  const inventory = await Deno.readTextFile(
    functionSourceUrl("ownership_get_inventory/index.ts"),
  );
  assertEquals(inventory.includes("readRamBalanceSnapshot"), true);
  assertEquals(inventory.includes("ramBalanceVersion: ramSnapshot.version"), true);
  assertEquals(inventory.includes("stampRamCost: config.stampRamCost"), true);
  assertEquals(inventory.includes("ramBalance,"), true);
  assertEquals(inventory.includes("stampEffectFieldsFromOperation(operation)"), true);
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
    "baseline_striker",
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
  const first = await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH, "baseline_striker");
  const retry = await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH, "baseline_striker");
  assertEquals(retry, first);
});

Deno.test("Stamp lock uses leftover digest bytes and stays inside catalog min/max", async () => {
  const secret = Uint8Array.from({ length: 64 }, (_, index) => index);
  const result = await deriveStampResult(secret, "profile-a", "known-a", 1, 1, EXPECTED_MODIFIER_POOL_HASH, "baseline_striker");
  assertEquals(result.modifierEffectValues.length, 1);
  const bounds = MODIFIER_EFFECT_BOUNDS[result.modifierIds[0] as keyof typeof MODIFIER_EFFECT_BOUNDS];
  const min = Math.min(bounds.min, bounds.max);
  const max = Math.max(bounds.min, bounds.max);
  assertEquals(result.modifierEffectValues[0] >= min, true);
  assertEquals(result.modifierEffectValues[0] <= max, true);
  assertEquals(formatLockedModifierEffect("+8-12% base power", 10), "10% base power");
  assertEquals(formatLockedModifierEffect("heal {n}% Max HP", 12.5), "heal 12.5% Max HP");
});

Deno.test("Phase 1 Stamp preparation enforces one modifier and resumes a canonical intent", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_prepare_stamp/index.ts"));
  assertEquals(source.includes("modifierCount !== 1"), true);
  assertEquals(source.includes("canonicalIdempotencyKey"), true);
  assertEquals(source.includes("stamp_intent_in_progress"), true);
  assertEquals(source.includes("modifier_effect_values: result.modifierEffectValues"), true);
  assertEquals(source.includes(".upsert(operation"), false);
});

Deno.test("Stamp confirmation persists signature before RPC and settles from the consumed program receipt", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_confirm_stamp/index.ts"));
  const submitted = source.indexOf('status: "submitted"');
  const rpcCheck = source.indexOf("finalizedSignatureOutcome(config.rpcUrl, signature)");
  assertNotEquals(submitted, -1);
  assertNotEquals(rpcCheck, -1);
  assertEquals(submitted < rpcCheck, true);
  assertEquals(source.includes("fetchAndVerifyStampReceipt"), true);
  assertEquals(source.includes('supabase.rpc("ram_ledger_mark_chain_confirmed"'), true);
  assertEquals(source.includes('status: "das_pending"'), false);
  assertEquals(source.includes("expires_at"), false);
});

Deno.test("Stamp preparation reserves coordinated RAM before chain build, metadata, or intent creation", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_prepare_stamp/index.ts"));
  const reserve = source.indexOf('supabase.rpc("ram_ledger_reserve_operation"');
  const ensure = source.indexOf("ensureRamLedgerAccount(");
  const chainReservation = source.indexOf("ensureRamReservationOnChain({");
  const chainBuild = source.indexOf("buildStampTransaction({");
  const preflight = source.indexOf("simulateStampTransaction(config.rpcUrl");
  const metadata = source.indexOf("publishStampedMetadata(");
  const operationWrite = source.indexOf(".insert(operation)");
  assertNotEquals(reserve, -1);
  assertNotEquals(ensure, -1);
  assertNotEquals(chainReservation, -1);
  assertNotEquals(chainBuild, -1);
  assertNotEquals(preflight, -1);
  assertNotEquals(metadata, -1);
  assertNotEquals(operationWrite, -1);
  assertEquals(ensure < reserve, true);
  assertEquals(reserve < chainReservation, true);
  assertEquals(chainReservation < chainBuild, true);
  assertEquals(chainBuild < preflight, true);
  assertEquals(preflight < metadata, true);
  assertEquals(metadata < operationWrite, true);
  assertEquals(source.includes("known.onchain_instance_id != null"), true);
  assertEquals(source.includes("known_source: built.knownSource"), true);
});

Deno.test("profile-native Stamp uses an authority ticket and no wallet MoveVault or chain RAM debit", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  assertEquals(source.includes("PREPARE_PROFILE_STAMP_DISCRIMINATOR"), true);
  assertEquals(source.includes("STAMP_PROFILE_KNOWN_DISCRIMINATOR"), true);
  const program = await Deno.readTextFile(
    new URL(`${repoPrefix}backend/chainbound_game/programs/chainbound_game/src/lib.rs`, import.meta.url),
  );
  const start = program.indexOf("pub fn stamp_profile_known_move");
  const end = program.indexOf("pub fn bind_chainling", start);
  assertNotEquals(start, -1);
  assertNotEquals(end, -1);
  const instruction = program.slice(start, end);
  assertEquals(instruction.includes("consume_ram_reservation"), false);
  assertEquals(instruction.includes("remove_known_by_instance"), false);
  assertEquals(instruction.includes("CreateV2CpiBuilder"), true);
});

Deno.test("Stamp reconciliation terminalizes failed signatures and expired unsigned reviews", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_reconcile_stamp/index.ts"));
  assertEquals(
    source.includes('failureCode = outcome === "failed" ? "transaction_failed" : "unsigned_intent_expired"'),
    true,
  );
  assertEquals(source.includes("pending: false"), true);
  assertEquals(source.includes('operation_status: "failed"'), true);
});

Deno.test("Stamp reconciliation can recover a signature from the deterministic asset", async () => {
  const source = await Deno.readTextFile(functionSourceUrl("ownership_reconcile_stamp/index.ts"));
  assertEquals(source.includes("findFinalizedSignatureForAddress"), true);
  assertEquals(source.includes("fetchAndVerifyStampReceipt"), true);
  assertEquals(source.includes('supabase.rpc("ownership_confirm_stamp_reserved"'), true);
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

Deno.test("Stamp verification uses a server-only Helius endpoint pinned to the ownership cluster", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  assertEquals(source.includes("requireHeliusDasUrl(Deno.env.get(HELIUS_DAS_URL_ENV), cluster)"), true);
  assertEquals(source.includes("mainnet_program_must_be_isolated"), true);
  assertEquals(source.includes("mainnet_ownership_disabled"), true);
  assertEquals(source.includes('dasUrl: requiredEnv("CHAINBOUND_DAS_RPC_URL")'), false);
});
