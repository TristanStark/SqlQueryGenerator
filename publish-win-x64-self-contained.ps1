$ErrorActionPreference = "Stop"

$project = ".\src\SqlQueryGenerator.App\SqlQueryGenerator.App.csproj"
$out = ".\artifacts\publish\win-x64-self-contained"

Write-Host "Publishing SQL Query Generator as self-contained single-file Windows executable..."
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:UseAppHost=true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o $out

Write-Host ""
Write-Host "Executable generated: $out\SqlQueryGenerator.App.exe"
Write-Host "This version includes the .NET runtime and is much larger, but can run without installing .NET."
