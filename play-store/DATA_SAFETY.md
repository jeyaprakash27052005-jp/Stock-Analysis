# Data safety form — answers

**Play Console → Policy and programs → App content → Data safety.** These answers match what the code actually does
(backend/src, Unity Scripts/Backend and Scripts/Services). If you add SDKs (ads, other analytics), update this.

## Overview questions

| Question | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | **Yes** |
| Is all of the user data collected by your app encrypted in transit? | **Yes** (HTTPS to the backend; Netcode traffic over UDP carries gameplay only) |
| Do you provide a way for users to request that their data is deleted? | **Yes** — in-app (Settings → Delete account) and web: `https://[your-project].vercel.app/delete-account` |
| Account creation methods | Username/other (guest), OAuth (Google, Facebook) |
| Data retention | Accounts with no activity for 30 days (e.g. app uninstalled) are permanently deleted by a daily job; purchase records are kept without the player link for tax purposes |
| Delete account URL | `https://[your-project].vercel.app/delete-account` |

## Data types

| Category → type | Collected | Shared* | Optional? | Purposes | Ephemeral? |
|---|---|---|---|---|---|
| Personal info → **User IDs** (player ID, Google/Facebook account ID) | Yes | No | Required | App functionality, Account management, Fraud prevention/security | No |
| Personal info → **Name** (in-game nickname, Facebook first name used as default nickname) | Yes | No | Required | App functionality, Account management | No |
| Financial info → **Purchase history** | Yes | No | Optional (only if you buy) | App functionality, Fraud prevention | No |
| Messages → **Other in-app messages** (guild/party chat) | Yes | No | Optional | App functionality | No |
| Audio → **Voice or sound recordings** (voice chat, only when mic on) | Yes | No | Optional | App functionality | **Yes** (streamed live, never stored) |
| App activity → **App interactions** (matches, missions, store use) | Yes | No | Required | App functionality, Analytics | No |
| App activity → **In-app search history** | No | | | | |
| App info and performance → **Crash logs** | Yes | No | Required | Analytics, App functionality | No |
| App info and performance → **Diagnostics** (FPS, load times, device model) | Yes | No | Required | Analytics | No |
| Device or other IDs → **Device or other IDs** (Firebase app instance ID, FCM push token) | Yes | No | Required | Analytics, App functionality (notifications) | No |
| Location | **No** | | | | |
| Contacts, Photos, Files, Calendar, Health, Web history | **No** | | | | |

\* "Shared" means transferred to a *third party for its own use*. Google, Meta, Unity (Vivox), Supabase and Vercel act as
**service providers** processing data on your behalf, which Google does not count as sharing.

## Related declarations (App content page)

- **Advertising ID:** Firebase Analytics adds the `AD_ID` permission. If you do **not** show ads, answer "Yes, my app uses
  advertising ID — for Analytics", *or* remove it by adding to `Assets/Plugins/Android/AndroidManifest.xml`:
  `<uses-permission android:name="com.google.android.gms.permission.AD_ID" tools:node="remove"/>` and answer "No".
- **Ads:** "No, my app does not contain ads" (no ad SDK is included).
- **App access:** "All functionality is available without special access" — reviewers can use **Play as Guest**.
  If you ever gate content, give reviewers instructions here.
- **Government app / Financial features / Health:** No.
