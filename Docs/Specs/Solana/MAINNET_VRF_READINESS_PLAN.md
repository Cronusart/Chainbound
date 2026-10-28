# Mainnet VRF Readiness Plan

**Status:** Plan (2026-10-27). Nothing in this document has been built yet, except where marked.
**Scope:** What has to be true before the `chainbound_game` program and its MagicBlock VRF rolls go live on Mainnet. Binding is out of scope here.
**Related:**
- [KNOWN_RARITY_WEIGHTS_v1.md](../Economy/KNOWN_RARITY_WEIGHTS_v1.md): odds, approved, live on Devnet.
- [ROLL_REQUEST_VRF_DESIGN_v1.md](../Economy/ROLL_REQUEST_VRF_DESIGN_v1.md): the two-step roll flow.
- [MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md](MAINNET_SMOKE_DEPLOYMENT_RUNBOOK.md): mainnet deploy mechanics.
- [DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY.md](DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY.md): client and backend profile split.
- [Docs/Legal](../../Legal/README.md): the public Terms and Privacy Policy.

## Summary

| # | Area | Direction (Cronus, 2026-10-27) | First concrete step |
|---|---|---|---|
| 1 | Authority | Move upgrade and program authority to a Squads multisig; rehearse on Devnet | Program change: transferable `program_authority` |
| 2 | Audit | Outsource to a Solana program security firm, plus a verified build | Freeze scope and collect quotes |
| 3 | Mainnet route | Confirmed | Follow the phases below |
| 4 | Roll failure | Never leave a player cheated out of a roll; strict anti-exploit guardrails | Program change: `cancel_roll`; server retry-then-refund |
| 5 | MagicBlock | Dependency accepted; keep a documented exit path | `generator_version` already allows a new entropy source |
| 6 | Legal | Odds disclosure, loot-box and randomness terms | Draft clauses added to Terms and Privacy (counsel review pending) |

## 1. Authority model and multisig

### The three keys today

| Role | Key | Powers | Risk today |
|---|---|---|---|
| Upgrade authority (loader) | `DkFmzzzF…gJmG` | Replace the whole program, odds and rules included | One keypair file in WSL. **The same key is also the recorded Mainnet upgrade authority and payer** (smoke runbook) |
| `GameConfig.program_authority` | `DkFmzzzF…gJmG` | `update_game_config` (incl. rotating `result_authority`), `initialize_roll_odds` / `update_roll_odds`, ownership config | **Cannot be rotated:** no instruction changes it after `initialize` |
| `GameConfig.result_authority` | `FaAegH2t…Pt5KF` | Server-side signer: RAM grants and debits, profile mutations, roll requests and consumes, stamps | Hot key by design: a Supabase secret, needed for automated signing |

### Target model

- **Upgrade authority and `program_authority`:** a Squads v4 multisig vault, one per cluster. Never the same key on Devnet and Mainnet.
- **`result_authority`:** stays a hot server key (one per cluster). The program limits what it can do, it holds a small SOL float, and the multisig can rotate it with `update_game_config`.
- **Threshold: 2-of-3, all three keys held by Cronus** (decided 2026-10-27). Each member has one job and lives on a separate device:

  | Member | Device | Role |
  |---|---|---|
  | A | Seeker Seed Vault | Daily approver; the key never leaves the secure element |
  | B | Phantom, a **fresh account used only for the multisig** | Proposer and second approver from desktop |
  | C | Offline recovery key `63PMxx4dp7qBr7NRzpFg1XMowJNT5G1H19UXrnG1XME` (created 2026-10-27; seed phrase on paper only) | Used only if A or B is lost or compromised |

  - **How to make key C:** run `solana-keygen new --no-outfile` on a machine that is offline at the time. Write the seed phrase on paper, store it somewhere safe, and record only the public key. It never goes on disk, in a password manager synced to the cloud, or in this repo.
  - **Recovery:** if A or B is lost, C plus the surviving key vote a Squads config change that swaps in a new member.
  - **Upgrade path:** a hardware wallet can later replace B or C through the same members vote.
  - **Squads settings:** set the config authority to **none**, so member and threshold changes also need 2-of-3.
- **Mainnet multisig created** (2026-10-27, verified from Mainnet RPC):

  | Field | Value |
  |---|---|
  | Multisig account | `BUMqmAsxyXG5P1C3UhKFGnt1zp7qT12WYb9GujzccGfD` |
  | Vault (index 0) | `3Xp4tmmVNpS9bSvDYkgitV1F17SjjaCMxc7hJEqM2tre` (derivation checked; holds 0.001 SOL) |
  | Threshold | 2 of 3 |
  | Config authority | none, so changes need a member vote |
  | Time lock | **0. Raise it to 24 h with a config transaction before the vault takes any Mainnet authority** |
  | Members (all with Initiate, Vote and Execute) | `63PMxx…XME` (offline recovery, C), `9zMwTbQtjvexJzuMZjzbKS7LtWNXVYG3auBNeV82VHUj`, `AxrH7pwxL1kdfMDHBRTt6YhuNCWpcNkqjJJ633gVoCLu` (A and B: which is which is still to be confirmed) |

  This multisig is on **Mainnet**, so it can't sign for the Devnet program. The Devnet rehearsal needs its own Devnet multisig with the same three members.

  Member roles: `AxrH7pwx…oCLu` is **Phantom** (B), so `9zMwTbQt…VHUj` is the **Seeker Seed Vault** (A).
- **Devnet multisig created** (2026-10-27) with `tools/vrf-efficacy/squads.cjs create-multisig`. The throwaway `Ei1HGT9c…` paid; it has no role in the multisig.

  | Field | Value |
  |---|---|
  | Multisig account | `9kcMg2WKwF2XdUBuKnpHnso4PRuVNPVgg1VuGtbjAv9m` |
  | Vault (index 0) | `G4eqvbNwpJazSYkmS6y1Zq4zjLcABrFDZKiPV2JVNP7S` |
  | Create tx | `2ePNAQ5n7QEYcR6Vn3wbeKH1sB8DgKUymKad1wW5Hc5DqZ1Qgaj5qPzqKoWgSHh2FNg7EFdRrcJLRF8G8chSdry9` |
  | Settings (read back from chain) | 2 of 3, time lock 0, config authority none; the same three members as Mainnet, all with Initiate, Vote and Execute |

  `propose_program_authority(G4eq…)` simulated cleanly against the live Devnet program (10,828 CUs).
- **Devnet signing path.** The hosted Squads app has no working Devnet mode (`devnet.squads.so` now redirects to squads.xyz), so the rehearsal uses a local page, `tools/vrf-efficacy/squads-signer/`, built on the pinned `@sqds/multisig` SDK.
  - **Wallets:** any Wallet Standard browser wallet (Phantom), plus Mobile Wallet Adapter for the Seeker's Seed Vault.
  - **Scope:** it builds only `accept_program_authority`, and refuses any RPC that isn't Devnet.
  - **Checks before approving:** it decodes the on-chain vault transaction and confirms one instruction, the right program, the right discriminator, the right accounts in order, and vault index 0.
  - **Sending:** wallets only sign; the page sends through the Devnet RPC itself.
  - **Tested:** create, approve and propose simulated cleanly as Phantom (51,028 CUs), but execute can't be simulated until the propose step has run.
  - **Steps:**
    1. Run `node tools/vrf-efficacy/squads-signer/server.cjs` and open `http://localhost:8787` (bound to 127.0.0.1 only).
    2. Phantom (desktop): Create proposal (this also records Phantom's approval).
    3. Seeker: run `adb reverse tcp:8787 tcp:8787` over USB, then open `http://localhost:8787` in Chrome on the Seeker. localhost is a secure context, which MWA needs. Approve and execute with the Seed Vault.
- **`program_authority` handed to the Devnet vault** (2026-10-27, verified from Devnet RPC):

  | Step | Signer | Tx |
  |---|---|---|
  | `propose_program_authority(G4eq…)` | DkFm | `47kxUGYVmnTT5VsDCjiQ1HVCUArWFDoywBBjGJrCaUyc7es1k2tpfZ5Pryn3YPwJpUfYSFL7nJYfyiZZ35YqKA7v` |
  | Vault transaction #1 create, proposal, approve | Phantom `AxrH…` | `46dFBommxYJTEThsG4CYJEQ8QSTUoZ2NnU9Ce4KLkgXXJBMzMxhZtRnDc17UdBBk6PpUALW7ecC6PiF8DXJxB5L6` |
  | Approve, execute → `AcceptProgramAuthority` | Seeker Seed Vault `9zMw…` over MWA | `2ykKMgC3Yfvq4s2zx37gAyJ6gMfaStDdFcSiLjdjjPi9ruhjwL4HfTx4qP8ESbs6MbFm8nmyfwUhNDAANXRiDH6x` (slot 515,901,688) |

  - **Result:** `GameConfig.program_authority` is now `G4eqvbNwpJazSYkmS6y1Zq4zjLcABrFDZKiPV2JVNP7S`, and the `authority_transfer` PDA is closed.
  - **Stored instruction:** the vault transaction stored the accept instruction with exactly the expected accounts.
  - **Seeker:** the Seed Vault can approve and execute Squads transactions over MWA in Chrome. That item in "to confirm in the rehearsal" is done.
  - **What DkFm can no longer do:** set odds or change `GameConfig`. Those now need 2 of 3. DkFm is still the **upgrade** authority.
- **Signing page, version 2** (2026-10-27): it now proposes, decodes and shows four kinds of vault transaction. The instruction builders live in `src/instructions.mjs`, shared by the page and `simulate.mjs`.

  | Proposal | Inner instruction | What approvers see decoded |
  |---|---|---|
  | Change roll odds | `update_roll_odds` | kind, per-tier %, pool hash, threshold validity |
  | Upgrade program | loader `Upgrade` | buffer address, size, sha256 (compare with the build), buffer authority, spill |
  | Recovery | loader `SetAuthority` | the new upgrade authority (default DkFm) |
  | Accept | `accept_program_authority` | the previous authority |

  - **Reject:** a Reject button clears a bad proposal (2 rejections).
  - **`simulate.mjs` on Devnet, all PASS:**
    - the vault updates Known odds;
    - DkFm updating odds now fails with `6012 InvalidAuthority`;
    - DkFm `SetAuthority` → vault succeeds;
    - in the same transaction, vault `SetAuthority` → DkFm also succeeds, which proves the recovery exit works once the vault holds the upgrade authority.
  - **Not offered:** `extend`, and `initialize_roll_odds`. Both odds accounts already exist.
- **Upgrade authority moved to the vault, and one upgrade done under the multisig** (2026-10-27, verified from Devnet RPC):

  | Step | Signer | Tx |
  |---|---|---|
  | `set-upgrade-authority` → `G4eq…` | DkFm | `4as3J4Q5n3vPFaR4uHAMf9zP6qqndgYRsKQNDUFBgkgrBAv1rdz2ThYkyG4Puog5YJ8PyEJt7aER8hp7syChqx7A` |
  | `write-buffer` `bpZm4UAz…` (1,044,512 bytes, sha256 `1f4293ab…`), then `set-buffer-authority` → vault | DkFm | — |
  | Vault transaction #2 create (Upgrade), proposal, approve | Phantom | `3D3ezRL64jVUbEv4aGvusbyHRQowxAEsibqWrxmTK2d4bSuhT5RbQPX3toAVN2vgqKVfo7aqBJiRd5rUvssAbGbX` |
  | Approve, execute → `Upgraded program` | Seeker Seed Vault | `3VFumixpexaCJj3BuN6Vp2spXs5D9r59QSczeQooagLBhFrypRwK79gRUJ4qRBXWK3Ku3dwwxwMa8zfmbgdT98JG` (slot 515,921,531) |

  - **Upgrade authority** is now the vault.
  - **Deployed code:** the programdata was redeployed at slot 515,921,531, and its first 1,044,512 bytes hash to `1f4293ab…`, the same build.
  - **Rent:** the buffer is closed, and its 5.3070 SOL went back to DkFm through the spill.
  - **What this proves:** the whole Mainnet upgrade path works with these wallets: buffer, vault proposal, 2-of-3 approval with Phantom and the Seed Vault, execute.
  - **Still owed:**
    - the time-lock rehearsal on a short-lived multisig;
    - an extend proposal type, needed before any build outgrows the current 1,052,912-byte programdata;
    - executing the recovery proposal once, if wanted. It has only been simulated so far.
- **Original upgrade checklist** (done above):
  1. Run `set-upgrade-authority` to move the upgrade authority to the vault (DkFm signs).
  2. `solana program write-buffer` the current build.
  3. `set-buffer-authority` to the vault.
  4. Create an Upgrade proposal with spill = DkFm; Phantom creates it, and the Seeker approves and executes.
  5. Verify the programdata hash matches the build.
- **Time lock** (decided 2026-10-27):
  - **Mainnet: 24 h.** Approved upgrades, odds changes and config changes wait 24 h before they can execute. Hotfixes are planned with that delay.
  - **Devnet: none.** Before Mainnet, rehearse the time lock once on a short-lived Devnet multisig with a short value.
- **To confirm in the rehearsal:**
  - Seeker Seed Vault can approve Squads proposals over Mobile Wallet Adapter.
  - Approvals from both wallets reach the Squads app.

  If the Seed Vault path doesn't work, member A falls back to a second fresh browser-wallet account on a different device until a hardware wallet is added.

### Program change needed first

**Status:** implemented, unit-tested, and deployed on Devnet at 7:59 PM CT on 2026-10-27 (program commits `423417a0` / `fdf9f88d`).

`program_authority` has to become transferable. Add two instructions:

- `propose_program_authority(new_authority)`, signed by the current authority, writes a pending authority to a new PDA `["authority_transfer"]`. A separate PDA avoids reallocating `GameConfig`.
- `accept_program_authority()`, signed by the pending authority (the Squads vault, through a proposal), sets `GameConfig.program_authority` and closes the PDA.

The two-step shape stops a typo from bricking config control. A third instruction, `cancel_program_authority_transfer`, lets the current authority withdraw an unaccepted proposal. `OwnershipConfig` has no authority field of its own; it checks `GameConfig.program_authority`, so one transfer covers both configs.

Tooling, in `tools/vrf-efficacy/roll.cjs`:
- `show-authority`: shows the current authority and any pending transfer.
- `propose-authority --new <vault> [--dry-run]`: proposes the new authority (signed by DkFm).
- `cancel-authority-transfer`: withdraws an unaccepted proposal.
- `accept-authority-ix`: prints the accept instruction for the Squads proposal. It never signs anything.

### Devnet rehearsal (the template for Mainnet)

1. Create a Squads v4 multisig on Devnet with the chosen members and threshold. Note its **vault** address: the vault is the authority, not the multisig account. Take the Squads program ID and app from Squads' docs, not memory.
2. Ship the authority-transfer instructions (Phase 1 below), then move config control:
   - DkFm runs `propose_program_authority(<vault>)`.
   - The vault runs `accept_program_authority` through a Squads proposal.
3. Move the upgrade authority. The vault can't co-sign a CLI command, hence the flag:
   ```
   solana program set-upgrade-authority 9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ --new-upgrade-authority <VAULT> --skip-new-upgrade-authority-signer-check --url devnet --keypair <DkFm keypair>
   ```
4. Rehearse an upgrade under the multisig:
   - `solana program write-buffer <.so>`
   - `solana program set-buffer-authority <BUFFER> --new-buffer-authority <VAULT>`
   - Create the upgrade proposal in Squads; members approve; execute.
   - On newer loaders, `extend` may also need the authority, so rehearse an extend too.
5. Rehearse an odds change under the multisig. Add a `--squads <multisig>` mode to `tools/vrf-efficacy/roll.cjs set-odds` that builds the `update_roll_odds` instruction and creates a vault proposal instead of signing directly.
6. Rehearse a `result_authority` rotation (`update_game_config` through a proposal) and record the runbook.
7. Record each step, address and signature in a new `MULTISIG_AUTHORITY_RUNBOOK.md`. That file becomes the Mainnet script.

**Mainnet differences:**
- Use a fresh Mainnet multisig, and a fresh Mainnet `result_authority` (not `FaAeg…`).
- Hand the upgrade authority to the vault immediately after the first deploy.
- Retire DkFm as the Mainnet authority, or treat it as deploy-payer only.

## 2. Audit and verified build

**What it is:** an outside firm that specializes in Solana and Anchor programs reviews the source for exploitable bugs, then reviews your fixes. Their findings report is published or shared with partners.

**Firms that audit Solana programs** (get 2–3 quotes; this is not an endorsement): OtterSec, Neodyme, Zellic, Sec3, Halborn, Ackee Blockchain, Accretion, Adevar Labs. Competitive-audit platforms (Code4rena, Cantina, Sherlock) are another model. Ask MagicBlock and Solana ecosystem contacts about audit subsidies or referrals.

**Scope to quote:**
- `programs/chainbound_game/src/lib.rs`, a single Anchor 0.32 program of about 3,700 lines, with these surfaces: RAM ledger, Known move vault, stamps, binding, encounter VRF, roll VRF, and config and authority.
- Optional second scope: the settlement path in Supabase Edge Functions (reconcile jobs and the RAM ledger), since that is where VRF bytes become rewards.

Price depends on scope, depth and timeline. Get quotes rather than budgeting from a rule of thumb.

**Prep before the audit starts:**
- Freeze the program feature set. Binding can be its own later scope.
- Write a one-page threat model: what each authority can do, the economic assets, the trust in MagicBlock.
- Get `cargo test` green and add negative tests for every `has_one` and status check.
- Run `program_autofixer` (Solana MCP), `cargo audit`, and Sec3 X-ray or a similar scanner, and fix what they find first.
- Write a short spec per instruction. Existing design docs cover most of it.

**Verified build:**
1. Build with `solana-verify build` (a pinned Docker toolchain), so anyone can reproduce the exact `.so` from a public commit.
2. Deploy that artifact.
3. Run `solana-verify verify-from-repo` and submit it to the public verification registry, so explorers show "verified".

This is what makes the published odds trustworthy: players can check that the deployed code is the audited source. The source must be public at the verified commit. The build pipeline must switch from plain `cargo build-sbf` to `solana-verify`.

## 3. Mainnet route (confirmed)

Existing Mainnet facts, from the smoke runbook:
- The program ID `7oBf57qG…M5Pj` is reserved and not deployed.
- The payer has 0 SOL.
- The artifact recorded there (741 KB) predates the roll instructions and must be rebuilt.

Mainnet-only checklist:
- MagicBlock's Mainnet oracle queue address and per-request fee. Devnet's charge was 500,000 lamports; confirm the Mainnet figure with MagicBlock.
- A paid RPC provider behind `solana_rpc_proxy`. Never ship a key in the APK.
- A priority-fee policy for result-authority transactions.
- The client and backend Mainnet profile (DEVNET_MAINNET_BUILD_PROFILE_BOUNDARY), un-pinning `ownershipCluster` only for the Mainnet profile.
- New `RollOdds` accounts, set with the **real exporter `pool_hash`**, not the draft-test hash.

### Cost model (measured on Devnet 2026-10-27; Mainnet rent is identical)

| Item | SOL |
|---|---|
| Program rent at the current ~1 MB (locked, refundable if the program is closed) | 5.09 |
| Deploy buffer (refunded when the deploy completes) | ~5.09 on hand |
| Upload fees (~1,000 transactions plus priority fees) | ~0.01–0.2 |
| Growth | ~0.005 per KB, using `extend` |
| Per roll, spent (VRF 500,000 lamports + 3 transaction fees) | ~0.00052 |
| Per roll, rent float (refunded at `close_roll`, after about 1 h) | ~0.0018 |

Cost controls to design in:
1. **One VRF request per bundle, not per pull.** Decided 2026-10-27: Candy Machine bundles of 1, 10 and 20 pulls. One 32-byte result feeds every pull through `sha256(domain ‖ randomness ‖ i)`, with tiers drawn on-chain. This is in the Phase 1 build; capacity rule and UI are in [ROLL_REQUEST_VRF_DESIGN_v1.md, Multi-roll bundles](../Economy/ROLL_REQUEST_VRF_DESIGN_v1.md#multi-roll-bundles).
2. **Use VRF where the result has value:** paid pulls, anything mintable or tradeable. Free and cosmetic rolls use server randomness.
3. **Always close roll accounts.** `EncounterRequest` has no close instruction today (0.00127 SOL locked per encounter), so add one.
4. **The server pays VRF costs** and folds them into prices, so players never need SOL, or a second popup, to roll.

## 4. Roll timeouts, retry and refund

**Goal:** a roll either completes or the player is made whole automatically, with no client action that can change an outcome.

**Pattern borrowed from:**
- Battle resume: an authoritative server row with a status; `PendingBattleResumeCoordinator` asks the server what to show after menu hydration.
- `AppLifecycleService`: interruptions pause presentation only, never server state.
- The `ram_shop_reconcile` lease-based cron.

### Program change: `cancel_roll`

**Status:** implemented, unit-tested, and deployed on Devnet at 7:59 PM CT on 2026-10-27. On Devnet, `roll.cjs cancel-check` makes one live roll and checks two refusals: cancel too early while Pending (`RollCancelTooEarly`, 6042), and cancel after fulfill (`InvalidRollStatus`, 6039).

- New status `ROLL_CANCELLED = 3`.
- `cancel_roll()`, signed by the result authority: allowed only while **Pending**, and only at least `ROLL_CANCEL_DELAY_SLOTS` after `requested_at_slot` (proposed 150 slots, about 60 s).
- A late oracle callback then fails, because `fulfill_roll` already requires Pending, so cancelled rolls never get randomness.
- `close_roll` also accepts Cancelled, which returns the rent. Add a `RollCancelled` event and its error codes.

**Why this can't be used to cheat:** cancel only works *before* randomness exists. The server can't see a bad roll and discard it. A fulfilled roll can only be consumed. If cancel and fulfill race, whichever lands first wins; if fulfill wins, the cancel fails and the roll settles normally.

### Server flow (extends ROLL_REQUEST_VRF_DESIGN's two-step roll)

| Time | What happens | Player sees |
|---|---|---|
| 0 | RAM reserved; attempt 1 `request_roll` (operation hash = `sha256(operation_id ‖ attempt)`) | Roll animation |
| ~2 s typical | Reconcile sees Fulfilled → settles (debit the reservation, write the result) → `consume_roll` | Reveal |
| 10 s | Still pending | "Still rolling…": they can leave; the result waits for them |
| ≥ 150 slots | Reconcile cancels attempt 1 and requests attempt 2 (new PDA, so no MagicBlock re-request is needed) | Nothing new |
| Attempt 2 also stuck | Cancel it; release the RAM reservation; operation becomes `refunded` | "The roll couldn't complete. Your RAM was returned." |

- **Max attempts:** 2 (proposed).
- **"Refund"** means releasing the reservation: RAM is only debited at settle, so nothing extra is ever minted or credited.
- **No HMAC fallback for the same operation** (closes ROLL_REQUEST decision 2 for VRF operations).

### Client

- A `PendingRollResumeCoordinator`, shaped like the battle one. On menu hydration and when the Candy Machine opens, it asks the status endpoint for unsettled roll operations and plays the reveal, or the refund notice, once.
- Force-quit, an app interruption or a lost connection changes nothing: the server finishes the roll anyway. Relaunching shows the stored result, never a new roll.
- The client has no cancel or retry calls. Only the reconcile job can cancel.

### Anti-exploit guardrails

- **Idempotency:** one idempotency key per purchase tap; a retried request returns the same operation.
- **In-flight cap:** at most N unsettled roll operations per profile (proposed 1 for single pulls); rate limit per profile.
- **Reservation required:** RAM is reserved before `request_roll`; settlement debits only from that reservation.
- **Odds pinned:** odds and pool are snapshotted on the PDA; settlement refuses a mismatched `odds_version` or `pool_hash`.
- **Audit queries:**
  - every settled operation has exactly one Consumed attempt;
  - every other attempt is Cancelled with zero randomness;
  - there are at most 2 attempts per operation.
- **Alerts:**
  - cancellation rate above 1% over an hour;
  - any Fulfilled-but-unsettled roll older than 5 minutes;
  - `result_authority` balance under its float threshold.
- **Account-level abuse** (multi-accounting to farm free rolls) stays covered by the beta allowlist and the Terms (Section 13).

## 5. MagicBlock dependency

Accepted, given MagicBlock's position in the ecosystem and its transparency. Exit path, should it ever be needed:
- `generator_version` already separates entropy sources. A future source becomes version 3, with its own request and fulfill instructions, and settled operations keep their version forever.
- `RollOdds` and tier math are independent of the oracle.
- Keep a MagicBlock contact for incidents, and watch their changelog for queue, fee or SDK changes. The SDK is pinned in `Cargo.toml`.

## 6. Legal: odds disclosure and loot-box rules

Draft clauses added on 2026-10-27, marked `[[COUNSEL REVIEW]]` so they are filled in before publishing:
- **Terms §7:** "Random Rewards and Published Odds".
- **Terms §8:** randomness provider risk.
- **Terms §11:** MagicBlock listed.
- **Terms §13:** roll manipulation.
- **Privacy §4 and §7:** roll records and MagicBlock.

**Product requirements that come with them:**
- The Candy Machine and Stamp screens show the current odds per tier **before** purchase, read from the on-chain `RollOdds` (with the version), plus a link to verify a past roll.
- Any odds change, events included, is a new `odds_version`, announced before it takes effect.

**Questions for counsel** (from general knowledge as of 2026; verify every point):
- **Paid input + chance + tradeable output.** RAM can be bought with SOL, and Known moves can be stamped into NFTs that trade on marketplaces. That combination is the classic gambling-test pattern in many US states and other countries. Ask specifically whether stamped or tradeable outputs from paid rolls need to be separated, region-gated, or made untradeable.
- **Regional rules:**
  - Belgium has treated paid loot boxes as gambling since 2018.
  - South Korea mandates probability disclosure (since March 2024).
  - China requires odds disclosure.
  - The Netherlands' position changed after a 2022 court ruling.
  - The UK relies on industry principles (2023).

  Decide whether to geo-restrict paid rolls (Terms §18 already supports restricted jurisdictions).
- **Store policies.** Google Play and Apple require loot-box odds disclosure before purchase. Check the Solana dApp Store publisher policy for its own rule before submission.
- **Refunds.** Confirm the "failed roll → RAM returned" wording works alongside "all sales final" (Terms §6).
- **Age.** The Terms are already 18+ (§2); confirm that's enough wherever paid randomness is offered.

## Phases

| Phase | Work | Approval gate |
|---|---|---|
| 0 | This plan, and the legal drafts | Done (2026-10-27) |
| 1 | Program: authority transfer, `cancel_roll`, encounter close; tests; autofixer; Devnet upgrade | Devnet upgrade |

**Phase 1 build (2026-10-27):**
- **Code:** 5 new instructions (43 total). New errors are 6042–6045; error codes 6000–6041 are unchanged. 23/23 unit tests pass.
- **Bundles (multi-roll) change two things:** `request_roll` gains a `pull_count` argument, and `RollRequest` changes layout (`tier` → `pull_count`, plus `tiers[20]`; 236 bytes). Every other existing instruction is unchanged.
- **Close old rolls first:** any old-layout `RollRequest` on Devnet can't be closed after the upgrade, so the 20 test rolls must be closed before it.
- **Artifact:** `chainbound_game.so` is 1,044,512 bytes, sha256 `1f4293ab1afa2d0966802633ab9102342c0e182547ca967bd91781a3d49dadba`.
- **Devnet upgrade:** needs `solana program extend 9312… 51200` first (about 0.26 SOL more rent; the current capacity is 1,001,667 bytes).
- **Autofixer:** `program_autofixer` has not run, because the Solana MCP isn't connected in this session. It's still owed; run it before the audit scope freezes.

**Phase 1 on Devnet (2026-10-27, verified from Devnet RPC):**
- **Upgrade:** first all 20 old-layout test rolls were closed (0 remain open). Then the program was extended by 51,200 bytes (data length now 1,052,912) and upgraded at slot 515,881,383 (tx `GjekHBXLGF3WAPZHRm7MtkRD81UVBXQ7VyxpupcLcAxgx1oodwG9tbggDM6UcJe48VXjNaYcr4uZ4nXci3vZkje`, finalized). The on-chain bytecode sha256 equals the build (`1f4293ab…`). The upgrade authority is still DkFm.
- **One ×10 bundle:** one VRF request produced 10 tiers (5 Common, 5 Uncommon). The account is 244 bytes, Consumed, with all 10 on-chain tiers matching the tiers recomputed from its randomness, unused entries left at 255, and the result hash set.
- **`cancel-check`:**
  - cancel while Pending → `6042 RollCancelTooEarly`;
  - cancel after fulfill → `6039 InvalidRollStatus`;
  - the roll then settled normally (Consumed, tier matches).
- **Results:** in `tools/vrf-efficacy/vrf-bundle-results.jsonl`. Close those two PDAs about an hour after consume.
- **Not yet exercised live:** the authority handoff, which waits for the Squads vault (Phase 2), and `close_encounter_request`.
| 2 | Devnet Squads rehearsal (section 1 steps); `roll.cjs --squads`; runbook | Multisig decided; each Devnet authority transfer |
| 3 | Server and Unity: two-step roll with retry and refund, status endpoint, resume coordinator, odds display, audit endpoint | — |
| 4 | Audit: scope freeze, quotes, fixes, re-review | Vendor and budget |
| 5 | Verified build, Mainnet multisig, Mainnet deploy (smoke runbook), odds with the real pool hash | Each Mainnet transaction |
| 6 | Counsel review of the Terms and Privacy drafts; dApp Store submission | Counsel sign-off |

## Decisions needed

1. ~~Multisig members, threshold and Mainnet time lock.~~ Decided 2026-10-27: 2-of-3 solo (Seeker Seed Vault, a fresh Phantom account, an offline recovery key); 24 h time lock on Mainnet, none on Devnet. See section 1.
2. `ROLL_CANCEL_DELAY_SLOTS` (proposed 150) and max attempts (proposed 2).
3. Which rolls use VRF (proposed: paid and mintable only).
4. Audit vendor and budget, and whether the Edge Function settlement is in scope.
5. Whether stamped or tradeable outputs of paid rolls need gating, pending counsel.
