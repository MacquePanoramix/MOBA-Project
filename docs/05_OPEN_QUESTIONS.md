# Open Questions — Designer Has Not Yet Decided

## Design

- Is 6v6 the intended main mode, an optional mode, or future expansion of a 5v5 core?
- For 6v6, is the sixth slot unassigned/flex or formally a top support/second jungler?
- Should bushes exist separately from trees? If yes, what distinct vision rule applies?
- How wide should the range of movement feel be across units and heroes? Which differences should come from turning, speed, acceleration, or braking?
- How much commitment should a basic attack require before and after contact? Which timing and interruption rules should vary across units?
- How destructive and persistent is the forest system (tree regrowth, path opening)?
- What match length and map travel times are intended?
- How should gold/XP be distributed to supports and secondary junglers?
- How are CC durations, tenacity, diminishing returns and cleansing balanced?
- How should the three attributes relate thematically to certain mechanics, while keeping zero inherent bonuses?

## Hero rules

- Tortuga shell: damage distribution/mitigation order; shell HP pool; Pet Owner persistence in shell-less form.
- Chibi: how corpses, death timers, revived form duration and hero abilities interact.
- Four-Form: 0-shot behavior, multicast self-knockback, ally dash consent, order selection rules.
- All-Passive: whether standing still while attacking qualifies for E and what constitutes a W damage-second.
- Luís: orb exact spawn logic, curse stacking, E protection of illusions, interrupt rules on R.
- Ogre: where does animation-based CC immunity end relative to recovery? How long can the R sequence be, how do queued inputs and executed strikes interact with effects triggered by casting, and does the provisional AGI (`+0.8%/AGI`) + Attack Speed (`+70%` of bonus AS) animation-speed weighting feel balanced?
- Airplane Fighter: how tightly can it turn, where does parking trigger, and which heroes' attacks or ground areas can hit it in flight? The continued straight flight during a stun or root is established.
- Old Man Swordsman: if the first strike lands and the opponent pushes him out of reach before the second, does the second strike still connect? This rule determines whether Ogre can deny the E stacks after the swordsman earns the W heal. The remaining kit questions are in the [hero design](02_HERO_DESIGNS.md#8-old-man-swordsman).
- Bomb Planter: multi-bomb stacking/timers; whether planter death defuses planted bombs immediately or only blocks explosion; hard-taunt vs. reveal-channel semantics for E; W wall-vs-tree rules; enemy R skill-check presentation.
- Automatron Bot 3000: engineer map presence and XP linkage; individual Automatron auto-buy vs. engineer purchasing; death/bounty rules; region/objective controls and tiered passive pools.
- Chain Laser Shooter: chain bounce range, damage falloff, and stack caps/duration; Q aim lock vs. cursor tracking; W stack consumption; R aim lock vs. tracking and beam proc cadence.
- Multiple Phases Raid Boss Hero: fountain/death reset with no healing or regen; burst-damage clamping at 20% phase thresholds; exact Q/W/E multi-part combo shapes; R arena wall vs. blinks and whether 1% true damage/sec also affects the Raid Boss.
- Kobold King: Q/W/E kobold respawn cadence vs. spawn-on-cooldown; proximity stat-scaling radius/curve; Sling Kobold RNG vs. PRD stun chance; R succession camera/control handoff, summon-slot reset, and whether the dead old king leaves a corpse or vanishes cleanly. (Note: required multi-unit RTS selection/control architecture is now established via `LaneControls` / `LaneRtsSelection`.)
- Mr. Random: modular ability-generation grammar and compatibility rules (preventing broken/recursive loops); relative weights of randomness vs. internal kit synergy vs. lane affinity across the 6 positions (`Top`, `Mid`, `Bot Carry`, `Support`, `Safe Jungle`, `Advanced Jungle`); power-budget constraints; loading-screen vs. match-start reveal timing; opponent/ally inspection UI; dynamic tooltip and VFX/telegraph synthesis.
- Twin-Stick Shooter Roguelike: 3 vs. 4 level-up options per level; whether pools are handcrafted modules, procedural, or hybrid; whether upgrades can modify basic `LMB` projectile shooting itself; minimum level gates or upgrade limits vs. unrestricted rush to 4th Ultimate slot; rarity distribution curves; exact rules for the 4th-slot (`E`) Ultimate transformation bonus; non-obstructive in-combat selection UI and kit inspection for opponents/allies.

## Practical

- Unreal Engine vs other candidate engines: recommended UE5, but first check machine specs and installed tooling.
- Art direction and use of placeholders.
- Official game name: **`Ultimate Free MOBA`** is now the provisional game name (`Woodland Lane` remains the current prototype battlefield / internal development name; `MOBA Project` is only the root repository folder name).
- Version control hosting and multiplayer test infrastructure.
