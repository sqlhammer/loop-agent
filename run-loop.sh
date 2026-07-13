#!/usr/bin/env bash
# run-loop.sh — thin macOS/Linux launcher for the PowerShell harness.
# Contains no framework logic: it just forwards every argument to run-loop.ps1
# via pwsh. Windows users can keep calling `pwsh -File run-loop.ps1` directly.
#
#   ./run-loop.sh              # first run: plan + acceptance tests, then stops
#   ./run-loop.sh -Approve     # start the unattended build loop
#   ./run-loop.sh -Status      # print phase/counters
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if ! command -v pwsh >/dev/null 2>&1; then
  echo "PowerShell 7+ (pwsh) is required but not found." >&2
  echo "Install it with:  brew install --cask powershell" >&2
  exit 1
fi
exec pwsh -NoProfile -File "$SCRIPT_DIR/run-loop.ps1" "$@"
