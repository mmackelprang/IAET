#!/usr/bin/env pwsh
param(
    [ValidateSet('clean', 'restore', 'build', 'test', 'pack', 'publish')]
    [string]$Target = 'build'
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot 'Iaet.slnx'

function Invoke-Clean {
    Write-Host "Cleaning..." -ForegroundColor Cyan
    dotnet clean $Solution -v q
    Get-ChildItem $RepoRoot -Recurse -Directory -Include bin, obj |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Invoke-Restore {
    Write-Host "Restoring..." -ForegroundColor Cyan
    dotnet restore $Solution
}

function Invoke-Build {
    Invoke-Restore
    Write-Host "Building..." -ForegroundColor Cyan
    dotnet build $Solution --no-restore -c Release
}

function Invoke-Test {
    Invoke-Build
    Write-Host "Testing..." -ForegroundColor Cyan
    dotnet test $Solution --no-build -c Release `
        --collect:"XPlat Code Coverage" `
        --results-directory (Join-Path $RepoRoot 'TestResults') `
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
}

function Invoke-Pack {
    Invoke-Build
    Write-Host "Packing NuGet packages..." -ForegroundColor Cyan
    dotnet pack $Solution --no-build -c Release -o (Join-Path $RepoRoot 'artifacts')
}

function Invoke-Publish {
    Invoke-Build
    Write-Host "Publishing..." -ForegroundColor Cyan
    dotnet publish src/Iaet.Cli/Iaet.Cli.csproj --no-build -c Release -o (Join-Path $RepoRoot 'artifacts/cli')
}

switch ($Target) {
    'clean'   { Invoke-Clean }
    'restore' { Invoke-Restore }
    'build'   { Invoke-Build }
    'test'    { Invoke-Test }
    'pack'    { Invoke-Pack }
    'publish' { Invoke-Publish }
}

Write-Host "Done: $Target" -ForegroundColor Green
