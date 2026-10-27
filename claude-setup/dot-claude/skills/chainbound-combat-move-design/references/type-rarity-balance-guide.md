# Type And Rarity Balance Guide

Use this guide to preserve the identity of each type and keep rarity from becoming raw power creep.

## Rarity Rules

- Common: one clear job, simple execution, no hidden condition. Examples: damage, one stat stage, one status.
- Uncommon: reliable with a minor condition, drawback, or secondary rider.
- Rare: meaningful condition, dual-purpose effect, reactive play, or build hook.
- Epic: multi-condition effect, strong synergy hook, stat substitution, or wider rule interaction.
- Legendary: constrained rule exception, signature interaction, or high-impact build enabler.
- Mythic: unique, volatile, build-defining effect with meaningful downside or scarcity.

Rarity controls complexity, availability, modification potential, and sometimes a modest intensity step. A rarer move may be a stronger sibling of a simpler move, but it must not strictly erase the lower-tier move.

## Tiered Sibling Moves

Tiered sibling moves are allowed. They are higher-rarity moves that share a tactical role with a lower-rarity move but sit one notch above it, like a reliable medium attack versus a stronger, less reliable attack.

Use this pattern to add depth without inventing new resolver mechanics:

- Keep the core mechanic familiar: damage, accuracy, AP cost, hit count, status chance, or a simple stage rider.
- Make the higher-tier sibling a sidegrade-plus, not a replacement.
- Preserve at least one reason to equip the lower-tier move: lower AP cost, higher accuracy, no drawback, better modifier compatibility, safer targeting, or simpler reliability.
- Keep the numeric step modest; prefer small power/status/accuracy differences over huge breakpoints.
- Use tradeoffs where needed: lower accuracy, higher AP cost, recharge, self-risk, narrower target use, or lower status reliability.
- Keep both descriptions clear and tactical; do not describe the higher-tier move as a direct upgrade.

Good framing:

- "Higher-risk, higher-impact sibling."
- "Stronger but less reliable variant."
- "More expensive pressure option."
- "Adds a status rider at the cost of accuracy or AP."

Bad framing:

- "The rare version is simply better."
- "Same move, more damage, no drawback."
- "Higher rarity means higher power."

Example patterns:

| Lower-Tier Move | Higher-Tier Sibling | Healthy Difference |
| --- | --- | --- |
| Reliable 2 AP / 85 power / 95 accuracy | 3 AP / 105 power / 85 accuracy | Stronger hit trades AP and accuracy |
| Simple 1 AP chip | 2 AP chip with 25% status chance | More utility, less efficient spam |
| 2-hit barrier pressure | 2-3 hits with lower per-hit power | Better ceiling, still variable |
| Basic debuff | Debuff plus minor damage or longer recharge | More impact, more commitment |

When auditing these, ask: "If I already own the higher-tier sibling, why might I still choose the lower-tier one?" If there is no answer, reduce the high-tier move or add a meaningful tradeoff.

## Numeric Guardrails

- AP cap is 10; baseline AP gain is +2 per turn.
- Damaging moves usually cost 1-3 AP; utility 0-2 AP; support 2-4 AP; passives 0 AP.
- Single-source mitigation must stay at or below 30%.
- Stage range is -6 to +6.
- Active move debuffs may apply up to -2 stages per stat per use.
- Passive automatic debuffs should not exceed -1 per stat per turn.
- ChemistryOnly statuses may only come from the chemistry resolver on damaging hits.
- Multi-hit moves should have lower per-hit power and are a primary barrier counter.

## Type Identities

| Type | Primary Role | Move Design Bias | Avoid Making This The Main Identity |
| --- | --- | --- | --- |
| None | Baseline toolkit | Reliable typeless damage, basic buffs/debuffs, simple AP/support primitives | Type-specific status identity |
| Node | Team coordinator | Support, AP routing, ally-wide buff/heal, redirect, buff duration | Direct enemy debuff spam |
| Gas | AP tempo disruptor | AP tax, AP spikes, volatile self-trade, resource pressure | Pure defense |
| Block | Physical wall | Barriers, mitigation, defense ramp, anti-mobility, slow | Speed burst |
| Encrypted | Controller/protector | AP drain, debuff immunity, cleanse, cipher denial | Direct DoT identity |
| Burned | Attrition attacker | DoT-adjacent pressure, Atk debuffs, anti-buff pressure, finishers | Sustained healing core |
| Frozen | Speed controller | Speed debuffs, switch lock, stall, timing control | Raw burst as default |
| Forked | Burst spreader | AoE, chain split, high speed, recoil tradeoffs | Safe sustain |
| Mined | Ramp attacker | Physical scaling, extraction, exploit windows, tank-crashing | Team support focus |
| Staked | Sustain/growth | Patience, AP yield, defense scaling, leech, long-game value | Fast burst identity |
| Compressed | Precision efficiency | Accuracy, evasion, AP efficiency, compressed tradeoffs | Broad AoE identity |
| Bot | Automation attrition | Persistent debuffs, AP denial, multi-hit, scripted pressure | Clean healing/setup focus |
| Glitched | Chaos exploiter | Randomness, defense corruption, exploit payoffs, unstable trades | Reliable clean buffs |
| Shard | Fragile precision | Partition, field fragmentation, defense/reflect tricks, laser focus | AoE spam as default |
| Meme | Wildcard risk/reward | Random outcomes, all-in plays, hype streaks, swingy value | Consistent reliability |

## Modifier Compatibility Lens

Before setting rarity or numbers, ask how the move behaves when modified:

- High base power plus power modifiers can create burst creep, especially on tiered siblings.
- Multi-hit moves multiply on-hit chances and barrier pressure.
- Priority plus high damage or strong utility can erase counterplay.
- AoE plus status application can over-compress team pressure.
- AP-positive moves can snowball if they also deal strong damage or deny enemy AP.
- Support moves with broad targeting should pay for reliability through AP cost, condition, or rarity.

Keep Commons competitive and readable. Let rarer moves be more conditional, flexible, build-defining, or carefully tuned sibling variants rather than automatically superior.
