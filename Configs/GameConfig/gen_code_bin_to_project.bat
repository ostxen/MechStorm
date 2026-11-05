@echo off
setlocal
pushd "%~dp0"
set "WORKSPACE=%~dp0..\.."
if not defined LUBAN_DLL set "LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll"

if not exist "%LUBAN_DLL%" (
    echo Luban.dll not found: "%LUBAN_DLL%"
    echo Install the agreed Luban release manually or set LUBAN_DLL before exporting.
    popd
    exit /b 1
)
where dotnet >nul 2>nul
if errorlevel 1 (
    echo dotnet was not found. Install the runtime required by the agreed Luban release.
    popd
    exit /b 1
)

dotnet "%LUBAN_DLL%" -t client -c cs-bin -d bin --conf "%~dp0luban.conf" ^
    -x code.lineEnding=crlf ^
    -x "outputCodeDir=%WORKSPACE%\MechStorm_Unity\Assets\GameScripts\HotFix\GameProto\GameConfig" ^
    -x "outputDataDir=%WORKSPACE%\MechStorm_Unity\Assets\AssetRaw\Configs\bytes"
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
