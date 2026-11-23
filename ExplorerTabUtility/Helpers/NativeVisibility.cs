using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExplorerTabUtility.Helpers;

// No global hook or executable interception. A message-only window advertises
// readiness to the optional native Explorer extension in this desktop session.
internal static class NativeVisibility
{
    private const string GuardProperty = "ETU.NativeVisibility.Guarded.v1";
    private static HwndSource? _controller;
    private static readonly object LogLock = new();
    private static readonly uint ReleaseMessage = RegisterWindowMessage("ETU.NativeVisibility.Release.v1");

    internal static bool Managed(nint window) => GetProp(window, GuardProperty) != 0;
    internal static bool Guarded(nint window) => GetProp(window, GuardProperty) == 1;
    internal static void SetEnabled(bool enabled)
    {
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (enabled && _controller == null)
                _controller = new HwndSource(new HwndSourceParameters("ETU.NativeVisibility.Controller.v2")
                { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
            else if (!enabled)
            {
                _controller?.Dispose();
                _controller = null;
            }
        }));
    }
    internal static void Release(nint window)
    {
        if (Managed(window)) PostMessage(window, ReleaseMessage, 0, 0);
    }
    internal static void Transferred(nint window)
    {
        if (Guarded(window)) SetProp(window, "ETU.NativeVisibility.Transferred.v1", 1);
    }
    internal static void Log(string message, nint window = 0)
    {
        try
        {
            lock (LogLock)
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExplorerTabUtilityBridge");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "controller.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) return;
                File.AppendAllText(path, $"{DateTime.Now:O} hwnd={window:X} {message}{Environment.NewLine}");
            }
        }
        catch { /* Diagnostics must not affect navigation. */ }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint GetProp(nint h, string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(nint h, string name, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint h, uint msg, nint wp, nint lp);
}
