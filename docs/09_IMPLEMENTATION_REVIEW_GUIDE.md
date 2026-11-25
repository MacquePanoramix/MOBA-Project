# Independent Design & Implementation Review Guide (`v0.2.7`)

This document was prepared for **independent architectural and game-design review** (including ChatGPT's external review of the synchronized GitHub repository `MacquePanoramix/MOBA-Project`). It maps the live source code and explicitly separates **designer-approved rules** and **user-authored hero concepts** from **provisional prototype formulas, items, and balancing defaults** created to make **Ultimate Free MOBA (`Woodland Lane`)** playable.

> [!IMPORTANT]
> Numerical values, ability scaling formulas, and most shop items listed as **Provisional** below have **not** yet been approved as final game design by Luís. Preserve that boundary when reviewing or suggesting changes.

---

## Source Code Map (`prototype/Assets/LaneSlice/`)

| Concern | Primary Source File(s) |
| --- | --- |
| Enums, `StatFormula`, `LaneNames`, 20-item Catalog, `LaneTuning`, Snapshots | `prototype/Assets/LaneSlice/Runtime/LaneTypes.cs` |
| Core 30 Hz authoritative sim, `ResistanceFactor`, `CooldownFactor`, order validation, combat/tower/shop rules | `prototype/Assets/LaneSlice/Runtime/LaneMatch.cs` |
| Hero stat recalculation, basic attacks, Ogre / Miravel / Swordsman execution | `prototype/Assets/LaneSlice/Runtime/LaneHeroes.cs` |
| Chain Laser Shooter passive chains, stacks, Q/W/E/R execution & decoy blocking | `prototype/Assets/LaneSlice/Runtime/LaneHeroLaser.cs` |
| Periodic creep waves, outer towers, Miravel orbs, R zones, illusion movement stop | `prototype/Assets/LaneSlice/Runtime/LaneMinionsAndTowers.cs` |
| Hero-specific control profiles, context-aware command catalog (`Global`, `ConventionalMoba`, `RtsMultiUnit`, future `FlightSteering` / `TwinStick`) | `prototype/Assets/LaneSlice/Runtime/LaneControls.cs` |
| Extensible personal preferences & keybinding store (`personal_settings.json`), conflict resolution | `prototype/Assets/LaneSlice/Runtime/LaneSettings.cs` |
| RTS multi-unit selection, drag-box query, ownership filtering & control groups `1..4` | `prototype/Assets/LaneSlice/Runtime/LaneRtsSelection.cs` |
| Scene lifecycle, Unity Relay / TCP wiring, input routing & marquee selection | `prototype/Assets/LaneSlice/Runtime/LaneScene.cs` |
| IMGUI lobby, HUD, Ability Cards launcher, Fountain Shop, Settings & Keybind UI | `prototype/Assets/LaneSlice/Runtime/LaneHud.cs`, `LaneHud.Settings.cs` |
| Data-driven Ability Cards, rate/checkpoint formatters & `[Show Math]` formulas | `prototype/Assets/LaneSlice/Runtime/LaneHeroSheet.cs` |
| Unity Transport + Relay (`MOBA-LANE/1` codec) + Direct TCP fallback | `prototype/Assets/LaneSlice/Runtime/LaneNet.cs` |
| GitHub Releases one-click Windows updater (`WoodlandLane-Windows.zip`) | `prototype/Assets/LaneSlice/Runtime/LaneUpdater.cs` |
| Sparring Bot AI, builds & wave logic | `prototype/Assets/LaneSlice/Runtime/LaneBot.cs` |
| Automated deterministic & network/settings tests + Windows build script | `prototype/Assets/LaneSlice/Editor/LaneMatchChecks.cs`, `LaneNetChecks.cs`, `LaneEditor.cs` |

---

## 1. Design Rules Explicitly Approved by Luís

- **Provisional Official Title:** **`Ultimate Free MOBA`** (while `Woodland Lane` remains the prototype battlefield / internal dev label and updater/executable asset names `WoodlandLane.exe` / `WoodlandLane-Windows.zip` are preserved for auto-updater compatibility).
- **Fundamental Attribute Philosophy (`docs/01_CORE_SYSTEMS.md`, `LaneHeroes.cs:RecalcHeroStats`):**
  - **Strength**, **Agility**, and **Intelligence** start at **`0`** and confer **zero intrinsic universal stats** (no automatic HP, Armor, AD, AS, Mana, or spell amp).
  - Individual ability properties and item passives explicitly decide what (if anything) they scale with — including speed, duration, size, range, knockback, stun duration, movement ratio, or damage.
  - Items do **not** have to grant attributes and must not be restricted to single specific heroes.
- **No-Hard-Cap Diminishing Philosophy:**
  - Replaced hard-capped `45%` CDR with linear **Ability Haste** using diminishing cooldown reduction `Cooldown = Base × 100 / (100 + AH)` (`LaneMatch.CooldownFactor`).
  - Armor & Magic Resist use `100 / (100 + R)` above `0` and `2 - 100 / (100 - R)` below `0` (`LaneMatch.ResistanceFactor`) so resistance shred past `0` amplifies damage without division-by-zero cliffs.
  - Attack speed and Ogre cast animation speed scale without upper hard caps (`Base / Speed`), and threshold-sensitive `StatFormula` parameters use hyperbolic diminishing curves (`M × Stat / (Stat + H)`).
- **Confirmed Hero Rules in Playtests:**
  - **Ogre** cannot perform basic attacks; **AD** scales damage, **STR** scales stun/knockback, and **AGI + bonus Attack Speed** accelerate club cast wind-ups/recovery; Ogre's **`[R]`** works by channeling and queueing `[Q]/[W]` strikes (aided in UI via the `RAMPAGE CHANNEL` banner).
  - **Miravel (Hero Luís)** base `HealPower` starts at **`100%` (`1.0x`)** at Level 1 (`LaneHeroes.cs:150`) — active immediately before takedowns or items; **`[E]`** Retribution Ward does **not** break active **`[W]`** stealth; **`[W]`** Shimmer Decoy walks to the exact cast-time cursor location (no distance cap) and stops both movement and walking animation upon arrival.
  - **Old Man Swordsman** `[Q]` Hamstring Arc marks targets for `[R]` Finishing Cross; `A` Attack-Move preserves an active wind-up on the same target and respects post-hit backswing recovery.
- **Confirmed Lane, Economy, & Control Architecture Rules:**
  - Last-hitting enemy minions awards Gold + CS; being alive within `12u` proximity of an enemy minion death awards full 100% XP without requiring a last hit (`DealDamageToMinion`).
  - Enemy outer tower immediately switches aggro onto an enemy hero who damages an allied hero within protection range (`DealDamageToHero`).
  - Fountain Shop supports selling owned items in Fountain / while dead (`70%` refund ratio).
  - **`S` Stop command** cancels movement and cancelable attack wind-ups / channels while respecting committed actions and being unavailable to incompatible hero control archetypes (`Airplane Fighter`, `Twin-Stick Shooter`).
  - **Optional RTS controls** are **off by default** on conventional heroes, customizable in Personal Settings, **automatically active** for RTS-required heroes (`Kobold King`), and server/simulation-enforced for unit ownership.
  - **Personal Settings & Keybindings** distinguish overlapping active contexts (`Global`, `ConventionalMoba`, `RtsMultiUnit`) from mutually exclusive hero contexts (`TwinStick`, `FlightSteering`) so `S = Stop` and `S = Move Down` never false-conflict, and persist outside the install folder so auto-updates never overwrite settings.

---

## 2. Original User-Authored Hero Mechanics (Intent, Not Numerical Specs)

Full verbatim/structured mechanics for all **15 hero concepts** live in [`docs/02_HERO_DESIGNS.md`](02_HERO_DESIGNS.md). Currently:
- **Implemented in `WoodlandLane` (`4` heroes):** Hero 1 (**Miravel / Hero Luís**), Hero 6 (**Ogre**), Hero 8 (**Old Man Swordsman**), Hero 11 (**Chain Laser Shooter**).
- **Documented Only — Not Implemented (`11` heroes):** All-Passive Hero (`2`), Chibi Necromancer (`3`), Four-Form Botlaner (`4`), Slow Tortuga (`5`), Airplane Fighter (`7`), Bomb Planter (`9`), Automatron Bot 3000 (`10`), Multiple Phases Raid Boss (`12`), Kobold King (`13`), Mr. Random (`14`), Twin-Stick Shooter Roguelike (`15`).
- **User-Authored Item Concept Example:** **The Coward's Shield** (grants temporary resistances scaling with Agility when retreating away from a recent hero attacker, decaying when turning back toward them).

---

## 3. Implementation Decisions Made to Realize Mechanics

- **Client-Hosted Authoritative 30 Hz Simulation:** Pure-C# fixed-step `LaneMatch` ticks at `30 Hz` (`tuning.tickHz`); Host/Practice simulates authoritatively and sends `LaneSnapshot`s while Guest transmits `LaneOrder`s (`Pick`, `Start`, `Order`, `Snapshot`, `Ping`, `Pong`) via Unity Transport + Relay (`FragmentationPipelineStage` + `ReliableSequencedPipelineStage`, `65,536`-byte max frame) or Direct TCP fallback (`MOBA-LANE/1`).
- **Control Architecture & Ownership Enforcement:** `HeroControlProfile` models `RequiredCommands`, `OptionalCommands`, `UnavailableCommands`, `RtsControlMode`, and custom ability slot mappings. `LaneOrder.unitIds` carries selected friendly unit IDs when RTS is active (`null` otherwise); `LaneMatch.EnqueueOrApplyOrder` rejects `AttackMove`/`Attack*` on `Ogre`, rejects `Stop` when `!profile.SupportsStop`, and strictly filters `unitIds` against `team`, `ownerHeroId`, `!ill.isLaserDecoy`, and `hp > 0`.
- **Movement & Responsiveness Feel (`AdvanceMotor`):** Acceleration `26f`, Deceleration `32f`, `TurnSlowdownAngle = 90f` (upgraded from `v0.2.1`'s `14f`/`18f`/`80°`), `basicMotionBuffer = 1.15f`, `attackMoveScanRadius = 4.5f`.
- **Input Buffering:** Ability orders received while stunned (`0.25s` window) or during another ability cast/channel/recovery (`0.30s` window) buffer briefly; casting an ability cleanly cancels basic wind-up or basic recovery backswing; `Stop` cancels wind-up and Miravel `[R]` without skipping basic `Recover` cooldowns.
- **Chain Laser Shooter Decoy Collision & Anti-Snowball Safeguards:** `ResolveLaserDecoyBlocking` pushes enemy heroes outside `decoyRadius + 0.48f` while preserving velocity; Chain Laser `[W]` stacked slow strength uses `rawSum / (1 + 0.35 × max(0, rawSum - perShockSlow))` clamped to `82%` max slow; Swordsman `[E]` minion armor shred clamps at `-48`; Swordsman `[W]` minion `% Max HP` bonus damage clamps at `140`.

---

## 4. Ability Scaling Relationships & Formulas Independently Designed *(Provisional — Not Approved)*

*Except where noted (Ogre's AD/AGI/STR split & `v0.2.6` AS cast scaling requested by Luís), every base stat, growth rate, coefficient, half-point, duration, radius, and cooldown below was authored provisionally for prototype playtesting (`LaneTypes.cs` / `LaneHeroes.cs` / `LaneHeroLaser.cs`):*

### Ogre *(Base: `720 HP +48/lvl`, `60 AD +5/lvl`, `Arm 16`, `MR 12`, `Regen 1.6/s`, `Move 3.75`, `Turn 270°/s`)*
- **Wind-up/Recovery Speed:** `1.00 + 0.008 × AGI + 0.70 × (AttackSpeed - 1.00)` (floored `0.4x`, no hard cap; divides Q/W/E/R wind-up & recovery durations)
- **`Q` Club Slam:** Beam `4.7u × 1.15u`, `0.90s` windup, `0.34s` recover, CD `6.8/6.2/5.6s` | Damage `55/73/91 + 135% AD` phys | Stun `1.05/1.20/1.35s + 0.85s × STR / (STR + 50)`
- **`W` Club Sweep:** Cone `3.0u` (`cos 0.15`), `0.72s` windup, `0.28s` recover, CD `6.2/5.6/5.0s` | Damage `40/55/70 + 105% AD` phys | Knockback `2.5/2.75/3.0u + 2.4u × STR / (STR + 50)`
- **`E` Quake Stomp:** Radius `2.45u`, `0.62s` windup, `0.30s` recover, CD `8.5/7.7/6.9s` | Damage `48/64/80 + 115% AD` phys | Stun `0.95/1.07/1.19s + 0.65s × STR / (STR + 50)`
- **`R` Rampage Combo:** `2.0s` channel (`65% DR`, `1.28x` scale), up to `4` queued strikes (`0.82x` Q/W dmg, `1.85x` final crit, `1.35u` lunge/strike, `0.36s` fast / `0.62s` final windup, `0.09s` beat), CD `28/24s`

### Miravel / Hero Luís *(Base: `530 HP +34/lvl`, `48 AD +3.5/lvl`, `Arm 8`, `MR 18`, `Regen 1.4/s`, `Move 4.35`, `Turn 540°/s`)*
- **Basic Attack:** `6.2u` range, `100% AD` physical bolt (`0.26s` windup, `0.52s` recover, `16.5u/s` projectile)
- **Passive Aura & Takedowns:** `7.8u` radius | Ally regen `+(2.2 + 0.35×Lvl + AuraItems) × HealPower` (`HealPower` starts `100%`, `+10%` per hero takedown) | Enemy regen penalty `-(2.4 + 0.35×Lvl)` HP/s
- **`Q` Sanctuary Orbs (Passive):** Spawn every `max(4.0s, 7.5/6.7/5.9s)`, lasts `11.0s`, pickup `1.55u`, pulse `4.1u` | Heal `(68/88/108 + 150% INT) × HealPower` | Damage `64/82/100 + 140% INT` magic
- **`W` Shimmer Decoy:** Stealth `3.2/3.6/4.0s`, `4.5s` decoy (`max(80, HP)`), CD `10.5/9.4/8.3s`
- **`E` Retribution Ward:** Ward `4.5/5.1/5.7s`, Curse `4.2/4.6/5.0s` base (`+1.2s` & `+1` stack per enemy cast up to `5`, `+35%` DPS/stack) | 1-stack DPS `22/30/38 + 65% INT` magic | CD `8.5/7.7/6.9s`
- **`R` Ramping Sanctum:** `5.0u` cast range, `4.4u` radius, `4.2s` linear ramp channel | Enemy vuln up to `+36/42% + 30% × INT / (INT + 60)` | Max slow `52/58%` | Ally heal amp `+45/53%` | CD `26/23s`

### Old Man Swordsman *(Base: `615 HP +40/lvl`, `54 AD +4.0/lvl`, `Arm 14`, `MR 14`, `Regen 1.5/s`, `Move 3.45`, `Turn 480°/s`)*
- **Passive Twin-Blade:** `2.35u` range, `60% AD × 2` (`0.24s` hit 1 windup + `0.22s` hit 2 delay + `0.50s` recover)
- **`Q` Hamstring Arc:** `3.5u` cone (`cos 0.35`), `0.28s` windup, `0.24s` recover, CD `5.5/5.0/4.5s`, marks target `12.0s` | Damage `45/65/85 + 90% AD` phys | Slow `40/45/50%` for `2.1s`
- **`W` Vital Draw (1st Hit):** Bonus magic dmg `4.5/5.9/7.3% + 6.0% × STR / (STR + 60)` target Max HP (`140` cap on minions); heals `55% × HealPower` of bonus damage
- **`E` Stance Temper (2nd Hit):** Self `+8/10/12` Arm/MR & enemy `-8` Arm/MR per stack (max `6` stacks = `+48/+60/+72` self, `-48` enemy), `9.0s` duration
- **`R` Finishing Cross:** `7.5u` range, `18.0u/s` dash, `95/135 + 135% AD` phys, true-dmg execute `<= 22/26%` HP + full CD reset on kill, CD `22/18s`

### Chain Laser Shooter *(Base: `480 HP +30/lvl`, `46 AD +3.5/lvl`, `Arm 8`, `MR 10`, `Regen 1.3/s`, `Move 4.00`, `Turn 560°/s`)*
- **Passive Chain & Stacks:** `6.0u` range (`0.22s` windup, `0.66s` recover), unlimited unvisited hops within `4.6u` (`×0.70` per hop, `25%` floor); stacks last `6.0s` (`half-point = 8`); damage mult `1 + (0.45 + 0.45 × INT / (INT + 60)) × s / (s + 8)`
- **`Q` Extending Line Beam:** `1.2u → 8.2u` (`0.58u` half-width) in `0.85s / (1.0 + 0.01 × AGI)` | `44/62/80 + 105% AD` phys | `+12g` escalating bonus gold per kill after the 1st | CD `7.2/6.5/5.8s`
- **`W` Resonant Shock:** `7.8u` scan, `2.65u` shock radius | `30/42/54 + 70% INT` magic (`+12%`/stack > 1) | Slow `(20/24/28% + 20% × INT / (INT + 60))` combined via diminishing sum (max `82%`) + `1.1s` additive duration per shock | CD `7.5/6.8/6.1s`
- **`E` Hologram Reposition:** `1.65u` step-back, `4.5s` / `120 HP` stationary decoy (`0.62u` radius) | Retaliation `34/48/62 + 65% INT` magic + stun `0.65/0.80/0.95s + 0.45s × STR / (STR + 55)` | CD `11.0/10.1/9.2s`
- **`R` Overdrive Prism Beam:** `0.50s` windup → `2.6s` mobile channel (`8.0u × 1.44u`), moves at `78/78% + 22% × AGI / (AGI + 50)` MS, chain-procs every `0.42s / AS` at `62% AD` | CD `28/24s`

---

## 5. Prototype Items & Effects (`LaneItems.Catalog`, `20` Items — Provisional)

Only **The Coward's Shield** (ID `12`) has a designer-authored concept (with provisional numbers); all other items, prices, tiers (`Starter` / `Core` / `Capstone`), and effects were independently created for the prototype:

| ID | Name | Tier | Cost | Stats & Passive | Provenance |
| --- | --- | --- | --- | --- | --- |
| `14` | Woodland Salve | Starter | `175g` | `+70 HP`; Moss Mend: `+4.5 HP/s` when out of hero combat `5.0s` | AI Provisional |
| `13` | Miner's Hatchet | Starter | `200g` | `+10 AD`; Cleaver: `+18` bonus physical dmg vs. minions | AI Provisional |
| `15` | Brawler's Buckler | Starter | `225g` | `+80 HP, +12 Arm`; Sturdy: blocks `6` dmg from minions & hero basics | AI Provisional |
| `3` | Vitality Vest | Starter | `250g` | `+160 HP, +1.4 Regen` | AI Provisional |
| `1` | Iron Greatsword | Core | `275g` | `+26 AD` | AI Provisional |
| `2` | Quickstep Dagger | Core | `275g` | `+30% AS` | AI Provisional |
| `4` | Stone Plate | Core | `300g` | `+30 Arm` | AI Provisional |
| `5` | Null Mantle | Core | `300g` | `+30 MR` | AI Provisional |
| `6` | Chrono Band | Core | `300g` | `+22 AH` | AI Provisional |
| `8` | Titan's Knuckle | Core | `300g` | `+25 STR` *(pure scalar)* | AI Provisional |
| `9` | Zephyr Feather | Core | `300g` | `+25 AGI` *(pure scalar)* | AI Provisional |
| `10` | Arcane Lens | Core | `300g` | `+25 INT` *(pure scalar)* | AI Provisional |
| `7` | Courier Boots | Core | `325g` | `+0.80 MS` | AI Provisional |
| `11` | Kindred Charm | Core | `325g` | `+22% HealPower, +1.6/s` allied aura regen | AI Provisional |
| `16` | Blighted Thorn | Core | `375g` | `+16 AD, +12 Arm`; `-35%` healing/regen on hero hit for `3.5s` | AI Provisional |
| `12` | **The Coward's Shield** | Capstone | `450g` | `+15 Arm/MR, +12 AGI`; retreating within `4.5s` grants `+(20 + 0.85×AGI)` Arm/MR | **Concept: Luís**; numbers AI Provisional |
| `17` | Momentum Greaves | Capstone | `575g` | `+0.85 MS, +18 AGI`; moving out of combat builds `+(0.45 + 0.65×AGI/(AGI+45))` MS | AI Provisional |
| `18` | Colossus Pauldron | Capstone | `625g` | `+220 HP, +20 Arm, +20 STR`; stun/KB or `<45% HP` grants `4s` `(65 + 2.0×STR)` shield (`12s` CD) | AI Provisional |
| `19` | Spellweaver's Hourglass | Capstone | `625g` | `+18 AH, +25 INT`; next hero hit within `5s` after cast deals `+(28 + 85% INT)` magic (`4.5s` CD) | AI Provisional |
| `20` | Executioner's Grudge | Capstone | `650g` | `+42 AD, +20% AS`; `+18%` damage vs. heroes `<40% HP` | AI Provisional |

---

## 6. Temporary Prototype Balancing Values & Economy (`LaneTuning`)

- **Arena & Towers:** `laneHalfLength = 24f`, `laneHalfWidth = 8.2f`, `fountainRadius = 3.2f`, `fountainHealPerSec = 42f`, `towerMaxHp = 1350f`, `towerArmor = 18f`, `towerAttackRange = 6.9f`, `towerAttackDamage = 68f`, `towerAttackInterval = 1.15f`, `towerProjectileSpeed = 14f`.
- **Waves & Economy:** `startingGold = 500`, `passiveGoldPerSecond = 2.0f`, `waveFirstSpawnDelay = 2.5f`, `waveInterval = 26f`, `3` melee (`240 HP`, `14 AD`, `22g`, `44 XP`) + `1` ranged (`155 HP`, `18 AD`, `28g`, `54 XP`), `minionXpRadius = 12.0f`, `heroKillGold = 210f`, `heroKillXp = 175f`, `baseRespawnSeconds = 6.5f` (`+1.4s`/lvl above `1`).
- **Bot Item Builds (`LaneBot.cs`):** hardcoded 6-item build sequences per hero (`Ogre`: `15,1,2,8,18,20`; `Miravel`: `14,10,11,19,12,17`; `Swordsman`: `13,1,2,16,18,20`; `Laser`: `13,1,2,10,17,20`) used only for offline sparring & 120s automated balance diagnostics.

---

## 7. Unresolved Design Questions & Exposed `LaneTuning` Flags

Check [`docs/05_OPEN_QUESTIONS.md`](05_OPEN_QUESTIONS.md) for all open questions across Heroes 1–15, map/team format (`5v5` vs. `6v6`), trees vs. bushes, and engine direction (Unity trial vs. UE5). Six policy questions are directly exposed as toggles on `LaneTuning`:

- `ogreImmuneDuringRecovery = true`
- `ogreRStacksCursePerStrike = false`
- `swordsmanDisplaceCancelsSecondHit = true`
- `laserQTracksCursor = false`
- `laserWConsumesStacks = false`
- `laserRTracksCursor = false`
