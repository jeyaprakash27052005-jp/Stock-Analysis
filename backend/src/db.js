import pg from "pg";
import { config } from "./config.js";

// Return DATE columns as "YYYY-MM-DD" strings (no timezone shifts).
pg.types.setTypeParser(1082, (v) => v);

// Serverless-friendly pool: Supabase's transaction pooler (port 6543) does the real pooling.
export const pool = config.databaseUrl
  ? new pg.Pool({
      connectionString: config.databaseUrl,
      max: 3,
      idleTimeoutMillis: 10_000,
      ssl: config.databaseUrl.includes("localhost") ? false : { rejectUnauthorized: false },
    })
  : null;

export class HttpError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

function requirePool() {
  if (!pool) throw new HttpError(503, "Database not connected. Set DATABASE_URL (Supabase transaction pooler string).");
  return pool;
}

export async function query(text, params = []) {
  return requirePool().query(text, params);
}

export async function one(text, params = []) {
  return (await query(text, params)).rows[0] ?? null;
}

/** Runs fn(client) inside a transaction. */
export async function tx(fn) {
  const client = await requirePool().connect();
  try {
    await client.query("begin");
    const result = await fn(client);
    await client.query("commit");
    return result;
  } catch (e) {
    await client.query("rollback").catch(() => {});
    throw e;
  } finally {
    client.release();
  }
}
