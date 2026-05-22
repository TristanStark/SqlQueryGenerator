$ErrorActionPreference = "Stop"

$project = ".\src\SqlQueryGenerator.App\SqlQueryGenerator.App.csproj"
$out = ".\artifacts\portable\SqlQueryGenerator-win-x64-folder"

Write-Host "Publishing SQL Query Generator as portable self-contained folder..."
Write-Host "Target: Windows x64, .NET Desktop Runtime embedded, no installation required."

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

# Folder deployment is the most robust USB distribution mode for WPF:
# it does not require .NET on the target PC and avoids single-file extraction surprises.
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:UseAppHost=true `
  -p:PublishSingleFile=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -p:PublishTrimmed=false `
  -o $out

Write-Host ""
Write-Host "Portable folder generated: $out"
Write-Host "Run: $out\SqlQueryGenerator.App.exe"
Write-Host "Copy the whole folder to the USB key, not only the exe."
