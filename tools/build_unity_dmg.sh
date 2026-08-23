#!/bin/bash
# RUNWAY! Unity DMG: wrap the ALREADY-BUILT unity player in the same styled
# drag-to-Applications window as the Godot DMG (same drawn background, same
# layout). Build the app first (Runway.Build.BuildMac), then:
#   bash tools/build_unity_dmg.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/dist"
SRC="$ROOT/unity/build/mac/RUNWAY!.app"
STAGE="$DIST/dmg_stage_unity"
DMG="$DIST/RUNWAY-Unity.dmg"
BG="$DIST/dmg_bg.tiff"   # hidpi 1x+2x pack: Finder draws backgrounds at NATIVE pixels
VOLNAME="RUNWAY! Unity"
APPNAME="RUNWAY! (Unity).app"

[ -d "$SRC" ] || { echo "no unity app at $SRC — build it first"; exit 1; }
mkdir -p "$DIST"
rm -rf "$STAGE" "$DMG" "$DIST/runway_unity_rw.dmg"

echo "── the RW! icon (deterministic: Unity's SetIcons wrote no icns in batch) ──"
ICONSET="$DIST/rw.iconset"
rm -rf "$ICONSET"; mkdir -p "$ICONSET"
for SZ in 16 32 64 128 256 512; do
  sips -z $SZ $SZ "$ROOT/game/icon_1024.png" --out "$ICONSET/icon_${SZ}x${SZ}.png" >/dev/null
  DZ=$((SZ*2))
  sips -z $DZ $DZ "$ROOT/game/icon_1024.png" --out "$ICONSET/icon_${SZ}x${SZ}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$DIST/rw_unity.icns"
rm -rf "$ICONSET"
cp "$DIST/rw_unity.icns" "$SRC/Contents/Resources/PlayerIcon.icns"
/usr/libexec/PlistBuddy -c "Set :CFBundleIconFile PlayerIcon" "$SRC/Contents/Info.plist" 2>/dev/null \
  || /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string PlayerIcon" "$SRC/Contents/Info.plist"
codesign --force --deep --sign - "$SRC" 2>/dev/null || true   # ad-hoc reseal after edit
touch "$SRC"

echo "── evicting stale volumes (a mounted twin steals the Finder styling) ──"
for V in /Volumes/*; do
  case "$(basename "$V")" in "$VOLNAME"|"$VOLNAME "*) hdiutil detach "$V" -force >/dev/null 2>&1 || true;; esac
done

echo "── staging the DMG ──"
mkdir -p "$STAGE/.background"
cp -R "$SRC" "$STAGE/$APPNAME"
ln -s /Applications "$STAGE/Applications"
[ -f "$BG" ] && cp "$BG" "$STAGE/.background/bg.tiff"

echo "── building the DMG ──"
hdiutil create -volname "$VOLNAME" -srcfolder "$STAGE" -ov -format UDRW "$DIST/runway_unity_rw.dmg" >/dev/null
DEV=$(hdiutil attach -readwrite -noverify -noautoopen "$DIST/runway_unity_rw.dmg" | awk '/\/Volumes\//{print $1; exit}')
sleep 2
for layout_try in 1 2; do
osascript <<APPLESCRIPT && break || sleep 2
tell application "Finder"
  tell disk "$VOLNAME"
    open
    delay 1
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
    set position of item "$APPNAME" of container window to {224, 360}
    set position of item "Applications" of container window to {672, 360}
    close
    open
    delay 1
    close
  end tell
end tell
APPLESCRIPT
done
sync
osascript -e 'tell application "Finder" to close every window' >/dev/null 2>&1 || true
hdiutil detach "$DEV" >/dev/null 2>&1 || (sleep 3; hdiutil detach "$DEV" -force >/dev/null)
hdiutil convert "$DIST/runway_unity_rw.dmg" -format UDZO -o "$DMG" >/dev/null
rm -f "$DIST/runway_unity_rw.dmg"
rm -rf "$STAGE"
echo "── done: $DMG ──"
du -h "$DMG"
