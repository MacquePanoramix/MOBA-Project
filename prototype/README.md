# Movement Lab v0.01

**Status:** Planned. Launch instructions are TBD until there is a runnable prototype. See [current state](../docs/CURRENT_STATE.md) and the [roadmap](../docs/04_ROADMAP.md).

Build one top-down course with a controllable capsule, open ground, and static tree-like obstacles. Right-click sets a destination. Show the destination, planned route, capsule facing, and movement status in the lab. Expose movement speed, turn rate, and stopping behavior as editable prototype settings.

## Playtest course

Start each check from the same marked spawn with the default tuning. Use marked destinations so another person can repeat the clicks.

| Check | What to do and observe | Result |
| --- | --- | --- |
| Clear route | Right-click a marker across open ground. The destination and route appear, and the capsule travels to the marker. | Unrun |
| Tree detour | Click a reachable marker across a tree cluster. The route and capsule go around the cluster without crossing an obstacle. | Unrun |
| Reversal and retargeting | While moving, click a marker behind the capsule. The visible target and route update, and the capsule turns toward the new route. | Unrun |
| Settle at destination | Click a nearby marker and watch the full arrival. The capsule comes to rest and stays there. | Unrun |
| Invalid or unreachable target | From rest, click a blocked or disconnected spot. Feedback shows the click outcome, and the capsule stays outside the obstacle. | Unrun |

## Compare tuning

Run the same course with the default settings, then rerun it with one setting changed. Record both settings and compare how quickly the capsule responds to a new click, how much commitment a turn feels like, and how the capsule settles. Repeat for each setting you want to compare. Treat tuning values as provisional.

**Engineering evidence:** Record the build, settings, check result, and a capture or log if available. Flag any checks left unrun.

**Designer playtest:** Luís judges whether the movement feels weighted and still responsive, and decides which tuning to keep or revisit. Feedback pending.
