import { HttpError } from "../db.js";
import { xpForNextLevel } from "./ranks.js";

/** Adds (or with negative delta, spends) gold/diamonds and writes the ledger. Throws if not enough. */
export async function changeCurrency(c, playerId, currency, delta, reason, ref = null) {
  if (!["gold", "diamonds"].includes(currency)) throw new Error("bad currency");
  if (delta === 0) return;
  const r = await c.query(
    `update players set ${currency} = ${currency} + $2 where id = $1 and ${currency} + $2 >= 0 returning ${currency}`,
    [playerId, delta]);
  if (!r.rowCount) throw new HttpError(400, `Not enough ${currency === "gold" ? "Gold" : "Diamonds"}`);
  await c.query("insert into currency_ledger (player_id, currency, delta, reason, ref) values ($1,$2,$3,$4,$5)",
    [playerId, currency, delta, reason, ref]);
  return Number(r.rows[0][currency]);
}

/** Grants any item: currency items add balance, pass XP adds to the active pass, others go to the vault. */
export async function grantItem(c, playerId, itemId, quantity, reason) {
  const item = (await c.query("select id, type, metadata from items where id = $1", [itemId])).rows[0];
  if (!item) throw new Error(`Unknown item ${itemId}`);
  if (item.type === "currency") {
    await changeCurrency(c, playerId, item.metadata.currency, item.metadata.amount * quantity, reason, itemId);
  } else if (item.type === "pass_xp") {
    await addPassXp(c, playerId, item.metadata.amount * quantity);
  } else {
    const stackable = item.type === "consumable";
    await c.query(
      `insert into player_items (player_id, item_id, quantity) values ($1,$2,$3)
       on conflict (player_id, item_id) do update
         set quantity = case when $4 then player_items.quantity + excluded.quantity else 1 end`,
      [playerId, itemId, stackable ? quantity : 1, stackable]);
  }
  return { itemId, type: item.type, quantity };
}

export async function addXp(c, playerId, xp) {
  const p = (await c.query("select level, xp from players where id = $1 for update", [playerId])).rows[0];
  let { level, xp: cur } = p;
  cur += xp;
  const levelsGained = [];
  while (level < 100 && cur >= xpForNextLevel(level)) {
    cur -= xpForNextLevel(level);
    level++;
    levelsGained.push(level);
  }
  await c.query("update players set level = $2, xp = $3 where id = $1", [playerId, level, cur]);
  // Level-up reward: 200 gold per level, 10 diamonds every 5 levels
  for (const l of levelsGained) {
    await changeCurrency(c, playerId, "gold", 200, "level_up", String(l));
    if (l % 5 === 0) await changeCurrency(c, playerId, "diamonds", 10, "level_up", String(l));
  }
  return { level, xp: cur, levelsGained };
}

export async function currentSeason(c) {
  return (await c.query("select * from pass_seasons where now() between starts_at and ends_at order by starts_at desc limit 1")).rows[0] || null;
}

export async function addPassXp(c, playerId, amount) {
  const s = await currentSeason(c);
  if (!s || amount <= 0) return;
  await c.query(
    `insert into player_pass (player_id, season_id, pass_xp) values ($1,$2,$3)
     on conflict (player_id, season_id) do update set pass_xp = least(player_pass.pass_xp + excluded.pass_xp, $4)`,
    [playerId, s.id, amount, s.xp_per_level * s.max_level]);
}
