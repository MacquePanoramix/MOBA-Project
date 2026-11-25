# Ultimate Free MOBA Prototype — Woodland Lane, Woodland Duel & Movement Lab

## Woodland Lane (`WoodlandLane` — Active Milestone 3+4 Vertical Slice, `v0.2.9: The Reliability Patch`)

An **Ultimate Free MOBA** vertical slice (1v1 woodland lane battlefield) pairing the unchanged **`v0.2.8` gameplay, scaling, items, controls, and Hero Numbers & Scaling UI** (**Ogre**, **Miravel / Hero Luís**, **Old Man Swordsman**, and **Chain Laser Shooter**; hero-specific `LaneControls.cs`; optional/required RTS selection `LaneRtsSelection.cs`; persistent settings `LaneSettings.cs` `[F10]`; automated `LaneBalanceHarness.cs`; **Unity Relay** and **Direct TCP** multiplayer) with **`v0.2.9: The Reliability Patch`** — a free, zero-account static-manifest auto-updater (`latest.json`) that avoids unauthenticated GitHub REST API HTTP 403 rate limits.

### Run Woodland Lane

1. **Standalone Windows player:** Extract `Builds/WoodlandLane-Windows.zip` (`35.23 MB`, includes `HOW_TO_PLAY.txt`, `version.txt`, and `updater_config.json`; matching local manifest at `Builds/latest.json`) or run `Builds/WindowsLane/WoodlandLane.exe` directly (`productName`: `Ultimate Free MOBA`).
2. **In Unity `6000.6.0f1`:** Open `Assets/LaneSlice/Scenes/WoodlandLane.unity` and press **Play**.
3. **Solo Practice:** Pick any of the four heroes and a sparring Bot hero in the lobby and click **Start Lane vs Bot**.

### Online Play via Unity Relay (Linked to Unity Cloud Project `Movement Lab`)

Unity Cloud Relay is linked in `ProjectSettings/ProjectSettings.asset` and embedded in `WoodlandLane.exe` so friends never need IP addresses, port-forwarding, or extra apps:
1. **Host:** Click **Host Relay Room** → wait for the 6-character code → click **Copy** and send the code to your friend. Once your friend joins, click **Start Match**.
2. **Guest:** Click **Paste** in the Room Code box → click **Join Code**.
3. *(Fallback)* Direct TCP (`0.0.0.0:47777` + `playit.gg`/port-forward or LAN) works via **Host TCP** / **Join**. Background simulation stays running when alt-tabbed (`Application.runInBackground = true`).

### Built-In Static-Manifest Updater (`v0.2.9`)

1. In the Lobby bottom bar, click **Check for Updates**. Starting in `v0.2.9`, checks fetch a public static `latest.json` manifest first (`https://github.com/MacquePanoramix/MOBA-Project/releases/latest/download/latest.json`, then `https://raw.githubusercontent.com/MacquePanoramix/MOBA-Project/update-manifest/latest.json`), touching `api.github.com` only as a last resort if all static manifests return HTTP 404/unavailable — zero accounts, logins, or bundled tokens required.
2. **Rate-limit, integrity & recovery hardening:** Detects HTTP `429`/`403` rate limits, honors `Retry-After` / `X-RateLimit-Reset` headers with exponential backoff (`15s` → `30s` → `60s` → `120s` → `300s`) and countdown `Wait (Ns)` UI, shows a highlighted **`Open Download Page`** / **`Releases`** browser fallback button, downloads to `.part` with exact byte-size + SHA-256 + `ZipArchive` validation, backs up/rolls back via `apply_update.ps1`, and recovers cleanly on next startup if a prior apply failed.
3. Personal preferences & keybindings are stored in Unity's `Application.persistentDataPath` (`personal_settings.json`, outside the game install directory) so automatic updates and recovery always preserve your custom settings.
4. See [`../docs/10_RELEASE_AND_UPDATER.md`](../docs/10_RELEASE_AND_UPDATER.md) for the release publication workflow and transition instructions for `v0.2.2–v0.2.8` builds.

### Lane Controls, Personal Settings & Optional RTS

- **Default Conventional Controls (all customizable in `[F10]` / `Settings`):**
  - **Right-Click Ground / Enemy:** Move smoothly or continuously auto-attack (`Ogre` has no basic attacks and approaches instead).
  - **`A` Key (Quick-Cast Attack-Move):** Acquires closest visible enemy near cursor/path (unavailable on `Ogre`).
  - **`S` Key (Stop):** Cancels movement, cancelable wind-ups, and Miravel `[R]` Sanctum while respecting committed ability casts (`Ogre`/`Swordsman`/`Laser`), Ogre `[R]`, and Chain Laser `[R]` overdrive beam (halting LaserShooter's feet cleanly during `[R]`). Unavailable on future continuous-movement/WASD archetypes (`Airplane Fighter`, `Twin-Stick Shooter`).
  - **`Q` / `W` / `E` / `R`:** Quick-cast toward cursor (`Ctrl + key` or `+` to skill up; Ult unlocks at Level `4`; hover bottom-dock slots or item slots for live tooltips).
  - **`B` / `C` / `F10` / `Y` / `Space` / `F1`:** Fountain Shop / Hero Numbers & Scaling Cards (with explicit `Linear` / `Bounded diminishing` / `Unbounded diminishing` / `Inverse duration` labels) / Personal Settings / Camera Lock / Center Hero / Help overlay.
- **Optional RTS Controls (disabled by default on conventional heroes; always enabled on RTS-required heroes like future `Kobold King`):**
  - Enable in **`Settings → Gameplay & RTS`**, then use **Left-Drag** marquee box selection, **Shift+Left-Click** additive select, **`F2`** (Hero), **`F3`** (All Controllable Units), **`Tab`** (Cycle), and **`Ctrl+1..4`** / **`1..4`** Control Groups (double-tap centers camera) across units you own (e.g. Miravel's `[W]` Shimmer Decoy; LaserShooter's stationary `[E]` decoy and enemy/creep units remain strictly non-controllable).

### Woodland Lane Engineering Evidence (`2026-11-25`, `v0.2.9`)

- **Automated `LaneSlice` unit, net & updater checks (`Moba.LaneSlice.LaneEditor.RunAllChecks`):** Passed `LaneMatchChecks` (`20/20` deterministic checks + `12/12` `120s` `[BalanceDiagnostic]` matchups, `Winner=None`) and `LaneNetChecks` (`TCP`/`UTP` loopbacks, Relay guard, `BuildVersion == v0.2.9`, static-manifest endpoint ordering & HTTPS allowlist, HTTP loopback `HttpListener` scenarios for normal checks, `429`/`403` rate limits & reset headers, 404/malformed manifests, interrupted/corrupt downloads & SHA-256 mismatch, `v0.2.8 -> v0.2.9` upgrade, `personal_settings.json` preservation, and failed-update startup recovery). Log: `Artifacts/lane_checks_v029.log` (**passed and read**, `557` lines, exit `0`, zero warnings/errors).
- **Unchanged `v0.2.8` gameplay/scaling & baseline regressions:** `v0.2.8` balance harness baseline (`Artifacts/balance_v028.log` / `balance_report_v028.txt`) remains unchanged (no gameplay/scaling edits in `v0.2.9`). Baseline regressions re-run after all `v0.2.9` edits: `MovementLabChecks.Run` (`Artifacts/movement_checks_v029.log`, `344` lines, exit `0`) and `DuelEditor.RunAllChecks` (`Artifacts/duel_checks_v029.log`, `357` lines, exit `0`) — both **passed and read**.
- **Windows build, package & loopback smoke test:** Built `Builds/WindowsLane/WoodlandLane.exe` (`Artifacts/build_v029.log`, `4463` lines, exit `0`, `94,3 MB`, `version.txt == v0.2.9`, `HOW_TO_PLAY.txt`, `updater_config.json`, `cloudProjectId: d0660faa-6410-4825-ad25-e4026b920c3e` and `Ultimate Free MOBA` verified in `globalgamemanagers`). Packaged via `scripts/package_windows_release.py` into `Builds/WoodlandLane-Windows.zip` (`36,937,415` bytes / `35.23 MB`, `189` entries, `testzip` clean, `sha256: 61bc3066307f58c06a188ee6814ba24a07c77717045e6f94b7f9e8ff24225919`) and `Builds/latest.json` (`schemaVersion: 1`). Headless `-laneHost 0.0.0.0` + `-laneJoin localhost:47777` loopback smoke test passed (`Artifacts/lane_smoke_host_v029.log`: `228` lines, `Lane match started (Host, Ogre vs Miravel).`; `Artifacts/lane_smoke_guest_v029.log`: `164` lines, `Lane match received (Guest, Miravel vs Ogre).`; zero uncaught exceptions). Live GitHub Release `v0.2.9` publication, live Actions workflow run, live friend download/update, live Unity Cloud Relay exchange, windowed UI review, and human playtest remain pending Luís (`not run`).

---

## Woodland Duel (1v1 — Prior Baseline)

The active 1v1 slice pairs **Ogre** (aimed Q Slam stun + W Sweep shove, animation CC immunity, no basic attack) against **Hero Luís / Miravel** (basic attack + periodic clearing orbs to collect for heal/pulse damage or deny).

### Run the duel

1. **Standalone Windows player:** Run `Builds/WindowsDuel/WoodlandDuel.exe` directly (or extract `Builds/WoodlandDuel-Windows.zip` on any Windows PC).
2. **In Unity `6000.6.0f1`:** Open `Assets/Duel/Scenes/WoodlandDuel.unity` and press **Play**.
3. **Solo Practice:** Pick **Ogre** or **Miravel** in the lobby and click **Practice against a bot**.

### Internet Playtest Guide (Amsterdam ↔ Brazil / Any Remote Friend)

Only the **Host** sets up a tunnel (zero VPN, zero account, and zero router setup needed on the **Guest**'s PC):

1. **Host (one-time ~2-minute setup via [playit.gg](https://playit.gg/)):**
   - Install and open the free `playit.gg` Windows agent, claim it in your browser, and add a **Custom TCP** tunnel pointing to local IP `127.0.0.1` and port `47777`. Keep the `playit` window running while hosting.
   - Start `WoodlandDuel.exe`, choose **Host a friend → Host on port 47777**, and send your friend the public `playit.gg` `host:port` string shown in your playit dashboard (for example `something.ply.gg:12345`).
   - *(Optional alternatives: forward TCP port `47777` on your router to your PC and send your public IPv4 for lowest direct ping, or share over Tailscale.)*
2. **Guest (zero-install):**
   - Download and extract `Builds/WoodlandDuel-Windows.zip` (`34.14 MB`, includes `HOW_TO_PLAY.txt`), then double-click `WoodlandDuel.exe` (if Windows SmartScreen prompts, click **More info → Run anyway**; allow Firewall access if asked).
   - Click **Join a friend**, copy the address your friend sent, click **Paste**, and click **Join**.
3. **Rematch:** After a knockout, both players click **Ready for rematch** to automatically swap heroes for the next round. Live RTT displays as `FRIEND DUEL · <N> ms`.

### Duel controls and provisional tuning

- **Both heroes:** **Right-click** ground to move around trees; **Esc** cancels an armed ability or opens the pause/rematch menu.
- **Ogre (`3.2` speed, `250°/s` turn, `140` HP):** **Q** (or HUD button + left-click) aims **Slam** (`0.85s` wind-up, rectangle stun); **W** aims **Sweep** (`0.50s` wind-up, arc shove that stops cleanly at tree trunks). Immune to CC during Slam/Sweep wind-ups.
- **Hero Luís / Miravel (`3.65` speed, `540°/s` turn, `105` HP):** **Right-click Ogre** to approach, face, and strike (`0.50s` wind-up, `13` damage; ground right-click cancels wind-up; post-hit recovery can be walk-canceled). Walk onto the spawning orb to heal `+14 HP` (shown with a rising mint halo and `(+N HP)` HUD notice) and pulse `17` damage to nearby enemies, or block/deny Ogre from stepping onto it first.
- *(Previous `2026-11-22` baseline preserved in `DuelMatch.cs`: Ogre `4.4` speed / `165°/s` turn / `0.82s` Q / `0.32s` W; Miravel `5.0` speed / `300°/s` turn.)*

### Duel engineering evidence (`2026-11-23`)

- **Editor checks (`Duel → Run All Checks`):** Compiled cleanly and passed `DuelMatchChecks` (including `+HP` orb heal notice verification) and `DuelLinkChecks` (`0.0.0.0` bind, `ip:port` and `tcp://localhost:port` DNS parsing/connect, invalid address/port rejection, `PING`/`PONG` RTT measurement, stale/invalid input rejection, rematch swap, disconnect/rejoin). Log: `Artifacts/unity-duel-checks.log` (**passed and read**).
- **Movement Lab regression (`Movement Lab → Run Movement Checks`):** Re-verified after Duel updates. Log: `Artifacts/unity-movement-regression.log` (**passed and read**).
- **Windows player build (`Duel → Build Windows Player`):** Built `Builds/WindowsDuel/WoodlandDuel.exe` + `HOW_TO_PLAY.txt` and packaged `Builds/WoodlandDuel-Windows.zip` (`34.14 MB`). Log: `Artifacts/unity-duel-build.log` (**passed and read**).
- **Headless Host/Guest loopback smoke test:** Launched `WoodlandDuel.exe -batchmode -nographics -duelHost 0.0.0.0` and `-duelJoin localhost:47777`; verified clean round start (`Duel round 1 started (Host, Ogre).` and `Duel round 1 received (Guest, Miravel).`) with zero exceptions (`Artifacts/duel-smoke-host.log`, `duel-smoke-guest.log`). Windowed visual review of the new tuning/heal ring and real cross-internet human play remain pending Luís and his friend.

---

## Movement and Basic Attack Lab (Baseline)

### Run the lab

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
