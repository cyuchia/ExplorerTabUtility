[CmdletBinding()]
param(
    [string]$DotNet = 'dotnet',
    [ValidateSet('net9.0-windows','net481')][string]$Framework = 'net9.0-windows',
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'Generate-Interop.ps1')
$project = Join-Path $repo 'ExplorerTabUtility\ExplorerTabUtility.csproj'
& $DotNet publish $project -c Release -f $Framework "-p:TargetFrameworks=$Framework" -p:UseGeneratedInterop=true --no-self-contained -o (Join-Path $repo "artifacts\managed\$Framework") --nologo
if ($LASTEXITCODE -ne 0) {throw 'Managed build failed.'}
if ($RunTests) {
    foreach ($directory in (Get-ChildItem -LiteralPath (Join-Path $repo 'tests') -Directory)) {
        $test = $directory.Name
        $testProject = Join-Path $directory.FullName "$test.csproj"
        if (-not (Test-Path -LiteralPath $testProject)) {continue}
        & $DotNet run --project $testProject -c Release -p:UseGeneratedInterop=true
        if ($LASTEXITCODE -ne 0) {throw "$test failed."}
    }
}
