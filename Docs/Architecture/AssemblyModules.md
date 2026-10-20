# Signal Jam 3D — Assembly Modules (asmdef)

This document is the source of truth for **where C# scripts compile** and **which assembly references are allowed**. Update it whenever an `.asmdef` or `.asmref` is added, moved, or changed.

Last reviewed: 2026-09-03

---

## Why this matters

Unity splits scripts into **assemblies** (modules). Each assembly can only `using` types from:

1. Itself
2. Assemblies listed in its `.asmdef` `references`
3. Unity engine / package assemblies it references

If a script lands in the wrong folder, or a reference crosses a forbidden boundary, you get errors like:

- `CS0246` — type or namespace not found
- `CS0234` — namespace does not exist in the assembly
- Circular dependency errors when adding `.asmdef` references

Most game code outside named modules still lives in **`Assembly-CSharp`**, which auto-references all runtime modules marked `"autoReferenced": true`.

---

## Module map (runtime)

| Assembly | Definition | Folder scope | autoReferenced |
|---|---|---|---|
| **Assembly-CSharp** | *(default — no asmdef)* | Most of `Assets/Scripts/**` not listed below | n/a |
| **Chainbound.Combat.Core** | `Combat/Core/Chainbound.Combat.Core.asmdef` | Deterministic, Unity-free combat/capture domain and service contracts | yes |
| **Chainbound.World.TimeOfDay** | `World/Environment/TimeOfDay/Chainbound.World.TimeOfDay.asmdef` | That folder + children | yes |
| **SignalJam.UI.VisualFX** | `UI/UI_VisualFX/SweepParticles/SignalJam.UI.VisualFX.asmdef` | **`SweepParticles/` only** | yes |
| **SignalJam.UI.MobileControls** | `UI/MobileControls/SignalJam.UI.MobileControls.asmdef` | `UI/MobileControls/**` (includes `InputLockService`, `InputLockHandle`) | yes |
| **Chainbound.Combat.Runtime** | `Combat/Runtime/Chainbound.Combat.Runtime.asmdef` | `Combat/Runtime/**` **plus** asmref folders (below) | yes |
| **SignalJam.UI.MenuNavigation** | `UI/MenuNavigation/SignalJam.UI.MenuNavigation.asmdef` | `UI/MenuNavigation/**` except `Editor/` | yes |
| **SignalJam.VisualFx** | `Core/VisualFx/SignalJam.VisualFx.asmdef` | `Core/VisualFx/**` | yes |

References: `Chainbound.Combat.Runtime`, `Chainbound.World.TimeOfDay`, `SignalJam.UI.MobileControls`, `SignalJam.VisualFx`, DOTween, InputSystem, TMP.

`Chainbound.Combat.Core` has `noEngineReferences: true` and no assembly dependencies. `Chainbound.Combat.Runtime` depends on Core; Core must never depend on Unity, ScriptableObjects, networking, presentation, or platform SDKs. The .NET battle service links this same Core source.

### asmref extensions (join parent assembly)

| asmref file | Folder | Joins assembly |
|---|---|---|
| `Chainlings/Chainbound.Combat.Runtime.asmref` | `Assets/Scripts/Chainlings/**` | **Chainbound.Combat.Runtime** |
| `Solana/Chainbound.Combat.Runtime.asmref` | `Assets/Scripts/Solana/**` | **Chainbound.Combat.Runtime** |

**Important:** `Assets/Scripts/Chainlings/**` and `Assets/Scripts/Solana/**` are **not** in `Assembly-CSharp`. They compile into **Combat.Runtime** because of `.asmref`.

### Split-folder gotcha: UI_VisualFX

| Path | Assembly |
|---|---|
| `UI/UI_VisualFX/SweepParticles/*.cs` | **SignalJam.UI.VisualFX** |
| `UI/UI_VisualFX/*.cs` (root, not in SweepParticles) | **Assembly-CSharp** |

Do not assume all files under `UI_VisualFX/` share one assembly.

---

## Module map (editor / tests)

| Assembly | Definition | Folder scope | autoReferenced |
|---|---|---|---|
| **Assembly-CSharp-Editor** | *(default — no asmdef)* | `Assets/Scripts/Editor/**` and other `Editor/` folders without their own asmdef | n/a |
| **Chainbound.Combat.Editor** | `Combat/Editor/Chainbound.Combat.Editor.asmdef` | `Combat/Editor/**` | yes |
| **Chainbound.Combat.Tests** | `Combat/Tests/Chainbound.Combat.Tests.asmdef` | `Combat/Tests/**` | **no** (test-only, Editor platform, listed under Edit Mode). References include `Unity.TextMeshPro` so card-view tests can call stamp-font APIs. |
| **SignalJam.UI.MenuNavigation.Editor** | `UI/MenuNavigation/Editor/SignalJam.UI.MenuNavigation.Editor.asmdef` | `UI/MenuNavigation/Editor/**` | **no** |

`Chainbound.Combat.Editor` references both Combat.Runtime and Combat.Core so manifest exporters can serialize the same deterministic capture DTO values consumed by the service.

References: `SignalJam.UI.MenuNavigation`, `Chainbound.Combat.Runtime`, `SignalJam.VisualFx`, `Unity.TextMeshPro`.

Editor tools under `Assets/Scripts/Editor/` (global) compile to **Assembly-CSharp-Editor** and may use any **autoReferenced** runtime module without extra setup.

---

## External / package assemblies (referenced by game code)

| Assembly | Referenced by | Notes |
|---|---|---|
| `Unity.InputSystem` | Combat.Runtime, MobileControls, MenuNavigation | New Input System |
| `Unity.TextMeshPro` | Combat.Runtime, MenuNavigation, MobileControls | TMP. MobileControls uses it for the auto-sprint HUD label. |
| `UnityEngine.UI` | MobileControls | uGUI |
| `Unity.RenderPipelines.Universal.Runtime` | Combat.Runtime, TimeOfDay | URP |
| `Unity.RenderPipelines.Core.Runtime` | Combat.Runtime, TimeOfDay | URP core (`Volume` / `VolumeProfile` for combat Post FX) |
| `DG.Tweening.Modules` | Combat.Runtime, MenuNavigation | DOTween module under `Assets/Resources/Plugins/Demigiant/` |
| `PSG1` | MobileControls | Play Solana handheld SDK package |

---

## Dependency graph (allowed references)

```mermaid
flowchart BT
  subgraph packages [Packages]
    PSG1[PSG1]
    DOTween[DG.Tweening.Modules]
  end

  subgraph world [World]
    TimeOfDay[Chainbound.World.TimeOfDay]
  end

  subgraph ui [UI]
    VisualFX[SignalJam.UI.VisualFX]
    MobileControls[SignalJam.UI.MobileControls]
    MenuNav[SignalJam.UI.MenuNavigation]
  end

  subgraph combat [Combat]
    CombatCore[Chainbound.Combat.Core]
    CombatRT[Chainbound.Combat.Runtime]
  end

  subgraph default [Default bucket]
    AC[Assembly-CSharp]
  end

  TimeOfDay --> URP[URP packages]
  MobileControls --> PSG1
  MobileControls --> InputSystem[Unity.InputSystem]
  MobileControls --> TMP[Unity.TextMeshPro]

  CombatRT --> CombatCore
  CombatRT --> TimeOfDay
  CombatRT --> MobileControls
  CombatRT --> VisualFX
  CombatRT --> DOTween
  CombatRT --> InputSystem
  CombatRT --> TMP[Unity.TextMeshPro]

  MenuNav --> CombatRT
  MenuNav --> MobileControls
  MenuNav --> TimeOfDay
  MenuNav --> InputSystem
  MenuNav --> TMP
  MenuNav --> DOTween

  AC -.->|auto-ref| CombatRT
  AC -.->|auto-ref| MenuNav
  AC -.->|auto-ref| MobileControls
  AC -.->|auto-ref| VisualFX
  AC -.->|auto-ref| TimeOfDay
```

### Allowed reference matrix

Rows **may reference** columns marked ✓. Empty = not allowed (will not compile).

| From ↓ / To → | Assembly-CSharp | Combat.Core | TimeOfDay | VisualFX | MobileControls | Combat.Runtime | MenuNavigation |
|---|---:|---:|---:|---:|---:|---:|---:|
| **Assembly-CSharp** | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| **Combat.Core** | | ✓ | | | | | |
| **TimeOfDay** | | | ✓ | | | | |
| **VisualFX** | | | | ✓ | | | |
| **MobileControls** | | | | | ✓ | | |
| **Combat.Runtime** | | ✓ | ✓ | ✓ | ✓ | ✓ | **✗** |
| **MenuNavigation** | | ✓ | ✓ | | ✓ | ✓ | ✓ |

**Hard rule:** `Chainbound.Combat.Runtime` must **never** reference `SignalJam.UI.MenuNavigation`. Menu code may depend on combat data/services; not the reverse.

---

## What lives in Assembly-CSharp (default)

Scripts compile here when no parent folder defines an `.asmdef` / `.asmref`:

- `Assets/Scripts/Audio/**`
- `Assets/Scripts/Camera/**`
- `Assets/Scripts/Core/**`
- `Assets/Scripts/Interactable/**`
- `Assets/Scripts/Movement/**`
- `Assets/Scripts/Narrative/**`
- `Assets/Scripts/NPC/**`
- `Assets/Scripts/World/**` *(except `Environment/TimeOfDay/`)*
- `Assets/Scripts/UI/**` except:
  - `UI/MobileControls/` → MobileControls
  - `UI/MenuNavigation/` → MenuNavigation
  - `UI/UI_VisualFX/SweepParticles/` → VisualFX
- `Assets/Scripts/Combat/**` except `Runtime/`, `Editor/`, `Tests/`

**Cross-module usage today (Assembly-CSharp → named modules):**

- Narrative sequence steps use `Chainbound.Combat` and `SignalJam.UI.MenuNavigation`
- Core / Camera / Audio / World scripts often use `Chainbound.Combat`
- Main menu UI uses `SignalJam.UI.MenuNavigation`

That works because Assembly-CSharp auto-references those modules.

---

## Common failure patterns

| Symptom | Likely cause | Fix |
|---|---|---|
| Type not found in bootstrap/helper, but file exists | Script and consumer in **different assemblies** without a reference | Move script, add asmdef reference, or use shared contract in lower layer |
| Type not found after adding `.asmdef` | New module missing `references` entry | Update `.asmdef` and this doc |
| Circular dependency | Combat ↔ Menu mutual references | Keep menu → combat only; extract shared types to Combat.Runtime or Assembly-CSharp |
| Script in `Chainlings/` can't see `MenuScreen` | Chainlings is **Combat.Runtime**, not Assembly-CSharp | Don't put UI screens in Combat; put UI in MenuNavigation |
| Menu module can't reference Assembly-CSharp type | Named modules cannot reference the default assembly | Move shared type to a referenced module, or expose an interface in MenuNavigation implemented in Assembly-CSharp (`IMenuUiSfx`) |
| `.asmref` surprise | Folder silently joins Combat.Runtime | Check folder for `.asmref` before assuming Assembly-CSharp |
| Editor tool can't see test types | Test asmdef has `autoReferenced: false` | Reference test assembly explicitly or move shared code to runtime |

---

## Pre-change checklist (use before every multi-file or cross-folder change)

1. **Locate target folder** — Where will the new/changed `.cs` file live?
2. **Resolve assembly** — Use the module map above (check for `.asmdef`, `.asmref`, and `Editor/` subfolders).
3. **List dependencies** — Every `using` and serialized type reference across assemblies.
4. **Verify edges** — Each dependency must be allowed by the reference matrix.
5. **Editor vs runtime** — Editor-only code belongs in an `Editor/` assembly or `#if UNITY_EDITOR` block; don't pull Editor into runtime asmdefs.
6. **Prefer existing module** — Default to **Assembly-CSharp** or an existing module; do not add new `.asmdef` files unless the user explicitly asks or the split is clearly justified.
7. **Update docs** — If an `.asmdef` / `.asmref` / reference list changes, update this file and `.cursor/rules/assembly-modules.mdc` in the same PR.

---

## When to add a new asmdef (rare)

Only when **all** of the following are true:

- The split reduces compile time or enforces a hard boundary you actually need
- Dependency direction is one-way (no cycles)
- You will update **this document**, the cursor rule, and any affected `.asmdef` references
- You know which global Editor tools / tests need new references

Default alternative: keep feature code in **Assembly-CSharp** or extend an existing module's folder scope.

---

## Verification after assembly changes

1. Let Unity reimport / recompile (or trigger `Assets → Reimport All` if needed).
2. Confirm **zero** console errors in Editor.
3. Enter Play Mode in a scene that loads `GameServices` (menu + combat path).
4. If you touched Editor tooling, run the relevant menu item or edit-mode test.
5. If you added/changed test asmdefs, run Edit Mode tests for that module.

---

## File index

| File | Assembly |
|---|---|
| `Assets/Scripts/Combat/Core/Chainbound.Combat.Core.asmdef` | Chainbound.Combat.Core |
| `Assets/Scripts/Combat/Runtime/Chainbound.Combat.Runtime.asmdef` | Chainbound.Combat.Runtime |
| `Assets/Scripts/Combat/Editor/Chainbound.Combat.Editor.asmdef` | Chainbound.Combat.Editor |
| `Assets/Scripts/Combat/Tests/Chainbound.Combat.Tests.asmdef` | Chainbound.Combat.Tests |
| `Assets/Scripts/Chainlings/Chainbound.Combat.Runtime.asmref` | → Combat.Runtime |
| `Assets/Scripts/Solana/Chainbound.Combat.Runtime.asmref` | → Combat.Runtime |
| `Assets/Scripts/World/Environment/TimeOfDay/Chainbound.World.TimeOfDay.asmdef` | Chainbound.World.TimeOfDay |
| `Assets/Scripts/UI/MobileControls/SignalJam.UI.MobileControls.asmdef` | SignalJam.UI.MobileControls |
| `Assets/Scripts/UI/UI_VisualFX/SweepParticles/SignalJam.UI.VisualFX.asmdef` | SignalJam.UI.VisualFX |
| `Assets/Scripts/UI/MobileControls/InputLockService.cs` | SignalJam.UI.MobileControls |
| `Assets/Scripts/UI/MobileControls/InputLockHandle.cs` | SignalJam.UI.MobileControls |
| `Assets/Scripts/Core/VisualFx/SignalJam.VisualFx.asmdef` | SignalJam.VisualFx |
| `Assets/Scripts/UI/MenuNavigation/SignalJam.UI.MenuNavigation.asmdef` | SignalJam.UI.MenuNavigation |
| `Assets/Scripts/UI/MenuNavigation/Editor/SignalJam.UI.MenuNavigation.Editor.asmdef` | SignalJam.UI.MenuNavigation.Editor |
