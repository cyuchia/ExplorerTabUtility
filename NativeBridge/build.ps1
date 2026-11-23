[CmdletBinding()]
param([switch]$RunTests)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. (Join-Path $repo 'scripts\Enter-BuildEnvironment.ps1')
$output = Join-Path $repo 'artifacts\native'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Push-Location $output
try {
    $libraries = @('user32.lib','ole32.lib','oleaut32.lib','uuid.lib','comctl32.lib','dwmapi.lib')
    $flags = @('/nologo','/std:c++17','/W4','/O2','/MT','/EHsc','/guard:cf','/DUNICODE','/D_UNICODE')
    & cl.exe @flags /LD (Join-Path $PSScriptRoot 'NativeBridge.cpp') /link /OUT:ETUNativeBridge.dll /EXPORT:DllGetClassObject,PRIVATE /EXPORT:DllCanUnloadNow,PRIVATE /DYNAMICBASE /NXCOMPAT /GUARD:CF @libraries
    if ($LASTEXITCODE -ne 0) {throw 'Native bridge build failed.'}
    if ($RunTests) {
        & cl.exe @flags /DETU_BRIDGE_TEST /LD (Join-Path $PSScriptRoot 'LifetimeTestDll.cpp') /link /OUT:GuardLifetimeTest.dll @libraries
        if ($LASTEXITCODE -ne 0) {throw 'Lifetime test library build failed.'}
        & cl.exe @flags /DETU_BRIDGE_TEST (Join-Path $PSScriptRoot 'BridgeTests.cpp') /Fe:ExplorerTabUtility.exe /link @libraries
        if ($LASTEXITCODE -ne 0) {throw 'Native test executable build failed.'}
        $test = Start-Process -FilePath (Join-Path $output 'ExplorerTabUtility.exe') -WorkingDirectory $output -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $output 'test-results.txt')
        Get-Content -LiteralPath (Join-Path $output 'test-results.txt')
        if ($test.ExitCode -ne 0) {throw 'Native guard tests failed.'}
    }
} finally {Pop-Location}
