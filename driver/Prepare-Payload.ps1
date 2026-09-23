[CmdletBinding()]
param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '..\artifacts\driver-umdf'),
    [switch]$AllowUnsigned,
    [Alias('AllowLocalDevelopment')][switch]$AllowSelfSigned,
    [switch]$RequireTimestamp
)
$ErrorActionPreference = 'Stop'
if ($AllowUnsigned -and ($AllowSelfSigned -or $RequireTimestamp)) { throw 'Choose unsigned or signed mode, not both.' }
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
$destination = Join-Path $PSScriptRoot 'payload\win-x64'
$files = @('XfeInputDriver.dll', 'XfeInput.inf', 'XfeInput.cat', 'XfeInputSetup.exe')
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) { throw "Missing driver payload: $file" }
}
$signed = !$AllowUnsigned
$kind = 'unsigned'
$certificate = $null
$timestamped = $signed
if ($signed) {
    foreach ($file in @('XfeInputDriver.dll', 'XfeInput.cat', 'XfeInputSetup.exe')) {
        $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $source $file)
        if ($signature.Status -ne 'Valid') { throw "Invalid or untrusted signature: $file ($($signature.Status)). Build signed packages on a machine that trusts the publisher." }
        if ($certificate -and $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'All payload binaries must use the same publisher certificate.' }
        $certificate = $signature.SignerCertificate
        if (!$signature.TimeStamperCertificate) { $timestamped = $false }
    }
    if ($RequireTimestamp -and !$timestamped) { throw 'All signed payload files must have a verified timestamp.' }
    if ($certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'Publisher certificate is not currently valid.' }
    $usage = @(($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }).EnhancedKeyUsages | ForEach-Object { $_.Value })
    $constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
    if ($usage.Count -ne 1 -or $usage[0] -ne '1.3.6.1.5.5.7.3.3' -or !$constraints -or $constraints.CertificateAuthority) {
        throw 'Use a non-CA certificate restricted to code signing.'
    }
    $selfSigned = [Convert]::ToHexString($certificate.SubjectName.RawData) -eq [Convert]::ToHexString($certificate.IssuerName.RawData)
    if ($selfSigned -and !$AllowSelfSigned) { throw 'A self-signed publisher requires -AllowSelfSigned; it is not a publicly trusted release.' }
    $kind = if ($selfSigned) { 'self-signed' } else { 'trusted-publisher' }
    $assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
    $signTool = $null
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder 'microsoft.windows.sdk.cpp\10.0.26100.1\c\bin\10.0.26100.0\x64\signtool.exe'
        if (Test-Path -LiteralPath $candidate) { $signTool = $candidate; break }
    }
    if (!$signTool) { throw 'Run Build-Driver.ps1 first to restore verification tools.' }
    foreach ($file in @('XfeInputDriver.dll', 'XfeInput.inf')) {
        & $signTool verify /pa /c (Join-Path $source 'XfeInput.cat') (Join-Path $source $file)
        if ($LASTEXITCODE) { throw "Signed catalog membership validation failed: $file" }
    }
}
$null = New-Item -ItemType Directory -Path $destination -Force
$hashes = [ordered]@{}
foreach ($file in $files) {
    Copy-Item -LiteralPath (Join-Path $source $file) -Destination (Join-Path $destination $file) -Force
    $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $destination $file) -Algorithm SHA256).Hash
}
if ($signed) {
    [IO.File]::WriteAllBytes((Join-Path $destination 'publisher.cer'), $certificate.RawData)
    $hashes['publisher.cer'] = (Get-FileHash -LiteralPath (Join-Path $destination 'publisher.cer') -Algorithm SHA256).Hash
} elseif (Test-Path -LiteralPath (Join-Path $destination 'publisher.cer')) {
    Remove-Item -LiteralPath (Join-Path $destination 'publisher.cer')
}
# Remove only the obsolete generated KMDF payload, never arbitrary user files.
if (Test-Path -LiteralPath (Join-Path $destination 'XfeInput.sys')) { Remove-Item -LiteralPath (Join-Path $destination 'XfeInput.sys') }
$manifest = [ordered]@{
    protocol = 1; signed = $signed; signingKind = $kind; timestamped = $timestamped
    certificateExpires = if ($signed) { $certificate.NotAfter.ToUniversalTime().ToString('o') } else { $null }
    files = $hashes
}
[IO.File]::WriteAllText((Join-Path $destination 'payload.json'), ($manifest | ConvertTo-Json -Depth 3), [Text.UTF8Encoding]::new($false))
$properties = "<Project><PropertyGroup><DriverPayloadSigned>$($signed.ToString().ToLowerInvariant())</DriverPayloadSigned><DriverPayloadSigningKind>$kind</DriverPayloadSigningKind></PropertyGroup></Project>"
[IO.File]::WriteAllText((Join-Path $destination 'payload.props'), $properties, [Text.UTF8Encoding]::new($false))
Write-Host "Embedded payload prepared ($kind): $destination"
