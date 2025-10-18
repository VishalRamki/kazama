@echo off
SETLOCAL

REM -------------------------------
REM Configuration
REM -------------------------------
SET ServiceName=Kazama
SET DisplayName=Kazama Game Save Sync
SET Description=Watches all your game folders and automatically backups it up everything there is a change.
SET ExePath=%~dp0..\..\Kazama.exe
REM Expand to full absolute path
FOR %%I IN ("%ExePath%") DO SET ExePath=%%~fI
SET StartType=auto
REM -------------------------------

echo Installing service "%ServiceName%"...

REM Check if service already exists
sc query "%ServiceName%" >nul 2>&1
IF %ERRORLEVEL% EQU 0 (
    echo Service already exists. Stopping and deleting it first...
    sc stop "%ServiceName%" >nul 2>&1
    sc delete "%ServiceName%" >nul 2>&1
    timeout /t 2 /nobreak >nul
)

REM Create the service
sc create "%ServiceName%" binPath= "%ExePath%" start= %StartType% DisplayName= "%DisplayName%" obj= "LocalSystem"

REM Set the description
sc description "%ServiceName%" "%Description%"

REM Configure the service to restart on crash
sc failure "%ServiceName%" reset= 0 actions= restart/5000

REM Start the service immediately
net start "%ServiceName%"

echo Service "%ServiceName%" installed and started successfully.
echo It is configured to automatically restart on crash.
pause
ENDLOCAL
