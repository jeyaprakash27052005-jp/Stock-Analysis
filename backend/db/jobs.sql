-- Daily scheduled jobs inside Supabase (pg_cron), so they run even if the API host is down.
-- Supabase: Database → Extensions → pg_cron is enabled by this script. Safe to re-run.
create extension if not exists pg_cron;

-- 21:15 UTC (02:45 IST) every day: delete accounts unused for 30 days, and accounts deleted in the app
select cron.schedule('stormdrop-purge-inactive', '15 21 * * *', $$select public.purge_inactive_players(30)$$);
