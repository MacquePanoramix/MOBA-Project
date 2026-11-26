# Ultimate Free MOBA — Major Development Update

We have completed an extensive collaborative game-design session while your usage allowance was limited. I would now like you to implement the accumulated decisions described below.

This update includes:

1. Hero Guide Readability.
2. Interactive scaling previews and visual ability demonstrations.
3. A new shared hero resource system.
4. Mana implementation for Miravel and Chain Laser.
5. An expanded, recipe-based item system.
6. Six custom-designed advanced items.
7. Twenty-one custom-designed components.
8. A redesign of the remaining temporary item catalogue.
9. Extensive automated testing, balancing, documentation and release preparation.

Please read this entire specification before beginning implementation.

---

# PART 1 — GENERAL DESIGN PHILOSOPHY

## 1.1 Game identity

Continue respecting our established vision for the MOBA:

- Slower-paced, more deliberate gameplay, drawing inspiration from early Dota 2.
- Meaningful positioning, movement, attack commitment and recovery animations.
- An emphasis on skill-based abilities and relatively limited crowd-control durations.
- Distinctive heroes with fundamentally different gameplay identities.
- Creative and unconventional item builds.
- Items primarily providing passive effects rather than active abilities.
- Attributes and stats that interact with individual mechanics in deliberately designed ways.

Do not homogenize heroes or items simply to make balancing easier.

## 1.2 Attributes

Preserve our established three attributes:

- Strength (STR).
- Agility (AGI).
- Intelligence (INT).

Attributes start at zero and have no intrinsic universal gameplay benefits.

Their effects arise through explicitly designed hero abilities, item passives and other mechanics.

For example, Strength must not automatically grant Health to every hero, and Intelligence must not automatically grant Mana.

## 1.3 Scaling philosophy

Continue using deliberately selected scaling curves.

- Linear: proportional growth.
- Bounded diminishing returns: progressively smaller gains approaching an upper limit.
- Unbounded diminishing returns: progressively smaller gains without a fixed upper limit, often using square-root scaling.
- Inverse: appropriate for mechanics such as cooldown reduction.

Preserve existing approved hero scaling relationships and global Attack Speed and Ability Haste formulas.

Do not silently replace approved scaling types while balancing.

Numerical coefficients, base values, caps, durations and costs remain adjustable unless explicitly specified as a qualitative design requirement.

## 1.4 Item design philosophy

Our item design process is now:

1. Establish the item's fantasy.
2. Identify mechanics that express that fantasy.
3. Deliberately select scaling relationships.
4. Evaluate all potential stats independently.
5. Design appropriate components and recipes.
6. Balance numerical values.

An item granting an attribute and an item scaling with that attribute are separate design decisions.

For example, Draconic Blade scales with Intelligence but does not grant Intelligence.

Do not automatically give an item every stat its passive scales with.

Components should generally have versatile fantasies and multiple potential upgrade paths. Specialized components are acceptable when justified, but should not dominate the catalogue.

Recipes may contain multiple copies of the same component.

Passive effects from components may:

- Transfer unchanged to advanced items.
- Evolve into thematically related, stronger effects.
- Be replaced where explicitly specified by the design.

Avoid accidentally counting both the component version and its inherited advanced version as separate effects.

---

# PART 2 — HERO GUIDE READABILITY PATCH

This is the previously discussed v0.2.10 Hero Guide Readability Patch, which has not yet been implemented.

The existing scaling guide is too difficult for players to read. It currently feels more like a developer mathematics panel than a usable hero guide.

Redesign it for all four implemented heroes.

This stage must not change gameplay or balance.

## 2.1 Ability presentation

Every ability should have a clearly structured presentation containing:

### A. Plain-language description

Explain what the ability actually does using natural, accessible language.

A player should be able to understand the ability without needing to interpret mathematical formulas.

### B. Important gameplay information

Prominently display relevant information, such as:

- Damage.
- Healing.
- Cooldown.
- Duration.
- Range.
- Radius.
- Crowd-control effects.
- Resource cost, once the resource system is implemented.
- Relevant ability restrictions.

Do not unnecessarily display properties that do not apply to the ability.

### C. What scales?

Create a clearly separated scaling section.

Display each relevant stat and the specific property it influences.

For example:

- STR → Outer shockwave width.
- STR → Central stun duration.
- AGI → Wind-up speed.
- Attack Damage → Damage dealt.
- Attack Speed → Recovery speed.

Do not combine several mathematical expressions into one difficult-to-read paragraph.

Explicitly explain whether a stat affects damage, geometry, duration, frequency, movement, recovery or another property.

### D. Detailed mathematics

Provide an expandable section labelled something like "Show Math".

It should contain:

- Exact implemented formulas.
- Base values.
- Scaling coefficients.
- Rank-dependent values.
- Scaling curve types.
- Relevant limits and caps.

The formulas must correspond to the real implementation, not outdated documentation.

Where possible, obtain both gameplay values and guide information from shared data definitions to prevent inconsistencies.

### E. Practical ability tip

Include short, useful gameplay advice.

For example, for Ogre's Q:

"Aim the center of your slam at enemies you want to stun. Use the wider shockwave to hit multiple enemies."

Tips should explain actual gameplay interactions rather than repeat the ability description.

## 2.2 Accessibility and layout

Improve:

- Typography.
- Spacing.
- Text hierarchy.
- Contrast.
- Separation between ability sections.
- Consistent visual representation of stats.
- Scrolling and expandable content.
- Readability at different window sizes.

The interface should be easy to understand for new players, including players who struggle with dense text.

Avoid excessive visual clutter and giant mathematical paragraphs.

Apply the same presentation principles to all four heroes.

---

# PART 3 — INTERACTIVE HERO GUIDE AND VISUAL DEMONSTRATIONS

Implement the previously discussed follow-up to the readability patch.

## 3.1 Visual ability previews

Allow players to view short, looping demonstrations of abilities by hovering over an ability or interacting with its guide entry.

The intended experience resembles the ability demonstration systems found in games such as Dota 2 and League of Legends.

However, the demonstrations should use our actual game's visual style, abilities and simulation behaviour.

Prefer reusable, isolated, scripted in-engine demonstrations over externally recorded videos.

Each demonstration should communicate:

- What the ability does.
- Its important geometry.
- Its timing.
- Its interaction with targets.
- How a player might use it effectively.

Where necessary, provide more than one example for complicated abilities.

Examples:

- Ogre Q: Demonstrate the central stun zone and outer damage-only shockwave.
- Ogre W: Demonstrate knockback.
- Chain Laser passive: Demonstrate attacks bouncing between multiple targets.
- Chain Laser R: Demonstrate the sustained laser and chained attacks.
- Old Man Swordsman passive: Demonstrate the distinct first and second sword strikes.
- Miravel: Demonstrate healing, curses and the effects of her ultimate.

Cover all four currently implemented heroes.

## 3.2 Preview isolation

Ability demonstrations must:

- Run independently of live multiplayer simulation.
- Never affect actual hero positions, cooldowns, resources, health or combat state.
- Never send gameplay commands into an active multiplayer session.
- Reset predictably when looping.
- Stop or release unnecessary resources when no longer visible.

Provide an option to disable animated demonstrations.

This setting should persist locally and must not affect another player's settings.

## 3.3 Interactive scaling preview

Allow players to change relevant stats within a hypothetical preview.

Depending on the ability, examples include:

- Strength.
- Agility.
- Intelligence.
- Attack Damage.
- Attack Speed.
- Ability Haste.
- Heal Power.
- Maximum Health.
- Other relevant stats.

Only display controls appropriate for the selected ability.

The guide should update its displayed values using the actual gameplay scaling formulas.

Where practical, the demonstration itself should reflect modified properties, including:

- Radius and width.
- Damage.
- Duration.
- Attack timing.
- Movement.
- Projectile speed.
- Cooldown.
- Healing.

Players should be able to investigate how a build might affect their hero without entering a real match.

Preview values must never modify the actual player's stats.

Provide sensible defaults and a reset option.

After the item system is implemented, ensure relevant new item effects and resource-related properties can also be described clearly in the guide.

---

# PART 4 — SHARED HERO RESOURCE SYSTEM

We realized that none of the implemented heroes currently use Mana or any alternative resource.

We have now deliberately designed the first stage of our resource system.

## 4.1 Resource philosophy

Do not impose Mana on every hero.

We want a versatile resource system resembling the flexibility of League of Legends, with room for more creativity.

Potential future resource families include:

- Mana.
- Energy.
- Rage.
- Heat.
- Focus.
- Ammunition.
- Other shared or exceptional systems.
- Resourceless heroes.

However, do not implement unused resource systems merely because they appear in this list.

We will design them when appropriate heroes require them.

Prefer shared resource families that multiple heroes can use, with individual variations where justified.

Unique resources should be reserved for heroes whose identities genuinely require them.

Energy, for example, might be especially appropriate for future heroes who perform ability combos followed by short recovery periods.

Mana is more appropriate where we want ability spam to have longer-term consequences, including during laning.

Resourceless heroes are an intentional design choice.

## 4.2 New conventional stats

Introduce the following three stats.

### Maximum Mana

Increases the maximum available Mana pool.

Scaling type: Linear.

### Mana Regeneration

Increases flat Mana recovery per second.

Scaling type: Linear.

Natural Mana regeneration remains active both inside and outside combat.

There is no inherent combat regeneration penalty.

### Resource Cost Reduction

Reduces resource costs through a percentage benefit.

Scaling type: Bounded diminishing returns.

The maximum possible reduction must remain below 100%.

Stacking the stat must not make abilities entirely free.

Design this stat so future appropriate resource systems can use it without requiring major architectural changes.

However, do not automatically apply it to every conceivable resource mechanic. Future resource systems may define their own compatibility.

It has no effect on resourceless heroes.

## 4.3 Supported Mana regeneration methods

Support:

1. Flat Mana regeneration, which is the standard.
2. Percentage-of-maximum-Mana regeneration, available through specialized hero abilities or item effects.

Maximum Mana does not inherently increase standard flat Mana regeneration.

These are separate build investments.

Mana regeneration should generally be gradual enough to make repeated ability usage a meaningful long-term decision.

## 4.4 Supported Mana consumption methods

Support the following mechanisms:

1. Flat Mana costs — the standard.
2. Costs based on a percentage of maximum Mana.
3. Costs based on a percentage of current Mana.
4. Continuous Mana consumption throughout an ability's duration.

Only implement these where appropriate.

Apply Resource Cost Reduction to applicable consumption amounts or rates.

Prevent negative Mana, invalid casting and other resource exploits.

For normal upfront-cost abilities:

- Validate affordability before accepting the cast.
- Consume the resource exactly once.
- Integrate with the existing ability commitment and interruption rules.
- Do not charge for rejected or invalid commands.

For continuous consumption:

- Integrate consumption with simulation time.
- Allow natural regeneration during the channel.
- End the ability appropriately when its resource requirements can no longer be sustained.
- Prevent negative resource values.

Document any technical decisions concerning interruption, rounding, changes to maximum Mana and resource replenishment on respawn or at base.

Those detailed rules have not all been explicitly finalized.

## 4.5 Resource presentation

Mana-using heroes should receive:

- A clearly visible Mana bar.
- Current and maximum Mana values.
- Mana regeneration information.
- Ability cost indicators.
- Clear insufficient-Mana feedback.
- Appropriate resource information in the hero guide and relevant tooltips.

Resourceless heroes should not display a meaningless Mana bar.

Integrate the resource system correctly with the existing authoritative simulation, multiplayer synchronization, bots, UI and testing infrastructure.

Do not allow the client to bypass affordability restrictions.

---

# PART 5 — HERO RESOURCE ASSIGNMENTS

Our approved resource assignments are:

- Miravel: Mana.
- Old Man Swordsman: Resourceless.
- Chain Laser: Mana.
- Ogre: Resourceless.

## 5.1 Miravel

Mana fits Miravel's combination of deliberate magical casting and supernatural healing.

Her inherent supernatural properties remain independent of Mana.

### Passive

No Mana cost.

Her healing and enemy vitality-draining aura continues functioning even when she has no Mana.

### Q

No Mana cost.

Important: Q activates automatically. Do not cause it to consume resources without the player's deliberate control.

Preserve its existing automatic pulse behaviour.

### W

Flat Mana cost.

Mana cost increases with ability rank.

### E

Flat Mana cost.

Mana cost increases with ability rank.

Extending an existing curse through its intended kit interactions must not consume additional Mana.

Only casting the curse itself consumes Mana.

### R

Continuous Mana consumption while channeling.

The Mana consumption rate per second increases with ability rank.

The consumption rate is independent of Intelligence.

Preserve Intelligence's existing duration scaling.

Consequently, an Intelligence-focused Miravel may be able to sustain the ultimate for longer, but needs sufficient Mana to realize the full potential duration.

If Mana runs out, the channel ends early.

Do not introduce an additional upfront cost unless we explicitly decide to do so later.

### Numerical balancing

Determine appropriate base Mana, Mana Regeneration and ability costs through testing.

Consider both laning sustainability and meaningful decisions during team combat.

Preserve her existing identity and scaling relationships.

## 5.2 Old Man Swordsman

Remain resourceless.

Do not add Mana, Energy or another mandatory consumable resource.

His dual-sword mechanic, attack commitment, wind-up, recovery and interruption risks already provide meaningful limitations.

Preserve all existing ability mechanics and scaling relationships.

## 5.3 Chain Laser

Use Mana.

We considered Energy and Heat but rejected them for this hero.

Heat would alter its fundamental identity too much.

Energy would potentially allow excessive sustained lane pressure because it could replenish too quickly between poke attempts.

Chain Laser already possesses reliable sustained offensive pressure through its basic attacks. Mana should impose meaningful long-term consequences for excessive ability usage.

### Passive

Chain attacks remain entirely Mana-free.

Running out of Mana must not remove the hero's fundamental ADC gameplay.

### Q

Flat Mana cost that increases with rank.

Relatively demanding cost compared to the other basic abilities.

Repeated poking should have meaningful consequences for lane sustainability.

Intelligence continues affecting laser speed without increasing Mana cost.

### W

Flat Mana cost that increases with rank.

Less Mana-demanding than Q.

Mana cost is independent of the number of enemies affected.

Preserve its overlapping shockwave and positioning mechanics.

### E

Low, flat Mana cost.

The cost is constant across ranks.

Its availability should primarily be constrained by its cooldown, but completely exhausting Mana through offensive abilities should risk losing access to this escape.

### R

Substantial upfront Mana cost.

The cost is constant across ranks.

Do not continuously consume Mana while firing.

Intelligence and Attack Speed investments continue affecting its existing properties without introducing additional Mana consumption.

### Numerical balancing

Balance Mana reserves, regeneration and costs around its ADC identity.

Do not make its poke essentially unlimited.

Do not make its basic combat functionality dependent on Mana.

## 5.4 Ogre

Remain resourceless.

The Ogre cannot perform basic attacks. Its abilities are its fundamental means of fighting.

Adding a mandatory consumable resource could leave it completely helpless.

Its deliberate wind-up and recovery mechanics already provide natural limitations.

Preserve the existing kit.

---

# PART 6 — RECIPE-BASED ITEM SYSTEM

We have designed a substantial permanent foundation for the future item catalogue.

The system needs to support:

- Standalone basic components.
- Standalone specialized components.
- Intermediate components.
- Advanced items.
- Multi-level recipe trees.
- Components with multiple upgrade destinations.
- Recipes requiring multiple copies of identical items.
- Components granting passive effects.
- Passive inheritance and thematic evolution.
- Items containing multiple distinct passives.

Where practical, support purchasing an advanced item by consuming already-owned required components and paying the remaining cost.

Ensure recipe accounting correctly handles duplicates and nested components.

For example, buying Heartstone requires two Life Crystals, and Giant's Heart directly requires two Titanstones.

Do not accidentally consume unrelated inventory items.

The shop should communicate:

- Item stats.
- Passive descriptions.
- Scaling relationships.
- Recipe components.
- Component quantities.
- Upgrade destinations.
- Total cost.
- Remaining cost when the player already owns required components.

Ensure recipe purchasing and selling cannot create unintended gold-generation exploits.

Use stable internal item identifiers and migrate renamed or replaced definitions safely.

The following designs are approved. Do not replace them with alternative fantasies or fundamentally different mechanics.

All exact numerical stat amounts, passive coefficients, costs, durations and cooldowns are provisional and should be carefully balanced.

---

# PART 7 — APPROVED COMPONENT CATALOGUE

There are 21 approved components.

## 7.1 Small Axe

Fantasy: A simple weapon embodying raw destructive force.

Stats:
- Attack Damage only.

Passive: None.

Recipe: Standalone.

## 7.2 Heavy Axe

Fantasy: A substantial weapon emphasizing raw offensive power.

Stats:
- Attack Damage only.

Passive: None.

Recipe:
- 2 × Small Axe.

The axe progression should emphasize larger Attack Damage investments than the sword progression.

## 7.3 Short Sword

Fantasy: A conventional, practical sword.

Stats:
- Attack Damage only.

Passive: None.

Recipe: Standalone.

## 7.4 Steel Sword

Fantasy: A well-crafted conventional steel weapon.

Stats:
- Attack Damage only.

Passive: None.

Recipe:
- Short Sword.

Steel Sword should be more affordable and provide less Attack Damage than Heavy Axe.

## 7.5 Titanstone

Previously called Weighted Gauntlet.

Fantasy: An exceptionally dense ancient mineral embodying physical might.

Stats:
- Strength only.

Passive: None.

Recipe: Standalone.

Use Titanstone in all recipes that previously referenced Weighted Gauntlet.

## 7.6 Windfeather

Fantasy: A delicate feather embodying nimbleness and physical finesse.

Stats:
- Agility only.

Passive: None.

Recipe: Standalone.

## 7.7 Sage's Eye

Fantasy: A luminous gemstone embodying wisdom, ingenuity, understanding and mental clarity.

Stats:
- Intelligence only.

Passive: None.

Recipe: Standalone.

Intelligence should not be interpreted exclusively as magical power.

## 7.8 Life Crystal

Fantasy: Crystallized life energy.

Stats:
- Health only.

Passive: None.

Recipe: Standalone.

## 7.9 Heartstone

Fantasy: A large reddish mineral with a gently pulsating core containing concentrated vitality.

Stats:
- Substantial Health.

Passive: None.

The pulsation is a visual expression of stored vitality, not an additional healing mechanic.

Recipe:
- 2 × Life Crystal.

Heartstone should remain versatile enough to serve numerous future advanced items.

## 7.10 Bloodstone

Stats:
- Health.

Passive:
- Increases damage dealt to wounded enemies.

Scaling:
- Damage amplification increases linearly with the target's missing Health percentage.
- No attribute scaling.

Recipe:
- Life Crystal.

Bloodstone's passive is the precursor to Executioner's Axe's Final Judgment.

## 7.11 Wooden Shield

Stats:
- Armor only.

Passive: None.

Recipe: Standalone.

## 7.12 Silver Amulet

Stats:
- Magic Resistance only.

Passive: None.

Recipe: Standalone.

## 7.13 Reinforced Shield

Stats:
- Armor.
- Magic Resistance.

Passive: None.

Recipe:
- 2 × Wooden Shield.
- Silver Amulet.

Maintain a lower Armor-to-Magic-Resistance ratio than Dragon Scale.

## 7.14 Spring Water

Stats:
- Health Regeneration only.

Passive: None.

Recipe: Standalone.

## 7.15 Sacred Pearl

Stats:
- Heal Power only.

Passive: None.

Recipe: Standalone.

## 7.16 Healing Elixir

Stats:
- Health Regeneration.
- Heal Power.

Passive — Lingering Restoration:

A percentage of actual healing received is gradually repeated over the following few seconds.

Rules:

- Linear scaling with the actual amount of Health restored.
- No attribute scaling.
- Overhealing does not create stored restoration.
- Healing generated by Lingering Restoration must not recursively trigger itself.

Recipe:
- Spring Water.
- Sacred Pearl.

The exact repeated-healing percentage and duration require balancing.

Explicitly test interactions with natural Health Regeneration, incoming healing amplification and Heal Power.

Some technical details regarding these interactions remain open. Choose a coherent implementation that does not create recursive healing or accidental unintended double amplification, and document the decision.

## 7.17 Dragon Scale

Fantasy: A rare scale embodying draconic toughness and adaptive protection.

Stats:
- Armor.
- Magic Resistance.
- Strength.

Armor is more prominent than Magic Resistance.

Its Armor-to-Magic-Resistance ratio is higher than Reinforced Shield's.

Passive — Adaptive Scales:

Successive direct damaging hits received gradually build additional Armor and Magic Resistance specifically against the respective attacker.

Rules:

- Track defensive stacks separately for different attackers.
- Physical and magical direct damaging hits qualify.
- Repeated damage-over-time ticks do not qualify.
- Successive qualifying hits refresh the duration of accumulated stacks.
- Once qualifying hits stop, stacks gradually disappear one at a time.
- Resistance benefits apply against the respective attacker, not universally against all enemies.

Scaling:

- Defensive stack count → resistance bonus: bounded diminishing returns.
- Strength → maximum resistance bonus: bounded diminishing returns.

Recipe: Standalone specialized component.

It should require a relatively meaningful single gold investment.

## 7.18 Primordial Blood

Previously called Dragon Blood.

Fantasy: Blood containing the concentrated life essence and physical might of ancient creatures.

The broader name allows it to serve different final items, including draconic and giant-themed artifacts.

Stats:
- Strength.
- Health Regeneration.

Passive — Primordial Vitality:

Natural Health Regeneration increases as the wielder's Health decreases.

Scaling:

- Missing Health percentage → regeneration increase: linear.
- Independent of attributes.
- Does not directly amplify healing received from other sources.

Recipe: Standalone specialized component.

## 7.19 Iron Dagger

Fantasy: A simple, lightweight iron blade designed for quick and practical attacks.

Stats:
- Attack Speed only.

Passive: None.

Recipe: Standalone.

Do not give it Attack Damage or Agility.

## 7.20 Nimble Dagger

Fantasy: A refined, lightweight and finely balanced dagger embodying quickness and finesse.

Stats:
- Attack Speed.
- Agility.

Passive — First Strike:

The first basic attack following a fixed period without basic attacking has a shorter wind-up.

Rules:

- The passive can become available again during combat.
- It does not require being out of combat.
- The inactivity requirement concerns basic attacking, not all combat actions.
- The inactivity duration remains fixed.
- The passive modifies the opening attack's wind-up, not its entire attack cycle.
- Preserve a meaningful minimum wind-up.

Scaling:

- Agility → wind-up reduction: bounded diminishing returns.

Recipe:
- Iron Dagger.
- Windfeather.

Do not grant direct Attack Damage or Movement Speed.

Handle cancelled attacks carefully so players cannot repeatedly exploit the shortened wind-up. Document the chosen commitment/reset rule.

## 7.21 Winged Charm

Fantasy: A small talisman shaped like outstretched wings, embodying freedom and swiftness of movement.

Stats:
- Movement Speed only.

Passive — Weightlessness:

Incoming slows have their strength reduced by a fixed percentage.

Rules:

- Does not reduce slow duration.
- Does not reduce stun duration.
- Does not provide general crowd-control resistance.
- Does not provide immunity to slows.

Scaling: Fixed, independent of all stats and attributes.

Recipe: Standalone.

---

# PART 8 — APPROVED ADVANCED ITEMS

Implement the following six custom-designed advanced items.

Do not add extra stats to these items merely because they would be useful or improve their numerical efficiency.

We have individually reviewed all existing and newly introduced resource-related stats for these designs.

None of the six should grant Maximum Mana, Mana Regeneration or Resource Cost Reduction.

---

## 8.1 Executioner's Axe

### Fantasy

A powerful executioner's weapon embodying raw destructive power and finishing wounded enemies.

### Stats

- Attack Damage.
- Strength.
- Health.

No other direct stats.

### Passive — Final Judgment

Amplifies damage dealt to wounded enemy heroes.

The lower the target's remaining Health, the greater the amplification.

This applies to outgoing damage generally, rather than exclusively basic attacks or physical damage.

Scaling:

- Target missing Health percentage → damage amplification: linear.
- Strength → maximum amplification: bounded diminishing returns.

The passive evolves from Bloodstone's effect.

Executioner's Axe should have a higher base maximum amplification than Bloodstone, alongside its additional Strength scaling.

Do not apply both Bloodstone's passive and Final Judgment separately after completing the item.

### Recipe

- Heavy Axe.
- Titanstone.
- Bloodstone.

### Identity

Rewards finishing wounded enemies and creative offensive Strength investments.

---

## 8.2 The Coward's Shield

### Fantasy

A shield embodying survival instincts, nimbleness and protection while escaping danger.

### Stats

- Armor.
- Magic Resistance.
- Agility.

No direct Movement Speed bonus or other stats.

### Passive — Flight Reflex

The wielder gradually gains additional resistances while genuinely retreating from a recent attacker.

Rules:

- Requires sustained movement away from the attacker.
- Does not grant the full benefit immediately.
- Includes a brief grace period for stops, interruptions and obstacles.
- Resistances gradually decay when retreating stops.
- Decay is faster when actively pursuing the enemy.
- Small movement reversals must not exploit the buildup system.
- External displacement should not count as intentional retreat.

Scaling:

- Agility → resistance bonus: linear.
- Agility does not modify the buildup timing.

Much of this passive was already implemented in the Scaling Patch.

Preserve the approved behaviour. Rebalance only where necessary for the new economy and item catalogue.

### Recipe

- Reinforced Shield.
- Windfeather.

### Identity

Rewards disengagement and survival through intelligent movement rather than standing in combat.

---

## 8.3 Chalice of Life

### Fantasy

An enchanted vessel containing extraordinary restorative life energy.

### Stats

- Health.
- Health Regeneration.
- Heal Power.

No Mana-related stats.

The item is specifically about Health and healing.

### Passive 1 — Fountain of Life

Amplifies all incoming healing received by the wielder.

The amplification increases as the wielder becomes wounded.

This includes:

- Natural Health Regeneration.
- Healing from the wielder's own abilities.
- Healing from allies.
- Other valid incoming healing sources.

It does not directly amplify healing given to other heroes through this passive.

Heal Power retains its normal outgoing-healing functionality separately.

Scaling:

- Wielder's missing Health percentage → healing amplification: linear.
- Heal Power → maximum healing amplification: bounded diminishing returns.

### Passive 2 — Lingering Restoration

Inherited unchanged from Healing Elixir.

A percentage of actual healing received is gradually repeated.

Preserve Healing Elixir's approved rules and scaling.

Avoid recursive restoration and unintended repeated amplification.

### Recipe

- Life Crystal.
- Healing Elixir.

### Identity

A dedicated restoration-focused item that rewards healing builds, self-sustain and cooperation with healing allies.

---

## 8.4 Draconic Blade

### Fantasy

A dark steel blade infused with draconic essence. Its surface contains glowing molten cracks and extraordinary dragon scales.

### Stats

- Attack Damage.
- Strength.
- Armor.
- Magic Resistance.
- Health Regeneration.

Important relative stat considerations:

- Draconic Blade should grant more Strength than Executioner's Axe.
- Armor is more prominent than Magic Resistance.
- Preserve the Armor-focused ratio of Dragon Scale.
- No direct Health.
- No Intelligence, despite its Intelligence-scaling passives.
- No other direct stats.

### Shared offensive mechanic — Heat stacks

Dragonfire and Molten Edge share the same Heat stacks on a target.

Rules:

- Basic attacks and abilities explicitly applying on-hit effects generate Heat.
- Ordinary physical ability damage does not automatically qualify.
- Dragonfire's damage-over-time ticks do not generate further Heat.
- Successive qualifying hits refresh the accumulated Heat stacks.
- Once qualifying hits stop, Heat gradually dissipates one stack at a time.

### Passive 1 — Dragonfire

Heat causes the target to suffer magical burning damage over time.

Scaling:

- Heat stacks → burning damage: bounded diminishing returns.
- Intelligence → burning damage: linear.

Intelligence should increase the burning effect's damage potential without turning individual Heat stacks into an unrestricted linear accumulation.

Damage type: Magical.

### Passive 2 — Molten Edge

Heat progressively reduces the target's Armor and Magic Resistance.

Scaling:

- Heat stacks → resistance reduction: bounded diminishing returns.
- Intelligence → maximum resistance reduction: bounded diminishing returns.

This shares Heat with Dragonfire rather than creating an additional independent stacking mechanic.

### Passive 3 — Adaptive Scales

Inherited unchanged from Dragon Scale.

Successive direct hits received build additional resistances against their respective attackers.

Preserve all approved trigger, refresh, decay and Strength-scaling rules.

### Passive 4 — Primordial Vitality

Inherited unchanged from Primordial Blood.

Natural Health Regeneration increases linearly with missing Health percentage.

No attribute scaling.

### Recipe

- Steel Sword.
- Dragon Scale.
- Primordial Blood.

### Identity

Rewards prolonged combat.

The wielder becomes increasingly threatening through offensive Heat buildup while also adapting defensively to attackers.

Strength enhances its defensive properties.

Intelligence enhances its offensive draconic magic, despite not being directly granted by the item.

Do not force the Ogre's ordinary ability hits to trigger weapon on-hit effects simply to make the item universally effective.

---

## 8.5 The Giant's Heart

### Fantasy

A massive petrified heart taken from an ancient mountain giant.

Amber light runs through its stone-like cracks, and despite centuries having passed, it continues to beat.

It embodies extraordinary vitality, physical might, endurance and immense weight.

### Stats

- Substantial Health.
- Substantial Strength.
- Modest Health Regeneration.

No direct Attack Damage.

Its Attack Damage contribution must come exclusively through Giant's Might.

No Armor or Magic Resistance.

The item's identity is enduring wounds rather than preventing them.

No other direct stats.

### Passive 1 — Ancient Vitality

A portion of incoming damage is received gradually rather than immediately.

For example, an incoming damage instance might deal part of its damage immediately and distribute the remaining portion evenly over the following seconds.

That example is conceptual; numerical percentages are not finalized.

Rules:

- Applies to physical, magical and true damage.
- Delaying damage does not inherently reduce its total amount.
- True damage remains true damage in the sense that the deferred portion cannot be mitigated again.
- Previously mitigated damage must not receive a second application of Armor or Magic Resistance when paid later.
- Delayed damage must not recursively become delayed again.
- Distribute each delayed damage instance evenly throughout its duration.
- Every incoming hit creates its own independent delayed damage instance.
- Different delayed damage instances may overlap.
- Receiving another hit never refreshes or postpones previously accumulated delayed damage.

Scaling:

- Maximum Health → proportion of damage delayed: bounded diminishing returns.
- Strength → duration over which the delayed damage is received: bounded diminishing returns.

Ensure the maximum delay proportion and duration remain meaningfully bounded.

Preserve the principle that the wielder eventually suffers the deferred damage unless other legitimate gameplay mechanics allow them to recover Health.

Do not clear deferred damage simply because combat ends or the item is sold.

Implement coherent rules for damage attribution, death, shields and damage-event interactions. These edge cases were not all explicitly finalized; document the chosen rules and prevent double mitigation, infinite deferral and recursive proc exploitation.

### Passive 2 — Giant's Might

Grants Attack Damage based on maximum Health.

Scaling:

- Maximum Health → additional Attack Damage: linear.

This is an actual Attack Damage stat contribution, not a separate on-hit damage instance or universal physical damage multiplier.

It must interact naturally with all existing Attack Damage scalings, including ability-based heroes such as the Ogre.

### Passive 3 — Immovable Mountain

Reduces forced displacement distance.

Applies to appropriate effects such as:

- Knockbacks.
- Pulls.
- Knock-ups involving physical displacement.

It does not reduce ordinary stun or slow durations.

Scaling:

- Strength → displacement distance reduction: bounded diminishing returns.

Even extremely high Strength should not provide complete displacement immunity through this item.

### Passive 4 — Primordial Vitality

Inherited unchanged from Primordial Blood.

Natural Health Regeneration increases linearly with missing Health percentage.

No attribute scaling.

### Recipe

- Heartstone.
- 2 × Titanstone.
- Primordial Blood.

### Identity

An advanced vitality and Strength item that creates opportunities for unconventional offensive tank builds.

Its passives work together to allow a hero to endure severe wounds, resist forced movement and convert extraordinary vitality into physical power.

---

## 8.6 Banana Blade

### Fantasy

A blade shaped like a banana.

Important: It is not literally made from a banana.

It is an unconventional curved weapon whose design evokes finesse, ingenuity, playful swordsmanship and slippery movement.

Its effectiveness comes from its unusual physical geometry and technique.

### Stats

- Substantial Agility.
- Substantial Intelligence, equal in amount to Agility.
- Moderate Attack Speed.
- Modest Attack Damage.
- Modest Movement Speed.

No other direct stats.

### Passive 1 — Crescent Strike

Basic attacks and abilities explicitly applying on-hit effects damage secondary enemies within a crescent-shaped area behind and around the primary target.

The crescent is oriented relative to the attacker and primary target.

It is not simply a circular area around the target or a standard frontal cone originating from the attacker.

Visualize an unusual curved sword technique that reaches enemies positioned behind and around the target.

Damage type: Physical.

This is an actual physical strike enabled by the blade's unconventional design, not an emitted magical crescent.

Scaling:

- Attack Damage → secondary damage: linear.
- Agility → secondary damage coefficient: bounded diminishing returns.
- Intelligence → crescent area size: bounded diminishing returns.

Agility represents the physical finesse required to perform the technique effectively.

Intelligence represents understanding and mastery of its unconventional geometry.

Do not hit the primary target an additional time through its own crescent.

Prevent crescent damage from recursively generating further crescents or accidentally applying additional on-hit effects.

Make the crescent geometry and affected targets visually understandable.

### Passive 2 — Slippery Trickster

Receiving enemy hero damage grants a sudden burst of Movement Speed that gradually fades.

Rules:

- Triggered by enemy hero damage.
- Minions, monsters and towers do not trigger it.
- Has its own cooldown.
- Speed bonus gradually decays over a fixed duration.
- Duration is independent of attributes.
- Cooldown is fixed and unaffected by Ability Haste.
- Repeated damage during cooldown must not continuously refresh the effect.

Scaling:

- Agility → initial Movement Speed bonus: bounded diminishing returns.

Intelligence does not affect this passive.

### Passive 3 — First Strike

Inherited unchanged from Nimble Dagger.

The first basic attack following a fixed period without basic attacking has a shorter wind-up.

Preserve its Agility scaling and inactivity rules.

### Passive 4 — Weightlessness

Inherited unchanged from Winged Charm.

Reduces incoming slow strength by a fixed percentage without reducing slow duration.

Independent of all stats.

### Recipe

- Steel Sword.
- Sage's Eye.
- Nimble Dagger.
- Winged Charm.

Steel Sword is intentionally selected rather than Short Sword.

The Windfeather requirement is already contained within Nimble Dagger's recipe.

### Identity

A nimble skirmishing weapon that rewards positioning, unconventional attack angles, agility and clever movement.

It should support creative Agility and Intelligence hybrid builds.

Do not turn it into a weapon focused primarily on raw Attack Damage.

---

# PART 9 — REDESIGN THE REMAINING TEMPORARY ITEM CATALOGUE

The six advanced items and 21 components above represent our collaboratively designed permanent foundation.

We have not yet personally designed the entire final item catalogue.

I would nevertheless like you to redesign the remaining temporary items to match the new design philosophy, so that we have a broader and more coherent playable shop.

These additional items remain provisional.

We will eventually review, redesign or replace them collaboratively.

## 9.1 Temporary item principles

For each additional item:

1. Begin with a recognizable fantasy.
2. Give it appropriate stats.
3. Design a passive only when it meaningfully expresses the fantasy.
4. Deliberately select scaling relationships.
5. Reuse existing components wherever appropriate.
6. Balance it as part of the whole catalogue.

Avoid generic stat bundles that have little thematic connection.

Avoid simply recreating League of Legends or Dota items under new names.

Maintain our own identity and encourage unconventional builds.

Do not introduce unnecessary components when existing ones can serve the same purpose.

The existing specialized components are not a reason to give every future advanced item its own exclusive recipe ingredients.

Do not modify the approved custom items to accommodate your temporary ones.

## 9.2 Coverage

Provide reasonable provisional itemization opportunities across our existing stats and playstyles, including:

- Attack Damage.
- Attack Speed.
- Health.
- Health Regeneration.
- Armor.
- Magic Resistance.
- Movement Speed.
- Heal Power.
- Ability Haste.
- Strength.
- Agility.
- Intelligence.
- Maximum Mana.
- Mana Regeneration.
- Resource Cost Reduction.

Do not assume every hero must purchase items intended for their conventional role.

Creative cross-role builds are part of the game's identity.

Introduce Mana-focused components and advanced items where appropriate, with distinct fantasies for magical reserves, magical replenishment and efficient resource consumption.

Do not add Mana-related stats to the six custom-designed advanced items or their approved components.

## 9.3 Starting items

It is acceptable and desirable to maintain some dedicated starting items, conceptually similar to the role starting items occupy in League of Legends.

They should provide interesting early-game choices.

Potential identities include:

- Lane sustain.
- Farming.
- Early aggression.
- Early protection.
- Resource sustainability.

They should fit our game's thematic and mechanical style.

Not every starting item needs to upgrade into an advanced item.

Review existing experimental starting items such as Woodland Salve, Miner's Hatchet and Brawler Buckler.

Previously discussed considerations:

- Woodland Salve's out-of-combat regeneration is appealing, but should not trivialize laning.
- Miner's Hatchet's flat physical damage bonus against minions is an experimental mechanic. We had intentionally retained it for playtesting rather than replacing it with percentage damage.
- Brawler Buckler's flat damage blocking against basic attacks is an interesting counter to frequent, relatively small attacks.
- Vitality Vest's combination of Health and regeneration was reasonable.

You may redesign temporary items to fit the new catalogue, but document what you retain, change or replace and why.

Do not treat these temporary designs as permanently approved.

## 9.4 Forgotten Hourglass

Do not implement the Forgotten Hourglass yet.

We explored its fantasy and a potential Borrowed Time passive, but deliberately shelved the design because we were not fully satisfied with its identity.

Do not assume that its preliminary mechanics are finalized.

---

# PART 10 — ITEM UI AND READABILITY

Apply the same readability philosophy used for the hero guide to the item shop.

Clearly communicate:

- Item fantasy.
- Directly granted stats.
- Individual named passives.
- Passive trigger conditions.
- Scaling relationships.
- Cooldowns and durations.
- Recipe components and quantities.
- Available upgrades.

Where useful, provide expandable exact mathematical details.

For example, Banana Blade should clearly distinguish:

- Attack Damage increases Crescent Strike damage.
- Agility increases its damage coefficient.
- Intelligence increases the crescent's area.
- Agility increases Slippery Trickster's speed burst.
- Agility improves First Strike's wind-up reduction.
- Weightlessness is fixed and does not scale.

Do not display a dense block of formulas as the primary description.

Make it possible to understand an item's fantasy and gameplay without studying its mathematics.

Provide appropriate combat feedback for new mechanics, such as:

- Heat buildup and dissipation.
- Adaptive Scales buildup.
- Deferred damage from Ancient Vitality.
- First Strike readiness.
- Slippery Trickster availability or cooldown.

For deferred damage in particular, consider a readable indication of outstanding delayed damage so players understand why they continue losing Health after an attack.

Use a coherent visual language and avoid overwhelming players with effects or status indicators.

---

# PART 11 — BALANCING PHILOSOPHY

All numerical values for our new item designs and hero Mana systems remain provisional.

You are responsible for establishing a sensible first playable balance baseline.

However, do not change approved mechanics or scaling curve types merely to obtain better simulated win rates.

## 11.1 Economy

Our current prototype has a temporarily accelerated economy.

We eventually intend to have slower, more deliberate gold and experience progression.

For now:

- Balance item prices against the current prototype economy.
- Preserve meaningful relative price differences.
- Maintain sensible component and advanced-item progression.
- Avoid making the first completed advanced item excessively easy to obtain.
- Do not treat current numerical prices as final values for the future slower-paced game.

## 11.2 Relative stat identities

Preserve the qualitative relationships specified in the designs.

Examples:

- Heavy Axe provides more Attack Damage and costs more than Steel Sword.
- Draconic Blade grants more Strength than Executioner's Axe.
- Dragon Scale has a higher Armor-to-Magic-Resistance ratio than Reinforced Shield.
- Banana Blade grants equal Agility and Intelligence.
- Banana Blade's Attack Damage and Movement Speed are secondary.
- Giant's Heart grants substantial Health and Strength, with modest regeneration.
- Chain Laser's Q has a higher Mana cost than W.
- Chain Laser's E has a low cost that does not increase with rank.

## 11.3 Balance methodology

Build on our existing automated balance harness and permanent balancing workflow.

Improve the quality of evidence rather than merely increasing the number of tests.

In particular:

- Compare builds with genuinely comparable actual gold expenditure.
- Report actual gold spent, not merely starting gold.
- Account for any additional bot purchases.
- Control starting levels and other relevant conditions.
- Use multiple scenarios, matchups and build paths.
- Use multiple seeds where randomness exists.
- Perform systematic parameter sweeps when useful.
- Examine early-game and later-game interactions.
- Test sustained combat, burst combat, disengagement and recovery.
- Include lane-focused tests rather than relying entirely on hero-versus-hero combat.

Do not optimize purely for 50% win rates.

Look for meaningful differences in:

- Damage output.
- Survivability.
- Resource sustainability.
- Crowd-control effectiveness.
- Mobility.
- Build diversity.
- Time to kill.
- Lane pressure.
- Counterplay.
- Item completion timings.

Bots have limitations. Treat automated tests as evidence supporting balance decisions, not as proof that the game is balanced for real players.

Human playtesting will remain essential.

---

# PART 12 — REQUIRED TESTING

Please expand the deterministic test suite and balance harness to cover the new systems.

## 12.1 Resource tests

Test:

- Mana regeneration inside and outside combat.
- Maximum Mana changes.
- Flat and percentage-based regeneration.
- Each supported consumption mechanism.
- Resource Cost Reduction scaling.
- Insufficient-Mana casting rejection.
- Invalid or duplicate command handling.
- Miravel's Mana-free passive and Q.
- Miravel's continuous ultimate consumption.
- Miravel's ultimate ending when Mana is exhausted.
- Chain Laser's Mana-free basic attacks.
- Chain Laser's upfront ultimate cost.
- Rank-dependent and rank-independent costs.
- Resourceless hero behaviour.
- Multiplayer resource synchronization.

## 12.2 Recipe and inventory tests

Test:

- Recipes with one component.
- Recipes with multiple distinct components.
- Duplicate component requirements.
- Nested recipes.
- Partial recipe completion.
- Upgrade costs.
- Insufficient gold.
- Inventory capacity.
- Item purchasing and selling.
- Shared component upgrade paths.
- Passive inheritance.
- Renamed item identifiers.
- Gold duplication or recipe accounting exploits.

## 12.3 Passive tests

Test every custom passive independently and in combination.

Important examples:

### Executioner's Axe

- Missing Health scaling.
- Strength scaling.
- Appropriate damage categories.
- No duplicate Bloodstone amplification.

### Coward's Shield

- Retreat buildup.
- Grace period.
- Stationary decay.
- Pursuit decay.
- Reversal and external displacement handling.

### Chalice of Life

- Natural regeneration amplification.
- Allied and self-healing.
- Missing Health scaling.
- Heal Power scaling.
- Lingering Restoration.
- Overhealing.
- Recursive healing prevention.

### Draconic Blade

- Shared Heat stacks.
- Qualifying on-hit effects.
- Nonqualifying damage ticks.
- Refresh and individual-stack decay.
- Magical burn damage.
- Resistance reduction.
- Intelligence scaling.
- Independent defensive stacks against different attackers.

### Giant's Heart

- Physical, magical and true damage deferral.
- Exact damage conservation.
- No double mitigation.
- Even damage distribution.
- Independent overlapping damage instances.
- No recursive deferral.
- Maximum Health scaling.
- Strength scaling.
- Attack Damage conversion.
- Displacement resistance.
- Deferred damage attribution.
- Death and item-sale edge cases.

### Banana Blade

- Crescent geometry and orientation.
- Primary and secondary target handling.
- Physical damage.
- Agility damage scaling.
- Intelligence area scaling.
- On-hit trigger conditions.
- No recursive crescent procs.
- Enemy-hero-only Slippery Trickster triggers.
- Speed decay.
- Fixed cooldown.
- First Strike inactivity and wind-up.
- Cancelled attack behaviour.
- Weightlessness reducing slow strength rather than duration.

## 12.4 Scaling stress tests

Test both realistic and extreme stat investments.

Verify:

- Linear relationships remain linear.
- Bounded diminishing returns approach the intended limits.
- Resource costs cannot reach zero through ordinary stacking.
- Forced displacement resistance does not become complete immunity.
- Cooldowns and wind-ups remain valid.
- Damage and healing never become invalid or negative unintentionally.
- Maximum Health changes update Giant's Might correctly.
- High Attack Speed does not create event recursion or simulation failures.

Evaluate the existing simulation tick-rate limitations where relevant, especially when modifying wind-ups through First Strike.

Do not silently change the approved global Attack Speed formula to compensate for timing quantization.

Document limitations and numerical accuracy.

## 12.5 UI and preview testing

Verify:

- All four hero guides are readable.
- Ability formulas match simulation results.
- Interactive stat previews calculate correctly.
- Demonstrations reset and loop correctly.
- Demonstrations cannot modify active multiplayer state.
- Closing the preview stops unnecessary simulation work.
- Animation settings persist.
- Mana information is accurate.
- Item recipes and passive descriptions match implementation.

---

# PART 13 — OPEN TECHNICAL DECISIONS

Some implementation edge cases remain deliberately unspecified.

Use conservative, coherent and testable defaults, but clearly document your choices.

Examples include:

- Whether multiple copies of an item may stack named passive effects.
- Exact interaction between Lingering Restoration and natural regeneration.
- Incoming healing amplification ordering.
- Exact damage attribution for delayed damage.
- Shield interaction with deferred damage.
- Multiple independent owners applying Heat to the same enemy.
- Whether resistance reduction is represented through flat or percentage values.
- Some passive trigger edge cases involving summons and damage attribution.
- First Strike cancellation and commitment handling.
- Slow stacking and Weightlessness application order.
- Resource replenishment on death, respawn and returning to base.
- Resource pool changes when buying or selling Maximum Mana items.

Avoid inventing major new mechanics to resolve these edge cases.

Where a choice would significantly change an approved fantasy or gameplay identity, flag it for later collaborative review.

---

# PART 14 — IMPLEMENTATION AND RELEASE WORKFLOW

Although this is a single consolidated request, implement it in logical stages.

Suggested sequence:

### Stage A — Hero Guide Readability

Complete the previously planned v0.2.10 readability work.

Verify no gameplay changes.

### Stage B — Interactive Hero Guide

Implement isolated visual demonstrations and interactive stat previews.

Test functionality, isolation and performance.

### Stage C — Hero Resource System

Implement shared resource infrastructure, the three new resource-related stats and the approved hero Mana assignments.

Test before introducing resource-focused items.

### Stage D — Item and Recipe Infrastructure

Implement reliable nested recipes, duplicate components, passive inheritance and catalogue presentation.

### Stage E — Custom Item Catalogue

Implement the 21 approved components and six approved advanced items.

Validate each item mechanically before attempting broad balance adjustments.

### Stage F — Temporary Catalogue Redesign

Create a coherent provisional shop around the approved designs, including Mana itemization and appropriate starting items.

### Stage G — Integrated Balancing and Testing

Conduct automated tests, systematic balance experiments and multiplayer verification.

### Stage H — Documentation and Release

Update the project documentation, item catalogue, hero scaling references, relevant AGENTS.md instructions, testing documentation and release notes.

Choose appropriate version numbers for the staged releases and document them.

Preserve the previously implemented release reliability improvements.

Ensure normal update checks use the static version manifest rather than unnecessary unauthenticated GitHub REST API requests.

Do not advance the published release manifest until its associated build artifacts are available and verified.

Perform Windows build and updater smoke tests before distributing the final playable release.

If possible, retain actual automated test reports and balance results in a reproducible, reviewable form rather than only reporting aggregate success claims.

---

# PART 15 — FINAL IMPLEMENTATION REPORT

Once the implementation is complete, provide a detailed report containing:

1. A summary of implemented stages.
2. Version numbers and relevant commits.
3. The final item catalogue, including temporary items.
4. Full recipes and item prices.
5. Direct stat values for all items.
6. Exact passive formulas and scaling coefficients.
7. Hero Mana pools, regeneration and ability costs.
8. Any technical decisions made for previously unspecified edge cases.
9. Automated test results and supporting reports.
10. Balance experiments and resulting adjustments.
11. Remaining limitations or known issues.
12. Multiplayer and release verification results.
13. Instructions for obtaining and testing the updated build.

Clearly distinguish approved custom designs from provisional designs you created independently.

Do not claim that tests passed unless they were actually executed.

Do not hide failed tests or unresolved balance problems.

The objective is not simply to add content. It is to establish a coherent, extensible and reasonably balanced foundation for our next round of collaborative design and human playtesting.

Please proceed systematically, preserving our existing hero identities, custom mechanics and long-term design philosophy throughout.

