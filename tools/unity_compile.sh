#!/bin/bash
# One compile pass: run the editor headless, harvest UNIQUE compile errors.
#   bash tools/unity_compile.sh            → error list + count
#   exit 0 = clean compile
set -uo pipefail
U="/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOG="${TMPDIR:-/tmp}/runway_unity_compile.log"
"$U" -batchmode -quit -nographics -projectPath "$ROOT/unity" -logFile "$LOG" >/dev/null 2>&1
ERRS=$(grep -E "error CS[0-9]+" "$LOG" | sed 's/^.*Assets\//Assets\//' | sort -u)
COUNT=$(printf "%s" "$ERRS" | grep -c "error CS" || true)
echo "── unique compile errors: $COUNT"
printf "%s\n" "$ERRS" | head -40
[ "$COUNT" -eq 0 ]
