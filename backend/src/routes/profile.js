import { Router } from "express";
import { config } from "../config.js";
import { HttpError, one, query, tx } from "../db.js";
import { requirePlayer } from "../lib/auth.js";
import { changeCurrency } from "../lib/economy.js";
import { rankInfo, xpForNextLevel } from "../lib/ranks.js";

const r = Router();

export async function getProfile(playerId) {
  const p = await one(
    `select p.id, p.player_tag, p.nickname, p.avatar_id, p.banner_id, p.region, p.level, p.xp, p.gold, p.diamonds,
            p.br_rank_points, p.cs_rank_points, p.created_at, g.id as guild_id, g.name as guild_name, g.tag as guild_tag,
            l.character_id, l.pet_id, l.parachute_id, l.outfit, l.weapon_skins, l.emotes,
            coalesce((select array_agg(provider) from auth_identities where player_id = p.id), '{}') as linked
     from players p left join loadouts l on l.player_id = p.id left join guilds g on g.id = p.guild_id
     where p.id = $1`, [playerId]);
  if (!p) throw new HttpError(404, "Player not found");
  const stats = (await query("select * from player_stats where player_id=$1", [playerId])).rows;
  const settings = await one("select data from player_settings where player_id=$1", [playerId]);
  return {
    ...p, gold: Number(p.gold), diamonds: Number(p.diamonds),
    xpToNext: xpForNextLevel(p.level),
    brRank: rankInfo(p.br_rank_points), csRank: rankInfo(p.cs_rank_points),
    stats, settings: settings?.data || {},
  };
}

r.use(requirePlayer);

r.get("/", async (req, res) => res.json(await getProfile(req.playerId)));

/** View another player's public card by tag. */
r.get("/player/:tag", async (req, res) => {
  const p = await one(
    `select p.player_tag, p.nickname, p.avatar_id, p.banner_id, p.level, p.br_rank_points, g.tag as guild_tag
     from players p left join guilds g on g.id = p.guild_id where p.player_tag=$1 and p.deleted_at is null`, [req.params.tag]);
  if (!p) throw new HttpError(404, "No player with that ID");
  res.json({ ...p, brRank: rankInfo(p.br_rank_points) });
});

r.patch("/", async (req, res) => {
  const { avatarId, bannerId, region } = req.body;
  await tx(async (c) => {
    for (const [col, id] of [["avatar_id", avatarId], ["banner_id", bannerId]]) {
      if (!id) continue;
      const owned = await c.query("select 1 from player_items where player_id=$1 and item_id=$2", [req.playerId, id]);
      if (!owned.rowCount) throw new HttpError(400, "You don't own that item");
      await c.query(`update players set ${col}=$2 where id=$1`, [req.playerId, id]);
    }
    if (region) {
      if (!config.regions.includes(region)) throw new HttpError(400, "Unknown region");
      await c.query("update players set region=$2 where id=$1", [req.playerId, region]);
    }
  });
  res.json(await getProfile(req.playerId));
});

/** Rename: free with a Name Change Card, otherwise costs diamonds. */
r.post("/rename", async (req, res) => {
  const name = String(req.body.nickname || "").trim();
  if (!/^[\p{L}\p{N}_ .-]{3,16}$/u.test(name)) throw new HttpError(400, "Name must be 3–16 letters, numbers, _ . -");
  await tx(async (c) => {
    const card = await c.query(
      "update player_items set quantity = quantity - 1 where player_id=$1 and item_id='card_name_change' and quantity > 0 returning 1",
      [req.playerId]);
    if (!card.rowCount) await changeCurrency(c, req.playerId, "diamonds", -config.renameCostDiamonds, "rename");
    await c.query("update players set nickname=$2 where id=$1", [req.playerId, name]);
  });
  res.json(await getProfile(req.playerId));
});

/** HUD layout, sensitivity, graphics preset — synced so they follow the player to a new phone. */
r.put("/settings", async (req, res) => {
  const data = req.body && typeof req.body === "object" ? req.body : {};
  if (JSON.stringify(data).length > 20000) throw new HttpError(413, "Settings too large");
  await query(`insert into player_settings (player_id, data, updated_at) values ($1,$2,now())
               on conflict (player_id) do update set data=excluded.data, updated_at=now()`, [req.playerId, data]);
  res.json({ ok: true });
});

r.put("/loadout", async (req, res) => {
  const { characterId, petId, parachuteId, outfit, weaponSkins, emotes } = req.body;
  await tx(async (c) => {
    const owned = async (id, type) => {
      if (!id) return;
      const ok = await c.query("select 1 from player_items pi join items i on i.id = pi.item_id where pi.player_id=$1 and pi.item_id=$2 and i.type=$3",
        [req.playerId, id, type]);
      if (!ok.rowCount) throw new HttpError(400, `You don't own ${id}`);
    };
    await owned(characterId, "character");
    await owned(petId, "pet");
    await owned(parachuteId, "parachute");
    for (const id of Object.values(outfit || {})) await owned(id, "outfit");
    for (const id of Object.values(weaponSkins || {})) await owned(id, "weapon_skin");
    for (const id of emotes || []) await owned(id, "emote");
    await c.query(
      `update loadouts set character_id = coalesce($2, character_id), pet_id = $3, parachute_id = $4,
         outfit = coalesce($5, outfit), weapon_skins = coalesce($6, weapon_skins), emotes = coalesce($7, emotes), updated_at = now()
       where player_id = $1`,
      [req.playerId, characterId || null, petId || null, parachuteId || null,
       outfit ? JSON.stringify(outfit) : null, weaponSkins ? JSON.stringify(weaponSkins) : null,
       emotes ? JSON.stringify(emotes.slice(0, 8)) : null]);
  });
  res.json(await getProfile(req.playerId));
});

r.post("/push-token", async (req, res) => {
  const token = String(req.body.token || "");
  if (!token) throw new HttpError(400, "token required");
  await query(`insert into push_tokens (token, player_id, platform, updated_at) values ($1,$2,$3,now())
               on conflict (token) do update set player_id=excluded.player_id, updated_at=now()`,
    [token, req.playerId, req.body.platform === "ios" ? "ios" : "android"]);
  res.json({ ok: true });
});

/** In-app account deletion (required by Google Play). Personal data is erased immediately. */
r.delete("/", async (req, res) => {
  await tx(async (c) => {
    const id = req.playerId;
    // Hand guild leadership to the longest-serving member, or delete an empty guild.
    const led = (await c.query("select id from guilds where leader_id=$1", [id])).rows;
    for (const g of led) {
      const next = (await c.query("select player_id from guild_members where guild_id=$1 and player_id<>$2 order by joined_at limit 1", [g.id, id])).rows[0];
      if (next) {
        await c.query("update guilds set leader_id=$2 where id=$1", [g.id, next.player_id]);
        await c.query("update guild_members set role='leader' where player_id=$1", [next.player_id]);
      } else {
        await c.query("update players set guild_id=null where guild_id=$1", [g.id]);
        await c.query("delete from guilds where id=$1", [g.id]);
      }
    }
    await c.query("delete from auth_identities where player_id=$1", [id]);
    await c.query("delete from push_tokens where player_id=$1", [id]);
    await c.query("delete from friendships where player_id=$1 or friend_id=$1", [id]);
    await c.query("delete from guild_members where player_id=$1", [id]);
    await c.query("delete from party_members where player_id=$1", [id]);
    await c.query("delete from chat_messages where player_id=$1", [id]);
    await c.query("delete from player_settings where player_id=$1", [id]);
    // Keep an anonymised row so match history of other players stays consistent.
    await c.query(`update players set nickname='Deleted', guild_id=null, deleted_at=now(), gold=0, diamonds=0 where id=$1`, [id]);
  });
  res.json({ ok: true });
});

export default r;
