# Audit reconciliation v1 — verification record

Date: 2026-10-19

## Before baseline

The clean live-Editor gate passed before implementation:

| Scope | Total | Passed | Failed | Notes |
|---|---:|---:|---:|---|
| `CombatDecisionRoundTests` | 10 | 10 | 0 | Fresh run after the Pipeline main-thread gate recovered. Bench AP remained 4; Group Hug user/partner AP remained 3; owned stage expiry and Cleanse reversion passed. |
| All EditMode tests (last completed monolithic run) | 1107 | 1052 | 55 | Baseline recorded in `followups_v1_baseline_tests.md`; this is the comparison ceiling required by the plan. |
| `Chainbound.Combat.Tests` (last completed split run) | 670 | 655 | 15 | Saved as `Logs/combat-followups-v1-chainbound-editmode.json`. The result payload contains 20 failed leaves because timeout/cascade leaves are reported in addition to the 15-test summary. |

A fresh pre-change `Chainbound.Combat.Tests` run was attempted asynchronously. It stalled in `CombatPostBattleRouterCapturePersistTests.CaptureTerminal_PersistsInsteadOfSkipping` and was cancelled before changing combat data or runtime code. The completed same-tree split result above is therefore retained as the before baseline. Final verification must name any delta from both the 55 monolithic failures and the 15-test Chainbound summary.

## P0 preconditions

- `CombatDecisionRoundTests`: 10/10 passed.
- Prior `combat_balance_followups_v1` frontmatter reconciled from code evidence; its P9 remains pending and points to this plan's P8.
- Supabase shared parity URLs account for both the top-level path and the nested `Supabase/supabase/functions/_shared` junction path. Known-pool and move-AP parity passed 2/2 from both locations; the two ownership tests that read the on-chain program source passed from both locations.

## After implementation

The data/import/export pass is complete. The live catalog, move assets, gameplay manifest, and generated server tables reflect the 2026-10-19 decisions.

### Focused Unity results after the clean Editor restart

| Scope | Total | Passed | Failed |
|---|---:|---:|---:|
| `AuditReconciliationV1Tests` | 27 | 27 | 0 |
| `CombatCsvIntegrityTests` | 5 | 5 | 0 |
| `CombatIssuesRemediationV2Tests` | 14 | 14 | 0 |
| `ApCostLadderTests` | 17 | 17 | 0 |
| `CombatDecisionRoundTests` | 10 | 10 | 0 |
| `ManifestParityTests` | 1 | 1 | 0 |
| `KnownPoolGenerationTests` | 1 | 1 | 0 |

`CombatDecisionRoundTests` confirms bench AP remains 4, Group Hug leaves the user and partner at 3 AP, and owned stage deltas revert on expiry/cleanse. This covers the AP and status-expiry regressions called out before implementation.

The reconciliation fixture contains 27 named tests so each P1-P6 acceptance case is directly discoverable, including the canonical status-catalog reference/Kindled guard. All 27 passed after the clean Editor restart.

### Import and parity

- Moves CSV rows: 211; assets: 211; field diffs: 0; CSV-only ids: 0; asset-only ids: 0.
- Gameplay manifest SHA-256: `92D83D7D37CA6614ACF4170869B645BB9B94F57F1C30AC5FDF563E4C41CB6F02`.
- Manifest includes `ApplyChemistryStatus`, `StripBuffs`, `StripAllStatuses`, `StripBuffsThenStage`, and `HealSelfPerTargetDebuff`; Pulse Shot remains power 26.
- The combat scene references `Assets/Data/Combat/StatusDef/Catalog/StatusCatalog.asset`; the older sibling catalog is unreferenced. The canonical catalog contains Kindled.
- Top-level Supabase shared suite: 36/36 passed.
- Nested Supabase shared suite: 36/36 passed. The nested folder is an NTFS junction to the top-level `_shared` directory; path-aware test URLs cover either invocation path.
- Ownership spine: 32/32 passed; generated catalog contains 10 moves, 104 modifiers, and 210 authorities; `grind_tax` occurs zero times.
- `Chainbound.BattleService.csproj --no-restore`: passed with 0 warnings and 0 errors.
- `Chainbound.CaptureCoreVerification --no-restore`: passed.

### Broad EditMode follow-up

The clean Editor restart restored the Pipeline execution gate. The live runner then enumerated 1,167 EditMode tests across three assemblies.

| Assembly | Enumerated | Result | Notes |
|---|---:|---|---|
| `Assembly-CSharp-Editor` | 406 | Cancelled at the 300-second ceiling | The async runner remained responsive but did not publish a completed result. |
| `SignalJam.UI.MenuNavigation.Editor` | 61 | 57 passed, 4 failed | Failures are confined to unrelated dossier/menu presentation tests: two dossier prefab authoring assertions, one unbound party layout assertion, and one locked-tab null reference. No combat runtime or combat-data test failed in this assembly. |
| `Chainbound.Combat.Tests` | 700 | Cancelled at the 300-second ceiling | The async runner repeated the known broad-suite hang and was cancelled cleanly. The completed 670/655/15 same-tree result remains the broad comparison baseline. |

Because both long-running shards were cancelled before publishing summaries, they do not establish a new whole-suite pass count. They also do not supersede the completed focused results above. The scoped combat acceptance gate is green; the four completed UI failures are out of scope for this combat reconciliation, while the pre-existing broad-run hang remains a separate test-infrastructure issue.
