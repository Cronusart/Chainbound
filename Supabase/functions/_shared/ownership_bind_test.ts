import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";

Deno.test("Bind endpoints use the shared reservation and receipt-backed settlement flow", async () => {
  const prepare = await Deno.readTextFile(new URL("../ownership_prepare_bind/index.ts", import.meta.url));
  const confirm = await Deno.readTextFile(new URL("../ownership_confirm_bind/index.ts", import.meta.url));
  const reconcile = await Deno.readTextFile(new URL("../ownership_reconcile_bind/index.ts", import.meta.url));
  const shared = await Deno.readTextFile(new URL("./ownership_bind.ts", import.meta.url));
  assertEquals(prepare.includes('p_operation_kind: "bind"'), true);
  assertEquals(prepare.includes("ensureRamReservationOnChain"), true);
  assertEquals(prepare.includes("kind: 2"), true);
  assertEquals(prepare.includes("simulateBindTransaction"), true);
  assertEquals(confirm.includes("advanceBindOperation"), true);
  assertEquals(reconcile.includes("advanceBindOperation"), true);
  assertEquals(shared.includes("fetchAndVerifyBindReceipt"), true);
  assertEquals(shared.includes("submittedLegacyTransactionOutcome"), true);
  assertEquals(shared.includes('supabase.rpc("ram_ledger_mark_chain_confirmed"'), true);
  assertEquals(shared.includes('supabase.rpc("ownership_settle_bind_reserved"'), true);
});

Deno.test("Bind production migration is empty, service-only, and replay constrained", async () => {
  const migration = await Deno.readTextFile(
    new URL("../../migrations/20261003183500_ownership_bind_coordination.sql", import.meta.url),
  );
  assertEquals(/INSERT\s+INTO\s+public\.ownership_bind_sources/i.test(migration), false);
  for (const table of ["ownership_bind_sources", "ownership_bind_operations", "ownership_chainling_bindings"]) {
    assertEquals(migration.includes(`ALTER TABLE public.${table} ENABLE ROW LEVEL SECURITY`), true, table);
    assertEquals(
      migration.includes(`REVOKE ALL ON TABLE public.${table} FROM PUBLIC, anon, authenticated`),
      true,
      table,
    );
  }
  assertEquals(migration.includes("ownership_bind_operation_signature_unique"), true);
  assertEquals(migration.includes("ownership_bind_operation_receipt UNIQUE"), true);
  assertEquals(migration.includes("ownership_chainling_binding_receipt UNIQUE"), true);
  assertEquals(migration.includes("FOR UPDATE"), true);
  assertEquals(migration.includes("SET search_path = ''"), true);
});

Deno.test("Bind receipt verification pins exact program fields and finalized evidence", async () => {
  const source = await Deno.readTextFile(new URL("./ownership_spine.ts", import.meta.url));
  assertEquals(source.includes("data.length !== 179"), true);
  assertEquals(source.includes("BIND_RECEIPT_DISCRIMINATOR"), true);
  assertEquals(source.includes('new Connection(args.config.rpcUrl, "finalized")'), true);
  assertEquals(source.includes("actual.serializeMessage()"), true);
  assertEquals(source.includes("expected.serializeMessage()"), true);
});
