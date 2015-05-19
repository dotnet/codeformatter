CALL .\build /p:Configuration=Release

SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe
%CACHED_NUGET% pack src\nuget\Octokit.CodeFormatter.nuspec -NoPackageAnalysis