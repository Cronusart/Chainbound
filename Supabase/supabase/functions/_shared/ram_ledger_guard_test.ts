import { assertEquals } from "https://deno.land/std@0.177.0/testing/asserts.ts";

const migrationsRoot = new URL("migrations/", new URL("../../", import.meta.url));

Deno.test("post-activation migrations reject unapproved direct RAM balance writes", async () => {
  const firstReviewedWriter = "20261003113101_ram_shop_unified_ledger.sql";
  const allowedWriters = new Set([
    firstReviewedWriter,
    "20261009010622_ram_v2_postgres_canonical_earn.sql",
    "20261009143000_ram_v2_pre_rollout_hardening.sql",
    "20261014030000_fisher_ape_lucky_crayon.sql",
    "20261015042331_profile_owned_known_rolls.sql",
    // Bundle debit: one settled known_roll op per pull, summed into one balance update.
    "20261027120000_known_roll_bundles.sql",
    // Shop settle: one settled shop_purchase credit per order, same transaction.
    "20261028120000_ram_shop_ledger_credit.sql",
    // Same settle body plus the credit_mode receipt gate.
    "20261028120100_ram_shop_credit_mode.sql",
    // Quest RAM through the daily cap: settled quest_reward credit of the applied amount.
    "20261028120200_quest_reward_daily_cap.sql",
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

Deno.test("quest reward RAM writes are backed by a settled ledger operation", async () => {
  const source = await Deno.readTextFile(
    new URL("20261014030000_fisher_ape_lucky_crayon.sql", migrationsRoot),
  );
  assertEquals(source.includes("INSERT INTO public.ram_ledger_operations"), true);
  assertEquals(source.includes("'quest_reward', 'credit'"), true);
  assertEquals(source.includes("'settled'"), true);
  assertEquals(source.includes("SET ram_balance = ram_balance + v_reward.ram_amount"), true);
});

Deno.test("profile-owned candy rolls debit RAM through a settled ledger operation", async () => {
  const source = await Deno.readTextFile(
    new URL("20261015042331_profile_owned_known_rolls.sql", migrationsRoot),
  );
  assertEquals(source.includes("INSERT INTO public.ram_ledger_operations"), true);
  assertEquals(source.includes("'known_roll', 'debit'"), true);
  assertEquals(source.includes("'settled'"), true);
  assertEquals(source.includes("SET ram_balance = ram_balance - p_ram_cost"), true);
});

Deno.test("known roll bundles debit RAM through one settled ledger operation per pull", async () => {
  const source = await Deno.readTextFile(
    new URL("20261027120000_known_roll_bundles.sql", migrationsRoot),
  );
  assertEquals(source.includes("INSERT INTO public.ram_ledger_operations"), true);
  assertEquals(source.includes("'known_roll', 'debit', p_ram_cost_per_pull, 'settled'"), true);
  assertEquals(source.includes("CHECK (ram_cost_total = pull_count * ram_cost_per_pull)"), true);
  assertEquals(source.includes("IF p_ram_cost_per_pull IS DISTINCT FROM 5 THEN"), true);
  assertEquals(source.includes("SET ram_balance = ram_balance - v_ram_cost_total"), true);
});

Deno.test("shop purchases credit RAM through a settled shop_purchase ledger operation", async () => {
  const source = await Deno.readTextFile(
    new URL("20261028120000_ram_shop_ledger_credit.sql", migrationsRoot),
  );
  assertEquals(source.includes("INSERT INTO public.ram_ledger_operations"), true);
  assertEquals(source.includes("'shop_purchase', 'credit', v_order.ram_amount, 'settled'"), true);
  assertEquals(source.includes("SET ram_balance = ram_balance + v_order.ram_amount"), true);
  // Purchases stay outside the daily earn cap (RAM_00 section 3, R2 decision D2).
  assertEquals(source.includes("UPDATE public.ram_daily_earnings"), false);
  assertEquals(source.includes("INSERT INTO public.ram_daily_earnings"), false);
});

Deno.test("credit_mode settle keeps the shop_purchase ledger credit", async () => {
  const source = await Deno.readTextFile(
    new URL("20261028120100_ram_shop_credit_mode.sql", migrationsRoot),
  );
  assertEquals(source.includes("'shop_purchase', 'credit', v_order.ram_amount, 'settled'"), true);
  assertEquals(source.includes("RAISE EXCEPTION 'credit_receipt_required'"), true);
  assertEquals(source.includes("UPDATE public.ram_daily_earnings"), false);
});

Deno.test("quest rewards credit only the capped amount through the daily earn bucket", async () => {
  const source = await Deno.readTextFile(
    new URL("20261028120200_quest_reward_daily_cap.sql", migrationsRoot),
  );
  assertEquals(source.includes("'quest_reward', 'credit', v_applied, 'settled'"), true);
  assertEquals(source.includes("v_applied := LEAST(v_reward.ram_amount, GREATEST(0, v_daily_cap - v_earned))"), true);
  assertEquals(source.includes("SET earned = earned + v_applied"), true);
  assertEquals(source.includes("SET ram_balance = ram_balance + v_applied"), true);
  assertEquals(source.includes("ram_balance + v_reward.ram_amount"), false);
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
