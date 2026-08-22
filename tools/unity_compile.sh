#!/bin/bash
# One compile pass: run the editor headless, harvest UNIQUE compile errors.
#   bash tools/unity_compile.sh            → error list + count
#   exit 0 = clean compile (with POSITIVE evidence compilation ran)
#   exit 2 = compile could not run (editor lock/license) — retry, don't trust
# Safe under concurrency: a mkdir mutex queues callers (many agents share
# this project; two editors on one Library corrupt or false-clean).
set -uo pipefail
U="/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCK="${TMPDIR:-/tmp}/runway_unity_compile.lock"
LOG="${TMPDIR:-/tmp}/runway_unity_compile_$$.log"

waited=0
until mkdir "$LOCK" 2>/dev/null; do
  sleep 5; waited=$((waited+5))
  if [ "$waited" -ge 900 ]; then echo "── COMPILE MUTEX TIMEOUT (15m) — a stuck run holds $LOCK"; exit 2; fi
done
trap 'rmdir "$LOCK" 2>/dev/null' EXIT

"$U" -batchmode -quit -nographics -projectPath "$ROOT/unity" -logFile "$LOG" >/dev/null 2>&1
if grep -q "It looks like another Unity instance is running" "$LOG"; then
  echo "── COMPILE DID NOT RUN: project open elsewhere — retry in 60s"; exit 2
fi
if ! grep -qE "Tundra build success|scripts have compiler errors|error CS|Compilation failed|CompileScripts:" "$LOG"; then
  echo "── COMPILE EVIDENCE MISSING (editor died early?) — do not trust; see $LOG"
  tail -5 "$LOG"; exit 2
fi
ERRS=$(grep -E "error CS[0-9]+" "$LOG" | sed 's/^.*Assets\//Assets\//' | sort -u)
COUNT=$(printf "%s" "$ERRS" | grep -c "error CS" || true)
echo "── unique compile errors: $COUNT"
printf "%s\n" "$ERRS" | head -40
[ "$COUNT" -eq 0 ]
