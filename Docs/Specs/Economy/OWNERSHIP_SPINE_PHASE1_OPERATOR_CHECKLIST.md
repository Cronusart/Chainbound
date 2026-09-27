# Ownership Spine Phase 1 — operator checklist

Status: **NO-GO; feature flags remain OFF.**  
This checklist does not authorize signatures, Archive Binding, re-stamp/add-mod-on-Stamped, or a
fee-design change.

## 1. Confirm the pins

- [x] Confirm the exact economics mapping:

| Operation | Phase 1 RAM | Gate-E/on-chain status |
|---|---:|---|
| Known roll | **5** | Program constant; not an OwnershipConfig field |
| First Stamp, Known → Stamped | **75** | `OwnershipConfig.stamp_ram_cost` |
| Second Stamp / add-mod on already-Stamped | **150** | Phase 1.1 pin only; BLOCKED/deferred, with no field or instruction in Gate E |
| Bind / Archives | **250** | `OwnershipConfig.bind_ram_cost`; configuration only, Archive Binding remains out of scope |
| Daily earning cap | **100** | `GameConfig.ram_daily_cap`; never a starting-balance grant |

- [x] Confirm that **150 is not overloaded into either on-chain cost field**: Gate E encodes only
      `stamp_ram_cost = 75` and `bind_ram_cost = 250`.
- [x] Program stays at `9312ay7VrpvNYmyEx8TYCqhafbZ2Y5WGVbt6Qp772uUQ`.
- [x] Both Unity ownership feature flags stay OFF through the full live matrix.

## 2. Supply missing public/config inputs

Do not send secrets in chat, logs, command arguments, or git.

- [x] result-authority public key: `FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF` (approved case-correction rotation);
- [x] trusted Moves collection: `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC`;
- [x] trusted Cosmetics collection: `76aVsw9xScewNG4wkiE75asdM5bbZaHS5CG78263yMcV`;
- [x] trusted Covers collection: `6wPnru8RDTsnCyujzPMq8v9U8pD6ynRa8es3ZXcesbNs`;
- [x] trusted Archives collection: `DP3rTx145BPfunueM9SHjYJXBDCK5GmrdwZjpCvgf6U`;
- [x] generator version: `1`;
- [x] exact 32-byte modifier-pool hash:
      `6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4`;
- [x] Stamp-ticket TTL: **900** seconds;
- [x] bind RAM cost: `250` as configuration only; Archive Binding remains out of scope;
- [x] metadata URI base: `https://jhzqcbrzkausrrtcavuu.supabase.co/storage/v1/object/public/chainbound-ownership-metadata`;
- [x] isolated funded Devnet smoke wallet and bootstrap owner `H68e...bBWq`, with PlayerProfile
      `HRFp...ioQa` and MoveVault `8Whf...J3YH` initialized and independently read back.

All four collection addresses are required by the current OwnershipConfig instruction. The Moves
collection update authority must be the program-derived Core authority
`FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys`.

## 3. A1 signing boundary — ProgramData extension

Re-run all read-only values immediately before approval. Current reviewed values:

| Field | Exact value |
|---|---:|
| Artifact bytes | 714,312 |
| Artifact SHA-256 | `58e78eb59c34b701c50729fdbdd77677232bec12bce1b6e9e0ae7e2a32d2a2ad` |
| Pre-extension allocation | 184,957 bytes |
| Target allocation | 714,357 bytes |
| Additional bytes | **529,400** |
| Target rent minimum | 3,629,583,800 lamports |
| Pre-extension ProgramData lamports | 1,288,191,600 |
| Rent top-up | **2,341,392,200 lamports** |
| Finalized ProgramData lamports | **3,629,583,800** |
| Finalized authority balance | **5.363977 SOL** |
| Finalized signature | `5fHYvNK15JmxFAyHU7tGYHXb1FaQKGdFW7DCMQd2bCJ6xkBx7NJBo78812W6sguwpjskyHxjDtHd4xp3qdrMiZUi` |

Expected accounts:

- program `9312…2uUQ`;
- ProgramData `5wq…HfzD`;
- rent payer `DkFm…61gJmG` (also the upgrade authority, although unchecked `ExtendProgram` only
  requires this account as the writable payer signer);
- upgradeable loader;
- System Program.

- [x] Confirm the exact program, ProgramData, authority, byte delta, and rent top-up.
- [x] construct and simulate an unsigned message with the no-keypair builder
      `backend/chainbound_game/scripts/prepare-program-extend-unsigned.cjs`;
- [x] verify the snapshot instruction data `06000000f8130800`, one signer, 5,000-lamport fee,
      2,520 compute units, and `err = null`;
- [x] re-run the builder to refresh its expiring blockhash, RPC fee, message hash, and simulation;
- [x] Present the finalized transaction/message summary.
- [x] Obtain explicit approval.
- [x] Only then execute `solana program extend 9312…2uUQ 529400` with the reviewed authority.
- [x] Record signature and post-transaction allocation/lamports.

Extension finalized successfully at slot 500,497,293 with `err = null`, a 5,000-lamport fee, exact
instruction data `06000000f8130800`, 714,357-byte post-allocation, and unchanged authority. This did
not upgrade the executable; Section 4 remains a separate signing boundary.

## 4. A1 signing boundary — explicit pinned upgrade

The upload/upgrade is a second approval boundary.

- [x] Audit the Agave v2.3.0 upload path with the no-sign/no-send planner
      `backend/chainbound_game/scripts/prepare-program-upgrade-unsigned.cjs`.
- [x] Verify the reviewed shape: 714,349-byte buffer; **3,629,583,800-lamport** temporary
      funding under Agave v2.3.0's 714,357-byte ProgramData rent basis; 706 writes of at most
      1,012 bytes; 852-byte last chunk at offset 713,460.
- [x] Verify current fees: buffer create 10,000; each write 5,000; all writes 3,530,000; final
      Upgrade 5,000; upload-and-upgrade total **3,545,000 lamports**.
- [x] Simulate the unsigned two-signer buffer-create sample: `err = null`, 2,520 compute units;
      verify `InitializeBuffer = 00000000`, `Write` variant 1, `Upgrade = 03000000`.
- [x] At the approved boundary, generate and securely retain a real ephemeral buffer signer without
      printing or committing its secret.
- [x] Re-run the planner and present the exact real buffer public key, refreshed blockhash/message
      hashes/fees, signer set, and artifact SHA before its first signature.
- [x] Use the dedicated `solana program upgrade <BUFFER> 9312…2uUQ` command. It has no
      auto-extend path, so `--no-auto-extend` is not applicable; never use a bare deploy.
- [x] Confirm upgrade authority remains `DkFm…61gJmG`.
- [x] Obtain explicit approval before the first buffer signature.
- [x] Upload, then use `backend/chainbound_game/scripts/verify-program-buffer.cjs` to verify the
      buffer owner, authority, exact payload hash/length, funding, and all 707 transactions before
      requesting separate approval for the final Upgrade.
- [x] Upgrade and atomically close/refund the buffer.
- [x] Run `backend/chainbound_game/scripts/verify-program-upgrade.cjs` with the Upgrade signature
      and buffer public key; verify exact deployed payload SHA/bytes, program ID, authority,
      advanced `lastDeploySlot`, successful transaction, atomic buffer close/refund, and balance.

Gate A completed on Devnet with buffer `B5w4…EkdDY`: all **707** transactions succeeded with no
failures, create signature `5W3L…og2J`, latest write signature `5VGj…9A2i`, and the verified
714,312-byte payload SHA is `58e78eb5…a2ad`. The payer balance after upload is
**1,730,853,200 lamports**. Including the 2,341,392,200-lamport ProgramData
top-up and all 3,550,000 lamports of projected fees, the lowest projected payer balance before the
buffer refund was **1,730,848,200 lamports**.

Gate B finalized at slot **500,558,952** with signature
`3N75R7yfgY6xJSABuLBx3YkLkMurBCCNC2w2Z8wVUxEdiswBj1bq6fpAgbxsj3nQbV1zEMDvkdtXSb59i5tAvfin`.
That same signature is the atomic buffer close/refund signature. The pinned program ID and authority
are unchanged, `lastDeploySlot` advanced, the buffer is absent, and both the CLI dump and direct
ProgramData byte comparison match SHA-256 `58e78eb5…a2ad`. Final payer balance:
**5,360,432,000 lamports**. The later collection and configuration gates are now complete; every
live matrix row remains unexecuted and blocked behind its listed follow-up inputs.

## 5. Collection and initialization boundaries

After the operator supplies the inputs:

- [x] prepare an exact Moves Core collection transaction whose update authority is
      `FNRy…L3ys`;
- [x] create the other three collections only because the current OwnershipConfig requires the full
      set, with their approved authority policy;
- [x] present accounts, lamports, names/URIs, authorities, and output addresses before signing;
- [x] verify each collection on-chain;
- [x] present GameConfig args/accounts/PDA before signing;
- [x] present OwnershipConfig args/accounts/PDA before signing;
- [x] verify schema 1, generator/pool values, first-Stamp 75, Bind 250, and all four collections
      after confirmation. Ticket TTL is a later server secret/config value, not an on-chain field.

Gate C finalized four collection creates at slots 500,693,407 through 500,693,422. All four account
owners are MPL Core, all names/URIs decode as approved, and all update authorities equal
`FNRyxJLpcGuWwEUXYiXqjJMDsHAAs1gU8CnkjjBRL3ys`. Gate D finalized at slot 500,698,363 with
signature `NmgbtYdgKhwtcBGLPr56fd7VFdq1mMsRwE7BvRc6qC4wB1qMzMig2g8zzegSQD9pBnXMS1jkrRnanXYiffkC2Wp`.
Independent verification decoded result authority `FaAeg…Pt5KF`, schema 1, daily cap 100,
RAM-per-burn 10, instruction flags 0, and bump 253 from the live program-owned PDA.

Gate E finalized at slot 500,705,191 with signature
`4Cq39Xz8d9pCfeT1i89375FqxzsonWDxGxwS4U4oiHPRndwpLmSgKUvDkJLGXpj4pq59oyjZETQxG4np6eSwhfp6`.
Independent transaction-byte and account verification confirmed the program-owned 183-byte PDA,
1,579,880 rent-exempt lamports, schema 1, all four collections, first-Stamp 75, Bind 250, generator
version 1, the approved pool hash, and bump 255. No second-Stamp field or instruction was encoded.

Do not add the 150-RAM future pin to an invented instruction or current config layout.

## 6. Supabase migration and local bundle

- [x] run `supabase migration list --linked` read-only (2026-09-18: remote rows through
      `20260906050103`, all Local cells blank because reviewed SQL is under `Supabase/migrations`
      while the linked CLI layout resolves `Supabase/supabase/migrations`);
- [x] reconcile the directory/ledger mismatch without copying the full local tree; only the
      ownership SQL was copied into `Supabase/supabase/migrations/` and renamed to the MCP version
      `20260919064343` after apply;
- [x] review and apply `20260916010000_ownership_spine_phase1_authority.sql` via MCP
      `apply_migration` as remote `20260919064343_ownership_spine_phase1_authority` (not
      `db push --include-all`);
- [x] do not use `db push --include-all` as a shortcut;
- [x] verify ownership tables have RLS enabled, zero client policies, and anon/authenticated
      PostgREST `select` is denied (`42501` / HTTP 401);
- [x] deploy the reviewed local `ownership_*` bundle after the migration (v3, `verify_jwt` true,
      2026-09-19 06:51:42 UTC);
- [x] replace/prove the server's Stamp pool against the approved canonical 104-row Pass-A catalog
      (`modifier_pool.generated.ts`; hash match; Deno tests 5 passed);
- [x] set `CHAINBOUND_RESULT_AUTHORITY_SECRET` through the Supabase secret store only (value
      redacted). The approved GameConfig rotation finalized at slot `500772832`, signature
      `5ByzaVkZ9TR546P82VLSw46as9XwGvFgSdqRSsY9CxG6JKUuNDJH5qrryp8fFc6aTvNsvuRDAGcm2wwUs3XANRxZ`,
      and the pinned public key is `FaAegH2tBCyDzxXcXyz64qEFWSrvpX8M6SSFNx1Pt5KF`;
- [x] verify an idempotent replay is not charged twice (SQL atomic RPC);
- [x] verify a balance below 5 returns `insufficient_ram` and creates no Known row (SQL atomic RPC);
- [x] verify wrong-profile (**401**) and wallet-link (**403**) failures on the HTTP endpoints;
- [x] post-secret authenticated `ownership_roll_known`: HTTP 200, Known
      `9293fc49-b5d0-48d9-b071-504bdc9b3b00`, **5 RAM** charged, balance `100 → 95`, generator `1`,
      approved pool hash present, `replayed=false`;
- [x] post-secret no-JWT `ownership_roll_known`: HTTP 401 `UNAUTHORIZED_NO_AUTH_HEADER`;
- [x] post-secret spoofed `player_profile_id`: HTTP 401 `authenticated_profile_not_found`.

Secret/config names and set/unset status are listed in
[OWNERSHIP_SPINE_PHASE1_DEVNET.md](OWNERSHIP_SPINE_PHASE1_DEVNET.md). Never print their values.

## 7. Unity fields

Already saved in the live Editor:

- MagicBlock VRF program:
  `Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz`;
- base-layer queue:
  `Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh`;
- Editor/test deterministic VRF switch: ON;
- Known roll: 5;
- first Stamp: 75.

Unity config-only wiring completed:

- [x] real Moves collection in `SolanaConfig.asset`:
      `AUQuvgpFDUekeVh2GHri99A59G4YY5LUWdGw9fKTB9wC`;
- [x] credential-free Devnet RPC/DAS configuration: `https://api.devnet.solana.com` for both RPC
      and DAS, plus `wss://api.devnet.solana.com` for subscriptions; live DAS `getAsset` verified
      the collection as `MplCoreCollection` with authority `FNRy…L3ys`;
- [x] Supabase anon configuration only (never service-role in Unity); verified 2026-09-19: both `Assets/Data/Supabase/SupabaseConfig.asset` and `Assets/Resources/SupabaseConfig.asset` carry JWT `role=anon` for project `jhzqcbrzkausrrtcavuu`; no `service_role` string in Assets;
- [x] both ownership flags remain false (`CandyMachinePanel.prefab` and `GameServices.prefab` each
      serialize `enableDevnetOwnershipSpine: 0`).

No Helius/admin key, result-authority secret, service-role key, or other private material is stored
in the shipping Unity config. This is read/config wiring only and does not authorize the live matrix,
mint, Stamp, roll, mainnet, or a feature-flag change.

Server-side Helius DAS proxy:

- [x] deploy `ownership_das_proxy` version 2 with `verify_jwt=true`;
- [x] allow only `getAsset`, Moves-pinned `getAssetsByGroup`, and profile-wallet-bound
      `getAssetsByOwner`; clamp page to 1–1000 and limit to 1–50;
- [x] no-JWT smoke returns HTTP 401 `UNAUTHORIZED_NO_AUTH_HEADER`;
- [x] set `HELIUS_DAS_URL` in Supabase secrets (name-only verification 2026-09-19; value redacted;
      Devnet Helius HTTPS URL only);
- [x] run authenticated getAsset/getAssetsByGroup and rejected-method/collection smokes: HTTP 200
      `MplCoreCollection`; HTTP 200 with limit clamped to 50 and total 0; HTTP 400
      `das_method_not_allowed`; HTTP 400 `collection_not_allowed` (redacted evidence
      `Logs/ownership-das-proxy-auth-smoke-20260919T213308Z.log`);
- [x] keep both Unity ownership flags false and all Helius credential material out of Assets.

Authenticated Unity read path and one scoped Moves smoke:

- [x] add `OwnershipDasProxyClient` using only the public anon `apikey` and process-memory user JWT;
      support `getAsset` and Moves-pinned `getAssetsByGroup` through `ownership_das_proxy`;
- [x] validate returned IDs, Core interface, Moves collection, and expected program-PDA authority;
      cap responses at 2 MiB and inventory limit at 50;
- [x] keep `HELIUS_DAS_URL`, service-role, result-authority, and operator key material out of Unity;
- [x] redeploy `ownership_prepare_stamp`, `ownership_confirm_stamp`, and
      `ownership_get_inventory` as version 6 so server-side DAS verification uses validated
      `HELIUS_DAS_URL`; transaction RPC remains the public Devnet endpoint;
- [x] capture the goal-permitted authenticated CLI proxy evidence for both reads and both rejection
      guards in `Logs/ownership-das-proxy-auth-smoke-20260919T213308Z.log`;
- [x] run **Chainbound -> Ownership -> Probe Moves via Supabase DAS proxy (authenticated)** in an
      authenticated Editor session: `getAsset` HTTP 200 with the pinned collection/authority and
      `getAssetsByGroup` HTTP 200 with page 1, limit 50, total 0; redacted evidence
      `Logs/ownership-das-proxy-unity-20260919T222028Z.log`;
- [ ] follow-up verification: rerun the five discovered `OwnershipDasProxyClientTests`; do not count
      the prior Pipeline timeout as a pass or as a blocker for the permitted CLI evidence;
- [ ] run keyless prepare mode in
      `backend/chainbound_game/scripts/ownership-moves-smoke-mint.cjs`, review its redacted summary,
      and obtain explicit approval before exposing the local linked-wallet keypair to execute mode;
- [ ] resolve the on-chain bootstrap blocker before retrying prepare: the linked wallet
      `H68e...bBWq` has no PlayerProfile or MoveVault; a new profile starts at 0 RAM, while DB-Known
      materialization plus first Stamp requires 5 + 75 = 80 on-chain RAM;
- [ ] mint exactly one Devnet Moves asset through the program Stamp CPI, then record the asset ID,
      transaction signature/slot, proxy inventory `total >= 1`, and redacted evidence filename;
- [ ] reconfirm both serialized ownership flags are `0` after the smoke.

Current blocker: a valid authenticated prepare request returned `move_vault_not_initialized`.
Read-only prerequisite evidence remains at
`Logs/ownership-moves-smoke-prereq-audit-20260920T041324Z.json`. The bootstrap design is locked in
`OWNERSHIP_SPINE_PHASE1_BOOTSTRAP_DESIGN.md`, and local code now implements a capped,
result-authority-only RAM grant without changing the zero starting balance.

Local owner bootstrap preparation is green and unsigned:

- [x] Devnet genesis and pinned program/wallet checked;
- [x] derived PlayerProfile `HRFpLiWfYNiSQt4mVwpcBSStZHBKhfFVXNBhP8nhioQa` and MoveVault
      `8Whfu75D1V3EP7Nt99qCGzvXuXRk1oBZRGRECAN1J3YH`;
- [x] verified both PDAs absent before preparation;
- [x] prepared only `create_profile` + `create_move_vault`;
- [x] refreshed the unsigned preparation after Gate B; simulation passed at 21,781 units;
- [x] reviewed rent 14,488,160 lamports, fee 5,000 lamports, wallet balance 10 Devnet SOL;
- [x] recorded refreshed evidence `Logs/ownership-player-bootstrap-20260921T010843Z.json` with
      `signingPerformed=false` and `sendingPerformed=false`; execute mode now also fails closed unless
      both reviewed PDAs remain absent and the exact two pinned instructions are still present;
- [x] execute and independently verify the approved 26,936-byte ProgramData extension: finalized at
      slot `501449795`; live capacity is now 741,248 payload bytes / 741,293 account bytes, the added
      tail is exactly zero-filled, the stored ProgramData slot is `501449795`, and redacted evidence is
      `Logs/bootstrap-programdata-extension-verified-20260920T153924Z.json`;
- [x] repin the upload/resume/buffer/upgrade read-only guards to artifact SHA-256
      `588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87`, 741,248 artifact bytes,
      741,285 buffer-account bytes, 3,766,418,680 temporary deposit lamports, 733 writes, Devnet
      genesis, and pre-upgrade ProgramData slot `501449795`;
- [x] run a throwaway-key unsigned sizing pass: create simulation `err=null`, 3,680,000 projected
      upload-plus-upgrade fee lamports; no key retained, signature produced, or transaction sent;
- [x] derive the retained signer public key without reading key bytes, confirm closed buffer
      `B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY` is absent on Devnet, and rerun the unsigned plan;
- [x] record the final Gate A-only review in
      `Logs/bootstrap-gate-a-unsigned-plan-20260920T160330Z.json`: 3,766,418,680-lamport temporary
      deposit, 733 writes, 3,675,000 projected base Gate A fee lamports before any retry fees,
      create simulation `err=null`, no sign/send;
- [x] prepare and syntax-check guarded operator wrapper
      `backend/chainbound_game/scripts/run-bootstrap-gate-a-upload.sh`; it requires the exact internal
      approval token, rechecks Devnet/public keys/artifact/ProgramData/buffer absence, uses up to 10
      signing attempts for the same idempotent loader writes, tees redacted logs, verifies the uploaded
      payload, and contains no Upgrade instruction;
- [x] execute the wrapper in `CHAINBOUND_GATE_VALIDATE_ONLY=1` mode: all preflight guards passed,
      create simulation remained `err=null`, and it reported `signing_performed=false` /
      `sending_performed=false`;
- [x] obtain exact approval `APPROVED BOOTSTRAP GATE A — create and upload buffer
      B5w4TUucVz6uw2NVAVUhjQf85ytLckSTW2DwTasEkdDY on devnet`;
- [x] operator-run the approved create/upload wrapper; buffer creation succeeded but public-RPC bulk
      upload ended with `Max retries exceeded` after 31 of 733 chunks. No Upgrade ran. Partial-run log:
      `Logs/bootstrap-gate-a-upload-20260920T184039Z.log`;
- [x] verify the existing partial buffer and prepare a resume-only plan: correct loader owner,
      authority, 741,285 bytes, 3,766,418,680 lamports; 702 writes remain; first/last simulations
      both `err=null`. Evidence: `Logs/bootstrap-gate-a-resume-plan-20260920T185258Z.json`;
- [x] prepare, syntax-check, and validate-only run
      `backend/chainbound_game/scripts/run-bootstrap-gate-a-resume.sh`; it requires the existing
      buffer, uses direct QUIC/TPU sending to reduce public-RPC submission pressure, performs no
      create or Upgrade, and reuses the existing Gate A approval;
- [x] operator-run the resume-only wrapper and independently verify all 734 Gate A transactions,
      zero failures, exact 741,248-byte payload SHA-256
      `588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87`, correct authority and
      3,766,418,680 refundable lamports. Evidence:
      `Logs/bootstrap-gate-a-buffer-verified-20260920T190155Z.json`;
- [x] prepare and simulate the unsigned Gate B-only Upgrade: one required signer, 5,000-lamport fee,
      instruction `03000000`, `err=null`, 2,370 units; no sign/send. Evidence:
      `Logs/bootstrap-gate-b-unsigned-plan-20260920T190447Z.json`;
- [x] prepare, syntax-check, and validate-only run the guarded Upgrade-only wrapper
      `backend/chainbound_game/scripts/run-bootstrap-gate-b-upgrade.sh`; it re-verifies Devnet,
      artifact, payer, ProgramData slot, buffer bytes/funding, and simulation; it contains no buffer
      creation/write path and automatically runs the post-Upgrade verifier. Validate-only evidence:
      `Logs/bootstrap-gate-b-upgrade-20260920T190653Z.log`;
- [x] obtain exact separate Gate B approval for upgrading program `9312…2uUQ` from buffer
      `B5w4…EkdDY` on Devnet and refresh the validate-only preflight: exact hashes, one signer,
      5,000-lamport fee, `err=null`, 2,370 units; no sign/send. Evidence:
      `Logs/bootstrap-gate-b-upgrade-20260920T191534Z.log`;
- [x] operator-run the approved Upgrade-only wrapper and verify exact deployed bytes plus atomic
      buffer close/refund: signature `zhf6dP…MEwi`, finalized slot `501530147`, fee 5,000 lamports,
      741,248-byte deployed SHA-256
      `588249aecc1c57956a1acf525af31ffa1e756145f5e26b65724f45d499751e87`, buffer absent, payer
      balance 11,211,886,680 lamports. Evidence:
      `Logs/bootstrap-gate-b-upgrade-verified-20260920T192050Z.json`;
- [x] audit the linked Supabase project read-only: target migration absent, both smoke-grant
      functions absent, remote Stamp confirm still version 6, target table/RPC absent; local nine-test
      suite and three target typechecks green. Evidence:
      `Logs/bootstrap-server-gate-audit-20260920T191041Z.json`;
- [x] prepare, syntax-check, and validate-only run
      `backend/chainbound_game/scripts/run-bootstrap-server-gate.sh`; it pins all target hashes,
      excludes broad push/unrelated migrations, performs no mutation in validation, and proves the
      exact 741,248-byte upgraded live program hash before its mutation path. Latest evidence:
      `Logs/bootstrap-server-gate-20260920T211202Z.log`; the fresh pass still found the target
      table/RPC and both grant functions absent, Stamp confirm at version 6, and
      `mutations_performed=false`;
- [x] diagnose the first approved server-gate attempt: it stopped before SQL execution because the
      Windows Supabase CLI shim could not open an absolute WSL path; switch only the CLI `--file`
      argument to the equivalent relative path and prove it with a linked read-only file query.
      Failed-attempt evidence: `Logs/bootstrap-server-gate-20260920T211026Z.log`;
- [x] verify the second attempt's partial state: the exact table and atomic Stamp RPC exist, but
      migration history still lacks `20260920010000`, both grant functions are absent, and Stamp
      confirm remains v6. Move Supabase CLI execution to the repository root so its hardcoded
      `supabase/migrations` and `supabase/functions` discovery targets the intended top-level project;
      the guarded resume now skips SQL when both target objects are present, verifies the repaired
      migration-history row, and still deploys only the approved functions. Latest partial-state
      validate-only evidence: `Logs/bootstrap-server-gate-20260920T213747Z.log`;
- [x] obtain exact server-gate approval for migration `20260920010000` and only
      `ownership_prepare_devnet_smoke_grant`, `ownership_confirm_devnet_smoke_grant`, and
      `ownership_confirm_stamp`;
- [x] resume that approved gate without reapplying SQL: migration version `20260920010000` now
      matches local/remote history; smoke-grant Prepare/Confirm are ACTIVE v1, Stamp Confirm is
      ACTIVE v7, and all three enforce JWT. The wrapper verified no broad push or unrelated
      migration. Evidence: `Logs/bootstrap-server-gate-20260921T010216Z.log`; independent read-only
      verification: `Logs/bootstrap-server-gate-20260921T010429Z.log`;
- [x] verify both new smoke-grant functions remain fail-closed without a JWT: Prepare HTTP 401 and
      Confirm HTTP 401, with no credentials or transactions; evidence:
      `Logs/bootstrap-server-gate-no-jwt-smoke-20260921T010755Z.log`;
- [x] obtain explicit `APPROVED_DEVNET_SMOKE_PLAYER_BOOTSTRAP` for the exact Devnet two-PDA init;
- [x] syntax-check and validate-only run `run-ownership-player-bootstrap.sh`; it accepts only the
      approved token, derives the matching signer pubkey without logging key bytes/path, and delegates
      to the fail-closed exact-two-instruction client. Latest unsigned evidence:
      `Logs/ownership-player-bootstrap-20260921T012936Z.json`;
- [x] operator-run the approved wrapper and independently verify the two PDAs: signature
      `2JekRfswoRpZnr5QNGaTb4oaeXfLCxwS6g84e7CY6Kww1ea5Va2mpy9sF2CkeJ4uhHoyQWrYXYbC2XVGu8u37aci`
      finalized at slot `501665505`, with two instructions, no error, and a 5,000-lamport fee;
- [x] read back fresh RAM `balance=0`, `daily=0`, `lifetime=0` before any grant; the vault is empty
      with capacity 50 and next instance ID 1. Operator evidence:
      `Logs/ownership-player-bootstrap-20260921T013435Z.json`; independent readback:
      `Logs/ownership-player-bootstrap-20260921T013651Z.json`; combined verification:
      `Logs/ownership-player-bootstrap-verified-20260921T013725Z.json`;
- [x] resolve the first post-bootstrap Prepare failure
      `missing_env:CHAINBOUND_DEVNET_SMOKE_WALLET`: set only the exact public Devnet wallet pin in
      project `jhzqcbrzkausrrtcavuu`, then verify its stored SHA-256 matches
      `H68e...bBWq`. No keypair, JWT, or chain transaction was involved. Evidence:
      `Logs/bootstrap-smoke-wallet-env-pin-20260921T022426Z.json`;
- [x] reconfirm Prepare remains fail-closed after the environment update: a credential-free request
      returned HTTP 401 `UNAUTHORIZED_NO_AUTH_HEADER`; evidence:
      `Logs/bootstrap-smoke-wallet-env-pin-no-jwt-20260921T022700Z.json`;
- [x] perform a current-state read-only audit after the environment update: GameConfig still has cap
      100 and flags 0; OwnershipConfig still has Stamp 75 and Bind 250; all seven ownership functions
      are ACTIVE with JWT enforcement; focused server tests pass 9/9; both shipping prefab flags are
      still 0; and the exact grant idempotency key has zero database operation rows before retry.
      Current remote revisions are smoke-grant Prepare/Confirm v2 and Stamp Confirm v8.
      Evidence: `Logs/ownership-bootstrap-current-state-audit-20260921T022849Z.json`;
- [x] prepare and syntax-check the guarded grant client
      `backend/chainbound_game/scripts/ownership-bootstrap-ram-grant.cjs`; prepare mode validates
      the exact 100-RAM instruction, PDAs, server partial signature, zero starting state, cap 100,
      flags OFF, and unsigned simulation, while execute mode requires the separate token
      `APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100` and never records credential material. If the chain
      send succeeds but database confirmation is interrupted, `recover` mode locates and fully
      validates the exact receipt-creation transaction before idempotently completing only the
      server confirmation; it never signs or resends;
- [x] syntax-check the Prepare/Execute/Recover shell wrappers and verify Execute plus Recover reject
      missing approval with exit 64 before prompting, loading a keypair, or calling the network;
      evidence: `Logs/ownership-bootstrap-ram-grant-wrapper-audit-20260921T023208Z.json`;
- [x] diagnose the authenticated Prepare retry's local `Invalid arguments` failure: installed
      `@solana/web3.js` 1.99.0 rejects a simulation config on its legacy `Transaction` overload.
      Route the unchanged legacy message/signatures through the public `VersionedTransaction`
      simulation overload; unsigned replacement-blockhash and signed signature-verification probes
      both pass without loading the smoke keypair or sending. Evidence:
      `Logs/ownership-bootstrap-ram-grant-simulation-compat-20260921T023908Z.json`;
- [x] prepare and review the exact 100-RAM grant: zero starting balance, cap 100, flags 0, exact
      wallet/profile/receipt, result-authority signature present, player signature absent, and
      unsigned simulation green at 13,791 CUs. The server has exactly one `prepared` row with no
      transaction signature and the receipt remains absent on-chain. Evidence:
      `Logs/ownership-bootstrap-ram-grant-prepare-20260921T024204Z.json` and
      `Logs/ownership-bootstrap-ram-grant-prepare-review-20260921T024246Z.json`;
- [x] obtain the separate `APPROVED_ONE_DEVNET_SMOKE_RAM_GRANT_100` execution token; evidence:
      `Logs/ownership-bootstrap-ram-grant-execute-approval-20260921T024816Z.json`;
- [x] execute exactly one approved grant and independently verify the chain result: signature
      `4FbUZ5...WYrBUH`, slot `501693453`, `err=null`, receipt fields exact, RAM
      balance/daily/lifetime `100/100/100`, vault still empty, and flags still 0. Evidence:
      `Logs/ownership-bootstrap-ram-grant-state-20260921T025345Z.json`;
- [x] diagnose the post-send confirm failure: both confirmation functions incorrectly used the
      public-key regex for transaction signatures. Replace it with decoded 64-byte base58
      validation in source and add a regression test; focused tests pass 11/11. No function was
      deployed and no second transaction was sent;
- [x] obtain `APPROVED_DEVNET_CONFIRM_SIGNATURE_VALIDATOR_HOTFIX`, then deploy exactly
      `ownership_confirm_devnet_smoke_grant` and `ownership_confirm_stamp` with
      `run-ownership-confirm-signature-hotfix.sh`; no migrations, secrets, chain transactions,
      economics/config changes, or flag flips. Preflight passed 11/11 tests; remote revisions are
      now grant Confirm v3 and Stamp Confirm v9 with JWT enforcement; credential-free probes remain
      HTTP 401. Evidence: `Logs/ownership-confirm-signature-hotfix-20260921T025817Z.log` and
      `Logs/ownership-confirm-signature-hotfix-postdeploy-20260921T025910Z.json`;
- [x] run `run-ownership-bootstrap-ram-grant-recover.sh` after the hotfix: it located and validated
      the exact landed transaction, then confirmed the existing database row without a keypair,
      signing, or resending. Independent reads match signature `4FbUZ5...WYrBUH`, amount/balance
      100, daily/lifetime `100/100`, and on-chain flags 0. Evidence:
      `Logs/ownership-bootstrap-ram-grant-recovered-20260921T030527Z.json` and
      `Logs/ownership-bootstrap-ram-grant-state-20260921T104628Z.json`;
- [x] audit the scoped Stamp prerequisites: the exact Known belongs to the active smoke wallet and
      is unconsumed/unmaterialized, the vault is empty at next instance 1, and balance is 100. Pin
      the client to the original unsigned operation so it refreshes rather than duplicates it. Two
      separate expired diagnostic rows for the second Known remain as audit history; all three
      ticket/asset pairs are absent on-chain. Harden Prepare review to require the exact
      materialize/prepare/stamp instruction sequence and 5+75=80 RAM economics, leaving 20.
      Evidence: `Logs/ownership-moves-smoke-prereq-audit-20260921T105100Z.json` and
      `Logs/ownership-moves-smoke-prepared-rows-audit-20260921T110026Z.json`;
- [x] rerun keyless Stamp Prepare: exact instructions 0 and 1 simulated successfully, then Stamp
      failed at instruction index 2 with Metaplex Core error 29 (`ConflictingAuthority`) because
      `CreateV2` supplied both the Moves collection and a separate update authority. Nothing was
      signed or sent;
- [x] correct only the Stamp CPI to use the collection-derived update authority
      (`update_authority(None)`), leaving Bind inactive and untouched. Rebuild to 741,248 bytes,
      SHA-256 `5741a024e981c3f7558752726564466cb6beabeefffe61254a620b915b8a6e5b`;
- [x] verify 7/7 Rust tests, 12/12 ownership server tests, clean program autofixer, exact fit in the
      current 741,248-byte ProgramData capacity, and a green unsigned buffer-create simulation.
      Evidence: `Logs/ownership-stamp-core-createv2-hotfix-review-20260921T112411Z.json`;
- [x] obtain `APPROVED_STAMP_CORE_CREATEV2_HOTFIX_GATE_A_CREATE_UPLOAD_B5W4`;
- [x] run the exact Gate A wrapper in validation-only mode: buffer absent, artifact/hash/slot pins
      match, 733 writes, projected fees 3,675,000 lamports, and create simulation green. No signing
      or sending. Evidence:
      `Logs/stamp-core-createv2-hotfix-gate-a-upload-20260921T112939Z.log`;
- [x] run only `run-stamp-core-createv2-hotfix-gate-a-upload.sh`; do not upgrade in Gate A;
      first send hit `Max retries exceeded` after buffer create; resume-writes-only completed.
      Evidence: `Logs/stamp-core-createv2-hotfix-gate-a-upload-20260921T113534Z.log`,
      `Logs/stamp-core-createv2-hotfix-gate-a-resume-20260921T124652Z.log`;
- [x] verify the uploaded buffer payload exactly matches the approved artifact hash;
      Evidence: `Logs/stamp-core-createv2-hotfix-gate-a-resume-buffer-verified-20260921T124652Z.json`
      (`verified: true`, `matchesArtifact: true`, 734/734 txs, SHA-256 `5741a024…`);
- [x] refresh the unsigned Gate B-only plan: exact buffer/artifact hash, pre-upgrade slot
      `501530147`, one signer, instruction `03000000`, fee 5,000 lamports, and simulation
      `err=null` at 2,370 CUs. The wrapper has no buffer-create/write path and rejects missing
      approval with exit 64. No sign/send and no Gate A rerun. Evidence:
      `Logs/stamp-core-createv2-hotfix-gate-b-preflight-20260921T201833Z.json`;
- [x] obtain separate approval `APPROVED_STAMP_CORE_CREATEV2_HOTFIX_GATE_B_UPGRADE_B5W4`;
- [x] rerun the exact Gate B wrapper in validation-only mode: Upgrade-only instruction, matching
      hashes, one signer, 5,000-lamport fee, `err=null` at 2,370 CUs; no sign/send. Evidence:
      `Logs/stamp-core-createv2-hotfix-gate-b-upgrade-20260921T204020Z.log`;
- [x] run the approved Upgrade-only wrapper and byte-verify the live program: signature
      `5vVxZmk…NaLnF`, slot `502081429`, `error=null`, deployed SHA-256 `5741a024…a6e5b`, exact byte
      match, buffer absent/refunded, and no Gate A operation. Evidence:
      `Logs/stamp-core-createv2-hotfix-gate-b-upgrade-20260921T204216Z.log`,
      `Logs/stamp-core-createv2-hotfix-gate-b-upgrade-verified-20260921T204216Z.json`, and
      `Logs/stamp-core-createv2-hotfix-gate-b-complete-20260921T204224Z.json`;
- [x] rerun keyless Stamp Prepare after byte verification: original Known/idempotency pair, three
      instructions, exact signer set, 5+75=80 RAM leaving 20, and simulation `error=null` at
      104,206 CUs. `Custom:29` is gone; wallet signature absent; no signing/sending. Public RPC
      confirms the ticket and predicted asset remain absent. Evidence:
      `Logs/ownership-moves-smoke-prepare-20260921T204804Z.log`,
      `Logs/ownership-moves-smoke-mint-prepare-20260921T205011Z.json`, and
      `Logs/stamp-core-createv2-hotfix-complete-20260921T205208Z.json`.

The bootstrap capacity extension, prior buffer upload/upgrade, and exact Supabase server gate are complete.
The player bootstrap is finalized and the zero-balance policy is independently verified. The exact
100-RAM grant is now verified on-chain and confirmed in the database. The validator hotfix is
deployed. The keyless one-modifier Stamp Prepare exposed a program-only Core `CreateV2` authority
conflict. Hotfix Gates A and B are complete: the buffer was byte-verified, the Upgrade finalized at
slot `502081429`, and the live ProgramData exactly matches artifact `5741a024…`. The next action is
the keyless Stamp Prepare rerun only. Execute still requires separate one-mint approval after a green
Prepare. Funding with faucet Devnet SOL is necessary for rent but is not a starting RAM grant. Do
not rerun either program gate.

Gate A must create/upload only; it must not invoke Upgrade. After buffer verification, Gate B must
be separately approved and must only upgrade from that verified buffer. No ProgramData extension is
needed for this artifact.

Gate A create/upload and its resume path are historical and closed. **Do not run either Gate A
wrapper again.** The verified buffer above is the only allowed Gate B input.

Gate B is historical and closed. **Do not run the Gate B wrapper again.** The post-upgrade Prepare
is green, so this hotfix goal stops here. Do not execute the Moves mint, change Unity/DAS wiring,
flip either ownership flag, or start matrix work without a new goal and any required approval.

All three wrappers were invoked without approval and failed closed with exit code 64 before any
RPC mutation, signature, or log creation.

The wrapper hard-pins Known `9293fc49-b5d0-48d9-b071-504bdc9b3b00` and idempotency key
`moves-smoke-mint-20260919-1`; ambient shell values cannot redirect it to the profile's second Known
row. A prior `idempotency_key_conflict` was diagnostic only and signed/sent nothing.

The first wrapper request returned HTTP 401 at the Edge gateway and created no evidence file. The
later valid JWT passed authentication and exposed the on-chain blocker above. No transaction was
built, signed, or sent.

The next separately approved step is an **Editor-only Ownership Spine pass**. Do not start it here;
it is not a live-matrix GO or permission to enable either player-build flag.

Production/player composition always uses the real `VrfService`; the deterministic service is
Editor/test-only.

## 8. MagicBlock Devnet efficacy

This does not change any economy fee.

- [x] upgrade the pinned game program first;
- [ ] in an Editor efficacy session only, turn off `useDeterministicVrfInEditor`;
- [ ] use a funded connected Devnet wallet;
- [ ] run **Chainbound → Solana → Run MagicBlock VRF Devnet Efficacy**;
- [ ] verify client simulation succeeds before the wallet request;
- [ ] record request PDA, request signature, latency, 32-byte result, and consume signature;
- [ ] verify callback status Pending → Fulfilled and owner consume Fulfilled → Consumed;
- [ ] verify a second consume fails;
- [ ] restore the Editor deterministic switch afterward;
- [ ] add public signatures to the Devnet report.

Defaults are a 2-second poll interval and 30-second timeout. Investigate simulation failure, wallet
cancellation, wrong-owned/discriminator request accounts, callback delay, RPC timeout, or consume
replay separately. Stamp remains on approved authority-server entropy until a later reviewed track.

## 9. Live matrix and flag decision

Run every non-deferred row in the Devnet report with isolated identities. The cost shown in each row
must be 5, 75, or the blocked/deferred 150 pin as applicable.

- [ ] all required non-deferred rows green;
- [ ] mpl-core CPI success and rollback behavior observed live;
- [ ] DAS inventory verification green;
- [ ] request/response/signature evidence recorded without secrets;
- [ ] explicit GO decision recorded;
- [ ] only then submit a separate feature-flag change for approval.

Until then, the decision remains **NO-GO**.
