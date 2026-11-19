# Instructions for Codex and Other Coding Agents

You are collaborating on a long-term standalone MOBA with a human game designer. You can handle substantial coding and setup, but the designer remains the authority on game design, subjective movement feel, balance, scope, and final approval.

## Context and source of truth

- Start with `README.md` and `docs/CURRENT_STATE.md` for the active milestone, next action, and pending decisions. Before planning implementations, read `docs/00_GAME_VISION.md`, `docs/01_CORE_SYSTEMS.md`, and `docs/02_HERO_DESIGNS.md`.
- **User-authored mechanics** in `docs/02_HERO_DESIGNS.md` are design intent, not final numerical specs. Do not silently alter them.
- Keep proposals, temporary prototype settings, implemented behavior, and designer-approved decisions distinct. Update the owning design document and `docs/05_OPEN_QUESTIONS.md` when the designer resolves a question.
- `docs/06_FIRST_CODEX_TASK.md` and `docs/07_CONVERSATION_HANDOFF.md` are historical context. Take the active scope from `docs/CURRENT_STATE.md`.
- If a detail is necessary for a minimal test, choose a clearly labeled, editable placeholder. Preserve a baseline when comparing movement changes so the designer can try both versions.

## Working style

- Prefer small, playable, testable milestones rather than a giant speculative codebase.
- Be concise and readable. Explain one actionable step at a time when the user needs to interact with an editor or install software.
- Show proposed changes, which files they affect, and how to verify them.
- Favor modular, data-driven design with clean interfaces, automated tests for pure logic, and documented assumptions.
- Keep future multiplayer/server authority in mind but do not implement the full multiplayer stack for the first prototype.
- Do not purchase assets, subscriptions, paid plugins, or provision paid hosting without explicit approval.
- Never copy copyrighted game assets/code from LoL or Dota; implement original mechanics/assets and use placeholders for prototyping.

## Workspace and review

- Check the working tree before editing. Preserve human changes and stage only the intended files. Use an isolated worktree when broad changes or validation could put existing work at risk. Avoid force pushes, destructive deletions, or unrelated repository changes.
- Keep shared instructions and settings portable. Keep credentials, personal paths, and machine-specific settings out of tracked files.
- Use independent review when the impact justifies it, especially for data loss, migrations, or future multiplayer authority. Give each reviewer a distinct failure mode, the relevant constraints, and the evidence they need.

## Evidence and handoff

- State what happened at each relevant level: implemented, compiled, tested in isolation, run through the normal player entry point, and accepted by the designer. Report failures and checks still pending.
- Read results before recording a pass. Verify the run exercised the intended behavior and note differences between the test setup and playable defaults. For movement, record the tuning settings and repeat the same course through the playable prototype.
- Record reproducible steps and results with the active milestone. Update `docs/CURRENT_STATE.md` when progress, evidence, or the next action changes so a fresh chat can resume from the files.

## Provisional technical direction

The engine choice is open. Unreal Engine 5 with C++/Blueprints and GAS was proposed in the original handoff. Check the exact version, installed tools, and designer preference before creating engine-specific files. A trial can inform the long-term engine decision.

## Current focus: movement laboratory

The first deliverable is the [Movement Lab](prototype/README.md), a small top-down playable prototype with right-click movement, navigable obstacles/trees, configurable turning and movement feel, and visible debugging feedback. Keep heroes, the item shop, minions, networking, art polish, and a full map outside this milestone. Ask only essential setup questions when truly blocked. Use a marked placeholder for reversible test details.
