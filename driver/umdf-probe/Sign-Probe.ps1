[CmdletBinding()]
param(
    [string]$PackageDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\umdf-probe'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\umdf-probe-signed')
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $PackageDirectory).Path
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count) {
    throw 'Choose an empty output directory to preserve existing probe packages.'
}
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\obj\project.assets.json') -Raw | ConvertFrom-Json
foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
    $candidate = Join-Path $folder 'microsoft.windows.sdk.cpp\10.0.26100.1\c\bin\10.0.26100.0\x64\signtool.exe'
    if (Test-Path -LiteralPath $candidate) { $signTool = $candidate }
    $candidate = Join-Path $folder 'microsoft.windows.wdk.x64\10.0.26100.6584\c'
    if (Test-Path -LiteralPath $candidate) { $wdk = $candidate }
}
if (!$signTool -or !$wdk) { throw 'Run Build-Probe.ps1 first.' }
$files = @('XfeInputUmProbe.dll', 'XfeInputUmProbe.inf', 'XfeInputUmProbe.pdb',
    'XfeInputUmProbeSetup.exe', 'XfeInputUmProbeClient.exe')
foreach ($file in $files) {
    if (!(Test-Path -LiteralPath (Join-Path $source $file) -PathType Leaf)) { throw "Missing: $file" }
}
$null = New-Item -ItemType Directory -Path $destination -Force
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $source $file) -Destination $destination }
# Private key remains non-exportable in this user's personal store. No system trust is changed.
$certificate = New-SelfSignedCertificate -Type CodeSigningCert `
    -Subject 'CN=XFE InputSimulator UMDF Probe (Local Development)' `
    -FriendlyName 'XFE UMDF feasibility probe - local development only' `
    -CertStoreLocation Cert:\CurrentUser\My -KeyAlgorithm RSA -KeyLength 3072 `
    -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddDays(30) `
    -TextExtension @('2.5.29.19={critical}{text}ca=false')
$null = Export-Certificate -Cert $certificate -FilePath (Join-Path $destination 'LocalDevelopment.cer')
& $signTool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My (Join-Path $destination 'XfeInputUmProbe.dll')
if ($LASTEXITCODE) { throw 'Probe DLL signing failed.' }
$inf2cat = Get-ChildItem -LiteralPath $wdk -Filter inf2cat.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $inf2cat "/driver:$destination" /os:10_CO_X64 /uselocaltime
if ($LASTEXITCODE) { throw 'Probe catalog generation failed.' }
foreach ($file in @('XfeInputUmProbe.cat', 'XfeInputUmProbeSetup.exe', 'XfeInputUmProbeClient.exe')) {
    & $signTool sign /fd SHA256 /sha1 $certificate.Thumbprint /s My (Join-Path $destination $file)
    if ($LASTEXITCODE) { throw "Signing failed: $file" }
}
$hashes = [ordered]@{}
foreach ($file in $files + @('XfeInputUmProbe.cat', 'LocalDevelopment.cer')) {
    $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $destination $file) -Algorithm SHA256).Hash
}
$manifest = [ordered]@{
    schemaVersion = 1
    status = 'local-development-signed-not-installed-or-runtime-verified'
    hardwareId = 'Root\XFEInputUmProbe'
    certificateThumbprint = $certificate.Thumbprint
    certificateSha256 = $hashes['LocalDevelopment.cer']
    expires = $certificate.NotAfter.ToUniversalTime().ToString('o')
    files = $hashes
}
[IO.File]::WriteAllText((Join-Path $destination 'probe.json'), ($manifest | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Probe.ps1'), (Join-Path $PSScriptRoot 'Remove-Probe.ps1') -Destination $destination
Write-Host "Development-signed probe prepared: $destination"
Write-Host "Public certificate SHA256: $($manifest.certificateSha256)"
Write-Host 'This is NOT a Microsoft/WHQL signature. No certificate was added to system trust and no driver was installed.'
