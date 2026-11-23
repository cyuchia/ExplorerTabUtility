[CmdletBinding()]
param([string]$TlbImpPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repo 'ExplorerTabUtility\interop'
if (-not $TlbImpPath) {
    $sdkTools = Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Windows\v10.0A\bin'
    $TlbImpPath = Get-ChildItem -LiteralPath $sdkTools -Filter TlbImp.exe -Recurse |
        Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $TlbImpPath -or -not (Test-Path -LiteralPath $TlbImpPath)) {
    throw 'Install the Windows SDK .NET Framework tools, or supply -TlbImpPath.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach ($library in @(@('SHDocVw','shdocvw.dll'), @('Shell32','shell32.dll'))) {
    $source = Join-Path $env:WINDIR ('System32\' + $library[1])
    $target = Join-Path $output ('Interop.' + $library[0] + '.dll')
    & $TlbImpPath $source "/out:$target" "/namespace:$($library[0])" /machine:Agnostic /silent
    if ($LASTEXITCODE -ne 0) {throw "COM wrapper generation failed for $source"}
}
