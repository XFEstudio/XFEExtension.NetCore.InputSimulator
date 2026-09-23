[CmdletBinding()]
param(
    [string]$PackageDirectory = $PSScriptRoot,
    [switch]$KeepCertificateTrust,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedCertificateSha256
)
$ErrorActionPreference = 'Stop'
if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run removal from an elevated PowerShell.' }
$certificatePath = Join-Path (Resolve-Path -LiteralPath $PackageDirectory).Path 'LocalDevelopment.cer'
if ((Get-FileHash -LiteralPath $certificatePath -Algorithm SHA256).Hash -ne $ExpectedCertificateSha256) {
    throw 'The certificate does not match the explicitly selected fingerprint.'
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
$instances = @(Get-CimInstance Win32_PnPEntity | Where-Object { $_.HardwareID -contains 'Root\XFEInputUmProbe' })
foreach ($device in $instances) {
    if (!$device.PNPDeviceID.StartsWith('ROOT\XFEINPUTUMPROBE\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected probe device instance.' }
    & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /remove-device $device.PNPDeviceID
    if ($LASTEXITCODE -eq -536870351) {
        $drivers = @(Get-CimInstance Win32_PnPSignedDriver)
        $binding = @($drivers | Where-Object { $_.DeviceID -eq $device.PNPDeviceID -and $_.DriverProviderName -eq 'XFEstudio Local Development' })
        if ($binding.Count -ne 1 -or $binding[0].InfName -notmatch '^oem\d+\.inf$') { throw 'Cannot identify the probe package safely.' }
        $inf = $binding[0].InfName
        if (@($drivers | Where-Object { $_.InfName -eq $inf -and $_.DeviceID -ne $device.PNPDeviceID }).Count) { throw 'Another device shares the probe package.' }
        & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /delete-driver $inf /uninstall
        if ($LASTEXITCODE -notin @(0, 3010)) { throw "Package removal failed: $LASTEXITCODE" }
        if ($LASTEXITCODE -eq 0) {
            & (Join-Path $env:SystemRoot 'System32\pnputil.exe') /remove-device $device.PNPDeviceID
        }
    }
    if ($LASTEXITCODE -notin @(0, 3010)) { throw "Device removal failed: $LASTEXITCODE" }
}
if (!$KeepCertificateTrust) {
    if (@(Get-CimInstance Win32_PnPEntity | Where-Object { $_.HardwareID -contains 'Root\XFEInput' }).Count) {
        throw 'The main input driver may share this certificate. Remove the probe with -KeepCertificateTrust.'
    }
    foreach ($store in @('Root', 'TrustedPublisher')) {
        $path = "Cert:\LocalMachine\$store\$($certificate.Thumbprint)"
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
}
Write-Host "Probe removal requested. Certificate trust retained: $KeepCertificateTrust. CurrentUser signing key retained."
