# Encounter Events (Supabase Live-Ops)

Server-wide encounter modifiers for limited-time events, favor boosts, trait/move/variant chance levers, injected species, and post-battle reward multipliers.

## Table: `encounter_events`

Applied by `Supabase/migrations/20260801200000_encounter_events.sql`.

```sql
CREATE TABLE IF NOT EXISTS public.encounter_events (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    starts_at           TIMESTAMPTZ NOT NULL,
    ends_at             TIMESTAMPTZ NOT NULL,
    enabled             BOOLEAN NOT NULL DEFAULT true,
    payload             JSONB NOT NULL DEFAULT '{}'::jsonb,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT encounter_events_name_not_blank CHECK (length(btrim(name)) > 0),
    CONSTRAINT encounter_events_window_ordered CHECK (ends_at > starts_at),
    CONSTRAINT encounter_events_payload_is_object CHECK (jsonb_typeof(payload) = 'object')
);

-- Partial index: only live-eligible rows are ever queried.
CREATE INDEX IF NOT EXISTS idx_encounter_events_active_window
    ON public.encounter_events (starts_at, ends_at)
    WHERE enabled;
```

`updated_at` is maintained by `trg_encounter_events_touch_updated_at` via the shared
`public.chainbound_touch_updated_at()` function.

### Access control

RLS is enabled with **no policies**, so only `service_role` (which bypasses RLS) can read or
write. `anon` and `authenticated` are explicitly revoked — live-ops levers must never be
client-readable or client-writable. Author rows via the Dashboard, SQL, or a service-role tool.

The `payload_is_object` constraint matters: the Unity client deserializes `payload` into
`EncounterModifierDto`, and an array/scalar/null would silently yield an empty modifier.

### Payload shape (`EncounterModifier`)

```json
{
  "modifier_id": "event_night_favor_boost",
  "display_name": "Night Favor Boost",
  "area_ids": [],
  "zone_ids": [],
  "species_ids": [],
  "segments": 1,
  "filter_by_difficulty": false,
  "difficulty": 0,
  "start_unix_utc": 0,
  "end_unix_utc": 0,
  "ops": [
    { "op_type": 10, "target_id": "", "mode": 1, "value": 50, "inject_weight": 0, "inject_capture_rate": 45, "inject_base_stars": 0 }
  ]
}
```

`segments` is a `TimeSegmentMask` bitfield: Night=1, Dawn=2, Day=4, Dusk=8, All=15, None/any=0.

### Op types (`EncounterModifierOpType`)

| Value | Name | Notes |
|------:|------|-------|
| 0 | EncounterChance | Multiply/Add/Set zone encounter chance |
| 1 | SpeciesWeight | `target_id` = speciesId (empty = all in scope) |
| 2 | MutationChance | Scalar on non-none mutation rarity weights (legacy boolean chance when no mutation table) |
| 3 | EntangledChance | Single entangled chance; type is rolled uniformly client-side |
| 4 | IvBumpChance | |
| 5 | IvMin | |
| 6 | IvMax | |
| 7 | MoveWeight | `target_id` = moveId |
| 8 | VariantWeight | `target_id` = visualVariantId |
| 9 | CaptureRate | |
| 10 | FavorBonus | Adds to effective favor for the roll |
| 11 | InjectSpecies | `target_id` = speciesId, uses inject_* fields |
| 12 | InjectVariant | `target_id` = visualVariantId |
| 13 | RewardRamMultiplier | Post-battle |
| 14 | RewardXpMultiplier | Post-battle |
| 15 | RewardRareDropChance | Additive chance |
| 16 | RewardBonusItem | `target_id` = itemId |

Modes: `0=Multiply`, `1=Add`, `2=Set`.

## Edge function: `get_encounter_events`

Read-only. Called by `SupabaseEncounterEventsProvider` via `SupabaseClient.PostJsonAsync`.

**Request**
```json
{ "now_unix": 1735689600 }
```

**Response**
```json
{
  "events": [
    {
      "id": "uuid",
      "name": "Night Favor Boost",
      "starts_at_unix": 1735689600,
      "ends_at_unix": 1736294400,
      "enabled": true,
      "payload": { "...EncounterModifier..." }
    }
  ],
  "server_now_unix": 1735689600
}
```

`now_unix` is optional; the function falls back to server time when it is absent or <= 0, and
echoes the instant it evaluated as `server_now_unix`.

On failure the function returns HTTP 500 with `{ "error": "...", "events": [] }`. The client
keeps its last cache (or stays empty) and local modifiers continue to apply.

### Implementation

Deployed from `Supabase/functions/get_encounter_events/index.ts`, using the shared
`_shared/account_helpers.ts` helpers (`requirePost` for method/CORS, `supabaseAdmin` for the
service-role client, `jsonResponse`).

The handler coerces every payload field to the exact DTO shape before returning it. This is
required, not cosmetic: Unity reads the response with `JsonUtility`, which discards the **entire**
object on a single type mismatch, so a designer writing `"segments": "1"` instead of `1` would
otherwise blank out the whole event. Missing fields become typed defaults and non-string array
entries are dropped.

Ops with a negative or non-numeric `op_type` are skipped and logged. Unknown-but-valid `op_type`
values pass through untouched, so adding a new `EncounterModifierOpType` never requires
redeploying this function — unrecognized ops are simply inert on the client.

### Deploying

```bash
supabase functions deploy get_encounter_events --project-ref <ref>
```

Older CLI versions bundle via Docker; if Docker isn't running, use a CLI >= 2.98 (for example
`npx supabase@latest functions deploy ...`), which bundles natively.

## Unity wiring

Already wired on the `SolanaServices` child of `Assets/Prefabs/GameServices/GameServices.prefab`,
beside `SolanaServiceBridge` and `ZoneEncounterProvider`. Re-runnable via the menu command
**Tools / Chainbound / Combat / Wire Encounter Services in GameServices Prefab**.

- `EncounterModifierService` — aggregates modifiers; `additionalSources` lists the events provider.
  `localModifiers` is left empty by default so no local event asset silently alters encounter odds;
  add `LocalEncounterModifierSO` assets deliberately.
- `SupabaseEncounterEventsProvider` — `supabaseConfig` assigned, `fetchOnStart` enabled (a first
  fetch happens on `Start`, not only after `refreshIntervalSeconds`), auto-registers with the
  modifier service.
- `FavorService` — required for favor-scoped ops; reads `Assets/Data/Combat/FavorConfig.asset`.

Offline / fetch failure: provider keeps its last cache (or empty) and local modifiers still apply.

## Authoring an event

Run in the Dashboard SQL editor (the table is service-role only, so the game client cannot write it):

```sql
INSERT INTO public.encounter_events (name, starts_at, ends_at, enabled, payload)
VALUES (
    'Night Favor Boost',
    now(),
    now() + interval '7 days',
    true,
    '{
      "modifier_id": "event_night_favor_boost",
      "display_name": "Night Favor Boost",
      "segments": 1,
      "ops": [{ "op_type": 10, "mode": 1, "value": 50 }]
    }'::jsonb
);
```

To end an event early, set `enabled = false` rather than deleting the row — the partial index drops
it from the live set immediately and you keep the history. Clients pick the change up on their next
refresh (`refreshIntervalSeconds`, default 300s).

## Example event rows

**Server-wide favor boost (any area/zone/time):**
```json
{ "ops": [{ "op_type": 10, "mode": 1, "value": 100 }] }
```

**Night-only rare variant weight for `gilarime` in area `starter_grove`:**
```json
{
  "area_ids": ["starter_grove"],
  "species_ids": ["gilarime"],
  "segments": 1,
  "ops": [{ "op_type": 8, "target_id": "entangled", "mode": 0, "value": 3.0 }]
}
```

**Inject limited-time species + XP/RAM reward boost:**
```json
{
  "ops": [
    { "op_type": 11, "target_id": "kitsauto", "mode": 1, "value": 0, "inject_weight": 5, "inject_capture_rate": 20, "inject_base_stars": 2 },
    { "op_type": 14, "mode": 0, "value": 1.5 },
    { "op_type": 13, "mode": 0, "value": 2.0 }
  ]
}
```
