# Stormdrop Royale Lite (Android APK)

A lightweight, installable Android version of the game, built **without Unity or the Android SDK**:
an HTML5 + three.js game (`web/`) inside a full-screen landscape WebView app (`android/`).

**Download:** `releases/stormdrop-royale-lite.apk`, or the website's Download APK button (`/download`).

| | |
|---|---|
| Android | 7.0+ (minSdk 24, targetSdk 34), signed with APK Signature Scheme v2 |
| Package | `com.stormdrop.royale.lite` |
| Online | Guest login, profile, Gold/Diamonds, rank, missions, store, daily reward — same API and Supabase database as the full game |
| Offline | Solo battle royale vs 19 bots: plane drop, parachute, loot (AR, SMG, shotgun, sniper, LMG, pistol, armor, med kits, Vigor Shrooms), shrinking storm, minimap, kill feed, results |

Practice matches against bots don't change rank points (the server can't verify them); ranked rewards come from
dedicated-server matches in the full Unity game.

## Rebuild
```bash
lite/build-apk.sh                 # needs Java 17+, curl, python3 — downloads apktool + apksig from Maven Central
cd backend && npm run assets      # bundles the new APK into the website's /download
```
Bump `versionCode` in `android/apktool.yml` for every new build you share.

`signing/lite-test-key.p12` (password `android`) is a **test key** committed so rebuilds can update an installed copy.
It is not secret — never use it for Google Play. Play Store releases use the full Unity game and your private upload key.

## Installing on a phone
Open the download link on the phone → allow *Install unknown apps* for the browser → Install. Play Protect may show
"unknown developer" because the app isn't from Google Play — that's expected for test builds.
