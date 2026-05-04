$ErrorActionPreference = "Stop"

$project = ".\src\SqlQueryGenerator.App\SqlQueryGenerator.App.csproj"
$out = ".\artifacts\publish\win-x64-framework-dependent"

Write-Host "Publishing SQL Query Generator as framework-dependent Windows executable..."
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -p:UseAppHost=true `
  -o $out

Write-Host ""
Write-Host "Executable generated: $out\SqlQueryGenerator.App.exe"
Write-Host "Requires Microsoft .NET 8 Desktop Runtime on the target machine."
