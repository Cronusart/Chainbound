# Ownership Spine — Isolated Live Matrix Plan (Devnet)

Status: **PLAN ONLY**. Live matrix remains **NO-GO**. Do not flip `enableDevnetOwnershipSpine`, mint, Stamp, or client roll until an explicit operator GO.

Last updated: 2026-09-19

## Purpose

Define the smallest isolated Devnet matrix that proves end-to-end ownership after Phase 1 config + Edge Function smokes, without shipping a live player build.

## Preconditions (must already be green)

- [x] Program upgrade + GameConfig / OwnershipConfig on Devnet
- [x] Result-authority rotation + `CHAINBOUND_RESULT_AUTHORITY_SECRET` set
- [x] `ownership_roll_known` smokes: valid JWT 200, no JWT 401, spoofed `player_profile_id` 401
- [x] Unity `SolanaConfig` Moves collection + public Devnet RPC/DAS (flags OFF)
- [x] Unity Supabase anon-only (no service-role)
- [x] Read-only Moves DAS probe (collection resolves; inventory may be empty)

## Explicit non-goals

- Mainnet
- Putting Helius / admin / service-role / result-authority secrets in Unity
- Enabling ownership flags in a shipping player build
- Changing economy pins (Known 5 / Stamp 75 / Bind 250) during the matrix

## Isolation rules

1. Use a **dedicated Devnet operator wallet** and a **dedicated smoke Auth user** (not production players).
2. Keep Editor/test deterministic VRF as documented for non-live paths; live matrix uses the approved Devnet VRF path only when that gate is explicitly called.
3. One operator at a time; log every signature and Edge Function response under `Logs/`.
4. If any step fails closed, **stop** — do not flip additional flags to “make it work.”

## Proposed matrix lanes (execute only after GO)

| Lane | Action | Pass criteria | Abort if |
|------|--------|---------------|----------|
| M0 | Re-run read-only DAS probe + flag audit (`enableDevnetOwnershipSpine: 0` on CandyMachinePanel + GameServices) | Probe OK; both flags still 0 | Flag drift |
| M1 | Fund smoke profile RAM (≥ 5 + buffer) via SQL | `ram_balance` sufficient | Wrong profile id |
| M2 | `ownership_roll_known` happy path (JWT) | HTTP 200; RAM −5 | 5xx / wrong authority |
| M3 | Spoof profile + no JWT negatives | Both 401 | Any 200 |
| M4 | **Optional GO:** enable flags **only in Editor** on a throwaway scene | Prefab/scene flags documented; not saved into shipping prefabs without review | Prefab accidentally saved ON |
| M5 | Prepare Stamp / mint path against Moves collection (Edge Function only) | Signature + DAS shows new asset in collection | Client-signed mint; wrong collection |
| M6 | Disable flags; re-export checklist | Both flags 0; report updated | Flags left ON |

## Flag policy

- Default: **OFF** everywhere.
- Temporary Editor-only ON requires a written GO naming scene/prefab and a same-day OFF restore.
- Shipping prefabs (`CandyMachinePanel`, `GameServices`) must end the session at `0`.

## Evidence pack (required before calling matrix green)

- Redacted Edge Function JSON + HTTP codes
- Solana signatures (mint/Stamp only)
- DAS `getAsset` / `getAssetsByGroup` snippets for any new Moves asset
- Prefab YAML grep showing `enableDevnetOwnershipSpine: 0` after restore
- Updated lines in `OWNERSHIP_SPINE_PHASE1_DEVNET.md` + operator checklist

## Authorization

This document does **not** authorize M4–M6. Operator must reply with an explicit GO naming which lanes are in scope.

## Execution log (operator)

### 2026-09-19 — M0–M3 (no flag flip)

**Clarification:** Entering Unity Play Mode / walking menus does **not** write the Editor DAS probe log. That log is only created by the menu item **Chainbound → Ownership → Probe Moves Collection (DAS read-only)** (or the CLI probe).

| Lane | Result | Evidence |
|------|--------|----------|
| M0 flags | PASS — both `enableDevnetOwnershipSpine: 0` | `CandyMachinePanel.prefab`, `GameServices.prefab` |
| M0 DAS | PASS — `MplCoreCollection`, authority match, inventory total 0 | `Logs/live-matrix-M0-20260919T205449Z.json`; earlier CLI `Logs/moves-collection-das-probe-20260919T152717Z.json` |
| M0 Editor menu log | NOT FOUND — Play Mode only; menu item not run | No `moves-das-editor-probe-*.log` |
| M1 RAM | PASS earlier today — smoke profile funded to 100 RAM | Pre-roll balance before Known roll |
| M2 Known roll | PASS — HTTP 200, RAM 100→95, `baseline_slam` instance | Operator curl smoke (valid JWT) |
| M3 negatives | PASS — no JWT 401; spoofed `player_profile_id` 401 `authenticated_profile_not_found` | Operator curl smokes |

**Production proximity:** M0–M3 prove config, auth, billing, and spoof rejection on Devnet Edge Functions with Unity flags still OFF. They do **not** enable live mint/Stamp/client ownership. Next production-ward step after GO would be M4+ (Editor-only flags / Stamp) — still not authorized here.

