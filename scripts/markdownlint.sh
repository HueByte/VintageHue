#!/bin/bash

# HueHordes Markdownlint Runner (Shell version)
# Runs markdownlint on all Markdown files in the repository

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
FIX=false
VERBOSE=false
PATH_PATTERN="**/*.md"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -f|--fix)
            FIX=true
            shift
            ;;
        -v|--verbose)
            VERBOSE=true
            shift
            ;;
        -p|--path)
            PATH_PATTERN="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -f, --fix     Automatically fix issues that can be fixed"
            echo "  -v, --verbose Show verbose output during linting"
            echo "  -p, --path    Specific path or pattern to lint (default: **/*.md)"
            echo "  -h, --help    Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

function print_colored() {
    local color=$1
    local message=$2
    if [[ -t 1 ]] && [[ -z "${NO_COLOR:-}" ]] && [[ -z "${CI:-}" ]]; then
        echo -e "${color}${message}${NC}"
    else
        echo "$message"
    fi
}

function check_node() {
    if ! command -v node &> /dev/null; then
        print_colored "$RED" "❌ Node.js is not installed or not in PATH"
        print_colored "$YELLOW" "Please install Node.js from https://nodejs.org/"
        exit 1
    fi

    local node_version=$(node --version)
    print_colored "$GREEN" "✅ Node.js version: $node_version"
}

function check_markdownlint() {
    if ! command -v markdownlint &> /dev/null; then
        print_colored "$YELLOW" "⚠️  markdownlint-cli not found, installing globally..."
        npm install -g markdownlint-cli
        print_colored "$GREEN" "✅ markdownlint-cli installed successfully"
    fi

    local markdownlint_version=$(markdownlint --version)
    print_colored "$GREEN" "✅ markdownlint-cli version: $markdownlint_version"
}

# Change to repository root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$REPO_ROOT"

print_colored "$CYAN" "🔍 HueHordes Markdownlint Runner"
print_colored "$CYAN" "==============================="
echo

# Check dependencies
check_node
check_markdownlint
echo

# Check configuration
CONFIG_FILE="config/.markdownlint.json"
if [[ ! -f "$CONFIG_FILE" ]]; then
    print_colored "$RED" "❌ Configuration file $CONFIG_FILE not found"
    print_colored "$YELLOW" "💡 Please create the configuration file first"
    exit 1
fi

print_colored "$CYAN" "📋 Using configuration from $CONFIG_FILE"
echo

# Build markdownlint command
MARKDOWNLINT_ARGS=()
MARKDOWNLINT_ARGS+=("$PATH_PATTERN")

# Add ignore patterns
IGNORE_PATTERNS=(
    "node_modules"
    "TestResults"
    "bin"
    "obj"
    "publish"
    "dist"
    "release"
    "coverage"
    ".vs"
    ".vscode"
    "VintagestoryData"
    "VintageStoryData"
    "Mods"
    "ModData"
    "dev-local"
    "docs/_site"
    "docs/*/api"
)

for pattern in "${IGNORE_PATTERNS[@]}"; do
    MARKDOWNLINT_ARGS+=("--ignore" "$pattern")
done

# Add configuration
MARKDOWNLINT_ARGS+=("--config" "$CONFIG_FILE")

# Add fix flag if requested
if [[ "$FIX" == "true" ]]; then
    MARKDOWNLINT_ARGS+=("--fix")
    print_colored "$CYAN" "🔧 Auto-fix mode enabled"
fi

print_colored "$CYAN" "🚀 Running markdownlint on HueHordes repository..."
print_colored "$CYAN" "Command: markdownlint ${MARKDOWNLINT_ARGS[*]}"
echo

# Run markdownlint
if [[ "$VERBOSE" == "true" ]]; then
    markdownlint "${MARKDOWNLINT_ARGS[@]}"
else
    if output=$(markdownlint "${MARKDOWNLINT_ARGS[@]}" 2>&1); then
        print_colored "$GREEN" "✅ Markdownlint completed successfully!"
        if [[ -n "$output" ]]; then
            print_colored "$CYAN" "Output:"
            echo "$output"
        fi
    else
        print_colored "$RED" "❌ Markdownlint found issues:"
        echo "$output"
        exit 1
    fi
fi

echo
if [[ "$FIX" == "true" ]]; then
    print_colored "$GREEN" "🎉 Markdownlint completed with auto-fix!"
    print_colored "$CYAN" "📝 Check the changes and commit if appropriate."
else
    print_colored "$GREEN" "🎉 Markdownlint completed successfully!"
    print_colored "$CYAN" "💡 Use --fix parameter to automatically fix issues."
    print_colored "$CYAN" "💡 Use --path parameter to lint specific files or directories."
fi

echo
print_colored "$CYAN" "📁 Linted areas:"
print_colored "$CYAN" "  • Repository README files"
print_colored "$CYAN" "  • HueHordes mod documentation"
print_colored "$CYAN" "  • Test documentation"
print_colored "$CYAN" "  • Future mod documentation"