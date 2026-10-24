# PlainShell Automated Build Script
# Publishes a single-file, self-contained Windows x64 .exe

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./dist"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Building PlainShell Single-File Executable " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# Stop any active PlainShell instances if currently running
Get-Process -Name PlainShell -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 300

# Ensure clean output directory
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

Write-Host "`n[1/3] Restoring and Publishing .NET 10 project..." -ForegroundColor Yellow

dotnet publish PlainShell.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}

Write-Host "`n[2/3] Copying Assets & Commands catalog..." -ForegroundColor Yellow
if (Test-Path "./Assets/commands.json") {
    Copy-Item "./Assets/commands.json" -Destination $OutputDir/commands.json -Force
}

$exePath = Join-Path $OutputDir "PlainShell.exe"
if (Test-Path $exePath) {
    $size = (Get-Item $exePath).Length / 1MB
    Write-Host "`n[3/3] Success! Binary generated:" -ForegroundColor Green
    Write-Host "  Path: $exePath" -ForegroundColor White
    Write-Host "  Size: $([math]::Round($size, 2)) MB" -ForegroundColor White
    Write-Host "`nYou can run it immediately with: & '$exePath'`n" -ForegroundColor Cyan
} else {
    Write-Error "Executable not found at expected path: $exePath"
}
