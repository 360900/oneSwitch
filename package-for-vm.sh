#!/usr/bin/env bash
# Builds OneSwitch and packages the output into a single zip so it can be
# copied into the Windows VM instead of running straight off the RDP-redirected
# shared folder (WinBoat), which caches stale file contents client-side.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR/OneSwitch"
OUTPUT_DIR="$PROJECT_DIR/bin/Debug/net10.0"
ZIP_PATH="$SCRIPT_DIR/OneSwitchApp.zip"

echo "Building..."
dotnet build "$PROJECT_DIR/OneSwitch.csproj"

echo "Packaging build output..."
rm -f "$ZIP_PATH"
python3 -c "
import shutil, os
shutil.make_archive('${ZIP_PATH%.zip}', 'zip', '$OUTPUT_DIR')
"

echo "Done: $ZIP_PATH ($(date '+%Y-%m-%d %H:%M:%S'))"
