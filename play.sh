#!/bin/bash
# RUNWAY! — play the game yourself.
#   ./play.sh            normal run, starts at the title screen
#   ./play.sh --fresh    wipe the saved run and profile first (new game, empty gallery)
#   ./play.sh --log      also write everything the game prints to /tmp/runway_play.log
#
# The window is yours: nothing in this repo will ever kill it.
set -e
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME="$HERE/game"

if [[ " $* " == *" --fresh "* ]]; then
  SAVE="$HOME/Library/Application Support/Godot/app_userdata/RUNWAY!"
  rm -f "$SAVE/run_save.json" "$SAVE/profile.json" 2>/dev/null || true
  echo "fresh start: saved run and profile cleared"
fi

# make sure any art the lanes produced since the last run is imported, otherwise
# new scenes silently fall back to older ones
godot --headless --path "$GAME" --import >/dev/null 2>&1 || true

echo "launching RUNWAY!  (close the window when you're done)"
if [[ " $* " == *" --log "* ]]; then
  godot --path "$GAME" 2>&1 | tee /tmp/runway_play.log
  echo "log written to /tmp/runway_play.log"
else
  godot --path "$GAME"
fi
