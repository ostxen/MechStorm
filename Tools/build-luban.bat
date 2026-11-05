@echo off
setlocal
set "LUBAN_REPO=https://github.com/focus-creative-games/luban.git"
set "LUBAN_DIR=%~dp0luban-source"
set "LUBAN_OUT=%~dp0Luban"

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git was not found.
    goto failed
)
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] dotnet was not found. Install the .NET SDK required by Luban.
    goto failed
)

if exist "%LUBAN_DIR%\.git" goto update
echo [INFO] Cloning Luban from %LUBAN_REPO% ...
git clone "%LUBAN_REPO%" "%LUBAN_DIR%"
if errorlevel 1 goto failed
goto build

:update
echo [INFO] Updating Luban source ...
git -C "%LUBAN_DIR%" fetch origin
if errorlevel 1 goto failed
git -C "%LUBAN_DIR%" pull --ff-only
if errorlevel 1 goto failed

:build
echo [INFO] Luban source revision:
git -C "%LUBAN_DIR%" rev-parse HEAD
if errorlevel 1 goto failed
echo [INFO] Building Luban into "%LUBAN_OUT%" ...
dotnet build "%LUBAN_DIR%\src\Luban\Luban.csproj" -c Release -o "%LUBAN_OUT%"
if errorlevel 1 goto failed
git -C "%LUBAN_DIR%" rev-parse HEAD > "%LUBAN_OUT%\source-revision.txt"
if errorlevel 1 goto failed
echo [INFO] Luban build succeeded.
if not defined AI_MODE pause
exit /b 0

:failed
echo [ERROR] Luban acquisition or build failed. Do not export with this output.
if not defined AI_MODE pause
exit /b 1
