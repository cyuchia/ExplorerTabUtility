using System;
using System.IO;
using System.Windows;
using System.Text.Json;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading;
using ExplorerTabUtility.Models;
using ExplorerTabUtility.Helpers;

namespace ExplorerTabUtility.Managers;

public static class SettingsManager
{
    private static readonly object Sync = new();
    // The in-process override is used by the isolated verification harness only.
    private static readonly string SettingsFilePath = AppContext.GetData("ExplorerTabUtility.SettingsPath") as string
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppName, Constants.SettingsFileName);
    private static readonly AppSettings Settings;
    private static readonly Timer ResizeSave = new(_ => SaveSettings(), null, Timeout.Infinite, Timeout.Infinite);
    private static bool _dirty;
    public static string? Recovery { get; }
    public static event EventHandler<PropertyChangedEventArgs>? StaticPropertyChanged;
    public static event Action<string>? SaveFailed;

    static SettingsManager()
    {
        Settings = AtomicJsonFile.Load<AppSettings>(SettingsFilePath, out var recovery);
        Recovery = recovery;
        if (Settings.Language is not ("auto" or "zh-CN" or "en")) Settings.Language = "auto";
    }

    private static T Read<T>(Func<T> get) { lock (Sync) return get(); }
    private static void Set<T>(Func<T> get, Action<T> set, T value, string name, bool delayed = false)
    {
        lock (Sync)
        {
            if (EqualityComparer<T>.Default.Equals(get(), value)) return;
            set(value);
            _dirty = true;
            if (delayed) ResizeSave.Change(300, Timeout.Infinite);
            else SaveSettings();
        }
        StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(name));
    }
    public static bool IsMouseHookActive { get => Read(() => Settings.MouseHook); set => Set(() => Settings.MouseHook, v => Settings.MouseHook = v, value, nameof(IsMouseHookActive)); }
    public static bool IsKeyboardHookActive { get => Read(() => Settings.KeyboardHook); set => Set(() => Settings.KeyboardHook, v => Settings.KeyboardHook = v, value, nameof(IsKeyboardHookActive)); }
    public static bool IsWindowHookActive { get => Read(() => Settings.WindowHook); set => Set(() => Settings.WindowHook, v => Settings.WindowHook = v, value, nameof(IsWindowHookActive)); }
    public static bool ReuseTabs { get => Read(() => Settings.ReuseTabs); set => Set(() => Settings.ReuseTabs, v => Settings.ReuseTabs = v, value, nameof(ReuseTabs)); }
    public static string HotKeyProfiles { get => Read(() => Settings.HotKeyProfiles); set => Set(() => Settings.HotKeyProfiles, v => Settings.HotKeyProfiles = v, value, nameof(HotKeyProfiles)); }
    public static Size FormSize { get => Read(() => Settings.FormSize); set => Set(() => Settings.FormSize, v => Settings.FormSize = v, value, nameof(FormSize), true); }
    public static bool SaveProfilesOnExit { get => Read(() => Settings.SaveProfilesOnExit); set => Set(() => Settings.SaveProfilesOnExit, v => Settings.SaveProfilesOnExit = v, value, nameof(SaveProfilesOnExit)); }
    public static bool IsFirstRun { get => Read(() => Settings.IsFirstRun); set => Set(() => Settings.IsFirstRun, v => Settings.IsFirstRun = v, value, nameof(IsFirstRun)); }
    public static bool IsTrayIconHidden { get => Read(() => Settings.IsTrayIconHidden); set => Set(() => Settings.IsTrayIconHidden, v => Settings.IsTrayIconHidden = v, value, nameof(IsTrayIconHidden)); }
    public static bool HaveThemeIssue { get => Read(() => Settings.HaveThemeIssue); set => Set(() => Settings.HaveThemeIssue, v => Settings.HaveThemeIssue = v, value, nameof(HaveThemeIssue)); }
    public static bool AutoUpdate { get => Read(() => Settings.AutoUpdate); set => Set(() => Settings.AutoUpdate, v => Settings.AutoUpdate = v, value, nameof(AutoUpdate)); }
    public static bool SaveClosedHistory { get => Read(() => Settings.SaveClosedWindows); set => Set(() => Settings.SaveClosedWindows, v => Settings.SaveClosedWindows = v, value, nameof(SaveClosedHistory)); }
    public static bool RestorePreviousWindows { get => Read(() => Settings.RestorePreviousWindows); set => Set(() => Settings.RestorePreviousWindows, v => Settings.RestorePreviousWindows = v, value, nameof(RestorePreviousWindows)); }
    public static WindowRecord[]? ClosedWindows { get => Read(() => Settings.ClosedWindows); set => Set(() => Settings.ClosedWindows, v => Settings.ClosedWindows = v, value, nameof(ClosedWindows)); }
    public static string Language { get => Read(() => Settings.Language); set => Set(() => Settings.Language, v => Settings.Language = v, value, nameof(Language)); }

    public static void SaveSettings()
    {
        lock (Sync)
        {
            if (!_dirty) return;
            try
            {
                AtomicJsonFile.Save(SettingsFilePath, JsonSerializer.Serialize(Settings));
                _dirty = false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                SaveFailed?.Invoke(ex.Message);
            }
        }
    }
}

internal class AppSettings
{
    public string Language { get; set; } = "auto";
    [System.Text.Json.Serialization.JsonExtensionData]
    public System.Collections.Generic.Dictionary<string, JsonElement>? Extra { get; set; }
    public bool MouseHook { get; set; }
    public bool KeyboardHook { get; set; } = true;
    public bool WindowHook { get; set; } = true;
    public bool ReuseTabs { get; set; } = true;
    public Size FormSize { get; set; } = new(852, 402);
    public bool SaveProfilesOnExit { get; set; } = true;
    public bool IsFirstRun { get; set; } = true;
    public bool IsTrayIconHidden { get; set; }
    public bool HaveThemeIssue { get; set; }
    public bool AutoUpdate { get; set; }
    public string HotKeyProfiles { get; set; } = Constants.DefaultHotKeyProfiles;
    public bool SaveClosedWindows { get; set; }
    public bool RestorePreviousWindows { get; set; }
    public WindowRecord[]? ClosedWindows { get; set; }
}