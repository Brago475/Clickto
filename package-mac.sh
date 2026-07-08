#!/bin/bash
set -e
APP_NAME="Clickto"
PROJECT="Clickto/Clickto.csproj"
ICONSET_SRC="clickto-icon"
OUT="dist"
BUNDLE="$OUT/$APP_NAME.app"

echo "==> Cleaning previous build"
rm -rf "$OUT"; mkdir -p "$OUT"

echo "==> Publishing self-contained arm64 build"
dotnet publish "$PROJECT" -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -o "$OUT/publish"

echo "==> Building .app bundle"
mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"
cp -R "$OUT/publish/." "$BUNDLE/Contents/MacOS/"

echo "==> Generating .icns"
ICONSET="$OUT/Clickto.iconset"; mkdir -p "$ICONSET"
cp "$ICONSET_SRC/clickto-16.png"   "$ICONSET/icon_16x16.png"
cp "$ICONSET_SRC/clickto-32.png"   "$ICONSET/icon_16x16@2x.png"
cp "$ICONSET_SRC/clickto-32.png"   "$ICONSET/icon_32x32.png"
cp "$ICONSET_SRC/clickto-64.png"   "$ICONSET/icon_32x32@2x.png"
cp "$ICONSET_SRC/clickto-128.png"  "$ICONSET/icon_128x128.png"
cp "$ICONSET_SRC/clickto-256.png"  "$ICONSET/icon_128x128@2x.png"
cp "$ICONSET_SRC/clickto-256.png"  "$ICONSET/icon_256x256.png"
cp "$ICONSET_SRC/clickto-512.png"  "$ICONSET/icon_256x256@2x.png"
cp "$ICONSET_SRC/clickto-512.png"  "$ICONSET/icon_512x512.png"
cp "$ICONSET_SRC/clickto-1024.png" "$ICONSET/icon_512x512@2x.png"
iconutil -c icns "$ICONSET" -o "$BUNDLE/Contents/Resources/Clickto.icns"

echo "==> Writing Info.plist"
cat > "$BUNDLE/Contents/Info.plist" << 'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>Clickto</string>
    <key>CFBundleDisplayName</key><string>Clickto</string>
    <key>CFBundleIdentifier</key><string>com.tcwstudio.clickto</string>
    <key>CFBundleVersion</key><string>1.0.0</string>
    <key>CFBundleShortVersionString</key><string>1.0.0</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleExecutable</key><string>Clickto</string>
    <key>CFBundleIconFile</key><string>Clickto.icns</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

chmod +x "$BUNDLE/Contents/MacOS/$APP_NAME"
rm -rf "$ICONSET" "$OUT/publish"
echo ""
echo "Done -> $BUNDLE"
