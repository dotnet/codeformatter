@ECHO OFF

SETLOCAL

SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe
SET SOLUTION_PATH=%~dp0src\DeadRegionAnalysis.sln

IF EXIST %CACHED_NUGET% goto restore
echo Downloading latest version of NuGet.exe...
IF NOT EXIST %LocalAppData%\NuGet md %LocalAppData%\NuGet
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest 'https://www.nuget.org/nuget.exe' -OutFile '%CACHED_NUGET%'"

:restore
IF EXIST "%~dp0src\packages" goto build
%CACHED_NUGET% restore %SOLUTION_PATH%

:build

"%ProgramFiles(x86)%\MSBuild\14.0\bin\MSBuild.exe" %SOLUTION_PATH% /p:Configuration=Release /nologo /m /v:m /flp:verbosity=normal %*