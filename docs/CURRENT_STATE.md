# Current state

## Start here

**Active milestone:** **Milestone 2 → 3 — Woodland Duel (1v1 graybox encounter)** in `prototype/Assets/Duel/Scenes/WoodlandDuel.unity` and Windows player `prototype/Builds/WindowsDuel/WoodlandDuel.exe`. Preserves the `MovementLab.unity` baseline alongside it.

**Next action:** Luís reviews the [working agreement](../AGENTS.md) upgrades, runs `prototype/Builds/WindowsDuel/WoodlandDuel.exe` in normal windowed play (testing Solo Practice from both Ogre and Luís/Miravel sides, plus optional Friend Duel Host/Join), decides whether to keep `"Miravel"` or rename to `"Luís"`, gives spacing/feel feedback, and approves committing the checkpoint.

**Current rung reached (`WoodlandDuel`):** **Tested on a rig + headless loopback smoke test passed and read (`2026-11-22`).** Automated simulation/connection checks, Movement Lab regression, Windows build (`WoodlandDuel.exe`), and headless Host/Guest handshake all passed and were read to the end. Normal rendered windowed play and designer feel evaluation are **not yet run**.

**Roster & research context:** All twelve hero concepts are now captured in [hero concepts](02_HERO_DESIGNS.md), with matchup priorities in [the duel proposal](04_ROADMAP.md#first-playable-match-proposal). Early Tortuga modeling and woodland-fork lane notes remain ready in [Tortuga and first lane research](08_TORTUGA_AND_FIRST_LANE.md).

## Holds, provisional settings, and known red

- **Provisional UI hero label (`"Miravel"` vs. `"Luís"`):** `DuelNames.cs` currently displays Hero Luís as `"Miravel"` in the Woodland Duel UI/logs (`Guest, Miravel`). Kept as a reversible label until Luís picks a preference.
- **Provisional Woodland Duel tuning & kit subset:** `DuelMatch.cs` uses editable placeholder values in `DuelTuning` (Ogre 140 HP, speed 4.4, turn 165°/s, Q Slam 0.82s wind-up / 0.60s stun, W Sweep 0.32s wind-up / 2.1 shove; Luís 105 HP, speed 5.0, turn 300°/s, basic 0.50s wind-up / 13 dmg, orb 14 heal / 17 dmg). Only Ogre's passive + Q/W and Luís's basic attack + Q orb are implemented in this slice; E/R abilities remain deferred until base spacing/denial is playtested.
- **Movement Lab baseline preserved:** `MovementLab.unity` and `Builds/Windows/MovementLab.exe` remain intact for baseline movement and basic-attack comparisons.
- **Long-term engine choice open:** Unity `6000.6.0f1` is used in `prototype/` for gameplay trials; Unreal Engine 5 (C++/GAS) remains an unverified long-term candidate in [engine and architecture](03_ENGINE_AND_ARCHITECTURE.md).
- **Playtest & visual check gap:** `WoodlandDuel.exe` has only executed automated checks and `-batchmode -nographics` Host/Guest loopback startup; rendered visuals, shader appearance, camera framing, and live mouse/ability feel await Luís's windowed playtest.

## Progress log

**2026-11-22 — Woodland Duel completed, 4 new hero designs added, and workflow upgraded.**
- **Twelve hero designs recorded:** Added Heroes 9–12 (**Bomb Planter**, **Automatron Bot 3000**, **Chain Laser Shooter**, **Multiple Phases Raid Boss Hero**) to [hero concepts](02_HERO_DESIGNS.md), [game vision](00_GAME_VISION.md), [roadmap](04_ROADMAP.md), and [open questions](05_OPEN_QUESTIONS.md). Kept Ogre vs. Hero Luís (#1) and Ogre vs. Old Man Swordsman (#2) as first 1v1 matchups; Chain Laser Shooter and Bomb Planter pair best once lane waves/walls exist (Milestone 4).
- **Workflow & guidelines updated:** Adopted lightweight workspace protection, exact evidence rungs/outcomes, consequence-proportional independent review rules, and structured `CURRENT_STATE.md` handoff conventions from *The Wanderer* into [AGENTS.md](../AGENTS.md).
- **Woodland Duel finished (`prototype/Assets/Duel/`):** Implemented [DuelWorld.cs](../prototype/Assets/Duel/Runtime/DuelWorld.cs) (oblique 52° orthographic camera, `DuelField` shader loading from `Resources`, floating orb crystal + pulse/denial burst rings, exact-geometry Slam rectangle and Sweep sector telegraphs, Ogre CC-immunity halo, Luís basic wind-up ring, and move marker) and [DuelEditor.cs](../prototype/Assets/Duel/Editor/DuelEditor.cs) (`Duel/Run Match Checks`, `Duel/Run All Checks`, `Duel/Build Windows Player`), plus minor cleanup in `DuelHud.cs` and `DuelWorld.Heroes.cs`.
- **Verification (`2026-11-22`, run outside sandbox for Unity licensing/Mono IPC & loopback sockets; logs read to the end):**
  - `Moba.Duel.DuelEditor.RunAllChecks` → **passed and read** (`prototype/Artifacts/unity-duel-checks.log`, exit code 0: `Duel match checks passed`, `Duel connection checks passed`, `All Duel checks passed.`).
  - `Moba.MovementLab.MovementLabChecks.Run` → **passed and read** (`prototype/Artifacts/unity-movement-regression.log`, exit code 0: `Basic attack checks passed`, `Movement Lab checks passed`).
  - `Moba.Duel.DuelEditor.BuildWindows` → **passed and read** (`prototype/Artifacts/unity-duel-build.log`, exit code 0: built `prototype/Builds/WindowsDuel/WoodlandDuel.exe`).
  - Headless loopback smoke test (`WoodlandDuel.exe -batchmode -nographics -duelHost 127.0.0.1` + `-duelJoin 127.0.0.1`) → **passed and read** (`duel-smoke-host.log`: `Duel round 1 started (Host, Ogre).`; `duel-smoke-guest.log`: `Duel round 1 received (Guest, Miravel).`; zero unhandled exceptions). Note: `-batchmode -nographics` validates startup, round creation, and TCP snapshot exchange, not rendered output or manual play feel.
- Left changes uncommitted as a clean reviewable checkpoint for Luís.

**2026-11-20 — Ready to start a playable encounter.** Luís played the basic attack build, said it looked good, and asked when work on the game itself could begin. Use the current controls as a working baseline. Per-unit attack timing and interruption rules remain open. The [attack drill](../prototype/README.md#basic-attack-drill) is available for comparison if the controls change.

**2026-11-20.** Luís tried the movement lab and liked both the quicker and more deliberate turns. He wants units and heroes to vary substantially in how they move. The Windows lab lets him select **Quick turn** or **Deliberate turn** and tune each independently on the same course. Both profiles start with the same settings except for turn rate.

**2026-11-19.** Luís approved trying Unity for the first movement lab. The first Windows player built and passed the mouse-driven course checks. Open `prototype/` with Unity `6000.6.0f1` or run the Windows player, then follow [the lab guide](../prototype/README.md) to compare tuning and record Luís's feedback.

The trial uses a flat course, static circular obstacles and a custom motor. The baseline uses placeholder values so we can compare turning and braking on repeatable routes. The long-term engine choice remains open. A future small networked test can check whether movement and a topology change stay responsive and consistent under latency.

The lab uses static trees and a stationary target. Record technical results in the lab guide. The [hero concepts](02_HERO_DESIGNS.md) and [open questions](05_OPEN_QUESTIONS.md) govern later design work.

## Workflow adopted from The Wanderer

- *The Wanderer*'s `AGENTS.md`, `Docs/Handoff.md`, and `Docs/CURRENT_STATE.md` use a short resume path into the design, current work, and evidence, one consolidated holds list, and strict evidence rungs. The MOBA project adopts those practices directly in [AGENTS.md](../AGENTS.md) and this file.
- Its validation practice distinguishes a rig result, a normal play run, and the designer's judgement, and requires reading full logs before writing results or committing.
- Its Claude-Code-specific `.claude/settings.json`, token-quota guards, and shell hooks stay specific to that setup; the MOBA working agreement captures the portable rules in repository docs instead.
