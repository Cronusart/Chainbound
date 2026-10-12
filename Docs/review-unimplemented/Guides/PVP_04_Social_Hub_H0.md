> **Parked for review.** Social hub H0 (presence foundations) brief + M0.3 punch list from audit round 3. Not implemented. Review list: [../README.md](../README.md).

# PvP Phase 4 — Social Hub H0 (Presence Foundations) + M0.3 Punch List

**Status:** Spec / pre-build. H0 may start on its own branch in parallel with matrix testing; ships only after the M2 gate  
**Audience:** Cronus, implementing agents  
**Depends on:** M0 battle spine (PVP_00), existing casual queue (`ColosseumQueueScreen`, `SupabaseMatchmakingService`, `CasualPvpMatchFlow`), `SceneLoader`, `PlayerMenuController`  
**Does not include:** Battles over NGO, chat, trading, economy, ranked escrow, tournaments, Mainnet  
**Related:** [`PVP_00_Battle_Spine_First.md`](PVP_00_Battle_Spine_First.md), [`PVP_01_Social_Lobby_And_Casual_Matchmaking.md`](PVP_01_Social_Lobby_And_Casual_Matchmaking.md), [`PVP_02_Ranked_Escrow_And_Ladder.md`](PVP_02_Ranked_Escrow_And_Ladder.md)

---

## 1. Where PvP stands (as of 2026-10-08)

- **M0.2 cleared for phone-vs-phone** matrix testing on Devnet (`jhzqcbrzkausrrtcavuu`). Mainnet Smoke has no PvP tables, functions, migrations, or pg_cron.
- **P0s fixed:** build stamp (`PvpBuildStamp.cs`, identical Editor/device), expired-queue snapshot (`20261008063332_pvp_spine_snapshot_pending.sql`), SingleAlly targets (`pvp_rules.ts`). P1-5 (rejected turn re-prompts instead of ending locally) and P1-6 (pending-action resubmit on rejoin, idempotent) fixed; P1-7 digest implemented; P1-4 partial.
- Devnet: 12 `pvp_*` SQL bodies match local migrations; deployed `pvp_rules.ts` matches local; `pvp_matchmaker` cron healthy (0 failures). Match tables were empty at audit time (no live match run yet).
- **PvP work is uncommitted on `main`** (HEAD `4968c3d`; edge functions, migrations from `20261007063318`, Unity PvP scripts untracked; Combat files modified). Commit to a branch before building matrix clients so builds are reproducible.
- **Access decision (2026-10-08):** multiplayer gated behind Ferryman → hub; player-menu Colosseum tab is dev-only.

## 2. M0.3 punch list (before Editor-vs-phone and the 20-match streak)

| ID | Issue | Fix |
|----|-------|-----|
| B2 | Legal swaps rejected: server active-chainling model goes stale (swap→Pass, auto-swap after faint, swap moves) → `swap_illegal` → "Choose again" loop until timeout forfeit | Server stops checking swaps / active chainling (like moves already); let both resolvers + forensic hash catch illegality |
| B4 | Digest floats (`"0.0000"` mitigation/accuracy) are inside the forensic hash → cross-arch float noise becomes a dispute | Quantize to scaled ints before hashing |
| B3 | Digest runs for every battle (extra `Debug.Log` + duplicate `TurnEnd` event, PvE hashes change) | Gate digest to PvP only; PvE unaffected |
| B1 | Build stamp is a hand-typed constant; data edits can pair with an older build | Derive from ruleset + move-data hash (or a CI-written content hash) |
| — | `winnerTeamIndex = -1` sent as-is → `winner_illegal` → Draw (`CombatBattleController.cs` ~2119) | Map -1 → -2 on client |
| B5 | Rejoin republishes and replays a finished battle during the result window (pause/resume) | Don't rejoin a match whose local battle already ended |
| B6 | Rejected turns are logged only locally | Log rejected turns server-side so model-caused disputes are visible |

**Carryover P2s**

- `pvp_matchmaker` service-role / cron only: drop the op from `pvp_edge`, stop enqueue running the full matchmaker.
- Sweep: `ORDER BY match_id` + `SKIP LOCKED`.
- Ruleset hash should cover move / species / chemistry / status catalogs (today counts only).
- Guest pairing limits + rate limits.
- `ColosseumQueueScreen.OnDestroy`: server-side cancel, not just local token.
- Move PvP off `Combat2_TestHarness` to a dedicated bootstrap.
- Remove duplicate `Supabase/supabase/functions/_shared` and nested `Supabase/supabase/supabase/.temp`.

**Matrix pre-flight**

1. Same working tree for both builds; same `PvpBuildStamp` + app version logged on both.
2. Run `CasualPvpSpineTests`, `CasualPvpDeterminismTests`, and `deno test` (`_shared/pvp_matchmaking_test.ts`).
3. First run with low-swap parties.
4. Start both searches within ~120 s.
5. Capture `[PvPTurn] state` + `[PvPMatch]` logs on both sides (adb logcat + Editor log); diff digests turn by turn.
6. Don't background either app during the result window.
7. Afterwards: `SELECT` from `pvp_matches` / `pvp_match_turns` (state, hashes, dispute reason).

**Order:** phone-vs-phone → M0.3 fixes → Editor-vs-phone + 20 honest matches with zero mismatches.

## 3. Package correction

PVP_01's "NGO 1.8.x + Lobby/Relay" is legacy on Unity 6. Use **Netcode for GameObjects 2.x + `com.unity.services.multiplayer` (Sessions)**. The separate `com.unity.services.lobby` / `.relay` / `.matchmaker` packages are deprecated in favor of the Multiplayer Services SDK. NGO 1.x is not supported on Unity 6000.3+.

Current state (audit 2026-10-08):

| Item | Value |
|------|-------|
| Unity | 6000.0.24f1 |
| `Packages/manifest.json` | `com.unity.netcode.gameobjects` 1.8.1, `com.unity.multiplayer.center` 1.0.0; lock adds `com.unity.transport` 2.3.0 |
| UGS packages | None (no multiplayer / lobby / relay / authentication) |
| Netcode usage in scripts | None; no SocialLobby scene or scripts |
| Cloud link | `cloudProjectId` set (org `cronustitannft`), but `cloudEnabled: 0` and all `UnityConnectSettings` services disabled |
| Android | IL2CPP, ARM64 |
| Assemblies | Combat.Runtime does not reference MenuNavigation (keep it so); hub code goes in Assembly-CSharp per PVP_01 §5 |

## 4. H0 implementation brief (copy for agents)

```text
Chainbound Social Hub H0: Presence Foundations. Own branch (hub/h0).
Devnet + Unity dev environment only. Presence only: no battles over
NGO, no chat, no trading, no economy. Must not touch the PvP battle path.

GOAL
Players walk into a SocialLobby scene, see up to 8 other players moving
around in real time, walk to a Colosseum object that opens the existing
casual queue, leave the hub cleanly for the battle, and rejoin afterwards.
Multiplayer is reachable ONLY through Ferryman NPC -> SocialLobby ->
Colosseum object. No player-menu path in player builds.

READ FIRST
- PVP_00 §1 (M2 gate), PVP_01 §3.1 (update package names as below)
- PlayerMenuController.OpenToTab, PlayerMenuTab_colosseum.asset,
  ChainlingPcTerminalInteractable, CasualPvpMatchFlow (scene return),
  SceneLoader, ColosseumQueueScreen

BUILD ORDER
A. Packages + services
  1. Upgrade com.unity.netcode.gameobjects 1.8.1 -> current 2.x; add
     com.unity.services.multiplayer (Sessions). No separate Lobby/Relay
  2. Unity dashboard: enable services, dev environment, Lobby + Relay
B. Scene shell (works offline first)
  3. SocialLobby scene: spawn points, build-settings entry, lanterns/
     docks feel (alive Loomville, not empty)
  4. ColosseumInteractable modeled on ChainlingPcTerminalInteractable
     -> opens the existing casual queue screen; queue code untouched
  4b. FerrymanNpc: if multiplayerUnlocked=false, show a "not sailing yet"
      line; if true, travel to SocialLobby and stamp hub_unlocked on the
      profile server-side
  4c. Hide the Colosseum player-menu tab in player builds; keep it only
      behind a dev-only flag (DEVELOPMENT_BUILD/Editor) for matrix testing
C. Identity
  5. UGS anonymous sign-in after the Supabase session exists
  6. Edge function hub_link_identity: stores supabase_user_id <->
     ugs_player_id in a service-role-only table (RLS on, no client grants)
  6b. pvp_enqueue rejects unless server multiplayerUnlocked=true AND
      profile hub_unlocked=true (dev-flag profiles exempt on Devnet only)
  7. Display name + cosmetics resolved by server catalog ID from Supabase;
     never trust peer-sent names, ownership, or cosmetics
D. Presence sync
  8. Avatar prefab: NetworkObject, owner-driven NetworkTransform (position
     + facing, ~10 Hz, compressed), anim state as a NetworkVariable
  9. No gameplay state, inventory, or RAM over NGO, ever
E. Shards
  10. Sessions quick-join, max 8 (raise to 16 later), filtered by
      PvpBuildStamp + app version
  11. Host leaves or backgrounds -> everyone falls back to solo hub and
      re-quick-joins; no host migration in H0
  12. Leave the session before the battle scene loads; rejoin on return.
      NGO scene management OFF so it never fights SceneLoader
F. Lifecycle
  13. OnApplicationPause/Quit -> leave session; resume -> re-quick-join
  14. Offline/UGS failure -> solo hub still works, Colosseum still opens

VERIFY
  15. Editor + phone in one shard: see each other move, names correct
  16. Host phone backgrounds -> other client recovers to solo + rejoins
  17. Enter casual queue from the Colosseum object -> battle -> return
      to hub -> back in a shard
  18. Logcat bandwidth check per client at 8 players
  19. PvE and PvP regressions: none (battle path files unchanged in diff)
  20. Player build: no Colosseum tab in the menu; enqueue from a non-hub
      profile is rejected by the server

SAFETY FALLBACKS
- Kill switch: server multiplayerUnlocked=false -> Ferryman stays docked,
  Colosseum shows closed, enqueue rejected. Hub off or network failure ->
  Colosseum closed, never a soft-lock; single-player unaffected
- Any network failure degrades to solo hub, never a soft-lock
- Peer data is display-only and catalog-validated
- Relay free tier: watch average concurrent users and GiB in the dashboard
- Not merged to main / not in player builds until the M2 gate

OUT OF SCOPE
Chat, emotes beyond animation state, trading, parties, spectating,
Distributed Authority, host migration, 16+ shards, Mainnet Smoke.

DONE WHEN
Steps 15-20 pass on Devnet, and the hub branch diff touches no
Combat.Runtime or PvP edge files.
```

## 5. Risks & costs

| Risk | Note |
|------|------|
| NGO 1.x legacy on Unity 6 | Upgrade to 2.x before writing any lobby code (nothing references NGO today) |
| Phone-as-host | Host fans out to every peer (bandwidth); a host phone that backgrounds kills the shard. H0 answer: fall back to solo + re-quick-join |
| Identity mapping | Anonymous UGS player ID changes on reinstall; guest → wallet upgrade must keep the Supabase ↔ UGS mapping; UGS ID ≠ Supabase profile ID |
| Relay / Lobby cost | Checked 2026-10-08, verify before launch: Relay free tier 50 average monthly CCU + 3 GiB per CCU (150 GiB/month cap); $0.16 per extra average CCU; bandwidth $0.09/GiB US/EU, $0.16/GiB Asia/Australia. Lobby 10 GiB/month free per region group (heartbeats/queries drive it). Distributed Authority billed separately |
| Timing | Presence only. Ships after the M2 gate: battle streak green (20 honest matches, zero mismatches) + M1 challenge codes / rematch |
