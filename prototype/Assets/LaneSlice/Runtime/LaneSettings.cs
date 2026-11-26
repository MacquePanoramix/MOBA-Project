using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Moba.LaneSlice
{
    [Serializable]
    public sealed class KeybindingEntry
    {
        public string command;
        public string key;
    }

    [Serializable]
    public sealed class ExtraPrefEntry
    {
        public string key;
        public string value;
    }

    /// <summary>
    /// Serializable personal preferences & keybindings payload stored outside the game install
    /// directory (Application.persistentDataPath) so automatic updates never erase player settings.
    /// ExtraPrefEntry allows future settings to be added cleanly without breaking older configs.
    /// </summary>
    [Serializable]
    public sealed class PlayerSettingsData
    {
        public int version = 1;
        public bool enableOptionalRtsControls = false;
        public bool rtsDoubleTapGroupCentersCamera = true;
        public bool rtsAutoSelectSummons = false;
        public bool enableAbilityDemos = true;
        public List<KeybindingEntry> keybindings = new List<KeybindingEntry>();
        public List<ExtraPrefEntry> extraPrefs = new List<ExtraPrefEntry>();
    }

    public enum ConflictResolutionMode
    {
        RejectIfConflict = 0,
        SwapWithConflicting = 1,
        UnbindConflicting = 2,
    }

    public readonly struct BindingConflict
    {
        public readonly ControlCommand ConflictingCommand;
        public readonly ControlContext ConflictingContext;
        public readonly KeyCode Key;

        public BindingConflict(ControlCommand conflictingCommand, ControlContext conflictingContext, KeyCode key)
        {
            ConflictingCommand = conflictingCommand;
            ConflictingContext = conflictingContext;
            Key = key;
        }
    }

    /// <summary>
    /// Manages loading, atomic local persistence, validation, and context-aware keybinding conflict
    /// detection for player preferences across game restarts and automatic updates.
    /// </summary>
    public sealed class PlayerSettingsStore
    {
        public const string DefaultFileName = "personal_settings.json";

        private readonly string settingsPath;
        private readonly bool persistToDisk;
        private readonly Dictionary<ControlCommand, KeyCode> bindings = new Dictionary<ControlCommand, KeyCode>();
        private PlayerSettingsData data = new PlayerSettingsData();

        public string SettingsPath => settingsPath;
        public PlayerSettingsData Data => data;
        public string LastStatus { get; private set; } = string.Empty;

        public PlayerSettingsStore(string customPath = null, bool persistToDisk = true)
        {
            this.persistToDisk = persistToDisk;
            settingsPath = string.IsNullOrWhiteSpace(customPath)
                ? Path.Combine(Application.persistentDataPath, DefaultFileName)
                : customPath;
            ApplyDefaultsToMemory();
        }

        public static bool IsReservedKey(KeyCode key)
        {
            return key == KeyCode.Escape ||
                   key == KeyCode.LeftControl ||
                   key == KeyCode.RightControl;
        }

        public void Load()
        {
            ApplyDefaultsToMemory();
            LastStatus = string.Empty;

            if (!File.Exists(settingsPath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return;

                PlayerSettingsData loaded = JsonUtility.FromJson<PlayerSettingsData>(json);
                if (loaded == null) return;

                if (loaded.version <= 0) loaded.version = 1;
                if (loaded.keybindings == null) loaded.keybindings = new List<KeybindingEntry>();
                if (loaded.extraPrefs == null) loaded.extraPrefs = new List<ExtraPrefEntry>();

                data = loaded;
                for (int i = 0; i < data.keybindings.Count; i++)
                {
                    KeybindingEntry entry = data.keybindings[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.command)) continue;
                    if (!Enum.TryParse(entry.command.Trim(), ignoreCase: true, out ControlCommand cmd)) continue;
                    if (Enum.TryParse((entry.key ?? string.Empty).Trim(), ignoreCase: true, out KeyCode parsedKey) && !IsReservedKey(parsedKey))
                    {
                        bindings[cmd] = parsedKey;
                    }
                }
                SyncBindingsListFromMap();
            }
            catch (Exception ex)
            {
                LastStatus = $"Could not read saved settings ({ex.Message}); restored defaults.";
                ApplyDefaultsToMemory();
            }
        }

        public void Save()
        {
            SyncBindingsListFromMap();
            if (!persistToDisk) return;

            try
            {
                string dir = Path.GetDirectoryName(settingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(data, prettyPrint: true);
                string tmp = settingsPath + ".tmp";
                File.WriteAllText(tmp, json, new UTF8Encoding(false));
                File.Copy(tmp, settingsPath, overwrite: true);
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (Exception ex)
            {
                LastStatus = $"Failed to save settings: {ex.Message}";
            }
        }

        public void RestoreDefaults(bool save = true)
        {
            ApplyDefaultsToMemory();
            LastStatus = "Restored default settings and keybindings.";
            if (save) Save();
        }

        public KeyCode GetKey(ControlCommand cmd)
        {
            if (bindings.TryGetValue(cmd, out KeyCode k)) return k;
            return ControlCommandCatalog.GetMeta(cmd).DefaultKey;
        }

        public string GetKeyLabel(ControlCommand cmd) => ControlCommandCatalog.FormatKeyLabel(GetKey(cmd));

        public string GetAbilityKeyLabel(HeroControlProfile profile, LaneAbilitySlot slot)
        {
            if (profile != null && profile.TryGetAbilityCommand(slot, out ControlCommand cmd))
            {
                return GetKeyLabel(cmd);
            }
            switch (slot)
            {
                case LaneAbilitySlot.Q: return GetKeyLabel(ControlCommand.CastQ);
                case LaneAbilitySlot.W: return GetKeyLabel(ControlCommand.CastW);
                case LaneAbilitySlot.E: return GetKeyLabel(ControlCommand.CastE);
                case LaneAbilitySlot.R: return GetKeyLabel(ControlCommand.CastR);
                default: return string.Empty;
            }
        }

        public void SetOptionalRtsEnabled(bool enabled)
        {
            data.enableOptionalRtsControls = enabled;
            Save();
        }

        public void SetDoubleTapCenterCamera(bool enabled)
        {
            data.rtsDoubleTapGroupCentersCamera = enabled;
            Save();
        }

        public void SetAutoSelectSummons(bool enabled)
        {
            data.rtsAutoSelectSummons = enabled;
            Save();
        }

        public void SetAbilityDemosEnabled(bool enabled)
        {
            data.enableAbilityDemos = enabled;
            Save();
        }

        public string GetExtraPref(string key, string fallback = "")
        {
            if (string.IsNullOrEmpty(key) || data.extraPrefs == null) return fallback;
            for (int i = 0; i < data.extraPrefs.Count; i++)
            {
                if (string.Equals(data.extraPrefs[i].key, key, StringComparison.Ordinal))
                {
                    return data.extraPrefs[i].value ?? fallback;
                }
            }
            return fallback;
        }

        public void SetExtraPref(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (data.extraPrefs == null) data.extraPrefs = new List<ExtraPrefEntry>();
            for (int i = 0; i < data.extraPrefs.Count; i++)
            {
                if (string.Equals(data.extraPrefs[i].key, key, StringComparison.Ordinal))
                {
                    data.extraPrefs[i].value = value;
                    Save();
                    return;
                }
            }
            data.extraPrefs.Add(new ExtraPrefEntry { key = key, value = value });
            Save();
        }

        public List<BindingConflict> FindConflicts(ControlCommand target, KeyCode candidateKey)
        {
            var results = new List<BindingConflict>();
            if (candidateKey == KeyCode.None) return results;

            ControlCommandMeta targetMeta = ControlCommandCatalog.GetMeta(target);
            for (int i = 0; i < ControlCommandCatalog.All.Length; i++)
            {
                ControlCommandMeta other = ControlCommandCatalog.All[i];
                if (other.Command == target) continue;
                if (targetMeta.PairedCommand.HasValue && targetMeta.PairedCommand.Value == other.Command) continue;
                if (other.PairedCommand.HasValue && other.PairedCommand.Value == target) continue;
                if (!ControlCommandCatalog.ContextsCanOverlap(targetMeta.Context, other.Context)) continue;

                if (GetKey(other.Command) == candidateKey)
                {
                    results.Add(new BindingConflict(other.Command, other.Context, candidateKey));
                }
            }
            return results;
        }

        public bool TrySetBinding(
            ControlCommand cmd,
            KeyCode newKey,
            ConflictResolutionMode mode,
            out List<BindingConflict> conflicts)
        {
            conflicts = new List<BindingConflict>();
            if (IsReservedKey(newKey))
            {
                LastStatus = $"{ControlCommandCatalog.FormatKeyLabel(newKey)} is reserved for system/modifier actions.";
                return false;
            }

            conflicts = FindConflicts(cmd, newKey);
            if (conflicts.Count == 0)
            {
                bindings[cmd] = newKey;
                LastStatus = $"Bound {ControlCommandCatalog.GetMeta(cmd).DisplayName} to {ControlCommandCatalog.FormatKeyLabel(newKey)}.";
                Save();
                return true;
            }

            if (mode == ConflictResolutionMode.RejectIfConflict)
            {
                LastStatus = $"{ControlCommandCatalog.FormatKeyLabel(newKey)} conflicts with {ControlCommandCatalog.GetMeta(conflicts[0].ConflictingCommand).DisplayName}.";
                return false;
            }

            KeyCode previousKey = GetKey(cmd);
            bindings[cmd] = newKey;

            if (mode == ConflictResolutionMode.SwapWithConflicting)
            {
                for (int i = 0; i < conflicts.Count; i++)
                {
                    ControlCommand other = conflicts[i].ConflictingCommand;
                    bool canTransfer = i == 0 &&
                                       previousKey != KeyCode.None &&
                                       !IsReservedKey(previousKey) &&
                                       FindConflicts(other, previousKey).Count == 0;
                    bindings[other] = canTransfer ? previousKey : KeyCode.None;
                }
                LastStatus = $"Bound {ControlCommandCatalog.GetMeta(cmd).DisplayName} to {ControlCommandCatalog.FormatKeyLabel(newKey)} (swapped).";
            }
            else
            {
                for (int i = 0; i < conflicts.Count; i++)
                {
                    bindings[conflicts[i].ConflictingCommand] = KeyCode.None;
                }
                LastStatus = $"Bound {ControlCommandCatalog.GetMeta(cmd).DisplayName} to {ControlCommandCatalog.FormatKeyLabel(newKey)} (unbound previous).";
            }

            Save();
            return true;
        }

        public void ValidateAllBindings(out List<BindingConflict> conflicts, out List<ControlCommand> unbound)
        {
            conflicts = new List<BindingConflict>();
            unbound = new List<ControlCommand>();

            for (int i = 0; i < ControlCommandCatalog.All.Length; i++)
            {
                ControlCommand cmd = ControlCommandCatalog.All[i].Command;
                KeyCode k = GetKey(cmd);
                if (k == KeyCode.None)
                {
                    unbound.Add(cmd);
                    continue;
                }
                List<BindingConflict> c = FindConflicts(cmd, k);
                for (int j = 0; j < c.Count; j++)
                {
                    conflicts.Add(c[j]);
                }
            }
        }

        private void ApplyDefaultsToMemory()
        {
            data = new PlayerSettingsData();
            bindings.Clear();
            for (int i = 0; i < ControlCommandCatalog.All.Length; i++)
            {
                ControlCommandMeta meta = ControlCommandCatalog.All[i];
                bindings[meta.Command] = meta.DefaultKey;
            }
            SyncBindingsListFromMap();
        }

        private void SyncBindingsListFromMap()
        {
            if (data.keybindings == null) data.keybindings = new List<KeybindingEntry>();
            data.keybindings.Clear();
            for (int i = 0; i < ControlCommandCatalog.All.Length; i++)
            {
                ControlCommand cmd = ControlCommandCatalog.All[i].Command;
                KeyCode key = GetKey(cmd);
                data.keybindings.Add(new KeybindingEntry
                {
                    command = cmd.ToString(),
                    key = key.ToString(),
                });
            }
        }
    }
}
