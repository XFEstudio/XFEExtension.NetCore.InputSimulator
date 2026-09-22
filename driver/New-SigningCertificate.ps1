[CmdletBinding()]
param(
    [ValidateRange(1, 20)][int]$ValidityYears = 10,
    [string]$ProfilePath = (Join-Path $PSScriptRoot 'signing.local.json')
)
$ErrorActionPreference = 'Stop'
$profileFile = [IO.Path]::GetFullPath($ProfilePath)
if (Test-Path -LiteralPath $profileFile) {
    $existing = Get-Content -LiteralPath $profileFile -Raw | ConvertFrom-Json
    if ($existing.thumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'Invalid signing profile.' }
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$($existing.thumbprint)"
    if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date)) { throw 'The existing signing key is missing or expired; restore its encrypted backup or explicitly choose a new profile.' }
    if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)) -ne $existing.certificateSha256) { throw 'Signing profile certificate mismatch.' }
    Write-Host "Reusing existing signing identity: $($certificate.Thumbprint), expires $($certificate.NotAfter.ToString('o'))"
    return
}
$null = New-Item -ItemType Directory -Path (Split-Path -Parent $profileFile) -Force
$issued = Get-Date
# Encrypted export enables an owner-controlled backup without exporting a key during builds.
$certificate = New-SelfSignedCertificate -Type CodeSigningCert `
    -Subject 'CN=XFEstudio InputSimulator Code Signing' `
    -FriendlyName "XFEstudio InputSimulator - local trust code signing ($ValidityYears years)" `
    -CertStoreLocation Cert:\CurrentUser\My -Provider 'Microsoft Software Key Storage Provider' `
    -KeyAlgorithm RSA -KeyLength 4096 -HashAlgorithm SHA256 -KeyUsage DigitalSignature `
    -KeyExportPolicy ExportableEncrypted -NotBefore $issued.AddMinutes(-10) -NotAfter $issued.AddYears($ValidityYears) `
    -TextExtension @('2.5.29.19={critical}{text}ca=false')
$profile = [ordered]@{
    schemaVersion = 1
    subject = $certificate.Subject
    thumbprint = $certificate.Thumbprint
    certificateSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData))
    notAfter = $certificate.NotAfter.ToUniversalTime().ToString('o')
    timestampServer = 'http://timestamp.digicert.com'
}
[IO.File]::WriteAllText($profileFile, ($profile | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
Write-Host "Signing identity created: $($certificate.Thumbprint)"
Write-Host "Expires: $($certificate.NotAfter.ToString('o')); profile: $profileFile"
Write-Host 'No private key exported, certificate trust changed, or driver installed.'
