# Development Checkpoint — Pause & Resume Guide (Completed at `v0.3.0`)

**Created:** 2026-11-26  
**Completed:** 2026-11-26 (`v0.3.0`, all Stages A–H finished and verified on `main`)  
**Status:** Resumed cleanly from the `v0.2.12` checkpoint, finished Stage D+E (`v0.2.13`, `819fa68`) and Stages F–H (`v0.3.0`), ran all automated checks, balance experiments, Windows build, packaging, and headless loopback smoke test, and prepared `v0.3.0` for GitHub Release publication and human playtesting.

---

## 1. Preserved Original Specification

- Complete verbatim 15-part specification preserved in: **[`docs/MAJOR_UPDATE_SPEC.md`](docs/MAJOR_UPDATE_SPEC.md)** (`1,943` lines, Parts 1–15 intact).
- Item, recipe, resource, and Part 13 technical decision documentation added in: **[`docs/11_ITEMS_AND_RESOURCES.md`](docs/11_ITEMS_AND_RESOURCES.md)**.

---

## 2. Completed & Verified Stages (`main`)

| Stage | Target Version | Commit / Milestone | Scope & Summary | Verification Status (`passed and read`) |
|---|---|---|---|---|
| Baseline | `v0.2.9` | `f47510f` | Static-manifest updater (`latest.json`), CI publish workflow, `scripts/package_windows_release.py` | Baseline (`lane_checks_v029.log`, `movement_checks_v029.log`, `duel_checks_v029.log`) |
| **Stage A** | `v0.2.10` | `c440e78` | **Hero Guide Readability** (`LaneHeroSheet.cs`, `LaneHud.cs`, `LaneUpdater.cs`, `LaneNetChecks.cs`, `LaneMatchChecks.cs`): plain descriptions, key facts, separated `What Scales` arrows, expandable `Show Math`, practical tips; zero gameplay changes | `lane_checks_v0210.log`, `movement_checks_v0210.log`, `duel_checks_v0210.log` |
| **Stage B** | `v0.2.11` | `8bdf4af` | **Interactive Hero Guide & Ability Previews** (`LaneAbilityPreview.cs`, `LaneScene.cs`, `LaneHud.cs`, `LaneMatch.cs`, `LaneSettings.cs`, `LaneMatchChecks.cs`): isolated offscreen `RenderTexture` previews, relevant stat sliders, persisted `enableAbilityDemos` toggle | `lane_checks_v0211.log`, `movement_checks_v0211.log`, `duel_checks_v0211.log` |
| **Stage C** | `v0.2.12` | `9b8a8af` | **Shared Hero Resource System & Mana**: `MaxMana`, active `ManaRegen`, bounded `ResourceCostReduction` (`< 100%`), Flat/`%Max`/`%Current`/`ContinuousPerSecond` cost modes; Mana on Miravel & Chain Laser Shooter; Ogre & Swordsman Resourceless | `lane_checks_v0212.log`, `movement_checks_v0212.log`, `duel_checks_v0212.log` |
| **Stage D+E** | `v0.2.13` | `819fa68` | **Recipe Engine, 21 Approved Components & 6 Approved Custom Advanced Items** (zero Mana stats, legacy ID `19` retired with zero Forgotten Hourglass), two-pane Shop UI, Banana Crescent VFX, deferred-damage bar, Part 12.2–12.3 checks | `lane_checks_v0213.log`, `movement_checks_v0213.log` (`348` lines), `duel_checks_v0213.log` (`361` lines) |
| **Stages F–H** | **`v0.3.0`** | **`v0.3.0` release commit** | **8 Provisional Mana Items, Recipe Bot Stepping, Part 12.4–12.5 Checks, `balance_report_v030.txt`, Documentation (`11_ITEMS_AND_RESOURCES.md`), Build, Package & Smoke Test** | `lane_checks_v030_final.log` (`569` lines, exit `0`), `balance_v030.log` (`522` lines, exit `0`), `balance_report_v030.txt` (`132` lines), `movement_checks_v030.log` (`346` lines, exit `0`), `duel_checks_v030.log` (`355` lines, exit `0`), `build_windows_v030.log` (`4546` lines, exit `0`), `smoke_host_v030.log` (`88` lines) & `smoke_guest_v030.log` (`91` lines), ZIP `36,978,165` bytes (`sha256: f7ccade695100fac146e737596c1113012c5ffc02d35b4fea7c30e51965972cb`) |

---

## 3. Not Yet Run (Pending Human / Live Release)

- Live GitHub Release `v0.3.0` publication (`MacquePanoramix/MOBA-Project`) uploading `prototype/Builds/WoodlandLane-Windows.zip` and live GitHub Actions `Publish Static Update Manifest` execution (`latest.json` auto-publication).
- Live friend download / static-manifest auto-update verification (`v0.2.9 -> v0.3.0`).
- Live Unity Cloud Relay playtest, windowed interactive Hero Guide / Shop / HUD review, and designer balance sign-off on all provisional item/Mana numbers (`docs/11_ITEMS_AND_RESOURCES.md`).
