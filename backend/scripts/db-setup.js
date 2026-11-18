// Creates all tables and loads starter content into your Supabase database.
// Usage: put DATABASE_URL in backend/.env, then `npm run db:setup`.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import pg from "pg";

const url = process.env.DATABASE_URL;
if (!url) { console.error("DATABASE_URL is not set (see .env.example)"); process.exit(1); }
const dir = path.join(path.dirname(fileURLToPath(import.meta.url)), "..", "db");
const client = new pg.Client({ connectionString: url, ssl: url.includes("localhost") ? false : { rejectUnauthorized: false } });
await client.connect();
for (const file of ["schema.sql", "seed.sql"]) {
  process.stdout.write(`Running ${file} … `);
  await client.query(fs.readFileSync(path.join(dir, file), "utf8"));
  console.log("done");
}
try {
  await client.query(fs.readFileSync(path.join(dir, "jobs.sql"), "utf8"));
  console.log("Scheduled daily jobs (pg_cron)");
} catch (e) {
  console.log("pg_cron not available, daily jobs run via Vercel Cron instead:", e.message);
}
const { rows } = await client.query("select type, count(*)::int n from items group by type order by type");
console.table(rows);
await client.end();
