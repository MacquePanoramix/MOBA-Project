# Movement Lab v0.01

## Run the lab

1. In Unity Hub, add this `prototype/` folder and open it with Unity `6000.6.0f1`.
2. Open `Assets/MovementLab/Scenes/MovementLab.unity` and press **Play**.
3. Right-click the course to move. Press **R** to reset your position while keeping your slider settings. **Restore baseline** returns the sliders to the scene defaults.

For a standalone build, choose **Movement Lab → Build Windows Player** in the editor. Run `Builds/Windows/MovementLab.exe` once Unity finishes.

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

Run the same course with the default settings, then rerun it with one setting changed. Record both settings and compare how quickly the capsule responds to a new click, how much commitment a turn feels like, and how the capsule settles. Repeat for each setting you want to compare. Treat tuning values as provisional.

**Engineering evidence**

- Unity `6000.6.0f1` compiled the project, passed the isolated checks, and built the Windows x64 player. Re-run the checks with **Movement Lab → Run Movement Checks** in the editor.
- In the Windows player, right-clicks exercised the marked course with the baseline of 5 units/s, 8 units/s² acceleration, 12 units/s² braking, 300°/s turn rate, and an 85° turn slowdown angle. Changing the turn-rate slider to 119°/s and restoring the 300°/s baseline updated the HUD. The **R** key and **Reset course** button returned the capsule to spawn.

**Designer playtest:** Luís judges whether the movement feels weighted and still responsive, and decides which tuning to keep or revisit. Feedback pending.
