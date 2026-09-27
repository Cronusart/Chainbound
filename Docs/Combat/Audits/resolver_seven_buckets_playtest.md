# Resolver seven-buckets beta playtest

Use deterministic seeds where possible. For each case, inspect the combat event log for the named move, its resolved target, and a fired/skipped reason. Repeat a wall or conditional case with `signal_redirect`, a multi-hit move, and Relay/ChemAdv barrier bypass.

## 11-species smoke

| Species | Flagship | Pool checks | Expected result |
|---|---|---|---|
| Validape | `hammer_sync` | `network_response`, `overflow_reboot` | Hammer scales only per currently buffed partner and caps at +30%; armed miss/KO windows fire once |
| Gilarime | `cryo_ray` | `stasis_field`, `chilling_retort` | Entry Speed loss fires on a later enemy swap; next incoming debuff reflects |
| Kitsauto | `blade_flurry` | `automation_burst`, `fox_feint` | Per-target random stage rolls are seeded; feint dodges/counters once and cannot be recast |
| Kreechank | `bug_payoff` | `chaos_strike`, `chaotic_resonance` | Two live debuffs enable +50%; chaos choices are seeded and still respect walls |
| Cinislug | `ember_wall` | `finish_burn`, `flame_charge` | Wall absorbs and drains attacker AP; HP/AP conditions re-evaluate at action resolve |
| Snek | `stake_heal` | `ap_reserve`, `patience_passive` | Banked AP returns next turn with bonus; non-attack EOT builds Def and heals at +3 |
| Whale | `barrier_wall` | `streamlined_flow`, `defensive_reflex` | Hit/turn budgets expire; high-HP AP discount and passive mitigation compose without bypassing caps |
| Lad | `refined_striker` | `field_split`, `parallel_passive` | Later enemy entrant loses Evasion; ally hits can raise owner Speed only to +2 |
| Ghosty | `memory_leak` | `phantom_hack`, `firewall_passive` | AP tax applies at the target's next turn start; no-buff rider and projectile mitigation tell the truth |
| Deghoul | `fuel_surge` | `vapor_barrier`, `momentum_shift` | Baseline remains stable; barrier blocks one hit; last-turn hit ledger gates momentum |
| Caw | `fork_split` | `charged_relay`, `surge_recovery` | Distinct-target ledger pays +2 AP at two targets; existing conditional bonus-heal payload survives reimport |

## Full-combat interaction lens

- Targeting: exercise Self, SingleAlly, SingleEnemy, AllEnemies, and Field; consume Broadcast/Bridged once.
- Priority: a PRIOR action that removes a condition must change a slower move's result.
- Walls: verify mitigation hit/turn budgets, full absorb, threshold reflection, ember AP drain, redirect, Relay, Unsealed, ChemAdv, and multi-hit consumption.
- Redirect: conditional damage reads the final interceptor; the interceptor receives hit/damage reactive hooks.
- Passives: direct and end-of-turn KOs fire OnKO/OnAllyKO once; ThreadLock blocks AP gains.
- Swap: FrozenAssets prevents voluntary swap, Immovable does not, entry fields fire after swap-in modifiers, and armed windows clear on swap-out.
- AP: costs, discounts, surcharge, bank/release, steal/drain, and refunds stay within the AP cap.
- Status/stages: move data never creates ChemistryOnly statuses; stage changes stay in the global range and authored local caps.
- Logging: every conditional/passive/random branch records enough context to explain fired versus skipped.
