# Validation and scope

## Automated checks

On 2026-11-23, the portable build scripts were run from a fresh checkout on Windows 11 build 26300 x64 using .NET SDK 9.0.321 and Visual C++ 2022 tools.

| Area | Result |
| --- | --- |
| .NET 9 and .NET Framework 4.8.1 managed publish | Both passed |
| Transfer permit ownership, expiry and exception handling | 10 checks passed |
| Settings recovery, concurrency, startup registration and localization | 29 checks passed |
| Native visibility, explicit release, timeout and COM/window lifetime | 20 checks passed |
| Registration helper | `-WhatIf` verified; publication did not replace the existing machine registration |

The source and commands for these checks are included. Temporary state, personal configuration, generated binaries, process dumps and local diagnostic logs are not repository inputs.

## Real Explorer checks

The desktop build from which this branch was prepared was checked with six real request types:

1. Select file A in an existing folder tab.
2. Select a different file B in that folder.
3. Select a file in a newly opened folder with Unicode, spaces, `#` and `%` in its path.
4. Select multiple files through `SHOpenFolderAndSelectItems`.
5. Open a directory through the Explorer command line.
6. Open a directory through `ShellExecute`.

All reached the expected directory and selection, with one matching destination tab and no leftover temporary window. Ten-millisecond visibility sampling did not observe a temporary window presented onscreen. Sampling is not frame-by-frame capture and cannot establish a universal absence of flashing.

An immediate A → B → A sequence also retained correct selection and one destination tab. The destination is shown before selection finishes; total timing still includes Explorer startup and directory loading.

For publication, the native build/test entry points and COM-wrapper generation were made portable, native tests received an isolated controller name, and upstream English enum descriptions/profile naming were retained. The controller window handle construction was made compatible with the legacy .NET Framework target. The portable checkout was rebuilt and its isolated tests rerun. The packaged checkout was not installed over the working desktop instance merely to publish a PR.

## Boundaries

- The optional native bridge has been exercised on build 26300 x64. Other Windows releases, ARM64, long uptime, suspend/resume, unavailable network folders and third-party shell extensions need further validation.
- Only .NET 9 received runtime tests. The legacy target remains in the project.
- Automatic tab limits and unconditional session restoration are not implemented. A trial close command addressed to an inactive tab closed the active tab on this Windows build; distinguishing whole-window close from closing the final tab is also unresolved. Those experiments are not part of the branch.
- Short resource samples do not demonstrate absence of long-term memory or handle leaks.
