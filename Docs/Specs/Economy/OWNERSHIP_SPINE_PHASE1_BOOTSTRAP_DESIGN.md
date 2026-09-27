# Ownership Spine Phase 1 — Devnet bootstrap and RAM synchronization design

Date: 2026-09-20  
Status: **DESIGN LOCKED; implementation and Devnet upgrade remain approval-gated.**  
Cluster: **Solana Devnet only.**

This design closes the new-wallet bootstrap deadlock without changing the product's starting balance
or enabling the player-build Ownership Spine. It does not authorize a program upgrade, signature,
transaction send, Unity flag change, live security matrix, or mainnet action by itself.

## Locked economy policy

| Pin | Value | Phase 1 treatment |
|---|---:|---|
| Known roll | **5 RAM** | Live; program constant and server atomic-RPC constant |
| First Stamp, Known → Stamped | **75 RAM** | Live; `OwnershipConfig.stamp_ram_cost` and server pin |
| Second Stamp / add-mod-on-Stamped | **150 RAM** | Pin only; no field, instruction, endpoint, UI, or matrix execution in this goal |
| Bind / Press | **250 RAM** | Existing `OwnershipConfig.bind_ram_cost` pin only; instruction/UI/matrix lane remains disabled |
| Daily RAM cap | **100 RAM** | Live policy/config pin; **not a starting balance** |

`PlayerProfile.ram_balance`, `daily_ram_earned`, and `ram_lifetime_earned` all start at zero.
`create_profile` must continue to grant **no RAM**. The phrase “100 RAM” means the daily award ceiling
and a separately authorized grant amount, never an automatic signup/profile-init credit.

## Accounts and PDAs

| Account | Seeds | Authority / purpose | Phase 1 state |
|---|---|---|---|
| `GameConfig` | `['game_config']` | Program authority configures result authority, schema, daily cap, RAM-per-burn, flags | Existing; cap `100`, flags `0` |
| `OwnershipConfig` | `['ownership_config']` | Program authority pins collections, first-Stamp cost, Bind pin, generator, pool hash | Existing; Stamp `75`, Bind `250` |
| `PlayerProfile` | `['player_profile', owner]` | Owner identity and authoritative on-chain RAM settlement state | Created by owner; balance starts `0` |
| `MoveVault` | `['move_vault', owner]` | Known instances pending consumption/Stamp | Created by owner; empty |
| `RamGrantReceipt` | `['ram_grant_receipt', owner, idempotency_hash]` | Immutable idempotency/audit receipt for an authority grant | New in this upgrade |
| `KnownRollReceipt` | `['known_roll_receipt', owner, known_hash]` | Existing materialization receipt | Existing |
| `StampTicket` | `['stamp_ticket', owner, operation_hash]` | Existing result-authority Stamp authorization | Existing |

The new `RamGrantReceipt` records owner, idempotency hash, amount, post-grant balance, UTC day,
result authority, and bump. The result authority pays its rent. A duplicate receipt seed fails closed,
so retrying the same grant cannot credit RAM twice.

## Daily-cap enforcement and grant paths

### Program rule

A new `grant_ram(idempotency_hash, amount)` instruction is authorized only by the
`GameConfig.result_authority`. It:

1. verifies the `PlayerProfile` PDA belongs to the supplied owner;
2. calculates the current UTC day from the Clock sysvar;
3. resets only `daily_ram_earned` when the recorded day is older;
4. requires `amount > 0` and `daily_ram_earned + amount <= GameConfig.ram_daily_cap`;
5. checked-adds the amount to `ram_balance`, `daily_ram_earned`, and `ram_lifetime_earned`;
6. creates the immutable grant receipt.

The same cap helper is applied to `burn_known_move_for_ram`; burning cannot bypass the configured
daily earning ceiling. Spending does not reduce `daily_ram_earned`, so spend-and-regrant cannot evade
the cap.

### Devnet smoke grant (live only after the upgrade gate)

The operator path is deliberately separate from profile creation:

1. fund the smoke owner with faucet Devnet SOL;
2. owner signs one initialization transaction for the missing PlayerProfile and MoveVault;
3. verify the fresh profile on-chain with `ram_balance = 0` and `daily_ram_earned = 0`;
4. call the authenticated, Devnet-only `ownership_prepare_devnet_smoke_grant` Edge Function;
5. the function requires the authenticated profile's active wallet, an approved beta-allowlist row,
   the configured exact smoke-wallet allowlist, cluster `devnet`, and grant amount `100`;
6. the function returns a result-authority-partially-signed transaction; the owner reviews, signs
   locally, simulates, and sends only after an explicit transaction approval;
7. `ownership_confirm_devnet_smoke_grant` verifies the signature and receipt/profile state, then
   records redacted SQL audit evidence.

The Edge Function never sends the transaction and never exposes the result-authority secret. The
owner key remains local. The smoke grant does not modify the product starting-balance policy.

### Future quest bounty — Phase 1.5 design hook only

A later content gate may award approximately 100 RAM through the same capped authority instruction
after a server-verifiable quest completion. This goal does **not** add the quest, completion RPC,
reward claim, UI, or player-accessible grant endpoint. Phase 1.5 must define its own replay key,
eligibility proof, award amount, and content telemetry before activation.

## Program/Supabase synchronization model

The two stores have different roles:

- `PlayerProfile.ram_balance` is authoritative for every on-chain ownership spend. A Stamp cannot
  succeed unless the program account can pay all on-chain debits.
- `GameConfig.ram_daily_cap` and program/OwnershipConfig cost checks are authoritative on-chain.
- `player_profiles.ram_balance` is the server-side gameplay mirror used by the current HTTP Known-roll
  path while Unity ownership flags are off.
- Each unmaterialized `ownership_known_moves` row represents a 5-RAM debit already applied to the
  Supabase mirror but not yet replayed on-chain. Materialization applies that same economic event to
  the chain; it is not a second player charge.

For the current smoke, Supabase began at 100 and the green Known roll produced 95. The on-chain smoke
grant establishes the independently authorized daily 100; the Stamp transaction then materializes
the existing Known for 5 and spends 75 for first Stamp, leaving 20. Confirmation must atomically
consume the Supabase Known and debit the server mirror by the same 75, also leaving 20.

The server change therefore adds an idempotent SQL confirmation RPC that locks the Stamp operation,
Known row, and profile; validates the hard pin `75`; debits exactly once; consumes the Known; and marks
the operation confirmed. A confirmed replay returns the stored result without another debit. A live
player rollout remains NO-GO until concurrency, expiry/refund, and multiple-pending-Known cases are
covered by the full matrix.

No SQL grant blindly adds 100 to the current 95 balance. The Devnet grant operation records the
chain-side authority grant and its receipt; it does not duplicate the already-established server
daily allowance.

## Spend enforcement

| Operation | Program enforcement | Server enforcement | Enabled by this gate? |
|---|---|---|---|
| Known roll | `KNOWN_ROLL_RAM_COST = 5`; checked subtraction in `roll_known_move` | `KNOWN_ROLL_RAM_COST = 5`; SQL RPC rejects any other cost | Existing/live authority path |
| First Stamp | `OwnershipConfig.stamp_ram_cost = 75`; checked subtraction in `stamp_known_move` | Config pin `75`; atomic confirm debit exactly once | Existing/live authority path |
| Second Stamp | No instruction or config field | Constant/documented pin `150` only | **No** |
| Bind / Press | Existing config value `250` | Constant/documented pin `250` only | **No; configuration only** |

The upgrade does not add a second-Stamp instruction, overload either current cost field, expose Bind,
or create Unity UI for either pinned future operation.

## Reviewed local build and capacity gate

The 2026-09-20 local build is complete and has not been deployed:

| Artifact/check | Result |
|---|---|
| SBF artifact | 741,248 bytes; SHA-256 `588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87` |
| Canonical IDL | 79,843 bytes; SHA-256 `f9740e55e8b70f1349852ffb2ba11caf76c1c853ca3f0b3bb927e16b06ecc709` |
| IDL copies | `target/idl` and checked-in `idl` byte-identical |
| Unity contract | regenerated from the canonical IDL; includes `grant_ram` and the receipt discriminator |
| Program tests | 7 passed; focused ownership Deno tests 9 passed; affected Edge modules type-check |
| Program autofixer | clean change-set result; no further call requested |

Before execution, live ProgramData was 714,357 account bytes (714,312 payload bytes). The reviewed
target was 741,293 account bytes, requiring an exact **26,936-byte** extension before upload. The
unsigned extension planner read the pinned Devnet accounts and produced:

- instruction data `0600000038690000` (`ExtendProgram`, 26,936 bytes);
- target rent 3,766,418,680 lamports; exact top-up 136,834,880 lamports;
- one required signer/payer: `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`;
- fee 5,000 lamports and projected payer balance 11,215,566,680 lamports;
- simulation `err = null`, 2,520 units, with the loader log confirming the exact extension.

The approved extension subsequently finalized at slot `501449795`, signature
`5kaDHZQvbEubehrLVwRTTQgvJw7ZBaXQKGvXTqmd7tk3kUKn6hRNH8Bxq98d2ZbiG7uMCYu9StJUj23mW4Jad64Q`.
Post-read verification proved 741,293 account bytes, 741,248 payload-capacity bytes, unchanged
authority, exact rent exemption, and a 26,936-byte zero-filled tail. Evidence:
`Logs/bootstrap-programdata-extension-verified-20260920T153924Z.json`. This changed capacity only;
buffer upload and program upgrade retain their separate approval gates.

## Rent and fee payer matrix

| Action/account | Payer | Required signer(s) | Notes |
|---|---|---|---|
| PlayerProfile initialization | Smoke player | Smoke player | Existing owner-derived PDA; verifies zero RAM after confirmation |
| MoveVault initialization | Smoke player | Smoke player | Existing owner-derived PDA |
| Grant receipt | Result authority | Result authority | Paid inside `grant_ram`; secret remains only in Edge Function |
| Grant transaction network fee | Smoke player | Result authority + smoke player | Server partial-signs; player reviews/signs locally |
| Known materialization receipt and Stamp ticket | Result authority | Result authority | Existing behavior |
| Stamped Core asset and Stamp transaction fee | Smoke player | Result authority + smoke player | Existing behavior |
| ProgramData extension/upload/upgrade | Operator/upgrade authority | Operator/upgrade authority; buffer signer where required | Separate reviewed gates |

Devnet funding checklist:

- confirm Solana CLI cluster and genesis hash are Devnet before any airdrop or transaction;
- target approximately **0.5–1 Devnet SOL** on the operator/upgrade authority;
- target approximately **0.2–0.5 Devnet SOL** on the smoke player when it pays profile, vault,
  Core-asset rent, and fees;
- use only faucet Devnet SOL—never mainnet SOL;
- re-read balances immediately before each reviewed gate;
- never paste a keypair, JWT, Helius URL, service-role key, or result-authority secret into chat/logs.

## Upgrade and initialization gates

1. **Design lock:** approve this document and the exact program/server diff.
2. **Local build:** format/test/lint, run the Solana program autofixer, generate canonical IDL and
   Unity contract metadata, and hash the `.so`.
3. **Capacity check:** compare artifact bytes with live ProgramData capacity. If larger, prepare and
   separately approve a ProgramData extension before buffer upload.
4. **Unsigned review:** output program ID, ProgramData, authority, artifact bytes/hash, required
   signers, projected fees/balances, and simulated messages. No signature or send.
5. **Upload gate:** create/resume one pinned buffer and verify its payload hash.
6. **Upgrade gate:** separately approve the pinned program upgrade, then byte-compare deployed
   ProgramData with the reviewed artifact.
7. **Server gate:** apply only the related SQL migration, deploy the smoke-grant prepare/confirm and
   updated Stamp-confirm functions, and verify `verify_jwt=true`.
8. **Config read:** verify live GameConfig cap `100`, flags `0`; OwnershipConfig Stamp `75`, Bind
   `250`; deployed program Known constant `5` through build/test evidence. No config update is needed
   if those reads match.
9. **Player init gate:** fund with Devnet faucet SOL, prepare/simulate, obtain explicit approval, then
   owner-sign PlayerProfile + MoveVault initialization.
10. **Zero-balance checkpoint:** read and validate owner, discriminator, PDA, and fresh RAM fields
    (`0/0/0`) before any grant.
11. **Smoke-grant gate:** prepare/simulate the exact 100-RAM result-authority grant, obtain explicit
    approval, owner-sign/send, then verify receipt and balance `100`.
12. **Scoped Moves smoke:** rerun keyless Stamp prepare, review, approve exactly one owner-signed
    Stamp, confirm through DAS, and verify both RAM mirrors converge to `20`.

Operator commands must be single-line WSL commands with no trailing continuation slash or stray
brace. Every signing/send stage has its own explicit approval token.

## Failure and rollback rules

- Program build/test/autofixer failure: stop before unsigned preparation.
- ProgramData capacity shortfall: stop for extension approval; do not attempt upgrade.
- Buffer upload interruption: retain signer and resume the same buffer; do not create another.
- Upgrade simulation/hash mismatch: do not sign or upgrade.
- Server deployment failure: keep grant endpoint unavailable; do not initialize/grant for smoke.
- Profile/vault partial existence: read and validate the existing account; initialize only the
  verified missing PDA in an owner-signed transaction.
- Fresh profile nonzero RAM: stop; this violates the locked starting policy.
- Grant receipt exists: treat matching confirmed state as idempotent; reject conflicting amount/key.
- Cap, cost, collection, authority, or cluster mismatch: fail closed and stop.
- Stamp transaction failure: atomic program execution leaves no partial on-chain spend/mint.
- Post-chain SQL confirmation failure: do not repeat the chain transaction; preserve signature and
  repair/replay only the idempotent confirmation RPC.
- A later rollback requires another reviewed program upgrade; never silently change authorities,
  configs, or account data.

## Explicit non-goals

- no automatic +100 RAM at signup/profile creation;
- no live quest bounty in this goal;
- no second-Stamp/add-mod instruction, endpoint, UI, or matrix lane;
- no Bind/Press activation, CPI/UI work, or matrix lane;
- no mainnet configuration or transaction;
- no Unity ownership flag change;
- no result-authority, Helius, service-role, JWT, or wallet secret in Unity/repository/logs/chat;
- no live matrix GO.

## Next GO (name only)

After this bootstrap/upgrade/smoke closes, choose exactly one separately scoped proposal:
**Phase 1.5 quest-gated RAM bounty design** or **Phase 2 second-Stamp/Bind design**. Start neither in
this goal.
