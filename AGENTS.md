# Instructions for Coding Agents

You are collaborating on a long-term standalone MOBA with Luís (the human game designer). You can handle substantial coding and setup, but Luís remains the authority on game design, subjective movement feel, balance, scope, and final approval. **Quality comes before speed.**

## Context and source of truth

- Start with `README.md` and `docs/CURRENT_STATE.md` (`## Start here`, `## Holds, provisional settings, and known red`, and the top dated log entry). Before planning implementations, read `docs/00_GAME_VISION.md`, `docs/01_CORE_SYSTEMS.md`, and `docs/02_HERO_DESIGNS.md`.
- **User-authored mechanics** in `docs/02_HERO_DESIGNS.md` are design intent, not final numerical specs. Do not silently alter them.
- Keep proposals, temporary prototype settings, implemented behavior, and designer-approved decisions distinct. Never silently promote an Open/Provisional choice to Locked. Update the owning design document and `docs/05_OPEN_QUESTIONS.md` when Luís resolves a question.
- `docs/06_FIRST_CODEX_TASK.md` and `docs/07_CONVERSATION_HANDOFF.md` are historical context. Take the active scope from `docs/CURRENT_STATE.md`.
- If a detail is necessary for a minimal test, choose a clearly labeled, editable placeholder. Preserve a baseline when comparing movement changes so Luís can try both versions.

## Working style

- Prefer small, playable, testable milestones rather than a giant speculative codebase.
- Keep responses concise and readable. Explain one actionable step at a time when Luís needs to interact with an editor or install software.
- Show proposed changes, which files they affect, and how to verify them.
- Favor modular, data-driven design with clean interfaces, automated tests for pure logic, and documented assumptions.
- Keep future multiplayer/server authority in mind without building a full matchmaking or multi-hero framework ahead of the active milestone.
- Do not purchase assets, subscriptions, paid plugins, or provision paid hosting without explicit approval.
- Never copy copyrighted game assets/code from LoL or Dota; implement original mechanics/assets and use placeholders for prototyping.

## Workspace and review

- **Protect human and in-progress workspace state** ("unfinished does not mean disposable"). Check the working tree before editing; never reset, clean, overwrite, or discard uncommitted modifications without explicit permission. Use an isolated worktree when broad changes or validation could put existing work at risk.
- Stage by explicit path only (`git add <file> ...`; never `git add -A` or `git add .`). Avoid force pushes, destructive deletions, or unrelated repository changes.
- Move obsolete local scratch/temp folders to the Recycle Bin rather than deleting them permanently, and only after verifying they belong to the current task and hold no uncommitted or unpushed work.
- Keep shared instructions and settings portable. Keep credentials, personal user-profile paths (including inside exported assets such as Blender FBXs), and machine-specific settings out of tracked files.
- Use consequence-proportional independent review when the impact justifies it (especially data loss, migrations, or multiplayer authority). Give each reviewer a distinct failure mode, do not tell it what was hoped, and require it to report what it was shown. `"No finding"` is a valid result; reviewers of the same model agreeing count as one opinion.

## Evidence and handoff

- Name the exact rung reached: `written → compiled → tested on a rig → tested as played → seen by designer → accepted`. Keep five results distinct: `not run`, `did not finish`, `failed`, `passed not read`, and `passed and read`.
- A bench/rig result is a claim about that rig until run through the normal player entry point with playable defaults; state what the rig pinned or left out. For movement, record tuning settings and repeat the same course through the playable prototype.
- A pass counts only if the run shows the intended behavior was exercised (not silently skipped or timed out). Key checks/detectors earn trust after being shown to fail on a known-bad case.
- **Read, then write:** read test and build logs to the end before recording a pass or committing. Never chain a commit behind a test.
- Keep `docs/CURRENT_STATE.md` current whenever progress, evidence, or the next action changes: rewrite `## Start here` in place, maintain one `## Holds, provisional settings, and known red` list, and prepend dated entries newest-first below.

## Provisional technical direction

The engine choice remains open. Unity `6000.6.0f1` lives in `prototype/` for early gameplay trials; Unreal Engine 5 with C++/Blueprints and GAS was proposed earlier for long-term evaluation. Check installed tools and Luís's preference before committing to a long-term engine.

## Current focus

Take the active scope from `docs/CURRENT_STATE.md` and `prototype/README.md`. Currently: preserve the `MovementLab` baseline (`MovementLab.unity`) while finishing and playtesting the 1v1 `WoodlandDuel` prototype (`WoodlandDuel.unity`) before adding waves, itemization, or full-map systems. Ask only essential setup questions when truly blocked, and keep reversible test values clearly labeled.
