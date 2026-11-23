# Build and verify

## Managed application

Requirements: 64-bit Windows, .NET SDK 9, and the Windows SDK .NET Framework tools (`TlbImp.exe`). Visual Studio can also generate the COM wrappers using the original COM references.

From the repository root in PowerShell:

```powershell
.\scripts\Build.ps1 -RunTests
```

Use `-DotNet 'path\to\dotnet.exe'` if the SDK is not on PATH. The script generates the two COM wrappers from the local Windows type libraries, then publishes to `artifacts/managed/net9.0-windows`. Generated wrappers and build output are ignored by Git. No wrappers from somebody else's Windows installation are distributed in this repository.

For a Visual Studio build, open `ExplorerTabUtility.sln` and use the original COM references; `UseGeneratedInterop` defaults to false. For the CLI build, the script explicitly enables `UseGeneratedInterop` after generating wrappers.

The existing .NET Framework target can be built using `-Framework net481` with its targeting pack installed. Runtime verification of the fork was performed on .NET 9; a successful net481 build is not a runtime compatibility claim.

Tests can also be run separately after generating wrappers:

```powershell
dotnet run --project tests/ManagedSafetyTests -c Release
dotnet run --project tests/PreferencesTests -c Release -p:UseGeneratedInterop=true
```

`ManagedSafetyTests` checks transfer permit ownership, expiry and exceptions. `PreferencesTests` uses an isolated temporary settings directory and a unique `HKCU\Software\ExplorerTabUtility-Verification` subkey; it does not toggle the real startup entry or start Explorer hooks. It renders existing settings controls offscreen in Chinese and English, checks live language changes, and preserves unknown settings and profile names. The output directory is printed at the end.

## Optional native visibility bridge

Requirements: Visual Studio 2022 Build Tools with **Desktop development with C++**, including an x64 compiler and Windows SDK.

```powershell
.\NativeBridge\build.ps1 -RunTests
```

This produces `artifacts/native/ETUNativeBridge.dll`. The tests create their own windows and use an isolated controller name; they do not register a COM server, restart Explorer, or interact with the running application's controller. The test executable is named `ExplorerTabUtility.exe` to exercise the production controller identity check, but has no startup registration or application mutex.

Building does not install or enable the bridge. See [native bridge behavior and registration](NATIVE-BRIDGE.md) before opting in. The managed application remains usable without it.
