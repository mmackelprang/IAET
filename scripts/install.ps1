# Install or update the iaet global dotnet tool from local source
# Usage: pwsh scripts/install.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $root) { $root = (Get-Location).Path }

Write-Host "Building IAET CLI..." -ForegroundColor Cyan
dotnet pack "$root/src/Iaet.Cli/Iaet.Cli.csproj" -c Release -o "$root/artifacts/" -v quiet
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }

# Try update first, fall back to install
Write-Host "Installing iaet tool..." -ForegroundColor Cyan
dotnet tool update -g Iaet.Cli --add-source "$root/artifacts/" --version 0.1.0 2>$null
if ($LASTEXITCODE -ne 0) {
    dotnet tool install -g Iaet.Cli --add-source "$root/artifacts/" --version 0.1.0
}

Write-Host ""
Write-Host "Done! Run 'iaet --help' to verify." -ForegroundColor Green
iaet --version
