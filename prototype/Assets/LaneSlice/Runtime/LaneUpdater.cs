using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Explicit button-triggered GitHub Releases updater for standalone Windows playtests.
    /// Never runs automatically during headless -batchmode, automated checks, or launch.
    /// </summary>
    public sealed class LaneUpdater
    {
        public const string BuildVersion = "v0.2.8";

        [Serializable]
        public sealed class UpdaterConfig
        {
            public string owner = "MacquePanoramix";
            public string repo = "MOBA-Project";
            public string assetName = "WoodlandLane-Windows.zip";
        }

        [Serializable]
        private sealed class GitHubAssetDto
        {
            public string name;
            public string browser_download_url;
            public long size;
        }

        [Serializable]
        private sealed class GitHubReleaseDto
        {
            public string tag_name;
            public GitHubAssetDto[] assets;
        }

        private readonly UpdaterConfig config;

        public bool IsBusy { get; private set; }
        public bool UpdateAvailable { get; private set; }
        public string LatestTag { get; private set; } = string.Empty;
        public string LatestDownloadUrl { get; private set; } = string.Empty;
        public long LatestSizeBytes { get; private set; }
        public float DownloadProgress { get; private set; }
        public string Status { get; private set; } = string.Empty;

        public LaneUpdater(UpdaterConfig overrideConfig = null)
        {
            config = overrideConfig ?? LoadConfig();
        }

        public static string GetGameDir()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
        }

        public static UpdaterConfig LoadConfig()
        {
            var cfg = new UpdaterConfig();
            try
            {
                if (!Application.isEditor)
                {
                    string path = Path.Combine(GetGameDir(), "updater_config.json");
                    if (File.Exists(path))
                    {
                        UpdaterConfig loaded = JsonUtility.FromJson<UpdaterConfig>(File.ReadAllText(path, Encoding.UTF8));
                        if (loaded != null && !string.IsNullOrWhiteSpace(loaded.owner) && !string.IsNullOrWhiteSpace(loaded.repo))
                        {
                            if (string.IsNullOrWhiteSpace(loaded.assetName)) loaded.assetName = cfg.assetName;
                            return loaded;
                        }
                    }
                }
            }
            catch
            {
                // Fall back to defaults on unreadable config
            }
            return cfg;
        }

        public static void CleanupStagingDir()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            try
            {
                string staging = Path.Combine(GetGameDir(), "_update_staging");
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup after prior self-update
            }
        }

        public static bool IsNewerVersion(string currentTag, string remoteTag)
        {
            int[] cur = ParseVersionParts(currentTag);
            int[] rem = ParseVersionParts(remoteTag);
            if (cur == null || rem == null) return false;

            int len = Math.Max(cur.Length, rem.Length);
            for (int i = 0; i < len; i++)
            {
                int c = i < cur.Length ? cur[i] : 0;
                int r = i < rem.Length ? rem[i] : 0;
                if (r > c) return true;
                if (r < c) return false;
            }
            return false;
        }

        private static int[] ParseVersionParts(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return null;
            string s = tag.Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            int dash = s.IndexOf('-');
            if (dash >= 0) s = s.Substring(0, dash);
            string[] tokens = s.Split('.');
            if (tokens.Length == 0) return null;

            var nums = new int[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!int.TryParse(tokens[i], out int n) || n < 0) return null;
                nums[i] = n;
            }
            return nums;
        }

        public static bool IsAllowedDownloadUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return false;
            string host = uri.Host.ToLowerInvariant();
            return host == "github.com" ||
                   host.EndsWith(".github.com", StringComparison.Ordinal) ||
                   host.EndsWith(".githubusercontent.com", StringComparison.Ordinal);
        }

        public static bool ParseReleaseJson(
            string json,
            string expectedAssetName,
            out string tag,
            out string downloadUrl,
            out long sizeBytes)
        {
            tag = string.Empty;
            downloadUrl = string.Empty;
            sizeBytes = 0;
            if (string.IsNullOrWhiteSpace(json)) return false;

            GitHubReleaseDto dto = JsonUtility.FromJson<GitHubReleaseDto>(json);
            if (dto == null || string.IsNullOrWhiteSpace(dto.tag_name) || dto.assets == null) return false;

            tag = dto.tag_name.Trim();
            for (int i = 0; i < dto.assets.Length; i++)
            {
                GitHubAssetDto a = dto.assets[i];
                if (a != null && string.Equals(a.name, expectedAssetName, StringComparison.OrdinalIgnoreCase) &&
                    IsAllowedDownloadUrl(a.browser_download_url))
                {
                    downloadUrl = a.browser_download_url;
                    sizeBytes = a.size;
                    return true;
                }
            }
            return false;
        }

        public static string BuildApplyScript(int appPid, string gameDir, string zipPath, string exePath)
        {
            string qGame = EscapePsSingle(gameDir);
            string qZip = EscapePsSingle(zipPath);
            string qExe = EscapePsSingle(exePath);
            string qExtract = EscapePsSingle(Path.Combine(Path.GetDirectoryName(zipPath) ?? gameDir, "extracted"));

            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            sb.AppendLine($"$targetPid = {appPid}");
            sb.AppendLine("for ($i = 0; $i -lt 30; $i++) {");
            sb.AppendLine("  if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { break }");
            sb.AppendLine("  Start-Sleep -Milliseconds 500");
            sb.AppendLine("}");
            sb.AppendLine("Start-Sleep -Milliseconds 500");
            sb.AppendLine($"$gameDir = '{qGame}'");
            sb.AppendLine($"$zipPath = '{qZip}'");
            sb.AppendLine($"$extractDir = '{qExtract}'");
            sb.AppendLine($"$exePath = '{qExe}'");
            sb.AppendLine("if (Test-Path -LiteralPath $extractDir) { Remove-Item -LiteralPath $extractDir -Recurse -Force }");
            sb.AppendLine("Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force");
            sb.AppendLine("Copy-Item -Path (Join-Path $extractDir '*') -Destination $gameDir -Recurse -Force");
            sb.AppendLine("Start-Process -FilePath $exePath -WorkingDirectory $gameDir");
            return sb.ToString();
        }

        private static string EscapePsSingle(string value) => (value ?? string.Empty).Replace("'", "''");

        public IEnumerator CheckForUpdatesCoroutine()
        {
            if (IsBusy) yield break;
            IsBusy = true;
            UpdateAvailable = false;
            LatestTag = string.Empty;
            LatestDownloadUrl = string.Empty;
            LatestSizeBytes = 0;
            Status = "Checking GitHub Releases...";

            string url = $"https://api.github.com/repos/{config.owner}/{config.repo}/releases/latest";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;
                req.SetRequestHeader("User-Agent", "WoodlandLane-Prototype");
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    if (req.responseCode == 404)
                    {
                        Status = $"No public release found yet (404). Publish a GitHub Release with {config.assetName} (or set updater_config.json).";
                    }
                    else
                    {
                        Status = $"Update check failed ({req.responseCode}): {req.error}";
                    }
                    IsBusy = false;
                    yield break;
                }

                string body = req.downloadHandler?.text ?? string.Empty;
                if (!ParseReleaseJson(body, config.assetName, out string tag, out string dlUrl, out long size))
                {
                    Status = $"Latest release does not contain {config.assetName}.";
                    IsBusy = false;
                    yield break;
                }

                LatestTag = tag;
                LatestDownloadUrl = dlUrl;
                LatestSizeBytes = size;
                if (IsNewerVersion(BuildVersion, tag))
                {
                    UpdateAvailable = true;
                    Status = $"Update ready: {tag} ({size / (1024f * 1024f):0.0} MB).";
                }
                else
                {
                    Status = $"Up to date ({BuildVersion}).";
                }
            }

            IsBusy = false;
        }

        public IEnumerator DownloadAndApplyCoroutine()
        {
            if (IsBusy || !UpdateAvailable) yield break;
            if (Application.isEditor)
            {
                Status = "Self-update only runs inside the standalone Windows player, not Unity Editor.";
                yield break;
            }
            if (!IsAllowedDownloadUrl(LatestDownloadUrl))
            {
                Status = "Blocked non-GitHub HTTPS update URL.";
                yield break;
            }

            IsBusy = true;
            DownloadProgress = 0f;
            Status = $"Downloading {LatestTag}...";

            string gameDir = GetGameDir();
            string stagingDir = Path.Combine(gameDir, "_update_staging");
            if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

            string zipPath = Path.Combine(stagingDir, "update.zip");
            string scriptPath = Path.Combine(stagingDir, "apply_update.ps1");
            string exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Path.Combine(gameDir, "WoodlandLane.exe");
            }

            using (var req = new UnityWebRequest(LatestDownloadUrl, UnityWebRequest.kHttpVerbGET))
            {
                req.timeout = 180;
                req.SetRequestHeader("User-Agent", "WoodlandLane-Prototype");
                req.downloadHandler = new DownloadHandlerFile(zipPath) { removeFileOnAbort = true };

                UnityWebRequestAsyncOperation op = req.SendWebRequest();
                while (!op.isDone)
                {
                    DownloadProgress = req.downloadProgress;
                    Status = $"Downloading {LatestTag}... {Mathf.RoundToInt(DownloadProgress * 100f)}%";
                    yield return null;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Status = $"Download failed: {req.error}";
                    IsBusy = false;
                    yield break;
                }
            }

            if (!HasValidZipHeader(zipPath))
            {
                Status = "Downloaded file is missing or not a valid ZIP archive.";
                IsBusy = false;
                yield break;
            }

            string scriptText = BuildApplyScript(Process.GetCurrentProcess().Id, gameDir, zipPath, exePath);
            File.WriteAllText(scriptPath, scriptText, new UTF8Encoding(false));

            Status = $"Installing {LatestTag} and restarting...";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                WorkingDirectory = gameDir,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);

            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit();
        }

        private static bool HasValidZipHeader(string path)
        {
            if (!File.Exists(path)) return false;
            var fi = new FileInfo(path);
            if (fi.Length <= 1024) return false;

            using (FileStream fs = File.OpenRead(path))
            {
                int b0 = fs.ReadByte();
                int b1 = fs.ReadByte();
                int b2 = fs.ReadByte();
                int b3 = fs.ReadByte();
                return b0 == 0x50 && b1 == 0x4B && b2 == 0x03 && b3 == 0x04;
            }
        }
    }
}
