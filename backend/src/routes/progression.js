import crypto from "node:crypto";
import { Router } from "express";
import { HttpError, one, query, tx } from "../db.js";
import { requirePlayer } from "../lib/auth.js";
import { addPassXp, addXp, changeCurrency, currentSeason, grantItem } from "../lib/economy.js";
import { dailyKey, periodKey } from "../lib/periods.js";

const r = Router();

/** Public: events calendar and spin odds (odds must be visible before paying). */
r.get("/events", async (_req, res) => {
  const events = (await query("select * from game_events where ends_at > now() order by starts_at")).rows;
  res.json({ events });
});

r.get("/spin/:eventId/odds", async (req, res) => {
  const rows = (await query(
    `select sr.item_id, i.name, i.rarity, sr.quantity, sr.weight,
            round(100.0 * sr.weight / sum(sr.weight) over (), 2) as percent
     from spin_rewards sr join items i on i.id = sr.item_id where sr.event_id=$1 order by sr.weight`, [req.params.eventId])).rows;
  res.json({ odds: rows });
});

r.use(requirePlayer);

// ------------------------------------------------------------ missions
r.get("/missions", async (req, res) => {
  const rows = (await query(
    `select d.*, coalesce(pm.progress,0) as progress, pm.claimed_at
     from mission_defs d
     left join player_missions pm on pm.mission_id = d.id and pm.player_id = $1
       and pm.period_key = case d.period when 'daily' then $2 else $3 end
     where d.active order by d.period, d.id`,
    [req.playerId, periodKey("daily"), periodKey("weekly")])).rows;
  res.json({ missions: rows.map((m) => ({ ...m, complete: m.progress >= m.target })) });
});

r.post("/missions/:id/claim", async (req, res) => {
  const out = await tx(async (c) => {
    const d = (await c.query("select * from mission_defs where id=$1 and active", [req.params.id])).rows[0];
    if (!d) throw new HttpError(404, "Mission not found");
    const key = periodKey(d.period);
    const m = await c.query(
      `update player_missions set claimed_at = now()
       where player_id=$1 and mission_id=$2 and period_key=$3 and claimed_at is null and progress >= $4 returning 1`,
      [req.playerId, d.id, key, d.target]);
    if (!m.rowCount) throw new HttpError(400, "Mission not complete or already claimed");
    if (d.reward_gold) await changeCurrency(c, req.playerId, "gold", d.reward_gold, "mission", d.id);
    if (d.reward_diamonds) await changeCurrency(c, req.playerId, "diamonds", d.reward_diamonds, "mission", d.id);
    if (d.reward_pass_xp) await addPassXp(c, req.playerId, d.reward_pass_xp);
    const lvl = d.reward_xp ? await addXp(c, req.playerId, d.reward_xp) : null;
    return { mission: d.id, levelUp: lvl?.levelsGained || [] };
  });
  res.json({ ok: true, ...out });
});

r.get("/achievements", async (req, res) => {
  const rows = (await query(
    `select d.*, coalesce(pa.progress,0) as progress, pa.completed_at, pa.claimed_at
     from achievement_defs d left join player_achievements pa on pa.achievement_id=d.id and pa.player_id=$1
     order by d.id`, [req.playerId])).rows;
  res.json({ achievements: rows });
});

r.post("/achievements/:id/claim", async (req, res) => {
  await tx(async (c) => {
    const d = (await c.query("select * from achievement_defs where id=$1", [req.params.id])).rows[0];
    if (!d) throw new HttpError(404, "Achievement not found");
    const u = await c.query(
      `update player_achievements set claimed_at=now() where player_id=$1 and achievement_id=$2
       and completed_at is not null and claimed_at is null returning 1`, [req.playerId, d.id]);
    if (!u.rowCount) throw new HttpError(400, "Not completed or already claimed");
    if (d.reward_gold) await changeCurrency(c, req.playerId, "gold", d.reward_gold, "achievement", d.id);
    if (d.reward_diamonds) await changeCurrency(c, req.playerId, "diamonds", d.reward_diamonds, "achievement", d.id);
  });
  res.json({ ok: true });
});

// ------------------------------------------------------------ Storm Pass
r.get("/pass", async (req, res) => {
  const s = await one("select * from pass_seasons where now() between starts_at and ends_at order by starts_at desc limit 1");
  if (!s) return res.json({ season: null });
  const mine = await one("select * from player_pass where player_id=$1 and season_id=$2", [req.playerId, s.id]);
  const rewards = (await query(
    `select pr.level, pr.track, pr.item_id, pr.quantity, i.name, i.type, i.rarity
     from pass_rewards pr join items i on i.id = pr.item_id where season_id=$1 order by level, track`, [s.id])).rows;
  const passXp = mine?.pass_xp || 0;
  res.json({ season: s, passXp, level: Math.min(s.max_level, Math.floor(passXp / s.xp_per_level)),
             premium: !!mine?.premium, claimed: mine?.claimed || [], rewards });
});

r.post("/pass/premium", async (req, res) => {
  await tx(async (c) => {
    const s = await currentSeason(c);
    if (!s) throw new HttpError(400, "No active season");
    const cur = (await c.query("select premium from player_pass where player_id=$1 and season_id=$2", [req.playerId, s.id])).rows[0];
    if (cur?.premium) throw new HttpError(409, "You already have Storm Pass Premium");
    await changeCurrency(c, req.playerId, "diamonds", -s.premium_price_diamonds, "pass_premium", s.id);
    await c.query(`insert into player_pass (player_id, season_id, premium) values ($1,$2,true)
                   on conflict (player_id, season_id) do update set premium=true`, [req.playerId, s.id]);
  });
  res.json({ ok: true });
});

r.post("/pass/claim", async (req, res) => {
  const level = Number(req.body.level), track = req.body.track;
  const out = await tx(async (c) => {
    const s = await currentSeason(c);
    if (!s) throw new HttpError(400, "No active season");
    const p = (await c.query("select * from player_pass where player_id=$1 and season_id=$2 for update", [req.playerId, s.id])).rows[0];
    if (!p || Math.floor(p.pass_xp / s.xp_per_level) < level) throw new HttpError(400, "Level not reached yet");
    if (track === "premium" && !p.premium) throw new HttpError(403, "Premium track is locked");
    const key = `${level}:${track}`;
    if (p.claimed.includes(key)) throw new HttpError(409, "Already claimed");
    const reward = (await c.query("select * from pass_rewards where season_id=$1 and level=$2 and track=$3", [s.id, level, track])).rows[0];
    if (!reward) throw new HttpError(404, "No reward at this level");
    await grantItem(c, req.playerId, reward.item_id, reward.quantity, "pass");
    await c.query("update player_pass set claimed = claimed || $3::jsonb where player_id=$1 and season_id=$2",
      [req.playerId, s.id, JSON.stringify([key])]);
    return { itemId: reward.item_id, quantity: reward.quantity };
  });
  res.json({ ok: true, ...out });
});

// ------------------------------------------------------------ Fortune Drop (lucky spin)
r.post("/spin/:eventId", async (req, res) => {
  const count = Number(req.body.count) === 10 ? 10 : 1;
  const out = await tx(async (c) => {
    const ev = (await c.query("select * from spin_events where id=$1 and active and now() between starts_at and ends_at", [req.params.eventId])).rows[0];
    if (!ev) throw new HttpError(404, "Event not running");
    const pool = (await c.query("select * from spin_rewards where event_id=$1", [ev.id])).rows;
    const total = pool.reduce((s, p) => s + p.weight, 0);
    await changeCurrency(c, req.playerId, "diamonds", -(count === 10 ? ev.cost_ten : ev.cost_single), "spin", ev.id);
    const wins = [];
    for (let i = 0; i < count; i++) {
      let roll = crypto.randomInt(0, total);           // server-side RNG only
      const pick = pool.find((p) => (roll -= p.weight) < 0);
      // Already-owned cosmetics are converted to gold so a spin is never wasted
      const owned = (await c.query(
        "select 1 from player_items pi join items i on i.id=pi.item_id where pi.player_id=$1 and pi.item_id=$2 and i.type not in ('consumable')",
        [req.playerId, pick.item_id])).rowCount;
      if (owned) {
        await changeCurrency(c, req.playerId, "gold", 1000, "spin_duplicate", pick.item_id);
        wins.push({ itemId: pick.item_id, duplicate: true, convertedGold: 1000 });
      } else {
        await grantItem(c, req.playerId, pick.item_id, pick.quantity, "spin");
        wins.push({ itemId: pick.item_id, quantity: pick.quantity });
      }
      await c.query("insert into spin_history (player_id, event_id, item_id, quantity) values ($1,$2,$3,$4)",
        [req.playerId, ev.id, pick.item_id, pick.quantity]);
    }
    return { wins };
  });
  res.json({ ok: true, ...out });
});

// ------------------------------------------------------------ 7-day login rewards
r.get("/login-rewards", async (req, res) => {
  const defs = (await query("select l.*, i.name from login_reward_defs l join items i on i.id=l.item_id order by day")).rows;
  const me = await one("select * from player_login_rewards where player_id=$1", [req.playerId]);
  const today = dailyKey();
  const claimedToday = me?.last_claim_date && String(me.last_claim_date).slice(0, 10) === today;
  res.json({ rewards: defs, streakDay: me?.streak_day || 0, canClaim: !claimedToday });
});

r.post("/login-rewards/claim", async (req, res) => {
  const out = await tx(async (c) => {
    const me = (await c.query("select * from player_login_rewards where player_id=$1 for update", [req.playerId])).rows[0];
    const today = dailyKey();
    const last = me?.last_claim_date ? String(me.last_claim_date).slice(0, 10) : null;
    if (last === today) throw new HttpError(409, "Already claimed today");
    const yesterday = dailyKey(new Date(Date.now() - 86400000));
    const day = last === yesterday ? (me.streak_day % 7) + 1 : 1;
    const def = (await c.query("select * from login_reward_defs where day=$1", [day])).rows[0];
    await grantItem(c, req.playerId, def.item_id, def.quantity, "login_reward");
    await c.query(`insert into player_login_rewards (player_id, streak_day, last_claim_date) values ($1,$2,$3)
                   on conflict (player_id) do update set streak_day=$2, last_claim_date=$3`, [req.playerId, day, today]);
    return { day, itemId: def.item_id, quantity: def.quantity };
  });
  res.json({ ok: true, ...out });
});

export default r;
