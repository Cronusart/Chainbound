# Chainling PC Terminal

## Purpose

PC terminals are the storage and party-management hub for owned Chainlings. The
Loominary remains a narrative character and is not a roster-management surface.

The first terminal is `Monitor_PcTerminal` in `Assets/Scenes/player_room.unity`
with terminal ID `player_room_pc`. The intro `Monitor` keep its redirect
dialogue until Mart leftover permission is granted. Later terminals reuse the
same binder and screen with distinct stable IDs.

## Unlock

`world.player.chainling_pc` is a durable profile flag. Mart leftover dialogue
(standing talk or exit intercept) writes it together with
`world.loomville_mart.open`. Until that flag exists:

- The player-room storage interactable stays inactive.
- The intro monitor interactable stays active.
- `GameplayMenuWorldFlagGate` keeps the player menu suppressed. Intro complete
  is not enough; Mart has to send the house tech.

Existing profiles that already opened the shop still unlock through the legacy
flag `world.loomville_mart.open`.

## Prefab

Layout lives in `Assets/Prefabs/UI/PlayerMenu/ChainlingPcTerminal.prefab`.
Author with **Tools → Chainbound → UI → Author Player Room Chainling PC**.
The screen canvas is Screen Space Overlay, scaled to 1920×1080. Runtime cards
instantiate `ChainlingCardView` into the authored storage root.

## V1 scope

- Load the full owned roster through `IPlayerRosterProvider`.
- Show stored Chainlings separately from Lead, Partner, and Bench.
- Select a stored Chainling, then assign or swap it into a party slot.
- Persist the ordered party through `IMenuPartyLineupService` / `profile_set_party`.
- Keep at least one Chainling in the party.
- Close through touch Close or gamepad/PSG1 B.
- Hold an `InputLockChannel.All` token while open.
- Respect `Screen.safeArea` and provide explicit selectable focus.

The pause-menu Chainlings tab remains inspect-only and refreshes profile data when
opened. Empty slots direct the player to a PC.

## Capture handoff

After a successful capture:

- Prompt for a valid 2–12 character nickname.
- Cancel or skip uses the species display name.
- Tell the player whether the Chainling joined the party or went to storage.
- Direct party management to a PC.

Practice captures may show the naming experience but must state that no dossier was
saved and must not issue profile mutations.

## Out of scope

- Rename-after-capture
- Move, cosmetic, Archive, or mutation management
- Additional world PC placements
- Enabling authoritative production capture
