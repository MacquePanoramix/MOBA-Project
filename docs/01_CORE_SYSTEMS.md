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
- **Conventional stats remain separate:** Health, Health Regen, Attack Damage (AD), Attack Speed, Ability Haste (replacing hard-capped CDR), Armor, Magic Resist, Movement Speed, Heal Power, **Maximum Mana**, **Mana Regeneration**, and **Resource Cost Reduction (RCR)** are independent stats rather than derived automatically from Str/Agi/Int.
- **Shared Hero Resource System (`v0.2.12–v0.3.0`, see [`docs/11_ITEMS_AND_RESOURCES.md`](11_ITEMS_AND_RESOURCES.md)):**
  - Supports `Mana` and `Resourceless` (`None`) kits via shared `AbilityResourceSpec` modes (`None`, `Flat`, `ContinuousPerSecond`, `PercentCurrent`, `PercentMax`).
  - **Approved kit assignments:** **Miravel (Hero Luís)** and **Chain Laser Shooter** use **Mana** (`0` Mana auto-attacks & Miravel auto-orbs; Miravel `[R]` drains `ContinuousPerSecond` independent of INT and ends cleanly at `0`; Laser `[W]`/`[E]` stay affordable and `[R]` pays a constant upfront cost), while **Ogre** and **Old Man Swordsman** are **Resourceless**.
  - **Resource Cost Reduction:** Bounded diminishing returns strictly below `100%` (`0.65 × RCR / (RCR + 65)` provisional `< 65%` cap) so ability costs never reach zero. Continuous Mana regeneration applies both inside and outside combat.
- **Accessible & Interactive Hero Guide (`v0.2.10–v0.2.11`):**
  - Hero Guide cards (`[C]`) prioritize plain gameplay fantasy, scannable key-fact pills, qualitative *"What Scales?"* rows, explicit mathematical curve labels, expandable **`[Show Math]`**, and isolated looping RenderTexture ability demonstrations (`LaneAbilityPreview` running on an offset `y = -260` sandbox that cannot mutate active matches and persists preferences in `[F10]`).
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
- **Recipe-based, mostly passive-focused shop (`v0.2.13–v0.3.0`, see [`docs/11_ITEMS_AND_RESOURCES.md`](11_ITEMS_AND_RESOURCES.md)):**
  - Duplicate-aware recursive recipe upgrades (`ResolvePurchase` matches owned intermediates before sub-components, deducts component value cleanly, frees slots on upgrade, `70%` sell refund, unique named passives).
  - **Foundation catalogue (`44` total items, all prices/values provisional):** **21 Approved Components** (`101–121`, zero Mana stats), **6 Approved Custom Advanced Items** (`201–206`, zero Mana stats: **Executioner's Axe**, **The Coward's Shield**, **Chalice of Life**, **Draconic Blade**, **The Giant's Heart**, **Banana Blade**), and **17 Provisional Starters/Components/Advanced Items** (`301–305, 311–315, 401–407` including the new Mana line: `Mana Dew Vial`, `Sapphire Shard`, `Dewdrop Charm`, `Runed Sigil`, `Wellspring Flask`, `Astral Reservoir`, `Tidecaller Chalice`, `Economist's Tome`).
  - Legacy ID `19` (`Spellweaver's Hourglass`) is retired, and **Forgotten Hourglass** remains deliberately shelved without implementation.
- **Approved Custom Advanced Item Concepts (`v0.3.0`; numerical values remain provisional):**
  - **Executioner's Axe:** Amplifies outgoing damage against wounded enemy heroes (`Final Judgment`, scaling linearly with target missing HP% and Bounded diminishing STR; supersedes `Bloodstone`'s `Wounded Strike`).
  - **The Coward's Shield:** Sustained retreat velocity away from a recent enemy hero attacker builds up to `+(20 + 0.85 × AGI)` Armor & MR (`0.15s` commit delay, `1.55s` ramp, `0.75s` grace hold on brief stops/stuns after `>= 0.40s` streak, `1.90s` stop / `0.85s` pursuit decay; displacement safe).
  - **Chalice of Life:** Amplifies valid incoming healing as HP drops (`Fountain of Life`, linear missing HP% × bounded bonus HealPower; excludes `Lingering Restoration`) plus `Healing Elixir`'s `Lingering Restoration` echo.
  - **Draconic Blade:** Combines `Steel Sword`, `Dragon Scale` (`Adaptive Scales` per-attacker direct-hit resists), and `Primordial Blood` (`Primordial Vitality` linear missing-HP natural regen) with **Shared Heat** (`Dragonfire` Magical burn + `Molten Edge` flat Arm/MR shred, zero direct HP/INT, `3.0:1` Arm:MR ratio).
  - **The Giant's Heart:** Combines `Heartstone`, `2 × Titanstone`, and `Primordial Blood` into **Ancient Vitality** (`<50%` bounded MaxHP damage deferral paid evenly over `STR`-bounded duration without double-mitigation or re-deferral), **Giant's Might** (`+3.0% MaxHP` as genuine AD), and **Immovable Mountain** (`<65%` STR-bounded displacement reduction), with zero direct AD/Arm/MR.
  - **Banana Blade:** Equal AGI + INT skirmisher blade featuring **Crescent Strike** (geometric crescent secondary Physical sweep: AD base, AGI damage ratio, INT reach/area), **Slippery Trickster** (hero-only `14s` fixed-CD AGI MS burst), **First Strike** (`Nimble Dagger`), and **Weightlessness** (`Winged Charm` `-25%` slow strength).
- **Kindred Censer (`v0.2.8` Universal Aura, `v0.3.0` Recipe — provisional item):** `+28% HealPower`, `+2.0 HP/s`, and `+2.2 × HealPower HP/s` allied aura (`7.5u`) on any hero.
- **Colossus Pauldron (`v0.2.8` Tooltip clarification, `v0.3.0` Recipe — provisional item):** Shield (`4s` `(65 + 2.0 × STR)`) triggers when its **owner inflicts** Stun/Knockback or drops below `45%` HP (`12s` CD).
- Prefer passive and mechanical effects over extra active buttons; the previously brainstormed active ratio is not a target.
- Item procs, cooldowns, stat interactions, and economy/experience distribution remain open for tuning.

## Map and team composition

- Consider 5v5 first for prototyping versus 6v6 as a distinctive full-game format.
- One suggested 6v6 composition is top, mid, jungle, bot carry, bot support, and **flex** (secondary top support/roamer or second jungler).
- Larger jungle should offer routes, camps, vision decisions, contested neutral spaces and travel-time tradeoffs, not merely larger empty space.
- No decision yet on forced role assignments, player draft constraints, lanes' geometry, respawn times or specific objectives.
