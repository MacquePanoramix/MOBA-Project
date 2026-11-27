# Ultimate Free MOBA Prototype — Woodland Lane, Woodland Duel & Movement Lab

## Woodland Lane (`WoodlandLane` — Active Milestone 3+4 Vertical Slice, `v0.3.1`)

An **Ultimate Free MOBA** vertical slice (1v1 woodland lane battlefield) pairing the **`v0.2.8`** hero scaling curves (**Ogre**, **Miravel / Hero Luís**, **Old Man Swordsman**, and **Chain Laser Shooter**; hero-specific `LaneControls.cs`; optional/required RTS selection `LaneRtsSelection.cs`; persistent settings `LaneSettings.cs` `[F10]`; **Unity Relay** and **Direct TCP** multiplayer), **`v0.2.9`** free static-manifest auto-updater (`latest.json`), and **`v0.3.0`** Hero Guide + Shared Mana + `44`-item recipe catalog with **`v0.3.1` (`The Player Experience Patch`)** additions: cached high-FPS **`30 Hz`** RenderTexture previews (`231.8–295.3 FPS` windowed vs `5.6–5.9 FPS` in `v0.3.0`), deterministic **automatic component purchasing** (`BuyItemSmart`), **interactive recipe trees** (single-click inspect vs right-click / double-click smart-buy, zero recipe scrolls), a UX-redesigned **Fountain Shop** (`64x64` procedural icons for all `44` items including curved metallic `Banana Blade`, all `15` direct-stat multi-filters, multi-token search, zero forced role silos), and a standalone **`Item Compendium (44)`** with **`28`** scripted real-simulation passive demonstrations. See [`../docs/11_ITEMS_AND_RESOURCES.md`](../docs/11_ITEMS_AND_RESOURCES.md) for the complete Approved vs. Provisional specification.

### Run Woodland Lane

1. **Standalone Windows player:** Extract `Builds/WoodlandLane-Windows.zip` (`35.30 MB`, includes `HOW_TO_PLAY.txt`, `version.txt`, and `updater_config.json`; matching local manifest at `Builds/latest.json`) or run `Builds/WindowsLane/WoodlandLane.exe` directly (`productName`: `Ultimate Free MOBA`).
2. **In Unity `6000.6.0f1`:** Open `Assets/LaneSlice/Scenes/WoodlandLane.unity` and press **Play**.
3. **Solo Practice:** Pick any of the four heroes and a sparring Bot hero in the lobby and click **Start Lane vs Bot**, or open **`Item Compendium (44)`** / **`Hero Guide`** straight from the Lobby without entering a match.

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
  - **`Q` / `W` / `E` / `R`:** Quick-cast toward cursor (`Ctrl + key` or `+` to skill up; Ult unlocks at Level `4`; hover bottom-dock slots or item slots for live tooltips; Miravel & Chain Laser spend Mana).
  - **`B` / `C` / `Compendium` / `F10` / `Y` / `Space` / `F1`:** Redesigned Fountain Shop (`64x64` icons, `15`-stat multi-filters, search, interactive recipe tree with **Left-Click Inspect** vs. **Right-Click / Double-Click Auto-Buy Missing Components**, `70%` Sell, expandable `[Show Math]`) / Optimized `30 Hz` Interactive Hero Guide (`[C]`) / Standalone **`Item Compendium (44)`** with `28` passive demonstrations / Personal Settings / Camera Lock / Center Hero / Help overlay.
- **Optional RTS Controls (disabled by default on conventional heroes; always enabled on RTS-required heroes like future `Kobold King`):**
  - Enable in **`Settings → Gameplay & RTS`**, then use **Left-Drag** marquee box selection, **Shift+Left-Click** additive select, **`F2`** (Hero), **`F3`** (All Controllable Units), **`Tab`** (Cycle), and **`Ctrl+1..4`** / **`1..4`** Control Groups (double-tap centers camera) across units you own (e.g. Miravel's `[W]` Shimmer Decoy; LaserShooter's stationary `[E]` decoy and enemy/creep units remain strictly non-controllable).

### Woodland Lane Engineering Evidence (`2026-11-27`, `v0.3.1`)

- **Post-polish Unity Editor checks (`Moba.LaneSlice.LaneEditor.RunAllChecks`):** Passed `LaneMatchChecks` (`21/21` + `12/12` `120s` `[BalanceDiagnostic]`), `LaneResourceAndItemChecks` (Stage C–G), `LanePerfAndUxChecks` (Stage A `HeroGuideCache`, Stage B `ResolveAutoPurchase` & `BuildRecipeTree` with zero recipe scrolls, Stage C `44` `64x64` icons & `15`-stat queries, Stage D–E `>=28` Compendium passive demos, Crescent Strike, Slippery Trickster, `Weightlessness` 0-slider fixed slow, Ancient Vitality, Giant's Might, Immovable Mountain, Draconic Blade Heat/shred, Adaptive Scales, Titan Aegis, preview isolation), and `LaneNetChecks` (`BuyItemSmart` sync, `TCP`/`UTP`/Relay guards, `BuildVersion == v0.3.1`, static-manifest & `HttpListener` scenarios). Log: `Artifacts/lane_checks_v031.log` (**passed and read**, `601` lines, exit `0`, zero warnings/errors).
- **Editor & Windowed `1280x720` Preview Performance Benchmarks (`LaneEditor.RunPreviewPerfBench` & `-lanePerfBench`, `RTX 4060 Laptop GPU`, `Ultra`):**
  - Editor batchmode (`Artifacts/perf_editor_after_v031.txt` vs `perf_editor_before_v030.txt`): `2_GuideOpen_DemosOFF` `173.992ms → 0.031ms` (`Gen0=41→0`), `3_GuideOpen_DemosON` `172.191ms → 0.236ms` (`Gen0=61→0`), `4_AbilityHeroSwitching` `257.684ms → 0.892ms`, `5_SliderDragging` `170.215ms → 0.228ms`, `6_ExtendedSession_300f` `168.083ms → 0.023ms`, `7_CompendiumItemSwitching` `0.671ms`, `8_CompendiumOpenClose` `0.060ms` (`Mesh+=0 Mat+=0`).
  - Windowed `1280x720` GPU (`Artifacts/perf_windowed_after_v031.txt` & `perf_windowed_after_v031_unity.log`, `43` lines, zero exceptions vs `perf_windowed_before_v030.txt`): `1_Lobby_GuideClosed` `263.1 → 302.1 FPS`, `2_Lobby_GuideOpen_DemosOFF` `5.9 → 295.3 FPS` (`169.05 → 3.39ms`), `3_Lobby_GuideOpen_DemosON` `5.7 → 231.8 FPS` (`175.31 → 4.31ms`, `Mesh+=32→0`, `Mat+=96→0`), `4_Match_GuideClosed` `266.1 → 288.8 FPS`, `5_Match_GuideOpen_DemosON` `5.6 → 276.7 FPS` (`179.48 → 3.61ms`, `Mesh+=7→0`, `Mat+=37→1`), `6_Compendium_DemosON` `267.9 FPS` (`3.73ms`), `7_Compendium_OpenCloseCycle` `294.8 FPS` (`3.39ms`, `Mesh+=0 Mat+=0`).
- **Pre-UI-polish balance & baseline regressions (`2026-11-27`, passed and read; only IMGUI layout/polish `.cs` edits occurred afterward with zero sim changes):** `Moba.LaneSlice.LaneEditor.RunBalanceExperiments` (`Artifacts/balance_v031.log`, `494` lines, exit `0`), `Moba.MovementLab.MovementLabChecks.Run` (`Artifacts/movement_checks_v031.log`, `349` lines, exit `0`), and `Moba.Duel.DuelEditor.RunAllChecks` (`Artifacts/duel_checks_v031.log`, `361` lines, exit `0`).
- **Windows build, package, UI visual inspection & loopback smoke test:** Built `Builds/WindowsLane/WoodlandLane.exe` (`Artifacts/build_windows_v031.log`, `4474` lines, exit `0`, `94,5 MB`, `version.txt == v0.3.1`, `cloudProjectId: d0660faa-6410-4825-ad25-e4026b920c3e` & `Ultimate Free MOBA` verified); packaged via `scripts/package_windows_release.py` into `Builds/WoodlandLane-Windows.zip` (`37,014,921` bytes / `35.30 MB`, `189` entries, `testzip` clean, `sha256: 941d281f6877da852c8f4fcff982c73156edc1ef38123473df60b437e524a7c2`) + `Builds/latest.json` (`schemaVersion: 1`, `version: v0.3.1`, `publishedAtUtc: 2026-11-27T16:01:01Z`); visually verified `12` UI captures at `1280x720` & `1600x900`; passed headless `-laneHost 0.0.0.0` + `-laneJoin localhost:47777` TCP loopback smoke test (`Artifacts/smoke_host_v031.log` `87` lines, `Artifacts/smoke_guest_v031.log` `77` lines, zero exceptions). Live GitHub Release `v0.3.1` publication, live Actions `Publish Static Update Manifest` execution, live friend update swap, live Unity Cloud Relay exchange, and human playtest remain pending Luís (`not run`).

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
