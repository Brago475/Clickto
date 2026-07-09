#!/bin/bash
set -e

APP_NAME="Clickto"
APP_BUNDLE="dist/Clickto.app"
BG="dmg/dmg-bg.png"
OUT_DMG="dist/${APP_NAME}.dmg"

rm -f "$OUT_DMG"

create-dmg \
  --volname "$APP_NAME" \
  --background "$BG" \
  --window-pos 200 120 \
  --window-size 660 400 \
  --icon-size 96 \
  --text-size 15 \
  --icon "$APP_NAME.app" 180 190 \
  --hide-extension "$APP_NAME.app" \
  --app-drop-link 480 190 \
  --no-internet-enable \
  "$OUT_DMG" \
  "$APP_BUNDLE"

echo ""
echo "Done -> $OUT_DMG"
