using System;
using Microsoft.Win32;
using ExplorerTabUtility.Helpers;

namespace ExplorerTabUtility.Managers;

public static class RegistryManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ExplorerAdvancedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private static readonly string? ExecutablePath = Helper.GetExecutablePath();
    private static StartupRegistration Registration => new(Registry.CurrentUser, RunKeyPath, StartupApprovedKeyPath, Constants.AppName, ExecutablePath ?? "");
    public static bool IsStartupEnabled => Registration.IsEnabled;
    public static event Action? StartupChanged;
    public static void ToggleStartup() => SetStartupEnabled(!IsStartupEnabled);
    public static void SetStartupEnabled(bool enabled)
    {
        try { Registration.SetEnabled(enabled); }
        finally { StartupChanged?.Invoke(); }
    }

    public static int GetDefaultExplorerLaunchId()
    {
        using var key = OpenCurrentUserKey(ExplorerAdvancedKeyPath, false);
        if (key == null) return 1;
        return key.GetValue("LaunchTo") as int? ?? 1;
    }

    private static RegistryKey? OpenCurrentUserKey(string name, bool writable) => Registry.CurrentUser.OpenSubKey(name, writable);
}