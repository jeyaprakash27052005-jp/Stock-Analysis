import { Router } from "express";
import { config } from "../config.js";
import { HttpError, query, tx } from "../db.js";
import { requirePlayer } from "../lib/auth.js";
import { changeCurrency, grantItem } from "../lib/economy.js";
import { verifyGooglePlayPurchase } from "../lib/providers.js";

const r = Router();

/** Public catalog (no login needed) — the game caches it. */
r.get("/catalog", async (_req, res) => {
  const items = (await query(
    `select id, type, name, rarity, description, price_gold, price_diamonds, in_store, metadata
     from items order by sort_order, id`)).rows;
  res.set("Cache-Control", "public, max-age=300");
  res.json({ items, diamondPacks: config.diamondPacks });
});

r.use(requirePlayer);

r.get("/inventory", async (req, res) => {
  const rows = (await query(
    `select pi.item_id, pi.quantity, pi.acquired_at, pi.expires_at, i.type, i.name, i.rarity, i.metadata
     from player_items pi join items i on i.id = pi.item_id
     where pi.player_id = $1 and pi.quantity > 0 and (pi.expires_at is null or pi.expires_at > now())
     order by i.type, i.sort_order`, [req.playerId])).rows;
  res.json({ items: rows });
});

r.post("/buy", async (req, res) => {
  const { itemId, currency } = req.body;
  if (!["gold", "diamonds"].includes(currency)) throw new HttpError(400, "currency must be gold or diamonds");
  const result = await tx(async (c) => {
    const item = (await c.query("select * from items where id=$1 and in_store", [itemId])).rows[0];
    if (!item) throw new HttpError(404, "Item not in store");
    const price = currency === "gold" ? item.price_gold : item.price_diamonds;
    if (price == null) throw new HttpError(400, `This item can't be bought with ${currency}`);
    if (item.type !== "consumable") {
      const owned = await c.query("select 1 from player_items where player_id=$1 and item_id=$2 and quantity>0", [req.playerId, itemId]);
      if (owned.rowCount) throw new HttpError(409, "You already own this");
    }
    const balance = await changeCurrency(c, req.playerId, currency, -price, "store", itemId);
    await grantItem(c, req.playerId, itemId, 1, "store");
    return { itemId, currency, balance };
  });
  res.json({ ok: true, ...result });
});

/**
 * Diamond top-up via Google Play Billing. The client sends the purchase token after
 * a successful purchase; we verify with Google and credit once (token is unique). The client then
 * confirms/consumes it through Unity IAP — if the app crashes first, Unity IAP re-delivers the
 * purchase on next launch, we answer "duplicate" and the client confirms it.
 */
r.post("/iap/google", async (req, res) => {
  const { productId, purchaseToken } = req.body;
  const diamonds = config.diamondPacks[productId];
  if (!diamonds) throw new HttpError(400, "Unknown product");
  if (!purchaseToken) throw new HttpError(400, "purchaseToken required");
  const v = await verifyGooglePlayPurchase(productId, purchaseToken);
  const out = await tx(async (c) => {
    const ins = await c.query(
      `insert into purchases (player_id, platform, product_id, order_id, purchase_token, diamonds, raw)
       values ($1,'google_play',$2,$3,$4,$5,$6) on conflict (purchase_token) do nothing returning id`,
      [req.playerId, productId, v.orderId, purchaseToken, diamonds, v.raw]);
    if (!ins.rowCount) return { duplicate: true };
    const balance = await changeCurrency(c, req.playerId, "diamonds", diamonds, "iap", v.orderId);
    return { duplicate: false, balance };
  });
  res.json({ ok: true, diamonds, ...out });
});

export default r;
