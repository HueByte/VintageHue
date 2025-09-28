#!/usr/bin/env pwsh

# Set environment variables to indicate automation context
$env:NO_COLOR = "1"
$env:TERM = "dumb"
$env:AUTOMATION = "1"
$Host.UI.RawUI.WindowTitle = "Release Script"

<#
.SYNOPSIS
    Enhanced release creator for HueHordes mods with full automation.

.DESCRIPTION
    Creates releases with version management, automated changelog generation,
    markdown linting, and proper git workflow integration.

.PARAMETER ModName
    Name of the mod to create a release for (e.g., "HueHordes")

.PARAMETER Version
    Version to release (e.g., "1.1.0"). If not provided, will increment patch version.

.PARAMETER VersionType
    Type of version increment: "major", "minor", "patch" (default: "patch")

.PARAMETER DryRun
    Show what would be done without executing

.PARAMETER SkipLint
    Skip markdown linting step

.PARAMETER SkipBuild
    Skip mod build step (useful if already built)


.PARAMETER BuildPath
    Custom path to build artifacts (default: auto-detect from build output)

.PARAMETER Help
    Show help message

.EXAMPLE
    .\scripts\create-release-enhanced.ps1 -ModName "HueHordes"
    Create patch release with auto-incremented version

.EXAMPLE
    .\scripts\create-release-enhanced.ps1 -ModName "HueHordes" -Version "1.1.0"
    Create release with specific version

.EXAMPLE
    .\scripts\create-release-enhanced.ps1 -ModName "HueHordes" -VersionType "minor"
    Create minor version release

.EXAMPLE
    .\scripts\create-release-enhanced.ps1 -ModName "HueHordes" -SkipBuild
    Create release without building (assumes already built)

#>

param(
    [string]$ModName = "",
    [string]$Version = "",
    [ValidateSet("major", "minor", "patch")]
    [string]$VersionType = "patch",
    [switch]$DryRun = $false,
    [switch]$SkipLint = $false,
    [switch]$SkipBuild = $false,
    [string]$BuildPath = "",
    [switch]$Help = $false
)

if ($Help) {
    Get-Help $MyInvocation.MyCommand.Path -Detailed
    exit 0
}

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )

    # Use PowerShell's built-in color support instead of ANSI codes for better compatibility
    if ($env:NO_COLOR -or $env:CI) {
        Write-Host $Message
    } else {
        try {
            Write-Host $Message -ForegroundColor $Color
        } catch {
            Write-Host $Message
        }
    }
}

# Header
Write-ColorOutput "🚀 HueHordes Enhanced Release Creator" "Cyan"
Write-ColorOutput "======================================" "Cyan"
Write-ColorOutput ""

# Check if we're in a git repository
if (-not (Test-Path ".git")) {
    Write-ColorOutput "❌ Error: Not in a git repository" "Red"
    exit 1
}

# Get repository root
$repoRoot = Get-Location

# Discover available mods
function Get-AvailableMods {
    $mods = @()
    $srcPath = Join-Path $repoRoot "src"

    if (Test-Path $srcPath) {
        $modFolders = Get-ChildItem -Path $srcPath -Directory
        foreach ($folder in $modFolders) {
            # Look for modinfo.json in the direct mod subfolder first (e.g., src/HueHordes/HueHordes/modinfo.json)
            $modSubFolder = Get-ChildItem -Path $folder.FullName -Directory | Where-Object { $_.Name -eq $folder.Name } | Select-Object -First 1
            $modInfoPath = $null

            if ($modSubFolder) {
                $directModInfo = Join-Path $modSubFolder.FullName "modinfo.json"
                if (Test-Path $directModInfo) {
                    $modInfoPath = Get-Item $directModInfo
                }
            }

            # Fallback: look for any modinfo.json in the folder (excluding bin/build directories)
            if (-not $modInfoPath) {
                $modInfoPath = Get-ChildItem -Path $folder.FullName -Filter "modinfo.json" -Recurse -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -notmatch "\\bin\\|\\build\\|\\obj\\|\\publish\\" } |
                    Select-Object -First 1
            }

            if ($modInfoPath) {
                $mods += @{
                    Name = $folder.Name
                    Path = $folder.FullName
                    ModInfoPath = $modInfoPath.FullName
                }
            }
        }
    }

    return $mods
}

# Interactive mod selection
if ([string]::IsNullOrWhiteSpace($ModName)) {
    Write-ColorOutput "📁 Discovering mods in repository..." "Cyan"
    $availableMods = Get-AvailableMods

    if ($availableMods.Count -eq 0) {
        Write-ColorOutput "❌ No mods found with modinfo.json files" "Red"
        exit 1
    }

    Write-ColorOutput "Available mods:" "Green"
    for ($i = 0; $i -lt $availableMods.Count; $i++) {
        $mod = $availableMods[$i]
        Write-ColorOutput "  $($i + 1). 📦 $($mod.Name)" "White"
    }
    Write-ColorOutput ""

    do {
        $selection = Read-Host "Select mod number (1-$($availableMods.Count))"
        $modIndex = [int]$selection - 1
    } while ($modIndex -lt 0 -or $modIndex -ge $availableMods.Count)

    $selectedMod = $availableMods[$modIndex]
    $ModName = $selectedMod.Name
    $modPath = $selectedMod.Path
    $modInfoPath = $selectedMod.ModInfoPath

    Write-ColorOutput "✅ Selected mod: $ModName" "Green"
}
else {
    # Validate provided mod name
    $modPath = Join-Path $repoRoot "src" $ModName
    if (-not (Test-Path $modPath)) {
        Write-ColorOutput "❌ Error: Mod '$ModName' not found in src/$ModName" "Red"
        exit 1
    }

    # Use same logic as Get-AvailableMods to find modinfo.json
    $modSubFolder = Get-ChildItem -Path $modPath -Directory | Where-Object { $_.Name -eq $ModName } | Select-Object -First 1
    $modInfoPath = $null

    if ($modSubFolder) {
        $directModInfo = Join-Path $modSubFolder.FullName "modinfo.json"
        if (Test-Path $directModInfo) {
            $modInfoPath = $directModInfo
        }
    }

    # Fallback: look for any modinfo.json in the folder (excluding bin/build directories)
    if (-not $modInfoPath) {
        $modInfoFile = Get-ChildItem -Path $modPath -Filter "modinfo.json" -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch "\\bin\\|\\build\\|\\obj\\|\\publish\\" } |
            Select-Object -First 1

        if ($modInfoFile) {
            $modInfoPath = $modInfoFile.FullName
        }
    }

    if (-not $modInfoPath) {
        Write-ColorOutput "❌ Error: modinfo.json not found for mod '$ModName'" "Red"
        exit 1
    }

    Write-ColorOutput "✅ Using mod: $ModName" "Green"
}

Write-ColorOutput ""

# Function to parse and update version
function Get-CurrentVersion {
    param([string]$ModInfoPath)

    try {
        if (-not (Test-Path $ModInfoPath)) {
            throw "ModInfo file not found at: $ModInfoPath"
        }

        $modInfo = Get-Content $ModInfoPath | ConvertFrom-Json
        if (-not $modInfo.version) {
            throw "Version property not found in modinfo.json"
        }
        return $modInfo.version
    }
    catch {
        Write-ColorOutput "❌ Error reading version from modinfo.json: $_" "Red"
        exit 1
    }
}

function Update-ModVersion {
    param(
        [string]$ModInfoPath,
        [string]$NewVersion
    )

    $modInfo = Get-Content $ModInfoPath | ConvertFrom-Json
    $oldVersion = $modInfo.version
    $modInfo.version = $NewVersion

    $modInfo | ConvertTo-Json -Depth 10 | Out-File $ModInfoPath -Encoding UTF8

    return $oldVersion
}

function Increment-Version {
    param(
        [string]$CurrentVersion,
        [string]$VersionType
    )

    $versionParts = $CurrentVersion -split '\.'
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]

    switch ($VersionType) {
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "patch" {
            $patch++
        }
    }

    return "$major.$minor.$patch"
}

function Get-ModBuildArtifacts {
    param(
        [string]$ModPath,
        [string]$ModName,
        [string]$CustomPath = ""
    )

    $artifacts = @()
    $searchPaths = @()

    if (-not [string]::IsNullOrWhiteSpace($CustomPath)) {
        $searchPaths += $CustomPath
    }
    else {
        # Mod-specific build output paths for Vintage Story mods
        $searchPaths += @(
            "$ModPath\$ModName\bin\Release\Mods\mod",
            "$ModPath\$ModName\bin\Release\Mods\mod\publish",
            "$ModPath\bin\Release",
            "$ModPath\publish",
            "$ModPath\artifacts",
            "$ModPath\dist",
            "$ModPath\build"
        )
    }

    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            Write-ColorOutput "🔍 Searching for artifacts in: $path" "Cyan"

            # Look for mod artifact types
            $files = Get-ChildItem -Path $path -File | Where-Object {
                $_.Extension -in @('.dll', '.json', '.deps.json', '.pdb') -or
                $_.Name -like "*$ModName*" -or
                $_.Name -eq "modinfo.json"
            }

            foreach ($file in $files) {
                $artifacts += @{
                    Path = $file.FullName
                    Name = $file.Name
                    Size = [math]::Round($file.Length / 1KB, 2)
                    RelativePath = $file.FullName.Replace("$ModPath\", "")
                }
            }
        }
    }

    return $artifacts
}


# Get current version and determine new version
$currentVersion = Get-CurrentVersion -ModInfoPath $modInfoPath
Write-ColorOutput "📋 Current version: $currentVersion" "Cyan"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Increment-Version -CurrentVersion $currentVersion -VersionType $VersionType
    Write-ColorOutput "📈 Auto-generated version: $Version (incremented $VersionType)" "Green"
}
else {
    Write-ColorOutput "🎯 Using specified version: $Version" "Green"
}

# Validate new version
if ($Version -eq $currentVersion) {
    Write-ColorOutput "❌ Error: New version ($Version) is same as current version" "Red"
    exit 1
}

Write-ColorOutput ""

# Check for uncommitted changes
$gitStatus = git status --porcelain
if ($gitStatus -and -not $DryRun) {
    Write-ColorOutput "⚠️ Warning: You have uncommitted changes:" "Yellow"
    git status --short
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-ColorOutput "Aborted by user" "Yellow"
        exit 0
    }
}

# Check current branch
$currentBranch = git branch --show-current
if ($currentBranch -ne "master") {
    Write-ColorOutput "⚠️ Warning: You are not on the master branch (current: $currentBranch)" "Yellow"
    $continue = Read-Host "Do you want to continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-ColorOutput "Aborted by user" "Yellow"
        exit 0
    }
}

Write-ColorOutput "🔧 Starting release process for $ModName v$Version..." "Cyan"
Write-ColorOutput ""

# Step 1: Update version in modinfo.json
Write-ColorOutput "📝 Updating mod version..." "Cyan"
if ($DryRun) {
    Write-ColorOutput "   [DRY RUN] Would update $modInfoPath from $currentVersion to $Version" "Yellow"
}
else {
    $oldVersion = Update-ModVersion -ModInfoPath $modInfoPath -NewVersion $Version
    Write-ColorOutput "✅ Updated modinfo.json: $oldVersion → $Version" "Green"
}

# Step 2: Generate release notes from git history
Write-ColorOutput "📝 Generating release notes from git history..." "Cyan"

# Get all commits for the mod since project start
$commitRange = "HEAD"
Write-ColorOutput "   Getting all commits for this release" "Cyan"

# Get commits that affect this mod
$modSpecificPath = "src/$ModName/"
$commits = git log $commitRange --pretty=format:"%h|%s|%an|%ad" --date=short --reverse -- $modSpecificPath

$formattedCommits = @()
if ($commits) {
    foreach ($commit in $commits) {
        if (-not [string]::IsNullOrWhiteSpace($commit)) {
            $parts = $commit -split '\|', 4
            if ($parts.Length -eq 4) {
                $hash = $parts[0].Trim()
                $message = $parts[1].Trim()
                $author = $parts[2].Trim()
                $date = $parts[3].Trim()
                $formattedCommits += "- **$message** ([``$hash``](https://github.com/HueByte/VintageHue/commit/$hash)) by @$author on $date"
            }
        }
    }
}

$commitsList = if ($formattedCommits.Count -gt 0) {
    $formattedCommits -join "`n"
}
else {
    "No mod-specific commits found since last release."
}

# Create release notes content
$releaseDate = Get-Date -Format "yyyy-MM-dd"
$changelogUrl = "https://huebyte.github.io/VintageHue/docs/$ModName/CHANGELOG.html"

$releaseNotes = @"
# $ModName v$Version Release Notes

**🎉 Release v$Version**

**Release Date:** $releaseDate
**Version:** v$Version
**Status:** ✅ Stable

---

## 📦 Download

- **[⬇️ Download $ModName v$Version](https://github.com/HueByte/VintageHue/releases)**
- **Compatibility:** Vintage Story 1.21.1+
- **Platforms:** Windows, Linux, macOS

---

## 🔄 Commits in this release:

$commitsList

---

## ℹ️ Release Information

- **Mod**: $ModName
- **Version**: v$Version
- **Branch**: $currentBranch
- **Generated on**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss UTC")
- **Detailed Changelog**: [$changelogUrl]($changelogUrl)

---

**Full Changelog**: https://github.com/HueByte/VintageHue/commits/master
"@

# Step 3: Create release notes file
$docsPath = Join-Path $repoRoot "docs" $ModName
$releaseNotesPath = Join-Path $docsPath "release-notes"
if (-not (Test-Path $releaseNotesPath)) {
    New-Item -ItemType Directory -Path $releaseNotesPath -Force | Out-Null
}

$releaseNotesFile = Join-Path $releaseNotesPath "v$Version-release.md"

Write-ColorOutput "📝 Creating release notes file..." "Cyan"
if ($DryRun) {
    Write-ColorOutput "   [DRY RUN] Would create: $releaseNotesFile" "Yellow"
}
else {
    $releaseNotes | Out-File -FilePath $releaseNotesFile -Encoding UTF8
    Write-ColorOutput "✅ Release notes saved to: $releaseNotesFile" "Green"
}

# Step 4: Build the mod using build.ps1
if (-not $SkipBuild) {
    Write-ColorOutput "🔨 Building mod using build.ps1..." "Cyan"
    $buildScript = Join-Path $modPath "build.ps1"

    if (Test-Path $buildScript) {
        if ($DryRun) {
            Write-ColorOutput "   [DRY RUN] Would run: $buildScript" "Yellow"
        }
        else {
            try {
                Push-Location $modPath
                & ./build.ps1
                if ($LASTEXITCODE -eq 0) {
                    Write-ColorOutput "✅ Mod built successfully" "Green"
                }
                else {
                    Write-ColorOutput "❌ Build failed with exit code: $LASTEXITCODE" "Red"
                    Pop-Location
                    exit 1
                }
                Pop-Location
            }
            catch {
                Pop-Location
                Write-ColorOutput "❌ Error running build script: $_" "Red"
                exit 1
            }
        }
    }
    else {
        Write-ColorOutput "❌ Error: build.ps1 not found in $modPath" "Red"
        exit 1
    }
}
else {
    Write-ColorOutput "⏭️ Skipping mod build (--SkipBuild specified)" "Yellow"
}

# Step 5: Run markdown lint and commit if changes made
if (-not $SkipLint) {
    Write-ColorOutput "📋 Running markdown lint..." "Cyan"
    $markdownLintScript = Join-Path $repoRoot "scripts" "markdownlint.ps1"

    if (Test-Path $markdownLintScript) {
        if ($DryRun) {
            Write-ColorOutput "   [DRY RUN] Would run: $markdownLintScript -Fix" "Yellow"
            Write-ColorOutput "   [DRY RUN] Would check for changes and commit if any" "Yellow"
        }
        else {
            # Check git status before linting
            $gitStatusBefore = git status --porcelain

            try {
                & $markdownLintScript -Fix
                if ($LASTEXITCODE -eq 0) {
                    Write-ColorOutput "✅ Markdown linting completed successfully" "Green"

                    # Check if linting made any changes
                    $gitStatusAfter = git status --porcelain
                    $lintChanges = Compare-Object $gitStatusBefore $gitStatusAfter -ErrorAction SilentlyContinue

                    if ($lintChanges) {
                        Write-ColorOutput "📝 Markdown linting made changes, committing them..." "Cyan"
                        git add .
                        git commit -m "docs: Fix markdown linting issues

- Auto-fix markdown formatting and style issues
- Update documentation to follow project standards

🤖 Automated markdown linting"

                        if ($LASTEXITCODE -eq 0) {
                            Write-ColorOutput "✅ Markdown lint changes committed" "Green"
                        }
                        else {
                            Write-ColorOutput "❌ Failed to commit markdown lint changes" "Red"
                            exit 1
                        }
                    }
                    else {
                        Write-ColorOutput "ℹ️ No markdown changes needed" "White"
                    }
                }
                else {
                    Write-ColorOutput "❌ Markdown linting failed" "Red"
                    exit 1
                }
            }
            catch {
                Write-ColorOutput "❌ Error running markdown lint: $_" "Red"
                exit 1
            }
        }
    }
    else {
        Write-ColorOutput "⚠️ Markdown lint script not found, skipping" "Yellow"
    }
}
else {
    Write-ColorOutput "⏭️ Skipping markdown lint (--SkipLint specified)" "Yellow"
}

Write-ColorOutput ""

# Step 6: Stage release changes (will be committed later if user confirms)
Write-ColorOutput "📤 Staging release changes..." "Cyan"
if ($DryRun) {
    Write-ColorOutput "   [DRY RUN] Would stage: modinfo.json, release notes, documentation" "Yellow"
    Write-ColorOutput "   [DRY RUN] Would commit: 'release: $ModName v$Version' (locally only)" "Yellow"
}
else {
    git add $modInfoPath
    git add "$docsPath/"

    Write-ColorOutput "✅ Release changes staged for commit" "Green"
}

Write-ColorOutput ""

# Step 6: Collect build artifacts
Write-ColorOutput "📦 Collecting build artifacts..." "Cyan"
$buildArtifacts = @()
if (-not $SkipBuild -or (Test-Path (Join-Path $modPath "$ModName\bin\Release"))) {
    $buildArtifacts = Get-ModBuildArtifacts -ModPath $modPath -ModName $ModName -CustomPath $BuildPath

    if ($buildArtifacts.Count -gt 0) {
        Write-ColorOutput "✅ Found $($buildArtifacts.Count) build artifact(s):" "Green"
        foreach ($artifact in $buildArtifacts) {
            Write-ColorOutput "  - $($artifact.Name) ($($artifact.Size) KB)" "White"
        }
    }
    else {
        Write-ColorOutput "⚠️ No build artifacts found" "Yellow"
    }
}
else {
    Write-ColorOutput "⏭️ Skipping artifact collection (build was skipped)" "Yellow"
}

Write-ColorOutput ""

# Step 7: Show summary and ask for push confirmation
Write-ColorOutput "📋 Release Summary:" "Yellow"
Write-ColorOutput "- Mod: $ModName" "White"
Write-ColorOutput "- Version: $currentVersion → $Version" "White"
Write-ColorOutput "- Release notes: Generated from commit history" "White"
Write-ColorOutput "- Release notes: $releaseNotesFile" "White"
Write-ColorOutput "- Commits: $($formattedCommits.Count) changes included" "White"
Write-ColorOutput ""

if ($DryRun) {
    Write-ColorOutput "🔍 DRY RUN COMPLETE - Actions that would be performed:" "Yellow"
    Write-ColorOutput ""
    Write-ColorOutput "1. Version Management:" "Cyan"
    Write-ColorOutput "   - Update modinfo.json: $currentVersion → $Version" "White"
    Write-ColorOutput "2. Release Preparation:" "Cyan"
    Write-ColorOutput "   - Generate release notes from git history" "White"
    if (-not $SkipBuild) {
        Write-ColorOutput "   - Build mod using $modPath\\build.ps1" "White"
    }
    if (-not $SkipLint) {
        Write-ColorOutput "   - Run markdown linting with auto-fix" "White"
    }
    Write-ColorOutput "   - Collect build artifacts for release" "White"
    Write-ColorOutput "3. Git Operations:" "Cyan"
    Write-ColorOutput "   - Commit markdown lint changes (if any)" "White"
    Write-ColorOutput "   - Commit version and documentation changes" "White"
    Write-ColorOutput "   - Create release documentation locally" "White"
    if ($CreateGitHubRelease) {
        Write-ColorOutput "4. GitHub Release:" "Cyan"
        Write-ColorOutput "   - Create GitHub release with artifacts" "White"
        Write-ColorOutput "   - Attach release notes and changelog link" "White"
        if ($buildArtifacts.Count -gt 0) {
            Write-ColorOutput "   - Upload $($buildArtifacts.Count) build artifact(s)" "White"
        }
    }
    Write-ColorOutput ""
    Write-ColorOutput "To execute this release, run without the -DryRun flag" "Yellow"
    exit 0
}

# Ask user confirmation to commit release changes
Write-ColorOutput "❓ Commit release changes to local repository?" "Yellow"
Write-ColorOutput "This will:" "White"
Write-ColorOutput "- Commit version bump and release notes locally" "White"
Write-ColorOutput "- Add release documentation to local repository" "White"
Write-ColorOutput "- NO changes will be pushed to remote" "Yellow"
Write-ColorOutput ""

$commitConfirm = Read-Host "Enter 'yes' to commit locally, or 'no' to finish without committing"

if ($commitConfirm -eq "yes") {
    Write-ColorOutput "📤 Committing release changes locally..." "Cyan"

    # Create the release commit
    git commit -m "release: $ModName v$Version

- Update modinfo.json version to $Version
- Generate release notes from git history

🤖 Automated release preparation"

    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Release changes committed locally!" "Green"
        Write-ColorOutput ""
        Write-ColorOutput "🎉 Release $ModName v$Version completed!" "Green"
    }
    else {
        Write-ColorOutput "❌ Failed to commit release changes" "Red"
        exit 1
    }
    Write-ColorOutput "📝 Release notes: $releaseNotesFile" "Cyan"
    Write-ColorOutput "📚 Documentation will be available at: $changelogUrl" "Cyan"
}
else {
    Write-ColorOutput "✅ Release prepared locally (not committed)" "Green"
    Write-ColorOutput "📝 To commit later, add and commit the changes manually" "Cyan"
    Write-ColorOutput ""
    Write-ColorOutput "🎉 Local release $ModName v$Version completed!" "Green"
}
