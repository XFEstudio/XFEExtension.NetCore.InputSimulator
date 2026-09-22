[CmdletBinding()]
param(
    [string]$PackageDirectory = $PSScriptRoot,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedCertificateSha256,
    [switch]$TrustLocalDevelopmentCertificate
)
$ErrorActionPreference = 'Stop'
if (!$TrustLocalDevelopmentCertificate) {
    throw 'Explicitly select -TrustLocalDevelopmentCertificate to trust this local development code-signing certificate in LocalMachine Root and TrustedPublisher.'
}
if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this installation from an elevated PowerShell.' }
if ([Environment]::OSVersion.Version.Build -lt 22000 -or ![Environment]::Is64BitOperatingSystem) {
    throw 'This probe requires Windows 11 x64.'
}
$package = (Resolve-Path -LiteralPath $PackageDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $package 'probe.json') -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.hardwareId -cne 'Root\XFEInputUmProbe') { throw 'Invalid probe manifest.' }
$expectedFiles = @('XfeInputUmProbe.dll', 'XfeInputUmProbe.inf', 'XfeInputUmProbe.pdb',
    'XfeInputUmProbeSetup.exe', 'XfeInputUmProbeClient.exe', 'XfeInputUmProbe.cat', 'LocalDevelopment.cer')
if (@($manifest.files.PSObject.Properties).Count -ne $expectedFiles.Count) { throw 'Invalid file list.' }
foreach ($file in $expectedFiles) {
    $path = Join-Path $package $file
    if (!(Test-Path -LiteralPath $path -PathType Leaf) -or
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $manifest.files.$file) {
        throw "Package hash mismatch: $file"
    }
}
$certificatePath = Join-Path $package 'LocalDevelopment.cer'
if ((Get-FileHash -LiteralPath $certificatePath -Algorithm SHA256).Hash -ne $ExpectedCertificateSha256) {
    throw 'The certificate does not match the explicitly selected fingerprint.'
}
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.Thumbprint -ne $manifest.certificateThumbprint -or $certificate.HasPrivateKey -or
    $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'Invalid development certificate.' }
$usageExtension = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }
$usage = @($usageExtension.EnhancedKeyUsages | ForEach-Object { $_.Value })
$constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
if ($usage.Count -ne 1 -or $usage[0] -ne '1.3.6.1.5.5.7.3.3' -or !$constraints -or $constraints.CertificateAuthority) {
    throw 'Only a non-CA certificate restricted to code signing is accepted.'
}
$addedStores = @()
try {
    foreach ($store in @('Root', 'TrustedPublisher')) {
        if (!(Test-Path -LiteralPath "Cert:\LocalMachine\$store\$($certificate.Thumbprint)")) {
            $null = Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\LocalMachine\$store"
            $addedStores += $store
        }
    }
    foreach ($file in @('XfeInputUmProbe.dll', 'XfeInputUmProbe.cat', 'XfeInputUmProbeSetup.exe', 'XfeInputUmProbeClient.exe')) {
        $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $package $file)
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
            throw "Signature verification failed: $file"
        }
    }
    & (Join-Path $package 'XfeInputUmProbeSetup.exe') install (Join-Path $package 'XfeInputUmProbe.inf')
    if ($LASTEXITCODE -notin @(0, 3010)) { throw "Probe installation failed: $LASTEXITCODE" }
    if ($LASTEXITCODE -eq 3010) {
        Write-Host 'Windows requested a restart. No restart was initiated; runtime verification is pending.'
        exit 3010
    }
} catch {
    foreach ($store in $addedStores) {
        Remove-Item -LiteralPath "Cert:\LocalMachine\$store\$($certificate.Thumbprint)" -ErrorAction Continue
    }
    throw
}
& (Join-Path $package 'XfeInputUmProbeClient.exe')
if ($LASTEXITCODE) { throw 'Installation returned success but the UMDF feature probe failed. Inspect the device status and SetupAPI log; use Remove-Probe.ps1 to clean up.' }
Write-Host 'Installed and answered the read-only HID probe. Keyboard/mouse input generation and game compatibility were NOT tested.'
