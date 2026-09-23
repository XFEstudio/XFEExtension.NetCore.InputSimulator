[CmdletBinding()]
param([string]$PackageDirectory = (Join-Path $PSScriptRoot 'payload\win-x64'))
$ErrorActionPreference = 'Stop'

# Reuse the bytes verified by Prepare-Payload.ps1 on the signing machine.
# This is a repository-integrity check, not Windows Authenticode trust validation.
# No SDK, WDK, signing key, certificate-store change or driver installation is needed.
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
$files = @('XfeInputDriver.dll', 'XfeInput.inf', 'XfeInput.cat', 'XfeInputSetup.exe', 'publisher.cer')
foreach ($file in ($files + @('payload.json', 'payload.props'))) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) {
        throw "Missing committed driver payload: $file. Restore the complete signed driver/payload/win-x64 directory from Git."
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $source 'payload.json') -Raw | ConvertFrom-Json
if ($manifest.protocol -ne 1 -or $manifest.signed -isnot [bool] -or !$manifest.signed -or
    $manifest.signingKind -cnotin @('self-signed', 'trusted-publisher') -or
    $manifest.timestamped -isnot [bool] -or !$manifest.timestamped) {
    throw 'The committed driver payload must be signed, timestamped and use protocol 1. Prepare it on the signing machine before committing.'
}
$entries = @($manifest.files.PSObject.Properties)
if ($entries.Count -ne $files.Count -or @($entries.Name | Where-Object { $_ -cnotin $files }).Count) {
    throw 'The driver manifest must contain exactly the five expected payload files.'
}
foreach ($file in $files) {
    $expected = $manifest.files.$file
    if ($expected -isnot [string] -or $expected -cnotmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $source $file) -Algorithm SHA256).Hash -ne $expected) {
        throw "Driver payload SHA-256 mismatch: $file. Do not modify signed files or normalize the INF line endings."
    }
}

[xml]$properties = Get-Content -LiteralPath (Join-Path $source 'payload.props') -Raw
if ($properties.Project.PropertyGroup.DriverPayloadSigned -cne 'true' -or
    $properties.Project.PropertyGroup.DriverPayloadSigningKind -cne $manifest.signingKind) {
    throw 'payload.props does not match the signed driver manifest.'
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new([IO.File]::ReadAllBytes((Join-Path $source 'publisher.cer')))
try {
    if ($certificate.HasPrivateKey) { throw 'Only the public publisher certificate may be committed.' }
    $now = [DateTime]::UtcNow
    if ($certificate.NotBefore.ToUniversalTime() -gt $now -or $certificate.NotAfter.ToUniversalTime() -le $now) {
        throw 'The publisher certificate is not currently valid. Refresh the signed driver package on the signing machine.'
    }
    # ConvertFrom-Json returns a string on older PowerShell and a DateTime on newer versions.
    if (([DateTimeOffset]$manifest.certificateExpires).UtcDateTime -ne $certificate.NotAfter.ToUniversalTime()) {
        throw 'The certificate expiry in payload.json does not match publisher.cer.'
    }
    $usage = @(($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }).EnhancedKeyUsages | ForEach-Object { $_.Value })
    $constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
    if ($usage.Count -ne 1 -or $usage[0] -ne '1.3.6.1.5.5.7.3.3' -or !$constraints -or $constraints.CertificateAuthority) {
        throw 'The publisher certificate must be non-CA and restricted to code signing.'
    }
    $selfSigned = [Convert]::ToBase64String($certificate.SubjectName.RawData) -eq [Convert]::ToBase64String($certificate.IssuerName.RawData)
    if ($selfSigned -ne ($manifest.signingKind -eq 'self-signed')) {
        throw 'The publisher certificate does not match the manifest signing kind.'
    }
} finally {
    $certificate.Dispose()
}

Write-Host "Committed driver payload integrity verified ($($manifest.signingKind)): $source"
Write-Host 'Reusing the signed package without native compilation, signing or certificate trust changes.'
