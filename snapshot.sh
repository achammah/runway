#!/bin/bash
# Snapshot the game source. No git, by the owner's standing rule — this is the
# safety net instead.
#
# WHY THIS EXISTS: game/ has never been under version control, so when a lane's
# edit accidentally spliced out _lock_week, _apply_lock and _free_move, there was
# nothing to restore from and one block of game logic had to be RECONSTRUCTED from
# the values printed on UI labels. That must never be the recovery path again.
#
#   ./snapshot.sh                 take a snapshot now
#   ./snapshot.sh list            list snapshots, newest first
#   ./snapshot.sh restore <name>  restore one INTO a sibling dir (never over game/)
#
# Snapshots are source only: scripts, data and docs. Generated art is excluded —
# it is large and reproducible.
set -e
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STORE="$HERE/.snapshots"
mkdir -p "$STORE"

case "${1:-take}" in
  list)
    ls -1t "$STORE" 2>/dev/null | sed 's/^/  /' || echo "  (none yet)"
    ;;
  restore)
    [ -n "$2" ] || { echo "usage: ./snapshot.sh restore <name>"; exit 1; }
    SRC="$STORE/$2"
    [ -d "$SRC" ] || { echo "no such snapshot: $2"; exit 1; }
    OUT="$HERE/restored-$2"
    # deliberately NOT over game/: restoring in place could clobber a lane's
    # in-flight work. Diff the two and take back only what you need.
    cp -R "$SRC" "$OUT"
    echo "restored to $OUT"
    echo "compare with:  diff -ru \"$OUT/src\" \"$HERE/game/src\" | head -80"
    ;;
  *)
    STAMP="$(date +%Y%m%d-%H%M%S)"
    DEST="$STORE/$STAMP"
    mkdir -p "$DEST"
    for d in src data docs tools tests; do
      [ -d "$HERE/game/$d" ] && cp -R "$HERE/game/$d" "$DEST/$d"
    done
    [ -f "$HERE/game/project.godot" ] && cp "$HERE/game/project.godot" "$DEST/"
    N=$(find "$DEST" -type f | wc -l | tr -d ' ')
    echo "snapshot $STAMP: $N files"
    # keep the last 40 so this never eats the disk
    ls -1t "$STORE" | tail -n +41 | while read -r old; do rm -rf "$STORE/${old:?}"; done
    ;;
esac
