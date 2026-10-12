import { assertEquals } from "https://deno.land/std@0.177.0/testing/asserts.ts";

const migrationsRoot = new URL("migrations/", new URL("../../", import.meta.url));

Deno.test("post-activation migrations reject unapproved direct RAM balance writes", async () => {
  const firstReviewedWriter = "20261003113101_ram_shop_unified_ledger.sql";
  const allowedWriters = new Set([
    firstReviewedWriter,
    "20261009010622_ram_v2_postgres_canonical_earn.sql",
    "20261009143000_ram_v2_pre_rollout_hardening.sql",
  ]);
  for await (const entry of Deno.readDir(migrationsRoot)) {
    if (!entry.isFile || !entry.name.endsWith(".sql") || entry.name < firstReviewedWriter) continue;
    const source = await Deno.readTextFile(new URL(entry.name, migrationsRoot));
    const writesRam = /SET\s+ram_balance\s*=|ram_balance\s*=\s*ram_balance\s*[+-]/i.test(source);
    assertEquals(
      writesRam && !allowedWriters.has(entry.name),
      false,
      `${entry.name} writes ram_balance outside the reviewed settlement migration`,
    );
  }
});

Deno.test("Edge functions never write player profile RAM directly", async () => {
  const functionsRoot = new URL("functions/", new URL("../../", import.meta.url));
  for await (const directory of Deno.readDir(functionsRoot)) {
    if (!directory.isDirectory || directory.name === "_shared") continue;
    const index = new URL(`${directory.name}/index.ts`, functionsRoot);
    try {
      const source = await Deno.readTextFile(index);
      const directWrite = /from\(["']player_profiles["']\)[\s\S]{0,300}update\([\s\S]{0,160}ram_balance/i.test(
        source,
      );
      assertEquals(directWrite, false, `${directory.name} directly writes player_profiles.ram_balance`);
    } catch (error) {
      if (!(error instanceof Deno.errors.NotFound)) throw error;
    }
  }
});

Deno.test("legacy receipt-less RAM writers are revoked after ledger activation", async () => {
  const source = await Deno.readTextFile(
    new URL("20261006110545_ram_reconciliation_operator_flow.sql", migrationsRoot),
  );
  for (
    const functionName of [
      "profile_upgrade_move_item_tx",
      "ownership_confirm_devnet_smoke_grant_atomic",
      "ownership_reconcile_operator_devnet_ram_topup_atomic",
    ]
  ) {
    assertEquals(source.includes(`REVOKE ALL ON FUNCTION public.${functionName}`), true, functionName);
  }
});
