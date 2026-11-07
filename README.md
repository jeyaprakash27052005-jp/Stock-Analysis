# Stock Analyzer (Vercel + Supabase)

Full NSE stock analysis website: market overview, automatic watchlist scanner,
technical + fundamental deep analysis, signal history with performance tracking,
alerts, and your own price database in Supabase. Works on phone and PC.

**For analysis and learning only. Not investment advice. It never places trades.**

## Features

| Tab | What you get |
|---|---|
| 🌐 Market | Nifty 50, Sensex, Bank Nifty, India VIX, 1-year Nifty chart, sector performance, breadth, top gainers/losers |
| 🔎 Scanner | Watchlist scored −100…+100 with signal, RSI, stop loss and target |
| 🧠 Analysis | Any NSE stock: overall rating /100, candlestick chart with EMA 20/50/200, Bollinger, volume, MACD, RSI; fundamentals health check, key ratios, yearly revenue/profit, returns and risk |
| 📜 Signals | Every signal change saved, with result since the call, target/stop hit |
| ⭐ Watchlist & Alerts | Add/remove stocks; price/RSI/strong-signal alerts (+ optional Telegram) |
| 🗄️ Database | Daily prices saved in Supabase, downloadable as CSV |

A daily scheduled job (Vercel Cron, 3:45 PM IST, Mon–Fri) scans your watchlist after
market close, so prices, signals and alerts are saved even if you don't open the site.

Data: Yahoo Finance (real NSE prices, about 15 minutes delayed) — no API key needed.

## Deploy on Vercel

1. Go to **vercel.com**, sign in with GitHub, click **Add New → Project** and import this repository.
2. Framework preset: **Other**. Leave build settings empty.
3. Open **Environment Variables** and add:

   | Name | Value |
   |---|---|
   | `DATABASE_URL` | Supabase → project **Stock Analyzer** → **Connect** → **Transaction pooler** string, with your database password |
   | `APP_PASSWORD` | a password to protect your site |
   | `DATA_MODE` | `yahoo` |
   | `CRON_SECRET` | any long random text (lets the daily job run) |
   | `TELEGRAM_BOT_TOKEN`, `TELEGRAM_CHAT_ID` | optional, for phone alerts |

4. Click **Deploy**. Open your `*.vercel.app` link on your phone and use **Add to Home Screen**.

Every `git push` to this repository redeploys automatically.

## Run on your PC

1. Install Python 3.12 (python.org, tick "Add python.exe to PATH").
2. Copy `.env.example` to `.env` and fill in your values.
3. Double-click **START_LOCAL.bat** (or `pip install -r requirements-local.txt` then `python run_local.py`).
4. Open http://127.0.0.1:8000

## Project structure

```
public/index.html     website (mobile-first, Plotly charts), served by Vercel CDN
app.py                Python API (FastAPI) – Vercel entrypoint
core/                 analysis engine
  indicators.py       EMA, RSI, MACD, Bollinger, ATR, ADX, Supertrend
  signals.py          technical score and stop/target
  fundamentals.py     fundamental score and overall rating
  data_source.py      Yahoo Finance / demo data
  database.py         Supabase (Postgres) or local SQLite
  engine.py           scan, save, alerts, signal performance
vercel.json           routing, timeout, daily cron
```

## Scoring

- **Technical (−100…+100):** trend (200 EMA, 20/50 EMA cross, Supertrend), momentum (RSI, MACD),
  breakout/volatility (Bollinger, volume spike, 52-week high/low), scaled down when ADX < 20.
  Stop loss = 2×ATR, target = 3×ATR.
- **Fundamental (0…100):** valuation (P/E, P/B), profitability (ROE, margin), growth (revenue, profit),
  health (debt/equity, current ratio, dividend).
- **Overall:** 50/50 blend. ≥70 Strong Buy · ≥58 Buy · 42–57 Hold · ≥30 Sell · <30 Strong Sell.

## Security

- Never commit `.env` or your database password (`.gitignore` already excludes it).
- Supabase tables have Row Level Security on with no public policies, so only the
  server (using your database password) can read or write them.
