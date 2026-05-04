$ErrorActionPreference = "Stop"

$exe = ".\src\SqlQueryGenerator.App\bin\Release\net8.0-windows\SqlQueryGenerator.App.exe"
$dll = ".\src\SqlQueryGenerator.App\bin\Release\net8.0-windows\SqlQueryGenerator.App.dll"

if (Test-Path $exe) {
    & $exe
    exit $LASTEXITCODE
}

if (Test-Path $dll) {
    dotnet $dll
    exit $LASTEXITCODE
}

Write-Host "Application not built yet. Running build first..."
.\build.ps1

if (Test-Path $exe) {
    & $exe
} elseif (Test-Path $dll) {
    dotnet $dll
} else {
    throw "Neither executable nor DLL was found after build. Try .\publish-win-x64.ps1."
}
