@echo off
rem Compatibility entry for the existing Unity menu; no synchronous lazy-load template is used.
call "%~dp0gen_code_bin_to_project.bat"
exit /b %ERRORLEVEL%
