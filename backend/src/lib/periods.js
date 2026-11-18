// Reset keys for daily (UTC date) and weekly (ISO week) missions.
export function dailyKey(d = new Date()) {
  return d.toISOString().slice(0, 10);
}

export function weeklyKey(d = new Date()) {
  const t = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()));
  const day = t.getUTCDay() || 7;
  t.setUTCDate(t.getUTCDate() + 4 - day);
  const yearStart = new Date(Date.UTC(t.getUTCFullYear(), 0, 1));
  const week = Math.ceil(((t - yearStart) / 86400000 + 1) / 7);
  return `${t.getUTCFullYear()}-W${String(week).padStart(2, "0")}`;
}

export const periodKey = (period, d) => (period === "weekly" ? weeklyKey(d) : dailyKey(d));
