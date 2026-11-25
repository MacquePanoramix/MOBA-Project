# Ultimate Free MOBA *(Provisional Name)*

A standalone, slower-paced, hero-based MOBA concept inspired by early Dota 2 movement/trees and League of Legends' skillshot-heavy combat and more passive-focused itemization (`MOBA Project` is the repository folder name; `Woodland Lane` is the current 1v1 prototype battlefield).

The playable prototype in [`prototype/`](prototype/) uses Unity `6000.6.0f1` for movement trials (`MovementLab`) and a networked 4-hero 1v1 vertical slice (`WoodlandLane`, `v0.2.9`) with hero-specific control profiles, optional RTS multi-unit controls, customizable context-aware keybindings, and a static-manifest zero-API-rate-limit Windows updater.

## Start here

1. Read the [working agreement](AGENTS.md) and [current state](docs/CURRENT_STATE.md).
2. Before planning implementation, read the [game vision](docs/00_GAME_VISION.md), [core systems](docs/01_CORE_SYSTEMS.md), and [hero concepts](docs/02_HERO_DESIGNS.md).
3. For an independent design or code review distinguishing approved decisions from provisional prototype formulas/items, read the [implementation review guide](docs/09_IMPLEMENTATION_REVIEW_GUIDE.md).
4. For release packaging, static-manifest hosting, and transitioning players onto the `v0.2.9` updater, read the [release and updater guide](docs/10_RELEASE_AND_UPDATER.md).
5. Open [`prototype`](prototype/) in Unity Hub and use the [lab guide](prototype/README.md) to run the lane vertical slice or movement lab.

## Project records

- [Engine and architecture](docs/03_ENGINE_AND_ARCHITECTURE.md) captures the earlier engine proposal and longer-term technical ideas.
- [Roadmap](docs/04_ROADMAP.md) defines the playable milestones.
- [Open questions](docs/05_OPEN_QUESTIONS.md) tracks unresolved design and production choices.
- The [original starter prompt](docs/06_FIRST_CODEX_TASK.md) and [conversation handoff](docs/07_CONVERSATION_HANDOFF.md) preserve the project's history.

Luís owns the game's design and accepts or revises the feel after playtesting. The hero concepts describe his intended mechanics, with balance values, visual direction, and team size still open.
