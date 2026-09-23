[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '..\artifacts\driver-umdf'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\driver-local'),
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
if ($CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$') { throw 'Invalid certificate thumbprint.' }
$certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
if (!$certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or $certificate.NotBefore -gt (Get-Date)) { throw 'A valid certificate with a private key in CurrentUser\My is required.' }
if ($TimestampServer -notmatch '^https?://[^\s]+$') { throw 'An RFC 3161 HTTP(S) timestamp server is required.' }
$usage = @(($certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' }).EnhancedKeyUsages | ForEach-Object { $_.Value })
$constraints = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
if ($usage.Count -ne 1 -or $usage[0] -ne '1.3.6.1.5.5.7.3.3' -or !$constraints -or $constraints.CertificateAuthority) { throw 'Use a non-CA certificate restricted to code signing.' }
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count) { throw 'Choose an empty output directory to preserve signed packages.' }
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'obj\project.assets.json') -Raw | ConvertFrom-Json
$signTool = $null
$wdk = $null
foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
    $candidate = Join-Path $folder 'microsoft.windows.sdk.cpp\10.0.26100.1\c\bin\10.0.26100.0\x64\signtool.exe'
    if (Test-Path -LiteralPath $candidate) { $signTool = $candidate }
    $candidate = Join-Path $folder 'microsoft.windows.wdk.x64\10.0.26100.6584\c'
    if (Test-Path -LiteralPath $candidate) { $wdk = $candidate }
}
if (!$signTool -or !$wdk) { throw 'Run Build-Driver.ps1 first.' }
$files = @('XfeInputDriver.dll', 'XfeInput.inf', 'XfeInputSetup.exe', 'XfeInputDriver.pdb')
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) { throw "Missing: $file" }
}
$null = New-Item -ItemType Directory -Path $destination -Force
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $source $file) -Destination $destination }
& $signTool sign /fd SHA256 /sha1 $CertificateThumbprint /s My /tr $TimestampServer /td SHA256 (Join-Path $destination 'XfeInputDriver.dll')
if ($LASTEXITCODE) { throw 'Driver signing failed.' }
$inf2cat = Get-ChildItem -LiteralPath $wdk -Filter inf2cat.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $inf2cat "/driver:$destination" /os:10_CO_X64 /uselocaltime
if ($LASTEXITCODE) { throw 'Catalog generation failed.' }
foreach ($file in @('XfeInput.cat', 'XfeInputSetup.exe')) {
    & $signTool sign /fd SHA256 /sha1 $CertificateThumbprint /s My /tr $TimestampServer /td SHA256 (Join-Path $destination $file)
    if ($LASTEXITCODE) { throw "Signing failed: $file" }
}
foreach ($file in @('XfeInputDriver.dll', 'XfeInput.cat', 'XfeInputSetup.exe')) {
    $signature = Get-AuthenticodeSignature -LiteralPath (Join-Path $destination $file)
    if ($signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint -or !$signature.TimeStamperCertificate) {
        throw "Missing publisher signature or timestamp: $file"
    }
}
[IO.File]::WriteAllBytes((Join-Path $destination 'publisher.cer'), $certificate.RawData)
Write-Host "Local signed package: $destination"
Write-Host "Expires: $($certificate.NotAfter.ToString('o')). No private key exported, system trust changed, or driver installed."
