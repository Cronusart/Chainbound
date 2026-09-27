# Closed Beta Feature Allowlist

**Status:** internal product/engineering contract (first slice proposed)  
**Date:** 2026-09-12  
**Cluster:** devnet until a later doc explicitly approves mainnet  
**Companion:** [`CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md`](../Specs/Solana/CHAINBOUND_SOLANA_BETA_BUILD_SEQUENCE.md)  
**Store gate:** [`SolanaSeekerBeta.md`](SolanaSeekerBeta.md)  
**Audit snapshot:** [`SOLANA_STACK_AUDIT_2026-09-11.md`](../Reports/SOLANA_STACK_AUDIT_2026-09-11.md)

This file is **not** a signup list. It answers: *what may a beta binary do?*  
Who may run that binary is the **tester allowlist** (section 3). Do not mix them.

## How to use this file

1. Treat the verdict column as the live contract for calendar step 1 (gate stubs).
2. When a calendar step promotes a path, change the verdict here in the same change as the code.
3. Do not add waitlist-site work, Core mint, VRF, or escrow until this table and the tester sequence say so.

Verdicts:

| Verdict | Meaning |
| --- | --- |
| **Allow** | Live in the production-oriented beta binary. |
| **Off-chain only** | Gameplay exists via BattleService / Supabase. No `chainbound_game` instruction. |
| **Gate** | Unreachable from player UX and from production service composition. |

---

## 1. First-beta sentence

**Devnet closed beta:** connect a proven wallet, create profile/vault PDAs, play overworld and off-chain combat, read verified cosmetics. No mint, no vending, no wager custody, no VRF, no client-authored wins/captures/ordinals.

That sentence is the product slice until calendar steps 5–7 promote grants through receipts.

---

## 2. Feature allowlist (first slice)

### 2.1 Player surfaces

| Player can… | Chain / service | First-slice verdict | Promote at |
| --- | --- | --- | --- |
| Continue as guest (local/unranked play) | `AccountSessionService` | **Allow** (no chain session) | — |
| Connect wallet (MWA) + nonce proof | `wallet_connect_verified` | **Allow**, then tester gate | Tester sequence step 2 |
| Restore profile by proven wallet | `wallet_restore_profile` | **Allow**, then tester gate | Tester sequence step 2 |
| Play overworld, NPCs, sequences | Narrative / world | **Allow** | — |
| Off-chain combat | `Chainbound.BattleService` | **Off-chain only** | Calendar 5–7 if results settle on-chain |
| Capture / party / bag inventory | BattleService + profile inventory | **Off-chain only** | Calendar 5–7 (capture receipts) |
| Cosmetic **equip from DAS reads** | `onchain_cosmetics.ts` + catalog IDs | **Allow** (server verifies; Unity does not submit ownership lists) | — |
| Bindery press / Core consume | `CoreAssetConsumeService` | **Gate** | Calendar 9 if beta must mint/burn Core |
| Vending UI / paid rolls | `VendingMachineService` | **Gate** | After Core/VRF exist (not this P0 slice) |
| Ranked wager / winner-pick custody | `StubEscrowWagerService` | **Gate** | Calendar 10 |
| Inscription / Rune mint / reroll | `MoveMintService`, `RuneMintService` | **Gate** | Calendar 9 |
| Encounter VRF | `VrfService` / `request_encounter` | **Gate** | P1 after `EncounterRequest` exists |

### 2.2 `chainbound_game` instructions

| Instruction | Audit reality | First-slice verdict | Notes |
| --- | --- | --- | --- |
| `create_profile` | Implemented, wallet-owned PDA | **Allow** | Owner init only. |
| `create_move_vault` | Implemented | **Allow** | Owner init only. |
| `initialize_chainling_vault` | Implemented | **Allow** | Owner init only. |
| `initialize_equipped_registry` | Implemented | **Allow** | Init only; do not treat registry as paid-asset authority. |
| `update_position` | Implemented, non-authoritative cache | **Allow** | Optional; never ladder/economy truth. |
| `initialize` | No-op today | **Gate** from player UX | Calendar 2 replaces this with `GameConfig`. |
| `update_wins_losses` | Client-trusted | **Gate** | Off-chain BattleService until receipts (calendar 5–7). |
| `add_ordinal` | Client-trusted | **Gate** | Same. |
| `add_chainling_from_capture` | Client-trusted | **Gate** | Same. |
| `create_chainling` | Client-trusted duplicate | **Gate** | Prefer delete later; do not expose. |
| `set_equipped_slot` | No ownership proof | **Gate** as paid-asset write | Cosmetics stay DAS/server. |
| `spend_ram` | Owner burn, no delivery | **Gate** | No spend-then-stub-mint. Revisit with atomic spend (calendar 6/9). |
| `burn_ordinal_for_ram` | Atomic but no UTC cap | **Gate** | Calendar 6. |
| `remove_chainling` | Owner destructive | **Gate** | Not needed for first slice. |
| `credit_ram_from_burn` | Fail-closed | **Gate** | Stay disabled. |
| `request_encounter` | Fail-closed | **Gate** | P1 VRF. |
| `mint_inscription_cnft`, `upgrade_inscription_to_rune`, `reroll_rune_modifier` | Fail-closed | **Gate** | Calendar 9. |
| `create/fulfill/consume_upgrade_ticket` | Fail-closed | **Gate** | Calendar 9 / P1. |
| `vending_*` | Fail-closed | **Gate** | After real vending CPI. |
| `apply_xp_and_maybe_level_up`, `claim_level_reward_ordinal` | Fail-closed | **Gate** | Calendar 5–7 if grant receipts cover it. |
| `mint_inscription` (legacy Core) | Disabled | **Gate** | Never reactivate. |

### 2.3 Unity composition that must not ship in the beta binary

Keep the types; do **not** compose them into player UX while this slice is active:

- `MoveMintService`
- `RuneMintService`
- `VendingMachineService`
- `UpgradeTicketService`
- `LevelUpMoveGrantService`
- `VrfService` as a live encounter/mint dependency
- `StubEscrowWagerService` on `WagerMatchOrchestrator` / winner-pick UI

`SolanaServiceBridge` now leaves these services uncomposed for the closed-beta player build. `ClosedBetaFeaturePolicy` is the fail-closed instruction allowlist, and the vending surface refuses to open when its service is unavailable.

---

## 3. Tester allowlist sequence

Solana Mobile has **no** closed test track. The store listing can be public; access is ours. A website is not auth.

Do this in order. Do not skip to a waitlist site.

1. **Write this feature allowlist** (this file).
2. **Add `beta_allowlist` (or a flag on `profile_wallets`) and enforce it after wallet proof**, not from a website cookie.
3. **Seed Ring A by hand.**
4. **Only then** a waitlist site, if more testers are wanted.
5. **If listed on the dApp Store while still closed:** keep the in-app gate, and prepare a reviewer account as Solana Mobile asks.

Optional later (not this slice): Seeker Genesis Token gate (SIWS + mint uniqueness on the server); U$ER QA on an already-approved listing.

### 3.1 Enforcement (step 2)

- Proof stays where it is: `wallet_connect_verified` and `wallet_restore_profile` after a consumed nonce/challenge.
- After a valid proof, the server loads `beta_allowlist` (preferred) or an explicit column on the active `profile_wallets` row.
- If the wallet is missing or `revoked`, return a dedicated error (for example `beta_not_allowlisted`). Do not create a playable beta session.
- Unity shows closed-beta copy and does not treat a local prefs/cookie flag as membership.
- Guest continue may remain for unranked local play. Linking or restoring into the closed beta requires an allowlisted proven wallet.

Suggested table (implement in a later migration; do not invent a cookie):

| Column | Role |
| --- | --- |
| `wallet_public_key` | Proven address (unique while active) |
| `ring` | `A`, `B`, or `reviewer` |
| `status` | `invited`, `approved`, `revoked` |
| `notes` | Discord/handle, device, who seeded |

Ring A seed fields to collect by hand: name, Seeker vs other Android, wallet pubkey, Discord handle.

### 3.2 Rings

| Ring | How people get in | Install |
| --- | --- | --- |
| **A** | Hand-seeded wallets (you + a small group) | Sideload |
| **B** | Waitlist form → humans copy wallets into `beta_allowlist` | Store or link; still prove wallet in-app |
| **C** | Public dApp Store listing, same in-app gate | Anyone can download; only allowlisted (or later SGT) wallets pass |

Reviewer wallet: one `ring = reviewer` / `status = approved` row. Give that account to Solana Mobile app review (`publishersupport@dappstore.solanamobile.com`) so gated content is testable. Device `Model == Seeker` is not the allowlist.

### 3.3 Waitlist site (step 4 only)

A form may collect email, Discord, wallet, and device. It must not grant access. Approval is a row in `beta_allowlist`. Do not start this site until Ring A is playing the gated binary.

---

## 4. Honest listing copy

Until calendar 5–7 (and 9–10 if those ship), store/in-app copy must not promise:

- on-chain captures, wins, or ordinal grants
- vending / gacha mint
- ranked SOL or Rune custody
- VRF-fair encounters

Allowed claims for this slice: wallet connect, profile, off-chain battles, cosmetic reads of a trusted collection, closed beta.

---

## 5. Change log

| Date | Change |
| --- | --- |
| 2026-09-12 | First-slice allowlist and tester sequence written from the 2026-09-11 audit and the agreed access order. |
| 2026-09-12 | Implemented the player instruction policy, removed gated service composition, added the verified-wallet tester gate, and added the manual Ring A seed runbook. |
