# NPC System Setup Guide

This guide covers how to set up NPCs in Signal Jam, including prefab wiring, sprite configuration, route authoring, time-of-day integration, interaction binding, and memory usage patterns.

## Table of Contents

1. [Prefab Wiring Checklist](#prefab-wiring-checklist)
2. [Sprite Setup](#sprite-setup)
3. [Route Authoring](#route-authoring)
4. [Time-of-Day Integration](#time-of-day-integration)
5. [Interaction-to-Sequence Binding](#interaction-to-sequence-binding)
6. [Memory Usage Patterns](#memory-usage-patterns)
7. [Claim/Release Patterns for Cutscenes](#claimrelease-patterns-for-cutscenes)

---

## Prefab Wiring Checklist

### NPCTemplate Prefab Structure

```
NPCRoot (prefab root)
├── Rigidbody
│   ├── Use Gravity: true
│   ├── Freeze Rotation: X, Y, Z (all checked)
│   └── Constraints: Freeze Rotation
├── CapsuleCollider (world collision)
│   ├── Is Trigger: false
│   └── Size: appropriate for NPC
├── SphereCollider (interaction trigger)
│   ├── Is Trigger: true
│   └── Radius: 2-3 units
├── NPCController
│   ├── NPC ID: "townsperson_a" (unique)
│   ├── Interrupt Policy: Normal
│   ├── Autonomy Enabled: true
│   └── Route Schedule: (assign RouteSchedule asset)
├── NPCMovementAgent
│   ├── Move Speed: 2.0
│   ├── Rotation Speed: 360
│   ├── Waypoint Reach Distance: 0.5
│   └── Repath Interval: 0.5
├── NPCSequencerBinding
│   └── (auto-finds NPCController)
├── NPCStateBlackboard
│   └── (default state)
├── NPCMemoryComponent
│   ├── Disposition: 0
│   └── (flags/counters empty by default)
│
├── Visual (child)
│   ├── MeshFilter
│   ├── MeshRenderer
│   │   └── Material: DepthWrite material
│   ├── MeshRendererNPCVisual
│   │   ├── Idle Sequences: (8-frame breathing loop)
│   │   ├── Walk Sequences: (7-8 frames per direction)
│   │   ├── Run Sequences: (fast tempo)
│   │   ├── Blink Sprites: Down, Left, Right (no Up)
│   │   ├── Blink Chance: 0.1
│   │   └── Blink Duration: 0.1
│   └── SpriteBillboard
│       └── Mode: FaceCamera
│
├── Silhouette (child)
│   ├── MeshFilter
│   ├── MeshRenderer
│   │   └── Material: SilhouetteOccluded material
│   └── (OcclusionSilhouette component on NPCRoot references this)
│
└── Shadow (child)
    ├── MeshFilter
    ├── MeshRenderer
    │   └── Material: SpriteShadowCaster shader
    └── NPCShadowCaster
        ├── Visual Mesh Renderer: (reference Visual)
        └── Y Position Offset: 0.01
```

### Component Setup Steps

1. **Create NPCRoot GameObject**
   - Add Rigidbody (UseGravity=true, FreezeRotation)
   - Add CapsuleCollider (world collision)
   - Add SphereCollider (IsTrigger=true, interaction)

2. **Add Core Components**
   - NPCController (set unique npcId)
   - NPCMovementAgent
   - NPCSequencerBinding
   - NPCStateBlackboard
   - NPCMemoryComponent

3. **Create Visual Child**
   - Add MeshFilter + MeshRenderer
   - Add MeshRendererNPCVisual
   - Add SpriteBillboard
   - Assign sprite sequences (Idle/Walk/Run)
   - Assign blink sprites (Down/Left/Right only)

4. **Create Silhouette Child**
   - Add MeshFilter + MeshRenderer
   - Assign SilhouetteOccluded material
   - Add OcclusionSilhouette component to NPCRoot, reference this mesh

5. **Create Shadow Child**
   - Add MeshFilter + MeshRenderer
   - Add NPCShadowCaster
   - Reference Visual's MeshRenderer
   - Assign SpriteShadowCaster shader material

---

## Sprite Setup

### Animation States

NPCs support **only** Idle, Walk, and Run states (no Sprint/Squat/Jump/Landing).

### Idle Animation (8-Frame Breathing Loop)

- **Frame Count**: Exactly 8 frames per direction
- **FPS**: 2.0 (slow tempo for breathing)
- **Directions**: Down, Left, Right, Up
- **Purpose**: Creates subtle breathing animation

### Walk Animation

- **Frame Count**: 7-8 frames per direction
- **FPS**: 8.0 (medium tempo)
- **Directions**: Down, Left, Right, Up

### Run Animation

- **Frame Count**: 7-8 frames per direction
- **FPS**: 12.0 (fast tempo)
- **Directions**: Down, Left, Right, Up

### Blink Overlay System

**Critical Constraints:**

1. Blink triggers **ONLY** when:
   - Animation state is **Idle**
   - Current displayed frame index is **0** (first frame of 8-frame loop)
   - Interrupt policy is **Normal** (not Suppressed/Frozen)

2. Blink replaces frame 0 with direction-specific blink sprite:
   - **Down** → Blink_Down sprite
   - **Left** → Blink_Left sprite
   - **Right** → Blink_Right sprite
   - **Up** → **NO BLINK** (skip)

3. Blink duration: 0.1 seconds (configurable)

4. Blink chance: 0.1 (10% per idle-frame-0 occurrence, multiplied by route schedule's blinkRateMultiplier)

**Sprite Requirements:**

- Blink_Down sprite (replaces Idle frame 0 when facing Down)
- Blink_Left sprite (replaces Idle frame 0 when facing Left)
- Blink_Right sprite (replaces Idle frame 0 when facing Right)
- **No Blink_Up sprite needed** (blink skipped when facing Up)

---

## Route Authoring

### Creating Waypoint Markers

1. Create empty GameObject in scene
2. Add `NPCWaypointMarker` component
3. Set `waypointId` (e.g., "townsquare_bench")
4. Position at desired waypoint location
5. Waypoint auto-registers with NPCWaypointRegistry on Awake

**Example:**
```
Waypoint_TownSquare_Bench
├── NPCWaypointMarker
│   └── Waypoint ID: "townsquare_bench"
└── Position: (10, 0, 5)
```

### Creating Route Assets

1. Right-click → `Create` → `SignalJam` → `NPC` → `Route`
2. Name: `Route_TownSquare_Day`
3. Configure:
   - **Waypoint IDs**: Add list of waypoint IDs in order
     - `townsquare_bench`
     - `townsquare_fountain`
     - `townsquare_shop`
   - **Loop Route**: true (NPC loops back to first waypoint)
   - **Wait Time at Waypoint**: 2.0 seconds
   - **Speed Multiplier**: 1.0 (normal speed)

### Creating Route Schedule Assets

1. Right-click → `Create` → `SignalJam` → `NPC` → `Route Schedule`
2. Name: `RouteSchedule_Townsperson_A`
3. Configure per time segment:

**Day Segment:**
- Route Asset: `Route_TownSquare_Day`
- Blink Rate Multiplier: 1.0
- Micro-Wander Chance: 0.3
- Micro-Wander Radius: 1.5
- Micro-Wander Cooldown: 10.0

**Night Segment:**
- Route Asset: `Route_TownSquare_Night`
- Blink Rate Multiplier: 0.5 (reduced)
- Micro-Wander Chance: 0.1 (reduced)
- Micro-Wander Radius: 1.0
- Micro-Wander Cooldown: 15.0

**Dawn/Dusk:** Configure similarly

4. Assign RouteSchedule to NPCController's `Route Schedule` field

---

## Time-of-Day Integration

NPCs automatically respond to time-of-day changes:

1. **NPCController** subscribes to `ITimeOfDayService.OnSegmentChanged`
2. On segment change:
   - Looks up new route from `NPCRouteScheduleSO`
   - Applies route to `NPCMovementAgent`
   - Updates blink rate multiplier on visual
3. Respects hold/pause state from time service

**Setup:**

- Ensure `TimeOfDayManager` exists in scene
- NPCController auto-finds it on Awake
- No manual configuration needed

**Testing:**

- Use TimeOfDayManager's debug UI to change time segments
- Verify NPC switches routes automatically
- Check blink rate changes per segment

---

## Interaction-to-Sequence Binding

### Method 1: Proximity-Only Interaction

Use `Step_NPC_WaitForInteraction` in sequence:

```
Step_NPC_WaitForInteraction:
  NPC ID: "townsperson_a"
  Mode: ProximityOnly
  Interaction Radius: 2.0
```

NPC triggers interaction automatically when player enters radius.

### Method 2: Button Press Interaction

```
Step_NPC_WaitForInteraction:
  NPC ID: "townsperson_a"
  Mode: ButtonPress
  Interaction Radius: 2.0
  Interact Button Name: "Interact"
```

NPC waits for player to press Interact button while in radius.

### Method 3: Scene Trigger

1. Add `SequenceTrigger` component to NPC's interaction SphereCollider
2. Set `triggerId` (e.g., "npc_townsperson_a_interact")
3. In sequence, use `Step_WaitForTrigger`:
   ```
   Step_WaitForTrigger:
     Trigger ID: "npc_townsperson_a_interact"
     Arm Trigger: true
   ```

### Recording Interactions

`Step_NPC_WaitForInteraction` automatically records interaction in NPC memory:
- Updates `lastInteractionTime`
- Can be queried later for reactive dialogue

---

## Memory Usage Patterns

### Setting Flags

Use `Step_NPCMemory_SetFlag`:

```
Step_NPCMemory_SetFlag:
  NPC ID: "townsperson_a"
  Flag Key: "quest_completed"
  Flag Value: true
```

**Common Flag Patterns:**
- `quest_completed` - Quest finished
- `gave_item` - Player gave item to NPC
- `met_before` - First meeting flag
- `dialogue_seen_X` - Dialogue variant flags

### Modifying Disposition

Use `Step_NPCMemory_AddDisposition`:

```
Step_NPCMemory_AddDisposition:
  NPC ID: "townsperson_a"
  Disposition Delta: +10
```

**Disposition Ranges:**
- -100 to -50: Hostile
- -50 to -10: Unfriendly
- -10 to +10: Neutral
- +10 to +50: Friendly
- +50 to +100: Very Friendly

**Common Patterns:**
- Help NPC: +5 to +10
- Complete quest: +10 to +20
- Harm NPC: -10 to -20
- Give gift: +5

### Incrementing Counters

Use `Step_NPCMemory_IncrementCounter`:

```
Step_NPCMemory_IncrementCounter:
  NPC ID: "townsperson_a"
  Counter Key: "times_visited"
  Increment Amount: 1
```

**Common Counter Patterns:**
- `times_visited` - How many times player talked to NPC
- `items_given` - Items given to NPC
- `quests_completed` - Quests completed for NPC

### Using Memory in Conditions

Memory flags/counters can be checked in sequence conditions:

1. Create `ConditionVariableCompare` asset
2. Set variable name (e.g., `npc_townsperson_a_flag_quest_completed`)
3. Use in `Step_If` or `Step_Branch`

**Note:** Memory is stored on NPC component, not in sequence variables. For sequence conditions, you may need to copy to sequence variables first, or create custom conditions.

---

## Claim/Release Patterns for Cutscenes

### Basic Pattern

```
Step_NPC_ClaimControl:
  NPC ID: "townsperson_a"
  Claim Type: Hard
  Reason: "Cutscene dialogue"

Step_NPC_MoveTo:
  NPC ID: "townsperson_a"
  Use Waypoint ID: true
  Waypoint ID: "cutscene_position"

Step_NPC_FaceTarget:
  NPC ID: "townsperson_a"
  Target Type: Player

Step_PlayDialogue:
  Dialogue Sequence: Dialogue_TownspersonGreeting

Step_NPC_ReleaseControl:
  NPC ID: "townsperson_a"
```

### Soft Claim Pattern (Finish Current Action)

```
Step_NPC_ClaimControl:
  NPC ID: "townsperson_a"
  Claim Type: Soft  // Allows finishing current micro-wander
  Reason: "Approaching dialogue"

// Wait for NPC to finish current action
Step_WaitForDistance:
  Transform ID: "townsperson_a"
  Distance Threshold: 1.0
  Within Distance: true

Step_NPC_FaceTarget:
  NPC ID: "townsperson_a"
  Target Type: Player

Step_PlayDialogue:
  Dialogue Sequence: Dialogue_TownspersonGreeting

Step_NPC_ReleaseControl:
  NPC ID: "townsperson_a"
```

### Multiple NPCs in Cutscene

```
// Claim all NPCs
Step_NPC_ClaimControl: NPC "npc_a", Hard
Step_NPC_ClaimControl: NPC "npc_b", Hard
Step_NPC_ClaimControl: NPC "npc_c", Hard

// Move NPCs to positions
Step_NPC_MoveTo: NPC "npc_a", waypoint "cutscene_pos_a"
Step_NPC_MoveTo: NPC "npc_b", waypoint "cutscene_pos_b"
Step_NPC_MoveTo: NPC "npc_c", waypoint "cutscene_pos_c"

// Face targets
Step_NPC_FaceTarget: NPC "npc_a", Player
Step_NPC_FaceTarget: NPC "npc_b", Player
Step_NPC_FaceTarget: NPC "npc_c", Player

// Play dialogue/cutscene
Step_PlayDialogue: Dialogue_GroupConversation

// Release all NPCs
Step_NPC_ReleaseControl: NPC "npc_a"
Step_NPC_ReleaseControl: NPC "npc_b"
Step_NPC_ReleaseControl: NPC "npc_c"
```

### Interrupt Policy Effects

- **Hard Claim** → `Frozen`: Movement canceled immediately, blink disabled
- **Soft Claim** → `Suppressed`: Finish current micro-action, then yield; blink reduced
- **Normal** → Full autonomy: routes, micro-wander, blink enabled

---

## Performance Considerations

### Mobile/PSG1 Optimization

1. **Repath Interval**: 0.3-0.5s (not per-frame)
2. **Blink Check**: Only when frame == 0, not every frame
3. **Micro-Wander Cooldown**: 5-15s minimum
4. **Route Updates**: Only on segment change events (not polling)
5. **Interaction Detection**: Single distance check per frame (not continuous raycasts)

### Best Practices

- Limit active NPCs in scene (use object pooling if needed)
- Use waypoint-based routes (avoid complex pathfinding)
- Keep micro-wander radius small (1-2 units)
- Set blink chance low (0.1 default) to reduce visual noise
- Use Suppressed policy when NPC is near player (reduces wander/blink)

---

## Troubleshooting

### NPC Not Moving

- Check `NPCController.AutonomyEnabled` is true
- Check `InterruptPolicy` is not Frozen
- Verify route schedule is assigned
- Check waypoints are registered (check console for warnings)

### Blink Not Working

- Verify blink sprites are assigned (Down/Left/Right)
- Check `InterruptPolicy` is Normal (not Suppressed/Frozen)
- Verify idle animation has 8 frames
- Check blink chance > 0

### NPC Not Responding to Time Changes

- Verify `TimeOfDayManager` exists in scene
- Check NPCController found time service (check console)
- Verify route schedule has routes for all segments

### Interaction Not Triggering

- Check interaction radius is appropriate (2-3 units)
- Verify player has correct tag
- Check `Step_NPC_WaitForInteraction` mode matches desired behavior
- For button press: verify button name matches Input System action

---

## Example: Townsperson_A Setup

See `Assets/Prefabs/NPCs/Townsperson_A.prefab` for complete example:

- Prefab configured with all components
- Route schedule with Day/Night routes
- Sprite sequences assigned
- Blink sprites configured
- Interaction trigger set up
- Demo sequence using NPC steps

---

For more information, see `Docs/Architecture/NPC_System_Spec.txt` and `Docs/Narrative/SEQUENCE_AUTHORING_GUIDE.md`.

