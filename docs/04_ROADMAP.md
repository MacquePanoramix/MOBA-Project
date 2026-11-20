# Roadmap — Small, Playable Milestones

## Milestone 0 — Validate the toolchain and project skeleton

- [x] Confirm Unity `6000.6.0f1` for the movement lab trial. The full-game engine decision remains open.
- [x] Inspect the installed tools and verify the build on Windows.
- [x] Create the Unity project inside `prototype/`.
- [x] Initialize Git, ignore generated files, and verify the Windows player launch.
- [x] Document the editor and Windows player launch steps in the [lab guide](../prototype/README.md).

**Done when:** local project opens reliably and source is versioned.

## Milestone 1 — Movement Lab v0.01

- [x] Fixed top-down camera.
- [x] Right-click movement with pathfinding around static tree-like obstacles.
- [x] Editable movement speed, turning rate, and stop behavior.
- [x] Visible path, destination, facing, and status feedback.
- [x] Repeatable test course with a restorable tuning baseline.
- [x] Designer playtest and movement feel feedback. Both tested turn rates fit the intended range.

**Done when:** human can run a tiny sandbox and report whether movement feels weighted enough.

## Milestone 2 — Basic combat laboratory

- [ ] Two targetable graybox heroes, teams, HP and respawn.
- [ ] Basic attacks, attack wind-up and backswing.
- [ ] One projectile skillshot, simple collision, short stun/root, damage and healing.
- [ ] Test lightweight attribute scaling formulas and unit tests.

**Done when:** tiny 1v1 graybox combat supports repeated tuning.

## Milestone 3 — First hero vertical slice

- [ ] Implement a simple, representative hero subsystem, possibly Tortuga shell damage splitting and transformation (not automatically committed).
- [ ] Test one interactable tree, alternate ability set, a couple of status effects.
- [ ] Keep ability values configurable and test transition edge cases.

**Done when:** a hero demonstrates unique identity without one-off hacks in all game systems.

## Milestone 4 — One lane and game economy

- [ ] Basic creeps, towers, gold/XP, item/stat pipeline.
- [ ] Basic support item and jungle camp proof of concept.
- [ ] Hero role and scaling validation.

## Milestone 5 — Map, vision, multiplayer proof of concept

- [ ] Fog/trees/bushes prototype, map architecture, objective/resource economy.
- [ ] Minimal authoritative networked interactions, latency tests.
- [ ] Profile creep/summon counts, performance.

## Milestone 6 — 5v5 or 6v6 alpha

- [ ] Choose team size based on tested economy/travel/role constraints.
- [ ] Expand map/jungle, test sixth-player strategies if 6v6 chosen.
- [ ] Implement remaining roster iteratively and test for meaningful counterplay.

**Do not skip ahead:** Milestone 1 movement feel is the immediate priority.
