#!/usr/bin/env pwsh

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

.PARAMETER CreateGitHubRelease
    Create GitHub release with built artifacts (default: true)

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

.EXAMPLE
    .\scripts\create-release-enhanced.ps1 -ModName "HueHordes" -CreateGitHubRelease:$false
    Create release without GitHub release (local only)
#>

param(
    [string]$ModName = "",
    [string]$Version = "",
    [ValidateSet("major", "minor", "patch")]
    [string]$VersionType = "patch",
    [switch]$DryRun = $false,
    [switch]$SkipLint = $false,
    [switch]$SkipBuild = $false,
    [switch]$CreateGitHubRelease = $true,
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
            $modInfoPath = Get-ChildItem -Path $folder.FullName -Filter "modinfo.json" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

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

    $modInfoPath = Get-ChildItem -Path $modPath -Filter "modinfo.json" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $modInfoPath) {
        Write-ColorOutput "❌ Error: modinfo.json not found for mod '$ModName'" "Red"
        exit 1
    }
    $modInfoPath = $modInfoPath.FullName
    Write-ColorOutput "✅ Using mod: $ModName" "Green"
}

Write-ColorOutput ""

# Function to parse and update version
function Get-CurrentVersion {
    param([string]$ModInfoPath)

    $modInfo = Get-Content $ModInfoPath | ConvertFrom-Json
    return $modInfo.version
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

function Test-GitHubCLI {
    try {
        $ghVersion = gh --version 2>$null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
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

# Check if tag already exists
$tagName = "v$Version"
$existingTag = git tag -l $tagName
if ($existingTag) {
    Write-ColorOutput "❌ Error: Tag '$tagName' already exists" "Red"
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
if ($currentBranch -ne "main") {
    Write-ColorOutput "⚠️ Warning: You are not on the main branch (current: $currentBranch)" "Yellow"
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

# Get commits since last tag
$lastTag = git describe --tags --abbrev=0 2>$null
if (-not $lastTag) {
    $commitRange = "HEAD"
    Write-ColorOutput "   No previous tags found, using all commits" "Yellow"
}
else {
    $commitRange = "$lastTag..HEAD"
    Write-ColorOutput "   Comparing against last tag: $lastTag" "Cyan"
}

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
                $formattedCommits += "- **$message** ([``$hash``](https://github.com/HueByte/HueHordes/commit/$hash)) by @$author on $date"
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
$changelogUrl = "https://huebyte.github.io/HueHordes/docs/$ModName/CHANGELOG.html"

$releaseNotes = @"
# $ModName v$Version Release Notes

**🎉 Release v$Version**

**Release Date:** $releaseDate
**Version:** v$Version
**Status:** ✅ Stable

---

## 📦 Download

- **[⬇️ Download $ModName v$Version](https://github.com/HueByte/HueHordes/releases/tag/v$Version)**
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

**Full Changelog**: https://github.com/HueByte/HueHordes/compare/$lastTag...v$Version
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

# Step 5: Run markdown lint
if (-not $SkipLint) {
    Write-ColorOutput "📋 Running markdown lint..." "Cyan"
    $markdownLintScript = Join-Path $repoRoot "scripts" "markdownlint.ps1"

    if (Test-Path $markdownLintScript) {
        if ($DryRun) {
            Write-ColorOutput "   [DRY RUN] Would run: $markdownLintScript -Fix" "Yellow"
        }
        else {
            try {
                & $markdownLintScript -Fix
                if ($LASTEXITCODE -eq 0) {
                    Write-ColorOutput "✅ Markdown linting completed successfully" "Green"
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

# Step 5: Stage changes and create commit
Write-ColorOutput "📤 Staging changes..." "Cyan"
if ($DryRun) {
    Write-ColorOutput "   [DRY RUN] Would stage: modinfo.json, release notes, documentation" "Yellow"
    Write-ColorOutput "   [DRY RUN] Would commit: 'release: $ModName v$Version'" "Yellow"
}
else {
    git add $modInfoPath
    git add "$docsPath/"
    git commit -m "release: $ModName v$Version

- Update modinfo.json version to $Version
- Generate release notes from git history
- Run markdown linting

🤖 Automated release preparation"

    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Changes committed successfully" "Green"
    }
    else {
        Write-ColorOutput "❌ Failed to commit changes" "Red"
        exit 1
    }
}

# Step 6: Create git tag
Write-ColorOutput "🏷️ Creating git tag..." "Cyan"
if ($DryRun) {
    Write-ColorOutput "   [DRY RUN] Would create tag: $tagName" "Yellow"
}
else {
    git tag -a $tagName -m "$ModName Release v$Version

$commitsList

🚀 Release v$Version
📅 Released on $releaseDate"

    if ($LASTEXITCODE -eq 0) {
        Write-ColorOutput "✅ Tag $tagName created successfully" "Green"
    }
    else {
        Write-ColorOutput "❌ Failed to create tag" "Red"
        exit 1
    }
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
        if ($CreateGitHubRelease -and -not $DryRun) {
            $continue = Read-Host "Continue without artifacts? (y/N)"
            if ($continue -ne "y" -and $continue -ne "Y") {
                Write-ColorOutput "Aborted by user" "Yellow"
                exit 0
            }
        }
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
Write-ColorOutput "- Tag: $tagName" "White"
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
    Write-ColorOutput "   - Commit version and documentation changes" "White"
    Write-ColorOutput "   - Create and push git tag: $tagName" "White"
    Write-ColorOutput "   - Push changes to origin/$currentBranch" "White"
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

# Ask user confirmation to push
Write-ColorOutput "❓ Push changes and tag to remote repository?" "Yellow"
Write-ColorOutput "This will:" "White"
Write-ColorOutput "- Push commit to origin/$currentBranch" "White"
Write-ColorOutput "- Push tag $tagName to origin" "White"
Write-ColorOutput "- Trigger CI workflows" "White"
Write-ColorOutput ""

$pushConfirm = Read-Host "Enter 'yes' to push, or 'no' to finish locally"

if ($pushConfirm -eq "yes") {
    Write-ColorOutput "📤 Pushing to remote repository..." "Cyan"

    # Push commit
    git push origin $currentBranch
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Failed to push commit" "Red"
        exit 1
    }

    # Push tag
    git push origin $tagName
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "❌ Failed to push tag" "Red"
        exit 1
    }

    Write-ColorOutput "✅ Successfully pushed changes and tag!" "Green"

    # Create GitHub release if requested
    if ($CreateGitHubRelease) {
        Write-ColorOutput ""
        Write-ColorOutput "🚀 Creating GitHub release..." "Cyan"

        if (-not (Test-GitHubCLI)) {
            Write-ColorOutput "⚠️ GitHub CLI (gh) not found or not authenticated" "Yellow"
            Write-ColorOutput "📦 Manual release creation:" "Cyan"
            Write-ColorOutput "   URL: https://github.com/HueByte/HueHordes/releases/new?tag=$tagName" "White"
            Write-ColorOutput "   Release notes file: $releaseNotesFile" "White"
        }
        else {
            try {
                $releaseArgs = @(
                    "release", "create", $tagName,
                    "--title", "$ModName v$Version",
                    "--notes-file", $releaseNotesFile
                )

                # Add artifacts if available
                if ($buildArtifacts.Count -gt 0) {
                    Write-ColorOutput "📎 Attaching $($buildArtifacts.Count) artifact(s)..." "Cyan"
                    foreach ($artifact in $buildArtifacts) {
                        $releaseArgs += $artifact.Path
                    }
                }

                & gh @releaseArgs

                if ($LASTEXITCODE -eq 0) {
                    Write-ColorOutput "✅ GitHub release created successfully!" "Green"
                    $releaseUrl = "https://github.com/HueByte/HueHordes/releases/tag/$tagName"
                    Write-ColorOutput "🔗 Release URL: $releaseUrl" "Cyan"
                }
                else {
                    Write-ColorOutput "❌ Failed to create GitHub release (exit code: $LASTEXITCODE)" "Red"
                    Write-ColorOutput "📦 Manual release creation:" "Cyan"
                    Write-ColorOutput "   URL: https://github.com/HueByte/HueHordes/releases/new?tag=$tagName" "White"
                }
            }
            catch {
                Write-ColorOutput "❌ Error creating GitHub release: $_" "Red"
                Write-ColorOutput "📦 Manual release creation:" "Cyan"
                Write-ColorOutput "   URL: https://github.com/HueByte/HueHordes/releases/new?tag=$tagName" "White"
            }
        }
    }

    Write-ColorOutput ""
    Write-ColorOutput "🎉 Release $ModName v$Version completed!" "Green"
    Write-ColorOutput "📚 Documentation will be available at: $changelogUrl" "Cyan"
}
else {
    Write-ColorOutput "✅ Release prepared locally" "Green"
    Write-ColorOutput "📝 To push later, run:" "Cyan"
    Write-ColorOutput "   git push origin $currentBranch" "White"
    Write-ColorOutput "   git push origin $tagName" "White"
    Write-ColorOutput ""
    Write-ColorOutput "🎉 Local release $ModName v$Version completed!" "Green"
}