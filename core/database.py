"""
Database: works with a local file (SQLite) or an online Supabase/Postgres database.
Tables are created automatically on first run.
"""
from __future__ import annotations

import datetime as dt
import os

import pandas as pd
from sqlalchemy import (Column, Date, DateTime, Float, Integer, MetaData, String, Table,
                        UniqueConstraint, create_engine, delete, insert, select, update)
from sqlalchemy.pool import NullPool

import config

# On Vercel the only writable folder is /tmp, so a missing DATABASE_URL uses a temporary file.
_LOCAL = "/tmp/stock_data.db" if os.environ.get("VERCEL") else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "stock_data.db")
URL = config.DATABASE_URL or f"sqlite:///{os.path.abspath(_LOCAL)}"
if URL.startswith("postgres://"):
    URL = URL.replace("postgres://", "postgresql://", 1)

# Serverless: open a fresh connection per request through Supabase's pooler.
engine = (create_engine(URL, poolclass=NullPool) if URL.startswith("postgresql")
          else create_engine(URL))
meta = MetaData()

watchlist = Table(
    "watchlist", meta,
    Column("symbol", String(30), primary_key=True),
    Column("added_at", DateTime, default=dt.datetime.utcnow),
)

alerts = Table(
    "alerts", meta,
    Column("id", Integer, primary_key=True, autoincrement=True),
    Column("symbol", String(30), nullable=False),
    Column("kind", String(20), nullable=False),     # price_above, price_below, rsi_above, rsi_below, signal
    Column("value", Float),
    Column("active", Integer, default=1),
    Column("created_at", DateTime, default=dt.datetime.utcnow),
    Column("triggered_at", DateTime),
    Column("triggered_price", Float),
)

price_history = Table(
    "price_history", meta,
    Column("symbol", String(30), nullable=False),
    Column("date", Date, nullable=False),
    Column("open", Float), Column("high", Float), Column("low", Float),
    Column("close", Float), Column("volume", Float),
    UniqueConstraint("symbol", "date", name="uq_symbol_date"),
)

signal_history = Table(
    "signal_history", meta,
    Column("id", Integer, primary_key=True, autoincrement=True),
    Column("created_at", DateTime, default=dt.datetime.utcnow),
    Column("symbol", String(30), nullable=False),
    Column("signal", String(20)),
    Column("score", Integer),
    Column("fund_score", Integer),
    Column("price", Float),
    Column("stop_loss", Float),
    Column("target", Float),
)


_ready = False


def init(default_watchlist: list[str]) -> None:
    global _ready
    if _ready:
        return
    meta.create_all(engine)
    _ready = True
    with engine.begin() as c:
        if c.execute(select(watchlist.c.symbol).limit(1)).first() is None:
            c.execute(insert(watchlist), [{"symbol": s} for s in default_watchlist])


def db_label() -> str:
    return "Online (Postgres/Supabase)" if engine.dialect.name == "postgresql" else "Local file (SQLite)"


# ---------------- Watchlist ----------------
def get_watchlist() -> list[str]:
    with engine.connect() as c:
        return [r[0] for r in c.execute(select(watchlist.c.symbol).order_by(watchlist.c.added_at))]


def add_symbols(symbols: list[str]) -> None:
    have = set(get_watchlist())
    new = [s for s in symbols if s and s not in have]
    if new:
        with engine.begin() as c:
            c.execute(insert(watchlist), [{"symbol": s} for s in new])


def remove_symbol(symbol: str) -> None:
    with engine.begin() as c:
        c.execute(delete(watchlist).where(watchlist.c.symbol == symbol))


# ---------------- Alerts ----------------
def add_alert(symbol: str, kind: str, value: float | None) -> None:
    with engine.begin() as c:
        c.execute(insert(alerts).values(symbol=symbol, kind=kind, value=value, active=1))


def get_alerts(active_only: bool = False) -> pd.DataFrame:
    q = select(alerts).order_by(alerts.c.created_at.desc())
    if active_only:
        q = q.where(alerts.c.active == 1)
    with engine.connect() as c:
        return pd.read_sql(q, c)


def trigger_alert(alert_id: int, price: float) -> None:
    with engine.begin() as c:
        c.execute(update(alerts).where(alerts.c.id == alert_id)
                  .values(active=0, triggered_at=dt.datetime.utcnow(), triggered_price=price))


def delete_alert(alert_id: int) -> None:
    with engine.begin() as c:
        c.execute(delete(alerts).where(alerts.c.id == alert_id))


# ---------------- Price history ----------------
def save_prices(symbol: str, df: pd.DataFrame, last_n: int = 10) -> None:
    """Store the latest daily candles (upsert by symbol + date)."""
    tail = df.tail(last_n)
    if tail.empty:
        return
    dates = [d.date() for d in tail.index]
    with engine.begin() as c:
        c.execute(delete(price_history).where(price_history.c.symbol == symbol)
                  .where(price_history.c.date.in_(dates)))
        c.execute(insert(price_history), [
            {"symbol": symbol, "date": d, "open": float(r.open), "high": float(r.high),
             "low": float(r.low), "close": float(r.close), "volume": float(r.volume)}
            for d, r in zip(dates, tail.itertuples())
        ])


def price_history_stats() -> pd.DataFrame:
    with engine.connect() as c:
        return pd.read_sql(
            "SELECT symbol, COUNT(*) AS days, MIN(date) AS first_date, MAX(date) AS last_date "
            "FROM price_history GROUP BY symbol ORDER BY symbol", c)


def get_saved_prices(symbol: str) -> pd.DataFrame:
    with engine.connect() as c:
        return pd.read_sql(select(price_history).where(price_history.c.symbol == symbol)
                           .order_by(price_history.c.date), c)


# ---------------- Signal history ----------------
def last_signal(symbol: str) -> str | None:
    with engine.connect() as c:
        r = c.execute(select(signal_history.c.signal).where(signal_history.c.symbol == symbol)
                      .order_by(signal_history.c.created_at.desc()).limit(1)).first()
        return r[0] if r else None


def save_signal(r: dict, fund_score: int | None = None) -> bool:
    """Saves only when a stock's signal changes, so history stays clean."""
    if last_signal(r["Symbol"]) == r["Signal"]:
        return False
    with engine.begin() as c:
        c.execute(insert(signal_history).values(
            symbol=r["Symbol"], signal=r["Signal"], score=int(r["Score"]),
            fund_score=fund_score, price=float(r["Price"]),
            stop_loss=float(r["Stop Loss"]), target=float(r["Target"])))
    return True


def get_signals(limit: int = 500) -> pd.DataFrame:
    with engine.connect() as c:
        return pd.read_sql(select(signal_history).order_by(signal_history.c.created_at.desc())
                           .limit(limit), c)
