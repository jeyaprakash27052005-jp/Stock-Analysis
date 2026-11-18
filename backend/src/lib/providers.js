// Verifies third-party login tokens and Google Play purchases on the server.
import { GoogleAuth, OAuth2Client } from "google-auth-library";
import { config } from "../config.js";
import { HttpError } from "../db.js";

/** Google: accepts an ID token (Credential Manager / Sign-In) or a Play Games server auth code. */
export async function verifyGoogle({ idToken, serverAuthCode }) {
  if (!config.google.clientId) throw new HttpError(503, "Google login is not configured on the server.");
  const client = new OAuth2Client(config.google.clientId, config.google.clientSecret);
  if (idToken) {
    const ticket = await client.verifyIdToken({ idToken, audience: config.google.clientId })
      .catch(() => { throw new HttpError(401, "Invalid Google token"); });
    const p = ticket.getPayload();
    return { uid: p.sub, name: p.given_name || p.name || null };
  }
  if (serverAuthCode) {
    // Play Games Services v2: exchange the one-time code, then read the player's stable id.
    const { tokens } = await client.getToken({ code: serverAuthCode, redirect_uri: "" })
      .catch(() => { throw new HttpError(401, "Invalid Play Games auth code"); });
    const r = await fetch("https://games.googleapis.com/games/v1/players/me", {
      headers: { Authorization: `Bearer ${tokens.access_token}` } });
    if (!r.ok) throw new HttpError(401, "Could not read Play Games profile");
    const me = await r.json();
    return { uid: `pgs_${me.playerId}`, name: me.displayName || null };
  }
  throw new HttpError(400, "idToken or serverAuthCode required");
}

export async function verifyFacebook({ accessToken }) {
  const { appId, appSecret } = config.facebook;
  if (!appId || !appSecret) throw new HttpError(503, "Facebook login is not configured on the server.");
  if (!accessToken) throw new HttpError(400, "accessToken required");
  const u = new URL("https://graph.facebook.com/debug_token");
  u.searchParams.set("input_token", accessToken);
  u.searchParams.set("access_token", `${appId}|${appSecret}`);
  const d = (await (await fetch(u)).json()).data;
  if (!d?.is_valid || d.app_id !== appId) throw new HttpError(401, "Invalid Facebook token");
  const me = await (await fetch(`https://graph.facebook.com/me?fields=id,first_name&access_token=${encodeURIComponent(accessToken)}`)).json();
  return { uid: d.user_id, name: me.first_name || null };
}

/** Google Play Developer API: products.get for a one-time in-app product. */
export async function verifyGooglePlayPurchase(productId, purchaseToken) {
  if (!config.playServiceAccount) throw new HttpError(503, "Purchase verification is not configured on the server.");
  const auth = new GoogleAuth({
    credentials: JSON.parse(config.playServiceAccount),
    scopes: ["https://www.googleapis.com/auth/androidpublisher"],
  });
  const client = await auth.getClient();
  const base = `https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${config.androidPackage}` +
               `/purchases/products/${encodeURIComponent(productId)}/tokens/${encodeURIComponent(purchaseToken)}`;
  const { data } = await client.request({ url: base }).catch(() => { throw new HttpError(400, "Purchase not found"); });
  if (data.purchaseState !== 0) throw new HttpError(400, "Purchase is not completed");
  // purchaseState 0 = purchased. The game client consumes it after we credit (see routes/store.js).
  return { orderId: data.orderId, raw: data };
}
