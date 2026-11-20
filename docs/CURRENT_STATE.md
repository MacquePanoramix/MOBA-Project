# Current state

## Start here

**Basic attack trial.** The updated Windows player uses a stationary dummy and the same provisional range, wind-up, and recovery for both movement profiles. Editor checks and a mouse-driven Windows smoke test passed. Run the [attack drill](../prototype/README.md#basic-attack-drill) to compare turning and interruption feel. Luís decides what to keep after playing it.

**2026-11-20.** Luís tried the movement lab and liked both the quicker and more deliberate turns. He wants units and heroes to vary substantially in how they move. The updated Windows lab lets him select **Quick turn** or **Deliberate turn** and tune each independently on the same course. Both profiles start with the same settings except for turn rate. The next milestone can test how those profiles feel with a basic attack wind-up.

**2026-11-19.** Luís approved trying Unity for the first movement lab. The first Windows player built and passed the mouse-driven course checks. Open `prototype/` with Unity `6000.6.0f1` or run the Windows player, then follow [the lab guide](../prototype/README.md) to compare tuning and record Luís's feedback.

The trial uses a flat course, static circular obstacles and a custom motor. The baseline uses placeholder values so we can compare turning and braking on repeatable routes. The long-term engine choice remains open. A future small networked test can check whether movement and a topology change stay responsive and consistent under latency.

The lab uses static trees and a stationary target. Record technical results in the lab guide. The [hero concepts](02_HERO_DESIGNS.md) and [open questions](05_OPEN_QUESTIONS.md) govern later design work.

## Workflow adopted from The Wanderer

- The Wanderer's `AGENTS.md`, `Docs/Handoff.md`, and `Docs/CURRENT_STATE.md` use a short resume path into the design, current work, and evidence. Here, the [working agreement](../AGENTS.md) and this page provide that path. The [lab guide](../prototype/README.md) holds the milestone plan, launch steps when available, and evidence.
- Its validation practice distinguishes a rig result, a normal play run, and the designer's judgement. The MOBA uses the same distinction and calls for independent review where the risk warrants it.
- Its shared `.claude/settings.json` sets a 250,000-token compaction window. Its local settings add an outlook at startup, resume, or compaction, a periodic reminder based on tool calls, and a shell command guard. The MOBA keeps continuity in repository docs and updates them when the next action or evidence changes. The Wanderer's model, quota thresholds, and Claude hooks are specific to that setup.
- The reference project uses local Git checks for profile paths. Approval for those checks and the proposed per-edit guard applies to The Wanderer. The MOBA working agreement covers portable files, careful staging, and protecting human changes.
