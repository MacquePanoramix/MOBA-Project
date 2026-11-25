# Independent Design & Implementation Review Guide (`v0.2.8`)

This document was prepared for **independent architectural and game-design review** (including ChatGPT's external review of the synchronized GitHub repository `MacquePanoramix/MOBA-Project`). It maps the live source code and explicitly separates **designer-approved rules & scaling relationships** from **provisional prototype formulas, items, and balancing defaults** created for **Ultimate Free MOBA (`Woodland Lane`)**.

> [!IMPORTANT]
> Numerical values, ability scaling formulas, and most shop items listed as **Provisional** below have **not** yet been approved as final game design by Luís. Preserve that boundary when reviewing or suggesting changes.

---

## Source Code Map (`prototype/Assets/LaneSlice/`)

| Concern | Primary Source File(s) |
| --- | --- |
| Enums, `ScalingCurve`, `StatFormula`, `LaneNames`, 20-item Catalog, `LaneTuning` (`v0.2.8`), Snapshots | `prototype/Assets/LaneSlice/Runtime/LaneTypes.cs` |
| Collision pushout sweep safety (up to `160` steps for extreme knockback) | `prototype/Assets/LaneSlice/Runtime/LaneMap.cs` |
| Core 30 Hz authoritative sim, `ResistanceFactor`, `CooldownFactor`, Laser W overlap/refreshed slow anti-bypass, combat/tower/shop rules | `prototype/Assets/LaneSlice/Runtime/LaneMatch.cs` |
| Hero stat recalculation, Global AS curve, Kindred Charm aura, Coward's Shield retreat tracking, Ogre / Miravel / Swordsman | `prototype/Assets/LaneSlice/Runtime/LaneHeroes.cs` |
| Chain Laser Shooter passive chains, INT Q speed & W radius, animated E step-back & decoy blocking, R accumulator procs | `prototype/Assets/LaneSlice/Runtime/LaneHeroLaser.cs` |
| Periodic creep waves, outer towers, Miravel Q orbs & R zones, illusion movement stop | `prototype/Assets/LaneSlice/Runtime/LaneMinionsAndTowers.cs` |
| Hero-specific control profiles, context-aware command catalog (`Global`, `ConventionalMoba`, `RtsMultiUnit`, future `FlightSteering` / `TwinStick`) | `prototype/Assets/LaneSlice/Runtime/LaneControls.cs` |
| Extensible personal preferences & keybinding store (`personal_settings.json`), conflict resolution | `prototype/Assets/LaneSlice/Runtime/LaneSettings.cs` |
| RTS multi-unit selection, drag-box query, ownership filtering & control groups `1..4` | `prototype/Assets/LaneSlice/Runtime/LaneRtsSelection.cs` |
| Scene lifecycle, Unity Relay / TCP wiring, input routing & marquee selection | `prototype/Assets/LaneSlice/Runtime/LaneScene.cs` |
| Light/heavy Swordsman 3D blades, procedural blade/club SFX, dual-zone Q & R-scaled telegraphs, animated E step | `prototype/Assets/LaneSlice/Runtime/LaneWorld*.cs` |
| IMGUI lobby, HUD, Ability Cards launcher, Fountain Shop, Settings & Keybind UI | `prototype/Assets/LaneSlice/Runtime/LaneHud.cs`, `LaneHud.Settings.cs` |
| Data-driven Ability Cards with curve classification & `[Show Math]` formulas | `prototype/Assets/LaneSlice/Runtime/LaneHeroSheet.cs` |
| Unity Transport + Relay (`MOBA-LANE/1` codec) + Direct TCP fallback | `prototype/Assets/LaneSlice/Runtime/LaneNet.cs` |
| GitHub Releases one-click Windows updater (`WoodlandLane-Windows.zip`) | `prototype/Assets/LaneSlice/Runtime/LaneUpdater.cs` |
| Sparring Bot AI, builds & dynamic E radius | `prototype/Assets/LaneSlice/Runtime/LaneBot.cs` |
| Automated unit/net checks & Windows build script | `prototype/Assets/LaneSlice/Editor/LaneMatchChecks.cs`, `LaneNetChecks.cs`, `LaneEditor.cs` |
| **Reusable automated balancing & stress-test harness (`RunBalanceExperiments`)** | `prototype/Assets/LaneSlice/Editor/LaneBalanceHarness.cs` |

---

## 1. Design Rules & Scaling Relationships Approved by Luís (`v0.2.8`)

- **Provisional Official Title:** **`Ultimate Free MOBA`** (`WoodlandLane.exe` / `WoodlandLane-Windows.zip` updater names preserved for auto-updater compatibility).
- **Zero Intrinsic Universal Attribute Bonuses:** **Strength**, **Agility**, and **Intelligence** start at **`0`** and confer **zero intrinsic universal stats**. Only explicit ability/item properties scale with them.
- **Explicit Scaling Curve Terminology:**
  - **Linear:** Constant gain per stat point (`Base + c × S`).
  - **Bounded diminishing returns:** Always beneficial, approaching a finite maximum/floor (`Base + M × S / (S + H)`), including Armor/MR (`100/(100+R)` above `0`, `2 - 100/(100-R)` below `0`) and **Ability Haste** (`Base × 100/(100+AH)`).
  - **Unbounded diminishing returns:** Always beneficial with decreasing marginal return and zero finite upper cap (`sqrt(1 + k × S)` or `coeff × (sqrt(1 + k × S) - 1)`).
  - **Inverse duration scaling:** `Duration = Base / SpeedMultiplier`.
- **Global Attack Speed Redesign:** `EffectiveAS = sqrt(1 + k × BonusAS)` (unbounded diminishing returns, zero upper hard cap) governs basic-attack frequency, Swordsman Strike 1 & 2, Ogre recovery durations, and Chain Laser `[R]` beam proc rate.
- **Hero Kits & Approved Scaling Relationships (`v0.2.8`):**
  - **Ogre:** No basic attacks; CC-immune during casts. Split animation scaling: **Wind-ups** scale exclusively with **AGI** (*Unbounded sqrt → Inverse duration*) and **Recoveries** scale exclusively with **Global AS** (*Unbounded sqrt → Inverse duration*). **AD** scales damage (*Linear*); **STR** scales Q/E stun & Q outer width & E radius & R size (*Bounded diminishing*) and W knockback (*Unbounded sqrt*). Q splits into mutually exclusive Central slam (full dmg+stun, R-scaled dimensions, hit feedback + heavy club audio) and Outer shockwave (reduced dmg, zero CC, STR width, dust VFX + thud audio); W range & E radius scale linearly proportionally with R size.
  - **Miravel (Hero Luís):** `HealPower` starts at `100%` (`+10%` permanent per takedown). Passive allied regen & enemy regen drain scale **Linearly** with **Heal Power** (enemy net regen can go **negative** for real HP decay & Miravel-credited lethal kills); aura radius scales **Bounded diminishing** with **INT**. `[Q]` orb heal/dmg scale **Linearly** with **Heal Power** (zero direct INT), radius scales **Bounded** with **INT**, spawn rate uses **Inverse AH** (`no 4s floor`). `[W]` scales with AH only (`cursor` stop). `[E]` DPS scales **Linearly** with **INT**, stealth-safe. `[R]` radius scales **Bounded** with **INT**; max ally heal amp & enemy vuln scale **Bounded diminishing** from **Bonus Heal Power (`HealPower - 1.0`)**.
  - **Old Man Swordsman:** Asymmetric Twin-Blade (**Hit 1 Light Right Sabre**: slender blade, faster, lower AD, procs **new `[W]` Stance Temper**; **Hit 2 Heavy Left Greatsword**: broader blade, heavier hit & audio, AGI-bounded shorter delay that stays heavier than Hit 1, procs **new `[E]` Vital Draw**). `[Q]` **AGI-bounded** windup, fixed recovery (`not` AGI-reduced), AD dmg, attribute-independent slow. `[W]` fixed self/enemy Arm/MR stacks with **INT-linear** separately refreshed self buff & enemy debuff durations. `[E]` **STR-bounded** `% Target Max HP` magic + **Linear HealPower** heal (denied if Strike 2 interrupted). `[R]` AD dmg, **STR-bounded** execute, **AGI-linear** dash speed, **INT-linear** mark memory.
  - **Chain Laser Shooter:** Unlimited non-repeating bounces (**INT-bounded** hop radius & stack asymptote). `[Q]` AD dmg + **INT-sqrt** extension speed (replacing AGI). `[W]` **INT** dmg & **INT-bounded** radius, rank-only base slow, **Bounded** overlap/refreshed slow strength + **Unbounded sqrt** overlap/refreshed duration (anti-bypass elapsed tracking). `[E]` animated fixed step-back (**AGI-bounded** shorter duration), spawns blocking decoy at start spot on completion, **INT** zap + **rank-only** stun. `[R]` AD procs @ **Global AS** frequency (`while` accumulator), **INT-bounded** beam duration, **AGI-bounded** move speed `<100%`.
- **Reworked Shop Concepts & Persistent Controls:** **The Coward's Shield** requires sustained retreat velocity away from a recent attacker (commit delay, gradual buildup, anti-abuse hold-only grace on brief stops/stuns, slower stop vs. faster pursuit decay, linear AGI); **Kindred Charm** emits an allied regen aura on **any** hero; **Colossus Pauldron** clarifies triggering when its *owner inflicts* Stun/KB or drops `<45%` HP. `[S]` Stop, Optional RTS toggle, `HeroControlProfile`, and `[F10]` persistent Settings stay intact.

---

## 2. Original User-Authored Hero Mechanics (Intent, Not Numerical Specs)

Full mechanics for **Heroes 1–15** live in [`docs/02_HERO_DESIGNS.md`](02_HERO_DESIGNS.md) (`4` implemented: **1 Miravel / Hero Luís**, **6 Ogre**, **8 Old Man Swordsman**, **11 Chain Laser Shooter**; `11` documented-only future heroes).

---

## 3. Implementation Decisions & 30 Hz Simulation Notes

- **Authoritative 30 Hz Sim & Transport:** `LaneMatch` ticks at `30 Hz` (`dt = 1/30s`); Unity Transport + Relay (`FragmentationPipelineStage` + `ReliableSequencedPipelineStage`) and Direct TCP (`MOBA-LANE/1`) transport `LaneOrder`s and `LaneSnapshot`s cleanly.
- **30 Hz Quantization Finding (`balance_report_v028.txt`):** Wind-up and Recover timers quantize upward to whole `33.3ms` ticks without sub-tick remainder carry across transitions (`+0% → +180% AS` yields `1.25 → 2.73` Miravel attacks/s vs. `1.28 → 2.96/s` continuous; `+500%` to `+1200%` AS reaches `3.75–6.00/s`), whereas Laser `[R]` uses an accumulator loop (up to `16` procs/tick) preserving continuous frequency (`2.38/s → 13.00/s` at `+1200% AS`). CPU cost stays `< 0.06 ms/tick` even with `20` clustered units under full `[R]` chains.
- **Anti-Bypass & Boundary Safeguards:** `LaneMap.PushOut` sweep step clamp raised `24 → 160` (`40u`) for extreme STR Ogre `[W]` knockbacks; `ApplyDisplacementToHero` updates `prevMotorPos` so enemy knockbacks never spoof retreat speed; Coward's Shield requires `0.40s` prior streak & `>= 0.15` charge to earn `0.75s` grace; Laser `[W]` tracks elapsed slow duration to prevent staggered-cast duration bypass.

---

## 4. Provisional `v0.2.8` Ability Formulas (`LaneTuning` — Not Locked)

### Ogre *(Base: `720 HP +48/lvl`, `60 AD +5/lvl`, `Arm 16`, `MR 12`, `Regen 1.6/s`, `Move 3.75`, `Turn 270°/s`)*
- **Split Anim Speed:** Windup = `sqrt(1 + 0.022 × AGI)` (`1.45x @ 50`, `1.79x @ 100`, `2.32x @ 200`); Recover = `EffectiveAS = sqrt(1 + 2.4 × BonusAS)`
- **`Q` Club Slam:** Central `4.7u × 1.90u` (`0.88s` wind / `0.36s` rec, CD `6.8/6.2/5.6s`) | Central `55/73/91 + 135% AD` & stun `1.05/1.20/1.35 + 0.85×STR/(STR+50)s` | Outer `+0.65/0.75/0.85 + 1.45×STR/(STR+55)u` (`55%` dmg, `0` CC) | Area scales linearly w/ R size
- **`W` Club Sweep:** Cone `3.0u` (`cos 0.15`), `0.72s` wind / `0.30s` rec, CD `6.2/5.6/5.0s` | `40/55/70 + 105% AD` | KB `2.40/2.65/2.90 + 2.80×(sqrt(1+0.04×STR)-1)u` | Range scales w/ R size
- **`E` Quake Stomp:** `0.62s` wind / `0.32s` rec, CD `8.5/7.7/6.9s` | `48/64/80 + 115% AD` | Stun `0.95/1.07/1.19 + 0.65×STR/(STR+50)s` | Radius `(2.35 + 1.55×STR/(STR+55)) × R_Scale` u
- **`R` Rampage Combo:** `2.0s` channel (`65% DR`), Size `1.22/1.28 + 0.52×STR/(STR+65)x`, `4` strikes (`0.82x` / final `1.85x`, `1.35u` lunge, `0.36s`/`0.62s` wind + `0.10s` follow + `0.28s` post recover), CD `28/24s`

### Miravel / Hero Luís *(Base: `530 HP +34/lvl`, `48 AD +3.5/lvl`, `Arm 8`, `MR 18`, `Regen 1.4/s`, `Move 4.35`, `Turn 540°/s`)*
- **Basic & Passive:** `6.2u` (`0.26s` wind / `0.52s` rec) | Aura `7.2 + 4.2×INT/(INT+60)u`, Ally `+(2.2 + 0.35×(Lvl-1)) × HealPower`, Drain `-(2.6 + 0.35×(Lvl-1)) × HealPower` (`+10%` HealPower/kill, real HP decay below `0`)
- **`Q` Sanctuary Orbs:** Interval `8.0/7.4/6.8s × 100/(100+AH)` (no 4s floor), pulse `3.8 + 2.4×INT/(INT+60)u` | Heal `(68/88/108) × HealPower`, Dmg `(62/80/98) × HealPower`
- **`W` Shimmer Decoy:** Invis `3.2/3.6/4.0s`, `4.5s` decoy, CD `10.5/9.4/8.3s`
- **`E` Retribution Ward:** Ward `4.5/5.1/5.7s`, Curse `4.2/4.6/5.0s + 1.2s/cast` (up to `5`, `+35%`/stack), `22/30/38 + 65% INT` 1-stack DPS, CD `8.5/7.7/6.9s`
- **`R` Ramping Sanctum:** `5.0u` range, `4.2s` channel, radius `4.2 + 2.6×INT/(INT+60)u`, slow `52/58%`, HealAmp `25/30% + 45%×(HPwr-1)/((HPwr-1)+0.80)`, Vuln `28/34% + 42%×(HPwr-1)/((HPwr-1)+0.80)`, CD `26/23s`

### Old Man Swordsman *(Base: `615 HP +40/lvl`, `54 AD +4.0/lvl`, `Arm 14`, `MR 14`, `Regen 1.5/s`, `Move 3.45`, `Turn 480°/s`)*
- **Passive Twin-Blade:** `2.35u` | Hit 1 `48% AD` (`0.20s` / `EffAS`), Hit 2 `76% AD` (`(0.40 - 0.14×AGI/(AGI+45)) / EffAS`), Recover `0.46s / EffAS`
- **`Q` Hamstring Arc:** `3.5u` cone (`0.30 - 0.15×AGI/(AGI+45)s` windup, fixed `0.24s` rec, CD `5.5/5.0/4.5s`, marks target) | `45/65/85 + 90% AD` | `40/45/50%` slow `2.1s`
- **`W` Stance Temper (1st Hit):** Self `+8/+10/+12` Arm/MR & enemy `-8` Arm/MR (max `6`), separate buff/debuff timers `6.5 + 0.08×INT` s
- **`E` Vital Draw (2nd Hit):** `4.5/5.9/7.3% + 6.5%×STR/(STR+60)` target Max HP magic (`140` minion cap); heals `55% × HealPower` of bonus dmg
- **`R` Finishing Cross:** `7.5u`, dash `16.0 + 0.16×AGI` u/s, mark `10.0 + 0.16×INT` s, `95/135 + 135% AD`, true-dmg exec `<= 20/24% + 25%×STR/(STR+70)`, CD `22/18s`

### Chain Laser Shooter *(Base: `480 HP +30/lvl`, `46 AD +3.5/lvl`, `Arm 8`, `MR 10`, `Regen 1.3/s`, `Move 4.00`, `Turn 560°/s`)*
- **Passive Chain & Stacks:** `6.0u` (`0.22s` wind / `0.66s` rec), hop `4.2 + 3.2×INT/(INT+60)u`, decay `0.70x` (`min 25%`), `6.0s` stacks (`halfPoint = 8`), `MaxAmp = 45% + 45%×INT/(INT+60)`
- **`Q` Extending Line Beam:** `1.2u → 8.2u` in `0.88s / sqrt(1 + 0.024 × INT)` | `44/62/80 + 105% AD` | `+12g`/extra kill, CD `7.2/6.5/5.8s`
- **`W` Resonant Shock:** `7.8u` scan, radius `2.40 + 1.85×INT/(INT+60)u` | `30/42/54 + 70% INT` | Single slow `20/24/28%`, overlap cap `82%`, overlap/refresh dur `1.15s × sqrt(n)`, CD `7.5/6.8/6.1s`
- **`E` Hologram Reposition:** `1.65u` animated step (`0.38 - 0.20×AGI/(AGI+50)s`), decoy `4.5s`/`120 HP` (`0.62u`), zap `34/48/62 + 65% INT`, rank-only stun `0.65/0.80/0.95s`, CD `11.0/10.1/9.2s`
- **`R` Overdrive Prism Beam:** `0.50s` windup, `2.4/2.7 + 2.0×INT/(INT+65)s` mobile channel, move `78% + 22%×AGI/(AGI+50)`, `62% AD` procs every `0.42s / EffectiveAS`, CD `28/24s`

---

## 5. Prototype Items & Effects (`LaneItems.Catalog`, `20` Items — Provisional)

Only **The Coward's Shield** (ID `12`) has a designer-authored concept (with provisional numbers); all other items, prices, tiers (`Starter` / `Core` / `Capstone`), and effects are provisional:

| ID | Name | Tier | Cost | Stats & Passive | Provenance |
| --- | --- | --- | --- | --- | --- |
| `14` | Woodland Salve | Starter | `175g` | `+70 HP`; Moss Mend: `+4.5 HP/s` out of hero combat `5.0s` | AI Provisional |
| `13` | Miner's Hatchet | Starter | `200g` | `+10 AD`; Cleaver: `+18` bonus physical dmg vs. minions | AI Provisional |
| `15` | Brawler's Buckler | Starter | `225g` | `+80 HP, +12 Arm`; Sturdy: blocks `6` dmg from minion hits & hero basics | AI Provisional |
| `3` | Vitality Vest | Starter | `250g` | `+160 HP, +1.4 Regen` | AI Provisional |
| `1` | Iron Greatsword | Core | `275g` | `+26 AD` | AI Provisional |
| `2` | Quickstep Dagger | Core | `275g` | `+30% AS` *(scales via `sqrt(1 + 2.4 × BonusAS)`)* | AI Provisional |
| `4` | Stone Plate | Core | `300g` | `+30 Arm` | AI Provisional |
| `5` | Null Mantle | Core | `300g` | `+30 MR` | AI Provisional |
| `6` | Chrono Band | Core | `300g` | `+22 AH` | AI Provisional |
| `8` | Titan's Knuckle | Core | `300g` | `+25 STR` *(pure scalar)* | AI Provisional |
| `9` | Zephyr Feather | Core | `300g` | `+25 AGI` *(pure scalar)* | AI Provisional |
| `10` | Arcane Lens | Core | `300g` | `+25 INT` *(pure scalar)* | AI Provisional |
| `7` | Courier Boots | Core | `325g` | `+0.80 MS` | AI Provisional |
| `11` | Kindred Charm | Core | `325g` | `+22% HealPower, +1.6 × HealPower HP/s` universal allied aura (`7.5u`) | AI Provisional (`v0.2.8` universal) |
| `16` | Blighted Thorn | Core | `375g` | `+16 AD, +12 Arm`; `-35%` healing/regen on hero hit for `3.5s` | AI Provisional |
| `12` | **The Coward's Shield** | Capstone | `450g` | `+15 Arm/MR, +12 AGI`; sustained retreat from recent hero attacker (`5.0s`) grants `+(20 + 0.85×AGI)` Arm/MR (`0.15s` commit, `1.55s` build, `0.75s` grace, `1.90s`/`0.85s` decay) | **Concept: Luís**; `v0.2.8` rework/numbers Provisional |
| `17` | Momentum Greaves | Capstone | `575g` | `+0.85 MS, +18 AGI`; moving out of combat builds `+(0.45 + 0.65×AGI/(AGI+45))` MS | AI Provisional |
| `18` | Colossus Pauldron | Capstone | `625g` | `+220 HP, +20 Arm, +20 STR`; inflicting Stun/KB or `<45% HP` grants `4s` `(65 + 2.0×STR)` shield (`12s` CD) | AI Provisional (`v0.2.8` wording) |
| `19` | Spellweaver's Hourglass | Capstone | `625g` | `+18 AH, +25 INT`; next hero hit within `5s` after cast deals `+(28 + 85% INT)` magic (`4.5s` CD) | AI Provisional |
| `20` | Executioner's Grudge | Capstone | `650g` | `+42 AD, +20% AS`; `+18%` damage vs. heroes `<40% HP` | AI Provisional |

---

## 6. Balance Experiments & Stress Results (`LaneBalanceHarness.cs` `v0.2.8`)

Run `Moba.LaneSlice.LaneEditor.RunBalanceExperiments` to write `prototype/Artifacts/balance_report_v028.txt`:
- **30 Hz Tick Resolution:** Confirms monotonic AS scaling up to `+1200% AS` (`5.46x`, `7.00/s` theoretical vs `6.00/s` simulated at `2+3` ticks/shot, `LaserRProc = 13.00/s` via accumulator loop) and Ogre AGI up to `400 AGI` (`3.13x`, `0.281s` → `9` ticks = `0.300s`).
- **Comparable-Gold Build Matrix & 12 Bot Matchups:** Verifies distinct identity tradeoffs across Early (`600g`), Mid (`1500g`), Late (`2400g`), and Extreme 6-slot builds; `11/12` `150s` bot matchups go full time (`Miravel vs LaserShooter` ends at `150s` via Red tower push; Swordsman wins `2-1` vs LaserShooter; Ogre goes `2-2` / `0-1`).
- **11 Stress Scenarios:** Miravel `0..6` Kindred Charm (`-1.30 → -4.73 HP/s` net enemy drain, `Q×HPwr×R` peak heal bounded `85.0 → 241.4`); Swordsman `0..150` INT W duration (`6.5s → 18.5s`, stays `< 26s` wave gap) & R execute (`24.0% → 41.0%`); Laser `4`/`12`/`20` clustered units under `[R]` (`464.7` → `952.5` → `1133.4` dmg on surviving `2040 HP` target, `0.009` → `0.059 ms/tick`); Ogre `0..150` STR W knockbacks (`2.72u → 7.33u`, `in-bounds=True`, `0` tunneling).

---

## 7. Unresolved Design Questions & Exposed `LaneTuning` Flags

Check [`docs/05_OPEN_QUESTIONS.md`](05_OPEN_QUESTIONS.md) for all open questions across Heroes 1–15, map/team format (`5v5` vs. `6v6`), trees vs. bushes, and engine direction (Unity trial vs. UE5). Six policy questions are exposed directly on `LaneTuning`:

- `ogreImmuneDuringRecovery = true`
- `ogreRStacksCursePerStrike = false`
- `swordsmanDisplaceCancelsSecondHit = true`
- `laserQTracksCursor = false`
- `laserWConsumesStacks = false`
- `laserRTracksCursor = false`
