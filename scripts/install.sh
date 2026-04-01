#!/usr/bin/env bash
# Install or update the iaet global dotnet tool from local source
# Usage: bash scripts/install.sh

set -e
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo "Building IAET CLI..."
dotnet pack "$ROOT/src/Iaet.Cli/Iaet.Cli.csproj" -c Release -o "$ROOT/artifacts/" -v quiet

echo "Installing iaet tool..."
dotnet tool update -g Iaet.Cli --add-source "$ROOT/artifacts/" --version 0.1.0 2>/dev/null \
  || dotnet tool install -g Iaet.Cli --add-source "$ROOT/artifacts/" --version 0.1.0

echo ""
echo "Done! Run 'iaet --help' to verify."
iaet --version
