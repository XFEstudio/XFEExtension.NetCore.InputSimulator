[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot ('artifacts\distribution-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$SignedPackageDirectory
)
$ErrorActionPreference = 'Stop'
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count) { throw 'Choose an empty distribution directory.' }
if ($SignedPackageDirectory) {
    & (Join-Path $PSScriptRoot 'driver\Prepare-Payload.ps1') -PackageDirectory $SignedPackageDirectory -AllowSelfSigned -RequireTimestamp
    dotnet build (Join-Path $PSScriptRoot 'XFEExtension.NetCore.InputSimulator.sln') -c Release -p:GeneratePackageOnBuild=false -warnaserror
    if ($LASTEXITCODE) { throw 'Managed build failed.' }
} else {
    & (Join-Path $PSScriptRoot 'Build.ps1') -LocalSigning
}
$profile = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'driver\signing.local.json') -Raw | ConvertFrom-Json
$publicCertificate = Join-Path $PSScriptRoot 'driver\payload\win-x64\publisher.cer'
if ((Get-FileHash -LiteralPath $publicCertificate -Algorithm SHA256).Hash -ne $profile.certificateSha256) { throw 'The driver publisher does not match the saved signing identity.' }
dotnet run --project (Join-Path $PSScriptRoot 'XFEExtension.NetCore.InputSimulator.Tests') -c Release --no-build -- --artifacts (Join-Path $destination 'tests')
if ($LASTEXITCODE) { throw 'Tests failed.' }
dotnet pack (Join-Path $PSScriptRoot 'XFEExtension.NetCore.InputSimulator') -c Release --no-build -p:GeneratePackageOnBuild=false -o $destination
if ($LASTEXITCODE) { throw 'NuGet packaging failed.' }
dotnet publish (Join-Path $PSScriptRoot 'XFEExtension.NetCore.InputSimulator.Console') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:GeneratePackageOnBuild=false -o (Join-Path $destination 'console')
if ($LASTEXITCODE) { throw 'Console publishing failed.' }
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'driver\obj\project.assets.json') -Raw | ConvertFrom-Json
$signTool = $null
foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
    $candidate = Join-Path $folder 'microsoft.windows.sdk.cpp\10.0.26100.1\c\bin\10.0.26100.0\x64\signtool.exe'
    if (Test-Path -LiteralPath $candidate) { $signTool = $candidate; break }
}
if (!$signTool) { throw 'Signing tool missing after native build.' }
$console = Join-Path $destination 'console\InputSimulator.Console.exe'
& $signTool sign /fd SHA256 /sha1 $profile.thumbprint /s My /tr $profile.timestampServer /td SHA256 $console
if ($LASTEXITCODE) { throw 'Console signing or timestamping failed.' }
$signature = Get-AuthenticodeSignature -LiteralPath $console
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $profile.thumbprint -or !$signature.TimeStamperCertificate) { throw 'Console signature verification failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'driver\payload\win-x64\publisher.cer') -Destination $destination
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'driver\SIGNING.md') -Destination $destination
Write-Host "Signed local-trust distribution ready: $destination"
Write-Host 'No key exported or system trust changed. Recipients must explicitly trust the publisher on first installation.'
