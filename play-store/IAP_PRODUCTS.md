# In-app products (Diamonds)

Create these in **Play Console → Monetize with Play → Products → In-app products** (managed/consumable).
The product IDs must match `diamondPacks` in `backend/src/config.js` and `PurchaseService.DiamondProducts` in Unity.

> In-app products can only be created after you upload a build that contains the Billing permission
> (i.e. built with `STORMDROP_IAP` and Unity In-App Purchasing installed) to any testing track.

| Product ID | Name | Diamonds | Suggested price (INR) | Suggested price (USD) |
|---|---|---|---|---|
| `diamonds_100` | 100 Diamonds | 100 | ₹79 | $0.99 |
| `diamonds_310` | 310 Diamonds | 310 | ₹239 | $2.99 |
| `diamonds_520` | 520 Diamonds | 520 | ₹399 | $4.99 |
| `diamonds_1060` | 1,060 Diamonds | 1,060 | ₹799 | $9.99 |
| `diamonds_2180` | 2,180 Diamonds | 2,180 | ₹1,599 | $19.99 |
| `diamonds_5600` | 5,600 Diamonds | 5,600 | ₹3,999 | $49.99 |

Use **Set price → local prices** so Google converts for every country.

## Server-side verification setup

1. Google Cloud console → create a **service account** (no roles needed) → *Keys → Add key → JSON*.
2. Play Console → **Users and permissions → Invite new users** → paste the service-account email →
   App permissions: your app → tick **View financial data** and **Manage orders and subscriptions**.
3. In Vercel set `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` to the whole JSON file contents and `ANDROID_PACKAGE_NAME` to your package.
   Never commit the JSON file.
4. Flow: client buys → `POST /api/store/iap/google` → backend calls Google's `purchases.products.get`, credits
   Diamonds once per purchase token (unique in the `purchases` table) → client confirms/consumes via Unity IAP.

## Testing purchases

Play Console → **Settings → License testing** → add tester Gmail accounts. They can buy with test cards and are never charged.

## Google Play Billing Library version

Google requires a recent Play Billing Library for every new app and update (each version is supported for about two years).
Install the **latest** Unity In-App Purchasing package from the Package Manager before release; it bundles the Billing
Library. If Play Console shows a "Billing Library version" warning, update the package.
