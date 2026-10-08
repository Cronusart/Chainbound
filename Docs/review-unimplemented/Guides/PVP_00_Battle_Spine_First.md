> **Parked for review.** Shipping-order addendum to PVP_01–03. Not implemented. Review list: [../README.md](../README.md).

# PvP Phase 0 — Battle Spine First (Shipping Order + M0 Slice)

**Status:** Spec / shipping order (pre-build)  
**Audience:** Cronus, implementing agents  
**Depends on:** Existing combat determinism (`CombatResolver`, `ConfigStamp`, `forensicHash`), wallet session (`AccountSessionService`), Supabase edge pattern (`SupabaseClient.PostJsonAsync`)  
**Does not include:** NGO social lobby, ranked escrow, tournaments  
**Related:** [`PVP_01_Social_Lobby_And_Casual_Matchmaking.md`](PVP_01_Social_Lobby_And_Casual_Matchmaking.md), [`PVP_02_Ranked_Escrow_And_Ladder.md`](PVP_02_Ranked_Escrow_And_Ladder.md), [`PVP_03_Tournaments_And_Themed_Brackets.md`](PVP_03_Tournaments_And_Themed_Brackets.md), [`COMPETITIVE_LADDER_AND_SEASONS_v1.md`](../Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md)

---

## 0. Why this document exists

PVP_01 packages **social lobby (NGO + UGS Relay)** and **casual matchmaking** as one phase. For a solo Seeker ship, that couples a real-time presence stack to the only capability that proves multiplayer: **two clients finishing a fair dual-sim battle**.

This doc **does not replace** PVP_01–03 architecture. It **reorders shipping**:

| Ship | Name | Ships when |
|------|------|------------|
| **M0** | Battle spine | First — this doc |
| **M1** | Casual social without plaza | After M0 happy-path + dispute path work |
| **M2** | NGO social lobby (PVP_01 plaza) | After players reopen for battles, not before |
| **M3** | Ranked escrow + ladder (PVP_02) | After casual sticky + near-zero forensic mismatches |
| **M4** | Tournaments / themed brackets (PVP_03) | Last — pool-only Approach A |

**Hard rule (unchanged from PVP_01):** Do **not** run combat state sync over NGO. Do **not** trust either client as battle host for legality or outcomes.

---

## 1. Shipping order (detail)

### M0 — Battle spine (this slice)

Menu/Colosseum **button** → casual queue → match found → lock parties → Realtime turns → both clients run `CombatResolver` → dual `forensicHash` → complete or dispute. **No SOL, no NFT deposits, no rating, no NGO plaza.**

### M1 — Lightweight social

Friend / challenge codes, rematch, optional “looking for match” list. Same `IMatchmakingService`; still no plaza.

### M2 — Social lobby (PVP_01 remainder)

`SocialLobby` scene, NGO 2.x + `com.unity.services.multiplayer` (Sessions), shard size 8 for H0, colosseum interactable. Presence only.

### M3 — Ranked + escrow (PVP_02)

Wallet-gated ranked queue, dedicated Anchor escrow program, referee settle, ladder metrics. Stakes = **small SOL ± optional Core Runes** — never a tradable soft-currency prize loop.

### M4 — Tournaments (PVP_03)

Shared tournament pool PDA, Supabase brackets, themes via `CombatRulesetSO` + eligibility predicates. **Pool-only** per-match (Approach A).

---

## 2. Existing architecture to reuse (do not rewrite)

| Piece | Path / note | M0 use |
|-------|-------------|--------|
| NGO package | `com.unity.netcode.gameobjects` **2.13.2** and `com.unity.services.multiplayer` **2.3.3** (Sessions) in `Packages/manifest.json`; Unity **6000.0.24f1** | **Leave unused** in M0. H0 presence uses these packages with shard size 8 |
| `CombatResolver` + seed | `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs` | Dual local sim with server seed |
| `ConfigStamp` / ruleset hash | Resolver + `CombatForensicLogModels.ConfigStampPayload` | Pin at match lock; reject enqueue on mismatch |
| `forensicHash` on result | `CombatBattleController` → `BattleResult.forensicHash`; `BattleOutcomeBundle`; `SupabaseBattleOutcomePersistence` | Both clients submit; server compares |
| `BattleType.PvP` | `BattleSnapshots.cs` | Tag PvP outcomes |
| `PvPMoveSelectController` | `Presentation/PvPMoveSelectController.cs` | Winner-pick UI later (M3); unused for M0 settle |
| `SetWagerOrchestrator` | `CombatBattleController` | **Do not require** in M0; inject only when escrow exists |
| `WagerMatchOrchestrator` + `IEscrowWagerService` / `StubEscrowWagerService` | `Combat/Runtime/PvP/**`, `Services/**` | Keep stubbed; M3 replaces stub |
| `RankedLoadoutValidator` | `Data/RankedLoadoutValidator.cs` | M3; optional dry-run validation in M0 staging |
| `FavorService.SetRankedLock` | `FavorService.cs` — **no callers yet** | Wire in M3 ranked only |
| Wallet gates | `AccountSessionService.isWalletConnected`, `HasFreshWalletProof`, MWA | M0 casual: wallet **optional** (`requireWalletForCasual=false`) |
| Persistence pattern | Edge via `SupabaseClient.PostJsonAsync` only | All queue/turn/result writes |
| Scene load | `SceneLoader` | Load PvP battle scene from match payload |
| Rulesets | `CombatRulesetSO` + create `Ruleset_PvP_Casual.asset` | Pin `rulesetId` + hash |
| Lever boards | Pattern from `LEVER_BOARDS_v1.md` / `BalanceConfigService` | New `MatchmakingConfigSO` |
| Closed-beta policy | Do not expose wager / ownership flags | M0 must not enable escrow player flags |

**Assembly:** Matchmaking interfaces that combat uses → `Chainbound.Combat.Runtime`. Lobby NGO later → Assembly-CSharp / Core/UI. No new `.asmdef` unless asked. Combat.Runtime must not reference MenuNavigation.

---

## 3. Anti-cheat & exploit guardrails (foundations in M0)

M0 has **no economic stake**, but every guardrail below is the foundation M3/M4 will inherit. Build them now so ranked does not invent a second trust model.

### 3.1 Trust model (M0)

| Layer | Trust |
|-------|-------|
| Unity client | **Untrusted** for parties, legality beyond UX, and outcomes |
| Supabase edge (JWT + service role) | Trusted for queue, lock, turn append, hash compare, forfeit |
| Peer NGO / Realtime client messages | **Untrusted** — clients must not write match rows directly |

### 3.2 Concrete guardrails (implement in M0)

| Threat | Guardrail |
|--------|-----------|
| Host / client authority cheat | No client-hosted battle SoT; server turn log is SoT; both clients only *simulate* |
| Loadout swap mid-queue / mid-match | `pvp_lock_match` freezes **server-authored** party snapshots from profile/lineup; ignore client re-sends of party |
| Fake stronger stats / illegal moves | Server validates each turn: move ∈ locked `moveIds`, target legal, AP affordable, within `turnActionTimeLimitSeconds` |
| Config / balance tampering | Pin `configHash` + `rulesetId` + `app_version` (+ resolver build stamp if available); reject enqueue if local ≠ `get_pvp_config` |
| Determinism drift / silent desync | Both submit `forensicHash`; mismatch → `disputed` + store full turn log for offline re-sim; **never** auto-pick a winner |
| Action injection / spoofed Realtime | Only edge functions mutate `pvp_matches` / turn log; channel is notify/subscribe after server write |
| Queue spoof / multi-queue | One active queue row per `profile_id`; JWT must match profile; expire via `expires_at` |
| Disconnect / stall grief | Missed turns ≥ `turnForfeitMissedTurns` → server forfeit; cancel queue on lobby/menu teardown |
| Version skew exploits | Reject pair if `app_version` / config hash incompatible (`maxAppVersionSkew`) |
| Cosmetic / ownership spoof (display) | Display uses server catalog IDs when available; never trust peer-sent ownership for eligibility |
| Replay as authority | Disputed matches quarantine; offline worker reuses resolver; untrusted client replay is evidence only |
| Wallet spoof for social | Casual does not grant ownership or rating; M3 requires fresh wallet proof |

### 3.3 Telemetry required before M3

Store per match: `match_id`, both profile ids, seed, config hash, app versions, ordered turn log, both forensic hashes, final state (`completed` / `disputed` / `forfeit_*`).  
**Ship gate for ranked:** internal mismatch rate near-zero on completed casual matches; dispute tooling can re-sim from turn log.

### 3.4 Explicit non-goals for M0 anti-cheat

- No kernel anti-cheat, no device attestation beyond existing session
- No on-chain combat VM
- No commit–reveal move crypto (optional later research; not needed for casual)
- No economic slash — forfeit is match state only

---

## 4. M0 deliverables (summary)

### 4.1 Lever board — `MatchmakingConfigSO`

Fields (Phase 1 subset): `casualEnabled=true`, `rankedEnabled=false`, `requireWalletForCasual=false`, `queueTimeoutSeconds`, `turnForfeitMissedTurns`, `maxAppVersionSkew`, `defaultCasualRulesetId`, `rematchCooldownSeconds` (even if rematch is M1).  
`ComputeConfigHash()`, `OnValidate()`, register on `BalanceConfigService`. Server mirror: `Supabase/functions/_shared/matchmaking_config.ts` + `get_pvp_config`.

### 4.2 Supabase

**Tables:** `matchmaking_queue`, `pvp_matches`, turn log (table or jsonb append), dispute/telemetry store.  
**Edge:** `pvp_enqueue`, `pvp_cancel_queue`, `pvp_matchmaker` (cron/trigger), `pvp_lock_match`, `pvp_submit_turn`, `pvp_submit_result`.  
**Realtime:** `pvp_match:{match_id}` — subscribe only; writes via edge.

### 4.3 Unity

- `IMatchmakingService` + Supabase implementation  
- Casual queue UI (menu button — **not** NGO plaza)  
- PvP battle bootstrap: inject seed + locked parties into combat **without** requiring `WagerMatchOrchestrator`  
- Wire action commit → `pvp_submit_turn` → feed opponent action into local resolver  
- Dual `forensicHash` → `pvp_submit_result`  
- Logs: `[PvPMatch]`, `[PvPTurn]` — never keys/secrets  

### 4.4 Acceptance (M0)

- [ ] Two devices complete a casual match with matching `forensicHash`
- [ ] Forced action desync → `disputed` + stored turn log
- [ ] Missed turns → clean forfeit
- [ ] Config hash mismatch blocks queue when server reachable
- [ ] Loadout change after lock ignored
- [ ] No escrow / SOL / NFT / rating movement
- [ ] NGO lobby still unused

---

## 5. Summarized implementation prompt (copy for agents)

Use this as the task brief for the first build slice:

```text
Implement Chainbound PvP M0 — Battle Spine Only (no NGO lobby, no escrow, no ranked).

Read first:
- Docs/review-unimplemented/Guides/PVP_00_Battle_Spine_First.md (this shipping order)
- Docs/review-unimplemented/Guides/PVP_01_Social_Lobby_And_Casual_Matchmaking.md §§1, 3.2–3.5, 6–9
  (reuse matchmaking/dual-sim/edge design; SKIP §3.1 NGO lobby scene)
- LEVER_BOARDS_v1.md + AssemblyModules.md + systems-first skill
- Existing: CombatResolver, ConfigStamp, forensicHash paths on CombatBattleController /
  SupabaseBattleOutcomePersistence; BattleType.PvP; AccountSessionService wallet flags

Build:
1. MatchmakingConfigSO + BalanceConfigService registration + server _shared mirror + get_pvp_config
2. Supabase migrations: matchmaking_queue, pvp_matches, turn log, dispute fields
3. Edge functions: pvp_enqueue, pvp_cancel_queue, pvp_matchmaker, pvp_lock_match,
   pvp_submit_turn, pvp_submit_result — JWT auth; service-role writes only
4. Unity IMatchmakingService → enqueue/cancel/OnMatchFound
5. Menu/Colosseum button UI for casual queue (NOT SocialLobby NGO scene)
6. PvP battle bootstrap: server seed + server-locked party snapshots into combat;
   do NOT require WagerMatchOrchestrator / IEscrowWagerService for M0
7. Each turn: UI commit → pvp_submit_turn → server cheap legality vs locked loadout →
   broadcast → both clients step CombatResolver with same action
8. End: both submit forensicHash; equal → completed; else disputed + persist turn log
9. Forfeit on missed turns per MatchmakingConfigSO
10. Refuse enqueue if local config hash ≠ server when reachable
11. Edit-mode / Deno tests: config hash, turn validation, matchmaker pairing
12. Manual smoke: two clients (Editor+device or two devices), happy path + forced desync

Anti-cheat foundations (must ship in M0):
- Server SoT for parties and turn log; no client-direct match writes
- Lock parties at match create from DB; ignore client party re-sends
- Pin rulesetId + configHash + app_version; version skew reject
- Dual forensicHash; never silent-settle mismatches
- One queue row per profile; queue expiry; teardown cancels queue

Out of scope for this slice:
- NGO / UGS Relay / SocialLobby scene
- Ranked queue, rating, FavorService.SetRankedLock callers
- Anchor escrow / replacing StubEscrowWagerService
- Tournaments, chat, spectating, parties >1v1
- Enabling ClosedBeta wager / ownership player flags
- Mainnet anything

Success = acceptance checklist in PVP_00 §4.4 all green on Devnet/staging.
```

---

## 6. Cross-links for later phases

| When starting… | Still obey |
|----------------|------------|
| M2 lobby | PVP_01 §3.1; still never sync combat over NGO |
| M2 hub foundations | [PVP_04](PVP_04_Social_Hub_H0.md); H0 may start on its own branch in parallel with matrix testing; ships only after M2 gate |
| M3 escrow | PVP_02; Solana MCP + `program_autofixer`; dedicated escrow program; combat starts only after on-chain `Locked` |
| M4 tournaments | PVP_03 Approach A; eligibility off-chain; money on-chain |

**Product stance for stakes (M3+):** Escrow is anti-ragequit + optional spice. Do not fund seasons with tradable soft-currency recirculation. MRR stays cosmetics/consumables/ownership desire — not injection events.

---

## 7. Dev / Editor notes (M0)

| Item | Notes |
|------|-------|
| Feature flags | `casualEnabled=true`, `rankedEnabled=false` |
| Ruleset | `Ruleset_PvP_Casual.asset` under `Assets/Data/Combat/Config/` |
| Scene | Reuse/adapt existing combat scene bootstrap; no SocialLobby in build for M0 |
| Supabase | Deploy functions; Realtime for match channels; Devnet project first |
| Testing | Two Editor instances or Editor + Android/Seeker |
| Harnesses | Disposable mocks only; no harnesses left in production scenes |