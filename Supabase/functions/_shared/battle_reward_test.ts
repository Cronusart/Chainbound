import { assertEquals, assertThrows } from "https://deno.land/std@0.224.0/assert/mod.ts";
import { battleRewardIdempotencyKey, loadBattleRewardMode, loadBattleRewardProfileAllowlist } from "./battle_reward.ts";

function withEnv(values: Record<string, string | undefined>, run: () => void): void {
  const previous = new Map<string, string | undefined>();
  try {
    for (const [key, value] of Object.entries(values)) {
      previous.set(key, Deno.env.get(key));
      if (value === undefined) Deno.env.delete(key);
      else Deno.env.set(key, value);
    }
    run();
  } finally {
    for (const [key, value] of previous) {
      if (value === undefined) Deno.env.delete(key);
      else Deno.env.set(key, value);
    }
  }
}

Deno.test("battle reward mode defaults to database-authoritative pass-through and rejects unknown values", () => {
  withEnv({ BATTLE_REWARD_MODE: undefined }, () => assertEquals(loadBattleRewardMode(), "grant"));
  withEnv({ BATTLE_REWARD_MODE: "wat" }, () => {
    assertThrows(() => loadBattleRewardMode(), Error, "invalid_battle_reward_mode");
  });
});

Deno.test("battle reward env can restrict but does not duplicate the database cluster gate", () => {
  withEnv({ BATTLE_REWARD_MODE: "off" }, () => assertEquals(loadBattleRewardMode(), "off"));
  withEnv({ BATTLE_REWARD_MODE: "shadow" }, () => assertEquals(loadBattleRewardMode(), "shadow"));
  withEnv({ BATTLE_REWARD_MODE: "grant", CHAINBOUND_SOLANA_CLUSTER: "mainnet-beta" }, () => {
    assertEquals(loadBattleRewardMode(), "grant");
  });
});

Deno.test("battle reward profile allowlist requires exact unique UUIDs", () => {
  const profileId = "31000000-0000-4000-8000-000000000001";
  withEnv({ BATTLE_REWARD_DEVNET_PROFILE_ALLOWLIST: undefined }, () => {
    assertEquals(loadBattleRewardProfileAllowlist(), null);
  });
  withEnv({ BATTLE_REWARD_DEVNET_PROFILE_ALLOWLIST: JSON.stringify([profileId]) }, () => {
    assertEquals(loadBattleRewardProfileAllowlist()?.has(profileId), true);
  });
  withEnv({ BATTLE_REWARD_DEVNET_PROFILE_ALLOWLIST: JSON.stringify([profileId, profileId]) }, () => {
    assertThrows(() => loadBattleRewardProfileAllowlist(), Error, "invalid_battle_reward_profile_allowlist");
  });
});

Deno.test("battle reward idempotency key stays domain separated", () => {
  assertEquals(
    battleRewardIdempotencyKey("devnet", "31000000-0000-4000-8000-000000000001"),
    "battle_reward:v1:devnet:31000000-0000-4000-8000-000000000001",
  );
});
