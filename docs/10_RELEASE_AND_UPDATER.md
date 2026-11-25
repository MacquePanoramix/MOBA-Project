# Release Publication & Reliable Auto-Updater Guide (`v0.2.9+`)

**v0.2.9: The Reliability Patch** removes ordinary runtime dependence on the GitHub REST API (`api.github.com`) so players never hit unauthenticated HTTP 403 IP rate-limit lockouts during playtest nights.

---

## 1. How the Updater Works (`LaneUpdater.cs`)

- **Zero accounts, zero tokens, zero bundled credentials:** Players run `WoodlandLane.exe` without signing in or installing anything extra.
- **Endpoint resolution order (`CheckForUpdates`):**
  1. Optional custom static manifest (`manifestUrl` in `updater_config.json` if set, strictly HTTPS GitHub / GitHub Pages).
  2. **Primary static GitHub Release CDN manifest:** `https://github.com/MacquePanoramix/MOBA-Project/releases/latest/download/latest.json`
  3. **Secondary static GitHub branch manifest:** `https://raw.githubusercontent.com/MacquePanoramix/MOBA-Project/update-manifest/latest.json`
  4. **Last-resort API fallback (disabled once static manifests exist):** `https://api.github.com/repos/MacquePanoramix/MOBA-Project/releases/latest` (contacted *only* if all static manifests return HTTP 404/unavailable; never used in normal checks).
- **Rate-limit & anti-spam protection:**
  - Detects HTTP `429` and `403` rate-limit responses, parses `Retry-After` / `X-RateLimit-Reset` headers, enforces request cooldowns with exponential backoff (`15s` -> `30s` -> `60s` -> `120s` -> `300s`), stops immediately on rate limits or offline errors instead of retrying across extra endpoints, and surfaces a highlighted **`Open Download Page`** browser fallback button.
- **Download integrity, rollback & startup recovery:**
  - Downloads into `_update_staging/update.zip.part`, verifies **exact byte size**, **SHA-256 checksum** (when present in `latest.json`), and full **`ZipArchive` entry readability + root `WoodlandLane.exe` presence** before moving to `update.zip` or exiting.
  - Interrupted or corrupted downloads delete `.part`, preserve installed game files, keep the **`Update`** button available for retry, and keep **`Open Download Page`** available.
  - `apply_update.ps1` extracts and verifies `WoodlandLane.exe` first, backs up replaced files, rolls back automatically and records `_update_staging/last_update_error.txt` on any copy error, and relaunches cleanly. Startup recovery surfaces any prior error marker cleanly on next launch while preserving `personal_settings.json` in Unity's `Application.persistentDataPath`.

---

## 2. Static Manifest Schema (`latest.json`)

```json
{
  "schemaVersion": 1,
  "version": "v0.2.9",
  "assetName": "WoodlandLane-Windows.zip",
  "downloadUrl": "https://github.com/MacquePanoramix/MOBA-Project/releases/download/v0.2.9/WoodlandLane-Windows.zip",
  "releaseUrl": "https://github.com/MacquePanoramix/MOBA-Project/releases/tag/v0.2.9",
  "sizeBytes": 36931011,
  "sha256": "<64-char-hex-sha256>",
  "publishedAtUtc": "2026-11-25T21:30:00Z",
  "notes": "Ultimate Free MOBA v0.2.9"
}
```

Security rule: production builds only accept `https://` URLs on `github.com`, `*.github.com`, `*.githubusercontent.com`, or `*.github.io`.

---

## 3. Manual Release Publication Steps (For Luís)

Whenever preparing a new build for playtesters:

1. **Confirm `BuildVersion` matches your release tag** (e.g., `v0.2.9` in `prototype/Assets/LaneSlice/Runtime/LaneUpdater.cs`).
2. **Build the Windows player cleanly:**
   - Either in Unity (`Moba -> Woodland Lane -> Build Windows Player`) or via batchmode (`Moba.LaneSlice.LaneEditor.BuildWindows`).
   - Verify `prototype/Builds/WindowsLane/version.txt` matches the planned release tag (e.g., `v0.2.9`).
3. **Package and generate the local verified manifest:**
   ```powershell
   python scripts/package_windows_release.py
   ```
   This produces verified:
   - `prototype/Builds/WoodlandLane-Windows.zip`
   - `prototype/Builds/latest.json` (with exact `sizeBytes` and `sha256`)
4. **Create and publish the GitHub Release:**
   - Open `https://github.com/MacquePanoramix/MOBA-Project/releases/new`.
   - Set **Tag** and **Release title** to the exact version (e.g., `v0.2.9`), keep **Set as a pre-release** UNCHECKED, and keep it as a normal published release (not draft).
   - Attach **`prototype/Builds/WoodlandLane-Windows.zip`** (and you may optionally attach **`prototype/Builds/latest.json`** right away too).
   - Click **Publish release**.
5. **Confirm the automated GitHub Actions manifest workflow succeeded:**
   - Wait ~15–30 seconds on GitHub -> **Actions** -> **Publish Static Update Manifest** (`.github/workflows/publish-update-manifest.yml`).
   - It publicly downloads `WoodlandLane-Windows.zip` **without authentication**, verifies ZIP integrity + `WoodlandLane.exe` + `version.txt == tag`, uploads verified `latest.json` to the Release assets, and updates branch `update-manifest` cleanly.
   - *(Optional static Pages backup)* In repository **Settings -> Pages**, you may set Source = **Deploy from a branch** -> `update-manifest` (`/` root) if you also want `https://macquepanoramix.github.io/MOBA-Project/latest.json` live alongside GitHub's Release CDN.

---

## 4. Transitioning Existing Players (`v0.2.2` – `v0.2.8` -> `v0.2.9`)

Players already holding `v0.2.2`–`v0.2.8` have the older updater that queries `api.github.com/repos/MacquePanoramix/MOBA-Project/releases/latest`.

- **Path A (Players whose IP is not currently rate-limited):**
  - Once GitHub Release **`v0.2.9`** is published with **`WoodlandLane-Windows.zip`**, clicking **Check for Updates -> Update to v0.2.9** inside `v0.2.2`–`v0.2.8` works normally and upgrades them automatically onto the `v0.2.9` static-manifest updater.
- **Path B (Players hitting HTTP 403 API rate limit, like your friend):**
  - The old client cannot read `latest.json` yet, so one final non-API update is needed.
  - Send them this **direct static download link** (which uses GitHub's download CDN — **zero GitHub REST API calls, zero login required**):
    `https://github.com/MacquePanoramix/MOBA-Project/releases/latest/download/WoodlandLane-Windows.zip`
  - Instruct them simply to extract that ZIP over their existing game folder (replacing existing files) and launch `WoodlandLane.exe`.
  - **Personal settings & keybindings survive automatically** in `%USERPROFILE%\AppData\LocalLow\...\personal_settings.json` (`Application.persistentDataPath`) across both paths. From `v0.2.9` onward, in-game **Check for Updates** uses static manifests and avoids the API rate limit completely.
