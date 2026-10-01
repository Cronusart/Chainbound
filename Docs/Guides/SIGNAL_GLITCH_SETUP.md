# Signal Glitch Effect — Prefab Setup

The Universal Signal Glitch Effect provides reusable glitch visuals for faint (GlitchOut), spawn (GlitchIn), and looped overworld effects. This guide documents the prefab setup for combat slots.

## Prerequisites

1. **Shader:** `Assets/Sprites/Shaders/SignalGlitch.shader` (SignalJam/Sprites/SignalGlitch)
2. **Material:** Create a material using the SignalGlitch shader (e.g. `Assets/Materials/SignalGlitch.mat`)
3. **Component:** `SignalGlitchEffect.cs` (Assets/Scripts/Combat/Runtime/Presentation)

## Combat Slot Setup

For each combat slot under `Combat2Battlefield.prefab`:

### Hierarchy

Under each slot's `VisualRoot` → `CombatChainlingVisual` child, add a **sibling** child named `GlitchOverlay`:

```
VisualRoot
├── CombatChainlingVisual   (has MeshFilter + MeshRenderer for sprite)
└── GlitchOverlay           (NEW: MeshFilter + MeshRenderer + SignalGlitchEffect)
```

### GlitchOverlay Configuration

1. **Create child:** Right-click `VisualRoot` → Create Empty → name it `GlitchOverlay`
2. **MeshFilter:** Add `MeshFilter` component. Leave `Mesh` unassigned — `SignalGlitchEffect` syncs it from the source at runtime.
3. **MeshRenderer:**
   - Assign **Material** = your SignalGlitch material
   - Set **Sorting Layer / Order** to match or slightly above `CombatChainlingVisual` so it renders on top
   - **Start with `MeshRenderer.enabled = false`** — the effect enables it during animations
4. **SignalGlitchEffect:**
   - **Glitch Material:** Assign the same SignalGlitch material (or leave null to use the MeshRenderer's)
   - **Source Mesh Renderer:** Assign the `CombatChainlingVisual`'s `MeshRenderer` (the sprite visual)
   - Other fields use defaults; adjust curves/durations as needed

### Wiring

- `SignalGlitchEffect.sourceMeshRenderer` → `CombatChainlingVisual`'s `MeshRenderer`
- `Combat2ChainlingView.glitchEffect` → the `GlitchOverlay`'s `SignalGlitchEffect` (auto-found if null)

## Reuse for Other Domains

- **Starter minting:** Add a `GlitchOverlay` child to the starter dog's visual. Call `PlayGlitchIn()` when the dog materializes.
- **NPC interactions:** Add a `GlitchOverlay` child to `MeshRendererNPCVisual`. Use `PlayGlitchLoop()` for holographic NPCs, or `PlayGlitchIn`/`PlayGlitchOut` for appear/disappear sequences.
