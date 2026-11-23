using System;
using Microsoft.Win32;

namespace ExplorerTabUtility.Helpers;

public sealed class StartupRegistration(RegistryKey root, string runPath, string approvedPath, string name, string executable)
{
    public bool IsEnabled
    {
        get
        {
            using var run = root.OpenSubKey(runPath);
            var command = (run?.GetValue(name) as string)?.Trim().Trim('"');
            if (!string.Equals(command, executable, StringComparison.OrdinalIgnoreCase)) return false;
            using var approved = root.OpenSubKey(approvedPath);
            var value = approved?.GetValue(name) as byte[];
            return value == null || value.Length == 0 || value[0] % 2 == 0;
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("Executable path unavailable.");
        if (enabled)
        {
            using var run = root.CreateSubKey(runPath, true);
            run!.SetValue(name, "\"" + executable + "\"", RegistryValueKind.String);
            using var approved = root.CreateSubKey(approvedPath, true);
            var state = new byte[12];
            state[0] = 2;
            approved!.SetValue(name, state, RegistryValueKind.Binary);
        }
        else
        {
            using var run = root.OpenSubKey(runPath, true);
            run?.DeleteValue(name, false);
            using var approved = root.OpenSubKey(approvedPath, true);
            approved?.DeleteValue(name, false);
        }
        if (IsEnabled != enabled) throw new InvalidOperationException("Startup registration did not match the requested state.");
    }
}
