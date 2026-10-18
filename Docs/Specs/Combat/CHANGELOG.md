# Combat Changelog

## 2026-10-16 — Battle-effect follow-up

- Bean Sip grants +1 AP. Its recharge still uses `rechargeTurns` 2, which skips the next turn, and the log now says 1 turn.
- Recharge log text reports upcoming skipped turns (`rechargeTurns - 1`). AP Boost and Extend Protocol say 2 turns. Fox Feint's 999 sentinel says it cannot be used again this battle.
- Recharge keeps ticking while the Chainling is benched. The 2026-09-16 pause applied to recharge is reversed. Other statuses still pause on the bench.
- Timed status stage changes revert when the status expires. Refreshing the status extends the duration and does not stack apply-once stages.
- Sear Field's Atk -1 is a 1-turn status (`SearField`) so it reverts at the end of the granting turn. Immovable Stance's Def +2 is on the Immovable status and reverts with that 2-turn window.
- Group Hug (`4.5-a`) no longer copies a support move onto both allies. It grants +1 AP to each active ally.
- Ice Wall's description comma is quoted so the mitigation and speed-AP opcodes import. The restored mitigation hit budget is still 0, so that window stays inactive until a hit count is chosen.
- Unimplemented clauses were listed in `Docs/Combat/Audits/UNIMPLEMENTED_MOVE_EFFECTS.md` and were not built. Catalog statuses with no producer were researched and left unchanged.

## 2026-09-16 — Balance hotfix

- Locked new IV rolls and authored encounter ranges to `-5..+5`; existing saved IVs are unchanged.
- Chemistry advantage remains a damage multiplier but no longer bypasses walls.
- Recharging now ticks once per active end of turn, and benched durations remain paused.
- `RandomEnemy` now uses deterministic uniform selection across living opposing actives.
- Reflected barrier KOs stop remaining multi-hit strikes after the current hit resolves.
- Encrypted Seal now blocks harmful chemistry reactions through the same debuff rule.
