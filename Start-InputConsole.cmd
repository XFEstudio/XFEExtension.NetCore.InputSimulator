@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "XFEExtension.NetCore.InputSimulator.Console\XFEExtension.NetCore.InputSimulator.Console.csproj" --configuration Release --no-launch-profile -p:GeneratePackageOnBuild=false -- %*
if errorlevel 1 (
    echo.
    echo Input console failed. Check the messages above. A fresh source checkout needs: pwsh -File Build.ps1
    pause
)
