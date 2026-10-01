# Ownership Spine Phase 1 — Devnet enablement report

Date: 2026-09-19  
Decision: **NO-GO for enabling the Ownership Spine feature flags.** Both flags remain off.

This is the authoritative Phase 1 deployment record. It covers the Ownership Spine and a separate
MagicBlock VRF efficacy track. It does not authorize Archive Binding, a second-Stamp instruction,
or any fee-design change.

## Economy pins

These values are fixed before either configuration account is initialized:

| Operation | Phase 1 RAM | Gate-E/on-chain status |
|---|---:|---|
| Known roll | **5** | Program constant; not an OwnershipConfig field |
| First Stamp, Known → Stamped | **75** | `OwnershipConfig.stamp_ram_cost` |
| Second Stamp / add-mod on an already-Stamped move | **150** | Phase 1.1 pin only; deferred, with no field or instruction in this gate |
| Bind / Archives | **250** | `OwnershipConfig.bind_ram_cost`; configuration only, with Archive Binding still out of scope |
| Daily earning cap | **100** | `GameConfig.ram_daily_cap`; cap only, never an automatic starting balance |

The program and authority server do not currently implement re-stamp/add-mod-on-Stamped. The
150-RAM value is therefore a documented Phase 1.1 pin and a blocked/deferred matrix row only. It is
not encoded in either `stamp_ram_cost` or `bind_ram_cost`. Do not invent an instruction, server
endpoint, on-chain field, or Unity confirmation flow for it in Phase 1. The 250-RAM Bind value is
configuration only and does not authorize or enable Archive Binding.

The local implementation now enforces the Known-roll value in all present Phase 1 paths:

- Unity candy-machine defaults and the saved Vending config asset show 5 RAM;
- the on-chain `roll_known_move` instruction requires a matching PlayerProfile and atomically
  debits 5 RAM before granting the Known instance;
- the local Supabase migration/function use one atomic database function that locks the profile,
  rejects balances below 5, and does not charge again on an idempotent replay.

First Stamp remains 75 in program/server/Unity configuration. Economy/fee design is not part of
the MagicBlock VRF track.

## Verified local build

| Tool or check | Result |
|---|---|
| Solana CLI | `solana-cli 2.3.0` |
| Anchor | `anchor-cli 0.32.1` |
| SBF platform tools | **v1.52**, required because the pinned MagicBlock SDK graph needs Rust 1.89 |
| Anchor program ID | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` |
| Rust host tests | **5 passed, 0 failed** |
| TypeScript typecheck / focused generated-IDL tests | passed / **2 passed, 0 failed** |
| Supabase focused runtime tests | **4 passed, 0 failed** with Deno `--no-check`; the repository's existing Node Buffer/Deno type mismatch still prevents a checked Deno run |
| Unity compilation | live Editor ready, zero current C# compile errors |
| Unity deterministic VRF | live Editor evaluator verified 32 bytes, first consume = Consumed, second consume = Failed; focused NUnit tests were also added/compiled, but the live Pipeline session did not load tests from the `UNITY_INCLUDE_TESTS` assembly, so those NUnit cases are not claimed as executed |

Current Phase 1 + MagicBlock artifact:

- SBF bytes: **714,312**
- SHA-256: **`58e78eb59c34b701c50729fdbdd77677232bec12bce1b6e9e0ae7e2a32d2a2ad`**
- generated/checked-in IDL: **74,732 bytes**, byte-identical, SHA-256
  **`c48ea5b3de8c42248cc6880dd844c753b4864a30dcc17eb36da1384e5e1a3a69`**
- required ProgramData allocation, including the 45-byte upgradeable-loader header: **714,357**
- generated IDL includes the scoped request, callback, consume instruction, EncounterRequest account,
  and the PlayerProfile account on `roll_known_move`;
- the checked-in Unity contract was regenerated from that IDL.

The v1.52 SBF compiler returned success but emitted the existing mpl-core diagnostic:
`registry_records_to_plugin_list` reached stack offset 4,184 (88 bytes beyond the 4,096 offset
limit; estimated frame size 4,224). The live Core CPI rows remain required matrix gates. Do not
describe this build as warning-free.

## Current Devnet state

State was independently reverified from Devnet RPC after the approved pinned-program Upgrade
finalized on 2026-09-18 and after the bootstrap capacity extension finalized on 2026-09-20.

| Item | Value / state |
|---|---|
| Cluster | `devnet` |
| Program | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` |
| Program owner | `BPFLoaderUpgradeab1e11111111111111111111111` |
| ProgramData | `5wqBqX7qubopQnteX5RPTwBJrSFA6xnMs9Zbwz2BHfzD` |
| Upgrade authority / payer | `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG` |
| Authority balance | **11.35241156 SOL** after OwnershipConfig initialization (the wallet was externally funded after the program Upgrade) |
| Executable state | Prior binary remains active; bootstrap artifact is **not upgraded yet** |
| Live ProgramData capacity / allocation | **741,248 payload bytes / 741,293 account bytes** after the finalized 26,936-byte extension |
| Live ProgramData lamports | **3,766,418,680** (rent exempt) |
| Prior deployed binary SHA-256 | `58e78eb59c34b701c50729fdbdd77677232bec12bce1b6e9e0ae7e2a32d2a2ad` — byte-verified before the capacity-only extension |
| Upgrade signature | `3N75R7yfgY6xJSABuLBx3YkLkMurBCCNC2w2Z8wVUxEdiswBj1bq6fpAgbxsj3nQbV1zEMDvkdtXSb59i5tAvfin` (**finalized**) |
| Buffer close/refund | same atomic Upgrade signature; `B5w4…EkdDY` no longer exists |
| GameConfig PDA | `FdA2EJ89Xtqn6Z1pxayBQnSJQ8pVpVAuEjaUW8qbU8nx` — initialized and independently verified |
| OwnershipConfig PDA | `6Gj8af5cnLSKFWcdfFLr7fihhNmp97BdPRt76zWemPPZ` — initialized and independently verified |
| Core authority PDA | `FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys` |
| Trusted collections | all four created and independently verified; addresses and signatures below |
| Ownership feature flags | **OFF** |

The pinned program now runs the reviewed Phase 1 artifact. Four separately approved Core collection
creates and the separately approved GameConfig and OwnershipConfig initializations have finalized.
Supabase authority tables, public ownership secrets, and ownership function v3 are live as of
2026-09-19; Unity ownership feature flags remain **OFF** and no on-chain re-init was sent.

### Trusted Metaplex Core collections

All four collections are owned by the MPL Core program
`CoREENxT6tW1HoK8ypY1SxRMZTcVPm7R94rH4PZNhX7d` and use the Chainbound Core authority PDA
`FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys` as update authority. Independent RPC reads decoded
the expected name and URI from every account and confirmed all four create signatures finalized.

| Role | Collection | Create signature | Finalized slot |
|---|---|---|---:|
| Moves | `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC` | `2Zoq66iWT1qEFNcyPQQAd1465GTr9ZgpKCsL8MNWuebY4byHujaEYwhaPwfwuVnRXPmx1KLj5YXkJh84gkEvstdq` | 500,693,407 |
| Archives | `DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U` | `3Pu3feqqPz6Gv8FZ2n5qoH5gqoyeRsZtpq3xJUgqVqswfwZi4FrmmyUTRdiVHw7SSps1swENEdKVExrx7NpSro8a` | 500,693,412 |
| Covers | `6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs` | `5XrCkvvZirQKH98W7fZWAYXNJynAs3LoeJCYrSbWjRh47rjY4i1JcCsXL7QNtL3qQd4HtqwoBcLYyNjenTDMsRzm` | 500,693,417 |
| Cosmetics | `76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV` | `7P8MRHtrvtuPPRAMRExAwGRFSRwaei7zabndVGKSeXHAU4iymB9C1Dm1YLCTWpCbzna9jsNm2iUEiN9nUrY8Sev` | 500,693,422 |

### GameConfig initialization

Gate D finalized at slot **500,698,363** with signature
`NmgbtYdgKhwtcBGLPr56fd7VFdq1mMsRwE7BvRc6qC4wB1qMzMig2g8zzegSQD9pBnXMS1jkrRnanXYiffkC2Wp`
and a 5,000-lamport fee. The PDA holds 1,102,360 rent-exempt lamports and is owned by the pinned
game program. Independent transaction and account verification confirmed:

- program authority `DkFmzzzF89tYZu2JWR1uq3H13U5np9NU6MHF4E61gJmG`;
- Gate D initially encoded `FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF`; the approved
  finalized rotation below corrected the index-25 case typo to
  `FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF`;
- schema version 1, RAM daily cap 100, RAM per burn 10;
- instruction flags `0` (all Ownership feature flags remain OFF);
- PDA bump 253 and the exact approved `initialize` instruction bytes.

The local generated deploy keypair resolves to `B7fXnhUe8kPG4PZttSdDaJcQm7JDpJUpZnBywhdMpB1Q`,
not the pinned program. It must never be allowed to select the deploy target.

### OwnershipConfig initialization

Gate E finalized at slot **500,705,191** with signature
`4Cq39Xz8d9pCfeT1i89375FqxzsonWDxGxwS4U4oiHPRndwpLmSgKUvDkJLGXpj4pq59oyjZETQxG4np6eSwhfp6`
and a 5,000-lamport fee. The 183-byte PDA holds 1,579,880 rent-exempt lamports and is owned by the
pinned game program. Independent transaction-byte and account verification confirmed:

- schema version 1 and PDA bump 255;
- the exact four trusted collection addresses listed above;
- `stamp_ram_cost = 75` and `bind_ram_cost = 250`;
- generator version 1 and pool hash
  `6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4`;
- no second-Stamp cost field or instruction. The 150-RAM pin remains documented/deferred only.

## Completed A1 ProgramData extension

For the reviewed artifact and completed extension:

- additional ProgramData bytes: **714,357 − 184,957 = 529,400 bytes**;
- rent-exempt minimum for 714,357 bytes: **3,629,583,800 lamports**;
- pre-extension ProgramData balance: **1,288,191,600 lamports**;
- exact rent top-up: **2,341,392,200 lamports**;
- finalized authority balance: **5.363977 SOL**, including the 5,000-lamport transaction fee;
- the 714,349-byte upgrade buffer has a **3,629,543,160-lamport** rent-exempt minimum,
  but Agave v2.3.0 `write-buffer` temporarily funds it with **3,629,583,800 lamports**
  using the 714,357-byte ProgramData rent basis; the full deposit is refundable during Upgrade.

The ProgramData extension transaction targets only:

- program `9312…2uUQ`;
- ProgramData `5wq…HfzD`;
- rent payer `DkFm…61gJmG` (this wallet is also the upgrade authority, but
  `ExtendProgram` does not take or verify an authority account);
- the upgradeable loader and System Program;
- 529,400 additional bytes and 2,341,392,200 rent lamports.

The reproducible unsigned builder is
`backend/chainbound_game/scripts/prepare-program-extend-unsigned.cjs`. It reads no keypair and cannot
sign or submit. Its 2026-09-18 Devnet snapshot produced:

- instruction data `06000000f8130800` (`ExtendProgram`, 529,400 bytes);
- one required signer: `DkFm…61gJmG`;
- exact RPC fee: **5,000 lamports**;
- serialized message: 212 bytes, SHA-256
  `71a24529d7e72f5ddcd0e50f1727d5e6725485165e1ce59484b1a5c9894fa0d9`;
- simulation: `err = null`, **2,520 compute units**, with the loader log confirming a 529,400-byte
  ProgramData extension.

The operator approved the exact boundary and executed the guarded Agave 2.3.0 CLI command in the
signer-enabled WSL shell. Independent RPC verification produced:

- signature `5fHYvNK15JmxFAyHU7tGYHXb1FaQKGdFW7DCMQd2bCJ6xkBx7NJBo78812W6sguwpjskyHxjDtHd4xp3qdrMiZUi`;
- finalized slot **500,497,293**, block time **2026-09-18 19:52:33 UTC**;
- transaction error `null`; fee **5,000 lamports**;
- exact account order: payer, ProgramData, pinned program, System Program, upgradeable loader;
- exact instruction data `06000000f8130800` and account indices `[1, 2, 3, 0]`;
- loader log: `Extended ProgramData account by 529400 bytes`;
- post-state: 714,312-byte payload capacity, 714,357-byte account allocation,
  3,629,583,800 lamports, and unchanged authority `DkFm…61gJmG`.

The extension alone did not replace the executable; the separately approved Gate A upload and
Gate B Upgrade below completed the pinned-program replacement.
The reproducible upload planner is
`backend/chainbound_game/scripts/prepare-program-upgrade-unsigned.cjs`. It accepts an
operator-generated buffer public key through `BUFFER_PUBKEY` without reading its secret (or uses a
throwaway public key in memory when none is supplied), and cannot sign or submit. The reviewed
Agave v2.3.0 path and its 2026-09-18 Devnet sample establish:

- buffer allocation: **714,349 bytes**; Agave v2.3.0 temporarily funds it with
  **3,629,583,800 lamports**, using the 714,357-byte ProgramData rent basis;
- buffer creation: two signers (`DkFm…61gJmG` and the future ephemeral buffer signer), exact fee
  **10,000 lamports**, `InitializeBuffer` data `00000000`, simulation `err = null`;
- upload: **706** `Write` transactions, **1,012 bytes** per full chunk, final offset **713,460** and
  final chunk **852 bytes**; `DkFm…61gJmG` is the only write signer;
- per-write fee **5,000 lamports**, all-write fees **3,530,000 lamports**;
- final `Upgrade` data `03000000`, one signer (`DkFm…61gJmG`), fee **5,000 lamports**;
- upload-and-upgrade fees: **3,545,000 lamports**;
- combined extension, upload, and upgrade fees: **3,550,000 lamports**;
- lowest projected payer balance before the upgrade refunds buffer rent:
  **1,730,848,200 lamports**; projected final balance after refund: **5,360,432,000 lamports**.

The final Upgrade account order is ProgramData, pinned program, buffer, spill payer, Rent sysvar,
Clock sysvar, and upgrade-authority signer; the instruction program is the upgradeable loader. The
loader drains the buffer on success and spills excess lamports to the payer. Gate A created buffer
`B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY` and completed all **707** successful
transactions with no failures. Create signature:
`5W3LLigdSX2CaUDPyiqCuxpYRRMrph8d3ehMP1mqWydMtNxMszdTtS4Ekuav6UBzMEN3jZ8CTcJ7jTXSRdR2og2J`;
latest write signature:
`5VGjkYvyMGs65R4TwrjsRXG9MCgy4SmxPEzUo5MaBJo59nm5cQRrE8jFPkEnt4krGDsBq7kSC8nFqsnX8Lsj9A2i`.
The buffer is owned by the upgradeable loader, retains authority `DkFm…61gJmG`, contains exactly
714,312 payload bytes matching SHA-256 `58e78eb5…a2ad`, and holds 3,629,583,800 refundable lamports.
The payer balance after upload was 1,730,853,200 lamports.

Gate B used the dedicated `solana program upgrade <BUFFER> <PROGRAM>` path, not `program deploy`.
Its unsigned simulation returned `err = null`, consumed 2,370 compute units, required only
`DkFm…61gJmG`, and targeted Upgrade instruction data `03000000`. The finalized Upgrade signature is
`3N75R7yfgY6xJSABuLBx3YkLkMurBCCNC2w2Z8wVUxEdiswBj1bq6fpAgbxsj3nQbV1zEMDvkdtXSb59i5tAvfin`
at slot **500,558,952**, with a 5,000-lamport fee and no transaction error. The loader atomically
drained `B5w4…EkdDY`, so that same signature is the buffer close/refund signature; the buffer account
is absent after Upgrade. The final payer balance is **5,360,432,000 lamports**.

Deployed bytes were verified by two independent methods:

1. `solana program dump 9312…2uUQ Logs/chainbound-game-deployed-20260918T224222Z.so` produced
   exactly 714,312 bytes with SHA-256 `58e78eb5…a2ad`.
2. `verify-program-upgrade.cjs` fetched ProgramData, removed the 45-byte loader metadata header,
   and byte-compared and SHA-256 hashed the payload against the reviewed local artifact; it was an
   exact match.

## Remaining configuration decisions and follow-up inputs

The result-authority public key, four distinct trusted collections, generator version, and modifier
pool hash are now fixed. The result-authority secret is set in Supabase; its value remains redacted
and must never enter this report.
The operator selected `bind_ram_cost = 250` as configuration only. This does not authorize or enable
Archive Binding. The remaining follow-up inputs outside the on-chain configuration gates are:

- isolated funded test wallets and the profile/MoveVault bootstrap plan.

### Result-authority rotation and secret status

The approved GameConfig rotation changed only `result_authority` from the Gate D case typo
`FaAegH2tBCyDzxXcXyz64qEFWsrvpX8M6SSFNx1Pt5KF` to the operator-held keypair
`FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF`. It finalized at slot **500,772,832** with
signature
`5ByzaVkZ9TR546P82VLSw46as9XwGvFgSdqRSsY9CxG6JKUuNDJH5qrryp8fFc6aTvNsvuRDAGcm2wwUs3XANRxZ`.
Independent verification confirmed every other GameConfig field was unchanged. The Supabase
secret `CHAINBOUND_RESULT_AUTHORITY_SECRET` is now **set (value redacted)** and matches this
on-chain public key.

The deterministic generator pin is `1`. The canonical modifier pool hash is
`6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4`; it covers 104 catalog rows
using the documented canonical binary layout. The first-Stamp cost remains 75 RAM. The source
requires all four collection addresses, and every collection now uses the verified program-derived
Core authority PDA rather than an operator wallet.

### Modifier-pool hash v1

Source: `Docs/Specs/Combat/Data/Modifiers.csv`. Blank lines and lines whose first non-whitespace
character is `#` are excluded. Non-comment data-row order is preserved. Values are trimmed at the
outer edges, normalized to Unicode NFC, and encoded as UTF-8. Missing trailing CSV values normalize
to the empty string. The display-only `DisplayName` and `EffectDescription` columns are excluded.

The fixed mechanical field order is:

1. `SectionId`, `Category`, `Scope`, `MinRarity`, `EffectMin`, `EffectMax`;
2. `AllowedCategories`, `ForbiddenCombos`, `SlotCost`, `IsTypeInfusion`, `IsStatSub`, `SubStat`;
3. `TriggerCondition`, `TriggerChance`, `PowerCap`, `APCostCap`, `ForbiddenTags`, `SlotType`, `Weight`.

Canonical binary layout, in order:

1. UTF-8 bytes for `chainbound.modifier_pool\0`;
2. format version as `u32` little-endian (`1`);
3. generator version as `u32` little-endian (`1`);
4. field count as `u16` little-endian;
5. each field name as `u16` little-endian byte length followed by UTF-8 bytes;
6. row count as `u32` little-endian;
7. for every row and fixed-order field, `u32` little-endian byte length followed by UTF-8 bytes.

Reproducible results:

- source CSV SHA-256: `9ea20b15a826af7e2928922e655f3ca85b9b76de9d11a449f25b015efb7fc14f`;
- rows: **104**; canonical byte length: **15,973**;
- generator version: **1**;
- pool SHA-256: `6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4`;
- Anchor `[u8; 32]`: `[108, 234, 92, 229, 105, 68, 168, 132, 134, 202, 251, 181, 104, 166, 40, 181, 170, 86, 100, 195, 219, 101, 42, 121, 205, 144, 1, 100, 39, 170, 164, 180]`.

`backend/chainbound_game/scripts/compute-modifier-pool-hash.cjs` implements this layout. A separate
PowerShell implementation reproduced the same row count, byte length, and hash.

## Supabase state (2026-09-19)

Project `jhzqcbrzkausrrtcavuu`. On-chain Gates A–E were not touched. Unity ownership flags remain **OFF**.
`supabase db push --include-all` was not used.

### Migration ledger

Reviewed SQL remains under `Supabase/migrations`. The linked CLI layout is `Supabase/supabase/migrations`
and had no files. Only
`20260916010000_ownership_spine_phase1_authority.sql` was copied into the CLI folder.
Folios (`20260909010000`) and beta allowlist (`20260912010000`) were left unapplied. Historical local
timestamps that already exist remotely under different versions (`20260904010000` vs `20260904075913`,
`20260906010000` vs `20260906050103`) were not copied.

MCP `apply_migration` applied the reviewed SQL as remote version **`20260919064343`**
(`ownership_spine_phase1_authority`). The CLI copy was renamed to
`Supabase/supabase/migrations/20260919064343_ownership_spine_phase1_authority.sql` so a later
`migration list` cannot double-apply it.

Post-apply checks:

- `ownership_known_moves` and `ownership_stamp_operations` exist with RLS enabled and **zero** policies
- `anon` / `authenticated` have no table privileges; PostgREST `select` returns `42501` / HTTP 401
- `ownership_roll_known_atomic` is `service_role` execute-only; `p_ram_cost <> 5` raises `invalid_known_roll_ram_cost`

### Generator / catalog

`backend/chainbound_game/scripts/compute-modifier-pool-hash.cjs` on current
`Docs/Specs/Combat/Data/Modifiers.csv` reproduced **104** rows, canonical length **15,973**, CSV SHA
`9ea20b15a826af7e2928922e655f3ca85b9b76de9d11a449f25b015efb7fc14f`, and pool hash
`6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4` (matches on-chain). The legacy
~56-ID Stamp subset was replaced by generated
`Supabase/functions/_shared/modifier_pool.generated.ts` (CSV-order sectionIds, including Pass A).
Deno `--no-check` focused ownership tests: **9 passed, 0 failed** on 2026-09-21. The permission set
was limited to source reads plus the four non-secret environment names imported by the compatibility
layer; no credentials were printed or required.

### Secret store (names only)

Dashboard: https://supabase.com/dashboard/project/jhzqcbrzkausrrtcavuu/settings/functions

| Exact name | Status | Notes |
|---|---|---|
| `CHAINBOUND_RESULT_AUTHORITY_SECRET` | **set (value redacted)** | 64-byte JSON-array secret for `FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF`; verified by authenticated HTTP smoke. |
| `CHAINBOUND_OWNERSHIP_GENERATOR_VERSION` | set | `1` |
| `CHAINBOUND_OWNERSHIP_MODIFIER_POOL_HASH` | set | `6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4` |
| `CHAINBOUND_OWNERSHIP_STAMP_RAM_COST` | set | `75` |
| `CHAINBOUND_OWNERSHIP_BIND_RAM_COST` | set | `250` (config-only; Archive Binding still unauthorized) |
| `CHAINBOUND_OWNERSHIP_SCHEMA_VERSION` | set | `1` |
| `CHAINBOUND_OWNERSHIP_TICKET_TTL_SECONDS` | set | `900` |
| `CHAINBOUND_OWNERSHIP_METADATA_URI_BASE` | set | `https://jhzqcbrzkausrrtcavuu.supabase.co/storage/v1/object/public/chainbound-ownership-metadata` (prepare embeds `{base}/stamped/{asset}.json`; no bucket required for this slice) |
| `CHAINBOUND_DEVNET_SMOKE_WALLETS` | **set; SHA-256 verified** | JSON allowlist containing desktop wallet `H68e...bBWq` and Seeker wallet `9zMw...VHUj`; public keys only, not secrets. Prepare/Confirm v4/v6 deployed 2026-09-26. |
| `CHAINBOUND_DEVNET_SMOKE_WALLET` | set (legacy fallback) | Original exact desktop-wallet pin retained temporarily for rollback compatibility; ignored while the plural setting is present. Historical evidence: `Logs/bootstrap-smoke-wallet-env-pin-20260921T022426Z.json` |
| `CHAINBOUND_CORE_MOVES_COLLECTION` | set | `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC` |
| `CHAINBOUND_CORE_ARCHIVES_COLLECTION` | set | `DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U` |
| `CHAINBOUND_CORE_COVERS_COLLECTION` | set | `6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs` |
| `CHAINBOUND_CORE_COSMETICS_COLLECTION` | set | `76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV` |
| `CHAINBOUND_CORE_MOVES_UPDATE_AUTHORITY` | set | `FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys` |
| `CHAINBOUND_CORE_COSMETICS_UPDATE_AUTHORITIES` | set | same PDA |
| `CHAINBOUND_SOLANA_CLUSTER` | set | `devnet` |
| `CHAINBOUND_GAME_PROGRAM_ID` | set | `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` |
| `CHAINBOUND_SOLANA_RPC_ENDPOINT` | set | `https://api.devnet.solana.com` |
| `CHAINBOUND_DAS_RPC_URL` | set | `https://api.devnet.solana.com` (public RPC; DAS not assumed) |
| `HELIUS_DAS_URL` | **set (value redacted)** | Name-only verification completed 2026-09-19; server-only Devnet Helius HTTPS URL, never stored in Unity or logs. |
| `SUPABASE_URL` / `SUPABASE_SERVICE_ROLE_KEY` | already set | values not printed |

Known roll **5 RAM** is `KNOWN_ROLL_RAM_COST` plus `ownership_roll_known_atomic`; it is not a secret.

### Function deploy

Ownership functions remain ACTIVE with `verify_jwt=true`. The current remote revisions and deployed
bundle hashes were read back after setting the public smoke-wallet environment pin; Solana
transaction RPC remains `CHAINBOUND_SOLANA_RPC_ENDPOINT`. Full evidence:
`Logs/ownership-bootstrap-current-state-audit-20260921T022849Z.json`.

| Function | Version | Status |
|---|---:|---|
| `ownership_roll_known` | 6 | ACTIVE; JWT enforced |
| `ownership_prepare_stamp` | 7 | ACTIVE; JWT enforced |
| `ownership_confirm_stamp` | 8 | ACTIVE; JWT enforced |
| `ownership_get_inventory` | 7 | ACTIVE; JWT enforced |
| `ownership_das_proxy` | 3 | ACTIVE; JWT enforced |
| `ownership_prepare_devnet_smoke_grant` | 2 | ACTIVE; JWT enforced |
| `ownership_confirm_devnet_smoke_grant` | 2 | ACTIVE; JWT enforced |

### Scoped smokes (Unity flags OFF; live matrix not run)

Isolated SQL smoke profiles were created, used, and deleted. JWTs and secrets were redacted. The
post-rotation authenticated smoke used an operator-local JWT; no token or secret was recorded.

| Case | Result |
|---|---|
| Anon PostgREST `select` on ownership tables | **401** `42501 permission denied` |
| `ownership_roll_known` / `ownership_prepare_stamp` without JWT | **401** `UNAUTHORIZED_NO_AUTH_HEADER` |
| Authenticated `ownership_roll_known` before secret set | **503** `missing_env:CHAINBOUND_RESULT_AUTHORITY_SECRET` (historical fail-closed check) |
| Authenticated `ownership_roll_known` after secret set | **HTTP 200**, `success=true`, `replayed=false`; Known `9293fc49-b5d0-48d9-b071-504bdc9b3b00`, `baseline_slam`, generator `1`, pool hash present; **5 RAM** charged, balance **100 → 95** |
| Post-secret spoofed `player_profile_id` | **HTTP 401** `authenticated_profile_not_found` |
| `ownership_das_proxy` without JWT | **HTTP 401** `UNAUTHORIZED_NO_AUTH_HEADER` |
| Authenticated DAS `getAsset` for Moves collection | **HTTP 200**, `success=true`, `interface=MplCoreCollection` |
| Authenticated DAS `getAssetsByGroup` for Moves collection | **HTTP 200**, `success=true`, `total=0`; requested limit 999 clamped to **50** |
| Authenticated DAS disallowed `sendTransaction` | **HTTP 400** `das_method_not_allowed` |
| Authenticated DAS wrong collection | **HTTP 400** `collection_not_allowed` |
| Body `player_profile_id` spoof | **401** `authenticated_profile_not_found` |
| `ownership_prepare_stamp` unlinked wallet | **403** `wallet_not_linked_to_profile` |
| `ownership_prepare_stamp` linked smoke wallet | `move_vault_not_initialized`; read-only Devnet audit also found no PlayerProfile and a 0-lamport wallet |
| Atomic Known roll (service RPC) | **5** RAM debit, `generator_version=1`, pool hash pin, `replayed=false` |
| Atomic idempotent replay | same Known id, `replayed=true`, **no second debit** |
| Atomic insufficient RAM (balance 3) | `insufficient_ram`; no Known row; balance unchanged |
| Atomic `p_ram_cost=4` | `invalid_known_roll_ram_cost` |

HTTP Known success and the authenticated DAS proxy allow-list smokes are green. The full isolated
live matrix remains blocked/NO-GO. One separately approved Devnet Moves smoke Stamp is being prepared
through the existing `ownership_prepare_stamp` -> owner-sign -> `ownership_confirm_stamp` path; it
does not authorize any second transaction, feature flag, or general matrix run.

### Unity Devnet read configuration (completed; flags OFF)

The saved Unity `SolanaConfig.asset` now contains only client-safe public Devnet configuration:

- cluster `devnet`;
- RPC `https://api.devnet.solana.com`;
- WebSocket `wss://api.devnet.solana.com`;
- DAS RPC `https://api.devnet.solana.com`;
- Moves collection `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC`;
- expected Core authority `FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys`.

The public endpoint returned a valid `getAssetsByGroup` response and resolved the collection through
`getAsset` as `MplCoreCollection` with the expected authority. No Helius/admin credential,
result-authority secret, service-role key, or other private material is present in the shipping
Unity configuration. `CandyMachinePanel.prefab` and `GameServices.prefab` both still serialize
`enableDevnetOwnershipSpine: 0`; this wiring enables no live roll, Stamp, mint, or ownership path.
Production ownership operations remain gated behind the Edge Functions.

The server-side Helius proxy is deployed as `ownership_das_proxy`. It accepts only `getAsset`,
Moves-pinned `getAssetsByGroup`, and profile-wallet-bound `getAssetsByOwner`; limit is clamped to 50.
Provider failures collapse to HTTP 502 `das_upstream` without returning provider details. Evidence:
`Logs/ownership-das-proxy-smoke-20260919T210717Z.log` and
`Logs/ownership-das-proxy-secret-set-20260919T211852Z.log`. Authenticated success/rejection evidence:
`Logs/ownership-das-proxy-auth-smoke-20260919T213308Z.log`. Unity continues to hold only the public
Devnet RPC/DAS URL; the Helius credential remains server-only.

The authenticated proxy evidence confirms: `getAsset` HTTP 200 for the Moves
`MplCoreCollection`, `getAssetsByGroup` HTTP 200 with `limit=50` and `total=0`, disallowed
`sendTransaction` HTTP 400 `das_method_not_allowed`, and a wrong collection HTTP 400
`collection_not_allowed`.

### Unity authenticated DAS proxy client (implemented; Editor evidence green)

`OwnershipDasProxyClient` calls only the pinned Devnet `ownership_das_proxy` function. It uses the
existing public anon `apikey` plus the process-memory authenticated user access JWT, implements
`getAsset` and Moves-pinned `getAssetsByGroup`, clamps inventory requests to 50, validates asset IDs,
collection membership, interface, and update authority, and caps responses at 2 MiB. It does not
read or store `HELIUS_DAS_URL`, a service-role key, or the result-authority secret.

Editor menu **Chainbound -> Ownership -> Probe Moves via Supabase DAS proxy (authenticated)** writes
a redacted `Logs/ownership-das-proxy-unity-*.log` after an in-Editor authenticated session exists.
The authenticated Editor probe passed on 2026-09-19 and wrote
`Logs/ownership-das-proxy-unity-20260919T222028Z.log`: `getAsset` returned HTTP 200 for the pinned
`MplCoreCollection` with `authorityMatched=true`; `getAssetsByGroup` returned HTTP 200, page 1,
limit 50, `reportedTotal=0`, and no dropped items. The log explicitly redacts the user JWT and anon
key, contains no provider URL or credential value, and confirms that no flag, mint, Stamp, or roll was
invoked. The earlier authenticated CLI evidence remains at
`Logs/ownership-das-proxy-auth-smoke-20260919T213308Z.log`.

Five focused parser tests are checked in and discovered by the Unity runner, but their execution timed
out; this is not counted as a test pass. The successful real Editor probe is the runtime evidence for
this goal.

The one-mint operator helper is
`backend/chainbound_game/scripts/ownership-moves-smoke-mint.cjs`. Prepare mode is keyless: it validates
the server-partially-signed transaction, pinned programs/accounts, Known instance, 75-RAM cost, absent
predicted asset, and unsigned simulation without sending. Execute mode requires the exact approval
token and a local linked-wallet keypair, signs locally, simulates with all signatures, sends exactly
one transaction, confirms it, then verifies the asset through the proxy before confirming the Stamp
in the database. Credential values and keypair bytes are never written to evidence.

**Current operator gate (2026-09-20):** the linked smoke wallet's PlayerProfile and MoveVault are now
initialized on Devnet. The bootstrap design is locked
in [OWNERSHIP_SPINE_PHASE1_BOOTSTRAP_DESIGN.md](OWNERSHIP_SPINE_PHASE1_BOOTSTRAP_DESIGN.md), and the
local implementation adds a result-authority `grant_ram` instruction with an immutable receipt and
the same 100-RAM daily-cap enforcement used by burn awards. Profile creation still initializes
`ram_balance = 0`; the new instruction does not auto-credit signup.

After the program upgrade, the owner bootstrap helper simulated and the operator signed one transaction
containing only `create_profile` and `create_move_vault` for `H68e...bBWq`. It created PlayerProfile
`HRFpLiWfYNiSQt4mVwpcBSStZHBKhfFVXNBhP8nhioQa` and MoveVault
`8Whfu75D1V3EP7Nt99qCGzvXuXRk1oBZRGRECAN1J3YH`. Simulation consumed 21,781 units. Signature
`2JekRfswoRpZnr5QNGaTb4oaeXfLCxwS6g84e7CY6Kww1ea5Va2mpy9sF2CkeJ4uhHoyQWrYXYbC2XVGu8u37aci`
finalized at slot `501665505` with no transaction error and a 5,000-lamport fee. Independent live
readback confirmed the profile owner and `ram_balance = 0`, `daily_ram_earned = 0`, and
`ram_lifetime_earned = 0`; the empty vault has capacity 50, count 0, vector count 0, and next instance
ID 1. Operator evidence: `Logs/ownership-player-bootstrap-20260921T013435Z.json`; independent
readback: `Logs/ownership-player-bootstrap-20260921T013651Z.json`; combined verification:
`Logs/ownership-player-bootstrap-verified-20260921T013725Z.json`. Faucet funding paid only Devnet
rent/fees and did not alter the zero-start RAM policy.

The new local SBF is 741,248 bytes. The operator-approved 26,936-byte ProgramData extension finalized
on Devnet at slot `501449795`, signature
`5kaDHZQvbEubehrLVwRTTQgvJw7ZBaXQKGvXTqmd7tk3kUKn6hRNH8Bxq98d2ZbiG7uMCYu9StJUj23mW4Jad64Q`.
Independent verification found a 741,293-byte account, 741,248-byte payload capacity, exactly 26,936
zero-filled new bytes, rent-exempt balance 3,766,418,680 lamports, and unchanged upgrade authority.
Redacted evidence is in `Logs/bootstrap-programdata-extension-verified-20260920T153924Z.json`.

The extension itself was capacity-only and did not replace the executable. Gate A uploaded and
independently verified the pinned buffer, Gate B upgraded and independently verified the live
executable, and the exact-only Supabase server gate is now complete. The player bootstrap has its
separate explicit approval and is now finalized with the fresh zero-balance checkpoint verified.
The next gate is prepare/review of exactly one 100-RAM Devnet smoke grant; do not sign/send it without
the separate `APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100` token, and do not rerun Stamp prepare first.
Prepare, Execute, and keyless Recover wrappers are syntax-valid; Execute and Recover were proven to
reject missing approval with exit 64 before prompting or touching the network. Evidence:
`Logs/ownership-bootstrap-ram-grant-wrapper-audit-20260921T023208Z.json`.
No mint or inventory increase is claimed; proxy inventory remains `total=0`, Unity flags remain OFF,
and the live matrix remains NO-GO.

The post-extension unsigned sizing pass is green with a throwaway public key: the selected artifact
requires a 741,285-byte buffer funded with a temporary 3,766,418,680-lamport deposit, 733 writes, and
3,680,000 projected upload-plus-upgrade fee lamports. Its create simulation returned `err=null`.
Because the sample key was not retained, it is not an upload authorization or usable Gate A target.
The final Gate A review must bind the same artifact hash to a locally retained buffer public key.

That final review now binds the artifact to retained signer public key
`B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY`. Its prior buffer account was atomically closed by
the earlier upgrade and is currently absent on Devnet, so the address can be recreated only with the
retained signer. Gate A requires a temporary 3,766,418,680-lamport deposit plus 3,675,000 fee
lamports across buffer creation and 733 writes at the no-retry base estimate; retry transactions may
increase actual fees. The payer projects to 7,445,473,000 lamports before
the deposit is refunded by a separately approved Gate B upgrade. The create simulation returned
`err=null` at 2,520 units. Redacted, unsigned evidence is
`Logs/bootstrap-gate-a-unsigned-plan-20260920T160330Z.json`; no secret bytes were read or recorded.
The guarded wrapper `backend/chainbound_game/scripts/run-bootstrap-gate-a-upload.sh` is prepared but
Gate A approval was granted on 2026-09-20 for buffer `B5w4…EkdDY`. The operator ran the wrapper; it
created the correctly sized/funded buffer, then public-RPC bulk upload ended with
`Max retries exceeded`. No Upgrade ran. Read-only comparison shows 31 of 733 artifact chunks match
and 702 remain; the first and last remaining writes both simulate with `err=null`. Evidence is in
`Logs/bootstrap-gate-a-upload-20260920T184039Z.log` and
`Logs/bootstrap-gate-a-resume-plan-20260920T185258Z.json`.

The resume-only wrapper `backend/chainbound_game/scripts/run-bootstrap-gate-a-resume.sh` then
completed the 702 remaining writes using direct QUIC/TPU submission. Independent verification found
all **734** Gate A transactions (one create plus 733 writes) successful and no failed transaction;
the latest write landed at slot `501523347`. The live buffer is owned by the upgradeable loader,
retains authority `DkFm…61gJmG`, is 741,285 account bytes with a 741,248-byte payload, holds
3,766,418,680 refundable lamports, and byte-matches artifact SHA-256
`588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87`. No Upgrade ran. Redacted
evidence is in `Logs/bootstrap-gate-a-resume-20260920T190155Z.log` and
`Logs/bootstrap-gate-a-buffer-verified-20260920T190155Z.json`.

Gate B remains separate from Gate A. The operator granted exact approval on 2026-09-20 for the
Upgrade from buffer `B5w4…EkdDY` into program `9312…2uUQ`. Its unsigned one-transaction plan uses the dedicated
`solana program upgrade <BUFFER> <PROGRAM>` path, requires only `DkFm…61gJmG`, has instruction data
`03000000`, a 5,000-lamport fee, and simulated with `err=null` at 2,370 units. The reviewed outcome
was an atomic buffer drain/close and return of its 3,766,418,680 lamports to the payer. Evidence:
`Logs/bootstrap-gate-b-unsigned-plan-20260920T190447Z.json`. The guarded wrapper
`backend/chainbound_game/scripts/run-bootstrap-gate-b-upgrade.sh` passed another fresh validate-only
preflight after approval with no sign/send (`Logs/bootstrap-gate-b-upgrade-20260920T191534Z.log`).

Gate B subsequently finalized successfully. Upgrade signature
`zhf6dPktpR59LRuofn3XmGqcwjttSAdsY9TuMbdJpLoVAZNz1u9hZCs4fzD5rKNepakFKjq8UdhaKQ4Vkt2MEwi`
landed at slot `501530147` with `error=null` and a 5,000-lamport fee. ProgramData now contains
exactly 741,248 executable bytes matching artifact SHA-256
`588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87`; the pinned program ID,
ProgramData address, loader owner, and upgrade authority are unchanged. The loader atomically
drained/closed buffer `B5w4…EkdDY`, using the same signature for its 3,766,418,680-lamport refund,
and the payer balance reads 11,211,886,680 lamports. Independent CLI confirmation later reported
`finalized`. Evidence: `Logs/bootstrap-gate-b-upgrade-20260920T192050Z.log` and
`Logs/bootstrap-gate-b-upgrade-verified-20260920T192050Z.json`.

A read-only server audit against linked Supabase project `jhzqcbrzkausrrtcavuu` confirms the exact
post-Upgrade server gate is still pending: migration `20260920010000` is absent from remote history,
both Devnet smoke-grant functions are absent, and the remote `ownership_confirm_stamp` remains at
version 6. Direct schema reads also found neither `ownership_ram_grant_operations` nor
`ownership_confirm_stamp_atomic`. The local nine-test ownership suite and all three target function
typechecks pass. Because remote migration history diverges and unrelated local migrations are also
pending, **do not use** a broad `supabase db push --include-all`; the later approved server gate must
apply only the pinned SQL file, mark only version `20260920010000` applied, and deploy only
`ownership_prepare_devnet_smoke_grant`, `ownership_confirm_devnet_smoke_grant`, and the updated
`ownership_confirm_stamp`. No remote mutation was performed. Evidence:
`Logs/bootstrap-server-gate-audit-20260920T191041Z.json`.

The guarded exact-only wrapper `backend/chainbound_game/scripts/run-bootstrap-server-gate.sh`
passed its WSL validate-only run. It pins every local input hash, confirms the linked project and
current remote absence/version state, explicitly excludes broad database push and unrelated
migrations, and reports `mutations_performed=false`. After Gate B, validate-only was strengthened and
rerun so it also dumped the live program and proved an exact 741,248-byte match to artifact SHA-256
`588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87` before the mutation branch.
The first approved mutation attempt stopped before executing SQL because the Windows Supabase CLI
shim could not open an absolute WSL `/mnt/c/...` filename. Evidence:
`Logs/bootstrap-server-gate-20260920T211026Z.log`. Remote reads immediately before that error still
showed the target objects absent, so no migration or function deployment occurred. The wrapper now
passes the same hash-verified migration through a path relative to its pinned Supabase working
directory; a linked read-only `SELECT 1` file probe verified that path translation.

Latest validate-only evidence: `Logs/bootstrap-server-gate-20260920T211202Z.log`. This fresh pass
again found the grant table/RPC and both grant functions absent, remote Stamp confirm still at
version 6, the exact live-program artifact hash matched, and `mutations_performed=false`. Gate B is now
independently verified, so this server gate is eligible for its own explicit approval; it remains
approved but not yet completed. At that checkpoint the linked project remained unmodified by this
gate; the later partial attempt below supersedes that state.

The second approved attempt successfully applied the exact SQL, then stopped at migration-history
repair because the Windows CLI searched its hardcoded `supabase/migrations` path from one directory
too deep. Read-only verification now finds `ownership_ram_grant_operations` and
`ownership_confirm_stamp_atomic(...)` live, while migration version `20260920010000` is still absent,
both grant functions remain absent, and Stamp confirm remains version 6. No unrelated migration or
function was touched. The wrapper now runs Supabase CLI commands from the repository root, where a
read-only migration list resolves the intended top-level `Supabase/migrations` directory. The
resume guard now requires the table and RPC to be either both present or both absent; in this known
partial state it skips SQL instead of replaying it, repairs only migration-history version
`20260920010000`, deploys only the three approved functions, and finally verifies the history row
plus ACTIVE/JWT-enforced function versions. Latest partial-state validate-only evidence is
`Logs/bootstrap-server-gate-20260920T213747Z.log`; it again confirmed the migration-history row and
all three deployments are still pending and performed no mutation.

The approved resume completed successfully at `2026-09-21T01:03:42Z`. It detected both SQL objects
already present and did not reapply SQL, repaired only migration-history version `20260920010000`,
and deployed only the approved functions. Final verification reported both smoke-grant functions
ACTIVE at version 1, `ownership_confirm_stamp` ACTIVE at version 7, JWT enforcement enabled on all
three, and no broad push or unrelated migration. A separate read-only pass then reconfirmed those
versions, the exact live program artifact hash, and matching local/remote migration history.
Evidence: `Logs/bootstrap-server-gate-20260921T010216Z.log` and
`Logs/bootstrap-server-gate-20260921T010429Z.log`. Credential-free POST probes also confirmed both
new smoke-grant endpoints reject missing JWTs with HTTP 401; evidence:
`Logs/bootstrap-server-gate-no-jwt-smoke-20260921T010755Z.log`.

Those are the deployment-time revision numbers. After the later public environment pin update, the
current remote list reports smoke-grant Prepare/Confirm v2 and Stamp Confirm v8, still ACTIVE with JWT
enforcement. No migration or function deployment command was run in that update. Current revision,
bundle-hash, GameConfig, OwnershipConfig, test, and prefab-flag evidence is consolidated in
`Logs/ownership-bootstrap-current-state-audit-20260921T022849Z.json`.

The first correctly invoked wrapper attempt returned HTTP 401 before the function ran. A later attempt
with a valid authenticated-user JWT passed the gateway and exposed the original on-chain bootstrap
blocker; that blocker is now resolved by the finalized zero-RAM PlayerProfile/MoveVault initialization.
The first post-bootstrap Prepare request then failed closed with
`missing_env:CHAINBOUND_DEVNET_SMOKE_WALLET`. Remote name/hash verification confirmed the setting was
absent, so the exact public wallet pin was added to the Supabase function environment on
2026-09-21. Its stored SHA-256 matches the expected public-key text. No transaction was built, signed,
or sent by the failed request or the environment update. A linked read-only database query also
confirmed zero rows for idempotency key `devnet-smoke-grant-20260920-1`, so the retry will begin from
a clean server operation state. Evidence:
`Logs/bootstrap-smoke-wallet-env-pin-20260921T022426Z.json`. The wrapper's local secret-free JWT
preflight remains in place; never paste a JWT into docs or chat. A post-update credential-free probe
also reconfirmed HTTP 401 `UNAUTHORIZED_NO_AUTH_HEADER`, so setting the public allowlist pin did not
weaken authentication. Evidence:
`Logs/bootstrap-smoke-wallet-env-pin-no-jwt-20260921T022700Z.json`.

The next authenticated Prepare reached the local operator client but stopped before review output
with `Invalid arguments`. This was not an Edge Function, signer, or chain failure: installed
`@solana/web3.js` 1.99.0 does not accept `SimulateTransactionConfig` with the legacy `Transaction`
overload. The grant client now preserves the same legacy message and ordered signature bytes inside
a `VersionedTransaction` wrapper solely to select the SDK's supported config overload. Devnet-only
non-mutating probes passed for partial-signature review (`sigVerify=false`, replacement blockhash,
`err=null`) and fully signed signature verification. No smoke-wallet keypair was loaded and nothing
was sent. Evidence: `Logs/ownership-bootstrap-ram-grant-simulation-compat-20260921T023908Z.json`.

After that compatibility repair, authenticated Prepare completed on 2026-09-21. It verified the
exact wallet/profile/receipt, amount 100 from a zero balance, daily cap 100, flags 0, the server-held
result-authority signature, absent player signature, and a green unsigned simulation at 13,791 CUs.
The server has exactly one `prepared` operation row with the matching receipt and no transaction
signature; a separate public-RPC read confirms the profile is still at zero and the receipt does not
exist. The reviewed fee is 10,000 lamports (two signatures); result authority pays the 1,264,920-
lamport receipt rent. Both payers have ample Devnet balance. No signing or sending occurred. Evidence:
`Logs/ownership-bootstrap-ram-grant-prepare-20260921T024204Z.json`,
`Logs/ownership-bootstrap-ram-grant-state-20260921T024246Z.json`, and
`Logs/ownership-bootstrap-ram-grant-prepare-review-20260921T024246Z.json`.

The separately approved Execute sent exactly one grant transaction on 2026-09-21. Public-RPC
verification proves signature `4FbUZ5hGuqW1QZkUCRhcQa5weSsEs1rqSjUb4iD8RDQWQz76LpMAVZBtTkbsiDn196vCtzmjBjnpD7ggfxWYrBUH`
at slot `501693453`, `err=null`, fee 10,000 lamports; the immutable receipt matches the pinned
wallet, idempotency hash, amount 100, balance-after 100, and rotated result authority. The profile
now has RAM balance/daily/lifetime `100/100/100`; the vault remains empty and flags remain 0.
Evidence: `Logs/ownership-bootstrap-ram-grant-state-20260921T025345Z.json`.

The transaction succeeded before server bookkeeping returned
`invalid_smoke_grant_confirm_request`. Root cause is a server validator bug: the two confirmation
functions applied the 32–44-character public-key regex to a 64-byte transaction signature. Source
now uses decoded 64-byte base58 validation in both `ownership_confirm_devnet_smoke_grant` and
`ownership_confirm_stamp`; focused tests pass 11/11. The database row intentionally remained
`prepared` with no recorded signature. The exact hotfix was approved and deployed on 2026-09-21:
`ownership_confirm_devnet_smoke_grant` advanced v2→v3 and `ownership_confirm_stamp` v8→v9, both
ACTIVE with JWT enforcement. Preflight passed 11/11 tests and reverified the exact chain receipt and
single prepared database row; deployment applied no migrations, secret changes, or chain
transactions. Post-deploy credential-free probes still return HTTP 401 for both functions. Evidence:
`Logs/ownership-confirm-signature-hotfix-20260921T025817Z.log` and
`Logs/ownership-confirm-signature-hotfix-postdeploy-20260921T025910Z.json`.

Signer-free recovery completed on 2026-09-21 without loading a keypair, signing, or sending. It
located and revalidated the exact grant transaction above, then confirmed the existing operation
row. An independent linked read shows `status=confirmed`, the exact signature, amount/balance-after
100, daily/lifetime `100/100`, UTC day `20717`, and confirmation time
`2026-09-21 03:05:25.365+00`. Public-RPC state remains RAM `100/100/100`, empty vault, and flags 0.
Evidence: `Logs/ownership-bootstrap-ram-grant-recovered-20260921T030527Z.json` and
`Logs/ownership-bootstrap-ram-grant-state-20260921T104628Z.json`. No keypair, signature, resend,
config/economics change, or flag flip occurred during recovery. The live matrix remains NO-GO.

The next scoped one-modifier Stamp prerequisite audit is green. Known
`9293fc49-b5d0-48d9-b071-504bdc9b3b00` is the unconsumed `baseline_slam` row for the active smoke
wallet and has no on-chain instance yet. The original keyless attempt left an unsigned `prepared`
operation under `moves-smoke-mint-20260919-1`; its ticket and predicted asset do not exist on-chain.
Two later diagnostic attempts created separate expired, unsigned `prepared` rows for the profile's
second Known (`baseline_snipe`). A public-RPC `getMultipleAccounts` read confirms that all three
ticket PDAs and all three predicted assets are absent on-chain. The two diagnostic rows are not
selected for this smoke and remain intact as audit history. The client is pinned back to the
original idempotency key so Prepare refreshes that transaction/blockhash rather than creating
another operation or asset address. The vault is empty with `next_instance_id=1`. The keyless client now requires the exact three-instruction transaction
(materialize Known, prepare Stamp, Stamp), exact PDAs/account privileges/data, only the wallet and
result-authority signer set, the authority signature present and wallet signature absent, and a green
unsigned simulation. Its reviewed economics must be 5 + 75 = 80 RAM, leaving 20; second Stamp 150
and Bind 250 remain inactive pins. Evidence:
`Logs/ownership-moves-smoke-prereq-audit-20260921T105100Z.json` and
`Logs/ownership-moves-smoke-prepared-rows-audit-20260921T110026Z.json`.

The first post-grant Stamp Prepare attempt returned `idempotency_key_conflict` without signing or
sending. A linked read confirmed the stored operation still belongs to the pinned `baseline_slam`
Known row, while the profile now also has a second unconsumed Known row (`baseline_snipe`). The
operator wrapper previously allowed an ambient `CHAINBOUND_SMOKE_KNOWN_ID` to override its default,
which can select the second row while reusing the first row's idempotency key. The wrapper now
exports the exact Known ID and idempotency key itself, and the Node client rejects any non-pinned
Known ID before making the HTTP request. No server deployment, chain transaction, economics change,
or flag flip was made for this guardrail correction. A later credential-free state read still shows
RAM `100/100/100`, an empty vault at next instance 1, cap 100, and flags 0. Evidence:
`Logs/ownership-bootstrap-ram-grant-state-20260921T105946Z.json`.

The corrected keyless Prepare then built the exact three-instruction transaction and reached the
Stamp CPI, but unsigned simulation failed at instruction index 2 with Metaplex Core custom error
`29` (`0x1d`, `ConflictingAuthority`). Instructions 0 and 1 (Known materialization and Stamp-ticket
preparation) completed in simulation. The program's Stamp `CreateV2` builder supplied both the
Moves collection and a separate update authority; Core requires the collection to become the asset
update authority and rejects that combination. The scoped source correction keeps the collection
and Core-authority signer but passes `update_authority(None)` for Stamp only. Bind remains inactive
and unchanged. No transaction was signed or sent.

The corrected local SBF artifact is 741,248 bytes with SHA-256
`5741a024e981c3f7558752726564466cb6beabeefffe61254a620b915b8a6e5b`; it exactly fits the existing
741,248-byte ProgramData capacity, so no extension is required. Program tests pass 7/7, ownership
server regression tests pass 12/12, and the Solana program autofixer reports no issues or required
follow-up. The unsigned Devnet upgrade plan uses the retained `B5w4…` buffer keypair, observes that
the buffer is absent, and simulates buffer creation successfully. The live program remains on hash
`588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87` at slot `501530147` until two
separate approvals: Gate A creates/uploads the buffer; Gate B upgrades only after byte verification.
Evidence: `Logs/ownership-stamp-core-createv2-hotfix-review-20260921T112411Z.json`.

Gate A received explicit approval and its exact wrapper passed validation-only preflight on
2026-09-21: Devnet and slot `501530147` matched, the `B5w4…` buffer was absent, the artifact hash
matched, 733 writes were planned, projected Gate A fees were 3,675,000 lamports, and buffer-create
simulation returned no error. Validation signed and sent nothing. Evidence:
`Logs/stamp-core-createv2-hotfix-gate-a-upload-20260921T112939Z.log`.

### Gate A complete (2026-09-21) — CreateV2 Stamp hotfix buffer uploaded + verified

Operator Gate A send finished successfully. First create/upload hit public-RPC
`Max retries exceeded` after buffer create (`Logs/stamp-core-createv2-hotfix-gate-a-upload-20260921T113534Z.log`).
A second create/upload correctly refused because the buffer already existed
(`Logs/stamp-core-createv2-hotfix-gate-a-upload-20260921T124316Z.log`). Resume-writes-only then
completed and verified:

- Resume log: `Logs/stamp-core-createv2-hotfix-gate-a-resume-20260921T124652Z.log`
- Buffer verify: `Logs/stamp-core-createv2-hotfix-gate-a-resume-buffer-verified-20260921T124652Z.json`
- Buffer: `B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY`
- Artifact SHA-256: `5741a024e981c3f7558752726564466cb6beabeefffe61254a620b915b8a6e5b` (`matchesArtifact: true`)
- Upload txs: **734 / 734 successful, 0 failed** (create + 733 writes)
- Live program `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ` still at `lastDeploySlot: 501530147` — **old bytecode remains live**
- Unity ownership flags remain OFF; live matrix remains NO-GO
- **Gate B (upgrade) is NOT done** and still requires a separate approval token before any upgrade wrapper

Next Codex/operator action: obtain separate Gate B approval, then run only
`run-stamp-core-createv2-hotfix-gate-b-upgrade.sh`. Do not re-run Gate A create/upload. Do not
rerun Stamp Prepare until Gate B upgrade is byte-verified on the live program.

A fresh unsigned Gate B preflight on 2026-09-21 revalidated the live pre-upgrade slot, exact buffer
and artifact hash, upgrade authority, and 741,248-byte capacity. The transaction contains only the
Upgradeable Loader `Upgrade` instruction (`03000000`), requires one signer, costs 5,000 lamports,
and simulated with `err=null` at 2,370 CUs. The wrapper contains no buffer-create or write path and
fails closed with exit 64 without the separate approval token. No signature was produced and
nothing was sent. Evidence:
`Logs/stamp-core-createv2-hotfix-gate-b-preflight-20260921T201833Z.json`.

### Gate B complete (2026-09-21) — CreateV2 Stamp hotfix live + byte-verified

The separately approved Upgrade-only wrapper finalized signature
`5vVxZmkJfCcSXVdWdyPcLo6XS2MkqHsVPHfaQ73S9Axd2dC4WFwCaWmgci1Jk6hfd2eHNfsLrnxZLQ5aFqaNaLnF`
at slot `502081429` with `error=null` and a 5,000-lamport fee. Direct ProgramData verification
removed the 45-byte loader header and proved an exact 741,248-byte match to SHA-256
`5741a024e981c3f7558752726564466cb6beabeefffe61254a620b915b8a6e5b`. The loader atomically
drained/closed buffer `B5w4…` and returned its 3,766,418,680 lamports to the payer under the same
signature. No buffer-create/write operation ran in Gate B. Unity ownership flags remain OFF and the
live matrix remains NO-GO. Evidence:
`Logs/stamp-core-createv2-hotfix-gate-b-upgrade-20260921T204216Z.log`,
`Logs/stamp-core-createv2-hotfix-gate-b-upgrade-verified-20260921T204216Z.json`, and
`Logs/stamp-core-createv2-hotfix-gate-b-complete-20260921T204224Z.json`.

The next and only action in this goal is the keyless Stamp Prepare rerun against the original pinned
Known/idempotency pair. It must confirm that Core error 29 is gone without signing or sending. Do
not rerun Gate A or Gate B, and do not proceed to the Moves mint.

That post-upgrade keyless Prepare is now green. It refreshed the original Known
`9293fc49-b5d0-48d9-b071-504bdc9b3b00` / idempotency key `moves-smoke-mint-20260919-1`, validated
the exact three-instruction transaction, retained only the wallet and result-authority signer set,
and simulated with `error=null` at 104,206 CUs. The reviewed RAM path remains 5 + 75 = 80, leaving
20 from the on-chain starting balance of 100. `Custom:29` is absent. The result-authority signature
is present, the wallet signature is absent, and `signingPerformed=false` / `sendingPerformed=false`.
A follow-up public Devnet read confirms both the ticket and predicted Core asset remain absent, so
Prepare created no persistent side effects. Evidence:
`Logs/ownership-moves-smoke-prepare-20260921T204804Z.log`,
`Logs/ownership-moves-smoke-mint-prepare-20260921T205011Z.json`, and
`Logs/stamp-core-createv2-hotfix-complete-20260921T205208Z.json`.

This hotfix track is complete. No Moves mint, Unity DAS/config change, feature-flag change, or live
matrix action is authorized by this result; each requires a new goal and any applicable approval.


### Supabase anon configuration verified (2026-09-19)

Both Unity `SupabaseConfig.asset` copies (`Assets/Data/Supabase` and `Assets/Resources`) decode to JWT claim `role=anon` for project `jhzqcbrzkausrrtcavuu`. A scan of `Assets` found no `service_role` / service-role key material. `SupabaseConfigSO` exposes only `supabaseAnonKey` (tooltip forbids service role).

### Read-only Moves DAS inventory probe (2026-09-19)

Credential-free probe against `https://api.devnet.solana.com` using `SolanaConfig.coreMovesCollectionId`:

- `getAsset` ? `interface=MplCoreCollection`, authority `FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys` (matches config).
- `getAssetsByGroup` (collection) ? `total=0` / no items — expected: live mint path and ownership flags remain OFF; no player Moves have been minted into the collection yet.
- No Unity flag changes, no Helius key, no mint/Stamp/roll.
### Next GO (not authorized or started)

After the scoped smoke is complete, the next separately approved step is an **Editor-only Ownership
Spine pass** on isolated identities. It is not the live matrix GO and still authorizes no player-build
feature flag, second Stamp, Archive Binding, mainnet, or Closed Beta enablement.

## MagicBlock VRF efficacy track

This track is separate from economy/fee design.

| Pin | Value |
|---|---|
| MagicBlock base-layer VRF program | `Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz` |
| Base-layer oracle queue | `Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh` |
| Slot hashes sysvar | `SysvarS1otHashes111111111111111111111111111` |

The program now creates one EncounterRequest PDA per owner and 32-byte request seed, CPIs to the
pinned queue, authenticates fulfillment through MagicBlock's program-scoped VRF identity, and
enforces Pending → Fulfilled → Consumed exactly once. The Rust transition test passes.

Unity's production/player path always composes the real `VrfService`. The saved Editor config keeps
`useDeterministicVrfInEditor = true`; this affects Editor/test builds only. To run the efficacy
harness, temporarily disable that Editor-only switch, enter Play Mode with a funded connected
Devnet wallet, and select **Chainbound → Solana → Run MagicBlock VRF Devnet Efficacy**. It reports request
PDA, request signature, 32 bytes, latency, and consume result without logging secrets.

Expected polling defaults are 2 seconds with a 30-second timeout. Failures include rejected
simulation, wallet cancellation, request transaction failure, missing/wrong-owned request PDA,
invalid discriminator/status, callback delay, RPC timeout, and consume replay.

There is no successful Devnet VRF request signature yet; VRF remains outside this deployment slice
and must not absorb fee design. Stamp continues to use its approved authority-server entropy.
Future Stamp VRF integration must
feed the fulfilled bytes into the existing compatibility/`CanApply` selection and lock
`effectValue`; it is not part of this efficacy slice.

## Live security matrix

Every row remains blocked until the Supabase authority bundle, runtime endpoints, Unity client
configuration, and isolated test identities are ready. The pinned program, collections, and both
on-chain configs are complete; costs below remain the Phase 1 pins.

| # | Required case | Cost | Result | Blocking dependency |
|---:|---|---:|---|---|
| 1 | Known roll creates a distinct zero-mod non-NFT | 5 | **SCOPED GREEN / MATRIX BLOCKED** | HTTP smoke is green; Unity client and isolated matrix remain |
| 2 | Known roll with insufficient RAM creates nothing | 5 | BLOCKED | SQL atomic `insufficient_ram` is green; isolated insufficient-RAM HTTP case has not been run |
| 3 | First Stamp with one modifier | 75 | **SCOPED PREPARE GREEN / MINT NOT RUN** | hotfix is byte-verified live; original three-instruction Prepare simulates at 104,206 CUs with `error=null`, no `Custom:29`, and no signature/send |
| 4 | First Stamp with two distinct modifiers | 75 | BLOCKED | outside the one-modifier scoped smoke; full matrix remains NO-GO |
| 5 | Same Known cannot perform the first Stamp twice | 75 | BLOCKED | live Stamp path |
| 6 | Second Stamp/add-mod on already-Stamped move | 150 | **BLOCKED / deferred** | no Phase 1 program/server instruction; do not invent one |
| 7 | Ticket/idempotency replay cannot double-mint | 75 | BLOCKED | live Stamp path |
| 8 | Wrong owner fails | 75 | BLOCKED | two configured identities |
| 9 | Insufficient RAM preserves Known | 75 | BLOCKED | separate isolated insufficient-RAM identity is not authorized in this scoped smoke |
| 10 | Expired/invalid ticket fails cleanly | 75 | BLOCKED | configured TTL and endpoint |
| 11 | Wrong collection/Core program fails | 75 | BLOCKED | live negative-path harness and test identity |
| 12 | Core CPI failure leaves no partial desync | 75 | BLOCKED | live failure harness |
| 13 | Confirmed inventory removes Known and DAS verifies `cb.*` asset | 75 | BLOCKED | DAS and authority endpoint |
| 14 | Authenticated walletless Known acquisition is idempotent | 5 | BLOCKED | HTTP first acquisition is green; post-secret replay and full isolated matrix remain |

## Enablement decision

- Merge with flags off: **eligible after normal code review**, subject to the documented mpl-core
  warning and unexecuted Unity test assembly.
- Enable for internal testers: **NO-GO**.
- Closed Beta/production: **NO-GO**.

Do not enable either Unity ownership flag until every non-deferred matrix row is green and the
operator has separately approved the flag change.

### Follow-on docs (2026-09-19)

- Editor read-only probe menu: `Assets/Editor/Ownership/MovesCollectionDasProbeMenu.cs` ? **Chainbound/Ownership/Probe Moves Collection (DAS read-only)**
- Live matrix plan (NO-GO until explicit GO): `Docs/review-unimplemented/Specs/Economy/OWNERSHIP_SPINE_LIVE_MATRIX_PLAN.md`
- Helius DAS proxy design (key server-side only): `Docs/Specs/Economy/OWNERSHIP_SPINE_HELIUS_DAS_PROXY_DESIGN.md`
