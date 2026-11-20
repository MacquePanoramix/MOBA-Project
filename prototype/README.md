# Movement and Basic Attack Lab

## Run the lab

1. In Unity Hub, add this `prototype/` folder and open it with Unity `6000.6.0f1`.
2. Open `Assets/MovementLab/Scenes/MovementLab.unity` and press **Play**.
3. Right-click the course to move or the **Training dummy** to attack. Use **Quick turn** or **Deliberate turn** to select a movement profile. Press **R** to return to spawn with the current settings. **Restore baseline** resets the sliders for the selected profile.

For a standalone build, choose **Movement Lab → Build Windows Player** in the editor. Run `Builds/Windows/MovementLab.exe` once Unity finishes.

## Basic attack drill

1. Right-click the dummy to approach, face it, and attack once. Watch the attack phase and hit counter in the side panel.
2. After the hit, right-click a short step toward the edge of the range circle, staying just inside it. Right-click the dummy again while facing away. Repeat with the other movement profile to compare how long it takes to face the target before the same wind-up starts.
3. During **Wind-up**, right-click open ground. The capsule moves and the hit counter stays the same. Try again and wait for contact before clicking the ground. The hit stays counted and you can move immediately.

Both profiles use the same provisional attack values of 2.25 units of range, a 0.50-second wind-up, and a 0.35-second recovery. Edit these values on the scene component before pressing **Play**. Switching profiles or resetting the course clears the attack and hit counter.

The Unity editor checks passed for collision-safe approach, turning at both profile rates, both attack timers, one hit per order, and movement commands just before and after impact. The Windows player test used the normal mouse controls to approach and hit with both profiles, wind up again after stepping away in range, interrupt an approach with zero hits, and move after a landed hit while retaining the count. The interruption test lowered approach speed to give time to observe the result. Attack feel awaits Luís's playtest.

## Playtest course

Reset to **Spawn** and restore the baseline before each check. Click the ring beside the station name.

| Check | What to do and observe | Result |
| --- | --- | --- |
| Clear route | Right-click **Open**. The destination and route appear, and the capsule travels to the marker. | Passed in Windows player |
| Tree detour | Right-click **Detour** across the grove. The route and capsule go around the trees. | Passed in Windows player |
| Reversal and retargeting | Right-click **Detour**. While moving, right-click **Return** and watch how the capsule turns and changes speed. | Passed in Windows player |
| Settle at destination | Right-click **Near** and watch the full arrival. The capsule comes to rest and stays there. | Passed in Windows player |
| Invalid destination | Right-click **Blocked** in the grove. Look for blocked-click feedback. A blocked click during a move keeps the previous route. | Feedback observed in Windows player. Route retention passed in editor checks. |

## Compare tuning

Select **Quick turn** (300°/s) or **Deliberate turn** (150°/s) and run **Detour → Return**. Changing profiles returns the capsule to spawn for a repeatable comparison. Each profile keeps its own slider edits until the player closes. Both profiles start with the same speed, acceleration, braking, and turn slowdown angle. To tune a profile further, change one slider and repeat the route. The scene Inspector holds the editable baselines.

**Engineering evidence**

- Unity `6000.6.0f1` compiled the project, passed the isolated checks, and built the Windows x64 player. Re-run the checks with **Movement Lab → Run Movement Checks** in the editor.
- In the Windows player, right-clicks exercised the marked course with the baseline of 5 units/s, 8 units/s² acceleration, 12 units/s² braking, 300°/s turn rate, and an 85° turn slowdown angle. Changing the turn-rate slider to 119°/s and restoring the 300°/s baseline updated the HUD. The **R** key and **Reset course** button returned the capsule to spawn.
- In the updated player, Quick turn kept its edited braking while Deliberate turn kept its edited movement speed. Switching returned the capsule to spawn and cleared the route. Restoring Quick turn's baseline preserved Deliberate turn's edit. The isolated turning check measured 6° and 3° over the same 0.02-second step.

**Designer playtest:** On 2026-11-20, Luís said both tested turns fit and asked for substantial variation across units and heroes. The profile baselines are provisional and their assignments remain open.
