# Engine and Architecture — Proposed, Not Yet Implemented

The movement lab uses Unity as a trial. The engine decision for the standalone game is open. See [Current state](CURRENT_STATE.md) for the active milestone.

## Engine direction

A Unity `6000.6.0f1` project lives in `prototype/`. In the lab, the scene passes right-click destinations to a route planner for static obstacles and a motor that controls facing, travel speed, and braking. The HUD exposes provisional tuning and movement feedback while the course runs.

A previous discussion recommended **Unreal Engine 5**, using C++ for common systems and Blueprints/Data Assets for per-hero settings and content. Unreal's Gameplay Ability System (GAS) is a strong candidate for skills, attributes, effects and networking. However, the recommendation has **not yet been validated against the developer's computer, installed UE version, editor/toolchain, or iteration speed**. Reconfirm before implementation. Avoid assuming any exact engine version has already been installed.

## Intended architecture, later

- **Unit model:** all heroes, creeps, summons and illusions implement common teams, health/combat and targeting semantics.
- **Movement:** tune feel as a core system (rather than hiding it in every hero script).
- **Abilities:** active/passive slots; targeting/cooldowns/projectiles; temporary and permanent resources; modular effect composition.
- **Status effects:** stack/time/refresh/reapplication policies explicit; separate mechanics from visuals.
- **Attributes:** Strength/Agility/Intelligence as *scalars with no built-in universal bonuses*; formula evaluation driven by hero/ability/item data.
- **Combat:** damage type, mitigation, shields, secondary health pools, damage-over-time, healing, attack events, critical hits, knockback and crowd-control interactions.
- **World:** tree entities/destruction/vision/nav updates, ground area effects, possible brush, fog of war, terrain collision.
- **Economy:** item inventory and mostly-passive effects, gold/XP, support/jungle role instruments.
- **Multiplayer:** eventually server authoritative with correct ability ownership, prediction, server-side hit validation and replayable tests.

## Critical technical risks

1. Replicated deterministic/authoritative simulation for many units and ability interactions.
2. Game-specific weighted movement feel over potentially complex navigation.
3. Scaling performance with 6v6, creeps, summons and terrain updates.
4. Readable rendering/UI for Chibi eyes in darkness and four-form transitions.
5. Ally control and resurrection semantics.
6. Engine complexity vs solo/AI-assisted project maintainability.

## Prototype principle

No expensive infrastructure before validating the fun of core movement. Begin with one gray capsule, a top-down camera, simple terrain/obstacles, right-click movement, tunable speeds/turning, and logging/visual debug indicators. Use version control from the first implementation.
