import { assert, assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";

const root = new URL("../", import.meta.url);
const walletConnect = await Deno.readTextFile(new URL("wallet_connect_verified/index.ts", root));
const accountHelpers = await Deno.readTextFile(new URL("_shared/account_helpers.ts", root));
const migration = await Deno.readTextFile(
  new URL("../../migrations/20261009143000_ram_v2_pre_rollout_hardening.sql", import.meta.url),
);

Deno.test("wallet resolve mode is read-only and reports profile_found or no_profile", () => {
  const resolveBranch = walletConnect.slice(
    walletConnect.indexOf('if (mode === "resolve")'),
    walletConnect.indexOf('const rpcName = mode === "create"'),
  );
  assert(resolveBranch.includes("wallet_resolve_bound_profile_service"));
  assert(resolveBranch.includes('status: str(resolution.status) || "no_profile"'));
  assert(!resolveBranch.includes("wallet_bind_profile_service"));
  assert(!resolveBranch.includes("wallet_create_bound_profile_service"));
});

Deno.test("wallet binding conflicts are stable 409 responses and outer failures never become 500", () => {
  for (const code of ["wallet_already_bound_to_profile", "wallet_bound_to_other_profile"]) {
    assert(walletConnect.includes(`"${code}"`));
  }
  assert(walletConnect.includes('code === "profile_not_found" ? 404 : 409'));
  assert(walletConnect.includes('error: "wallet_connect_conflict" }, 409'));
  assert(!walletConnect.includes('error: "wallet_connect_verified_failed" }, 500'));
});

Deno.test("spend helpers map ledger and permanent-binding mismatches to wallet_profile_mismatch", () => {
  assert(accountHelpers.includes('throw new WalletBindingError("wallet_profile_mismatch")'));
  assert(accountHelpers.includes('code === "ram_ledger_scope_mismatch"'));
  assertEquals((accountHelpers.match(/wallet_profile_mismatch/g) ?? []).length >= 2, true);
});

Deno.test("binding schema is permanent unique and service-role-only", () => {
  assert(migration.includes("CREATE UNIQUE INDEX player_profiles_bound_wallet_address_key"));
  assert(migration.includes("wallet_binding_is_permanent"));
  assert(migration.includes("wallet_create_bound_profile_service"));
  assert(migration.includes("wallet_bind_profile_service"));
  assert(migration.includes("FROM PUBLIC, anon, authenticated"));
});
