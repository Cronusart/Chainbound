import { assert, assertEquals, assertNotEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  deriveKnownBundle,
  KNOWN_ROLL_MAX_PULLS,
  knownBundlePullKey,
  knownBundleRpcPulls,
  parsePullCount,
} from "./known_roll_bundle.ts";
import { deriveKnownResult, deriveRamOperationId, KNOWN_ROLL_RAM_COST } from "./ownership_spine.ts";

const secret = Uint8Array.from({ length: 64 }, (_, index) => index);
const migrationUrl = new URL("../../migrations/20261027120000_known_roll_bundles.sql", import.meta.url);

async function readMigration(): Promise<string> {
  return (await Deno.readTextFile(migrationUrl)).replaceAll("\r\n", "\n");
}

Deno.test("pull_count defaults to one and rejects anything outside 1..20", () => {
  assertEquals(parsePullCount(undefined), 1);
  assertEquals(parsePullCount(null), 1);
  assertEquals(parsePullCount(1), 1);
  assertEquals(parsePullCount(10), 10);
  assertEquals(parsePullCount(KNOWN_ROLL_MAX_PULLS), 20);
  for (const invalid of [0, -1, 21, 2.5, "5", true, {}, Number.NaN]) {
    assertEquals(parsePullCount(invalid), null, `pull_count ${String(invalid)}`);
  }
});

Deno.test("bundle pulls are deterministic in (profile, key, index) and replay byte-identically", async () => {
  const first = await deriveKnownBundle(secret, "profile-a", "bundle-a", 10);
  const replay = await deriveKnownBundle(secret, "profile-a", "bundle-a", 10);
  assertEquals(replay, first);
  assertEquals(first.length, 10);
  assertEquals(new Set(first.map((pull) => pull.operationId)).size, 10);
  assertEquals(new Set(first.map((pull) => pull.idempotencyKey)).size, 10);

  // A smaller retry is a prefix, which is why the RPC pins pull_count on the bundle row.
  assertEquals(await deriveKnownBundle(secret, "profile-a", "bundle-a", 5), first.slice(0, 5));

  const otherProfile = await deriveKnownBundle(secret, "profile-b", "bundle-a", 10);
  const otherKey = await deriveKnownBundle(secret, "profile-a", "bundle-b", 10);
  for (let index = 0; index < 10; index++) {
    assertNotEquals(otherProfile[index].operationId, first[index].operationId);
    assertNotEquals(otherKey[index].operationId, first[index].operationId);
  }
});

Deno.test("each bundle pull matches a single roll keyed by its per-pull key", async () => {
  const pulls = await deriveKnownBundle(secret, "profile-a", "bundle-a", 3);
  for (const pull of pulls) {
    assertEquals(pull.idempotencyKey, knownBundlePullKey("bundle-a", pull.index));
    assertEquals(pull.result, await deriveKnownResult(secret, "profile-a", pull.idempotencyKey));
    assertEquals(pull.operationId, await deriveRamOperationId("profile-a", "known_roll", pull.idempotencyKey));
  }
});

Deno.test("RPC pull payload carries one ordered entry per pull", async () => {
  const pulls = await deriveKnownBundle(secret, "profile-a", "bundle-a", 4);
  const payload = knownBundleRpcPulls(pulls);
  assertEquals(payload.length, 4);
  payload.forEach((entry, index) => {
    assertEquals(Object.keys(entry).sort(), ["base_power", "element", "move_id", "onchain_move_id", "operation_id"]);
    assertEquals(entry.operation_id, pulls[index].operationId);
    assertEquals(entry.move_id, pulls[index].result.moveId);
  });
});

Deno.test("bundle RPC derives the same per-pull key and limits as the Edge helper", async () => {
  const sql = await readMigration();
  assertEquals(knownBundlePullKey("k", 7), "k#7");
  assert(sql.includes("v_idempotency_key || '#' || v_index::TEXT"));
  assert(sql.includes(`pull_count BETWEEN 2 AND ${KNOWN_ROLL_MAX_PULLS}`));
  assert(sql.includes(`v_pull_count NOT BETWEEN 2 AND ${KNOWN_ROLL_MAX_PULLS}`));
  assert(sql.includes(`p_ram_cost_per_pull IS DISTINCT FROM ${KNOWN_ROLL_RAM_COST}`));
});

Deno.test("bundle RPC prices the whole bundle before writing any pull", async () => {
  const sql = await readMigration();
  const body = sql.slice(sql.indexOf("CREATE OR REPLACE FUNCTION public.ownership_roll_known_bundle_profile_atomic"));
  const lock = body.indexOf(
    "FROM public.player_profiles\n     WHERE player_profile_id = p_player_profile_id\n     FOR UPDATE",
  );
  const replay = body.indexOf("FROM public.ownership_known_roll_bundles");
  const capacity = body.indexOf("IF v_known_count + v_pull_count > 50 THEN");
  const insufficient = body.indexOf("v_profile.ram_balance < v_ram_cost_total THEN RAISE EXCEPTION 'insufficient_ram'");
  const firstWrite = body.indexOf("INSERT INTO public.ownership_known_moves");
  const debit = body.indexOf("SET ram_balance = ram_balance - v_ram_cost_total");
  assert(lock > 0 && replay > lock, "profile lock precedes the bundle replay lookup");
  assert(capacity > replay, "replay returns before the capacity check, so a settled bundle is never refused");
  assert(insufficient > capacity, "capacity is checked before RAM");
  assert(firstWrite > insufficient, "no pull is written before the bundle is priced");
  assert(debit > firstWrite, "one debit for the whole bundle");
  assertEquals(body.split("SET ram_balance = ram_balance -").length - 1, 1);
});

Deno.test("bundle RPC refuses bundles that overflow the 50-Known inventory", async () => {
  const sql = await readMigration();
  assert(sql.includes("AND consumed_at IS NULL;"));
  assert(sql.includes("RAISE EXCEPTION 'inventory_full free_slots=% required=%'"));
  assertEquals(/inventory_full free_slots=(\d+)/.exec("inventory_full free_slots=3 required=10")?.[1], "3");
});

Deno.test("bundle table and RPC are service-only", async () => {
  const sql = await readMigration();
  assert(sql.includes("ALTER TABLE public.ownership_known_roll_bundles ENABLE ROW LEVEL SECURITY;"));
  assert(sql.includes("REVOKE ALL ON TABLE public.ownership_known_roll_bundles FROM PUBLIC, anon, authenticated;"));
  assert(sql.includes("UNIQUE (player_profile_id, idempotency_key)"));
  assert(sql.includes("SECURITY DEFINER\nSET search_path = ''"));
  assert(
    /REVOKE ALL ON FUNCTION public\.ownership_roll_known_bundle_profile_atomic\([^)]*\) FROM PUBLIC, anon, authenticated;/
      .test(sql),
  );
  assert(
    /GRANT EXECUTE ON FUNCTION public\.ownership_roll_known_bundle_profile_atomic\([^)]*\) TO service_role;/.test(sql),
  );
});

Deno.test("ownership_roll_known routes pull_count > 1 to the bundle RPC and keeps single-roll fields", async () => {
  const source = await Deno.readTextFile(new URL("../ownership_roll_known/index.ts", import.meta.url));
  assert(source.includes("parsePullCount(body.pull_count)"));
  assert(source.includes('"invalid_pull_count"'));
  assert(source.includes("if (pullCount > 1) return await rollBundle("));
  assert(source.includes('supabase.rpc("ownership_roll_known_bundle_profile_atomic"'));
  assert(source.includes('supabase.rpc("ownership_roll_known_profile_atomic"'));
  assert(source.includes("knowns: [knownResponse(payload.known as JsonMap)]"));
  assert(source.includes("known: knowns[0]"));
  assert(source.includes("ramCost = KNOWN_ROLL_RAM_COST * pullCount"));
  assert(source.includes('error: "inventory_full", freeSlots: Number(inventoryFull[1]), required: pullCount'));
});
