# Google Play release checklist

Work through this top to bottom. Items marked **(required)** block publishing.
Google changes requirements every year — when Play Console shows a warning, it wins over this file.

## 1. Developer account
- [ ] **(required)** Create a Google Play developer account at <https://play.google.com/console/signup> — one-time US$25 fee.
- [ ] **(required)** Complete identity verification (government ID; organisations also need a D-U-N-S number).
- [ ] Personal accounts created after Nov 2023 must run a **closed test with at least 12 testers opted-in for 14 days in a row**
      before you can apply for production access. Plan for this — recruit testers early (friends, a Discord, a Google Group).

## 2. Create the upload key (once — back it up!)
```bash
keytool -genkeypair -v -keystore stormdrop-upload.keystore -alias stormdrop \
        -keyalg RSA -keysize 2048 -validity 10000
```
- Store the `.keystore` file and both passwords in a password manager **and** an offline backup. Never commit them
  (`.gitignore` already excludes `*.keystore` / `*.jks`).
- Google **Play App Signing** (mandatory for new apps) holds the real app-signing key; this file is only your *upload* key.
  If you lose it, you can request an upload-key reset in Play Console, but it takes days.

## 3. Build requirements (handled by `Stormdrop ▸ 2. Build Settings…`)
- [x] Android App Bundle (`.aab`) — APKs are not accepted for new apps
- [x] 64-bit: IL2CPP with ARM64 (+ ARMv7 for old 2 GB phones)
- [ ] **(required)** **Target API level**: Google requires a recent Android version (API 35 / Android 15 for apps
      updated after Aug 2025; expect API 36 / Android 16 from Aug 2026). Install the latest SDK Platform in
      *Unity Hub → Installs → Add modules → Android SDK & NDK Tools*, or via Android Studio's SDK Manager. Our build uses
      "Automatic (highest installed)".
- [ ] **(required)** **16 KB memory page support** for apps targeting Android 15+: use Unity **6000.0.38f1 or newer**
      (earlier Unity 6 versions produce non-compliant native libraries). Check *App bundle explorer → Memory page size* after upload.
- [ ] Version code increases on every upload (the build tool bumps it automatically after a successful build).
- [ ] Download size: keep the base module under 200 MB; move extra maps/skins to Play Asset Delivery if you grow past it.
- [ ] Enable **Create symbols.zip: Public** in *File ▸ Build Profiles ▸ Android* and upload the symbols with each release
      so Play's Android vitals show readable native crashes.

## 4. Create the app in Play Console
- [ ] *Create app* → name **Stormdrop Royale**, Game, Free, accept declarations.
- [ ] **(required)** The package name (`com.yourstudio.stormdrop`) is set by your first upload and can never change.

## 5. App content (Policy → App content) — all required
- [ ] Privacy policy URL → `https://[your-project].vercel.app/privacy` (edit `backend/public/privacy.html` first:
      studio name, email)
- [ ] App access → "All functionality available without special access" (Guest login)
- [ ] Ads → **No ads**
- [ ] Content rating → see `CONTENT_RATING.md`
- [ ] Target audience → 13+ only (see `CONTENT_RATING.md`)
- [ ] Data safety → see `DATA_SAFETY.md`
- [ ] Account deletion → in-app path + URL `https://[your-project].vercel.app/delete-account`
- [ ] Advertising ID declaration → see `DATA_SAFETY.md`
- [ ] Government apps, Financial features, Health, News: No
- [ ] **Permissions**: the app uses INTERNET, ACCESS_NETWORK_STATE, POST_NOTIFICATIONS (Android 13+) and, with voice chat,
      RECORD_AUDIO. Request the microphone permission only when the player turns voice chat on, and explain why first.

## 6. Store listing
- [ ] Texts, icon, feature graphic and screenshots → `STORE_LISTING.md`, `graphics/`
- [ ] Store settings → category Action, contact email, (optional) website

## 7. Monetisation
- [ ] Merchant/payments profile set up (Play Console → Settings → Payments profile) to sell in-app products
- [ ] In-app products → `IAP_PRODUCTS.md`
- [ ] Service account for server-side purchase verification → `IAP_PRODUCTS.md`
- [ ] License testers added
- [ ] Fortune Drop odds visible in game before purchase ✔ (already implemented) — required in several countries

## 8. Testing tracks
1. [ ] **Internal testing** (up to 100 testers, available within minutes) — upload the first `.aab` here.
       Check the **Pre-launch report** (automatic tests on real devices) for crashes and accessibility issues.
2. [ ] **Closed testing** — at least 12 testers for 14 days (personal accounts). Fix feedback, upload updates.
3. [ ] Apply for production access (Dashboard), answer the questionnaire about your test.
4. [ ] **Production** — select countries, start a **staged rollout** (e.g. 10% → 50% → 100%) and watch Android vitals.

## 9. Live-ops readiness
- [ ] Backend deployed on Vercel with all env vars, `/api/health` shows `database: connected`
- [ ] At least one dedicated-server VM per launch region registered (`game_servers` table shows `ready`)
- [ ] Firebase Crashlytics receiving test crashes; Analytics DebugView shows events
- [ ] Support email monitored; `deletion_requests` and `reports` tables reviewed weekly
- [ ] Supabase: Point-in-time recovery or daily backups enabled; database password rotated after setup

## 10. iOS (App Store) — later
Apple Developer Program (US$99/year), Sign in with Apple is **required** if you offer Google/Facebook login on iOS,
App Store Connect privacy "nutrition labels" (same data as `DATA_SAFETY.md`), loot-box odds disclosure, and
App Tracking Transparency only if you add ad tracking.
