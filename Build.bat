SETLOCAL
SET Version=6.0.0
SET Prerelease=auto

@SET Config=%1%
@IF [%1] == [] SET Config=Debug

REM Updating the build version.
PowerShell -ExecutionPolicy ByPass .\tools\Build\ChangeVersion.ps1 %Version% %Prerelease% || GOTO Error0

REM Disabled RhetosDeploy, to allow building the TestAppDirect* projects without an active database connection.
dotnet build "Rhetos.sln" --configuration %Config% /p:RhetosDeploy=false || GOTO Error1
PowerShell -ExecutionPolicy ByPass .\CreateInstallationPackage.ps1 || GOTO Error1

REM Restoring the build version back to "dev" (internal development build), to avoid spamming git history with timestamped prerelease versions.
PowerShell -ExecutionPolicy ByPass .\tools\Build\ChangeVersion.ps1 %Version% dev || GOTO Error0

@REM ================================================

@ECHO.
@ECHO %~nx0 SUCCESSFULLY COMPLETED.
@EXIT /B 0

:Error1
@PowerShell -ExecutionPolicy ByPass .\tools\Build\ChangeVersion.ps1 %Version% dev >nul
:Error0
@ECHO.
@ECHO %~nx0 FAILED.
@EXIT /B 1
