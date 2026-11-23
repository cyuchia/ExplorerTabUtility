$ErrorActionPreference = 'Stop'
if ((Get-Command cl.exe -ErrorAction SilentlyContinue) -and $env:VSCMD_ARG_TGT_ARCH -eq 'x64') {return}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {throw 'Install Visual Studio Build Tools with Desktop development with C++.'}
$installation = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $installation) {throw 'Visual C++ x64 build tools were not found.'}
Import-Module (Join-Path $installation 'Common7\Tools\Microsoft.VisualStudio.DevShell.dll')
Enter-VsDevShell -VsInstallPath $installation -SkipAutomaticLocation -DevCmdArguments '-arch=x64 -host_arch=x64' | Out-Null
