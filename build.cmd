@ECHO OFF

SETLOCAL

SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe
SET SOLUTION_PATH="%~dp0src\CodeFormatter.sln"
SET VSWHERELOCATION="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
 
IF NOT EXIST %VSWHERELOCATION% (
  goto :error
)

for /f "usebackq tokens=*" %%i in (`%VSWHERELOCATION% -latest -prerelease -products * -requires Microsoft.Component.MSBuild -property installationPath`) do (
  set BUILD_TOOLS_PATH="%%i\MSBuild\15.0\Bin\MSBuild.exe"
)

if exist %BUILD_TOOLS_PATH% (
  goto :restore   
)

:error
echo In order to run this tool you need either Visual Studio 2017 Update 2 or
echo Microsoft Build Tools 2017 Update 2 installed.
echo.
echo Visit this page to download either:
echo.
echo  https://go.microsoft.com/fwlink/?linkid=840931
echo.
exit /b 2

:restore
%BUILD_TOOLS_PATH% %SOLUTION_PATH% /t:restore /nologo /m /v:m /bl:restore.binlog

%BUILD_TOOLS_PATH% %SOLUTION_PATH% /p:OutDir="%~dp0bin" /nologo /m /v:m /bl:build.binlog %*
