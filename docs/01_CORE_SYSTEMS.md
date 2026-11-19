# Core Systems — Accepted Intent vs. Proposals

## Movement, actions and world

**User intent:** slower and weightier, reminiscent of early Dota 2, with janky-feeling movement and trees. This should be tuned empirically in the first prototype.

**Proposed parameters to expose for tuning (not approved values):** move speed, acceleration/deceleration, turn rate, stopping distance, attack wind-up/backswing, body/collision radius, pathfinding responsiveness, targeting rules, obstruction avoidance.

**Trees:** important to map character and physicality. Tortuga can eat one. Details of felling, destruction, regrowth, damage interactions, and whether trees obstruct attacks/vision all need experiments.

**Bushes:** possible additional vegetation/stealth layer; not decided. A candidate distinction is trees block physical movement/vision while bushes conceal occupants without blocking movement.

## Combat, targeting and CC

- Lean toward skillshots and positional abilities over enemy-targeted point-and-click abilities.
- Aim for shorter CC durations than traditional longer-lockdown MOBA design.
- Keep combat readable despite unusual interactions and potentially 12 players.
- Exact target acquisition, hitbox, immunity, turn, projectile, attack and crowd-control stacking rules are TBD.

## Attributes and scaling

- Three core attributes: **Strength, Agility, Intelligence**.
- **No intrinsic universal effects:** Strength does not automatically grant HP; Agility does not automatically grant attack speed; Intelligence does not automatically grant mana. Abilities/items explicitly refer to these attributes.
- Individual abilities may scale with one or multiple attributes, as well as other values (health, recent damage, total collected souls, size, resistances, etc.).
- These names may have thematic conventions, but no requirement forces every hero into one primary attribute or one damage archetype.
- Distinguish *attribute scalars* from *base stats* (HP, resistance, speed, etc.), which are still needed for game simulation.

## Economy and items

- Support and jungler items likely exist.
- Supports should gain satisfying gold and scaling opportunities; no exact percentage of carry income approved.
- Most finished items should have passive effects and/or stats rather than additional active abilities. The previously discussed possible 10–20% active item ratio was only brainstorming, not a target.
- Item procs, cooldowns, stat interactions, and economy/experience distribution are TBD.

## Map and team composition

- Consider 5v5 first for prototyping versus 6v6 as a distinctive full-game format.
- One suggested 6v6 composition is top, mid, jungle, bot carry, bot support, and **flex** (secondary top support/roamer or second jungler).
- Larger jungle should offer routes, camps, vision decisions, contested neutral spaces and travel-time tradeoffs, not merely larger empty space.
- No decision yet on forced role assignments, player draft constraints, lanes' geometry, respawn times or specific objectives.
