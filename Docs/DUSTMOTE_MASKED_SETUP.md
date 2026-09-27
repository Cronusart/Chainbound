# DustMote_Masked Shader Setup

Dust particles that respect the window beam mask. Shader: `Assets/Sprites/Shaders/DustMoteMasked.shader`. Reads global shader properties set by `BeamMaskProvider`.

## Quick Create

**GameObject → Signal Jam → Create Dust Mote Particle System** — Creates a preconfigured particle system with `DustMoteMaskApplier`. Assign the material if not auto-found, then parent it to your beam and wire `dustParticles` in `WindowBeamController`.

## Multiple Beams per Window

Use one `WindowBeamController` for all beams at a window. Add child quads (top, camera-facing, etc.) and assign their MeshRenderers to `beamRenderers`. They inherit the controller's scale; rotate/position each child as needed.

## Time of Day (Optional)

Enable **Use Time Of Day** to drive beam visibility from `TimeOfDayManager`. Configure `visibleStartTime` (default 0.25 ≈ dawn) and `visibleEndTime` (default 0.75 ≈ dusk) for the visible window. Use `fadeDurationSeconds` for smooth fade in/out. Leave disabled for beams that persist (e.g. outdoor nighttime ambient).

## Dependencies

| Component | Role |
|-----------|------|
| **BeamMaskProvider** | Pushes `_BeamOrigin`, `_BeamForward`, `_BeamRight`, `_BeamUp`, `_BeamHalfWidth`, `_BeamHalfHeight`, `_BeamLength`, and dust mask params to global shader properties every frame |
| **WindowBeamController** | Drives beam transform, calls `BeamMaskProvider.UpdateGlobals()`, configures dust particle shape |
| **DustMoteMaskApplier** | Attaches to ParticleSystem; assigns the dust material to the ParticleSystemRenderer |

## Wiring

1. **WindowBeamController**
   - Assign `beamRenderers` (array of MeshRenderers — e.g. top beam + camera-facing beam). Or use `beamRenderer` for a single beam.
   - Assign `windowTransform` (window position/orientation)
   - Assign `maskProvider` → BeamMaskProvider component
   - Optionally assign `dustParticles` and `maskProvider`
   - **Beams Visible**: Manual toggle when `Use Time Of Day` is off. Ignored when `Use Time Of Day` is on.
   - **Time of Day**: Enable `useTimeOfDay` and assign `timeOfDayManager` (or leave null to use `TimeOfDayManager.Instance`). Set `visibleStartTime`/`visibleEndTime` and `fadeDurationSeconds` for smooth day-only beams.
   - **Lock Window Position**: When the controller is on the window GameObject (with BoxCollider), enable `lockWindowPosition` to prevent physics/gravity from moving it.

2. **BeamMaskProvider**
   - Assign `beamController` → WindowBeamController (so dust params sync with beam params)
   - Place on same GameObject as WindowBeamController, or ensure `WindowBeamController.maskProvider` points to it

3. **DustMoteMaskApplier**
   - Add to the ParticleSystem GameObject
   - Assign `dustMaterial` → Material using `SignalJam/Sprites/DustMote_Masked` shader

## Material Properties (Inspector)

- **Particle Color** (HDR) — tint of the dust
- **Base Alpha** (0–1) — overall opacity multiplier

## Particle System

- Enable **Color Over Lifetime** and use the alpha curve for fade-in/fade-out. The shader multiplies vertex color alpha by the beam mask.
- **Shape**: Box (driven by `WindowBeamController.UpdateParticleShape()` when `dustParticles` is assigned)
- Use a simple quad or soft particle texture

## URP

Enable **Depth Texture** in your URP Asset for depth fade to work.
