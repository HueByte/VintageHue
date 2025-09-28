#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Simple docs generation and serving script for VintageHue mods.

.DESCRIPTION
    Generates documentation for all mods found in src/ folder and serves them using docfx.
    No templates or automatic file generation - user should create content manually.

.PARAMETER Serve
    Whether to serve the docs after building (default: true)

.PARAMETER Port
    Port to serve docs on (default: 8080)

.EXAMPLE
    ./generate-docs.ps1
    ./generate-docs.ps1 -Serve:$false
    ./generate-docs.ps1 -Port 3000
#>

param(
    [bool]$Serve = $true,
    [int]$Port = 8080
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Store original directory to restore later
$originalLocation = Get-Location

Write-Host "VintageHue Documentation Generator" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

# Change to docs directory
$docsPath = Join-Path $PSScriptRoot ".." "docs"
if (-not (Test-Path $docsPath)) {
    Write-Error "Docs directory not found at: $docsPath"
    exit 1
}

Set-Location $docsPath
Write-Host "Working in: $docsPath" -ForegroundColor Yellow

# Check if docfx.json exists
if (-not (Test-Path "docfx.json")) {
    Set-Location $originalLocation
    Write-Error "docfx.json not found. Please ensure documentation structure is set up."
    exit 1
}

try {
    # Build documentation
    Write-Host "`nBuilding documentation..." -ForegroundColor Green
    docfx build

    if ($LASTEXITCODE -ne 0) {
        Set-Location $originalLocation
        Write-Error "Documentation build failed"
        exit 1
    }

    Write-Host "Documentation built successfully!" -ForegroundColor Green

    # Serve if requested
    if ($Serve) {
        Write-Host "`nServing documentation on port $Port..." -ForegroundColor Green
        Write-Host "Open your browser to: http://localhost:$Port" -ForegroundColor Yellow
        Write-Host "Press Ctrl+C to stop serving" -ForegroundColor Yellow

        docfx serve _site --port $Port
    }
}
catch {
    Set-Location $originalLocation
    Write-Error "Error: $_"
    exit 1
}
finally {
    # Always restore original directory
    Set-Location $originalLocation
}