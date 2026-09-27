# Player Menu Test Harnesses

Use this pattern when a player-menu tab needs realistic data before the live backend, wallet, or progression flow can safely produce it.

The goal is to test menu behavior with real UI components and real service contracts while keeping all mock state disposable.

## Recommendation

Build small harnesses as each menu tab reaches practical testing. Do not wait for every tab foundation to exist, but also do not create a broad mock framework ahead of known tab needs.

Good harnesses are useful when they:

- Exercise the same provider/mutation interfaces the live tab uses.
- Store generated state only in memory.
- Use fake IDs that are obvious in logs, such as `debug-*`.
- Are wired through an explicit sandbox prefab or editor tool.
- Can be removed without changing live gameplay code.

Avoid harnesses that:

- Write PlayerPrefs, save files, Supabase rows, wallet state, or ScriptableObject asset data.
- Add `if debug` branches inside production menu logic.
- Share the production prefab as their default output.
- Invent a second data shape that the real tab will not use.

## Current Example

The Chainlings tab uses:

- `ChainlingsMenuTestHarness`
- `IPlayerRosterProvider`
- `IPlayerPartyProvider`
- `IMenuPartyLineupService`
- `IMoveInventoryProvider`
- `IMenuMoveEquipService`
- `ICosmeticInventoryService`
- `IMenuVisualIdentityService`
- `IChainlingRebirthService`

The sandbox prefab is created from:

`Tools > SignalJam > UI > Create Player Menu Scaffold (Chainlings Sandbox)`

This writes:

`Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab`

It does not overwrite:

`Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab`

## Chainlings Prefab-First Layout

Author Chainlings tab layout in separate prefabs so `PlayerMenuRoot.prefab` stays a frozen menu shell: hub, tabs, stack host, and close button.

### Assets

- `Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab` - four-panel active-party view: orientation, preview, focused context, and party slots.
- `Assets/Prefabs/UI/PlayerMenu/ChainlingPartySlotView.prefab` - fixed Lead / Partner / Bench slot card template.
- `Assets/Prefabs/UI/PlayerMenu/ChainlingDetailView.prefab` - detail header, Overview/Moves/Style/Progress segments, panel hosts.
- `Assets/Prefabs/UI/PlayerMenu/ChainlingCardView.prefab` - reused by the PC storage/equip UI.
- `Assets/Data/UI/ChainlingsMenuViews.asset` - references the party, slot, detail, and retained card prefabs.

### Editor Workflow

1. Run `Tools/Chainbound/UI/Player Menu Dev/Create Chainlings Menu View Prefabs` once to generate baseline prefabs and wire `GameServices` bootstrap.
2. Edit layout in `ChainlingsPartyView.prefab` and `ChainlingDetailView.prefab`, not in `PlayerMenuRoot`.
3. Use `Tools/Chainbound/UI/Player Menu Dev/Use Harness Chainlings Data` for in-memory roster preview data.
4. Play with `GameServices` in the scene; bootstrap instantiates the view prefabs under the screen stack on startup.

### Party View Checklist

- Hub to Chainlings opens exactly three fixed slots: Lead, Partner, Bench.
- D-pad default focus lands on Lead, or the first filled slot.
- Left/right moves across party slots.
- Up from any filled slot reaches Inspect; A/click opens the existing detail stack.
- B returns from detail to party, then from party to hub.
- Empty slots hide Inspect and show `Assign at a PC`.
- Portrait changes debounce briefly and use a small unscaled-time crossfade/pop.

### Safety

- Do not run `Tools/Chainbound/UI/Create Player Menu Scaffold` on a hand-authored `PlayerMenuRoot`; it overwrites the shell prefab.
- Legacy embedded `Tab_Chainlings` content in `PlayerMenuRoot` is hidden at runtime when view prefabs are assigned; layout work belongs in the view prefabs.

### PC Terminal Split

The Chainlings tab is party-only. Full storage browsing, card grids, and equip-to-slot mutations belong at reusable PC terminals. The first usable terminal is the Monitor in `player_room`; it reuses `ChainlingCardView` and `IMenuPartyLineupService`.

## Harness Shape

For a new menu tab, prefer this structure:

1. Define the tab's read providers and write services as interfaces.
2. Keep the live Supabase, wallet, or inventory path in thin service adapters.
3. Add one tab-specific harness MonoBehaviour that implements the same interfaces.
4. Generate fake entries with stable, readable debug IDs.
5. Clone DTOs before returning them if the UI can edit them.
6. Keep all mutation results in the harness's private memory.
7. Add an explicit scaffold/editor menu item that wires the harness into a separate sandbox prefab.
8. Hide sandbox-only controls unless the assigned service implements the harness marker interface.

## Mock Asset Guidance

Prefer generated runtime DTOs for volatile player state:

- roster entries
- wallet inventory
- party/loadout order
- temporary unlocks
- leaderboard rows
- profile stats

Use ScriptableObject assets as read-only source catalogs:

- species database
- move database
- visual identity database
- rarity palettes
- product catalogs

Only create persistent mock assets when the asset itself is the thing being authored or reviewed. Put those in an obvious dev/test location and keep them out of live default wiring.

## Future Tab Checklist

Before adding a new harness, answer:

- What live interfaces will this tab eventually call?
- Which data can be generated in memory?
- Which catalogs should be read from existing assets?
- What mutations should be simulated locally?
- How will the sandbox prefab/tool stay separate from production?
- What fake IDs should appear in logs and UI?
- What state must never be written durably?

## Verification

For each harness:

- Run the menu in Play Mode using the sandbox prefab.
- Confirm refresh/regenerate does not call Supabase or wallet services.
- Confirm mutations update only the current harness state.
- Confirm closing/reopening the scene starts clean unless the harness intentionally regenerates from its seed.
- Run `dotnet build "Signal Jam 3D.sln" -v minimal`.

## Leaving the harness behind

When a tab is ready for live profile testing, follow:

[`PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md`](PRODUCTION_CUTOVER_AND_HARNESS_DISENGAGE.md)

Keep sandbox prefabs; do not leave harness components on production `PlayerMenuRoot`.
