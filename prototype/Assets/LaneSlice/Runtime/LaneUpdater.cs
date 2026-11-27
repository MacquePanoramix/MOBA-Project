using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Moba.LaneSlice
{
    public enum UpdateErrorKind
    {
        None = 0,
        Network = 1,
        RateLimited = 2,
        Unavailable = 3,
        MalformedManifest = 4,
        DownloadFailed = 5,
        VerificationFailed = 6,
        ApplyFailed = 7,
    }

    public interface IUpdateHttpOp : IDisposable
    {
        bool IsDone { get; }
        bool IsSuccess { get; }
        bool IsNetworkError { get; }
        long StatusCode { get; }
        string Error { get; }
        string Text { get; }
        float Progress { get; }
        IDictionary<string, string> ResponseHeaders { get; }
    }

    public interface IUpdateHttpClient
    {
        IUpdateHttpOp GetText(string url);
        IUpdateHttpOp DownloadFile(string url, string destinationPath);
    }

    /// <summary>
    /// Static-manifest-first updater with GitHub CDN hosting, rate-limit cooldowns,
    /// SHA-256 + full ZIP integrity validation, rollback recovery, and browser fallback.
    /// Never runs automatically in headless -batchmode or automated tests unless invoked explicitly.
    /// </summary>
    public sealed class LaneUpdater
    {
        public const string BuildVersion = "v0.3.1";
        public const int ExpectedSchemaVersion = 1;
        private const string StagingDirName = "_update_staging";
        private const string ErrorMarkerFileName = "last_update_error.txt";

        [Serializable]
        public sealed class UpdaterConfig
        {
            public string owner = "MacquePanoramix";
            public string repo = "MOBA-Project";
            public string assetName = "WoodlandLane-Windows.zip";
            public string manifestUrl = string.Empty;
            public string manifestBranch = "update-manifest";
            public string manifestFileName = "latest.json";
            public bool allowGitHubApiFallback = true;
        }

        [Serializable]
        public sealed class UpdateManifestDto
        {
            public int schemaVersion;
            public string version;
            public string assetName;
            public string downloadUrl;
            public string releaseUrl;
            public long sizeBytes;
            public string sha256;
            public string publishedAtUtc;
            public string notes;
        }

        public struct CheckEndpoint
        {
            public string Url;
            public bool IsGitHubApi;

            public CheckEndpoint(string url, bool isGitHubApi)
            {
                Url = url;
                IsGitHubApi = isGitHubApi;
            }
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
            public string html_url;
            public GitHubAssetDto[] assets;
        }

        private sealed class UnityHttpOp : IUpdateHttpOp
        {
            private readonly UnityWebRequest req;
            private readonly UnityWebRequestAsyncOperation op;
            private readonly bool captureText;

            public UnityHttpOp(UnityWebRequest req, bool captureText)
            {
                this.req = req;
                this.captureText = captureText;
                op = req.SendWebRequest();
            }

            public bool IsDone => op.isDone;
            public bool IsSuccess => req.result == UnityWebRequest.Result.Success;
            public bool IsNetworkError => req.result == UnityWebRequest.Result.ConnectionError;
            public long StatusCode => req.responseCode;
            public string Error => req.error ?? string.Empty;
            public string Text => captureText ? (req.downloadHandler?.text ?? string.Empty) : string.Empty;
            public float Progress => req.downloadProgress;
            public IDictionary<string, string> ResponseHeaders => req.GetResponseHeaders();

            public void Dispose()
            {
                req.Dispose();
            }
        }

        private sealed class UnityWebRequestHttpClient : IUpdateHttpClient
        {
            public IUpdateHttpOp GetText(string url)
            {
                var req = UnityWebRequest.Get(url);
                req.timeout = 10;
                req.SetRequestHeader("User-Agent", $"UltimateFreeMOBA-Updater/{BuildVersion}");
                req.SetRequestHeader("Cache-Control", "no-cache");
                req.SetRequestHeader("Accept", "application/json");
                return new UnityHttpOp(req, captureText: true);
            }

            public IUpdateHttpOp DownloadFile(string url, string destinationPath)
            {
                var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
                req.timeout = 180;
                req.SetRequestHeader("User-Agent", $"UltimateFreeMOBA-Updater/{BuildVersion}");
                req.downloadHandler = new DownloadHandlerFile(destinationPath) { removeFileOnAbort = true };
                return new UnityHttpOp(req, captureText: false);
            }
        }

        private readonly UpdaterConfig config;
        private readonly IUpdateHttpClient httpClient;
        private readonly bool allowLoopbackUrls;
        private readonly Func<DateTime> utcNowProvider;
        private readonly string overrideGameDir;
        private readonly string overrideCurrentVersion;
        private readonly IReadOnlyList<CheckEndpoint> overrideEndpoints;

        private DateTime? cooldownUntilUtc;
        private int consecutiveFailures;

        public bool IsBusy { get; private set; }
        public bool UpdateAvailable { get; private set; }
        public bool ShowBrowserFallback { get; private set; }
        public string LatestTag { get; private set; } = string.Empty;
        public string LatestDownloadUrl { get; private set; } = string.Empty;
        public string LatestSha256 { get; private set; } = string.Empty;
        public string BrowserReleaseUrl { get; private set; } = string.Empty;
        public long LatestSizeBytes { get; private set; }
        public float DownloadProgress { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public UpdateErrorKind LastErrorKind { get; private set; }
        public bool ApplyScriptLaunchedForTest { get; private set; }

        public LaneUpdater(
            UpdaterConfig overrideConfig = null,
            IUpdateHttpClient httpClient = null,
            bool allowLoopbackUrls = false,
            Func<DateTime> utcNowProvider = null,
            string overrideGameDir = null,
            string overrideCurrentVersion = null,
            IReadOnlyList<CheckEndpoint> overrideEndpoints = null)
        {
            config = SanitizeConfig(overrideConfig ?? LoadConfig());
            this.httpClient = httpClient ?? new UnityWebRequestHttpClient();
            this.allowLoopbackUrls = allowLoopbackUrls;
            this.utcNowProvider = utcNowProvider ?? (() => DateTime.UtcNow);
            this.overrideGameDir = overrideGameDir;
            this.overrideCurrentVersion = overrideCurrentVersion;
            this.overrideEndpoints = overrideEndpoints;
            BrowserReleaseUrl = DefaultReleasePageUrl();
        }

        private string CurrentVersion => string.IsNullOrWhiteSpace(overrideCurrentVersion) ? BuildVersion : overrideCurrentVersion.Trim();

        public string DefaultReleasePageUrl() => $"https://github.com/{config.owner}/{config.repo}/releases/latest";

        public static string GetGameDir()
        {
            return Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;
        }

        private string ResolveGameDir()
        {
            return string.IsNullOrWhiteSpace(overrideGameDir) ? GetGameDir() : overrideGameDir;
        }

        public static UpdaterConfig SanitizeConfig(UpdaterConfig raw)
        {
            var def = new UpdaterConfig();
            var cfg = raw ?? def;
            if (string.IsNullOrWhiteSpace(cfg.owner)) cfg.owner = def.owner;
            if (string.IsNullOrWhiteSpace(cfg.repo)) cfg.repo = def.repo;
            if (string.IsNullOrWhiteSpace(cfg.assetName)) cfg.assetName = def.assetName;
            if (string.IsNullOrWhiteSpace(cfg.manifestBranch)) cfg.manifestBranch = def.manifestBranch;
            if (string.IsNullOrWhiteSpace(cfg.manifestFileName)) cfg.manifestFileName = def.manifestFileName;
            cfg.owner = cfg.owner.Trim();
            cfg.repo = cfg.repo.Trim();
            cfg.assetName = cfg.assetName.Trim();
            cfg.manifestUrl = (cfg.manifestUrl ?? string.Empty).Trim();
            cfg.manifestBranch = cfg.manifestBranch.Trim();
            cfg.manifestFileName = cfg.manifestFileName.Trim();
            return cfg;
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
                            return SanitizeConfig(loaded);
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

        public void RecoverFromPreviousUpdateIfNeeded(bool forceInTest = false)
        {
            if (!forceInTest && (Application.isEditor || Application.isBatchMode)) return;

            string gameDir = ResolveGameDir();
            string staging = Path.Combine(gameDir, StagingDirName);
            if (!Directory.Exists(staging)) return;

            try
            {
                string errFile = Path.Combine(staging, ErrorMarkerFileName);
                if (File.Exists(errFile))
                {
                    string detail = (File.ReadAllText(errFile, Encoding.UTF8) ?? string.Empty).Trim();
                    if (detail.Length > 140) detail = detail.Substring(0, 140) + "...";
                    LastErrorKind = UpdateErrorKind.ApplyFailed;
                    ShowBrowserFallback = true;
                    Status = string.IsNullOrEmpty(detail)
                        ? "Previous update could not be applied; game files were preserved. Retry or download in browser."
                        : $"Previous update did not complete ({detail}); game files preserved. Retry or open download page.";
                }
            }
            catch
            {
                // Best-effort read of failure marker
            }

            try
            {
                Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // Best-effort cleanup after prior self-update
            }
        }

        public static void CleanupStagingDir()
        {
            if (Application.isEditor || Application.isBatchMode) return;
            try
            {
                string staging = Path.Combine(GetGameDir(), StagingDirName);
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

        public List<CheckEndpoint> BuildCheckEndpoints()
        {
            if (overrideEndpoints != null && overrideEndpoints.Count > 0)
            {
                return new List<CheckEndpoint>(overrideEndpoints);
            }

            var list = new List<CheckEndpoint>(4);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(config.manifestUrl))
            {
                AddUniqueEndpoint(list, seen, config.manifestUrl.Trim(), isGitHubApi: false);
            }

            string releaseStatic = $"https://github.com/{config.owner}/{config.repo}/releases/latest/download/{config.manifestFileName}";
            AddUniqueEndpoint(list, seen, releaseStatic, isGitHubApi: false);

            string rawStatic = $"https://raw.githubusercontent.com/{config.owner}/{config.repo}/{config.manifestBranch}/{config.manifestFileName}";
            AddUniqueEndpoint(list, seen, rawStatic, isGitHubApi: false);

            if (config.allowGitHubApiFallback)
            {
                string apiUrl = $"https://api.github.com/repos/{config.owner}/{config.repo}/releases/latest";
                AddUniqueEndpoint(list, seen, apiUrl, isGitHubApi: true);
            }

            return list;
        }

        private static void AddUniqueEndpoint(List<CheckEndpoint> list, HashSet<string> seen, string url, bool isGitHubApi)
        {
            if (!string.IsNullOrEmpty(url) && seen.Add(url))
            {
                list.Add(new CheckEndpoint(url, isGitHubApi));
            }
        }

        public int GetCooldownRemainingSeconds()
        {
            if (!cooldownUntilUtc.HasValue) return 0;
            double rem = (cooldownUntilUtc.Value - utcNowProvider()).TotalSeconds;
            if (rem <= 0d)
            {
                cooldownUntilUtc = null;
                return 0;
            }
            return (int)Math.Ceiling(rem);
        }

        public bool IsInCooldown => GetCooldownRemainingSeconds() > 0;

        public void OpenReleasePageInBrowser()
        {
            string url = IsAllowedWebPageUrl(BrowserReleaseUrl, allowLoopbackUrls)
                ? BrowserReleaseUrl
                : DefaultReleasePageUrl();
            if (!IsAllowedWebPageUrl(url, allowLoopbackUrls)) return;
            if (!Application.isBatchMode)
            {
                Application.OpenURL(url);
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

        public static bool IsAllowedDownloadUrl(string url) => IsAllowedDownloadUrl(url, allowLoopbackUrls: false);

        public static bool IsAllowedDownloadUrl(string url, bool allowLoopbackUrls)
        {
            return IsAllowedGitHubUrl(url, allowLoopbackUrls);
        }

        public static bool IsAllowedManifestUrl(string url, bool allowLoopbackUrls = false)
        {
            return IsAllowedGitHubUrl(url, allowLoopbackUrls);
        }

        public static bool IsAllowedWebPageUrl(string url, bool allowLoopbackUrls = false)
        {
            return IsAllowedGitHubUrl(url, allowLoopbackUrls);
        }

        private static bool IsAllowedGitHubUrl(string url, bool allowLoopbackUrls)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            string host = uri.Host.ToLowerInvariant();
            if (allowLoopbackUrls &&
                string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
                (host == "127.0.0.1" || host == "localhost"))
            {
                return true;
            }
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return false;
            return host == "github.com" ||
                   host.EndsWith(".github.com", StringComparison.Ordinal) ||
                   host.EndsWith(".githubusercontent.com", StringComparison.Ordinal) ||
                   host.EndsWith(".github.io", StringComparison.Ordinal);
        }

        public static bool IsValidSha256Hex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string s = value.Trim();
            if (s.Length != 64) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }

        public static bool ParseManifestJson(
            string json,
            string expectedAssetName,
            out UpdateManifestDto manifest,
            out string error,
            bool allowLoopbackUrls = false)
        {
            manifest = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "empty response";
                return false;
            }

            UpdateManifestDto dto;
            try
            {
                dto = JsonUtility.FromJson<UpdateManifestDto>(json);
            }
            catch (Exception ex)
            {
                error = $"invalid JSON ({ex.Message})";
                return false;
            }

            if (dto == null)
            {
                error = "unreadable manifest object";
                return false;
            }
            if (dto.schemaVersion != ExpectedSchemaVersion)
            {
                error = $"unsupported schemaVersion ({dto.schemaVersion})";
                return false;
            }
            if (ParseVersionParts(dto.version) == null)
            {
                error = "invalid version";
                return false;
            }
            dto.version = dto.version.Trim();
            dto.assetName = (dto.assetName ?? string.Empty).Trim();
            dto.downloadUrl = (dto.downloadUrl ?? string.Empty).Trim();
            dto.releaseUrl = (dto.releaseUrl ?? string.Empty).Trim();
            dto.sha256 = (dto.sha256 ?? string.Empty).Trim().ToLowerInvariant();
            dto.publishedAtUtc = (dto.publishedAtUtc ?? string.Empty).Trim();
            dto.notes = (dto.notes ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(expectedAssetName) && !string.IsNullOrEmpty(dto.assetName) &&
                !string.Equals(dto.assetName, expectedAssetName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"asset mismatch ({dto.assetName})";
                return false;
            }
            if (!IsAllowedDownloadUrl(dto.downloadUrl, allowLoopbackUrls))
            {
                error = "untrusted or non-HTTPS downloadUrl";
                return false;
            }
            if (dto.sizeBytes < 0)
            {
                error = "negative sizeBytes";
                return false;
            }
            if (!string.IsNullOrEmpty(dto.sha256) && !IsValidSha256Hex(dto.sha256))
            {
                error = "invalid sha256";
                return false;
            }
            if (!string.IsNullOrEmpty(dto.releaseUrl) && !IsAllowedWebPageUrl(dto.releaseUrl, allowLoopbackUrls))
            {
                error = "untrusted releaseUrl";
                return false;
            }

            manifest = dto;
            return true;
        }

        public static bool ParseReleaseJson(
            string json,
            string expectedAssetName,
            out string tag,
            out string downloadUrl,
            out long sizeBytes,
            bool allowLoopbackUrls = false)
        {
            tag = string.Empty;
            downloadUrl = string.Empty;
            sizeBytes = 0;
            if (string.IsNullOrWhiteSpace(json)) return false;

            GitHubReleaseDto dto;
            try
            {
                dto = JsonUtility.FromJson<GitHubReleaseDto>(json);
            }
            catch
            {
                return false;
            }
            if (dto == null || ParseVersionParts(dto.tag_name) == null || dto.assets == null) return false;

            tag = dto.tag_name.Trim();
            for (int i = 0; i < dto.assets.Length; i++)
            {
                GitHubAssetDto a = dto.assets[i];
                if (a != null && string.Equals(a.name, expectedAssetName, StringComparison.OrdinalIgnoreCase) &&
                    IsAllowedDownloadUrl(a.browser_download_url, allowLoopbackUrls))
                {
                    downloadUrl = a.browser_download_url;
                    sizeBytes = a.size;
                    return true;
                }
            }
            return false;
        }

        public static bool IsRateLimitFailure(long statusCode, IDictionary<string, string> headers, string body, bool isGitHubApi)
        {
            if (statusCode == 429) return true;
            if (statusCode != 403) return false;
            if (isGitHubApi) return true;

            if (TryGetHeader(headers, "X-RateLimit-Remaining", out string rem) && string.Equals(rem.Trim(), "0", StringComparison.Ordinal))
            {
                return true;
            }
            if (TryGetHeader(headers, "Retry-After", out string retry) && !string.IsNullOrWhiteSpace(retry))
            {
                return true;
            }
            if (!string.IsNullOrEmpty(body) &&
                (body.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 body.IndexOf("secondary rate limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 body.IndexOf("abuse detection", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
            return false;
        }

        public static int ParseRetryDelaySeconds(long statusCode, IDictionary<string, string> headers, DateTime utcNow)
        {
            if (TryGetHeader(headers, "Retry-After", out string retryAfter) && !string.IsNullOrWhiteSpace(retryAfter))
            {
                string trimmed = retryAfter.Trim();
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int deltaSec))
                {
                    return ClampDelaySeconds(deltaSec);
                }
                if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset untilDto))
                {
                    int diff = (int)Math.Ceiling((untilDto.UtcDateTime - utcNow).TotalSeconds);
                    return ClampDelaySeconds(diff);
                }
            }

            if (TryGetHeader(headers, "X-RateLimit-Reset", out string resetStr) &&
                long.TryParse(resetStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long epochSec))
            {
                DateTime resetUtc = DateTimeOffset.FromUnixTimeSeconds(epochSec).UtcDateTime;
                int diff = (int)Math.Ceiling((resetUtc - utcNow).TotalSeconds);
                if (diff > 0) return ClampDelaySeconds(diff);
            }

            return statusCode == 429 || statusCode == 403 ? 60 : 30;
        }

        private static int ClampDelaySeconds(int seconds)
        {
            if (seconds < 10) return 10;
            if (seconds > 3600) return 3600;
            return seconds;
        }

        private static bool TryGetHeader(IDictionary<string, string> headers, string name, out string value)
        {
            value = null;
            if (headers == null || string.IsNullOrEmpty(name)) return false;
            if (headers.TryGetValue(name, out value)) return true;
            foreach (var kv in headers)
            {
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
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
            string stagingDir = Path.GetDirectoryName(zipPath) ?? gameDir;
            string qExtract = EscapePsSingle(Path.Combine(stagingDir, "extracted"));
            string qBackup = EscapePsSingle(Path.Combine(stagingDir, "backup"));
            string qErrFile = EscapePsSingle(Path.Combine(stagingDir, ErrorMarkerFileName));

            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$ProgressPreference = 'SilentlyContinue'");
            sb.AppendLine($"$targetPid = {appPid}");
            sb.AppendLine("for ($i = 0; $i -lt 40; $i++) {");
            sb.AppendLine("  if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { break }");
            sb.AppendLine("  Start-Sleep -Milliseconds 500");
            sb.AppendLine("}");
            sb.AppendLine("Start-Sleep -Milliseconds 500");
            sb.AppendLine($"$gameDir = '{qGame}'");
            sb.AppendLine($"$zipPath = '{qZip}'");
            sb.AppendLine($"$extractDir = '{qExtract}'");
            sb.AppendLine($"$backupDir = '{qBackup}'");
            sb.AppendLine($"$errFile = '{qErrFile}'");
            sb.AppendLine($"$exePath = '{qExe}'");
            sb.AppendLine("try {");
            sb.AppendLine("  if (Test-Path -LiteralPath $extractDir) { Remove-Item -LiteralPath $extractDir -Recurse -Force }");
            sb.AppendLine("  if (Test-Path -LiteralPath $backupDir) { Remove-Item -LiteralPath $backupDir -Recurse -Force }");
            sb.AppendLine("  if (Test-Path -LiteralPath $errFile) { Remove-Item -LiteralPath $errFile -Force }");
            sb.AppendLine("  Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force");
            sb.AppendLine("  $stagedExe = Join-Path $extractDir 'WoodlandLane.exe'");
            sb.AppendLine("  if (-not (Test-Path -LiteralPath $stagedExe)) { throw 'Missing WoodlandLane.exe in extracted archive.' }");
            sb.AppendLine("  New-Item -ItemType Directory -Path $backupDir -Force | Out-Null");
            sb.AppendLine("  Get-ChildItem -LiteralPath $extractDir | ForEach-Object {");
            sb.AppendLine("    $dest = Join-Path $gameDir $_.Name");
            sb.AppendLine("    if (Test-Path -LiteralPath $dest) { Copy-Item -LiteralPath $dest -Destination (Join-Path $backupDir $_.Name) -Recurse -Force }");
            sb.AppendLine("  }");
            sb.AppendLine("  Copy-Item -Path (Join-Path $extractDir '*') -Destination $gameDir -Recurse -Force");
            sb.AppendLine("  if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }");
            sb.AppendLine("  if (Test-Path -LiteralPath $backupDir) { Remove-Item -LiteralPath $backupDir -Recurse -Force }");
            sb.AppendLine("} catch {");
            sb.AppendLine("  $errMsg = $_.Exception.Message");
            sb.AppendLine("  if (Test-Path -LiteralPath $backupDir) { Copy-Item -Path (Join-Path $backupDir '*') -Destination $gameDir -Recurse -Force -ErrorAction SilentlyContinue }");
            sb.AppendLine("  Set-Content -LiteralPath $errFile -Value $errMsg -Encoding UTF8 -ErrorAction SilentlyContinue");
            sb.AppendLine("}");
            sb.AppendLine("Start-Process -FilePath $exePath -WorkingDirectory $gameDir");
            return sb.ToString();
        }

        private static string EscapePsSingle(string value) => (value ?? string.Empty).Replace("'", "''");

        public IEnumerator CheckForUpdatesCoroutine()
        {
            if (IsBusy) yield break;

            int cooldownRem = GetCooldownRemainingSeconds();
            if (cooldownRem > 0)
            {
                if (LastErrorKind == UpdateErrorKind.None)
                {
                    if (UpdateAvailable) Status = $"Update ready: {LatestTag}. Click Update or wait {cooldownRem}s to re-check.";
                }
                else
                {
                    ShowBrowserFallback = true;
                    Status = $"Update checks paused ({cooldownRem}s remaining after {DescribeLastError()}). Retry shortly or use Open Download Page.";
                }
                yield break;
            }

            IsBusy = true;
            UpdateAvailable = false;
            LastErrorKind = UpdateErrorKind.None;
            ShowBrowserFallback = false;
            LatestTag = string.Empty;
            LatestDownloadUrl = string.Empty;
            LatestSha256 = string.Empty;
            LatestSizeBytes = 0;
            BrowserReleaseUrl = DefaultReleasePageUrl();
            Status = "Checking for updates...";

            if (!string.IsNullOrWhiteSpace(config.manifestUrl) && !IsAllowedManifestUrl(config.manifestUrl, allowLoopbackUrls))
            {
                FailCheck(UpdateErrorKind.MalformedManifest, "Blocked invalid custom manifest URL. Using browser fallback.", isRateLimit: false, retryDelaySec: 0);
                IsBusy = false;
                yield break;
            }

            List<CheckEndpoint> endpoints = BuildCheckEndpoints();
            long lastHttpStatus = 0;
            string lastHttpErr = string.Empty;

            for (int i = 0; i < endpoints.Count; i++)
            {
                CheckEndpoint ep = endpoints[i];
                Status = ep.IsGitHubApi ? "Checking fallback release metadata..." : "Checking update manifest...";

                using (IUpdateHttpOp op = httpClient.GetText(ep.Url))
                {
                    while (!op.IsDone)
                    {
                        yield return null;
                    }

                    long code = op.StatusCode;
                    string body = op.Text ?? string.Empty;
                    IDictionary<string, string> headers = op.ResponseHeaders;

                    if (op.IsSuccess && code >= 200 && code < 300)
                    {
                        string remoteTag;
                        string dlUrl;
                        long sizeBytes;
                        string sha256 = string.Empty;

                        if (ep.IsGitHubApi)
                        {
                            if (!ParseReleaseJson(body, config.assetName, out remoteTag, out dlUrl, out sizeBytes, allowLoopbackUrls))
                            {
                                FailCheck(UpdateErrorKind.MalformedManifest,
                                    "Release metadata was invalid. Click Open Download Page to download manually.",
                                    isRateLimit: false, retryDelaySec: 0);
                                IsBusy = false;
                                yield break;
                            }
                        }
                        else
                        {
                            if (!ParseManifestJson(body, config.assetName, out UpdateManifestDto manifest, out string parseErr, allowLoopbackUrls))
                            {
                                FailCheck(UpdateErrorKind.MalformedManifest,
                                    $"Update manifest is malformed ({parseErr}). Click Open Download Page to download manually.",
                                    isRateLimit: false, retryDelaySec: 0);
                                IsBusy = false;
                                yield break;
                            }

                            remoteTag = manifest.version;
                            dlUrl = manifest.downloadUrl;
                            sizeBytes = manifest.sizeBytes;
                            sha256 = manifest.sha256;
                            if (!string.IsNullOrEmpty(manifest.releaseUrl))
                            {
                                BrowserReleaseUrl = manifest.releaseUrl;
                            }
                        }

                        consecutiveFailures = 0;
                        cooldownUntilUtc = utcNowProvider().AddSeconds(3);
                        LatestTag = remoteTag;
                        LatestDownloadUrl = dlUrl;
                        LatestSizeBytes = sizeBytes;
                        LatestSha256 = sha256;
                        LastErrorKind = UpdateErrorKind.None;
                        ShowBrowserFallback = false;

                        if (IsNewerVersion(CurrentVersion, remoteTag))
                        {
                            UpdateAvailable = true;
                            Status = sizeBytes > 0
                                ? $"Update ready: {remoteTag} ({sizeBytes / (1024f * 1024f):0.0} MB)."
                                : $"Update ready: {remoteTag}.";
                        }
                        else
                        {
                            Status = $"Up to date ({CurrentVersion}).";
                        }

                        IsBusy = false;
                        yield break;
                    }

                    if (IsRateLimitFailure(code, headers, body, ep.IsGitHubApi))
                    {
                        int waitSec = ParseRetryDelaySeconds(code, headers, utcNowProvider());
                        string rateDesc = code == 429 ? "HTTP 429 Too Many Requests" : "GitHub rate limit (HTTP 403)";
                        FailCheck(UpdateErrorKind.RateLimited,
                            $"{rateDesc}. Wait {waitSec}s or click Open Download Page.",
                            isRateLimit: true, retryDelaySec: waitSec);
                        IsBusy = false;
                        yield break;
                    }

                    if (op.IsNetworkError || code == 0)
                    {
                        string netErr = string.IsNullOrEmpty(op.Error) ? "Connection failed" : op.Error;
                        FailCheck(UpdateErrorKind.Network,
                            $"Cannot reach update server ({netErr}). Retry shortly or click Open Download Page.",
                            isRateLimit: false, retryDelaySec: 0);
                        IsBusy = false;
                        yield break;
                    }

                    lastHttpStatus = code;
                    lastHttpErr = op.Error;
                }
            }

            string codeText = lastHttpStatus > 0 ? $"HTTP {lastHttpStatus}" : (string.IsNullOrEmpty(lastHttpErr) ? "Unavailable" : lastHttpErr);
            FailCheck(UpdateErrorKind.Unavailable,
                $"No published update manifest is available yet ({codeText}). Click Open Download Page or retry later.",
                isRateLimit: false, retryDelaySec: 0);
            IsBusy = false;
        }

        private void FailCheck(UpdateErrorKind kind, string message, bool isRateLimit, int retryDelaySec)
        {
            LastErrorKind = kind;
            ShowBrowserFallback = true;
            Status = message;
            if (isRateLimit && retryDelaySec > 0)
            {
                consecutiveFailures++;
                cooldownUntilUtc = utcNowProvider().AddSeconds(retryDelaySec);
            }
            else
            {
                consecutiveFailures++;
                int backoff = Math.Min(300, 15 * (1 << Math.Min(4, consecutiveFailures - 1)));
                cooldownUntilUtc = utcNowProvider().AddSeconds(backoff);
            }
        }

        private string DescribeLastError()
        {
            switch (LastErrorKind)
            {
                case UpdateErrorKind.RateLimited: return "rate limit";
                case UpdateErrorKind.Network: return "network error";
                case UpdateErrorKind.MalformedManifest: return "invalid manifest";
                case UpdateErrorKind.DownloadFailed: return "interrupted download";
                case UpdateErrorKind.VerificationFailed: return "failed verification";
                default: return "recent failure";
            }
        }

        public IEnumerator DownloadAndApplyCoroutine()
        {
            if (IsBusy || !UpdateAvailable) yield break;
            if (string.IsNullOrWhiteSpace(overrideGameDir) && Application.isEditor)
            {
                Status = "Self-update only runs inside the standalone Windows player, not Unity Editor.";
                yield break;
            }
            if (!IsAllowedDownloadUrl(LatestDownloadUrl, allowLoopbackUrls))
            {
                LastErrorKind = UpdateErrorKind.VerificationFailed;
                ShowBrowserFallback = true;
                Status = "Blocked untrusted download URL. Use Open Download Page.";
                yield break;
            }

            IsBusy = true;
            DownloadProgress = 0f;
            LastErrorKind = UpdateErrorKind.None;
            Status = $"Downloading {LatestTag}...";

            string gameDir = ResolveGameDir();
            string stagingDir = Path.Combine(gameDir, StagingDirName);
            if (!Directory.Exists(stagingDir)) Directory.CreateDirectory(stagingDir);

            string partPath = Path.Combine(stagingDir, "update.zip.part");
            string zipPath = Path.Combine(stagingDir, "update.zip");
            string scriptPath = Path.Combine(stagingDir, "apply_update.ps1");
            string exePath = Path.Combine(gameDir, "WoodlandLane.exe");
            if (string.IsNullOrWhiteSpace(overrideGameDir))
            {
                string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe)) exePath = currentExe;
            }

            TryDeleteFile(partPath);
            TryDeleteFile(zipPath);

            using (IUpdateHttpOp op = httpClient.DownloadFile(LatestDownloadUrl, partPath))
            {
                while (!op.IsDone)
                {
                    DownloadProgress = op.Progress;
                    Status = $"Downloading {LatestTag}... {Mathf.RoundToInt(DownloadProgress * 100f)}%";
                    yield return null;
                }

                if (!op.IsSuccess || op.StatusCode < 200 || op.StatusCode >= 300)
                {
                    TryDeleteFile(partPath);
                    ShowBrowserFallback = true;
                    if (IsRateLimitFailure(op.StatusCode, op.ResponseHeaders, op.Text, isGitHubApi: false))
                    {
                        int waitSec = ParseRetryDelaySeconds(op.StatusCode, op.ResponseHeaders, utcNowProvider());
                        cooldownUntilUtc = utcNowProvider().AddSeconds(waitSec);
                        LastErrorKind = UpdateErrorKind.RateLimited;
                        Status = $"Download rate-limited (HTTP {op.StatusCode}). Game files untouched — wait {waitSec}s or use Open Download Page.";
                    }
                    else
                    {
                        LastErrorKind = UpdateErrorKind.DownloadFailed;
                        string err = string.IsNullOrEmpty(op.Error) ? $"HTTP {op.StatusCode}" : op.Error;
                        Status = $"Download interrupted ({err}). Game files untouched — click Update to retry or Open Download Page.";
                    }
                    IsBusy = false;
                    yield break;
                }
            }

            if (!ValidateDownloadedZip(partPath, LatestSizeBytes, LatestSha256, out string verifyErr))
            {
                TryDeleteFile(partPath);
                ShowBrowserFallback = true;
                LastErrorKind = UpdateErrorKind.VerificationFailed;
                Status = $"Downloaded ZIP failed integrity check ({verifyErr}). Game files untouched — retry or use Open Download Page.";
                IsBusy = false;
                yield break;
            }

            File.Move(partPath, zipPath);
            string scriptText = BuildApplyScript(Process.GetCurrentProcess().Id, gameDir, zipPath, exePath);
            File.WriteAllText(scriptPath, scriptText, new UTF8Encoding(false));

            Status = $"Installing {LatestTag} and restarting...";
            ApplyScriptLaunchedForTest = true;

            if (string.IsNullOrWhiteSpace(overrideGameDir) && !Application.isBatchMode)
            {
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

            IsBusy = false;
        }

        public static bool ValidateDownloadedZip(string zipPath, long expectedSizeBytes, string expectedSha256, out string error)
        {
            error = string.Empty;
            if (!File.Exists(zipPath))
            {
                error = "file missing";
                return false;
            }

            var fi = new FileInfo(zipPath);
            if (fi.Length <= 1024)
            {
                error = "archive too small";
                return false;
            }
            if (expectedSizeBytes > 0 && fi.Length != expectedSizeBytes)
            {
                error = $"size mismatch ({fi.Length} vs {expectedSizeBytes} bytes)";
                return false;
            }
            if (!HasValidZipHeader(zipPath))
            {
                error = "invalid ZIP header";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                string actualHash = ComputeFileSha256(zipPath);
                if (!string.Equals(actualHash, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    error = "SHA-256 mismatch";
                    return false;
                }
            }

            try
            {
                bool foundExe = false;
                byte[] drain = new byte[16384];
                using (FileStream fs = File.OpenRead(zipPath))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false))
                {
                    if (archive.Entries.Count == 0)
                    {
                        error = "empty archive";
                        return false;
                    }
                    for (int i = 0; i < archive.Entries.Count; i++)
                    {
                        ZipArchiveEntry entry = archive.Entries[i];
                        string normalized = (entry.FullName ?? string.Empty).Replace('\\', '/');
                        if (string.IsNullOrEmpty(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) ||
                            normalized.Contains(".."))
                        {
                            error = $"unsafe entry path ({entry.FullName})";
                            return false;
                        }
                        if (string.Equals(normalized, "WoodlandLane.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            foundExe = true;
                        }
                        if (!normalized.EndsWith("/", StringComparison.Ordinal))
                        {
                            using (Stream es = entry.Open())
                            {
                                while (es.Read(drain, 0, drain.Length) > 0) { }
                            }
                        }
                    }
                }
                if (!foundExe)
                {
                    error = "missing WoodlandLane.exe";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"corrupt ZIP ({ex.Message})";
                return false;
            }

            return true;
        }

        public static string ComputeFileSha256(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static bool HasValidZipHeader(string path)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                int b0 = fs.ReadByte();
                int b1 = fs.ReadByte();
                int b2 = fs.ReadByte();
                int b3 = fs.ReadByte();
                return b0 == 0x50 && b1 == 0x4B && b2 == 0x03 && b3 == 0x04;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Best-effort temp cleanup
            }
        }
    }
}
