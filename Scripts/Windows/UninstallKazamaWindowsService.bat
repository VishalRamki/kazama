@echo off
SETLOCAL

SET ServiceName=Kazama

echo Stopping and removing service "%ServiceName%"...

REM Stop service if running
sc query "%ServiceName%" >nul 2>&1
IF %ERRORLEVEL% EQU 0 (
    net stop "%ServiceName%" >nul 2>&1
    sc delete "%ServiceName%" >nul 2>&1
    echo Service "%ServiceName%" stopped and deleted successfully.
) ELSE (
    echo Service "%ServiceName%" not found.
)

pause
ENDLOCAL
