"""
Scan watchlist, store prices and signals, check alerts, measure signal performance.
"""
from __future__ import annotations

import urllib.parse
import urllib.request

import pandas as pd

import config
import database as db
from signals import analyze


def scan(source, symbols: list[str], save: bool = True) -> tuple[list[dict], list[str]]:
    results, errors = [], []
    try:
        hist = source.bulk_history(symbols)
    except Exception as e:
        return [], [f"Data download failed: {e}"]
    for sym in symbols:
        df = hist.get(sym)
        if df is None or len(df) < 60:
            errors.append(f"{sym}: not enough data (check the symbol)")
            continue
        try:
            r = analyze(sym, df)
            results.append(r)
            if save:
                db.save_prices(sym, df)
                db.save_signal(r)
        except Exception as e:
            errors.append(f"{sym}: {e}")
    results.sort(key=lambda r: r["Score"], reverse=True)
    return results, errors


def describe_alert(kind: str, value) -> str:
    v = value if value is not None and not pd.isna(value) else None
    return {
        "price_above": f"price above ₹{v:,.2f}" if v else "price above",
        "price_below": f"price below ₹{v:,.2f}" if v else "price below",
        "rsi_above": f"RSI above {v:.0f}" if v else "RSI above",
        "rsi_below": f"RSI below {v:.0f}" if v else "RSI below",
        "strong_signal": f"strong buy/sell signal (score ±{config.ALERT_MIN_SCORE})",
    }.get(kind, kind)


def check_alerts(results: list[dict]) -> list[str]:
    by_sym = {r["Symbol"]: r for r in results}
    fired = []
    active = db.get_alerts(active_only=True)
    for a in active.itertuples():
        r = by_sym.get(a.symbol)
        if r is None:
            continue
        p, rsi, v = r["Price"], r["RSI"], a.value
        has_v = v is not None and not pd.isna(v)
        hit = {
            "price_above": has_v and p >= v,
            "price_below": has_v and p <= v,
            "rsi_above": has_v and rsi >= v,
            "rsi_below": has_v and rsi <= v,
            "strong_signal": abs(r["Score"]) >= config.ALERT_MIN_SCORE,
        }.get(a.kind, False)
        if hit:
            db.trigger_alert(int(a.id), p)
            msg = (f"🔔 {a.symbol}: {describe_alert(a.kind, v)} | ₹{p:,.2f} | "
                   f"RSI {rsi} | {r['Signal']} ({r['Score']:+d})")
            fired.append(msg)
            send_telegram(msg)
    return fired


def send_telegram(text: str) -> None:
    if not (config.TELEGRAM_BOT_TOKEN and config.TELEGRAM_CHAT_ID):
        return
    url = f"https://api.telegram.org/bot{config.TELEGRAM_BOT_TOKEN}/sendMessage"
    data = urllib.parse.urlencode({"chat_id": config.TELEGRAM_CHAT_ID, "text": text}).encode()
    try:
        urllib.request.urlopen(url, data=data, timeout=8)
    except Exception:
        pass


def signal_performance(signals: pd.DataFrame, current: dict[str, float]) -> pd.DataFrame:
    if signals.empty:
        return signals
    df = signals.copy()
    df["now"] = df["symbol"].map(current)
    df["move_pct"] = (df["now"] / df["price"] - 1) * 100
    bull = df["signal"].str.contains("BUY", na=False)
    bear = df["signal"].str.contains("SELL", na=False)
    df["result_pct"] = df["move_pct"].where(bull, -df["move_pct"]).where(bull | bear)
    df["hit_target"] = (bull & (df["now"] >= df["target"])) | (bear & (df["now"] <= df["target"]))
    df["hit_stop"] = (bull & (df["now"] <= df["stop_loss"])) | (bear & (df["now"] >= df["stop_loss"]))
    return df
