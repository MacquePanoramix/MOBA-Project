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
- **Scaling curve terminology (`v0.2.8` Approved terminology):** Ability and tooltip math distinguish four scaling behaviors:
  - **Linear:** Each additional point provides the same constant absolute benefit (`Base + c × Stat`).
  - **Bounded diminishing returns:** Additional investment always helps, asymptotically approaching a finite maximum or floor (`Base + M × S / (S + H)`). Used for Armor/MR mitigation (`100 / (100 + R)` above `0` and `2 - 100 / (100 - R)` below `0`), **Ability Haste** (`Base × 100 / (100 + AH)`), thresholds, radii, stun/KB caps where bounded, and Miravel's R amps.
  - **Unbounded diminishing returns:** Additional investment always grants positive gain with progressively decreasing marginal return and **no finite mathematical maximum** (`sqrt(1 + k × S)` or `coeff × (sqrt(1 + k × S) - 1)`).
  - **Inverse duration scaling:** Increasing an effective speed multiplier divides the associated duration (`Duration = Base / SpeedMultiplier`).
- **Global Attack Speed redesign (`v0.2.8` Approved concept, provisional `k = 2.4`):**
  - Instead of scaling attack frequency linearly forever, bonus Attack Speed follows an **unbounded diminishing square-root curve** with zero upper hard cap:
    - `EffectiveAttackSpeed = sqrt(1 + k × BonusAttackSpeed)` *(provisional `k = 2.4`: `+30% AS → 1.31x`, `+60% → 1.56x`, `+180% → 2.31x`, `+500% → 3.61x`, `+1200% → 5.46x`)*.
  - Governs basic-attack wind-up & recovery (`Base / EffectiveAS`), Swordsman Strike 1 & Strike 2 cadence, Ogre ability recovery durations, and Chain Laser `[R]` beam proc frequency (`0.42s / EffectiveAS`, using an accumulator loop in simulation so high AS is not bottlenecked to one proc per tick).
  - **30 Hz simulation tick-rate limitation (Engineering finding, `v0.2.8`):** `LaneMatch` ticks at `30 Hz` (`dt = 0.0333s`). Individual Wind-up and Recover phase timers round up to whole ticks without carrying sub-tick remainder across phase transitions; at normal AS (`+0%` to `+180%`), simulated Miravel basic rate (`1.25/s` to `2.73/s`) stays close to continuous theory (`1.28/s` to `2.96/s`), while extreme stacking (`+500%` to `+1200%`) loses ~14–19% throughput to discrete `2–3` tick wind-up/recover quantization.

## Economy and items

- Support and jungler items likely exist.
- Supports should gain satisfying gold and scaling opportunities; no exact percentage of carry income approved.
- **Diverse, mostly passive-focused shop designed independently of specific heroes:**
  - Not every item grants Strength, Agility, or Intelligence. Items can grant pure conventional stats (e.g., Attack Damage, Attack Speed, Health, Armor, Magic Resist, Ability Haste, Movement Speed), pure attributes, combinations, attribute-scaled passives (including scaling attributes a hero's kit doesn't natively focus on), or mechanical/situational passives with no attribute scaling.
  - **The Coward's Shield (`v0.2.8` Rework — Concept: Luís; numbers provisional):**
    - Grants `+15 Arm`, `+15 MR`, `+12 AGI` (`450g`). Passive grants up to `+(20 + 0.85 × AGI)` Armor & MR *(Linear AGI scaling)* based on genuine sustained retreat movement (`>= 45%` of move speed with velocity dot product `>= 0.45` away from a hero who damaged the owner within `5.0s`).
    - Requires a brief `0.15s` commit delay and `1.55s` gradual buildup; facing away while stationary, enemy displacement, or rapid left-right wobble no longer grants stacks.
    - Holds (without building) through a `0.75s` grace window on brief stops/stuns/path blockers (earned only after `>= 0.40s` continuous retreat and `>= 0.15` charge), then decays gradually on stationary stops (`1.90s` full fade) and decays noticeably faster (`0.85s` full fade) if turning toward/pursuing the attacker.
  - **Kindred Charm (`v0.2.8` Universal Aura — provisional item):** `+22% Heal Power` and emits an allied regeneration aura (`+1.6 × HealPower HP/s` within provisional `7.5u` radius) on **any** hero who purchases it (detached from Miravel's kit-exclusive aura; exact range & multi-holder stacking rules remain open).
  - **Colossus Pauldron (`v0.2.8` Tooltip clarification — provisional item):** Shield (`4s` `(65 + 2.0 × STR)`) triggers when its **owner inflicts** Stun/Knockback or drops below `45%` HP (`12s` CD).
- Prefer passive and mechanical effects over extra active buttons; the previously brainstormed active ratio is not a target.
- Item procs, cooldowns, stat interactions, and economy/experience distribution remain open for tuning.

## Map and team composition

- Consider 5v5 first for prototyping versus 6v6 as a distinctive full-game format.
- One suggested 6v6 composition is top, mid, jungle, bot carry, bot support, and **flex** (secondary top support/roamer or second jungler).
- Larger jungle should offer routes, camps, vision decisions, contested neutral spaces and travel-time tradeoffs, not merely larger empty space.
- No decision yet on forced role assignments, player draft constraints, lanes' geometry, respawn times or specific objectives.
