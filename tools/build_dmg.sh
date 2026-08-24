#!/bin/bash
# RUNWAY! macOS build: export the .app, then wrap it in a styled DMG with a
# drawn background and a drag-to-Applications layout. Run from repo root:
#   bash tools/build_dmg.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/game"
DIST="$ROOT/dist"
STAGE="$DIST/dmg_stage"
APP="$DIST/RUNWAY!.app"
DMG="$DIST/RUNWAY.dmg"
BG="$DIST/dmg_bg.tiff"   # hidpi 1x+2x pack: Finder draws backgrounds at NATIVE pixels

mkdir -p "$DIST"
rm -rf "$APP" "$STAGE" "$DMG"

echo "── stamping the build ──"
date "+%Y-%m-%d %H:%M" | tr -d '\n' > "$GAME/build_stamp.txt"
echo " · $(git -C "$ROOT" rev-parse --short HEAD)" >> "$GAME/build_stamp.txt"

echo "── exporting the .app ──"
godot --headless --path "$GAME" --export-release "macOS" "$APP"
[ -d "$APP" ] || { echo "export failed"; exit 1; }

echo "── staging the DMG ──"
mkdir -p "$STAGE/.background"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
[ -f "$BG" ] && cp "$BG" "$STAGE/.background/bg.tiff"

echo "── evicting stale volumes (a mounted twin steals the Finder styling) ──"
for V in /Volumes/*; do
  case "$(basename "$V")" in "RUNWAY!"|"RUNWAY! "*) hdiutil detach "$V" -force >/dev/null 2>&1 || true;; esac
done

echo "── building the DMG ──"
hdiutil create -volname "RUNWAY!" -srcfolder "$STAGE" -ov -format UDRW "$DIST/runway_rw.dmg" >/dev/null
DEV=$(hdiutil attach -readwrite -noverify -noautoopen "$DIST/runway_rw.dmg" | awk '/\/Volumes\//{print $1; exit}')
VOL="/Volumes/RUNWAY!"
sleep 1
# window layout: icon view, background, app left, Applications right
osascript <<APPLESCRIPT || true
tell application "Finder"
  tell disk "RUNWAY!"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set the bounds of container window to {200, 120, 1096, 624}
    set viewOptions to the icon view options of container window
    set arrangement of viewOptions to not arranged
    set icon size of viewOptions to 128
    try
      set background picture of viewOptions to file ".background:bg.tiff"
    end try
    set position of item "RUNWAY!.app" of container window to {268, 368}
    set position of item "Applications" of container window to {628, 368}
    close
    open
    delay 1
    close
  end tell
end tell
APPLESCRIPT
VOLPATH=$(df | awk -v d="$DEV" '$1==d {print substr($0, index($0,"/Volumes/"))}')
[ -n "$VOLPATH" ] && {
  chflags hidden "$VOLPATH/.background" 2>/dev/null || true
  rm -rf "$VOLPATH/.fseventsd" 2>/dev/null || true
  mdutil -i off "$VOLPATH" >/dev/null 2>&1 || true
}
sync
osascript -e 'tell application "Finder" to close every window' >/dev/null 2>&1 || true
hdiutil detach "$DEV" >/dev/null 2>&1 || (sleep 3; hdiutil detach "$DEV" -force >/dev/null)
hdiutil convert "$DIST/runway_rw.dmg" -format UDZO -o "$DMG" >/dev/null
rm -f "$DIST/runway_rw.dmg"
rm -rf "$STAGE"
echo "── done: $DMG ──"
du -h "$DMG"
