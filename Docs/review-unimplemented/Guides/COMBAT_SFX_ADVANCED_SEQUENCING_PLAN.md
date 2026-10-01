> **Parked for review.** Yet to implement. Foundation SFX shipped; this recipe/role model did not. Review list: [../README.md](../README.md).

# Combat SFX Advanced Sequencing Plan

## Purpose

This plan covers the deeper combat audio work after the foundation layer is stable. It focuses on multi-target sequencing, layered attack recipes, species and move fallbacks, mix control, and data-driven authoring patterns for complex battle moments.

Use this plan only after the foundation plan has established:
- A universal combat SFX catalog.
- Presenter-driven outcome sounds.
- Frame-synced species and move sounds through presentation tracks.
- UI sounds routed through the shared audio service path.

## Design Goal

Combat SFX should communicate three things clearly:

1. Who acted.
2. What move was used.
3. What happened to each target.

The system should support layered sound without turning 2v2 combat into audio clutter.

## Advanced Sequencing Model

### Audio Roles

Each sound in a battle beat should belong to one role:

- `Anticipation`: windup, charge, targeting, breath-in.
- `Actor`: species voice, melee grunt, ranged effort.
- `Move`: elemental or move-specific cast/release.
- `Travel`: projectile, beam, dash, lingering motion.
- `Impact`: hit, block, miss, shield contact.
- `Consequence`: KO, status applied, barrier break, heal settle.
- `UI`: confirm/cancel/ineligible feedback outside the battle sound queue.

Sequencing should be based on roles rather than hard-coded event names.

## Suggested Recipe Data

Create a recipe layer only when the foundation catalog begins to feel too rigid.

Possible asset:

`CombatSfxRecipeSO`

Suggested fields:
- `recipeId`
- `steps`
- `defaultTargetStaggerSeconds`
- `maxSimultaneousOutcomeVoices`
- `allowCritAccent`
- `koDelayAfterImpactSeconds`
- `fallbackToUniversalOutcome`

Suggested step fields:
- `role`
- `soundEvent`
- `source`
- `playbackSpace`
- `delaySeconds`
- `volumeScale`
- `perTarget`
- `skipIfBlocked`
- `skipIfMissed`
- `onlyIfCrit`
- `onlyIfKo`

Example recipe:

```text
damaging_single_target
  Actor: species attack frame event
  Move: move release frame event
  Impact: universal hit/block/miss from event outcome
  Consequence: species KO after impact if target faints
```

The recipe layer should not replace frame tracks. It should coordinate event-side sounds and provide fallback rules.

## Multi-Target Sequencing

### Default Rule

For one move affecting multiple targets:

1. Play actor/species sound once.
2. Play move-specific cast/release sound once.
3. Play per-target outcome sounds with a tiny stagger.
4. Play KO sounds after each affected target's impact.

Recommended target stagger:
- 0.02 to 0.04 seconds for simultaneous-feeling impacts.
- 0.06 to 0.10 seconds for readable chain hits.
- 0.12+ seconds only for intentionally sequential moves.

### Mixed Outcomes

If one target is hit and another misses:
- Play the move sound once.
- Play hit and miss outcome sounds per target.
- Stagger by target order.

If one target blocks and another is hit:
- Play block for the blocked target.
- Play hit for the hit target.
- Do not play hit on the blocked target.

If multiple targets are KO'd:
- Play each impact first.
- Play KO sounds after a short per-target delay.
- Stagger KO cries slightly if needed.

### Polyphony Policy

Use `SoundEventSO` polyphony keys intentionally.

Recommended keys:
- `combat_hit`
- `combat_block`
- `combat_miss`
- `combat_status`
- `combat_ap`
- `combat_ko`
- `combat_ui`

Recommended max instances:
- Hit/block/miss: 3 or 4.
- AP: 1 or 2.
- KO: 2.
- UI: 1 or 2 for repeat-prone sounds.

Cooldown should be short enough to avoid machine-gun clutter but not long enough to suppress legitimate multi-target results.

## Priority Rules

### Same Target, Same Moment

Pick one outcome sound:

1. KO consequence can follow another outcome.
2. Block replaces hit.
3. Miss replaces hit.
4. Crit can replace hit or add a short accent.
5. Normal hit is the fallback.

### Same Move, Multiple Targets

Allow multiple outcome sounds, but stagger them.

### UI Versus Battle

UI sounds should be immediate and should not wait on combat sequencing.

### Status Plus Damage

If a move deals damage and applies status:

1. Hit/block/miss outcome first.
2. Status sound after a short delay if status actually applied.
3. Status immune sound replaces status applied sound.

## Species And Move Fallbacks

### Species Fallbacks

Frame-synced species SFX should remain preferred.

Optional future fields on species/profile data:
- `spawnSound`
- `swapInSound`
- `meleeAttackFallbackSound`
- `rangedAttackFallbackSound`
- `hitReactionSound`
- `koSound`

Fallback rule:
- If the active profile has a frame event for the role, let it play.
- If not, the orchestrator may play the fallback sound at the relevant event.

### Move Fallbacks

Move presentation tracks should remain preferred for cast/release/travel sounds.

Optional future fields on move data:
- `castSound`
- `releaseSound`
- `travelSound`
- `impactAccentSound`

Fallback rule:
- If `MoveDefinitionSO.presentationTrack` has audio events, use those.
- If not, use simple move category defaults from a catalog.

## Layering Patterns

### Melee Attack

```text
0ms     Species effort / slash frame
40ms    Move identity accent if any
Impact  Universal hit/block/miss
+100ms  KO if target faints
```

### Ranged Attack

```text
0ms     Species ranged effort
50ms    Move cast/release
Travel  Projectile loop or whoosh if applicable
Impact  Universal hit/block/miss
+100ms  KO if target faints
```

### Utility Buff

```text
0ms     Move cast
Impact  Universal buff apply
```

### Utility Debuff

```text
0ms     Move cast
Impact  Universal debuff apply or immune
```

### Barrier / Block

```text
0ms     Move or barrier raise sound
Impact  Universal block or barrier contact
Break   Barrier broken sound if applicable
```

## Mix Control Ideas

### Volume Scaling

Use lower volume for simultaneous per-target sounds:
- Single target: 1.0
- Two targets: 0.85
- Three or more: 0.7

### Important Event Accents

Let important consequence sounds cut through:
- KO
- Crit
- Barrier break
- Status immune

These should have cleaner clips, shorter cooldowns, and distinct frequency space.

### Avoiding Audio Mud

Avoid stacking:
- Multiple full-volume hits on the same frame.
- AP grant sounds per slot at full volume.
- Long move tails over KO cries.
- UI error sounds while battle outcome sounds are firing, unless the player directly caused the UI event.

## Potential Orchestrator API

Future service:

```csharp
public interface ICombatSfxOrchestrator
{
    void PlayTurnStartApGrant(IReadOnlyList<CombatEvent> apEvents);
    void PlayDamageOutcome(CombatEvent evt, Combat2ChainlingView source, Combat2ChainlingView target, bool isCrit, bool isKo);
    void PlayMiss(CombatEvent evt, Combat2ChainlingView source, Combat2ChainlingView target);
    void PlayBlock(CombatEvent evt, Combat2ChainlingView source, Combat2ChainlingView target);
    void PlayHeal(CombatEvent evt, Combat2ChainlingView target);
    void PlayStatus(CombatEvent evt, Combat2ChainlingView target);
    void PlaySwap(CombatEvent evt, Combat2ChainlingView outgoing, Combat2ChainlingView incoming);
}
```

Keep the presenter responsible for knowing combat context and views. Let the orchestrator decide sound selection, delay, location, and fallback.

## Advanced Implementation Milestones

1. Add `CombatSfxOrchestrator` behind an interface.
2. Move universal outcome sound selection out of presenter handlers and into the orchestrator.
3. Add multi-target grouping support for events that share a move/action context.
4. Add per-target stagger and outcome priority rules.
5. Add optional species fallback sounds where frame tracks are missing.
6. Add optional move category fallback sounds where move tracks are missing.
7. Add `CombatSfxRecipeSO` only if catalog fields become too rigid.
8. Add debug logging for final resolved sound sequence.
9. Add test cases for hit/miss/block/KO priority and TurnStart AP batch routing.

## QA Scenarios

- One damaging move hits one target.
- One damaging move crits one target.
- One damaging move misses one target.
- One damaging move is blocked.
- One damaging move KOs a target.
- One multi-target move hits both targets.
- One multi-target move hits one target and misses another.
- One multi-target move hits one target and is blocked by another.
- One move deals damage and applies a debuff.
- One status move is immune.
- Turn-start AP batch with two, three, and four active chainlings.
- Swap out and swap in with universal and species sounds.

## Out Of Scope Until Needed

- Dynamic music stingers.
- Combat slow-motion audio ducking.
- Fully timeline-authored sound sequences.
- Procedural audio.
- Per-surface combat impact material sounds.
- Voice line cooldown and bark scheduling.

