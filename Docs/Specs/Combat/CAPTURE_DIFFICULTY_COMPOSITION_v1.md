# Capture Difficulty Composition v1

**Status:** Practice path shipped for species kits, trait tables, and the 15% HP cliff. SQL assignment rows and net-IV spectrum are still follow-ups.  
**Playtest:** 2026-09-04 — `CaptureBalanceConfig.asset` full-HP levers remain raised (see [Test run](#test-run-2026-09-04)).  
**Related:** [AuthoritativeTradingTraceCapture.md](../../Architecture/AuthoritativeTradingTraceCapture.md), [LEVER_BOARDS_v1.md](LEVER_BOARDS_v1.md), [PROFILE_FIRST_CAPTURE.md](PROFILE_FIRST_CAPTURE.md)

## Goal

One readable stack that stays competitive and achievable:

1. **Who is this species?** → volatility profile (Calm → Extreme) on `SpeciesDefinitionSO`
2. **What extra traits did this specimen roll?** → global multipliers (variant rarity, mutated + mutation rarity, entangled)
3. **How strong is this exact body?** → HP cliff + ramp, positive-IV bonus, Alertness
4. **What is the player bringing?** → orb, head/body, boosts (already shipped)

Each layer has one owner. Do not encode species specials in `CaptureChallengeController` or encounter trigger scripts.

---

## What a capture profile actually is

`CaptureVolatilityProfile` is **not** a catch percentage. It is a named motion kit:

| Profile | Motion | Timer | Spike chance | Feel |
|---|---:|---:|---:|---|
| Calm | 0.75× | 1.15× (23s) | 0.75× | Starter / common |
| Standard | 1.00× | 1.00× (20s) | 1.00× | Default wild |
| Volatile | 1.30× | 0.875× (17.5s) | 1.30× | Scarcer wild |
| Extreme | 1.60× | 0.65× (13s) | 1.60× | Trophy / event |

Numbers come from `Assets/Data/Combat/Capture/CaptureProfileCatalog.asset`. They scale the global trace physics on `CaptureBalanceConfig` (max speed, accel, mean reversion, spike chance). Band size, lock rate, and escape rate are **not** profile-scaled today.

This is independent of `EncounterDifficulty` (`Standard` / `Elite` / `Boss`), which is battle cadence, not capture motion.

---

## What selects a wild Chainling's profile

Resolution order in `CaptureChallengeResolver.ResolveDifficulty`:

```
assignedProfile (SQL / live event, when present)
        ↓
SpeciesDefinitionSO.defaultCaptureProfile  (practice: always stamped by CombatFactory)
        ↓
MapLegacyBaseRate(legacyBaseCaptureRate)   (fallback only)
```

Practice passes `assignedProfile: null`. New `ChainlingInstance`s created from a species definition stamp `hasCaptureProfile` + `captureProfile` from `defaultCaptureProfile`. Species assets currently default to **Standard** until you set Calm / Volatile / Extreme per species.

`baseCaptureRate` on encounter entries is a **legacy name**. It is not a roll-to-catch %. It only maps when no species profile was stamped: ≥70 Calm, ≥40 Standard, ≥20 Volatile, else Extreme.

Postgres `capture_profile_assignments` can still override by species / variant / mutation / zone when rows exist. No rows are seeded; that table does not affect current catches.

---

## Current stacking (shipped)

All motion terms are basis points (`10000` = 1.0×) and **add**, then scale the global trace numbers.

```
hpResistance = 0                                            if HP% ≤ 15%
             = floor + (1 − floor) * t^curve                otherwise
               t = (HP% − 15%) / 85%
               floor default 5000 (50% of the full-HP bonus at 16%)
               curve default 1000 (linear)

velocityMult = profile.motion
             + fullHpVelocityBonus   * hpResistance
             + ivBonus               (positive non-HP IV sum / 25 * max)
             + traitMotion           (variant + mutated + mutation rarity + entangled)
             + alertness * alertVel
             − gear.stability

accelMult    = same shape as velocity, with fullHpAccelBonus

timerMult    = profile.timer
             − fullHpTimerPenalty    * hpResistance
             + gear.captureTime
             (traits do not shorten the timer)

spikeChance  = global.spikeChance * (profile.spikeChance + traitMotion * traitSpikeFrac)
spikeSize    = global.impulse * (profile.motion + traitMotion + alertness*spikeAmp − stability)
```

**HP cliff:** at or below 15% there is no HP motion/timer penalty. The first tick above 15% applies `hpAboveThresholdFloorBasisPoints` (default 50% of the full-HP bonus), then ramps to 100% HP. Full HP still uses `resistance = 1`.

**Traits (global tables on `CaptureBalanceConfig`, not on visual sets):**

| Trait | Default add | Notes |
|---|---:|---|
| Variant rarity | 0 / 500 / 1200 / 2000 / 3000 / 4500 | Looked up from `CombatVisualDatabaseSO` catalog rarity |
| Mutated (flat) | 1500 | On top of mutation rarity |
| Mutation rarity | 0 / 400 / 800 / 1500 / 2500 / 4000 | Stamped from the wild mutation table at roll time |
| Entangled (flat) | 2000 | Already-entangled wilds, not Entangle Orb |
| Trait → spike | 4000 (40% of trait motion) | Timer stays HP-owned |

**IV today:** sum of `max(0, iv)` across Melee Atk/Def, Ranged Atk/Def, Speed. Range 0…50. Caps at `positiveIvMotionBonusMaximumBasisPoints` (2000 = +20% motion). Negative IVs do nothing. MaxHP IV is excluded.

**Authoring:** set `defaultCaptureProfile` on each `SpeciesDefinitionSO`. Tune trait and HP numbers on `CaptureBalanceConfig`. Use **Tools → Chainbound → Combat → Capture Balance Simulator** (includes mutated / entangled / rarity sliders).

---

## Test run (2026-09-04)

Asset-only change on `Assets/Data/Combat/Capture/CaptureBalanceConfig.asset`. Core test defaults (`CaptureBalanceData.CreateDefault`) stay at 3500 / 4500 / 2500 so EditMode tests do not move.

| Lever | Was | Playtest (still on asset) |
|---|---:|---:|
| `fullHpVelocityBonusBasisPoints` | 3500 | **7500** |
| `fullHpAccelerationBonusBasisPoints` | 4500 | **9000** |
| `fullHpTimerPenaltyBasisPoints` | 2500 | **4000** |

Standard at 100% HP is about **1.75× speed, 1.90× accel, 12s** before traits. Extreme at 100% HP is about **2.35× speed, 2.50× accel, 5s**. Low-HP is now ≤**15%**.

---

## Remaining (not this pass)

- **Net IV spectrum** — replace positive-only IVs with −50…+50 so deep negatives make the trace easier.
- **SQL `capture_profile_assignments`** — seed rows if a species/zone exception must beat the species default. Practice still passes `assignedProfile: null`.
- **Preview line** on the capture board (`Profile Standard · HP 100% → …`).
- **Alertness writer** — field works if > 0; practice still does not increment it.
- Authority variant-rarity ranks stay 0 until the visual catalog is exported with the manifest.

Do not implement those from presentation code or from `baseCaptureRate` hacks on a single mutated species.
