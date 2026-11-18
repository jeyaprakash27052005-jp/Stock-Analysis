# Stormdrop Royale Lite (Android APK)

A lightweight, installable Android version of the game that plays **fully offline**: an HTML5 + three.js game
(`web/`) inside a full-screen landscape WebView app (`android/`). No server, no internet permission.

**APK:** `releases/stormdrop-royale-lite.apk`

| | |
|---|---|
| Android | 7.0+ (minSdk 24, targetSdk 34), signed with APK Signature Scheme v2 |
| Package | `com.stormdrop.royale.lite` |
| Permissions | none |
| Gameplay | Solo battle royale vs 19 bots: plane drop, parachute, animated 3D soldiers, loot (AR, SMG, shotgun, sniper, LMG, pistol, armor, med kits, Vigor Shrooms), shrinking storm, minimap, kill feed, results |
| Progress | Name, level/XP, Gold, matches, wins, eliminations, best finish — saved on the phone |

## Rebuild
```bash
lite/build-apk.sh      # needs Java 17+, curl, python3 — downloads apktool + apksig from Maven Central
```
Bump `versionCode` in `android/apktool.yml` for every new build you share.

`signing/lite-test-key.p12` (password `android`) is a **test key** committed so rebuilds can update an installed copy.
It is not secret — never use it for Google Play; create your own upload key for store releases.

## Installing on a phone
Open the APK on the phone → allow *Install unknown apps* for the app you opened it from → Install.
Play Protect may show "unknown developer" because the app isn't from Google Play — expected for test builds.
