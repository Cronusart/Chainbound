import { assertEquals } from "https://deno.land/std@0.224.0/assert/mod.ts";
import {
  hashMatchmakingConfig,
  MATCHMAKING_CONFIG,
  matchmakingCanonical,
  rejectEnqueue,
  versionsCompatible,
} from "./matchmaking_config.ts";
import {
  applyDeadlineMisses,
  pairingCompatible,
  type PairingIdentity,
  partyLockError,
  settleHashes,
  settleResultWindow,
  validateTurnActions,
} from "./pvp_rules.ts";

Deno.test("matchmaking config hash is stable sha256 of the canonical lever string", async () => {
  const canonical = matchmakingCanonical();
  assertEquals(
    canonical,
    "matchmaking|v=1|casual=1|ranked=0|walletCasual=0|walletRanked=1|queueTimeout=120|forfeitMisses=2|turnLimit=60|versionSkew=0|ruleset=pvp_casual|rematch=30|lobbyMax=16|ratingStart=100|widen=1|bandCap=400|resultWindow=45|lockDeadline=30",
  );
  const hash = await hashMatchmakingConfig();
  assertEquals(hash, "f7354f292abebd03a5a1fee545d2f967ae8945339cf11bb78ad330a535800a43");
  assertEquals(await hashMatchmakingConfig(MATCHMAKING_CONFIG), hash);
});

Deno.test("enqueue rejects hash, ruleset, ranked, and missing wallet", async () => {
  const hash = await hashMatchmakingConfig();
  const base = {
    queueType: "casual",
    rulesetId: "pvp_casual",
    configHash: hash,
    appVersion: "1.0",
    resolverHash: "resolver",
    rulesetHash: "ruleset",
    buildStamp: "editor",
    walletLinked: false,
  };
  assertEquals(rejectEnqueue(MATCHMAKING_CONFIG, hash, base), null);
  assertEquals(rejectEnqueue(MATCHMAKING_CONFIG, hash, { ...base, configHash: "nope" }), "config_mismatch");
  assertEquals(rejectEnqueue(MATCHMAKING_CONFIG, hash, { ...base, rulesetId: "pvp_ranked" }), "ruleset_rejected");
  assertEquals(rejectEnqueue(MATCHMAKING_CONFIG, hash, { ...base, queueType: "ranked" }), "ranked_disabled");
  assertEquals(
    rejectEnqueue({ ...MATCHMAKING_CONFIG, requireWalletForCasual: true }, hash, base),
    "wallet_required",
  );
  assertEquals(rejectEnqueue({ ...MATCHMAKING_CONFIG, casualEnabled: false }, hash, base), "casual_disabled");
});

Deno.test("version skew zero requires an exact app version", () => {
  assertEquals(versionsCompatible("1.2.3", "1.2.3", 0), true);
  assertEquals(versionsCompatible("1.2.3", "1.2.4", 0), false);
  assertEquals(versionsCompatible("1.2.3", "1.2.4", 1), true);
  assertEquals(versionsCompatible("1.2.3", "9.2.4", 1), false);
});

Deno.test("pairing requires equal pins and refuses the same auth user", () => {
  const left: PairingIdentity = {
    queueType: "casual",
    rulesetId: "pvp_casual",
    configHash: "abc",
    appVersion: "1",
    resolverHash: "resolver",
    rulesetHash: "ruleset",
    buildStamp: "editor",
    authUserId: "user-a",
  };
  assertEquals(pairingCompatible(left, { ...left, authUserId: "user-b" }, 0), true);
  assertEquals(pairingCompatible(left, { ...left }, 0), false);
  assertEquals(pairingCompatible(left, { ...left, authUserId: "user-b", configHash: "def" }, 0), false);
  assertEquals(pairingCompatible(left, { ...left, authUserId: "user-b", resolverHash: "other" }, 0), false);
  assertEquals(pairingCompatible(left, { ...left, authUserId: "user-b", buildStamp: "device" }, 0), false);
  assertEquals(pairingCompatible(left, { ...left, authUserId: "user-b", appVersion: "2" }, 0), false);
});

Deno.test("turn validation checks the acting chainling, AP, and swaps", () => {
  const party = [{ moveIds: ["baseline_jab"], moveApCosts: [1] }];
  assertEquals(validateTurnActions("a", party, [{ actionType: 0, moveIndex: 0, teamIndex: 0 }]), null);
  assertEquals(validateTurnActions("a", party, [{ actionType: 0, moveIndex: 1, teamIndex: 0 }]), "move_illegal");
  assertEquals(validateTurnActions("a", party, [{ actionType: 0, moveIndex: 0 }]), "seat_mismatch");
  assertEquals(
    validateTurnActions("a", [{ moveIds: ["not_a_move"], moveApCosts: [1] }], [{
      actionType: 0,
      moveIndex: 0,
      teamIndex: 0,
    }]),
    "move_unknown",
  );
  assertEquals(validateTurnActions("a", party, [{ actionType: 3, teamIndex: 0 }]), "action_type");
  assertEquals(validateTurnActions("b", party, [{ actionType: 2, teamIndex: 0 }]), "seat_mismatch");
  assertEquals(validateTurnActions("a", party, [{ actionType: 1, swapTargetIndex: 0, teamIndex: 0 }]), "swap_illegal");
  assertEquals(validateTurnActions("a", party, []), "action_count");

  const bench = [
    { moveIds: ["baseline_jab"], moveApCosts: [1] },
    { moveIds: ["baseline_jab"], moveApCosts: [1] },
    { moveIds: ["baseline_jab"], moveApCosts: [1], fainted: true },
  ];
  assertEquals(
    validateTurnActions("a", bench, [
      { actionType: 1, swapTargetIndex: 2, teamIndex: 0 },
      { actionType: 2, teamIndex: 0 },
    ]),
    "swap_illegal",
  );
  assertEquals(
    validateTurnActions("a", bench, [
      { actionType: 1, swapTargetIndex: 1, teamIndex: 0 },
      { actionType: 2, teamIndex: 0 },
    ]),
    "swap_illegal",
  );
  assertEquals(
    validateTurnActions("a", party, [{
      actionType: 0,
      moveIndex: 0,
      teamIndex: 0,
      targetTeamIndex: 0,
      targetSlotIndex: 0,
    }]),
    null,
  );
  assertEquals(
    validateTurnActions("a", party, [{
      actionType: 0,
      moveIndex: 0,
      teamIndex: 0,
      targetTeamIndex: 1,
      targetSlotIndex: 0,
    }]),
    null,
  );
  assertEquals(
    validateTurnActions("a", party, [{ actionType: 0, moveIndex: 0, teamIndex: 0, targetTeamIndex: 2 }]),
    "target_illegal",
  );

  const benchMove = [
    { moveIds: ["baseline_jab"], moveApCosts: [1] },
    { moveIds: ["baseline_jab", "baseline_jab"], moveApCosts: [1, 1] },
  ];
  assertEquals(
    validateTurnActions("a", benchMove, [
      { actionType: 0, moveIndex: 1, teamIndex: 0 },
      { actionType: 2, teamIndex: 0 },
    ]),
    null,
  );
});

Deno.test("party lock rejects an oversized or all-fainted roster", () => {
  assertEquals(partyLockError([{ currentHp: -1 }]), null);
  assertEquals(partyLockError([]), "empty_party");
  assertEquals(partyLockError([{ currentHp: 0 }, { currentHp: 0 }, { currentHp: 0 }]), "party_fainted");
  assertEquals(partyLockError([{ currentHp: 0 }, { currentHp: -1 }]), null);
  assertEquals(
    partyLockError([{ currentHp: 10 }, { currentHp: 10 }, { currentHp: 10 }, { currentHp: 10 }]),
    "party_too_large",
  );
});

Deno.test("one forensic hash at the result window is disputed and misses stop", () => {
  assertEquals(settleResultWindow("abc", "", 0, null, false), { state: "pending", winnerSeat: null });
  assertEquals(settleResultWindow("abc", "", 0, null, true), { state: "disputed", winnerSeat: null });
  assertEquals(settleResultWindow("abc", "abc", 1, 1, false), { state: "completed", winnerSeat: "b" });
  assertEquals(settleResultWindow("abc", "abc", 0, 1, false), { state: "disputed", winnerSeat: null });
  assertEquals(settleResultWindow("abc", "def", 0, 0, false), { state: "disputed", winnerSeat: null });
  assertEquals(applyDeadlineMisses(1, 1, false, false, 2, true), { missesA: 1, missesB: 1, state: "continue" });
});

Deno.test("equal forensic hashes complete and mismatches dispute", () => {
  assertEquals(settleHashes("abc", ""), "pending");
  assertEquals(settleHashes("abc", "abc"), "completed");
  assertEquals(settleHashes("abc", "def"), "disputed");
});

Deno.test("missed turns forfeit the absent seat and cancel when both miss", () => {
  assertEquals(applyDeadlineMisses(0, 0, true, false, 2), { missesA: 0, missesB: 1, state: "continue" });
  assertEquals(applyDeadlineMisses(1, 0, true, false, 2), { missesA: 1, missesB: 1, state: "continue" });
  assertEquals(applyDeadlineMisses(0, 1, true, false, 2), { missesA: 0, missesB: 2, state: "forfeit_b" });
  assertEquals(applyDeadlineMisses(1, 1, false, false, 2), { missesA: 2, missesB: 2, state: "cancelled" });
});

Deno.test("migration keeps pvp tables service-role only and pairs with skip locked", async () => {
  const source = await Deno.readTextFile(
    new URL("../../migrations/20261007233709_pvp_battle_spine.sql", import.meta.url),
  );
  assertEquals(source.includes("CREATE TABLE IF NOT EXISTS public.matchmaking_queue"), true);
  assertEquals(source.includes("CREATE TABLE IF NOT EXISTS public.pvp_matches"), true);
  assertEquals(source.includes("CREATE TABLE IF NOT EXISTS public.pvp_match_turns"), true);
  assertEquals(source.includes("FOR UPDATE SKIP LOCKED"), true);
  assertEquals(source.includes("REVOKE ALL ON TABLE public.pvp_matches FROM PUBLIC, anon, authenticated"), true);
  assertEquals(source.includes("GRANT EXECUTE ON FUNCTION public.pvp_submit_turn_service"), true);
  assertEquals(source.includes("extensions.gen_random_bytes"), true);

  const hardeningFiles = [
    "20261008031031_pvp_spine_hardening.sql",
    "20261008031129_pvp_spine_result_window.sql",
    "20261008031150_pvp_spine_snapshot.sql",
    "20261008031212_pvp_spine_matchmaker.sql",
    "20261008031233_pvp_spine_lock_and_result.sql",
  ];
  let hardening = "";
  for (const name of hardeningFiles) {
    hardening += await Deno.readTextFile(new URL(`../../migrations/${name}`, import.meta.url));
  }
  assertEquals(hardening.includes("pvp_matchmaker_service"), true);
  assertEquals(hardening.includes("FOR UPDATE SKIP LOCKED"), true);
  assertEquals(hardening.includes("idx_pvp_matches_player_a_created"), true);
  assertEquals(hardening.includes("state = 'disputed'"), true);
  assertEquals(
    hardening.includes("REVOKE ALL ON FUNCTION public.pvp_matchmaker_service() FROM PUBLIC, anon, authenticated"),
    true,
  );

  let snapshotFix = false;
  for await (const entry of Deno.readDir(new URL("../../migrations/", import.meta.url))) {
    if (!entry.name.endsWith(".sql")) continue;
    const text = await Deno.readTextFile(new URL(`../../migrations/${entry.name}`, import.meta.url));
    if (text.includes("pending_actions_json") && text.includes("v_queue.enqueued_at > v_match.created_at")) {
      snapshotFix = true;
    }
  }
  assertEquals(snapshotFix, true);
});
