[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run removal from an elevated PowerShell.' }
$devices = @(Get-CimInstance Win32_PnPEntity | Where-Object { $_.HardwareID -contains 'Root\XFEInput' })
foreach ($device in $devices) {
    if (!$device.PNPDeviceID.StartsWith('ROOT\XFEINPUT\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected input device instance.' }
    & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /remove-device $device.PNPDeviceID
    # Live HID collections can reject direct removal. Uninstall only an exclusively owned OEM package.
    if ($LASTEXITCODE -eq -536870351) {
        $drivers = @(Get-CimInstance Win32_PnPSignedDriver)
        $binding = @($drivers | Where-Object { $_.DeviceID -eq $device.PNPDeviceID -and $_.DriverProviderName -eq 'XFEstudio' })
        if ($binding.Count -ne 1 -or $binding[0].InfName -notmatch '^oem\d+\.inf$') { throw 'Cannot identify the project driver package safely.' }
        $inf = $binding[0].InfName
        if (@($drivers | Where-Object { $_.InfName -eq $inf -and $_.DeviceID -ne $device.PNPDeviceID }).Count) { throw 'Another device shares this package; use Device Manager.' }
        & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /delete-driver $inf /uninstall
        if ($LASTEXITCODE -notin @(0, 3010)) { throw "Package removal failed: $LASTEXITCODE" }
        if ($LASTEXITCODE -eq 0) {
            & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /remove-device $device.PNPDeviceID
        }
    }
    if ($LASTEXITCODE -notin @(0, 3010)) { throw "Device removal failed: $LASTEXITCODE" }
    if ($LASTEXITCODE -eq 3010) { Write-Warning 'Windows requires a restart to finish removal; no automatic restart was requested.' }
}
Write-Host 'XFE input devices removed (or pending restart as reported above). Publisher trust retained.'
