# Competitive Ladder & Seasons — Groundwork v1

**Status:** Groundwork / pre-launch (do not soft-launch ranks until population gates pass)  
**Date:** 2026-07-21  
**Related:** [`CHAINLING_PVE_SERVICE_RECORD.md`](CHAINLING_PVE_SERVICE_RECORD.md), [`CHAINLINGS_PARTY_TAB.md`](../UI/CHAINLINGS_PARTY_TAB.md) §5.6, economy / rarity “power ≠ rarity” heuristics

---

## 0. Purpose

Define the **smallest durable backend + product spine** so Chainbound can later launch a **fair, seasonal competitive ladder** without rewriting telemetry or Party Stats UI.

This doc is intentionally **not** a full ranked-PvP ruleset. It locks:

1. What we measure vs what we rank.
2. Population scopes that stay fair.
3. Season lifecycle and soft-launch gates.
4. Schema / job groundwork we can ship early at low cost.
5. How Party Stats may eventually show **Top %** without live global rank scans.

---

## 1. Non-goals (now)

- Exact `(#346)` ranks on every Party Stats line at menu open.
- Live `COUNT(*) WHERE metric > mine` against `chainlings` on every roster fetch.
- Treating lifetime PvE damage farm as the primary competitive prestige.
- Pay-to-win rank advantages (premium access must not buy ladder placement).
- Shipping a public leaderboard UI before soft-launch gates pass.

---

## 2. Design principles

| Principle | Rule |
|-----------|------|
| **Skill > grind volume** | Competitive boards prefer **mode-tagged ranked outcomes** over raw lifetime `pve.damage_*`. Career totals remain a dossier / brag layer. |
| **Species-fair by default** | Primary competitive brackets are **per species** (or species + role). Global all-creatures boards are secondary / novelty. |
| **Seasons reset prestige** | Seasonal rating and seasonal boards reset; career service record does **not** reset with seasons (except metrics already defined to reset on rebirth). |
| **Rarity ≠ power** | Ladder eligibility and rating must not grant raw combat power for holding Mythics / entangled variants. Cosmetics and chase stay orthogonal. |
| **Cache ranks; never rebuild on open** | Client reads materialized percentile / rank fields. Jobs or battle-end queues update them. |
| **Percentile first** | Soft-launch presentation prefers `Top 12%` over exact `#N` until population is large and stable. |
| **Honest cold start** | Hide or dim ladder chrome until minimum population / battles thresholds are met. |

---

## 3. Two metric layers (do not conflate)

### 3.1 Career service record (exists today)

Source of truth: [`CHAINLING_PVE_SERVICE_RECORD.md`](CHAINLING_PVE_SERVICE_RECORD.md) columns on `chainlings`.

- Lifetime / career dossier numbers (victories, damage dealt/taken, XP earned, etc.).
- Party Stats summary binds these (§5.6).
- Suitable later for **optional career percentiles** (same-species), not as the ranked ladder itself.

### 3.2 Competitive ladder metrics (new)

Separate, season-scoped aggregates that only increment from **eligible competitive matches**.

| Field family | Intent |
|--------------|--------|
| `season_id` | Active season key |
| `bracket_key` | e.g. `species:gilarime` or `open` |
| `rating` | Primary skill rating (ELO / Glicko / custom — algorithm TBD) |
| `season_wins` / `season_losses` | Mode-eligible outcomes only |
| `season_matches` | Sample-size gate for display |
| Optional: `season_damage_dealt` etc. | Flavor boards; **not** the primary ladder sort |

**Rule:** PvE wild battles feed the **career** record. They do **not** move competitive `rating` unless a future “ranked PvE challenge” mode is explicitly tagged eligible.

Until ranked PvP (or tagged competitive PvE events) exists, implement schema + idle jobs only; do not surface player-facing ranks.

---

## 4. Population scopes

| Scope | Use |
|-------|-----|
| **Species bracket** | Default fair competitive board |
| **Open / mixed** | Optional secondary board; expect role/species skew |
| **Player rating** | Account-level ladder (best-of / team rating) — phase after Chainling brackets |
| **Career percentile (species)** | Soft brag on Party Stats / Archive without implying ranked skill |

Always store `population_n` (or bucket metadata) with any presented rank/percentile so UI can hide when `n` is too small.

---

## 5. Presentation contract (when enabled)

### 5.1 Soft presentation (recommended first)

- Party Stats / Archive: `Top 12%` (species career or season board).
- Exact `#346 of 120,004` only on a dedicated ladder / drill-down screen.

### 5.2 Hide conditions

Do not show ladder chrome when any of:

- Feature flag `competitive_ladder_ui` is off
- `population_n < N_min` (suggest start: **200** Chainlings in bracket, tune later)
- `season_matches < M_min` for rating boards (suggest start: **10**)
- Client offline / ranks stale beyond TTL and no cached value

### 5.3 Party Stats relationship

Per [`CHAINLINGS_PARTY_TAB.md`](../UI/CHAINLINGS_PARTY_TAB.md) §5.6:

- Summary ships **without** ranks.
- Optional later seasoning: append muted `· Top 12%` on damage or victories **only** after materialization + flags.
- Never block Stats summary bind on ladder RPC failure — ranks are progressive enhancement.

---

## 6. Season lifecycle

```text
Draft → Scheduled → Active → Soft-freeze → Ended → Archived
```

| State | Behavior |
|-------|----------|
| **Draft** | Designers set `season_id`, dates, eligible modes, reward table IDs |
| **Scheduled** | Visible in “next season” copy; no rating writes |
| **Active** | Eligible matches update seasonal rows |
| **Soft-freeze** | No new matches count; finalization job runs |
| **Ended** | Ratings immutable; rewards grant job |
| **Archived** | Read-only history for dossiers / titles |

**Config as data:** season definitions should be rows or versioned config (DB + optional ScriptableObject mirror for client copy), not hard-coded C# season names.

**Rewards bias:** cosmetics, titles, binder flair, bounded currency — not permanent combat power spikes. Align with rarity-as-complexity philosophy.

---

## 7. Schema groundwork (ship early, use later)

Additive Supabase work that is cheap now and prevents a rewrite later.

### 7.1 `competitive_seasons`

| Column | Notes |
|--------|-------|
| `season_id` | text PK |
| `display_name` | player-facing |
| `starts_at` / `ends_at` | timestamptz |
| `state` | enum/text per §6 |
| `eligible_modes` | text[] e.g. `{ranked_pvp}` |
| `ruleset_version` | ties to combat ranked rules |
| `ui_flag` | optional override for client |

### 7.2 `chainling_season_ladder`

One row per `(chainling_id, season_id, bracket_key)`:

| Column | Notes |
|--------|-------|
| `rating` | numeric |
| `season_wins` / `season_losses` / `season_matches` | ints |
| `percentile` | 0–100 materialized |
| `rank_exact` | optional int; nullable until needed |
| `population_n` | int at last materialization |
| `updated_at` | timestamptz |

Indexes: `(season_id, bracket_key, rating DESC)`, `(chainling_id, season_id)`.

### 7.3 Career percentile cache (optional, separate)

For dossier bragging without ranked mode:

| Column / table | Notes |
|----------------|-------|
| `chainling_career_percentiles` or columns on `chainlings` | e.g. `pct_pve_damage_dealt_species` |
| Updated by **batch job**, not per menu open | Nightly or post-battle debounce queue |

### 7.4 Mode tagging on outcomes

Battle outcome persistence must eventually carry:

- `battle_mode`: `pve_wild` \| `pve_challenge` \| `pvp_casual` \| `ranked_pvp` \| …
- Idempotent event id (already required for PvE apply)

Career PvE RPC stays as-is; ranked path writes seasonal rows only when `battle_mode` ∈ season `eligible_modes`.

---

## 8. Materialization & performance

| Approach | When |
|----------|------|
| **Batch percentile refresh** | Nightly / hourly per active season bracket |
| **Debounced post-match bump** | Recompute local rating immediately; percentile lag OK (minutes–hours) |
| **Histogram buckets** | Prefer for career `Top %` at scale |
| **Forbidden** | Per-stat live table scans on party open |

Target: party / roster fetch returns cached `percentile` / `rank_exact` fields already joined or nested — **O(party size)** reads, not O(population).

---

## 9. Fairness & anti-abuse checklist

- [ ] Species brackets default for Chainling boards
- [ ] Minimum match count before rating/percentile displays
- [ ] Season soft-freeze + immutable archive
- [ ] Smurf / alt policy TBD (account identity + rate limits) before public prizes
- [ ] Entangled / premium variants: no rating MMR bonus
- [ ] Ordinal-tier / move-eligibility rules for ranked reuse existing ranked move gates
- [ ] Rebirth: career metrics follow existing PvE reset rules; seasonal rating policy = **carry vs reset mid-season** decided before first Active season (default proposal: rating stays on the Chainling identity; document if rebirth creates a new competitive entity)
- [ ] Ties: use dense rank or shared percentile buckets; document in UI copy

---

## 10. Soft-launch gates (when player inflow arrives)

Launch ladder **UI + seasons** only when:

1. Ranked (or tagged competitive) match pipeline is live and idempotent.
2. At least one **Active** season row exists with clear end date.
3. Bracket population ≥ `N_min` for featured species (or open board if that is the v1 scope).
4. Materialization job has run successfully for ≥ 24h in staging/prod.
5. Feature flag on; Party Stats still functions with flag off.
6. Support/docs ready for “what counts for ranked.”

Until then: ship schema + jobs dark; keep Party Stats as career dossier only.

---

## 11. Implementation phases

| Phase | Deliverable | Player-visible? |
|-------|-------------|-----------------|
| **G0** | This spec + Party Stats §5.6 without ranks | Career Stats UI only |
| **G1** | `competitive_seasons` + `chainling_season_ladder` (+ optional `chainling_career_percentiles`) tables; RLS on, no public policies — see `Supabase/migrations/20260721200000_competitive_ladder_seasons_groundwork.sql` | No |
| **G2** | `battle_mode` on outcome writes; ranked path no-ops if no Active season | No |
| **G3** | Rating update on eligible matches + batch percentile job | Internal dashboards |
| **G4** | Career species percentile cache for damage/victories | Optional Archive / Stats `Top %` behind flag |
| **G5** | First public season + ladder screen + rewards | Yes — soft launch |

---

## 12. Open decisions (do not block G0–G1)

| ID | Question | Default lean |
|----|----------|--------------|
| L1 | Rating algorithm (ELO vs Glicko-2 vs W/L + strength of schedule) | Glicko-2 or simple ELO for v1 |
| L2 | Team ladder vs per-Chainling ladder first | Per-Chainling species bracket first |
| L3 | Mid-season rebirth rating policy | Keep rating on same `chainling_id` |
| L4 | Prize currency vs cosmetics-only for season 1 | Cosmetics/titles first |
| L5 | Career damage percentiles on Party Stats vs Archive-only | Archive first; Party Stats optional muted suffix |

---

## 13. Acceptance (groundwork)

1. Career PvE service record remains authoritative for Party Stats summary numbers.
2. No menu code path requires live global ranking queries.
3. Season + ladder tables can exist empty without affecting PvE persistence.
4. Spec cross-links from Party Tab §5.6 and PvE service record remain valid.
5. Soft-launch checklist (§10) is reviewed before enabling `competitive_ladder_ui`.
