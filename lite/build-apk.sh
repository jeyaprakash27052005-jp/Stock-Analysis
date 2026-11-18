#!/usr/bin/env bash
# Builds releases/stormdrop-royale-lite.apk (Android 7+) without the Android SDK or Unity:
#   apktool (bundles aapt2 + the Android framework) compiles resources + smali, apksig signs it.
# Requirements: Java 17+, curl, python3.   Usage: lite/build-apk.sh
set -euo pipefail
cd "$(dirname "$0")"
TOOLS=".tools"; BUILD="build"; OUT="../releases/stormdrop-royale-lite.apk"
APKTOOL_VER=3.0.3; APKSIG_VER=2.3.0
mkdir -p "$TOOLS"

fetch() {   # fetch <url> <file> — downloads from Maven Central and checks the published SHA-1
  [ -f "$2" ] && return
  curl -fsSL -o "$2" "$1"
  local want; want=$(curl -fsSL "$1.sha1" | cut -c1-40)
  local got; got=$(sha1sum "$2" | cut -c1-40)
  [ "$want" = "$got" ] || { echo "checksum mismatch for $1"; rm -f "$2"; exit 1; }
}
fetch "https://repo1.maven.org/maven2/org/apktool/apktool-cli/$APKTOOL_VER/apktool-cli-$APKTOOL_VER.jar" "$TOOLS/apktool.jar"
fetch "https://repo1.maven.org/maven2/com/android/tools/build/apksig/$APKSIG_VER/apksig-$APKSIG_VER.jar" "$TOOLS/apksig.jar"

rm -rf "$BUILD"; mkdir -p "$BUILD/project/assets/game" "$BUILD/signer"
cp -r android/. "$BUILD/project/"
cp -r web/. "$BUILD/project/assets/game/"

java -jar "$TOOLS/apktool.jar" b "$BUILD/project" -o "$BUILD/unsigned.apk" --frame-path "$BUILD/framework"

# Test signing key (sideload builds only — use your Play upload key for store releases)
KEY="signing/lite-test-key.p12"
if [ ! -f "$KEY" ]; then
  mkdir -p signing
  keytool -genkeypair -keystore "$KEY" -storetype PKCS12 -storepass android -keypass android -alias stormdrop-lite \
    -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Stormdrop Royale Lite Test, O=Stormdrop" >/dev/null
fi
javac -cp "$TOOLS/apksig.jar" -d "$BUILD/signer" tools/ApkSign.java
mkdir -p "$(dirname "$OUT")"
java --add-exports java.base/sun.security.x509=ALL-UNNAMED --add-exports java.base/sun.security.pkcs=ALL-UNNAMED --add-exports java.base/sun.security.util=ALL-UNNAMED \
  -cp "$BUILD/signer:$TOOLS/apksig.jar" ApkSign "$BUILD/unsigned.apk" "$OUT" "$KEY" android stormdrop-lite
ls -la "$OUT"
