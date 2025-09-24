#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Generates documentation for mods using DocFX.

.DESCRIPTION
    Creates and updates documentation for each mod in the repository using DocFX.
    Sets up proper folder structure, generates API documentation, and builds the site.

.PARAMETER ModName
    Name of the specific mod to generate docs for. If not specified, generates for all mods.

.PARAMETER BuildOnly
    Only build existing documentation without regenerating metadata

.PARAMETER Serve
    Start a local server after building documentation

.PARAMETER Port
    Port for local documentation server (default: 8080)

.PARAMETER Clean
    Clean existing documentation before regenerating

.PARAMETER Help
    Show help message

.EXAMPLE
    .\scripts\generate-docs.ps1
    Generate documentation for all mods

.EXAMPLE
    .\scripts\generate-docs.ps1 -ModName "HueHordes"
    Generate documentation only for HueHordes mod

.EXAMPLE
    .\scripts\generate-docs.ps1 -ModName "HueHordes" -Serve
    Generate and serve HueHordes documentation locally

.EXAMPLE
    .\scripts\generate-docs.ps1 -Clean
    Clean and regenerate all documentation
#>

param(
    [string]$ModName = "",
    [switch]$BuildOnly = $false,
    [switch]$Serve = $false,
    [int]$Port = 8080,
    [switch]$Clean = $false,
    [switch]$Help = $false
)

if ($Help) {
    Write-Host @"
HueHordes Documentation Generator (DocFX)

Usage: .\scripts\generate-docs.ps1 [options]

Options:
    -ModName <name>         Generate docs for specific mod (e.g., "HueHordes")
    -BuildOnly              Only build existing documentation
    -Serve                  Start local server after building
    -Port <port>            Port for local server (default: 8080)
    -Clean                  Clean existing docs before regenerating
    -Help                   Show this help message

Examples:
    .\scripts\generate-docs.ps1                           # Generate all mod docs
    .\scripts\generate-docs.ps1 -ModName "HueHordes"      # Generate specific mod
    .\scripts\generate-docs.ps1 -Serve                    # Generate and serve locally
    .\scripts\generate-docs.ps1 -Clean                    # Clean regenerate all
"@
    exit 0
}

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )

    $colors = @{
        "Red"     = "91"
        "Green"   = "92"
        "Yellow"  = "93"
        "Blue"    = "94"
        "Magenta" = "95"
        "Cyan"    = "96"
        "White"   = "97"
    }

    if ($colors.ContainsKey($Color)) {
        Write-Host "`e[$($colors[$Color])m$Message`e[0m"
    }
    else {
        Write-Host $Message
    }
}

# Header
Write-ColorOutput "📚 HueHordes Documentation Generator" "Cyan"
Write-ColorOutput "====================================" "Cyan"
Write-ColorOutput ""

# Get repository root
$repoRoot = Get-Location
$docsRoot = Join-Path $repoRoot "docs"
$srcRoot = Join-Path $repoRoot "src"

# Check if DocFX is installed
function Test-DocFXInstalled {
    try {
        $null = docfx --version 2>$null
        return $true
    }
    catch {
        return $false
    }
}

# Install DocFX if not present
if (-not (Test-DocFXInstalled)) {
    Write-ColorOutput "⚠️  DocFX not found, installing..." "Yellow"
    try {
        dotnet tool install -g docfx
        Write-ColorOutput "✅ DocFX installed successfully" "Green"
    }
    catch {
        Write-ColorOutput "❌ Failed to install DocFX" "Red"
        Write-ColorOutput "Error: $_" "Red"
        exit 1
    }
}

$docfxVersion = docfx --version
Write-ColorOutput "✅ DocFX version: $docfxVersion" "Green"
Write-ColorOutput ""

# Discover available mods
function Get-AvailableMods {
    $mods = @()

    if (Test-Path $srcRoot) {
        $modFolders = Get-ChildItem -Path $srcRoot -Directory
        foreach ($folder in $modFolders) {
            # Check if it has a .sln or .csproj file (indicates it's a mod project)
            $hasSolution = Get-ChildItem -Path $folder.FullName -Filter "*.sln" -ErrorAction SilentlyContinue
            $hasProject = Get-ChildItem -Path $folder.FullName -Filter "*.csproj" -Recurse -ErrorAction SilentlyContinue

            if ($hasSolution -or $hasProject) {
                $mods += @{
                    Name = $folder.Name
                    Path = $folder.FullName
                    HasSolution = $hasSolution -ne $null
                }
            }
        }
    }

    return $mods
}

# Create DocFX configuration for a mod
function New-DocFXConfig {
    param(
        [string]$ModName,
        [string]$ModPath,
        [string]$OutputPath
    )

    $configContent = @"
{
  "metadata": [
    {
      "src": [
        {
          "files": [
            "$ModName/**/*.csproj"
          ],
          "src": "../../../src/$ModName"
        }
      ],
      "dest": "api",
      "includePrivateMembers": false,
      "disableGitFeatures": false,
      "disableDefaultFilter": false,
      "noRestore": false,
      "namespaceLayout": "flattened",
      "memberLayout": "samePage",
      "allowCompilationErrors": true
    }
  ],
  "build": {
    "content": [
      {
        "files": [
          "api/**.yml",
          "api/index.md"
        ]
      },
      {
        "files": [
          "articles/**.md",
          "articles/**/toc.yml",
          "toc.yml",
          "*.md"
        ]
      }
    ],
    "resource": [
      {
        "files": [
          "images/**"
        ]
      }
    ],
    "output": "_site",
    "globalMetadataFiles": [],
    "fileMetadataFiles": [],
    "template": [
      "default"
    ],
    "postProcessors": [],
    "markdownEngineName": "markdig",
    "noLangKeyword": false,
    "keepFileLink": false,
    "cleanupCacheHistory": false,
    "disableGitFeatures": false,
    "globalMetadata": {
      "_appTitle": "$ModName Documentation",
      "_appName": "$ModName",
      "_appFaviconPath": "images/favicon.ico",
      "_enableSearch": true,
      "_enableNewTab": true
    }
  }
}
"@

    $configPath = Join-Path $OutputPath "docfx.json"
    $configContent | Out-File -FilePath $configPath -Encoding UTF8
    return $configPath
}

# Create documentation structure for a mod
function Initialize-ModDocumentation {
    param(
        [string]$ModName,
        [string]$ModPath
    )

    $modDocsPath = Join-Path $docsRoot $ModName

    # Create directory structure
    $directories = @(
        $modDocsPath,
        (Join-Path $modDocsPath "articles"),
        (Join-Path $modDocsPath "articles" "getting-started"),
        (Join-Path $modDocsPath "articles" "api-reference"),
        (Join-Path $modDocsPath "articles" "changelog"),
        (Join-Path $modDocsPath "release-notes"),
        (Join-Path $modDocsPath "images"),
        (Join-Path $modDocsPath "api")
    )

    foreach ($dir in $directories) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            Write-ColorOutput "📁 Created directory: $dir" "Green"
        }
    }

    # Create main index.md
    $indexPath = Join-Path $modDocsPath "index.md"
    if (-not (Test-Path $indexPath)) {
        $indexContent = @"
# $ModName Documentation

Welcome to the $ModName documentation!

## Quick Links

- [Getting Started](articles/getting-started/index.md)
- [API Reference](api/index.md)
- [Changelog](articles/changelog/index.md)

## About $ModName

$ModName is a mod for the HueHordes repository, providing advanced functionality and features.

## Navigation

Use the navigation on the left to explore the documentation sections:

- **Getting Started**: Installation, setup, and basic usage
- **Articles**: Detailed guides and tutorials
- **API Reference**: Complete API documentation
- **Changelog**: Version history and changes

---

*Documentation generated on $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")*
"@
        $indexContent | Out-File -FilePath $indexPath -Encoding UTF8
    }

    # Create table of contents
    $tocPath = Join-Path $modDocsPath "toc.yml"
    if (-not (Test-Path $tocPath)) {
        $tocContent = @"
- name: Articles
  href: articles/
- name: API Reference
  href: api/
  homepage: api/index.md
"@
        $tocContent | Out-File -FilePath $tocPath -Encoding UTF8
    }

    # Create articles index
    $articlesIndexPath = Join-Path $modDocsPath "articles" "index.md"
    if (-not (Test-Path $articlesIndexPath)) {
        $articlesContent = @"
# $ModName Articles

## Getting Started
- [Installation](getting-started/installation.md)
- [Configuration](getting-started/configuration.md)

## Guides
- [Basic Usage](guides/basic-usage.md)
- [Advanced Features](guides/advanced-features.md)

## Reference
- [API Reference](api-reference/index.md)
- [Changelog](changelog/index.md)
"@
        $articlesContent | Out-File -FilePath $articlesIndexPath -Encoding UTF8
    }

    # Create articles TOC
    $articlesTocPath = Join-Path $modDocsPath "articles" "toc.yml"
    if (-not (Test-Path $articlesTocPath)) {
        $articlesTocContent = @"
- name: Getting Started
  href: getting-started/
- name: Changelog
  href: changelog/
"@
        $articlesTocContent | Out-File -FilePath $articlesTocPath -Encoding UTF8
    }

    # Create getting started index
    $gettingStartedPath = Join-Path $modDocsPath "articles" "getting-started" "index.md"
    if (-not (Test-Path $gettingStartedPath)) {
        $gettingStartedContent = @"
# Getting Started with $ModName

## Installation

Instructions for installing $ModName...

## Configuration

How to configure $ModName...

## First Steps

Your first steps with $ModName...
"@
        $gettingStartedContent | Out-File -FilePath $gettingStartedPath -Encoding UTF8
    }

    # Create changelog index
    $changelogIndexPath = Join-Path $modDocsPath "articles" "changelog" "index.md"
    if (-not (Test-Path $changelogIndexPath)) {
        $changelogContent = @"
# $ModName Changelog

## Version History

This section contains the complete changelog for $ModName.

## Latest Releases

- [Latest Version](../../release-notes/)

---

*For detailed release notes, see the [Release Notes](../../release-notes/) section.*
"@
        $changelogContent | Out-File -FilePath $changelogIndexPath -Encoding UTF8
    }

    return $modDocsPath
}

# Process specific mod or all mods
$modsToProcess = @()

if ([string]::IsNullOrWhiteSpace($ModName)) {
    Write-ColorOutput "📁 Discovering all mods..." "Cyan"
    $availableMods = Get-AvailableMods
    $modsToProcess = $availableMods
    Write-ColorOutput "Found $($modsToProcess.Count) mod(s) to process" "Green"
}
else {
    Write-ColorOutput "📁 Processing specific mod: $ModName" "Cyan"
    $modPath = Join-Path $srcRoot $ModName
    if (-not (Test-Path $modPath)) {
        Write-ColorOutput "❌ Error: Mod '$ModName' not found in src/$ModName" "Red"
        exit 1
    }
    $modsToProcess = @(@{
        Name = $ModName
        Path = $modPath
        HasSolution = (Get-ChildItem -Path $modPath -Filter "*.sln" -ErrorAction SilentlyContinue) -ne $null
    })
}

if ($modsToProcess.Count -eq 0) {
    Write-ColorOutput "❌ No mods found to process" "Red"
    exit 1
}

# Process each mod
$modIndex = 0
foreach ($mod in $modsToProcess) {
    $currentModName = $mod.Name
    $currentModPath = $mod.Path
    $modIndex++

    Write-ColorOutput "`n📚 Processing documentation for $currentModName..." "Cyan"

    # Clean if requested
    if ($Clean) {
        $modDocsPath = Join-Path $docsRoot $currentModName
        if (Test-Path $modDocsPath) {
            Write-ColorOutput "🧹 Cleaning existing documentation..." "Yellow"
            Remove-Item -Path $modDocsPath -Recurse -Force
        }
    }

    # Initialize documentation structure
    $modDocsPath = Initialize-ModDocumentation -ModName $currentModName -ModPath $currentModPath

    # Create DocFX configuration
    $docfxConfigPath = New-DocFXConfig -ModName $currentModName -ModPath $currentModPath -OutputPath $modDocsPath

    # Change to mod docs directory
    Push-Location $modDocsPath

    try {
        if (-not $BuildOnly) {
            # Generate metadata (API documentation)
            Write-ColorOutput "🔍 Generating API metadata for $currentModName..." "Cyan"
            docfx metadata docfx.json --warningsAsErrors false --disableGitFeatures false
            if ($LASTEXITCODE -ne 0) {
                Write-ColorOutput "⚠️ Metadata generation completed with warnings for $currentModName" "Yellow"
            }
            else {
                Write-ColorOutput "✅ Metadata generated successfully for $currentModName" "Green"
            }
        }

        # Build documentation
        Write-ColorOutput "🔨 Building documentation site for $currentModName..." "Cyan"
        docfx build docfx.json --warningsAsErrors false
        if ($LASTEXITCODE -ne 0) {
            Write-ColorOutput "⚠️ Documentation build completed with warnings for $currentModName" "Yellow"
        }
        else {
            Write-ColorOutput "✅ Documentation built successfully for $currentModName" "Green"
        }

        # Start server if requested
        $shouldServe = $Serve -and (
            # Serve if specific mod requested and this is that mod
            ($currentModName -eq $ModName) -or
            # OR serve if no specific mod requested and this is the first mod
            ([string]::IsNullOrWhiteSpace($ModName) -and $modIndex -eq 1)
        )

        if ($shouldServe) {
            Write-ColorOutput "🌐 Starting documentation server for $currentModName on port $Port..." "Cyan"
            Write-ColorOutput "📖 Documentation will be available at: http://localhost:$Port" "Green"
            Write-ColorOutput "Press Ctrl+C to stop the server" "Yellow"
            docfx serve _site --port $Port
        }
    }
    catch {
        Write-ColorOutput "❌ Error processing documentation for $currentModName`: $_" "Red"
    }
    finally {
        Pop-Location
    }
}

Write-ColorOutput "`n🎉 Documentation generation completed!" "Green"
Write-ColorOutput "📁 Documentation location: $docsRoot" "Green"

if (-not $Serve) {
    Write-ColorOutput "💡 Use -Serve parameter to start a local documentation server" "Cyan"
    Write-ColorOutput "💡 Use -ModName parameter to generate docs for a specific mod only" "Cyan"
}

# Summary
Write-ColorOutput "`n📊 Summary:" "Cyan"
foreach ($mod in $modsToProcess) {
    $modDocsPath = Join-Path $docsRoot $mod.Name
    $sitePath = Join-Path $modDocsPath "_site"
    if (Test-Path $sitePath) {
        Write-ColorOutput "✅ $($mod.Name): Documentation ready at docs/$($mod.Name)/_site" "Green"
    }
    else {
        Write-ColorOutput "❌ $($mod.Name): Documentation generation failed" "Red"
    }
}