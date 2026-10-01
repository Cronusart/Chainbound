# Production Cutover & Harness Disengage

Use this checklist when moving a feature from **sandbox / harness** play into **production-shaped** play (real Supabase profile, wallet session, live adapters).

Companion: [`PLAYER_MENU_TEST_HARNESSES.md`](PLAYER_MENU_TEST_HARNESSES.md) (how to build harnesses).  
This guide is the reverse: **how to turn them off** and what must be wired instead.

---

## 0. Mental model

| Mode | Prefab / scene | Data authority | Mutations |
|------|----------------|----------------|-----------|
| **Sandbox** | `PlayerMenuRoot_ChainlingsSandbox.prefab` (+ harness component) | In-memory harness / stub | Local only; fake `debug-*` IDs |
| **Production** | `PlayerMenuRoot.prefab` in real overworld / menu flow | Supabase (+ wallet for Core) | Edge RPC / signed txs |

**Rule:** Sandbox and production keep **separate prefabs**. Never leave a harness on the production root “just in case.”

Screens resolve services in this order (Inventory example):

1. Explicit serialized service reference  
2. Parent / scene `ChainlingsMenuTestHarness` → stub path  
3. `SupabaseConfigSO` → production adapter  
4. Fail with a clear status (do **not** silently fall back to stub)

---

## 1. Global disengage checklist (every feature)

Before calling a tab or flow “production-ready for testing”:

### Scene / prefab

- [ ] Production play uses `PlayerMenuRoot.prefab` (or the live menu host), **not** `*_Sandbox.prefab`
- [ ] No `ChainlingsMenuTestHarness` (or other tab harness) in the production scene hierarchy
- [ ] No serialized references on screens pointing at harness / stub MonoBehaviours
- [ ] Sandbox-only UI (regenerate roster, randomize party, etc.) stays hidden when harness is absent
- [ ] `FindObjectOfType<*Harness>` cannot accidentally pick a leftover DDOL / bootstrap object

### Config assets

- [ ] `SupabaseConfigSO` assigned on screens / services that call Edge Functions
- [ ] `SolanaConfigSO.useStubServices = false` for wallet/chain paths under test
- [ ] Feature ScriptableObjects present where required (`FavorConfigSO`, `FieldItemConfigSO`, battle reward config, etc.) — runtime defaults are OK for early smoke, assets preferred for tuning

### Session

- [ ] `GameSessionContext` has a real `PlayerProfileId` (and wallet when Core / ownership matters)
- [ ] Guest or wallet link path that matches how Edge `resolveProfileFromBody` works
- [ ] Connectivity state shows Ready (not Offline) when bag / roster loads

### Backend

- [ ] Required migrations applied on the target project
- [ ] Required Edge Functions deployed and callable with the project anon key Unity uses
- [ ] Secrets present for any DAS / Core verify paths you are testing
- [ ] Migration history drift acknowledged (some SQL may have been applied via Editor / `db query` — verify with SQL, not only `migration list`)

### Verification

- [ ] Play Mode path under test never logs harness regenerate / stub consume unless intentionally in sandbox
- [ ] One happy-path mutation hits Supabase (row or RPC result changes)
- [ ] Closing and reopening the menu still shows server-backed state (not only client cache)

---

## 2. Player Menu — per-tab matrix

| Tab / concern | Harness / stub to disengage | Production wiring |
|---------------|-----------------------------|-------------------|
| **Chainlings party / roster** | `ChainlingsMenuTestHarness` as `IPlayerRosterProvider` / party / moves / cosmetics | `SupabasePlayerRosterProvider`, `SupabasePlayerLineupPartyProvider`, `SupabaseMenuMoveEquipService`, `SupabasePartyLineupMutation`, `CoreCosmeticInventoryService` |
| **Inventory bag read** | Harness `IInventoryService` / `StubInventoryService` | `CoreBagItemInventoryService` → `profile_get_item_inventory` |
| **Inventory Use (field / Favor)** | `StubPlayerItemUseService` | `SupabasePlayerItemUseService` → `profile_use_field_item` |
| **Inventory Use (Core / Bindery)** | Stub prepare/finalize offline | `CoreAssetConsumeService` + Bindery rites (**Pass 2**) |
| **Favor session** | Harness-local `FavorService` only | Same `FavorService` component, but ampule set by production Use; sinks read `FavorRuntime` |
| **Solana bridge** | `SolanaConfigSO.useStubServices` | Real wallet + RPC adapters |

Inventory auto-wires today:

- Harness in scene → stub Use + harness bag  
- Else + `supabaseConfig` on `InventoryScreen` → `CoreBagItemInventoryService` + `SupabasePlayerItemUseService`

Confirm `InventoryView.prefab` / tab host has `supabaseConfig` assigned (production prefab already expects this).

---

## 3. Inventory-specific cutover

### Must be true for Pass 1 production smoke

- [ ] Open Inventory from **production** `PlayerMenuRoot` with a linked profile
- [ ] Bag loads stacks from `chainbound_profile_item_stacks` (not harness seed)
- [ ] Use **Chainling Update** on a damaged party member → stack decrements in DB → `chainlings.current_hp` updates → party HP bar updates after reopen / cache patch
- [ ] Use on full HP → error `already_full_hp`, stack unchanged
- [ ] Use **Chainling Reboot** only when fainted
- [ ] Use **Favor Boost** → `FavorService` score/duration set; no target required
- [ ] Capture / Bindery / Archive Use returns `*_not_supported_yet` (expected until Pass 2)

### Known gaps (do not block Pass 1 smoke)

| Gap | Why it matters for testing |
|-----|----------------------------|
| No **shop** faucet | Testers who skip Link 1 still need SQL or a world pickup to own patches / Favor |
| Rare battle drop rolls onto `BattleRewardSummary` but may not persist into stacks | Side-drop not required for Use smoke |
| Party tab may need reopen/refetch to show HP if cache patch missed a provider | Note during QA |
| Core collectible Use blocked until Pass 2 | Expected |
| `supabase migration list` history may omit manually applied SQL | Verify with catalog / RPC queries |

World pickups are a live bag grant: `WorldPickupGrant` → `profile_grant_world_pickup` (spawn catalog, not client `itemId`). Link 1 backpack (`world.pickup.link1.backpack_01`) grants `chainling_patch` × 1. Apply `Supabase/migrations/20260901010000_world_pickup_grant.sql` and deploy `profile_grant_world_pickup` before overworld grant smoke. `already_claimed` is success for the chest flag; the obtain panel shows only on a new grant.

### Dev grant (temporary)

Until shops / drops land, grant stacks for a profile (or collect the Link 1 backpack):

```sql
INSERT INTO public.chainbound_profile_item_stacks (player_profile_id, item_id, quantity)
VALUES
  ('<profile-uuid>', 'chainling_update', 5),
  ('<profile-uuid>', 'chainling_reboot', 2),
  ('<profile-uuid>', 'favor_boost_s', 2),
  ('<profile-uuid>', 'party_patch', 2)
ON CONFLICT (player_profile_id, item_id) DO UPDATE
SET quantity = EXCLUDED.quantity, updated_at = now();
```

Prefer a future `profile_dev_grant_items` (gated) over leaving ad-hoc SQL as the only path.

---

## 4. Solana / wallet cutover (when bag Core or capture is in scope)

- [ ] `useStubServices = false`
- [ ] WalletController present and connected
- [ ] Cluster matches Edge secrets (`devnet` vs mainnet)
- [ ] Core ownership cache populated for any Core bag item under test
- [ ] Never treat client ownership lists as authority

See [`WALLET_CONNECTION_SETUP.md`](WALLET_CONNECTION_SETUP.md). The older main-menu wallet notes file is not in this public snapshot.

---

## 5. Combat / overworld cutover (Favor sinks + HP)

Favor and HP are only meaningful if battle and menu share the same profile:

- [ ] PvE battle end persists HP (`profile_apply_pve_battle_outcome`)
- [ ] `CombatPostBattleRouter` ticks `FavorService.NotifyBattleCompleted`
- [ ] Encounter / capture / wild IV paths can see `FavorRuntime` in the play scene (FavorService alive)

Sandbox combat harnesses (`Combat2TestHarness`, etc.) are separate from Player Menu harnesses — disengage each when testing that loop.

---

## 6. Definition of done: “ready for production-mode testing”

A feature is ready when:

1. Sandbox still works for UI iteration (optional, separate prefab).  
2. Production path works with harnesses **absent**.  
3. One written smoke script (below) passes on the linked Supabase project.  
4. Failures return actionable errors (missing config, empty bag, RPC error) instead of silent stubs.

### Inventory Pass 1 smoke script

1. Grant stacks (SQL above) for the test profile.  
2. Damage a party Chainling via a short PvE battle (or set `current_hp` in SQL); leave at least one party member at full HP.  
3. Boot production menu → Inventory → Field → confirm patch → Actions → **Use**.  
4. Use popup opens as a **vertical row list** of all active party Chainlings (type icon, name, stars, cycle, HP + XP) plus dialogue “Choose a Chainling to use … on.”  
5. Up/Down to a full-HP Chainling → A/Submit → Invalid SFX + “already at full health” in dialogue; **popup stays open**.  
6. Up/Down to the damaged Chainling → A/Submit → HP bar tween + “recovered N HP” dialogue → A/Submit dismisses → ListEditing on same item; qty −1.  
7. Confirm DB stack qty −1 and HP increased.  
8. Use Favor Boost → confirm session Favor score; run an encounter/capture smoke if Favor sinks are in scope.  
9. Confirm Capture / Press Charge Use is rejected with a clear Pass 2 message.

---

## 7. What not to do

- Do not add `if (Debug.isDebugBuild) use stub` inside production screens.  
- Do not put harness components on `PlayerMenuRoot.prefab`.  
- Do not grant real Core burn authority from Unity-only stubs.  
- Do not treat “compiles + sandbox works” as production cutover complete.

---

## Changelog

| Date | Change |
|------|--------|
| 2026-07-25 | Initial cutover / harness-disengage checklist; Inventory Pass 1 smoke path. |
