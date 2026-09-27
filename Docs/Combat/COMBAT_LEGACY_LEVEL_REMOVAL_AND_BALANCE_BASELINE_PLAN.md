# Combat Legacy Level Removal & Balance Baseline Plan

> **Purpose:** Deprecate the legacy numeric level (1–100) pipeline, align combat with **stars + rebirth** only, and establish a temporary PvP-friendly damage baseline.  
> **Status:** Complete — legacy combat level removed; stars/rebirth authoritative; chemistry and flat progression baseline landed.

---

## 1. Goals

1. **Remove legacy level confusion** — No `ResolveLegacyLevel`, no `ProgressionLevelEquivalent`, no `(level + 37) / 100` damage scaling.
2. **Stars + Cycle are authoritative for progression cadence** — Combat power uses Cycle flats from `rebirthCount` + `rebirthIvBonuses`; stars drive rewards/learn gates/initiative tie-breaks, not multiplicative stats.
3. **Chemistry baseline** — Per-stack **×1.2** advantage and **×0.8** resistance (`1.2^Adv × 0.8^Dis`), with resistance inferred from reverse type matrix lookups when the forward cell is empty.
4. **Progression damage factor = 1.0** — Flat multiplier for now; revisit after PvP modifier/burst tuning.

---

## 2. What Was Removed / Replaced

| Legacy | Replacement |
|--------|-------------|
| `ChainlingInstance.level` | Removed; use `stars`, `rebirthCount` |
| `ProgressionLevelEquivalent` | Removed |
| `GetLegacyLevelMultiplier` / `ResolveLegacyLevel` | Removed from `ChainlingProgressionUtility` |
| Damage `levelFactor = (level + 37) / 100` | `CombatConstants.PROGRESSION_DAMAGE_FACTOR` (= 1.0) |
| Chemistry `1.3^(Adv − Dis)` | `1.2^Adv × 0.8^Dis` |
| Initiative tie-break: higher legacy level | Higher `stars`, then `rebirthCount` |
| Snapshot `level` for combat power | `stars` / `rebirthCount`; `level` retained only as optional import hint (migrated once to stars when `stars == 0`) |

**Not changed in this pass:** `LevelUpMoveGrantService` inscription **tier level** (MVP cap 5) — different concept from Chainling combat level.

---

## 3. Cycle Flat Progression (Authoritative Combat Power)

**Final pre-stage stat (PvE = PvP):**

```
final = max(1, speciesBase + baseIV + cycleAutoFlat + cyclePickFlat)
```

| Source | Combat stats | MaxHP |
|--------|--------------|-------|
| Cycle auto (`rebirth_count` N) | `+N` | `+5N` |
| Cycle pick (`rebirth_iv_bonuses` counts) | `+1` per point | `+5` per point |
| Stars | no combat effect | no combat effect |

`GetStatScaleMultiplier` is legacy and always returns `1`. Stars remain reward cadence (XP, star-up rolls, learn odds, mastery, Cycle gate).

**Initiative tie-break (after Speed):** higher `stars`, then higher `rebirthCount`, then RNG.

**One-time snapshot import:** If `stars == 0` and `level > 0`, derive stars via `ApproximateStarsFromLegacyLevel(level)` at roster boundaries only.

---

## 4. Damage Formula (Current Baseline)

```
preMitigation = floor(
  Power × (Atk ÷ Def) × PROGRESSION_DAMAGE_FACTOR × Random(0.85–1.0) + additiveConstant
)
× chemistryMultiplier × crit × moveTuning …
→ mitigation (10% floor)
```

- `PROGRESSION_DAMAGE_FACTOR = 1.0` (all stages, PvE and PvP).
- Progression power on Atk/Def comes from Cycle flats on base+IV (plus stages, chemistry, modifiers) — not star/Cycle multipliers.

---

## 5. Chemistry (Current Baseline)

Per defender type on a damaging hit:

1. **Forward** `GetReaction(attackType, defenderType)` — apply statuses; each successful application stack adds **+1 ChemAdv**.
2. **Resistance** — If forward produces no outputs, **reverse** `GetReaction(defenderType, attackType)` adds **+1 ChemDis per reverse output count** (statuses not applied on resist-only path).

**Damage multiplier:** `1.2^chemAdv × 0.8^chemDis`

| Adv | Dis | Multiplier |
|----:|----:|-----------:|
| 0 | 0 | ×1.00 |
| 1 | 0 | ×1.20 |
| 0 | 1 | ×0.80 |
| 2 | 0 | ×1.44 |
| 3 | 0 | ×1.73 |

---

## 6. Follow-Up (Not This Pass)

- [ ] Revisit whether `PROGRESSION_DAMAGE_FACTOR` should ever differ from 1.0 in PvP.
- [x] Spec sync: `COMBAT_Resolution.txt` damage/chemistry baseline.
- [x] Spec sync: `COMBAT_UI.txt` star display and chemistry preview strings.
- [ ] Explicit resistance cells in matrix CSV (optional; reverse lookup is the interim rule).
- [ ] PvP damage caps / chemistry exponent caps for modifier-heavy metas.
- [ ] Supabase schema: stop persisting legacy `level` on `chainlings` when backend is ready.

---

## 7. Verification

- [x] `ChainlingProgressionAndChemistryTests` — Cycle flats, unused scale multiplier, flat progression factor, chemistry 1.2/0.8.
- Manual: equal-star mirror match — damage should not swing from legacy levelFactor spread.
- Forensic log: `levelFactor` should read `1.0`; initiative traces use `stars` / `rebirthCount`.
