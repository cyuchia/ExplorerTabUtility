[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory)][ValidateSet('Register','Unregister')][string]$Action,
    [Parameter(Mandatory)][string]$LibraryPath
)
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitProcess) {throw 'Use a 64-bit PowerShell session.'}
$LibraryPath = [IO.Path]::GetFullPath($LibraryPath)
$classId = '{54BC92A2-D2B5-4BC6-8F9B-5C64F614A309}'
$name = 'ETU Native Visibility Bridge'
$class = "HKCU:\Software\Classes\CLSID\$classId"
$server = "$class\InprocServer32"
$bho = "HKLM:\Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\$classId"
if ($Action -eq 'Register' -and -not (Test-Path -LiteralPath $LibraryPath -PathType Leaf)) {throw 'Build the x64 DLL and supply its stable installation path.'}
if (-not $PSCmdlet.ShouldProcess("$LibraryPath; $class; $bho", $Action)) {return}
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {throw 'The machine BHO enumeration key requires an elevated PowerShell session, under the same user account.'}
foreach ($key in @($class, $bho)) {
    if ((Test-Path -LiteralPath $key) -and (Get-Item -LiteralPath $key).GetValue('') -ne $name) {throw "Unexpected registration at $key. Inspect it before proceeding."}
}
if (Test-Path -LiteralPath $server) {
    $registered = (Get-Item -LiteralPath $server).GetValue('')
    if ($registered -ne $LibraryPath) {throw 'A different library is registered. Unregister its exact path before changing locations.'}
}
if ($Action -eq 'Unregister') {
    # Remove only the explicit keys owned by this component; do not traverse other CLSIDs.
    foreach ($key in @($server, $class, $bho)) {
        if (Test-Path -LiteralPath $key) {
            if ((Get-Item -LiteralPath $key).SubKeyCount -gt 0) {throw "Unexpected child keys at $key; registration was not removed recursively."}
            Remove-Item -LiteralPath $key
        }
    }
    Write-Output 'Registration removed. Loaded modules remain mapped until Explorer exits normally.'
    return
}
$created = [Collections.Generic.List[string]]::new()
try {
    foreach ($key in @($class, $server, $bho)) {
        if (-not (Test-Path -LiteralPath $key)) {
            New-Item -Path $key -Force | Out-Null
            $created.Add($key)
        }
    }
    Set-Item -LiteralPath $class -Value $name
    Set-Item -LiteralPath $server -Value $LibraryPath
    New-ItemProperty -LiteralPath $server -Name ThreadingModel -Value Apartment -PropertyType String -Force | Out-Null
    Set-Item -LiteralPath $bho -Value $name
    New-ItemProperty -LiteralPath $bho -Name NoInternetExplorer -Value 1 -PropertyType DWord -Force | Out-Null
    Write-Output 'Registered for the current user. Newly created Explorer processes can load the bridge.'
} catch {
    # Roll back keys created by this invocation, in child-before-parent order.
    for ($i = $created.Count - 1; $i -ge 0; $i--) {
        $key = $created[$i]
        if ((Test-Path -LiteralPath $key) -and (Get-Item -LiteralPath $key).SubKeyCount -eq 0) {Remove-Item -LiteralPath $key}
    }
    throw
}
