[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\artifacts\umdf-probe'))
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$obj = Join-Path $output 'obj'
$null = New-Item -ItemType Directory -Path $obj -Force
dotnet restore (Join-Path $PSScriptRoot '..\BuildTools.csproj') --verbosity quiet
if ($LASTEXITCODE) { throw 'WDK restore failed.' }
$assets = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\obj\project.assets.json') -Raw | ConvertFrom-Json
function PackagePath([string]$id, [string]$version) {
    foreach ($folder in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $folder "$id\$version\c"
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "Package missing: $id $version"
}
$wdk = PackagePath 'microsoft.windows.wdk.x64' '10.0.26100.6584'
$sdk = PackagePath 'microsoft.windows.sdk.cpp' '10.0.26100.1'
$sdk64 = PackagePath 'microsoft.windows.sdk.cpp.x64' '10.0.26100.1'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$vs) { throw 'Visual Studio C++ build tools are required.' }
$vcVersion = (Get-Content -LiteralPath (Join-Path $vs 'VC\Auxiliary\Build\Microsoft.VCToolsVersion.default.txt')).Trim()
$vc = Join-Path $vs "VC\Tools\MSVC\$vcVersion"
$compiler = Join-Path $vc 'bin\Hostx64\x64\cl.exe'
$kit = '10.0.26100.0'
$env:PATH = "$sdk\bin\$kit\x64;$vc\bin\Hostx64\x64;$env:PATH"
$includes = @("/external:I$vc\include", "/external:I$sdk\Include\$kit\shared",
    "/external:I$sdk\Include\$kit\ucrt", "/external:I$sdk\Include\$kit\um")
$driverIncludes = $includes + @("/external:I$wdk\Include\$kit\km", "/external:I$wdk\Include\$kit\um",
    "/external:I$wdk\Include\wdf\umdf\2.15")
$libraries = @("/LIBPATH:$vc\lib\x64", "/LIBPATH:$sdk64\um\x64", "/LIBPATH:$sdk64\ucrt\x64")
& $compiler /nologo /TC /W4 /WX /external:W0 /MT /O2 /Zi /LD /utf-8 @driverIncludes `
    /DUMDF_VERSION_MAJOR=2 /DUMDF_VERSION_MINOR=15 /DUMDF_USING_NTSTATUS /D_UNICODE /DUNICODE `
    /D_WIN32_WINNT=0x0A00 "/Fo$obj\ProbeDriver.obj" "/Fd$obj\ProbeDriver.pdb" "/Fe$output\XfeInputUmProbe.dll" `
    (Join-Path $PSScriptRoot 'ProbeDriver.c') /link @libraries "/LIBPATH:$wdk\Lib\wdf\umdf\x64\2.15" `
    WdfDriverStubUm.lib ntdll.lib mincore.lib /DEBUG:FULL /OPT:REF /OPT:ICF /DYNAMICBASE /NXCOMPAT /GUARD:CF `
    "/PDB:$output\XfeInputUmProbe.pdb" '/PDBALTPATH:%_PDB%'
if ($LASTEXITCODE) { throw 'UMDF probe build failed.' }
& $compiler /nologo /TC /W4 /WX /external:W0 /MT /O2 /utf-8 @includes `
    "/Fo$obj\ProbeClient.obj" "/Fe$output\XfeInputUmProbeClient.exe" (Join-Path $PSScriptRoot 'ProbeClient.c') `
    /link @libraries hid.lib setupapi.lib /DYNAMICBASE /NXCOMPAT
if ($LASTEXITCODE) { throw 'Probe client build failed.' }
$setupPath = Join-Path $PSScriptRoot 'ProbeSetup.c'
& $compiler /nologo /TC /W4 /WX /external:W0 /MT /O2 /utf-8 @includes "/Fo$obj\ProbeSetup.obj" `
    "/Fe$output\XfeInputUmProbeSetup.exe" $setupPath /link @libraries setupapi.lib newdev.lib advapi32.lib `
    /DYNAMICBASE /NXCOMPAT /MANIFEST:EMBED "/MANIFESTUAC:level='requireAdministrator' uiAccess='false'"
if ($LASTEXITCODE) { throw 'Probe setup build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'XfeInputUmProbe.inf') -Destination $output -Force
& (Join-Path $wdk "tools\$kit\x64\infverif.exe") /w (Join-Path $output 'XfeInputUmProbe.inf')
if ($LASTEXITCODE) { throw 'Probe INF verification failed.' }
$inf2cat = Get-ChildItem -LiteralPath $wdk -Filter inf2cat.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $inf2cat "/driver:$output" /os:10_CO_X64 /uselocaltime
if ($LASTEXITCODE) { throw 'Probe catalog generation failed.' }
Write-Host "UMDF feasibility probe built, not installed or signed: $output"
