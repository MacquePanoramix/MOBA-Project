# Five Hero Concepts — User-Authored Mechanics

These are **faithful working transcriptions** of the designer's ideas, cleaned for readability but with numbers, edge cases and balances left undefined. Where commentary adds a possible interpretation, it is explicitly labeled. No change here should be treated as an approved redesign.

---

## 1. Hero Luís

### Passive
- Increase allied health regeneration in an aura.
- Decrease enemy health regeneration in an aura.
- Participating in a hero takedown permanently increases healing power slightly.

### Q (passive)
- Periodically spawns an orb in the world.
- Luís can pick it up; pickup automatically causes a Necrophos-Q-like pulse: heal nearby allies including himself slightly, and deal some damage to nearby enemies.
- Enemies can step on the orb to deny/remove it.
- The orb expires after some time.

### W
- Become invisible for a short time and leave behind a temporary illusion.

### E
- For a short duration, when Luís or any of his illusions takes damage from an enemy hero, curse that attacker.
- The curse burns over time; each time the cursed enemy uses an ability, its damage/power and duration increase.

### R
- Channel a nearby area over time.
- Enemies inside accumulate progressively stronger slowing and increased damage taken.
- Allies progressively gain increased healing received/effectiveness.
- Continue until full duration or channel interrupted.

**Unresolved:** orb placement, orb pickup by others, whether multiple curses stack, illusion behavior, exact channel constraints, and all numbers.

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

## Design feedback (non-canonical)

- Luís: orb denial, W/E interaction and escalating R are standouts; avoid overwhelming raw amplification.
- All-Passive Hero: overlap condition creates an interesting achievement state but might become too binary; maintain passive-only gimmick.
- Chibi: distinguish current souls vs lifetime souls; balance ally revival carefully.
- Four-Form: shared cooldowns and predefined sequence are fundamental; clarify complex interactions and form readability.
- Tortuga: shell protection vs aggressive liberation is the core; keeping shell-less form glassier helps meaningful tradeoffs.
