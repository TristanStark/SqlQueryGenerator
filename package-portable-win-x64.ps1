$ErrorActionPreference = "Stop"

$portableFolder = ".\artifacts\portable\SqlQueryGenerator-win-x64-folder"
$zipPath = ".\artifacts\portable\SqlQueryGenerator-win-x64-portable.zip"

if (-not (Test-Path $portableFolder)) {
    Write-Host "Portable folder not found. Building it first..."
    .\publish-portable-folder-win-x64.ps1
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$portableFolder\*" -DestinationPath $zipPath -Force

Write-Host ""
Write-Host "Portable ZIP generated: $zipPath"
Write-Host "Unzip it on the USB key and run SqlQueryGenerator.App.exe from the extracted folder."
