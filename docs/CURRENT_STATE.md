# Current state

## Start here

**2026-11-19.** The movement lab is the first playable milestone. The design handoff is in place, and [the lab guide](../prototype/README.md) defines the test course. No runtime project has been created and the course is unrun.

Choose a trial runtime before adding engine files. Unity `6000.6.0f1` is the lowest-setup candidate found locally and matches the editor version recorded in The Wanderer. The executable is present. Editor launch, build, and play mode are still unverified for the MOBA. No Unreal or Godot editor turned up in the local checks, and Unreal's C++ toolchain was not verified. The trial should show how quickly we can tune turning and stopping, inspect a route, and repeat the same course. The long-term engine choice remains open.

The lab scope is right-click movement around static trees, adjustable movement and turning, and enough on-screen feedback to debug a route. Record technical results in the lab guide. Luís decides whether walking, turning, and stopping feel right. The [hero concepts](02_HERO_DESIGNS.md) and [open questions](05_OPEN_QUESTIONS.md) still govern later design work.

## Workflow adopted from The Wanderer

- The Wanderer's `AGENTS.md`, `Docs/Handoff.md`, and `Docs/CURRENT_STATE.md` use a short resume path into the design, current work, and evidence. Here, the [working agreement](../AGENTS.md) and this page provide that path. The [lab guide](../prototype/README.md) holds the milestone plan, launch steps when available, and evidence.
- Its validation practice distinguishes a rig result, a normal play run, and the designer's judgement. The MOBA uses the same distinction and calls for independent review where the risk warrants it.
- Its shared `.claude/settings.json` sets a 250,000-token compaction window. Its local settings add an outlook at startup, resume, or compaction, a periodic reminder based on tool calls, and a shell command guard. The MOBA keeps continuity in repository docs and updates them when the next action or evidence changes. The Wanderer's model, quota thresholds, and Claude hooks are specific to that setup.
- The reference project uses local Git checks for profile paths. Approval for those checks and the proposed per-edit guard applies to The Wanderer. The MOBA working agreement covers portable files, careful staging, and protecting human changes.
