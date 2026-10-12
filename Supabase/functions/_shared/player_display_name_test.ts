import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  isPlayerDisplayNameClaimBlocked,
  isPlayerDisplayNameUniqueViolation,
  playerDisplayNamesConflict,
  shouldStampDisplayNameClaim,
} from "./player_display_name.ts";

Deno.test("player names collide case-insensitively and ignore surrounding space", () => {
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-b", "Nova", "nova"), true);
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-b", " Nova ", "NOVA"), true);
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-b", "Nova", "Nona"), false);
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-b", "Nova", null), false);
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-b", "Nova", "   "), false);
});

Deno.test("the same profile can re-set its own name", () => {
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-a", "NOVA", "nova"), false);
  assertEquals(playerDisplayNamesConflict("profile-a", "profile-a", "Nova", "Nova"), false);
  assertEquals(isPlayerDisplayNameClaimBlocked("tonic", "TONIC", ["tonic", "Tonic"]), false);
  assertEquals(isPlayerDisplayNameClaimBlocked("Nova", "nova", ["Nova"]), false);
});

Deno.test("a new claim is blocked by a grandfathered name and by another new claim", () => {
  assertEquals(isPlayerDisplayNameClaimBlocked(null, " TONIC ", ["tonic", "TONIC"]), true);
  assertEquals(isPlayerDisplayNameClaimBlocked("", "Nova", ["nova"]), true);
  assertEquals(isPlayerDisplayNameClaimBlocked("Pebble", "Nova", ["NOVA"]), true);
  assertEquals(isPlayerDisplayNameClaimBlocked(null, "Pebble", ["tonic"]), false);
});

Deno.test("only the sole holder is stamped into the unique index", () => {
  assertEquals(shouldStampDisplayNameClaim("Nova", []), true);
  assertEquals(shouldStampDisplayNameClaim("Pebble", ["tonic"]), true);
  assertEquals(shouldStampDisplayNameClaim("tonic", ["Tonic"]), false);
});

Deno.test("unique violations map to the name_taken race", () => {
  assertEquals(isPlayerDisplayNameUniqueViolation({ code: "23505", message: "duplicate key value" }), true);
  assertEquals(
    isPlayerDisplayNameUniqueViolation({
      message: 'duplicate key value violates unique constraint "ux_player_profiles_display_name_ci"',
    }),
    true,
  );
  assertEquals(isPlayerDisplayNameUniqueViolation({ code: "23502", message: "null value" }), false);
});

Deno.test("display name function and migration keep the case-insensitive contract", async () => {
  const functionSource = await Deno.readTextFile(new URL("../profile_set_display_name/index.ts", import.meta.url));
  const sharedSource = await Deno.readTextFile(new URL("./player_display_name.ts", import.meta.url));
  const mirrorSource = await Deno.readTextFile(
    new URL("../../supabase/functions/profile_set_display_name/index.ts", import.meta.url),
  );
  const mirrorShared = await Deno.readTextFile(
    new URL("../../supabase/functions/_shared/player_display_name.ts", import.meta.url),
  );
  const migration = await Deno.readTextFile(
    new URL("../../migrations/20261005061500_player_display_name_unique.sql", import.meta.url),
  );

  const charsetSource = await Deno.readTextFile(new URL("./display_name_charset.ts", import.meta.url));
  const mirrorCharset = await Deno.readTextFile(
    new URL("../../supabase/functions/_shared/display_name_charset.ts", import.meta.url),
  );
  const chainlingSource = await Deno.readTextFile(
    new URL("../profile_set_chainling_display_name/index.ts", import.meta.url),
  );
  const mirrorChainling = await Deno.readTextFile(
    new URL("../../supabase/functions/profile_set_chainling_display_name/index.ts", import.meta.url),
  );

  const normalizeLineEndings = (value: string) => value.replaceAll("\r\n", "\n");
  assertEquals(normalizeLineEndings(functionSource), normalizeLineEndings(mirrorSource));
  assertEquals(normalizeLineEndings(sharedSource), normalizeLineEndings(mirrorShared));
  assertEquals(normalizeLineEndings(charsetSource), normalizeLineEndings(mirrorCharset));
  assertEquals(normalizeLineEndings(chainlingSource), normalizeLineEndings(mirrorChainling));
  assertEquals(chainlingSource.includes("isDisplayNameCharsetValid"), true);
  assertEquals(chainlingSource.includes("\\p{L}"), false);
  assertEquals(functionSource.includes("profile_set_display_name_tx"), true);
  assertEquals(functionSource.includes("PLAYER_DISPLAY_NAME_TAKEN"), true);
  assertEquals(sharedSource.includes('PLAYER_DISPLAY_NAME_TAKEN = "name_taken"'), true);
  assertEquals(functionSource.includes("isPlayerDisplayNameUniqueViolation"), true);
  assertEquals(migration.includes("ux_player_profiles_display_name_ci"), true);
  assertEquals(migration.includes("lower(btrim(display_name))"), true);
  assertEquals(migration.includes("display_name_claimed_at IS NOT NULL"), true);
  assertEquals(migration.includes("ADD COLUMN IF NOT EXISTS display_name_claimed_at TIMESTAMPTZ NULL"), true);
  assertEquals(migration.includes("IF NOT v_same_name AND NOT v_sole_holder"), true);
  assertEquals(migration.includes("'name_taken'"), true);
  assertEquals(functionSource.includes("isDisplayNameCharsetValid"), true);
  assertEquals(migration.includes("RAISE EXCEPTION"), false);

  const allowlist = await Deno.readTextFile(
    new URL("../../migrations/20261010031500_display_name_allowlist.sql", import.meta.url),
  );
  assertEquals(allowlist.includes("display_name_characters_allowed"), true);
  assertEquals(allowlist.includes("profile_set_chainling_display_name_tx"), true);
  assertEquals(allowlist.includes("profile_commit_starter_tx"), true);
  assertEquals(allowlist.includes("lower(btrim(display_name))"), true);
  assertEquals(migration.includes("This migration does not change existing display_name values."), true);
  assertEquals(/DELETE\s+FROM\s+public\.player_profiles/i.test(migration), false);
});
