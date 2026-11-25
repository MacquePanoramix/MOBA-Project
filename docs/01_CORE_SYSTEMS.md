# Core Systems — Accepted Intent vs. Proposals

## Movement, actions and world

**User intent:** slower and weightier, reminiscent of early Dota 2, with janky-feeling movement and trees. This should be tuned empirically in the first prototype.

Luís wants substantial variation in movement feel across units and heroes. In the first lab playtest, both the quicker and slower turn rates felt suitable. Specific values and unit assignments are still open.

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
- **No intrinsic universal effects:** Strength does not automatically grant HP or regen; Agility does not automatically grant attack speed or armor; Intelligence does not automatically grant mana or spell damage. Conceptually, all three work like Ability Power in League of Legends — values that individual ability properties and item passives can explicitly scale with.
- **Thematic flavors (guidelines, not rigid rules):**
  - **Strength:** often associated with physical force, impact, displacement, durability, or heavy crowd-control effects.
  - **Agility:** often associated with speed, responsiveness, precision, movement, or fluidity.
  - **Intelligence:** often associated with magical, intricate, spatial, sustained, or unusual effects.
- **Conventional stats remain separate:** Health, Health Regen, Attack Damage (AD), Attack Speed, Ability Haste (replacing hard-capped CDR), Armor, Magic Resist, Movement Speed, and Heal Power are independent stats rather than derived automatically from Str/Agi/Int.
- **Smooth hyperbolic diminishing returns instead of artificial hard caps (Provisional prototype direction, `v0.2.4`):** Percentage-based and threshold-sensitive stats use theoretical diminishing curves so stacking remains less rewarding past high thresholds instead of hitting a flat ceiling:
  - Armor & MR use `100 / (100 + R)` above `0` and `2 - 100 / (100 - R)` when shredded below `0`.
  - **Ability Haste (`AH`)** replaces hard-capped `45%` CDR using `Base × 100 / (100 + AH)`.
  - Attack Speed and Ogre Wind-up Speed scale animation speed linearly while cast/attack times follow `Base / Speed` hyperbolically.
  - Threshold-sensitive `StatFormula` properties (such as stun duration, knockback distance, `% Max HP` bonus damage, vulnerability amplification, and bonus move speed) use `Diminishing` (`M × Stat / (Stat + H)`) instead of clamped hard caps, while single abilities can still scale different properties from different stats simultaneously.
- **Provisional prototype hero showcase (not locked numbers):**
  - **Ogre** demonstrates multi-dimensional scaling: Attack Damage linearly scales his club strike damage, Agility scales his ability wind-up/recovery speed (`+1% / AGI`, hyperbolically reducing cast times with zero hard cap), and Strength hyperbolically increases stun durations and knockback distance (`Diminishing`, no hard cap). Hybrid builds are valid.
  - **Hero Luís (Miravel)** and **Old Man Swordsman** keep provisional, configurable scalings (`LaneTuning`) pending designer review.

## Economy and items

- Support and jungler items likely exist.
- Supports should gain satisfying gold and scaling opportunities; no exact percentage of carry income approved.
- **Diverse, mostly passive-focused shop designed independently of specific heroes:**
  - Not every item grants Strength, Agility, or Intelligence. Items can grant pure conventional stats (e.g., Attack Damage, Attack Speed, Health, Armor, Magic Resist, Cooldown Reduction, Movement Speed), pure attributes, combinations, attribute-scaled passives (including scaling attributes a hero's kit doesn't natively focus on), or mechanical/situational passives with no attribute scaling.
  - **Design example — The Coward's Shield:** grants temporary resistances scaling with Agility when retreating away from an enemy hero who recently damaged you, fading quickly if you turn back toward them to pursue.
- Prefer passive and mechanical effects over extra active buttons; the previously brainstormed active ratio is not a target.
- Item procs, cooldowns, stat interactions, and economy/experience distribution remain open for tuning.

## Map and team composition

- Consider 5v5 first for prototyping versus 6v6 as a distinctive full-game format.
- One suggested 6v6 composition is top, mid, jungle, bot carry, bot support, and **flex** (secondary top support/roamer or second jungler).
- Larger jungle should offer routes, camps, vision decisions, contested neutral spaces and travel-time tradeoffs, not merely larger empty space.
- No decision yet on forced role assignments, player draft constraints, lanes' geometry, respawn times or specific objectives.
