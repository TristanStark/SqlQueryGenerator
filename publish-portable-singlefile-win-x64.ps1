$ErrorActionPreference = "Stop"

$project = ".\src\SqlQueryGenerator.App\SqlQueryGenerator.App.csproj"
$out = ".\artifacts\portable\SqlQueryGenerator-win-x64-singlefile"

Write-Host "Publishing SQL Query Generator as portable self-contained single-file executable..."
Write-Host "Target: Windows x64, .NET Desktop Runtime embedded, no installation required."

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

# Single-file WPF is convenient, but can self-extract native/runtime assets on first launch.
# If a locked-down workstation blocks temp extraction, use publish-portable-folder-win-x64.ps1 instead.
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:UseAppHost=true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:PublishTrimmed=false `
  -o $out

Write-Host ""
Write-Host "Portable executable generated: $out\SqlQueryGenerator.App.exe"
Write-Host "Copy this exe to the USB key. If it fails on a locked-down PC, use the folder deployment script."
