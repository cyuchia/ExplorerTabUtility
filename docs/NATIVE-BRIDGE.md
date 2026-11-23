# Optional native visibility bridge

Explorer-based “Show in folder” calls can create a visible temporary window before an out-of-process observer sees it. The bridge is an x64 COM `IObjectWithSite` extension loaded by Explorer through BHO enumeration. At `SetSite`, it guards a new, not-yet-presented window on the window's own thread. It does not replace `explorer.exe`, inject code into arbitrary applications, or alter the default folder-opening command.

The managed transfer starts showing or navigating the destination while capturing source selection. It verifies the destination path and applied selection before closing the source. The bridge itself does not read directory contents or selected file names.

## Lifetime and fallback

- Only the system `explorer.exe` host is accepted.
- A live ETU message-only controller and another reusable Explorer window must exist. With no controller, the bridge is inert. The first Explorer window opens normally.
- DWM cloaking, offscreen placement and a window-position callback prevent the temporary window from being presented during handoff.
- A 100 ms watchdog releases the window if the controller disappears or the eight-second guard deadline expires. Explicit release restores styles, position and visibility as well.
- Recovery requires Explorer's window thread to continue processing messages; this is not a recovery mechanism for a completely hung Explorer process.
- The module is pinned once a window callback is installed. This prevents COM reference release from unloading code while a window callback is still on the stack. It stays mapped until that Explorer process exits.
- Diagnostic logs contain timestamps, window handles and state transitions, not file names or selection contents. Each log file is bounded at about 2 MB. Native logs are per process, so the directory can accumulate files across many Explorer processes.

This extension is **opt-in** and depends on Explorer internals. Its registration is separate from the normal application installer. Runtime validation so far covers one Windows 11 build (26300 x64), not every supported Windows version or third-party Explorer extension combination.

## Registration

Build first. Copy the DLL to a stable installation location; do not register a temporary build directory if you intend to keep rebuilding it. Run the following from **64-bit elevated PowerShell under the same user account** that runs ETU:

```powershell
.\NativeBridge\Register.ps1 -Action Register -LibraryPath 'C:\YourStableFolder\ETUNativeBridge.dll' -WhatIf
.\NativeBridge\Register.ps1 -Action Register -LibraryPath 'C:\YourStableFolder\ETUNativeBridge.dll'
```

The script verifies the component identity before changing existing entries. The COM class is registered under the current user; the BHO enumeration entry is under HKLM and requires elevation. Existing registrations at a different DLL path must be explicitly unregistered first.

Class ID: `{54BC92A2-D2B5-4BC6-8F9B-5C64F614A309}`.

New Explorer processes can pick up the registration. No script forces Explorer, the desktop, or other applications to restart.

To unregister:

```powershell
.\NativeBridge\Register.ps1 -Action Unregister -LibraryPath 'C:\YourStableFolder\ETUNativeBridge.dll'
```

It removes only the component's named class/server/enumeration keys, refuses unexpected identities or child keys, and does not delete the library. Loaded copies remain mapped until Explorer exits normally. Exiting ETU also disables new guarded handoffs because its controller disappears.
