# Chainbound Solana Closed-Beta Build Sequence

**Status:** working plan (execute in order; not parallel dumps)  
**Date:** 2026-09-12  
**Cluster:** devnet/localnet until a later doc explicitly approves mainnet  
**Feature contract:** [`BETA_FEATURE_ALLOWLIST.md`](../../Release/BETA_FEATURE_ALLOWLIST.md)  
**Store APK gate:** [`SolanaSeekerBeta.md`](../../Release/SolanaSeekerBeta.md)  
**Audit snapshot:** [`SOLANA_STACK_AUDIT_2026-09-11.md`](../../Reports/SOLANA_STACK_AUDIT_2026-09-11.md)  
**Escrow spec (step 10 only):** [`PVP_02_Ranked_Escrow_And_Ladder.md`](../../Guides/PVP_02_Ranked_Escrow_And_Ladder.md)  
**Taxonomy:** [`CHAINBOUND_SOLANA_ASSET_TAXONOMY.md`](CHAINBOUND_SOLANA_ASSET_TAXONOMY.md)

P0 is not “finish Solana.” It is **make the live program trustworthy, and keep everything else unreachable.** Escrow, Core mint, VRF, and Ephemeral Rollups come after this foundation.


## How to read this calendar

Do **steps 1–4 before any new mint/CPI work.** That turns scaffolding into a program you can keep extending.

**Honest gameplay beta (after step 1, still gated testers):** wallet, profile, vault PDAs, cosmetic **reads**, off-chain battles. Captures/wins stay off-chain until 5–7.

**Devnet product beta (after 1–7):** same, plus captures/wins/ordinals only through server receipts — never Unity-chosen IVs/wins.

**Still not in this sequence:** transaction v0/v1, MagicBlock VRF, Ephemeral Rollups, public waitlist-as-auth, mainnet value.

Two tracks. Do not swap them.

| Track | What it controls | Starts when |
| --- | --- | --- |
| **A — access** | Who may run the binary | After the feature allowlist exists |
| **B — program** | What the binary and program may do | Calendar row 1 |

Track A does not unlock economic instructions. Track B does not replace wallet proof.

---

## Track A — tester allowlist (do not skip)

Canonical order lives in the [feature allowlist](../../Release/BETA_FEATURE_ALLOWLIST.md#3-tester-allowlist-sequence):

1. Feature allowlist written (**this week’s starting artifact** — done as of 2026-09-12; update verdicts when code changes).
2. `beta_allowlist` (or a `profile_wallets` flag) enforced **after** wallet proof in `wallet_connect_verified` / `wallet_restore_profile`.
3. Seed Ring A by hand.
4. Waitlist site only if more testers are needed.
5. If the dApp Store lists while still closed: keep the in-app gate; seed a `reviewer` wallet for Solana Mobile.

Implement step 2 beside calendar step 1 if convenient. Do not block calendar 1 on a website.

---

## Track B — calendar (not parallel dumps)

| Order | Work | Unlocks |
| --- | --- | --- |
| 1 | Gate stubs + stub escrow | Honest gameplay beta |
| 2 | `GameConfig` + real authority | Any safe economic ix |
| 3 | Generated IDL + C# | Safe to add ixs |
| 4 | Account read envelopes | Trust PDA state |
| 5–7 | Receipts + RAM cap + BattleService settle | Real captures/wins/ordinals |
| 8 | Test-wallet / CDN | Shippable client |
| 9 | Core CPI (only if mint/burn in beta) | Runes / consume |
| 10 | Escrow program | Ranked wagers |

Parked until 1–8 are real: P1 transaction envelopes, review DTO, VRF, ER. Do not enable `request_encounter` until an `EncounterRequest` PDA and a non-player fulfill authority exist.

---

## Step 1 — Gate stubs + stub escrow

**Goal:** A production-oriented build cannot call fail-closed or fake-economy code.

**Depends on:** feature allowlist verdicts.

**Work:**

- Drive composition from the allowlist, not scattered `if`s. Prefer one policy on `SolanaServiceBridge` / `SolanaConfigSO` (or a small Combat.Runtime policy type) that refuses to construct gated services in player builds.
- Do not compose `MoveMintService`, `RuneMintService`, `VendingMachineService`, `UpgradeTicketService`, `LevelUpMoveGrantService`, or live `VrfService` mint/encounter paths unless the deployed program implements them (today they return `ContractNotImplemented`).
- Keep `StubEscrowWagerService` out of production composition (`WagerMatchOrchestrator` / winner-pick). Leave `IEscrowWagerService`; the stub is not custody.
- Hide or disable UI that implies mint, upgrade, vending, or wager.
- Player-facing builders for `update_wins_losses`, `add_ordinal`, `add_chainling_from_capture`, `create_chainling`, `spend_ram` (as a purchase), and `burn_ordinal_for_ram` stay unreachable.

**Likely files:** `Assets/Scripts/Solana/Services/SolanaServiceBridge.cs`, wager composition under `Assets/Scripts/Combat/Runtime/`, gated menu/vending entry points, `Docs/Release/BETA_FEATURE_ALLOWLIST.md` if a verdict moves.

**Done when:** a player in that build cannot submit a mint, vending, VRF, escrow, or client-grant transaction, even if they poke a service.

**Unlocks:** honest gameplay beta (Track A testers + off-chain combat).

---

## Step 2 — GameConfig + real authority

**Goal:** Stop `ProgramAuthorityOnly` (any signer) and the no-op `initialize`.

**Depends on:** step 1 (stubs already unreachable).

**Work:**

- Change `initialize` in `backend/chainbound_game/programs/chainbound_game/src/lib.rs` so it creates a `GameConfig` PDA: program/result authority pubkey(s), schema version, RAM daily cap / per-burn (even if unused until step 6), optional instruction flags.
- Replace `ProgramAuthorityOnly` with contexts that check `signer == config.result_authority` or a dedicated receipt PDA. Players never become that key.
- Put BattleService / a server keypair in config as **result authority** (same referee idea as PVP_02).
- LiteSVM or Anchor tests: wrong signer fails; missing config fails; player cannot call authority ixs.

**Done when:** one on-chain config account exists, and economic instructions cannot run without that authority or a receipt it issued.

**Unlocks:** any later safe economic instruction.

---

## Step 3 — Generated IDL + C#

**Goal:** Stop handwritten Borsh / account-order drift before adding more instructions.

**Depends on:** step 2 (config layout is the first real contract change worth generating).

**Work:**

- `anchor build` → check in a **generated** IDL. Stop treating `idl/chainbound_game_contract.json` as a hand-edited manifest.
- Generate C# (Codama where it fits, or the existing project generator). Stop extending duplicate layouts in profile/vault builders.
- Account-order parity tests (Anchor TS + Unity EditMode).
- Update [`CHAINBOUND_PROGRAM_REGISTRY.md`](CHAINBOUND_PROGRAM_REGISTRY.md) if the artifact location or generation command changes.

**Done when:** adding an instruction is “IDL → generated client,” not “edit Rust and a C# byte array by hand.”

**Unlocks:** safe instruction additions (receipts, RAM, later Core wrappers).

---

## Step 4 — Account read envelopes

**Goal:** Unity/RPC cannot treat raw bytes as a profile or vault.

**Depends on:** step 3 so parsers match the generated layout.

**Work:**

- Extend `IRpcService` / `SolanaRpcService` so account reads return owner, lamports, executable, data, and context slot — not bytes only.
- Each parser: expected program ID, discriminator, min length, embedded wallet, bump/seeds.
- Confirmation: return Confirmed vs Finalized honestly (today confirmed is reported as Finalized).
- EditMode tests: wrong owner, truncated data, spoof program.

**Done when:** a spoofed account is rejected in tests.

**Unlocks:** trusting PDA state in the beta client.

---

## Steps 5–7 — Receipts + RAM cap + BattleService settle

Treat as one product slice. Do not ship client `add_ordinal` / `add_chainling_from_capture` / `update_wins_losses` in the meantime.

### 5 — Split owner prefs from economic state

Keep **owner-signed** (harmless): `create_profile`, vault/registry inits, `update_position`, later owner-destroy paths once caps exist.

Make **authority- or receipt-gated:** `update_wins_losses`, `add_ordinal`, `add_chainling_from_capture`. Prefer deleting `create_chainling` (duplicate, no invariant). `set_equipped_slot` must not authorize paid assets.

Pattern for this repo: BattleService (or an edge function with the result-authority key) writes a **one-shot receipt PDA** (match/capture id, species, IVs, ordinal ids, expiry, consumed). The player transaction `consume_capture_receipt` / `apply_match_result` is the only way those fields change.

**Done when:** a connected wallet cannot `add_ordinal(999)` or `update_wins_losses(true)` on its own.

### 6 — RAM rules on-chain

On `burn_ordinal_for_ram`: UTC-day reset from `Clock`, then `daily_ram_earned + credit <= cap` from `GameConfig` (not Unity `PlayerPrefs`). Do not add a second credit instruction. Spend that buys something must be the same transaction as the action, or receipt + refund.

**Done when:** tests cover cap hit, day rollover, and no standalone credit.

### 7 — BattleService as the only grant path

After a verified battle/capture, the server creates the receipt from step 5. Unity only signs consume/apply. Fail closed on missing, expired, consumed, or wallet-mismatched receipts. Keep combat persistence in `Chainbound.BattleService`; add a thin settlement adapter — do not put the combat engine in Anchor.

**Done when:** a capture either writes a chainling via receipt, or stays fully off-chain by design — never “client called `add_chainling_from_capture`.”

Update the [feature allowlist](../../Release/BETA_FEATURE_ALLOWLIST.md) when these paths move from **Gate** / **Off-chain only** to **Allow**.

**Unlocks:** real captures, wins, and ordinal grants on devnet.

---

## Step 8 — Test-wallet / CDN (shippable client)

May overlap 5–7. Do not wait for Core.

**Work:**

- Strip `/api/use-test-wallet` from `DesktopWalletBridge` in player builds (`UNITY_EDITOR` or a dedicated define).
- Pin/vendor the desktop signing stack; do not load web3.js from a public CDN.
- Confirm MWA/desktop paths never log or persist private keys.
- Keep [`SolanaSeekerBeta.md`](../../Release/SolanaSeekerBeta.md) as the APK/store checklist.

**Done when:** a release player build has no test-wallet route and no CDN signer.

**Unlocks:** a shippable Seeker/desktop client around the gated program.

---

## Step 9 — Core CPI (only if the beta mints or burns)

Cosmetic **equip from a pre-minted collection** already has the right shape (`onchain_cosmetics.ts`). Skip this step if the beta only **reads** cosmetics.

**Depends on:** steps 2–4. Do not start Core CPI before them.

If mint or consume is in the beta:

- Pin `mpl-core`; use official Create/Transfer/Burn CPI; require collection + update authority + plugin policy.
- Never model Core as mint + ATA.
- `CoreAssetConsumeService` prepare must return a real burn transaction; finalize only after confirmation.
- Adversarial tests: wrong collection, freeze, delegate, spoof program owner.
- Do not implement `RuneMintService`’s fake “rune PDA.”
- Inscription cNFT mint stays gated unless product explicitly changes taxonomy (canonical new Inscriptions are cNFT; v1 export direction for Stamped/Bound is Core — do not invent a third path here).

**Unlocks:** Runes / Core consume. Leave **Gate** on the allowlist until this is real.

---

## Step 10 — Escrow program (ranked wagers)

Last among this sequence. Casual/devnet PvP without stakes does **not** need this for the first beta.

**Depends on:** Phase 1 matchmaking **and** the result authority from step 2. Implementing escrow before receipts means you will still settle from the client.

Follow `PVP_02`: dedicated escrow program, Core Runes + SOL only, no cNFT Inscriptions. Deposit → `Locked` → server-signed settle / forfeit / timeout. External audit before mainnet or real funds. Rename leftover “inscription wager” copy to Core Rune semantics before wiring UI.

**Unlocks:** ranked wagers. Keep `StubEscrowWagerService` out of production forever.

---

## What to start now

One thread, in this order:

1. Keep [`BETA_FEATURE_ALLOWLIST.md`](../../Release/BETA_FEATURE_ALLOWLIST.md) as the live contract (already written; edit when verdicts change).
2. **Calendar 1:** gate `SolanaServiceBridge` and stub escrow to that contract.
3. **Track A step 2:** `beta_allowlist` after wallet proof; seed Ring A by hand.
4. **Calendar 2:** `GameConfig` + fix `initialize` / `ProgramAuthorityOnly`.
5. **Calendar 3:** generated IDL and C# from that change.

Everything else in the audit is a later row (Core, escrow) or P1 (tx v1, VRF, ER).

---

## Parked (P1)

Do not pull these into steps 1–8:

- Version-aware legacy / v0 / v1 message parsing and review DTO
- Durable business idempotency records
- MagicBlock VRF (`EncounterRequest` + non-player fulfill)
- Ephemeral Rollups (delegate session PDAs only; Core/escrow stay on base layer)

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-12 | Calendar captured as the working sequence; access track separated from program track. |
