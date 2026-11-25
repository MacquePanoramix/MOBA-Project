# Fifteen Hero Concepts — User-Authored Mechanics

These are **working transcriptions of Luís's hero designs**. Each kit records the mechanics he described and flags tentative numbers and open questions. Design feedback appears at the end.

---

## 1. Hero Luís (Miravel in `WoodlandLane`)

### Basic attack
- Ranged staff projectile attack (designer-confirmed for `WoodlandLane` prototype); wind-up and recovery scale inversely with global `EffectiveAttackSpeed = sqrt(1 + k × BonusAS)` (`k = 2.4` provisional).

### Passive
- Increase allied health regeneration in an aura.
- Decrease enemy health regeneration in an aura **(can push total net enemy health regeneration negative, causing real HP decay and lethal takedowns credited to Miravel)**.
- Participating in a hero takedown permanently increases Heal Power slightly (base Heal Power starts at `100%` / `1.0x`, active from Level 1).
- **`v0.2.8` Approved scaling:** Aura radius scales with **Intelligence** (*Bounded diminishing*); allied healing bonus & enemy regen reduction scale **Linearly** with **Heal Power** (zero direct INT scaling on aura regen/drain values).

### Q (passive)
- Periodically spawns an orb in the world.
- Luís can pick it up; pickup automatically causes a pulse: heal nearby allies including himself slightly, and deal damage to nearby enemies.
- Enemies can step on the orb to deny/remove it.
- The orb expires after some time.
- **`v0.2.8` Approved scaling:** Heal and damage scale **Linearly** with **Heal Power** (zero direct INT damage/heal scaling); pulse radius scales with **Intelligence** (*Bounded diminishing*); spawn interval scales via **Inverse Ability Haste** (`Base × 100 / (100 + AH)`) with no `4.0s` artificial floor.

### W
- Become invisible for a short time and leave behind a temporary illusion.
- Designer-confirmed prototype behavior (`v0.2.6`): the illusion walks to the cursor's location at cast time (no maximum distance limit), stops walking and stops its walk animation once it reaches that point, and can absorb enemy hero hits to trigger E's curse.
- **`v0.2.8` Approved scaling:** No attribute scaling (cooldown scales with Ability Haste only).

### E
- For a short duration, when Luís or any of his illusions takes damage from an enemy hero, curse that attacker.
- Designer-confirmed prototype rule (`v0.2.2`): casting E does **not** break active W invisibility.
- The curse burns over time; each time the cursed enemy uses an ability, its damage/power and duration increase.
- **`v0.2.8` Approved scaling:** Curse DPS scales **Linearly** with **Intelligence**; duration remains attribute-independent.

### R
- Channel a nearby area over time.
- Enemies inside accumulate progressively stronger slowing and increased damage taken.
- Allies progressively gain increased healing received/effectiveness.
- Continue until full duration or channel interrupted.
- **`v0.2.8` Approved scaling:** Area radius scales with **Intelligence** (*Bounded diminishing*); maximum allied healing amplification and maximum enemy damage vulnerability scale via **Bounded diminishing returns** from **bonus Heal Power above `100%`** (`healPower - 1.0`); slow remains fixed by rank.

**Provisional `v0.2.8` numbers (`LaneTuning`):** `530 HP (+34/lvl)`, `48 AD (+3.5/lvl)`, `Arm 8`, `MR 18`, `Regen 1.4/s`, `Move 4.35`, `Turn 540°/s`, Basic `6.2u` (`0.26s` wind / `0.52s` rec); Aura `7.2 + 4.2×INT/(INT+60)u`, Ally `+(2.2+0.35×(Lvl-1))×HealPower`, Drain `-(2.6+0.35×(Lvl-1))×HealPower`, `+10%` HealPower/kill; Q `8.0/7.4/6.8s × AH`, pulse `3.8 + 2.4×INT/(INT+60)u`, heal `(68/88/108)×HealPower`, dmg `(62/80/98)×HealPower`; W invis `3.2/3.6/4.0s`, decoy `4.5s`, CD `10.5/9.4/8.3s`; E ward `4.5/5.1/5.7s`, curse `4.2/4.6/5.0s + 1.2s/cast`, 1-stack DPS `22/30/38 + 65% INT`, CD `8.5/7.7/6.9s`; R range `5.0u`, `4.2s` channel, radius `4.2 + 2.6×INT/(INT+60)u`, max slow `52/58%`, HealAmp `25/30% + 45%×(HPwr-1)/((HPwr-1)+0.80)`, Vuln `28/34% + 42%×(HPwr-1)/((HPwr-1)+0.80)`, CD `26/23s`.

**Unresolved:** orb placement, orb pickup priority, exact classification/resistance interactions of negative natural regeneration, whether multiple curses stack, exact channel constraints, and final numbers.

---

## 2. All-Passive Hero (working name)

### Main Passive
- If **all other passives** on this hero are active simultaneously, gain a temporary buff granting lifesteal, status resistance/tenacity, resistances and amplified versions of those other passives.
- Refresh this state when the condition is met again.

### Q (passive)
- Attacking an enemy hero gains one Q stack.
- At a threshold, consume stacks to activate temporary attack-speed bonus.
- Reaching the threshold again refreshes the bonus.
- Unused stacks decay after some time.

### W (passive)
- Every full second taking damage from an enemy hero gains one W stack.
- At a threshold, consume stacks to activate a temporary damage-over-time burning aura around the hero.
- Retriggering refreshes duration; unused stacks decay after some time.

### E (passive)
- Each full second remaining stationary gains an E stack.
- At a threshold, consume stacks for a temporary movement-speed buff.
- Retriggering refreshes duration; unused stacks decay after some time.

### R (passive)
- Killing an enemy hero grants a large damage bonus for a duration; further kills refresh it.

**Important:** Hero has **no active Q/W/E/R abilities**. Full-state buff only triggers when all four secondary passives overlap.

**Unresolved:** whether attacking while stationary counts for E, definition of damage taken each second for W, how buffs refresh, exact stack behavior and whether R only accepts killing blows (original says yes).

---

## 3. Chibi Necromancer (inspired by Nicolini's idea)

### Passive
- Gain a quantity of **Chibi Souls** when killing minions, killing enemy heroes or participating in hero takedowns.
- Click a deceased ally hero's corpse to **spend all current Chibi Souls** and revive them in **Chibi form**.
- Revived Chibi's power scales with the number of souls spent on that resurrection.
- Most item stats are less effective on this hero.

### Q
- Send a slow, zigzagging Chibi ghost in the target direction, damaging enemies it passes through.
- Its damage scales with **total Chibi Souls earned throughout the game**, not just current unspent souls.

### W
- Summon several Chibi skeletons.
- Their strength scales with lifetime Chibi Souls collected.

### E
- Expand a dark aura out from the hero to a certain radius for a duration.
- Enemies inside are nearsighted, except that they can see the Necromancer's eyes and the eyes of Chibi beings with red eyes.

### R
- The Necromancer and **all Chibi beings** gain red eyes, a movement-speed buff and critical hits/critical amplification on all damage during the effect.
- During R, Q ghost also fears enemies that it damages.

**Unresolved:** revival duration/form mechanics, interaction with respawn timers, simultaneous revived allies, exact souls received, what counts as a Chibi being, if crit is usual crit or unique multiplier, and item reduction formula.

---

## 4. Four-Form Botlaner (inspired by Lacerda's play style; Lopes might enjoy it)

### Global rules
- Four forms: ADC, Poke Mage Support, Engage Support, Enchanter Support.
- At game start, determine an **ordered cycle** containing all four forms.
- Hero has low base attack speed.
- Q/W/E/R cooldowns are **shared by slot across all forms**.
- Every time any ability is cast, immediately advance to the **next form in the predetermined order**.

### ADC form

**Form passive:** One second after casting an ability, next basic attack is an empowered critical strike. Each attack on an enemy hero grants a **Shot** stack, up to 3; stacks reset if no enemy hero is attacked for a while. When using an ADC-form ability, cast it **once for each Shot stack**.

- **Q:** Shoot a projectile forward, damage first enemy hit and knock yourself backward.
- **W:** Target a region; after a warning indicator, a projectile hits a **random location inside that region**, dealing damage and rooting those in the impact zone.
- **E:** Empower next basic attack's damage.
- **R:** Glow red; after a delay, explode around self, dealing damage based on recent basic-attack damage dealt.

**Unresolved:** Can abilities cast at 0 Shot stacks? Does multicast replicate movement/self-knockback? Are shot stacks consumed on cast? Exactly when the crit arms and expires?

### Poke Mage Support form

**Form passive:** Abilities have high mana costs. Hitting an enemy hero with an ability refunds its mana cost.

- **Q:** Piercing projectile; speeds up after hitting each enemy.
- **W:** Choose an area around the hero and throw a delayed projectile that lands at the selected **relative** region (precise movement-relative behavior TBD).
- **E:** Target an ally; after a delay, explode around them, damaging and slowing nearby enemies. Target gets a visible cue.
- **R:** Fire a projectile through enemies; bounces off walls, gets stronger with each bounce, and explodes after a certain number of bounces.

### Engage Support form

**Form passive:** Crowd controlling an enemy hero grants a shield.

- **Q:** Attempt a double hook; if both hooks hit targets, pull those targets to the midpoint of hook range. They collide, taking damage and being stunned.
- **W:** Slam ground in an area around self, damaging enemies and reducing their damage dealt (weaker Tidehunter-like comparison).
- **E:** Dash in a chosen direction, damaging and knocking up the first enemies hit (Gragas-like reference; probably weaker).
- **R:** Explode around self, damaging and knocking away enemies. Enemies that collide with a wall are stunned.

### Enchanter Support form

**Form passive:** Using an ability on an ally hero rapidly dashes that ally toward the enchanter until next to them (designer compares to Thresh lantern).

- **Q:** Buff target ally's next basic attack damage.
- **W:** Buff target ally's next ability damage.
- **E:** Buff target ally's next ability range and radius.
- **R:** Area-targeted heal for nearby allies.

**Unresolved:** Whether allied forced movement requires teammate consent; whether R targets allies and activates form passive; how delayed effects behave after form changes; form ordering UI; rules for stat/animation/attack change; shared cooldown timing.

---

## 5. Slow Tortuga

### With shell — state and passive
- Very slow movement, very low attack speed.
- Can **slowly eat a tree**; when finished, gain temporary health regeneration. Has cooldown.
- Shell serves as a secondary life-like pool.
- **50% of incoming damage** to Tortuga is distributed to shell; shell benefits from higher resistances than Tortuga.
- When shell HP reaches zero, shell is destroyed and only returns **after death**.

### With shell — abilities
- **Q:** Retreat inside shell; cannot take actions; shell takes **all** incoming damage while inside.
- **W:** Sleep; regenerate a percentage of maximum health each second.
- **E:** Lick an ally, channeling/completing the lick to designate them as **Pet Owner**. Only one at a time. When near owner, owner's attacks lifesteal/heal **for Tortuga**.
- **R:** Grow a lot.

### Without shell — state and passive
- Slow movement and low attack-speed penalties removed; gain movement-speed and attack-speed bonuses.
- Turns red.
- Basic attacks inflict bleed.

### Without shell — abilities
- **Q:** Big bite on target, applies many bleeding stacks; counts as a basic attack.
- **W:** Spit acid onto ground; damage-over-time zone that reduces resistances.
- **E:** Briefly channel, then charge in target direction.
- **R:** Grow large; additionally gain bonus damage based on how large Tortuga is.

**Unresolved:** Shell's actual HP and damage splitting order relative to resistances; Q interruption and exit; sleep interruption; pet owner status after shell break; giant size scaling/collisions; shell replenishment exceptions.

---

## 6. Ogre

Luís's animation reference is the Dota jungle ogre, particularly the weight and anticipation of its ground slams.

### Passive

- Ogre cannot perform basic attacks.
- Ogre is immune to crowd control while performing an ability animation, including its cast and active portions (`ogreImmuneDuringRecovery = true` default; immunity boundary into recovery remains an open detail).
- **`v0.2.8` Approved split animation scaling:**
  - **Wind-up speeds** scale exclusively with **Agility** (*Unbounded diminishing square root → Inverse duration*, no hard cap).
  - **Recovery speeds** scale exclusively with **Global Attack Speed** (`EffectiveAS = sqrt(1 + k × BonusAS)`, *Unbounded diminishing → Inverse duration*, no hard cap).

### Q

- After a long wind-up, slam the club into the ground ahead. The slam reaches a substantial distance in front of Ogre and stuns targets hit.
- **`v0.2.8` Approved dual-zone mechanics & scaling:**
  - Divides into two **mutually exclusive** impact zones: a **Central club impact zone** (full damage + stun) and an **Outer shockwave zone** (reduced damage, zero CC; visually & auditorily distinct).
  - Central impact damage scales **Linearly** with **Attack Damage**; stun scales with **Strength** (*Bounded diminishing*); central zone dimensions scale **Linearly** proportionally with Ogre's `[R]` transformation size multiplier; Outer shockwave width scales with **Strength** (*Bounded diminishing*).

### W

- Sweep the club horizontally along the ground in front of Ogre. The sweep has less reach than Q and pushes targets hit backward.
- **`v0.2.8` Approved scaling:** Damage scales **Linearly** with **Attack Damage**; knockback scales with **Strength** (*Unbounded diminishing square root*, zero hard cap — extreme Strength can push targets far beyond melee reach); attack dimensions scale **Linearly** proportionally with `[R]` size multiplier.

### E

- Wind up and pound the ground, stunning targets directly beneath Ogre and in the surrounding area.
- **`v0.2.8` Approved scaling:** Damage scales **Linearly** with **Attack Damage**; stun duration and impact radius scale with **Strength** (*Bounded diminishing*; stun and damage apply uniformly across the entire scaled area); radius also scales **Linearly** proportionally with `[R]` size multiplier.

### R

- Channel for a few seconds. Ogre visibly grows larger and angrier and gains high damage reduction during the channel.
- While channeling, the player enters a combination. For each input, choose Q or W and aim it individually.
- When the channel ends, Ogre performs the attacks in the order entered. Each strike uses the direction chosen for that input, and Ogre moves a short distance with each strike.
- These Q and W attacks animate and recover faster than their regular versions, following each other almost immediately. The final strike has an extra delay and deals a very large critical hit.
- **`v0.2.8` Approved scaling:** Transformation size multiplier scales with **Strength** (*Bounded diminishing*, proportionally enlarging `[Q]`/`[W]`/`[E]` attack dimensions); strike wind-ups use **Agility** (*Unbounded sqrt*) and strike followthrough/recovery use **Global Attack Speed** (*Unbounded sqrt*); final strike preserves extra weight/delay and large crit multiplier; `[R]` damage reduction remains attribute-independent.

**Provisional `v0.2.8` numbers (`LaneTuning`):** `720 HP (+48/lvl)`, `60 AD (+5/lvl)`, `Arm 16`, `MR 12`, `Regen 1.6/s`, `Move 3.75`, `Turn 270°/s`; `ogreWindupSpeed = sqrt(1 + 0.022 × AGI)`, `ogreRecoverSpeed = EffectiveAS`; Q Central `4.7u × 1.90u`, `0.88s` wind / `0.36s` rec, CD `6.8/6.2/5.6s`, `55/73/91 + 135% AD`, stun `1.05/1.20/1.35 + 0.85×STR/(STR+50)s`, Outer `+0.65/0.75/0.85 + 1.45×STR/(STR+55)u` (`55%` dmg, `0` CC); W `3.0u` cone (`cos 0.15`), `0.72s` wind / `0.30s` rec, CD `6.2/5.6/5.0s`, `40/55/70 + 105% AD`, KB `2.40/2.65/2.90 + 2.80×(sqrt(1+0.04×STR)-1)u`; E `0.62s` wind / `0.32s` rec, CD `8.5/7.7/6.9s`, `48/64/80 + 115% AD`, stun `0.95/1.07/1.19 + 0.65×STR/(STR+50)s`, radius `2.35 + 1.55×STR/(STR+55)u`; R `2.0s` channel (`65%` DR), size `1.22/1.28 + 0.52×STR/(STR+65)x`, `4` strikes (`0.82x` / final `1.85x`), `0.36s`/`0.62s` strike windup + `0.10s` followthrough + `0.28s` post-combo recover, CD `28/24s`.

**Open details.** Where does CC immunity end relative to recovery? How many inputs does R accept, and what happens if the player enters an incomplete sequence?

---

## 7. Airplane Fighter

### Passive — parked and airborne

- Start parked and immobile on the ground at base. The player can shop. The only ability available is a temporary takeoff command, potentially on Q.
- Once airborne, the fighter moves forward continuously, steering toward the cursor and ignoring terrain.
- A stun or root disables steering for its duration. The fighter continues forward on its current heading even if the player moves the cursor.
- Returning to base parks the fighter again.

### Q (airborne)

- Fire downward at the cursor's location at cast time, dealing damage in a small area without slowing targets. Luís cites Urgot's Q for the targeting feel.
- The cooldown should be very short. Luís suggested 0.5 or 0.75 seconds as examples.

### W

- Launch a forward missile aimed at the cursor's location at cast time. It can travel a substantial distance and explodes on first contact with an enemy unit, dealing area damage.

### E

- For a short duration, the fighter lays a carpet of bombs along its actual flight path. Each bomb stays on the ground for a time and damages enemies that walk over it. Luís cites Ziggs's E for the lingering hazard.

### R

- Fire a missile upward. It strikes the cursor's location recorded at cast time after a delay, dealing area damage. Enemies get a clear warning of where it will land. Luís cites Kog'Maw's R for the delayed strike.
- R has a relatively short cooldown and stores many charges. Around ten charges, possibly fewer, is Luís's tentative example.

**Open details.** The turning radius, parking boundary, airborne targetability and basic attack behavior are unspecified. The design describes how a stun or root affects steering. Casting during those effects and interactions with forced displacement remain open.

---

## 8. Old Man Swordsman

### Passive

- The swordsman has reduced movement speed. Each basic attack is an **asymmetric twin-blade pair**:
  - **1st Strike (Light Right Sabre):** faster wind-up, lower AD damage, visually distinct slender blade, procs **`[W]` Stance Temper**.
  - **2nd Strike (Heavy Left Greatsword):** heavier follow-up wind-up, higher AD damage, visually broader blade and heavier audio cue, procs **`[E]` Vital Draw**.
- **`v0.2.8` Approved scaling:** Both strike wind-ups and recovery divide by **Global Attack Speed** (`sqrt(1 + k × BonusAS)`, *Unbounded diminishing → Inverse duration*); **Agility** additionally shortens the Hit-1 → Hit-2 transition delay (*Bounded diminishing*), while keeping Hit 2 heavier and slower than Hit 1; strike damage scales **Linearly** with **Attack Damage**.

### Q

- Slash in a forward arc with a decent reach. Enemies caught in the arc take damage and are slowed.
- Designer-confirmed prototype rule (`v0.2.3`, from playtester feedback): hitting enemies with Q also applies R's lookback mark (prioritizing heroes).
- **`v0.2.8` Approved scaling:** Wind-up reduces with **Agility** (*Bounded diminishing*); **recovery duration remains fixed** (`not` reduced by Agility); damage scales **Linearly** with **Attack Damage**; slow strength & slow duration remain attribute-independent.

### W (`v0.2.8`: Swapped with E — Stance Temper on 1st Hit)

- When the **1st light strike** hits, the swordsman steals defenses: he gains stacks that increase his resistances and the opponent gains stacks that reduce theirs (can push enemy resistances below zero).
- **Self buff duration and enemy debuff duration refresh and track independently** and both scale **Linearly** with **Intelligence**; per-stack Armor/MR values remain attribute-independent.

### E (`v0.2.8`: Swapped with W — Vital Draw on 2nd Hit)

- The **2nd heavy strike** of the pair deals percentage-health magic damage and heals the swordsman for a percentage of that bonus damage.
- Interrupting or escaping before the 2nd strike completes denies this healing (`swordsmanDisplaceCancelsSecondHit = true` default).
- **`v0.2.8` Approved scaling:** Bonus `% Target Max HP` damage scales with **Strength** (*Bounded diminishing*); healing efficiency scales **Linearly** with **Heal Power**.

### R

- Dash by the last enemy the swordsman basic-attacked or hit with Q recently (updated from basic-attacks-only per `v0.2.3` playtest feedback), slashing them for substantial damage and ending in front of them.
- If that enemy is below a health threshold, execute them and refresh R's cooldown.
- **`v0.2.8` Approved scaling:** Slash damage scales **Linearly** with **Attack Damage**; execute threshold (`<100%`) scales with **Strength** (*Bounded diminishing*); dash speed scales **Linearly** with **Agility** (*Inverse travel duration*); lookback target mark duration scales **Linearly** with **Intelligence**.

**Provisional `v0.2.8` numbers (`LaneTuning`):** `615 HP (+40/lvl)`, `54 AD (+4/lvl)`, `Arm 14`, `MR 14`, `Regen 1.5/s`, `Move 3.45`, `Turn 480°/s`, Melee `2.35u`; Hit 1 `48% AD` (`0.20s` wind / `EffAS`), Hit 2 `76% AD` (`(0.40 - 0.14×AGI/(AGI+45)) / EffAS`), Recover `0.46s / EffAS`; Q `3.5u` cone, windup `0.30 - 0.15×AGI/(AGI+45)s`, fixed rec `0.24s`, `45/65/85 + 90% AD`, `40/45/50%` slow `2.1s`, CD `5.5/5.0/4.5s`; W `+8/+10/+12` Arm/MR self & `-8` enemy (max `6`), separate timers `6.5 + 0.08×INT` s; E `4.5/5.9/7.3% + 6.5%×STR/(STR+60)` target Max HP magic (`140` minion cap), heals `55% × HealPower` of bonus dmg; R `7.5u`, dash `16.0 + 0.16×AGI` u/s, mark `10.0 + 0.16×INT` s, `95/135 + 135% AD`, exec `20/24% + 25%×STR/(STR+70)`, CD `22/18s`.

**Open details.** Whether `[R]` cooldown should also reset on minion kills (in addition to hero kills) vs. hero-kill reset only; if the first strike lands and the opponent pushes Swordsman out of reach before Strike 2, does Strike 2 still connect? The resistances W affects and whether R checks execute before or after slash damage remain open.

---

## 9. Bomb Planter

### Passive
- Basic attacks deal no direct damage to enemy heroes; instead, they plant a bomb on hit.
- Each planted bomb has a very long timer (Luís suggested roughly 30 seconds as a tentative example). When the timer expires, if the Bomb Planter did not die during that window, the bomb explodes and deals area damage around the hero carrying it.
- Last-hitting minions or monsters causes them to explode, dealing area damage.

### Q
- Push off an adjacent enemy, dealing very small damage while knocking the enemy away and knocking the Bomb Planter away in the opposite direction.

### W (passive)
- Periodically, the Bomb Planter can jump over a wall (Luís compares to Talon in LoL), but after crossing can only run alongside that wall for a short time before automatically jumping back to the side it came from.

### E
- Channel a taunt over a very large area around the hero.
- While channeling, perform a taunting animation and give enemies a clear indication of the Bomb Planter's location (note: original voice transcription read "unification"; recorded as clear reveal/location indicator).
- Throughout the channel, continuously increase the damage of planted bombs on affected enemies.

### R
- Can only be activated while running; the Bomb Planter continues running in the locked initial direction during the cast.
- Over a short window, the player performs successive fast, difficult skill checks (Luís compares to *Dead by Daylight*).
- Each succeeded skill check leaves a large horizontal obstacle (such as a tree log) along the run direction.
- Enemies attempting to pass one of those obstacles must pass the same skill check; if they fail, the obstacle blocks them like a wall for its duration. Each consecutive placed obstacle lasts longer than the previous one.

**Open details.** Do multiple planted bombs on the same hero stack damage or run independent timers? Does the Bomb Planter's death defuse planted bombs immediately, or only prevent detonation if dead at expiry? Is E a hard forced-movement taunt or a loud provocation/reveal channel that amplifies bombs while inviting enemies to collapse? How does W distinguish terrain walls from trees, and what breaks the wall-run early? How do R's skill checks present on screen for allies vs. enemies without pulling attention fatally off cursor movement?

---

## 10. Automatron Bot 3000

### Passive
- You play as an outside observer engineer who does not control the Automatron directly. Instead, each Automatron runs on simple AI and accepts general region-movement or objective directives.
- You start the game with the 1st-edition Automatron and choose a starting passive for it from a list of passives.
- Each Automatron has its own individual level and gold to spend, while the engineer has a separate experience/level track related to the Automatrons.
- Whenever the engineer reaches an ultimate level threshold (up to 4 total Automatrons), you gain an additional Automatron of the next tier. Each new Automatron starts at level 1 with zero gold, inherits the 1st-edition passive, and lets you select an additional passive from a higher tier.
- Starts with Q, W, and E available; R requires owning more than one Automatron.

### Q
- Toggle between minimap view and a camera following the currently selected Automatron.

### W
- Toggle the currently selected Automatron's combat posture between **Brave** (fight enemy heroes) and **Coward** (automatically flee from enemy heroes).

### E
- Toggle whether the currently selected Automatron automatically farms.

### R
- Switch active control/selection to the next tier of Automatron, cycling back to the first tier after the highest tier.

**Open details.** Where does the engineer exist (off-map/untargetable vs. vulnerable avatar), and how is engineer XP derived from Automatron activity? How do Automatrons spend their individual gold (auto-build priorities vs. remote engineer purchasing), and what are death timers/bounties per Automatron? How do cursor clicks issue region/objective orders when in minimap vs. follow-camera mode? What are the exact tiered passive pools?

---

## 11. Chain Laser Shooter

### Passive
- Basic attacks fire a laser gun whose beam chains to additional targets in order with no cap on chain count, never repeating an already-hit target.
- Each subsequent bounce deals less damage, and every damaged target gains a **Laser Damage** stack.
- Each Laser Damage stack increases subsequent laser damage taken by that target from the hero's laser sources.
- **`v0.2.8` Approved scaling:** Attack frequency uses **Global Attack Speed** (`EffectiveAS = sqrt(1 + k × BonusAS)`, *Unbounded diminishing → Inverse duration*); bounce/hop search distance scales with **Intelligence** (*Bounded diminishing*); maximum stack damage amplification asymptote scales with **Intelligence** (*Bounded diminishing*); stack decay floor and hop damage falloff remain attribute-independent.

### Q
- Fire a continuous laser in a target direction for a duration, starting at very short range and steadily extending until it reaches maximum range, at which point it ends.
- Each target struck by the beam takes a single instance of laser damage.
- Killing multiple targets with Q grants escalating bonus gold for every kill after the first.
- **`v0.2.8` Approved scaling:** Damage scales **Linearly** with **Attack Damage**; extension speed scales with **Intelligence** (*Unbounded diminishing square root → Inverse duration*, replacing Agility); beam max range remains fixed.

### W
- Every target within range that carries Laser Damage stacks emits a laser area shock, dealing small damage and slowing both itself and nearby targets.
- Both the slow strength and slow duration from overlapping shocks stack (plus refreshed/staggered casts use the same diminishing model without allowing recast timer bypass).
- **`v0.2.8` Approved scaling:** Shock damage scales **Linearly** with **Intelligence**; shock radius scales with **Intelligence** (*Bounded diminishing*); single-shock slow percentage depends on rank only; overlapping/refreshed slow strength stacks with **Bounded diminishing returns**, and overlapping/refreshed slow duration scales with **Unbounded diminishing square root** (`Base × sqrt(shocks)` subtracting prior elapsed duration).

### E
- Step **patiently and calmly backward** in an animated single pace (no instant blink/teleport; interrupted cleanly if stunned/displaced mid-step), spawning a stationary laser decoy at the starting position upon completion that blocks enemy hero movement and projectiles.
- Creeps do not target the decoy. If an enemy damages it, they take small laser damage and a brief stun; if damaged by a hero, the decoy disappears.
- **`v0.2.8` Approved scaling:** Step-back animation duration scales with **Agility** (*Bounded diminishing shorter duration*); retaliation damage scales **Linearly** with **Intelligence**; retaliation stun scales with **ability rank only** (zero Strength scaling); step distance stays fixed.

### R
- After a brief pause, fire a continuous laser in the target direction while remaining able to move.
- Every enemy hero, minion, and monster inside the beam continuously receives individual laser basic-attack procs as though the hero were basic-attacking each of them directly.
- **`v0.2.8` Approved scaling:** Beam duration scales with **Intelligence** (*Bounded diminishing*); proc damage scales from **Attack Damage**; proc cadence scales inversely with **Global Attack Speed** (*Unbounded sqrt*); retained movement speed scales with **Agility** (*Bounded diminishing*, never reaching `100%`).

**Provisional `v0.2.8` numbers (`LaneTuning`):** `480 HP (+30/lvl)`, `46 AD (+3.5/lvl)`, `Arm 8`, `MR 10`, `Regen 1.3/s`, `Move 4.00`, `Turn 560°/s`, Basic `6.0u` (`0.22s` wind / `0.66s` rec); Passive hop `4.2 + 3.2×INT/(INT+60)u`, decay `0.70x` (`min 25%`), `6.0s` stacks (`halfPoint = 8`), `MaxAmp = 45% + 45%×INT/(INT+60)`; Q `1.2→8.2u`, base `0.88s / sqrt(1 + 0.024×INT)`, `44/62/80 + 105% AD`, `+12g` multikill bonus, CD `7.2/6.5/5.8s`; W scan `7.8u`, radius `2.40 + 1.85×INT/(INT+60)u`, `30/42/54 + 70% INT`, single slow `20/24/28%`, overlap cap `82%`, overlap/refresh dur `1.15s×sqrt(n)`, CD `7.5/6.8/6.1s`; E step `1.65u` over `0.38 - 0.20×AGI/(AGI+50)s`, decoy `4.5s`/`120 HP`, zap `34/48/62 + 65% INT`, rank-only stun `0.65/0.80/0.95s`, CD `11.0/10.1/9.2s`; R `0.50s` wind, `2.4/2.7 + 2.0×INT/(INT+65)s`, `62% AD` proc every `0.42s / EffAS`, move `78% + 22%×AGI/(AGI+50)`, CD `28/24s`.

**Open details.** `laserQTracksCursor = false`, `laserWConsumesStacks = false`, `laserRTracksCursor = false` toggles in `LaneTuning`; exact bounce radius, falloff floor, and stack cap/duration remain open for tuning.

---

## 12. Multiple Phases Raid Boss Hero (Schwan-inspired)

### Passive
- Has **five phases**, unlocking the next phase each time he loses 20% of his maximum health.
- Cannot be healed by any source and has zero health regeneration.
- Each phase transition plays a brief animation that cleanses all active effects and grants invulnerability for its duration.
- Starts Phase 1 with **no abilities unlocked** (basic attacks and movement only). Each new phase unlocks one ability in order—**Q** in Phase 2, **W** in Phase 3, **E** in Phase 4, **R** in Phase 5—provided the player has leveled that ability slot.
- Each phase transition also grants stat buffs, strengthens previously unlocked phase abilities, and shifts his appearance and oversized sword progressively further toward a demonic raid-boss form.

### Q (Phase 2+)
- Multi-part sword combo with a distinct pattern (pattern still open; Luís cites Aatrox/Riven as rough structural inspiration).

### W (Phase 3+)
- Multi-part combo of ground-erupting volcanic abilities.

### E (Phase 4+)
- Multi-part combo built around dashes.

### R (Phase 5)
- Summon a decently large circular arena that isolates everyone inside from the outside world.
- Units inside take roughly 1% max-HP (or current-HP, open) true damage per second while inside.
- Persists until either no enemy heroes remain inside the arena or the Raid Boss hero is no longer alive inside it.

**Open details.** How does recovery between fights work without healing or regen—only on death/respawn, or does fountain override the rule? Does burst damage stop/clamp at each 20% phase boundary so transitions cannot be skipped in one hit? Can shields or damage mitigation substitute for healing, and can ability points be invested before a phase unlocks its slot? Exact shapes/patterns of Q, W, and E combos, whether blinks/teleports can cross R's arena wall, and whether the 1% true damage/sec also ticks on the Raid Boss himself.

---

## 13. Kobold King

Based on the concept of a hero commanding RTS-controlled minion/servant units.

### Passive
- Whenever a unit you control kills a creep or monster, you gain bonus gold.
- Units you control gain bonus stats based on how close they are to you (starting within a fair proximity area).
- All units summoned by this hero can be fully controlled directly like units in an RTS.

### Q — Whip Kobold
- Periodically spawns a **Whip Kobold** if there is currently none alive.
- The Whip Kobold's basic attacks slow enemies on hit.
- Pressing **Q** orders it to perform a whip attack that damages and slows enemies hit in a straight line.

### W — Sling Kobold
- Periodically spawns a **Sling Kobold** if there is currently none alive.
- Has a ranged basic attack and uses its sling to hurl rocks with an on-hit chance to stun the enemy.
- Pressing **W** orders it to hurl a rock in a target direction, hitting and stunning the first enemy unit hit.

### E — Spear Kobold
- Periodically spawns a **Spear Kobold** if there is currently none alive.
- Has a piercing melee basic attack. Each target hit has its armor reduced on hit, stacking and refreshing/resetting its stack duration on each hit.
- Pressing **E** orders it to stretch its spear forward, passing through all enemies in a line and dealing damage strongly amplified by how much armor reduction each enemy currently has applied to them at the time of the damage.

### R — Succession (Heart Attack)
- Can only be cast while you are currently selecting one of your kobold servants.
- After casting R and waiting a short delay, if the kobold selected at cast time is still alive, the Kobold King suffers a heart attack and dies (this does **not** count as a real hero death for kill credit/bounty/deaths). If that selected kobold died before the delay finishes, the ability enters cooldown without doing anything.
- The selected kobold servant becomes the new **Kobold King**, effectively becoming the hero: it receives **full health**, retains **none of the previous king's active status conditions**, and has **all basic ability cooldowns (Q/W/E) reset**.

**Open details.** Do Q/W/E respawn timers pause while that kobold is alive or tick only once killed, and can servants walk anywhere on the map vs. leashing? Which stats scale with king proximity (damage, attack speed, resistances, move speed) and is the curve stepped or continuous? When R turns a servant into the new king (losing that servant slot until Q/W/E respawns a replacement), how does camera/RTS selection recover seamlessly, and does the new king inherit all items, level, and gold identically? Also consider Sling Kobold's RNG on-hit stun vs. pseudo-random distribution (PRD) to prevent frustrating back-to-back stunlocks.

---

## 14. Mr. Random

*Status: Documented concept for future roster — **do not implement the procedural generation system during the current milestone.** Numerical values, balance parameters, and generation weights remain undecided.*

### Core concept
- Mr. Random's entire ability kit is **procedurally generated at the beginning of each match**.
- Rather than randomly picking complete abilities from existing heroes, a **modular procedural generation algorithm** constructs original abilities by combining mechanics, properties, effects, and scaling relationships so the player experiences a potentially different hero every match.

### Passive
- All of Mr. Random's abilities (`Q`, `W`, `E`, `R`) are custom randomized abilities created by the modular procedural generation algorithm for each match.
- Also receives a **randomly generated passive ability** each match.

### Q / W / E / R
- **Q:** Randomly generated basic ability.
- **W:** Randomly generated basic ability.
- **E:** Randomly generated basic ability.
- **R:** Randomly generated ultimate ability with an appropriate ultimate-level power budget and identity.

### Procedural ability generation
- Genuinely modular construction rather than selecting from a finite pool of complete predetermined abilities.
- Candidate building-block categories: delivery methods, targeting methods, activation conditions, effects, triggers, projectile behavior, movement behavior, area shapes, durations, summoning mechanics, environmental interactions, resource interactions, scaling relationships, and other compatible mechanical modifiers.
- *Illustrative example (not a finalized ability):* combining a projectile + returning trajectory + offensive effect on the outward path + supportive effect on return + attribute-based scaling relationship.
- Should eventually be capable of producing abilities and combinations not individually hand-authored.

### Internal kit synergy
- Generation should gently encourage abilities within a rolled kit to synergize without becoming so rigid that the hero loses randomness or always rolls hyper-curated combos (retaining unexpected mechanics and occasionally awkward but playable rolls).
- Potential synergy mechanisms (illustrative): status setup/detonation across abilities, positional setup for another spell, shared attribute scaling across different mechanics, compensating weaknesses, shared resource loops, environmental or complementary combat/movement interactions — leaving room for player discovery.

### Lane affinity
- Rolled kit should have a soft affinity bias toward the player's selected position among the six intended positions: **Top**, **Mid**, **Bot Carry**, **Support**, **Safe Jungle**, and **Advanced Jungle**.
- Influences rather than hard locks: e.g., **Support** biases higher likelihood of ally utility, protection, healing, or crowd control while still allowing unusual offensive rolls; **Safe Jungle** biases clear/sustain/farming tools; two Support Mr. Randoms can still roll radically different kits.
- Balances three weights (still undecided): randomness/discovery, internal kit synergy, and position suitability.

### Kit reveal & intended experience
- Intended reveal timing: when the match begins or, preferably, during the **match loading screen** so the player can read the rolled kit before spawn (after choosing lane position).
- Opponents and allies must have a clear, fair way to inspect Mr. Random's generated abilities.
- Rewards adaptability, creativity, broad game knowledge, experimentation, and on-the-fly itemization rather than muscle memory for one static kit.

### Open technical & design details
- Modular ability definitions and mechanical compatibility validation (preventing broken or infinitely recursive triggers), reasonable power budgets, dynamic tooltip generation, readable procedural VFX/telegraphs, authoritative seed/kit replication in multiplayer, flexible per-property `StatFormula` scaling, and exact reveal/inspection timing and position weights.

---

## 15. Twin-Stick Shooter Roguelike

*Status: Documented concept for future roster — **do not implement procedural generation, roguelike progression, or specialized WASD combat systems during the current milestone.** Numerical values, balance rules, and pools remain undecided.*

### Core concept
- Combines **twin-stick shooter controls** with an in-match **roguelike progression system**: unlike Mr. Random (full rolled kit at match start), this hero begins with **no active abilities** and gradually builds its kit throughout the match via randomized level-up choices.

### Passive — Alternative movement controls
- Moves using **WASD** rather than conventional mouse click-to-move.
- Movement and aiming are independent (can strafe/retreat while aiming in another direction).
- Conventional **Stop** command is **unavailable** (releasing movement keys stops movement).

### Passive — Basic shooting mechanic
- Left mouse click (`LMB` default) fires a slow-moving, round, **physical and dodgeable** projectile toward the mouse cursor (replacing conventional targeted basic attacks).
- Deals damage to the first enemy touched or disappears upon reaching maximum range.
- Fire rate scales with **Attack Speed**; holding left mouse click continuously fires whenever the next shot is available.

### Passive — Roguelike level-up progression
- Starts with **zero active abilities**.
- At **every level (including Level 1)**, receives **3 or 4 randomized level-up options** (exact option count open): either a **new ability** or an **upgrade to an already-acquired ability**.
- Upgrades should be creative rather than purely numerical — potentially altering behavior, adding bouncing projectiles, delayed secondary effects, conditional triggers, or cross-mechanic interactions (illustrative examples only).

### Ability and upgrade rarity
- Rewards roll a rarity reflecting how **unusual, transformative, or mechanically wild** an ability or upgrade is rather than raw power: lower rarity = straightforward mechanics/stats, intermediate = unusual conditions/behaviors, higher = transformative changes to how an ability or the character plays.
- Higher rarity is **not** automatically stronger; a straightforward numeric option may outperform a transformative roll for a specific build.

### Ability slots and default controls
Acquired abilities fill slots in acquisition order (all default bindings compatible with the customizable keybinding system and Twin-Stick control context):
1. **Right mouse click (`RMB` default) — First acquired ability.**
2. **`Shift` default — Second acquired ability.**
3. **`Q` default — Third acquired ability.**
4. **`E` default — Fourth acquired ability (Ultimate slot):** whichever ability is chosen for this 4th slot receives an **additional Ultimate bonus** (increased power, extra effects, or expanded functionality appropriate to that ability; exact transformation rules undecided).

### Progression decisions & intended experience
- Creates real run-to-run build tension between acquiring a wider toolkit quickly, specializing heavily into existing abilities, pushing toward unlocking the 4th Ultimate slot, and adapting item builds around rolled synergies.
- Rewards action-roguelike adaptation, aim/kiting skill, and iterative build construction atop a consistent projectile foundation.

### Open technical & design details
- Whether ability/upgrade pools are handcrafted modules, procedurally generated, or hybrid; whether upgrades can modify the basic `LMB` projectile itself; 3 vs. 4 choices per level; minimum level gates / guaranteed new-ability safeguards / upgrade caps; dynamic tooltip & slot reassignment UI; non-obstructive level-up UI during live combat; opponent/ally inspection of the evolving kit; authoritative multiplayer level-up choices; and preventing recursive/overlapping effect loops.

---

## Relationship between Mr. Random and Twin-Stick Shooter Roguelike
- **Mr. Random** receives a complete procedurally generated kit at the beginning of the match and adapts its strategy and itemization to that fixed rolled kit.
- **Twin-Stick Shooter Roguelike** gradually constructs and mutates its kit level-by-level mid-match through curated/randomized choices paired with independent WASD + projectile shooter mechanics.
- Preserve this distinct experiential identity between the two concepts.

---

## Design feedback (non-canonical)

- Luís: orb denial, W/E interaction and escalating R are standouts; avoid overwhelming raw amplification.
- All-Passive Hero: overlap condition creates an interesting achievement state but might become too binary; maintain passive-only gimmick.
- Chibi: distinguish current souls vs lifetime souls; balance ally revival carefully.
- Four-Form: shared cooldowns and predefined sequence are fundamental; clarify complex interactions and form readability.
- Tortuga: shell protection vs aggressive liberation is the core; keeping shell-less form glassier helps meaningful tradeoffs.
- Ogre & Old Man Swordsman: telegraphed club commitments and paired strike timing fit early 1v1 spacing tests directly.
- Airplane Fighter: strong spatial identity; warrants an isolated flight/steering lab before 1v1 tuning.
- Bomb Planter & Chain Laser Shooter: high-personality lane kits whose wave interactions (minion explosions, chain hops, Q multi-kill gold) come alive once lane creeps and walls exist.
- Automatron Bot 3000 & Multi-Phase Raid Boss: bold structural experiments (RTS-style macro delegation and boss-bar life economy) best deferred until core MOBA lane/map systems exist.
- Kobold King: cohesive RTS-micro pet hero where each servant fills a distinct combat job (slow line, ranged stun, piercing armor-shred nuke) and R's succession cheat-death turns servant positioning into a tense escape/bait mechanic; requires multi-unit selection controls beyond single-hero MOBA input.
- Mr. Random & Twin-Stick Shooter Roguelike: complementary roguelike pillars separated cleanly by pre-match full-kit roll (Mr. Random) vs. level-by-level acquisition + WASD/cursor control (Twin-Stick); our context-aware control profiles and data-driven ability cards are designed to accommodate both without rewriting shared input systems.
- Mr. Random: high-replayability roguelike-style kit concept; needs strict per-slot power-budget constraints and readable auto-generated tooltips/telegraphs so opponents aren't blindsided by illegible spells.
