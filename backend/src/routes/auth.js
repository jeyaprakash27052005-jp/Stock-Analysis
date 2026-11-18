import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query, tx } from "../db.js";
import { changeCurrency } from "../lib/economy.js";
import { randomPlayerTag, requirePlayer, signSession } from "../lib/auth.js";
import { verifyFacebook, verifyGoogle } from "../lib/providers.js";
import { getProfile } from "./profile.js";

const r = Router();

async function createPlayer(c, nickname, region) {
  for (let attempt = 0; attempt < 5; attempt++) {
    const tag = randomPlayerTag();
    const res = await c.query(
      `insert into players (player_tag, nickname, region) values ($1,$2,$3)
       on conflict (player_tag) do nothing returning id`, [tag, nickname, region]);
    if (res.rowCount) {
      const id = res.rows[0].id;
      await c.query("insert into loadouts (player_id) values ($1)", [id]);
      await c.query("insert into player_items (player_id, item_id) values ($1,'char_kael'),($1,'avatar_default'),($1,'banner_default')", [id]);
      await changeCurrency(c, id, "gold", config.startingGold, "welcome");
      await changeCurrency(c, id, "diamonds", config.startingDiamonds, "welcome");
      return id;
    }
  }
  throw new Error("Could not allocate player tag");
}

function cleanName(name) {
  const n = String(name || "").replace(/[^\p{L}\p{N}_ .-]/gu, "").trim().slice(0, 16);
  return n.length >= 3 ? n : `Dropper${Math.floor(Math.random() * 9000 + 1000)}`;
}

/** Find the player for (provider, uid) or create a new account. */
async function loginWith(provider, uid, name, region) {
  const playerId = await tx(async (c) => {
    const found = (await c.query(
      "select a.player_id from auth_identities a join players p on p.id = a.player_id where provider=$1 and provider_uid=$2 and p.deleted_at is null",
      [provider, uid])).rows[0];
    if (found) return found.player_id;
    const id = await createPlayer(c, cleanName(name), config.regions.includes(region) ? region : "IND");
    await c.query("insert into auth_identities (provider, provider_uid, player_id) values ($1,$2,$3)", [provider, uid, id]);
    return id;
  });
  const p = await one("update players set last_login_at = now(), last_active_at = now() where id=$1 returning is_banned, ban_reason", [playerId]);
  if (p.is_banned) throw new HttpError(403, `Account banned: ${p.ban_reason || "violation of terms"}`);
  return { token: signSession(playerId), profile: await getProfile(playerId) };
}

r.post("/guest", async (req, res) => {
  const deviceId = String(req.body.deviceId || "");
  if (deviceId.length < 16) throw new HttpError(400, "deviceId required");
  res.json(await loginWith("guest", deviceId, req.body.nickname, req.body.region));
});

r.post("/google", async (req, res) => {
  const g = await verifyGoogle(req.body);
  res.json(await loginWith("google", g.uid, g.name, req.body.region));
});

r.post("/facebook", async (req, res) => {
  const f = await verifyFacebook(req.body);
  res.json(await loginWith("facebook", f.uid, f.name, req.body.region));
});

/** Link Google/Facebook to the signed-in (usually guest) account so progress is never lost. */
r.post("/link", requirePlayer, async (req, res) => {
  const { provider } = req.body;
  const v = provider === "google" ? await verifyGoogle(req.body)
          : provider === "facebook" ? await verifyFacebook(req.body)
          : (() => { throw new HttpError(400, "provider must be google or facebook"); })();
  const existing = await one("select player_id from auth_identities where provider=$1 and provider_uid=$2", [provider, v.uid]);
  if (existing && existing.player_id !== req.playerId) throw new HttpError(409, "That account is already linked to another player");
  await query("insert into auth_identities (provider, provider_uid, player_id) values ($1,$2,$3) on conflict do nothing",
    [provider, v.uid, req.playerId]);
  res.json({ ok: true, profile: await getProfile(req.playerId) });
});

export default r;
