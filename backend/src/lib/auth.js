import crypto from "node:crypto";
import jwt from "jsonwebtoken";
import { config } from "../config.js";
import { HttpError, one } from "../db.js";

function secret() {
  if (!config.jwtSecret) throw new HttpError(503, "JWT_SECRET is not set on the server.");
  return config.jwtSecret;
}

export function signSession(playerId) {
  return jwt.sign({ sub: playerId, typ: "session" }, secret(), { expiresIn: "30d" });
}

/** Short-lived ticket the game client presents to the dedicated server (verified there with SERVER_SECRET). */
export function signJoinTicket({ playerId, matchId, team, nickname, characterId, petId }) {
  if (!config.serverSecret) throw new HttpError(503, "SERVER_SECRET is not set on the server.");
  return jwt.sign({ sub: playerId, mid: matchId, team, nick: nickname, chr: characterId, pet: petId || "" },
    config.serverSecret, { expiresIn: "10m", algorithm: "HS256" });
}

/** Express middleware: requires `Authorization: Bearer <session token>`. Sets req.playerId. */
export async function requirePlayer(req, _res, next) {
  try {
    const token = (req.headers.authorization || "").replace(/^Bearer\s+/i, "");
    if (!token) throw new HttpError(401, "Not signed in");
    let payload;
    try { payload = jwt.verify(token, secret()); } catch { throw new HttpError(401, "Session expired, sign in again"); }
    if (payload.typ !== "session") throw new HttpError(401, "Bad token");
    const p = await one("select id, is_banned, ban_reason, deleted_at from players where id = $1", [payload.sub]);
    if (!p || p.deleted_at) throw new HttpError(401, "Account not found");
    if (p.is_banned) throw new HttpError(403, `Account banned: ${p.ban_reason || "violation of terms"}`);
    req.playerId = p.id;
    next();
  } catch (e) { next(e); }
}

function safeEqual(a, b) {
  const x = Buffer.from(a || ""), y = Buffer.from(b || "");
  return x.length === y.length && crypto.timingSafeEqual(x, y);
}

/** Dedicated game servers authenticate with `X-Server-Secret`. */
export function requireServer(req, _res, next) {
  if (!config.serverSecret || !safeEqual(req.headers["x-server-secret"], config.serverSecret))
    return next(new HttpError(401, "Bad server secret"));
  next();
}

export function requireCron(req, _res, next) {
  if (!config.cronSecret || !safeEqual(req.headers.authorization, `Bearer ${config.cronSecret}`))
    return next(new HttpError(401, "Bad cron secret"));
  next();
}

export function randomPlayerTag() {
  // 10-digit public id, first digit non-zero
  return String(crypto.randomInt(1, 10)) + String(crypto.randomInt(0, 1e9)).padStart(9, "0");
}
