[CmdletBinding()]
param([switch]$ReleasePackage, [string]$LocalCertificateThumbprint, [switch]$LocalSigning)
$ErrorActionPreference = 'Stop'
if ($ReleasePackage -and ($LocalCertificateThumbprint -or $LocalSigning)) { throw 'Choose a release package or local certificate.' }
$timestampServer = 'http://timestamp.digicert.com'
if ($LocalSigning) {
    if ($LocalCertificateThumbprint) { throw 'Choose a saved signing identity or an explicit thumbprint.' }
    $profilePath = Join-Path $PSScriptRoot 'driver\signing.local.json'
    if (!(Test-Path -LiteralPath $profilePath)) { throw 'Run driver/New-SigningCertificate.ps1 once to create a reusable signing identity.' }
    $profile = Get-Content -LiteralPath $profilePath -Raw | ConvertFrom-Json
    $LocalCertificateThumbprint = $profile.thumbprint
    $timestampServer = $profile.timestampServer
}
& (Join-Path $PSScriptRoot 'driver\Build-Driver.ps1')
if ($ReleasePackage) {
    & (Join-Path $PSScriptRoot 'driver\Prepare-Payload.ps1') -PackageDirectory (Join-Path $PSScriptRoot 'driver\signed\win-x64')
} elseif ($LocalCertificateThumbprint) {
    $signedDirectory = Join-Path $PSScriptRoot ('artifacts\driver-local-' + [Guid]::NewGuid().ToString('N'))
    & (Join-Path $PSScriptRoot 'driver\Sign-LocalDriver.ps1') -CertificateThumbprint $LocalCertificateThumbprint -OutputDirectory $signedDirectory -TimestampServer $timestampServer
    & (Join-Path $PSScriptRoot 'driver\Prepare-Payload.ps1') -PackageDirectory $signedDirectory -AllowSelfSigned -RequireTimestamp
} else {
    & (Join-Path $PSScriptRoot 'driver\Prepare-Payload.ps1') -AllowUnsigned
}
dotnet build (Join-Path $PSScriptRoot 'XFEExtension.NetCore.InputSimulator.sln') -c Release -p:GeneratePackageOnBuild=false -warnaserror
if ($LASTEXITCODE) { throw 'Managed build failed.' }
