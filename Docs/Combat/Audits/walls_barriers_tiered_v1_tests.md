# Walls & Barriers Tiered v1 — Verification Record

Date: 2026-10-20 through 2026-10-21

Reference: `Docs/Combat/Design/WALLS_BARRIERS_PVP_REDESIGN_V2.md`

## Baseline (before implementation)

| Surface | Result |
|---|---|
| `CombatWallInterceptTests` | 12 passed, 0 failed |
| Combat UI EditMode assembly | 61 total, 57 passed, 4 failed |
| Authoritative PvP Deno test | 10 passed, 0 failed before the new duplicate-block case |
| Nested `_shared` Deno suite | 117 total, 106 passed, 11 failed |

The four pre-existing UI failures were:

1. Dossier pages vertical wireframes.
2. Pixeloid labels stamp ink.
3. Unbound layouts hidden.
4. Locked menu tab focus submit.

The eleven pre-existing nested Deno failures were in bindery (2), hub (2), ownership proxy (1), ownership metadata (1), player display name (1), PvP migration path (1), RAM ledger (2), and wallet uncaught path (1).

## Implementation checks completed before Unity import

| Check | Result |
|---|---|
| Authored `Moves.csv` assertions for the eight block moves plus Guard Posture | 9/9 rows passed |
| `git diff --check` for in-scope combat/data/server files | Passed; line-ending warnings only |
| `deno check` for `pvp_rules.ts`, `pvp_matchmaking_test.ts`, `move_flags.generated.ts`, and `move_ap.ts` | Passed |
| `pvp_matchmaking_test.ts` including `block_target_duplicate` | 11 passed, 0 failed |
| `known_pool_parity_test.ts` | 1 passed, 0 failed |
| `move_ap_parity_test.ts` before Unity reimport/manifest export | Expected failure: the authoritative manifest still contains the pre-pass AP values |

## Added focused EditMode coverage

`WallsBarriersTieredV1Tests` currently contains 24 tests covering:

- Wall/melee and Barrier/ranged filtering.
- Tier percent mitigation, one-hit consumption, misses, two-turn lifetime, and target release.
- Held and teammate-queued target denial, all-target denial, passive/modifier override, and resolver no-spend safety.
- Guard Posture Armor stacking and prefix-only wall tags.
- Recharge display, Blockade presentation, and Core Breach bypass semantics.
- Ice Wall AP, Lattice reflection, priority ordering, swap/bench lifecycle, and DoT behavior.
- AI target deconfliction, category-aware valuation, and Greater-tier AP banking.
- Imported move display names, AP, recharge, rarity, type, targeting, PRIOR tags, stack keys, and Guard Posture Armor metadata.

The existing suites also contain the rewritten one-hit multi-hit sequence, Ember AP retaliation, and D1 Ignore Walls/Wallbreaker coverage.

## Final Unity gate after clean restart

Unity 6000.0.24f1 restarted as PID 12816, compiled without errors, and accepted live Editor commands.

| Check | Result |
|---|---|
| Combat Data Importer | `Moves.csv` imported successfully |
| Blockade presentation | `wallSfxId = wall_blockade`; existing `wallSprite` preserved |
| CSV/asset parity | 211 CSV rows, 211 assets, 0 field diffs, 0 CSV-only, 0 asset-only |
| `WallsBarriersTieredV1Tests` | 26 passed, 0 failed |
| `CombatWallInterceptTests` | 12 passed, 0 failed |
| `CombatActionSelectorBridgeTests` | 3 passed, 0 failed |
| `CombatDecisionRoundTests` | 10 passed, 0 failed |
| `AuditReconciliationV1Tests` | 27 passed, 0 failed |
| `CombatNarrationComposerTests` | 19 passed, 0 failed |
| Modifier resolver fixtures | 15 passed, 0 failed |
| Wall popup labels | 2 passed, 0 failed |
| CSV/manifest/pool/SFX/manual-provider fixtures | 24 passed, 0 failed |

The AP-sensitive decision-round coverage passed, including bench recharge and Group Hug. The stage-expiry coverage also passed for both apply-once and repeating stage effects.

`CombatPopupVisualTests` remained 12/15 because of three unrelated facing/anchor assertions. These are outside the wall/barrier changes. The separate menu UI assembly baseline remains 57/61 with its four pre-existing authoring failures.

The full 724-test `Chainbound.Combat.Tests` live-Editor run did not return an aggregate before the 900-second Pipeline timeout. It emitted no failure payload, but this is not counted as a passing broad-suite result; the focused fixtures above are the acceptance gate. After the runner released, Unity reported a completed compile with no errors and `test_status = no_tests` before the final focused reruns.

## Final server/export gate

| Check | Result |
|---|---|
| Authoritative gameplay manifest export | Completed |
| Server table export | Completed to both `_shared` trees |
| Top-level PvP/AP/Known Deno tests | 13 passed, 0 failed |
| Nested PvP/AP/Known Deno tests | 12 passed, 1 pre-existing fixture-path failure |
| Deno formatting | 10 relevant files clean |
| Post-implementation balance checker | 158 passed, 0 failed |

The nested failure is the baselined `pvp_matchmaking_test.ts` migration lookup for a migration that is not mirrored beneath `Supabase/supabase/migrations`; the gameplay validation and both parity tests pass there.

The repository-local checker validates authored data, resolver/legality/UI/AI anchors, imported assets, manifests, both generated server-table trees, and focused test coverage. The external Monte Carlo artifacts (`walls_pvp_v2_sim.py` and its JSON snapshots) are not present in this workspace, so the checker reports that simulation portion as not evaluated rather than fabricating a regression result.

Authoritative gameplay manifest SHA-256:

`D0AA02FE2DA0DC48F0DCC0528422DC487D9DC70465E120524AF8A58C9181BF37`

The final added cases passed: Compound Barrier recast stacking, AI safety-net non-fire, and selector back-navigation conflict clearing. Final walls/barriers focused acceptance status: **green**. The broad live-Editor runner limitation and unrelated baseline failures remain explicitly excluded from that focused gate.
