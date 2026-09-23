@echo off
setlocal
cd /d "%~dp0"
dotnet run --project "XFEExtension.NetCore.InputSimulator.Console\XFEExtension.NetCore.InputSimulator.Console.csproj" --configuration Release --no-launch-profile -p:GeneratePackageOnBuild=false -- %*
if errorlevel 1 (
    echo.
    echo Input console failed. Check the messages above. Building requires the .NET 10 SDK.
    echo Detect driver: Start-InputConsole.cmd --driver-check
    echo Explicitly install or update driver: Start-InputConsole.cmd --driver-install
    pause
)
