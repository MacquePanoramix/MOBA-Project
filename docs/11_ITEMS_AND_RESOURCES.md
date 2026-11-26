# Items, Recipes & Shared Hero Resource System (`v0.3.0`)

This document specifies the **`v0.2.12–v0.3.0`** resource infrastructure, recipe-based shop architecture, the **21 Approved Components**, **6 Approved Custom Advanced Items**, **17 Provisional Temporary Items**, and **Part 13 Technical Decisions** for **Ultimate Free MOBA (`Woodland Lane`)**.

> [!IMPORTANT]
> **Approved vs. Provisional Boundary:**
> - **Designer-Approved:** Qualitative hero resource assignments (`Mana` vs. `Resourceless`), zero intrinsic universal attribute bonuses (`STR`/`AGI`/`INT` grant no innate Mana, HP, AS, Armor, or damage), scaling curve classifications (`Linear`, `Bounded diminishing`, `Unbounded diminishing`, `Inverse duration`), the concepts/stat identities/scaling relationships of the **21 Approved Components** and **6 Approved Custom Advanced Items**, zero Mana stats on those 27 approved items, and retiring the shelved **Forgotten Hourglass**.
> - **Provisional:** All specific numerical costs, base stats, half-points, passive ratios, hero Mana pool/regen/cost numbers, the `17` temporary starter/component/advanced items, and the **Part 13** implementation defaults below remain **provisional** and subject to iterative playtesting and collaborative review.

---

## 1. Shared Hero Resource System (`v0.2.12`)

Implemented in `LaneTypes.cs`, `LaneHeroes.cs`, and `LaneMatch.cs` (`HeroResourceKind`, `ResourceCostMode`, `AbilityResourceSpec`).

### Resource Stats (Independent of `STR` / `AGI` / `INT`)
- **Maximum Mana (`MaxMana`):** Expands the Mana pool. Buying or selling Max Mana items preserves the hero's current Mana **percentage** (`preserveHpRatio: true`).
- **Mana Regeneration (`ManaRegen` + `PercentManaRegenPerSec`):** Continuous Mana/s active **both inside and outside combat**, plus Fountain recovery (`+48.0 Mana/s` provisional). Death sets `netManaRegen = 0`; respawning restores `100%` HP & Mana.
- **Resource Cost Reduction (`RCR`):** Dimensionless rating stat using **Bounded diminishing returns** strictly below `100%` so ability costs never reach zero through ordinary stacking:
  $$\text{RCR\%} = \frac{0.65 \times \text{RCR}}{\text{RCR} + 65} \qquad (< 65\% \text{ asymptote, provisional})$$

### Consumption Modes (`ResourceCostMode`)
1. `None` (`0` resource cost — remains active at `0` Mana).
2. `Flat` (upfront deduction on cast start: `Cost × (1 - RCR%)`; rejected with feedback popup and no cooldown/cost deduction if Mana is insufficient).
3. `ContinuousPerSecond` (channel drain per tick: `(RatePerSec × (1 - RCR%)) × dt`; requires a `25%` single-second minimum start buffer and cancels the channel cleanly at `0` Mana without going negative).
4. `PercentCurrent` & `PercentMax` (supported in shared `AbilityResourceSpec` infrastructure for future kits).

### Approved Hero Assignments & Provisional Numbers
| Hero | Resource | Pool & Natural Regen *(Provisional)* | Ability Costs *(Approved Mechanics / Provisional Numbers)* |
| --- | --- | --- | --- |
| **Ogre** | **Resourceless** (`None`) | — | All abilities cost `0`. Unaffected by Max Mana / Mana Regen / RCR. |
| **Old Man Swordsman** | **Resourceless** (`None`) | — | All abilities cost `0`. Unaffected by Max Mana / Mana Regen / RCR. |
| **Miravel (Hero Luís)** | **Mana** | `320 + 32/lvl` Mana • `2.6 + 0.25/lvl Mana/s` | Passive & auto-`[Q]` Orbs = **`0` Mana**; `[W]` = `55/65/75` Flat; `[E]` = `45/55/65` Flat (curse extensions cost `0`); `[R]` = `26/34 Mana/s` `ContinuousPerSecond` (**INT does not increase Mana costs/drain**; stops cleanly when exhausted). |
| **Chain Laser Shooter** | **Mana** | `300 + 28/lvl` Mana • `2.2 + 0.22/lvl Mana/s` | Passive chained basics & `[R]` procs = **`0` Mana**; `[Q]` = `65/78/91` Flat; `[W]` = `45/52/59` Flat; `[E]` = `35` Flat (rank-independent low cost); `[R]` = `110` Flat constant upfront cast cost. |

---

## 2. Recipe-Based Shop Architecture (`LaneItems.cs`)

- **Inventory & Selling:** `6` item slots (`MaxSlots = 6`), `70%` sell refund (`SellRefundRatio = 0.70f`), buying/selling permitted inside Fountain or while Respawning (`LanePhase.Dead`).
- **Duplicate-Aware Recursive `ResolvePurchase`:**
  - Direct owned recipe ingredients are matched and reserved **before** descending into nested sub-recipes (so holding an assembled intermediate such as `Heartstone` or `Wellspring Flask` takes priority without disturbing unrelated slots).
  - Owned components deduct their full purchase price from `RemainingCost` (`Mathf.Max(0, Cost - ConsumedValue)`).
  - Consumed slots are freed during resolution, allowing a player at `6/6` slots to complete any recipe that consumes at least one held ingredient.
  - **Catalog Invariants (tested):** Every recipe item satisfies `Cost >= sum(direct ingredient costs)`, preventing gold-duplication or infinite-refund loops.
- **Unique Named Passives (`NormalizeEffectivePassives`):** Duplicate copies of the same item grant full direct stats, while identical named passives do not stack twice; **`Final Judgment`** supersedes `Wounded Strike`, and **`Astral Surge`** supersedes `Wellspring`.
- **Legacy ID `19` & Forgotten Hourglass:** `MigrateItemId(19) == 0`. Legacy **Spellweaver's Hourglass** is retired, and **Forgotten Hourglass** is intentionally **not implemented** pending future designer review.

---

## 3. Approved Component Catalogue (`21` Items — Zero Mana Stats, Costs & Exact Values Provisional)

| ID | Name | Category | Cost | Recipe | Direct Stats | Passive *(if any)* |
| --- | --- | --- | ---: | --- | --- | --- |
| `101` | Small Axe | Component | `240g` | — | `+22 AD` | — |
| `102` | Heavy Axe | Component | `580g` | `2 × Small Axe + 100g` | `+52 AD` | — |
| `103` | Short Sword | Component | `180g` | — | `+14 AD` | — |
| `104` | Steel Sword | Component | `360g` | `Short Sword + 180g` | `+26 AD` | — |
| `105` | Titanstone | Component | `300g` | — | `+25 STR` | — |
| `106` | Windfeather | Component | `300g` | — | `+25 AGI` | — |
| `107` | Sage's Eye | Component | `300g` | — | `+25 INT` | — |
| `108` | Life Crystal | Component | `220g` | — | `+160 Max HP` | — |
| `109` | Heartstone | Component | `560g` | `2 × Life Crystal + 120g` | `+400 Max HP` | — |
| `110` | **Bloodstone** | Specialized | `440g` | `Life Crystal + 220g` | `+180 Max HP` | **Wounded Strike** *(Linear Missing HP% → up to `+14%` dmg vs wounded heroes; no attribute scaling; replaced by Final Judgment)* |
| `111` | Wooden Shield | Component | `180g` | — | `+18 Armor` | — |
| `112` | Silver Amulet | Component | `200g` | — | `+20 MR` | — |
| `113` | Reinforced Shield | Component | `640g` | `2 × Wooden Shield + Silver Amulet + 80g` | `+42 Armor, +26 MR` (`1.62:1`) | — |
| `114` | Spring Water | Component | `200g` | — | `+2.5 HP/s` | — |
| `115` | Sacred Pearl | Component | `240g` | — | `+20% Heal Power` | — |
| `116` | **Healing Elixir** | Specialized | `560g` | `Spring Water + Sacred Pearl + 120g` | `+3.2 HP/s, +25% Heal Power` | **Lingering Restoration** *(Repeats `28%` of actual non-overheal `AbilityOrItem` HP restored smoothly over `3.5s`; non-recursive; no attribute scaling)* |
| `117` | **Dragon Scale** | Specialized | `620g` | — | `+42 Armor, +14 MR` (`3.0:1`), `+16 STR` | **Adaptive Scales** *(Per-attacker direct Phys/Mag hits, max `6` stacks, hold `3.5s`, decay `1/1.2s`: `[20 + 35×STR/(STR+65)] × s/(s+3)` Arm/MR — Bounded diminishing)* |
| `118` | **Primordial Blood** | Specialized | `540g` | — | `+18 STR, +3.0 HP/s` | **Primordial Vitality** *(Natural regen `+{12.0 × (1 - HP/MaxHP)} HP/s` Linear missing HP; does not amplify heals; no attribute scaling)* |
| `119` | Iron Dagger | Component | `260g` | — | `+28% Attack Speed` | — |
| `120` | **Nimble Dagger** | Specialized | `660g` | `Iron Dagger + Windfeather + 100g` | `+34% AS, +25 AGI` | **First Strike** *(After `3.0s` without starting a basic attack, next basic windup × `1 - [0.22 + 0.43×AGI/(AGI+60)]` — Bounded diminishing; `0.08s` raw / `0.065s` final floor; consumed on windup start)* |
| `121` | **Winged Charm** | Specialized | `340g` | — | `+0.75 Move Speed` | **Weightlessness** *(Incoming slow strength × `0.75`; full slow & stun durations preserved; no stat scaling)* |

---

## 4. Approved Custom Advanced Items (`6` Items — Zero Mana Stats, Numbers Provisional)

| ID | Name | Cost | Full Recipe | Direct Stats | Named Passives & Provisional Formulas |
| --- | --- | ---: | --- | --- | --- |
| `201` | **Executioner's Axe** | `1550g` | `Heavy Axe + Titanstone + Bloodstone + 230g` | `+56 AD, +28 STR, +240 HP` | **Final Judgment** *(supersedes Wounded Strike)*: Outgoing hero damage amp = `(1 − TargetHP/MaxHP) × [0.24 + 0.28×STR/(STR+65)]` *(Linear missing HP × Bounded STR; applies to Physical/Magical/True direct & ability dmg, excl. non-qualifying procs)*. |
| `202` | **The Coward's Shield** | `1200g` | `Reinforced Shield + Windfeather + 260g` | `+46 Armor, +28 MR, +28 AGI` | **Flight Reflex**: Sustained movement away from a recent hero attacker (`<=5.0s`) grants `+(20 + 0.85×AGI)` Armor & MR *(Linear AGI)* (`0.15s` commit, `1.55s` ramp, `0.75s` stop/stun hold grace after `0.40s` streak, `1.90s` stop / `0.85s` pursuit decay; displacement safe). |
| `203` | **Chalice of Life** | `1180g` | `Life Crystal + Healing Elixir + 400g` | `+280 HP, +4.5 HP/s, +35% Heal Power` | **Fountain of Life**: Incoming valid heals (`NaturalRegen`, `AbilityOrItem`, `Fountain`; excludes `LingeringRestoration`) × `1 + (1 − HP/MaxHP) × [0.28 + 0.55×BonusHPwr/(BonusHPwr+0.65)]` *(Linear missing HP × Bounded Bonus Heal Power)* + **Lingering Restoration** *(inherited)*. |
| `204` | **Draconic Blade** | `1950g` | `Steel Sword + Dragon Scale + Primordial Blood + 430g` | `+44 AD, +40 STR, +48 Arm, +16 MR` (`3.0:1`), `+4.5 HP/s` *(no HP, no INT)* | **Shared Heat** (max `8`, `3.5s` hold, `1/1.0s` decay, refreshed on basic/on-hit): **Dragonfire** Magical DoT (`0.5s` ticks) = `(18 + 0.55×INT) × heat/(heat+4)` DPS *(Bounded Heat × Linear INT)*; **Molten Edge** flat shred = `[14 + 26×INT/(INT+65)] × heat/(heat+4)` Armor & MR *(Bounded Heat × Bounded INT)* + **Adaptive Scales** & **Primordial Vitality** *(inherited)*. |
| `205` | **The Giant's Heart** | `2100g` | `Heartstone + 2 × Titanstone + Primordial Blood + 400g` | `+650 HP, +70 STR, +4.0 HP/s` *(no direct AD/Arm/MR)* | **Ancient Vitality**: Defers `0.16 + 0.34×MaxHP/(MaxHP+1800)` (`<50%` Bounded MaxHP) of post-mitigation, post-shield Phys/Mag/True dmg over `2.4 + 2.6×STR/(STR+80)` s *(Bounded STR)* evenly with no double-mitigation or re-deferral + **Giant's Might** (`+3.0% MaxHP` as genuine AD — Linear MaxHP) + **Immovable Mountain** (`1 - 0.65×STR/(STR+70)` forced displacement factor — Bounded STR `<65%`) + **Primordial Vitality** *(inherited)*. |
| `206` | **Banana Blade** | `1850g` | `Steel Sword + Sage's Eye + Nimble Dagger + Winged Charm + 190g` | `+28 AD, +38% AS, +0.80 MS, +34 AGI, +34 INT` | **Crescent Strike**: Basics/on-hit sweep secondary targets behind primary in a curved arc (radius `2.30 + 1.75×INT/(INT+75)` u — Bounded INT) for `AD × [0.28 + 0.52×AGI/(AGI+70)]` Physical *(Linear AD × Bounded AGI, non-recursive)*; **Slippery Trickster**: hero dmg bursts `0.70 + 1.80×AGI/(AGI+60)` MS *(Bounded AGI, not INT)* decaying over `2.2s` on fixed `14.0s` CD; **First Strike** & **Weightlessness** *(inherited)*. |

---

## 5. Provisional Temporary Item Catalogue (`17` Items — Starters, Utility & Mana Items)

Designed around Part 9 principles (clear fantasies, reusing approved components, Mana resource playstyles, clearly marked **`IsApproved = false`**):

| ID | Name | Category | Cost | Recipe | Stats & Provisional Effect | Provenance / Transition vs. `v0.2.8` |
| --- | --- | --- | ---: | --- | --- | --- |
| `301` | Woodland Salve | Starter | `180g` | — | `+75 HP`; **Moss Mend** (`+4.5 HP/s` out of hero combat `5.0s`) | Retained from legacy `14` (`175→180g, 70→75 HP`) |
| `302` | Miner's Hatchet | Starter | `200g` | — | `+10 AD`; **Cleaver** (`+18` flat physical dmg vs minions) | Retained from legacy `13` (`200g`) |
| `303` | Brawler's Buckler | Starter | `220g` | — | `+80 HP, +12 Arm`; **Sturdy** (`-6` dmg vs minions & hero basics, min `1`) | Retained from legacy `15` (`225→220g`) |
| `304` | **Mana Dew Vial** | Starter | `200g` | — | `+90 MaxMana, +1.2 Mana/s`; **Dew Sip** (`+1.5 Mana/s` out of hero combat `5.0s`) | **New** (`v0.3.0`) Mana sustain starter |
| `305` | Vitality Vest | Starter | `250g` | — | `+160 HP, +1.5 HP/s` | Rebalanced starter from legacy `3` |
| `311` | **Sapphire Shard** | Prov. Component | `200g` | — | `+160 MaxMana` | **New** (`v0.3.0`) pure Mana reserve leaf |
| `312` | **Dewdrop Charm** | Prov. Component | `220g` | — | `+2.8 Mana/s` | **New** (`v0.3.0`) pure Mana regen leaf |
| `313` | **Runed Sigil** | Prov. Component | `280g` | — | `+20 RCR` | **New** (`v0.3.0`) Mana efficiency component |
| `314` | Clockwork Pin | Prov. Component | `280g` | — | `+22 AH` | Replaces legacy `6` (`Chrono Band`, `300→280g`) |
| `315` | **Wellspring Flask** | Prov. Component | `520g` | `Sapphire Shard + Dewdrop Charm + 100g` | `+180 MaxMana, +2.8 Mana/s`; **Wellspring** (`+0.4% MaxMana/s`, unique) | **New** (`v0.3.0`) Mana reserve+replenish bridge |
| `401` | Kindred Censer | Prov. Advanced | `720g` | `Sacred Pearl + Spring Water + 280g` | `+28% HealPower, +2.0 HP/s, +2.2 × HealPower` allied aura (`7.5u`, universal) | Upgraded via recipe from legacy `11` (`Kindred Charm`) |
| `402` | Blighted Thorn | Prov. Advanced | `760g` | `Short Sword + Wooden Shield + 400g` | `+22 AD, +24 Arm`; **Wither** (`-35%` enemy heal/regen `3.5s`) | Upgraded via recipe from legacy `16` |
| `403` | **Astral Reservoir** | Prov. Advanced | `1120g` | `Wellspring Flask + Sage's Eye + 300g` | `+340 MaxMana, +3.5 Mana/s, +30 INT`; **Astral Surge** (`+0.7% MaxMana/s`, supersedes Wellspring) | **New** (`v0.3.0`) INT/reserve capstone |
| `404` | **Tidecaller Chalice** | Prov. Advanced | `1080g` | `Dewdrop Charm + Sacred Pearl + Clockwork Pin + 340g` | `+4.2 Mana/s, +26% HealPower, +25 AH`; **Tidecaller Flow** (up to `+6.0 Mana/s` linearly with missing Mana%) | **New** (`v0.3.0`) caster/enchanter replenishment capstone |
| `405` | **Economist's Tome** | Prov. Advanced | `1160g` | `Runed Sigil + Clockwork Pin + Sage's Eye + 300g` | `+32 RCR, +25 AH, +30 INT`; **Frugal Focus** (`12%` post-cast Flat Mana refund) | **New** (`v0.3.0`) spell-economy capstone |
| `406` | Momentum Greaves | Prov. Advanced | `1100g` | `Winged Charm + Windfeather + 460g` | `+0.85 MS, +26 AGI`; **Windstrider** (`+0.45 + 0.65×AGI/(AGI+45)` OOC MS) + **Weightlessness** | Upgraded via recipe from legacy `17` |
| `407` | Colossus Pauldron | Prov. Advanced | `1360g` | `Heartstone + Wooden Shield + Titanstone + 320g` | `+450 HP, +28 Arm, +28 STR`; **Titan Aegis** (`65 + 2.0×STR` 4s shield on Stun/KB or `<45% HP`, 12s CD) | Upgraded via recipe from legacy `18` |

*(Legacy ID mappings: `1→104`, `2→119`, `3→305`, `4→111`, `5→112`, `6→314`, `7→121`, `8→105`, `9→106`, `10→107`, `11→401`, `12→202`, `13→302`, `14→301`, `15→303`, `16→402`, `17→406`, `18→407`, **`19→0` Retired**, `20→201`.)*

---

## 6. Part 13 Conservative Technical Decisions (Open for Designer Review)

1. **Duplicate Item Copies vs. Unique Named Passives:** Direct stats stack per slot; identical named passives do not stack twice (`NormalizeEffectivePassives`), and `Final Judgment` supersedes `Wounded Strike` while `Astral Surge` supersedes `Wellspring`.
2. **Lingering Restoration vs. Natural Regen & Fountain of Life:** Triggers on actual non-overheal `HealSourceKind.AbilityOrItem` restoration, applies as `HealSourceKind.LingeringRestoration` ticks over `3.5s`, does not re-trigger itself, and is excluded from `Fountain of Life` to prevent exponential double-amplification.
3. **Fountain of Life Scope:** Amplifies `NaturalRegen`, `AbilityOrItem`, and `Fountain` only.
4. **Ancient Vitality Shield, Payout & Attribution Order:** Shields absorb damage **before** the immediate/deferred split; deferred damage pays out evenly per tick via cumulative elapsed fraction (`totalAmount * Clamp01(elapsed/duration) - paidSoFar`), bypassing Armor/MR, shields, and re-deferral. Instances track independently per hit, survive leaving combat or selling Giant's Heart, clear on death, and credit lethal ticks to the original `attackerHeroId`.
5. **Shared `Heat` & `Molten Edge`:** `Dragonfire` (0.5s Magical DoT) and `Molten Edge` (flat Armor & MR shred, matching `Stance Temper` mechanics) read a shared `Heat` entry stored per `(target, attackerHeroId)` so multiple enemies wielding `Draconic Blade` never overwrite one another.
6. **Per-Attacker `Adaptive Scales`:** Keyed by `(target, attackerHeroId)` on direct Physical/Magical hero hits only (ignoring Heat DoT and Miravel Curse DoT).
7. **`First Strike` Consumption & 30 Hz Quantization:** Consumed & reset to `0.0s` immediately at `StartBasicWindup` so move-canceling mid-windup cannot abuse rapid First Strikes. Minimum raw (`0.08s`) and final (`0.065s` → `2` ticks @ `30 Hz` = `0.067s`) floors preserve visual/simulation readability without modifying the approved global `sqrt(1 + 2.4 × BonusAS)` curve.
8. **`Weightlessness` Application Order:** Applied multiplicatively (`× 0.75`) to incoming slow strength in both `ApplySlowToHero` and Chain Laser `[W]` (`ApplyLaserWSlow`) while preserving full slow and stun durations.
9. **Resource Changes on Buy/Sell, Death & Fountain:** Changing `MaxMana` preserves current Mana `%` ratio; death sets `netManaRegen = 0` and respawns at `100%`; Fountain grants `+48 Mana/s`.
10. **`Frugal Focus` Timing:** Enforces full `EffectiveCost` before casting, ignores `ContinuousPerSecond` / `0`-cost skills, and refunds `12%` of actual Flat Mana spent immediately post-deduction (clamped to `MaxMana`).
