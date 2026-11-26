# Development Checkpoint — Pause & Resume Guide

**Created:** 2026-11-26  
**Branch:** `main` (local `main` holds verified commits `c440e78`, `8bdf4af`, `9b8a8af`, plus checkpoint docs; **not yet pushed** to `origin/main`)  
**Shutdown Status:** Safe to close applications and shut down computer once the final status confirmation is shown (zero running `Unity`, `VBCSCompiler`, `dotnet`, or `WoodlandLane` processes; `prototype/Temp/UnityLockfile` absent).

---

## 1. Preserved Original Specification

- Complete verbatim 15-part specification preserved in: **[`docs/MAJOR_UPDATE_SPEC.md`](docs/MAJOR_UPDATE_SPEC.md)** (`1,943` lines, Parts 1–15 intact).

---

## 2. Completed & Verified Stages (Committed on `main`)

| Stage | Target Version | Commit Hash | Scope & Summary | Verification Status |
|---|---|---|---|---|
| Baseline | `v0.2.9` | `f47510f` | Static-manifest updater (`latest.json`), CI publish workflow, `scripts/package_windows_release.py` | Baseline on `origin/main` |
| **Stage A** | `v0.2.10` | `c440e78` | **Hero Guide Readability** (`LaneHeroSheet.cs`, `LaneHud.cs`, `LaneUpdater.cs`, `LaneNetChecks.cs`, `LaneMatchChecks.cs`): plain descriptions, key facts, separated `What Scales` arrows, expandable `Show Math`, practical tips; zero gameplay changes | `lane_checks_v0210.log`, `movement_checks_v0210.log`, `duel_checks_v0210.log` exited cleanly |
| **Stage B** | `v0.2.11` | `8bdf4af` | **Interactive Hero Guide & Ability Previews** (`LaneAbilityPreview.cs`, `LaneScene.cs`, `LaneHud.cs`, `LaneMatch.cs`, `LaneSettings.cs`, `LaneMatchChecks.cs`): isolated offscreen `RenderTexture` previews, relevant stat sliders, persisted `enableAbilityDemos` toggle | `lane_checks_v0211.log`, `movement_checks_v0211.log`, `duel_checks_v0211.log` exited cleanly |
| **Stage C** | `v0.2.12` | `9b8a8af` | **Shared Hero Resource System & Mana** (`LaneTypes.cs`, `LaneMatch.cs`, `LaneHeroes.cs`, `LaneHeroLaser.cs`, `LaneBot.cs`, `LaneHud.cs`, `LaneHeroSheet.cs`, `LaneAbilityPreview.cs`, `LaneResourceAndItemChecks.cs`, `LaneMatchChecks.cs`, `LaneNetChecks.cs`): `MaxMana` (Linear), `ManaRegen` (Linear active in/out of combat + `%Max`), `ResourceCostReduction` (Bounded diminishing `< 100%`), Flat/`%Max`/`%Current`/ContinuousPerSecond cost modes; Miravel (Passive & auto-Q 0 Mana, W/E rank flat, R continuous/s independent of INT halting cleanly at 0 Mana) & Chain Laser (Passive basics 0 Mana, Q high rank flat, W lower rank flat validated before spending, E constant flat, R constant upfront); Ogre & Swordsman Resourceless | `lane_checks_v0212.log`, `movement_checks_v0212.log`, `duel_checks_v0212.log` — **passed and read** |

### Verified `v0.2.12` Test Logs on Disk
- [`prototype/Artifacts/lane_checks_v0212.log`](prototype/Artifacts/lane_checks_v0212.log) (`35,830` bytes, Tundra build success, `Exiting batchmode successfully now!`):
  - `[LaneMatchChecks] All 21 deterministic checks passed.` (plus `12/12` `120s` `[BalanceDiagnostic]` matchups)
  - `[LaneResourceAndItemChecks] All Stage C Resource & Hero Mana checks passed.`
  - `[LaneNetChecks] All TCP loopback, UTP Fragmented+Reliable loopback, Relay, Static-Manifest Updater, and Update-Persistence checks passed.`
  - `[LaneEditor] All WoodlandLane automated checks PASSED.`
- [`prototype/Artifacts/movement_checks_v0212.log`](prototype/Artifacts/movement_checks_v0212.log) (`21,462` bytes, `Basic attack checks passed...`, `Movement Lab checks passed...`, `Exiting batchmode successfully now!`)
- [`prototype/Artifacts/duel_checks_v0212.log`](prototype/Artifacts/duel_checks_v0212.log) (`21,936` bytes, `All Duel checks passed.`, `Exiting batchmode successfully now!`)

### Not Yet Run Since `v0.2.9`
- Stage D–H item/recipe/passive/stress/UI checks (`LaneResourceAndItemChecks.cs` Parts 12.2–12.5).
- `Moba.LaneSlice.LaneEditor.RunBalanceExperiments` (`balance_report_v030.txt`).
- `v0.3.0` Windows build (`BuildWindows`), headless Host/Guest smoke test, `scripts/package_windows_release.py`, live Relay, live GitHub Release / Actions run, and human windowed playtesting.

---

## 3. Current Progress — Uncommitted Stage D Foundation (Preserved on Disk)

**IMPORTANT COMPILE NOTE:** The 4 files below were deliberately left **uncommitted** in the working tree so nothing verified on `main` (`9b8a8af`) is broken by partial work, and nothing written so far is lost:

1. `prototype/Assets/LaneSlice/Runtime/LaneItems.cs` **(untracked, preserved)** + `prototype/Assets/LaneSlice/Runtime/LaneItems.cs.meta` **(untracked, preserved)**
   - Implements `LaneItemCategory`, `ItemPassiveFlags`, `RecipeIngredient`, `ItemPassiveDoc`, `LaneItemDef`, `LaneItemIds`.
   - Implements `NormalizeEffectivePassives` (`FinalJudgment` supersedes `WoundedStrike`), `MigrateItemId` (maps legacy `1–18, 20`; maps `6 -> ClockworkPin`; explicitly retires `19` Spellweaver's Hourglass with **zero** Forgotten Hourglass replacement), `GetUpgradesInto`, and duplicate-aware recursive `ResolvePurchase` (prefers direct owned components before sub-recipes, allows `6/6` upgrades, preserves unrelated inventory slots).
   - Defines all **21 Approved Components (`101–121`)** and all **6 Approved Custom Advanced Items (`201–206`: Executioner's Axe, The Coward's Shield, Chalice of Life, Draconic Blade, The Giant's Heart, Banana Blade)** with **zero** Mana stats (`AddMaxMana`, `AddManaRegen`, `AddPercentManaRegen`, `AddResourceCostReduction` all `0f`) and monotonic component-to-upgrade stat progression.
   - Defines transitional non-Mana starters (`301–303, 305`), provisional `Clockwork Pin (314)` (keeping Stage C/existing AH tests valid), and provisional capstones (`401` Kindred Censer, `402` Blighted Thorn, `406` Momentum Greaves, `407` Colossus Pauldron).
2. `prototype/Assets/LaneSlice/Runtime/LaneTypes.cs` **(modified, preserved)**
   - Added `LaneEventKind.BananaCrescent = 31`, `DamageFlags`, `HealSourceKind`, Stage D/E `LaneTuning` passive formulas/constants, and lean `HeroSnap` telemetry (`deferredDamageTotal`, `heatStacks`, `heatTimer`, `adaptiveScalesStacks`, `adaptiveScalesBonus`, `firstStrikeReady`, `firstStrikeIdle`, `slipperyBonus`, `slipperyCd`).
   - Removed legacy `LaneItemTier`, `LaneItemDef`, `LaneItems`, `spellweaverEchoFormula`, `executionerGrudgeAmp`, `executionerGrudgeThreshold`, and `HeroSnap.spellweaverReady`.
3. `prototype/Assets/LaneSlice/Runtime/LaneUpdater.cs` **(modified, preserved)**
   - Bumped `BuildVersion = "v0.2.13"` for the intermediate Stage D+E milestone (use dotted numeric semver; Stage F–H bumps to `"v0.3.0"`).

### Current Issue / Workspace State Warning
- **Working tree does not currently compile in Unity** because callers (`LaneMatch.cs`, `LaneHeroes.cs`, `LaneHeroLaser.cs`, `LaneHud.cs`, `LaneHeroSheet.cs`, `LaneBot.cs`, `LaneMatchChecks.cs`, `LaneNetChecks.cs`, `LaneBalanceHarness.cs`) still reference removed symbols (`LaneItemTier`, `spellweaverEchoFormula`, `executionerGrudgeAmp`, `spellweaverReady`) and legacy item IDs `1..20`.
- If you want to test or play verified `v0.2.12` temporarily without losing Stage D work, run `git stash push -u -m "wip-stage-d"` (and later restore via `git stash pop`).

---

## 4. Remaining Tasks & Exact Next Steps on Resume

### Phase 1 — Finish Stage D & E (`v0.2.13`: Item/Passive Mechanics, UI & Tests)
1. **Wire Simulation (`LaneMatch.cs`, `LaneHeroes.cs`, `LaneHeroLaser.cs`, `LaneMinionsAndTowers.cs`):**
   - Update `TryBuyItem` to use `LaneItems.ResolvePurchase` (supports `6/6` slot replacement cleanly and logs net gold spent).
   - Aggregate `h.passiveFlags` via `LaneItems.NormalizeEffectivePassives` in `RecalcHeroStats`.
   - Implement **Giant's Might** (`AttackDamage += maxHp * giantsMightAdPerMaxHp` after `maxHp` calc), **Molten Edge** flat Armor & MR shred from `heatEntries`, and **Slippery Trickster** decaying MS bonus.
   - Fix latent bug in `DealDamageToHero`: call `RecalcHeroStats(target, preserveHpRatio: true)` when resetting `momentumFactor`/`momentumBonus`.
   - Extend `HealHero` with `HealSourceKind`: apply **Fountain of Life** (`source != LingeringRestoration`, plus natural regen in `TickSingleHero`) and **Lingering Restoration** (`AbilityOrItem` net HP restored * `0.28f` over `3.5s`, no overhealing stored, non-recursive).
   - Extend `DealDamageToHero` with `DamageFlags`: **Final Judgment** / **Wounded Strike**, per-attacker **Adaptive Scales** (max `6`, direct physical/magical hero hits only, refresh + 1-by-1 decay), **Ancient Vitality** (defer % by MaxHP, duration by STR, independent `DeferredDamageEntry` structs ticked evenly directly against HP with original attacker attribution, cleared on death, **retained** across combat exit and item sale), **Slippery Trickster** (enemy hero damage only, fixed `14.0s` CD, fixed `2.2s` linear decay, AGI burst), **Heat** (**Dragonfire** 0.5s magical DoT + **Molten Edge** shared stacks; `DamageFlags.OnHit` only on Miravel bolt, Swordsman Hit 1/2, Chain Laser primary basic hit), and **Banana Blade `CrescentStrike`** (`IsPointInBananaCrescent` geometric outer-minus-inner crescent behind primary target, Physical damage, AGI coeff, INT size, secondary targets only, emits `LaneEventKind.BananaCrescent`).
   - Update **Swordsman R** threshold execute (`LaneHeroes.cs`) to deal `enemy.maxHp * 4f + 999f` so execute stays lethal even with Ancient Vitality deferral.
   - Implement **First Strike** in `StartBasicWindup` & `TickSingleHero` (`firstStrikeIdleTimer` increments when not in basic windup/Twin-2nd; consumed & reset immediately on `StartBasicWindup` start to block cancel-spam exploits; shortens ONLY opening windup with `0.08s` raw / `0.065s` final floors).
   - Apply **Weightlessness** (`-25%` slow strength, full duration preserved) in **BOTH** `ApplySlowToHero` AND `ApplyLaserWSlow(HeroState ...)`.
   - Apply **Immovable Mountain** (`distance *= 1 - resist(STR)`, strictly `< 65%`) in `ApplyDisplacementToHero` (`prevMotorPos = moved` already prevents false Coward's Shield retreat triggers).
2. **Wire Visuals, Two-Pane Shop, Guide & Net (`LaneWorld.cs`, `LaneHud.cs`, `LaneHeroSheet.cs`, `LaneBot.cs`, `LaneNetChecks.cs`):**
   - `LaneWorld.cs`: render `LaneEventKind.BananaCrescent` golden crescent arc oriented behind target.
   - `LaneHud.cs`: add amber deferred-damage HP bar segment + concise status tags (`[HEAT xN]`, `[SCALES xN]`, `[DEFER N]`, `[1ST]`, `[SLIP]`); build readable two-pane Shop (`LaneItemCategory` filter tabs, Remaining Cost via `ResolvePurchase`, Approved/Provisional tags, Selected-item Fantasy/Stats/Named Passives/`What Scales`, clickable `Builds From`/`Builds Into`, expandable `Show Math`).
   - `LaneHeroSheet.cs`: replace removed `LaneItemTier` in `BuildItemTooltip`, update `RuleLines` for new Coward's Shield (`1200g`), Kindred Censer (`720g`), Colossus Pauldron (`1360g`), remove Spellweaver reference.
   - `LaneBot.cs`: update temporary starter/component/capstone IDs (no ID `19`) so bot builds remain valid in `v0.2.13`.
   - `LaneNetChecks.cs`: bump expected `BuildVersion` to `"v0.2.13"` and add snapshot roundtrip assertions for the new `HeroSnap` telemetry fields (verify payload stays well below `LaneWireCodec.MaxFrameBytes = 65536`).
3. **Update & Expand Tests (`LaneMatchChecks.cs`, `LaneResourceAndItemChecks.cs`) & Verify `v0.2.13`:**
   - Migrate `LaneMatchChecks.cs` item IDs to `LaneItemIds.*`, update `cowardShieldResistFormula` expected AGI from `12f` to `28f`, replace `Spellweaver's Hourglass` test with assertion that ID `19` does not exist, update Executioner test to Executioner's Axe.
   - Add ALL **Part 12.2** recipe/inventory/exploit tests and ALL **Part 12.3** custom passive tests to `LaneResourceAndItemChecks.cs`.
   - Run `Moba.LaneSlice.LaneEditor.RunAllChecks`, `Moba.MovementLab.MovementLabChecks.Run`, and `Moba.Duel.DuelEditor.RunAllChecks` (launch `Unity.exe` with `-PassThru` + polling loop, stop `VBCSCompiler` and `dotnet.exe` running `VBCSCompiler` after exit, read all 3 logs in full). Commit verified Stage D+E (`v0.2.13`).

### Phase 2 — Stage F (`v0.3.0`: Provisional Mana Catalogue & Bot Recipe Stepping)
- Add `304` **Mana Dew Vial** (`DewSip`), `311` **Sapphire Shard**, `312` **Dewdrop Charm**, `313` **Runed Sigil**, `315` **Wellspring Flask** (`Wellspring`), `403` **Astral Reservoir** (`Wellspring | AstralSurge`), `404` **Tidecaller Chalice** (`TidecallerFlow`), and `405` **Economist's Tome** (`FrugalFocus`) to `LaneItems.cs` (`IsApproved = false`; **never** add Mana stats to `101–121` or `201–206`, and **never** add Forgotten Hourglass).
- Wire those provisional Mana passives cleanly in `LaneHeroes.cs`/`LaneMatch.cs`.
- Upgrade `LaneBot.cs` to step through planned advanced item recipes component-by-component via `ResolvePurchase`.
- Bump `LaneUpdater.BuildVersion = "v0.3.0"` and update `LaneNetChecks.cs`.

### Phase 3 — Stage G (`v0.3.0`: Stress Tests & Upgraded Balance Harness)
- Add **Part 12.4** scaling stress checks (linearity, bounded DR limits, RCR `< 100%`, Immovable Mountain `< 100%`, First Strike 30 Hz quantization evaluation without altering global AS, dynamic Giant's Might MaxHP->AD, zero recursion/NaN) and **Part 12.5** UI/preview/recipe consistency tests to `LaneResourceAndItemChecks.cs`.
- Upgrade `LaneBalanceHarness.cs` to output `prototype/Artifacts/balance_report_v030.txt` (actual gold spent, First Strike 30 Hz audit, comparable-gold custom & cross-role & Mana builds, burst/sustained/retreat/Mana/lane scenarios, 12 matchups with completion times & OOM counts, extreme multi-passive stress).
- Run `RunAllChecks` + `RunBalanceExperiments`, read `balance_report_v030.txt` in full, and tune provisional numbers only if needed (never changing approved mechanics/curves).

### Phase 4 — Stage H (`v0.3.0`: Docs, Final Verification, Build, Packaging, Push & Part 15 Report)
- Create `docs/11_ITEMS_AND_RESOURCES.md` (strictly separating approved vs. provisional items + documenting all Part 13 edge-case decisions).
- Update `docs/01_CORE_SYSTEMS.md`, `docs/02_HERO_DESIGNS.md`, `docs/05_OPEN_QUESTIONS.md`, `docs/09_IMPLEMENTATION_REVIEW_GUIDE.md`, `AGENTS.md`, `README.md`, `prototype/README.md`, `docs/CURRENT_STATE.md`, and `LaneEditor.cs` (`HOW_TO_PLAY.txt`).
- Re-run all checks (`RunAllChecks`, `RunBalanceExperiments`, `MovementLabChecks.Run`, `DuelEditor.RunAllChecks`), run `BuildWindows`, run headless Host/Guest smoke test against `WoodlandLane.exe`, run `python scripts/package_windows_release.py` (`WoodlandLane-Windows.zip` + `latest.json` for `v0.3.0`), read every log completely, audit repo hygiene, commit & push `v0.3.0` to `origin/main`, and produce the full 13-point **Part 15 Final Implementation Report**.
