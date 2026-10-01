> **Parked for review.** Yet to implement as a mode. Client wager types exist under `Assets/Scripts/Combat/Runtime/PvP/` and are not a playable ranked flow. No escrow program. The closed-beta client policy does not expose wager services. Review list: [../README.md](../README.md).

# PvP Phase 2 — Ranked Escrow & Competitive Ladder

**Status:** Spec / implementation guide (pre-build)  
**Audience:** Implementing agents, Unity engineers, Anchor/backend engineers, auditors  
**Depends on:** Phase 1 dual-sim + matchmaking + forensic hash agreement pipeline  
**Related:** [`PVP_01_Social_Lobby_And_Casual_Matchmaking.md`](PVP_01_Social_Lobby_And_Casual_Matchmaking.md), [`PVP_03_Tournaments_And_Themed_Brackets.md`](PVP_03_Tournaments_And_Themed_Brackets.md), [`COMPETITIVE_LADDER_AND_SEASONS_v1.md`](../Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md), [`LEVER_BOARDS_v1.md`](../../Specs/Combat/LEVER_BOARDS_v1.md), solana-unity skill, Solana MCP (mandatory for chain work)

---

## 0. Purpose

Add **economic stakes** and **competitive integrity** on top of Phase 1:

1. Wallet-gated ranked queue (MWA / Seeker).
2. On-chain **match escrow** for wagered **Runes (Metaplex Core)** + small **SOL**, with protocol fee to treasury.
3. Server referee settlement bound to match id + forensic outcome.
4. Disconnect/forfeit still pays the winner (escrow is the anti-ragequit deterrent).
5. Wire `FavorService.SetRankedLock`, `RankedLoadoutValidator`, and ladder metrics per competitive ladder groundwork.
6. Lever boards for wager + matchmaking extended; server-authoritative `get_pvp_config`.

**Non-goals for Phase 2:** Tournament brackets (Phase 3), player-to-player free trade, Inscription/cNFT escrow (deferred), mainnet without audit.

---

## 1. Trust model (read first)

| Layer | Trust |
|-------|-------|
| Unity client | **Untrusted** for ownership, legality beyond UX, and settle authority |
| Supabase edge (JWT + service role) | Trusted for matchmaking, turn relay, dispute re-sim trigger, **referee signing** |
| Anchor escrow program | Trusted for custody + settle rules; **cannot** know who “really won” without oracle input |
| Result oracle | Server keypair signs `(match_id, winner, forensic_hash)` — **single point of failure** mitigated by timeouts + key rotation + optional dual attestation |

**Product stance:** Off-chain combat + on-chain custody is intentional. Fairness comes from dual-sim + dispute re-run + config stamps, not from putting the full combat engine on-chain.

---

## 2. What already exists

| Piece | Location |
|-------|----------|
| Escrow interface + stub | `IEscrowWagerService`, `StubEscrowWagerService` — TODOs `initialize_escrow`, `deposit_inscription`, `forfeit_escrow`, `settle_escrow` |
| State machine | `WagerMatchOrchestrator` — Idle → CreateMatch → AwaitDeposits → Locked → InProgress → VictoryDeclared → WinnerSelecting → Settled + local persistence/resume |
| Winner pick UI | `PvPMoveSelectController`, `WinnerSelectionAPI` |
| Ranked loadout | `RankedLoadoutValidator` (no Ordinals; Rune caps via `MAX_RUNES_PER_CHAINLING_RANKED`) |
| Favor ranked lock | `FavorService.SetRankedLock` — **no callers yet** |
| Anchor game program | `backend/chainbound_game` — **no escrow instructions**; vaults exist for moves/chainlings but are not wager escrow |
| Ladder groundwork | `Docs/review-unimplemented/Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md` |
| Config stamps | Resolver `ConfigStamp`, battle outcome `config_hash` |

**Note on product semantics:** Current stub models “winner picks one opponent inscription.” Phase 2 ranked may also support **SOL + NFT pool winner-takes-wager** (both stakes minus fee). Spec both paths; implement the path product locks before coding. Default recommendation for v1 ranked: **SOL wager + optional Core Rune stakes**, settle = winner receives SOL pool − fee + chosen/all NFT stakes per match rules encoded on escrow PDA.

---

## 3. Lever boards

### 3.1 WagerConfigSO (new — money-critical)

| Field | Intent |
|-------|--------|
| `minSolLamports` / `maxSolLamports` | Wager bounds |
| `allowedAssetClasses` | Enum flags: `RunesCore` only in v1 |
| `maxNftsPerSide` | e.g. 1–3 |
| `protocolFeeBps` | Fee to treasury (checked math) |
| `treasuryPubkey` | Display/config mirror of on-chain `EscrowConfig` |
| `depositDeadlineSeconds` | Opponent must deposit by T |
| `resultDeadlineSeconds` | Server must settle by T or mutual refund |
| `disputeWindowSeconds` | Optional hold before settle finalize |
| `requireFreshWalletProof` | true |
| `configVersion` / hash | Stamped into match + settle |

**Ownership rule:** Fee bps and deadlines live **only** on `WagerConfigSO` / on-chain `EscrowConfig`. Do not duplicate into `MatchmakingConfigSO` or `PostBattleRewardsConfigSO`.

**Server mirror (mandatory):**

- `Supabase/functions/_shared/pvp_wager_config.ts`
- Extend `get_pvp_config` to return `{ matchmaking, wager, hashes }`
- Unity applies override via `BalanceConfigService` (same pattern as vending)
- **Refuse ranked enqueue** if client hash ≠ server hash when server reachable

### 3.2 MatchmakingConfigSO (extend Phase 1)

Enable `rankedEnabled`, force `requireWalletForRanked=true`, rating band widen parameters active.

### 3.3 CombatRulesetSO

Use `Ruleset_PvP_Ranked.asset`. Pin its hash at match lock. Ranked must call `FavorService.SetRankedLock(true)` at battle start and clear on exit.

---

## 4. Anchor escrow program — structure

### 4.1 Solana MCP gate (agents)

Before writing or changing **any** escrow Rust:

1. `list_sections` on Solana MCP.
2. Pull canonical sources: escrow pattern (`gh-anchor-escrow-2024` / Chainstack escrow guide), Metaplex Core CPI (`metaplex-docs` / `gh-mpl-core`), Sealevel attacks (`gh-sealevel-attacks`), Solana Mobile MWA (`solana-mobile-docs`).
3. After each Rust edit pass: run MCP `program_autofixer` until `require_another_tool_call_after_fixing` is false.
4. Follow project `solana-unity` / `solana-dev` skills: Unity never trusted for ownership; show full tx UX before MWA sign.

### 4.2 Accounts

**`EscrowConfig` PDA** — seeds `["escrow_config"]` (or with program authority):

- `authority` (admin / Squads multisig later)
- `result_authority` (server referee pubkey — rotatable)
- `treasury`
- `fee_bps`
- `allowed_core_collection` (trusted Runes collection address)
- `paused` bool

**`MatchEscrow` PDA** — seeds `["match_escrow", match_id]`:

- `match_id` (16–32 bytes)
- `player_a`, `player_b`
- `wager_lamports_each`
- `asset_a[]`, `asset_b[]` (Core asset pubkeys, fixed max)
- `state`: `Created | ADeposited | BDeposited/Locked | Settled | Refunded | Forfeited`
- `deposit_deadline_slot` or unix timestamp
- `result_deadline`
- `winner` (optional until settle)
- `forensic_hash` (bytes stored at settle for public audit)
- `bump`

SOL sits as lamports on the `MatchEscrow` account (or a dedicated vault PDA — pick one pattern and stick to it). Core assets: transfer ownership to the escrow PDA via **mpl-core `TransferV1` CPI** (`TransferV1CpiBuilder`).

### 4.3 Instructions

| Instruction | Who signs | Behavior |
|-------------|-----------|----------|
| `initialize_config` | admin | Once / upgrade path |
| `update_config` | admin | Rotate `result_authority`, fee, pause |
| `initialize_escrow` | player A (or server-paid create + both later) | Create PDA; record terms; optional A deposit in same tx |
| `deposit` | player | Transfer SOL + Core assets; verify collection; advance state; on both deposited → `Locked` |
| `refund_deposit_timeout` | either depositor (permissionless after deadline) | If opponent never deposited → return assets |
| `settle` | `result_authority` (+ optional winner) | Verify state `Locked`/`Forfeited`; pay winner SOL − fee; fee to treasury; transfer NFTs per rules; set `Settled`; store forensic hash; **one-shot** |
| `forfeit_settle` | `result_authority` | Same as settle with forfeit reason code |
| `refund_result_timeout` | either player (permissionless after result deadline) | Mutual refund if no settle — **prevents stranded funds if oracle dies** |

### 4.4 Collection / fake-NFT guardrails

On every NFT deposit:

1. Account owner must be Metaplex Core program.
2. Asset `owner` must be depositor before transfer.
3. Asset must belong to `allowed_core_collection` (and expected update authority policy as configured).
4. Reject frozen / non-transferable plugins (`canTransfer` semantics).
5. **Do not accept Bubblegum cNFTs in v1** — proof paths + DAS dependency are a separate project (see Bubblegum vault examples only as future reference).

### 4.5 Critical vulnerabilities & mitigations

Drawn from Sealevel attacks + escrow production patterns (re-verify with MCP/`program_autofixer`):

| Attack / bug | Mitigation |
|--------------|------------|
| Missing signer checks | `Signer` on depositor; `result_authority` must sign settle; never trust account data alone |
| Account confusion / wrong escrow | PDA seeds include `match_id`; `has_one` style constraints on players |
| Double settle | State enum check **before** transfers; transition to `Settled` before CPI returns; reject if already terminal |
| Replay settle across matches | Signature payload / ix args bind `match_id` + winner + hash; authority cannot reuse message |
| Fake NFT | Collection + Core program id checks |
| Fee overflow / underpay | Checked arithmetic; fee = `total * bps / 10_000`; remainder to winner |
| Close account before transfers | Transfers first; close last; rent to safer party |
| Oracle compromise | Pause switch; rotate `result_authority` via multisig; permissionless result-timeout refund; optional dual-player attestation path later |
| Deposit grief (lock opponent forever) | Deposit deadline refund |
| Server outage | Result deadline mutual refund |
| Client shows wrong tx | MWA UX must show cluster, program id, match id, assets, SOL, fee before sign (solana-unity skill) |

Reference study: [coral-xyz/sealevel-attacks](https://github.com/coral-xyz/sealevel-attacks), Chainstack escrow guide, Metaplex Core CPI transfer docs.

### 4.6 Testing before mainnet

- Anchor/LiteSVM/Mollusk unit tests for every state transition.
- Adversarial tests: wrong collection, double settle, wrong authority, expired refund, fee edge (0 bps, max bps).
- Devnet soak with real MWA on Seeker.
- **External audit** before mainnet escrow with real user assets.
- Upgrade authority → Squads multisig when productionizing.

---

## 5. Unity / Supabase wiring

### 5.1 Replace stub

Implement `AnchorEscrowWagerService : IEscrowWagerService` in `Assets/Scripts/Solana/**` (Combat.Runtime asmref):

- Build txs via project Solana client (not reintroduced Unity SDK).
- Sign via `SolanaWalletService` / MWA on device; DesktopWalletBridge in Editor.
- Map orchestrator states to chain confirms; keep `WagerMatchOrchestrator` resume (pending tx signatures).

### 5.2 Ranked match flow

```text
Enqueue ranked (wallet proof)
  → Match found (parties locked server-side)
  → initialize_escrow + both deposit (MWA)
  → Server waits for Locked on-chain
  → Reveal opponent party + start battle (Phase 1 dual-sim)
  → Turns / forfeit timers
  → forensicHash agree (or dispute re-sim)
  → Server signs settle → settle ix
  → Winner selection UI if NFT pick rules apply
  → Rating update (ladder)
```

**Hard rule:** Do not start ranked combat until escrow is `Locked` on-chain (confirmed commitment). Do not reveal full opponent loadout before lock if that enables last-second swaps (server already locked snapshots — still hide from client UX until Locked).

### 5.3 Edge functions (add)

| Function | Role |
|----------|------|
| `get_pvp_config` | Authoritative matchmaking + wager configs + hashes |
| `pvp_ranked_enqueue` | Wallet link required; loadout validate; rating band |
| `pvp_create_escrow_intent` | Returns unsigned ix accounts / match_id terms |
| `pvp_confirm_deposit` | Index signature; verify on-chain state |
| `pvp_referee_settle` | After hash agreement / forfeit; sign & submit settle (or return settle tx for crank) |
| `pvp_dispute_resolve` | Headless re-sim; then settle or refund policy |

Store settle `forensic_hash` on-chain and in Supabase battle outcome (already has `forensic_hash` / `config_hash` columns path).

### 5.4 Ladder integration

Follow [`COMPETITIVE_LADDER_AND_SEASONS_v1.md`](../Specs/Combat/COMPETITIVE_LADDER_AND_SEASONS_v1.md):

- Only `queue_type=ranked` + eligible ruleset increments seasonal rating.
- Soft-launch gates (population) before showing ranks.
- PvE does not move ranked rating.
- Wire `SetRankedLock(true)` for favor zeroing during ranked battles.

---

## 6. MWA / Seeker UX requirements

Per Solana Mobile docs + project wallet guide:

1. One clear deposit transaction (or minimal ix count) showing: cluster, program, match id, SOL amount, NFT names/ids, fee bps, deadlines.
2. Never ask for private keys; Seed Vault / MWA only.
3. Handle app background mid-deposit via orchestrator persistence + re-confirm signature status.
4. Simulation result before sign when available.
5. Failure paths: user reject, insufficient SOL, NFT frozen → revert queue with clear UI.

---

## 7. Assembly & placement

| Code | Folder | Assembly |
|------|--------|----------|
| Anchor client builders | `Assets/Scripts/Solana/**` | Combat.Runtime |
| `AnchorEscrowWagerService` | `Solana/Services` or `Combat/Runtime/Services` implementing interface | Combat.Runtime |
| `WagerConfigSO` | `Combat/Runtime/ScriptableObjects` | Combat.Runtime |
| Assets | `Assets/Data/Solana/**` and/or `Assets/Data/Combat/Config/**` | — |
| Program Rust | `backend/` (extend `chainbound_game` **or** dedicated `match_escrow` program — prefer dedicated for blast radius) | — |

Update `Docs/Specs/Solana/CHAINBOUND_PROGRAM_REGISTRY.md` when program id ships. Update `AssemblyModules.md` only if asmdef references change.

---

## 8. Guardrails checklist (ship gate)

- [ ] `program_autofixer` clean on final Rust
- [ ] Sealevel-attack checklist reviewed (signer, owner, PDA bump, account confusion)
- [ ] Pause + authority rotation tested
- [ ] Deposit timeout refund tested
- [ ] Result timeout mutual refund tested (oracle down)
- [ ] Double-settle test fails closed
- [ ] Wrong collection deposit fails
- [ ] Client/server wager config hash mismatch blocks queue
- [ ] Combat never starts before on-chain Locked
- [ ] Ranked disconnect → forfeit settle to winner
- [ ] Audit report accepted for mainnet
- [ ] Feature flag: ranked off by default until soak metrics OK

---

## 9. Implementation notes for agents

1. **Do not** implement escrow Rust from training memory — MCP first.
2. Prefer a **dedicated escrow program** over stuffing into `chainbound_game` (isolation, audit surface).
3. Keep `IEscrowWagerService` as the only combat-facing API; swap stub → Anchor impl via injection.
4. Reuse Phase 1 turn relay unchanged; escrow is orthogonal custody.
5. Log `[PvPEscrow]` with match id + signatures never private keys.
6. Devnet only until audit; configure cluster in `SolanaConfigSO`.
7. Winner-selection vs winner-takes-all: confirm product decision and encode in escrow state machine before UI work.
8. Update lever board docs + economy docs if fee/revenue rules change.

---

## 10. Dev / Editor configuration

| Item | Notes |
|------|-------|
| `WagerConfig_Default.asset` | Assign on `BalanceConfigService`; fee bps visible in inspector with OnValidate clamps |
| Escrow program id | `SolanaConfigSO` or dedicated `EscrowProgramConfigSO` under `Assets/Data/Solana/` |
| Referee key | Supabase secret only — never embed in Unity |
| Editor deposits | DesktopWalletBridge + localnet/devnet airdrop; document in wallet setup |
| Test wallets | Two seeded wallets with Core test Runes in allowed collection |
| Tools menu | Optional: `Tools/Chainbound/PvP/Validate Wager Config` extending BalanceBoardValidator |
| Staging flag | `rankedEnabled=false` in production builds until go-live |

---

## 11. Acceptance criteria

- [ ] Ranked queue rejects non-wallet / stale proof profiles.
- [ ] Both players deposit Core + SOL; escrow reaches Locked; battle starts.
- [ ] Winner receives funds minus fee; treasury receives fee; loser assets returned per rules.
- [ ] Forfeit path settles without requiring loser online.
- [ ] Oracle downtime past deadline → mutual refund permissionlessly.
- [ ] Hash mismatch → dispute path; no silent settle.
- [ ] Rating updates only for completed ranked matches.
- [ ] `SetRankedLock` active during ranked combat.

---

## 12. Out of scope

- Tournaments / multi-depositor pools → Phase 3  
- cNFT / Inscription escrow → future  
- Trustless on-chain combat VM → not planned  
- Open trading marketplace → future (may reuse escrow PDA patterns)  
