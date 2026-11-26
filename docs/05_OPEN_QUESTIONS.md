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

## Hero rules & v0.2.8–v0.3.0 Open Policies

- **Part 13 Item & Resource Technical Defaults (`v0.3.0`, see [`docs/11_ITEMS_AND_RESOURCES.md`](11_ITEMS_AND_RESOURCES.md#L6-part-13-conservative-technical-decisions-open-for-designer-review)):**
  - Unique named passive normalization vs. duplicate item stacking (`Final Judgment` supersedes `Wounded Strike`; `Astral Surge` supersedes `Wellspring`).
  - `Lingering Restoration` excluded from triggering itself and from `Fountain of Life` amplification.
  - `Ancient Vitality` shield-first order, unmitigated non-deferred even payout, sale/out-of-combat persistence, death reset, and original-attacker attribution.
  - Shared per-attacker `Heat` (`Dragonfire` + flat `Molten Edge` shred vs. percentage shred) and per-attacker `Adaptive Scales` (excluding DoTs).
  - `First Strike` windup-start consumption & 30 Hz discrete tick quantization around `0.065s–0.100s` floor vs. preserving approved global AS curve.
  - `Weightlessness` multiplicative `-25%` slow-strength reduction preserving full durations, plus exact slow-stacking interaction order.
  - **Forgotten Hourglass:** Intentionally **shelved** (legacy ID `19` retired) until Luís finalizes its fantasy & Borrowed Time identity.
  - **Provisional `17` Shop Items & Mana Economy Numbers:** All item costs, stat budgets, `304/311–315/401–407` provisional items, and Miravel/Laser Mana pools, regen, and RCR curve parameters (`0.65 × RCR / (RCR + 65)`) remain open for collaborative review and human playtest tuning.
- **Kindred Censer (formerly Charm) aura scope & stacking:** `v0.2.8` detached Kindred Censer from Miravel and gave it a universal allied regen aura on any hero (`7.5u` provisional radius, `+2.2 × HealPower HP/s` in `v0.3.0` recipe), scaling with holder HealPower. Exact aura radius, whether allied aura sources stack when held by multiple heroes, and whether holder self-regen also includes it remain open.
- **Miravel negative natural regeneration:** `v0.2.8` fixes Miravel's passive so enemy HP/s can drop below `0` and kill targets (crediting Miravel with kill/gold/XP/takedown HealPower), bypassing Grievous Wounds/resistances/shields/`[E]` Ward provisionally as natural negative regeneration. Exact classification (natural regen loss vs. heal-reduction/DoT interaction and whether shields or MR absorb negative regen) remains open.
- **Ogre animation CC immunity & `[R]`:** `ogreImmuneDuringRecovery` defaults `true` in `v0.2.8` (`ogreImmuneDuringRecovery` toggle in `LaneTuning`): does CC immunity end immediately at impact or extend into recovery (`BaseRecover / EffectiveAS`)? Also open: max `[R]` queue length beyond provisional `4`, incomplete-sequence rules, and `[E]` Ward stacking per strike (`ogreRStacksCursePerStrike = false`).
- **Old Man Swordsman `[W]`/`[E]`/`[R]`:** `swordsmanDisplaceCancelsSecondHit` defaults `true` (interrupting Strike 2 via displacement or cancel denies `[E]` Vital Draw healing). Open: whether `[R]` Finishing Cross should also allow resetting cooldown on minion kills (in addition to hero kills) or remain hero-kill reset only; exact resistance rules for `[W]` Stance Temper; and whether `[R]` execute threshold checks before or after slash damage.
- **Chain Laser Shooter (`LaneTuning` flags):** `laserQTracksCursor` (`false`), `laserWConsumesStacks` (`false`), and `laserRTracksCursor` (`false`) remain open toggles alongside exact chain bounce distance (`4.2u` provisional base), bounce floor (`25%`), and stack duration (`6.0s`) / half-point (`8`).
- Tortuga shell: damage distribution/mitigation order; shell HP pool; Pet Owner persistence in shell-less form.
- Chibi: how corpses, death timers, revived form duration and hero abilities interact.
- Four-Form: 0-shot behavior, multicast self-knockback, ally dash consent, order selection rules.
- All-Passive: whether standing still while attacking qualifies for E and what constitutes a W damage-second.
- Miravel (Luís): orb exact spawn position logic, orb ally/enemy priority, curse stacking rules across multiple applications, `[E]` illusion protection edge cases, and interrupt rules on `[R]`.
- Airplane Fighter: how tightly can it turn, where does parking trigger, and which heroes' attacks or ground areas can hit it in flight? The continued straight flight during a stun or root is established.
- Bomb Planter: multi-bomb stacking/timers; whether planter death defuses planted bombs immediately or only blocks explosion; hard-taunt vs. reveal-channel semantics for E; W wall-vs-tree rules; enemy R skill-check presentation.
- Automatron Bot 3000: engineer map presence and XP linkage; individual Automatron auto-buy vs. engineer purchasing; death/bounty rules; region/objective controls and tiered passive pools.
- Multiple Phases Raid Boss Hero: fountain/death reset with no healing or regen; burst-damage clamping at 20% phase thresholds; exact Q/W/E multi-part combo shapes; R arena wall vs. blinks and whether 1% true damage/sec also affects the Raid Boss.
- Kobold King: Q/W/E kobold respawn cadence vs. spawn-on-cooldown; proximity stat-scaling radius/curve; Sling Kobold RNG vs. PRD stun chance; R succession camera/control handoff, summon-slot reset, and whether the dead old king leaves a corpse or vanishes cleanly. (Note: required multi-unit RTS selection/control architecture is established via `LaneControls` / `LaneRtsSelection`.)
- Mr. Random: modular ability-generation grammar and compatibility rules (preventing broken/recursive loops); relative weights of randomness vs. internal kit synergy vs. lane affinity across the 6 positions (`Top`, `Mid`, `Bot Carry`, `Support`, `Safe Jungle`, `Advanced Jungle`); power-budget constraints; loading-screen vs. match-start reveal timing; opponent/ally inspection UI; dynamic tooltip and VFX/telegraph synthesis.
- Twin-Stick Shooter Roguelike: 3 vs. 4 level-up options per level; whether pools are handcrafted modules, procedural, or hybrid; whether upgrades can modify basic `LMB` projectile shooting itself; minimum level gates or upgrade limits vs. unrestricted rush to 4th Ultimate slot; rarity distribution curves; exact rules for the 4th-slot (`E`) Ultimate transformation bonus; non-obstructive in-combat selection UI and kit inspection for opponents/allies.

## Practical

- Unreal Engine vs other candidate engines: recommended UE5, but first check machine specs and installed tooling.
- Art direction and use of placeholders.
- Official game name: **`Ultimate Free MOBA`** is now the provisional game name (`Woodland Lane` remains the current prototype battlefield / internal development name; `MOBA Project` is only the root repository folder name).
- Version control hosting and multiplayer test infrastructure.
