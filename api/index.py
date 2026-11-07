"""
Stock Analyzer API (FastAPI) — runs as a Vercel Python serverless function.
All /api/* requests are routed here by vercel.json.
"""
from __future__ import annotations

import math
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "_core"))

import pandas as pd                                    # noqa: E402
from fastapi import Depends, FastAPI, HTTPException, Request   # noqa: E402
from fastapi.responses import FileResponse, JSONResponse   # noqa: E402
from pydantic import BaseModel                         # noqa: E402

import config                                          # noqa: E402
import database as db                                  # noqa: E402
import engine                                          # noqa: E402
from data_source import get_source                     # noqa: E402
from fundamentals import analyze_fundamentals, combined_rating   # noqa: E402
from signals import analyze                            # noqa: E402

app = FastAPI(title="Stock Analyzer API")


# ---------------------------------------------------------------- helpers
def clean(o):
    """Make pandas / numpy values JSON-safe (NaN -> null)."""
    if isinstance(o, dict):
        return {str(k): clean(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [clean(v) for v in o]
    if isinstance(o, pd.Timestamp):
        return o.isoformat()
    if hasattr(o, "item") and not isinstance(o, (str, bytes)):
        try:
            o = o.item()
        except Exception:
            pass
    if isinstance(o, float) and (math.isnan(o) or math.isinf(o)):
        return None
    if hasattr(o, "isoformat"):
        return o.isoformat()
    return o


def ok(data):
    return JSONResponse(clean(data), headers={"Cache-Control": "no-store"})


def auth(request: Request):
    if config.CRON_SECRET and request.headers.get("authorization") == f"Bearer {config.CRON_SECRET}":
        return
    if config.APP_PASSWORD and request.headers.get("x-app-password") != config.APP_PASSWORD:
        raise HTTPException(401, "Wrong or missing password")


def ready():
    db.init(config.DEFAULT_WATCHLIST)
    return get_source()


def strip(r: dict) -> dict:
    return {k: v for k, v in r.items() if k != "_data"}


# ---------------------------------------------------------------- routes
INDEX_HTML = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "index.html")


@app.get("/", include_in_schema=False)
def home():
    """Serves the website itself."""
    return FileResponse(INDEX_HTML, media_type="text/html",
                        headers={"Cache-Control": "public, max-age=0, must-revalidate"})


@app.get("/api/health")
def health():
    return ok({"status": "ok", "data": get_source().name,
               "database": db.db_label(), "password_required": bool(config.APP_PASSWORD)})


@app.get("/api/login", dependencies=[Depends(auth)])
def login():
    return ok({"ok": True})


@app.get("/api/market", dependencies=[Depends(auth)])
def market():
    src = ready()
    out: dict = {}
    idx = src.snapshot(list(config.INDICES.values()))
    names = {v: k for k, v in config.INDICES.items()}
    idx["name"] = idx["symbol"].map(names)
    out["indices"] = idx.to_dict("records")
    try:
        n = src.history("^NSEI", 400).tail(250)
        out["nifty"] = {"dates": [d.strftime("%Y-%m-%d") for d in n.index],
                        "open": n.open.round(2).tolist(), "high": n.high.round(2).tolist(),
                        "low": n.low.round(2).tolist(), "close": n.close.round(2).tolist(),
                        "ema50": n.close.ewm(span=50).mean().round(2).tolist()}
    except Exception as e:
        out["nifty_error"] = str(e)
    n50 = src.snapshot(config.NIFTY50)
    if not n50.empty:
        out["breadth"] = {"advancing": int((n50.change_pct > 0).sum()),
                          "declining": int((n50.change_pct < 0).sum())}
        out["gainers"] = n50.nlargest(5, "change_pct").to_dict("records")
        out["losers"] = n50.nsmallest(5, "change_pct").to_dict("records")
    return ok(out)


@app.get("/api/scan", dependencies=[Depends(auth)])
def scan():
    src = ready()
    results, errors = engine.scan(src, db.get_watchlist(), save=True)
    fired = engine.check_alerts(results)
    return ok({"results": [strip(r) for r in results], "errors": errors, "alerts": fired})


@app.get("/api/stock/{symbol}", dependencies=[Depends(auth)])
def stock(symbol: str):
    src = ready()
    sym = symbol.strip().upper()
    try:
        hist = src.history(sym)
    except Exception as e:
        raise HTTPException(404, str(e))
    if len(hist) < 60:
        raise HTTPException(404, f"{sym}: not enough history")
    tech = analyze(sym, hist)
    try:
        f = src.fundamentals(sym)
        fa = analyze_fundamentals(f)
    except Exception as e:
        f, fa = {"error": str(e)}, {"score": None, "rating": "NO DATA", "checks": [], "metrics": {}}
    overall, label = combined_rating(tech["Score"], fa["score"])

    d = tech["_data"].tail(250)
    chart = {"dates": [x.strftime("%Y-%m-%d") for x in d.index]}
    for col in ["open", "high", "low", "close", "volume", "ema20", "ema50", "ema200",
                "bb_upper", "bb_lower", "macd", "macd_signal", "macd_hist", "rsi"]:
        chart[col] = d[col].round(2).tolist()
    x = tech["_data"].iloc[-1]

    c = hist["close"]
    returns = []
    for name, n in [("1W", 5), ("1M", 21), ("3M", 63), ("6M", 126), ("1Y", 252), ("2Y", 500)]:
        if len(c) > n:
            returns.append({"period": name, "pct": float((c.iloc[-1] / c.iloc[-n - 1] - 1) * 100)})
    daily = c.pct_change().dropna().tail(252)

    yearly = None
    if isinstance(f.get("yearly"), pd.DataFrame):
        y = (f["yearly"] / 1e7).round(0)
        yearly = {"years": [str(i) for i in y.index],
                  **{col: y[col].tolist() for col in y.columns}}

    return ok({
        "symbol": sym, "name": f.get("longName") or sym, "sector": f.get("sector"),
        "industry": f.get("industry"), "market_cap": f.get("marketCap"),
        "summary": f.get("longBusinessSummary"),
        "overall": overall, "overall_label": label,
        "technical": strip(tech),
        "levels": {"rsi": x.rsi, "adx": x.adx, "atr": x.atr, "supertrend": x.supertrend,
                   "high_52w": x.high_52w, "low_52w": x.low_52w},
        "fundamental": fa, "yearly": yearly, "chart": chart, "returns": returns,
        "risk": {"volatility": float(daily.std() * (252 ** 0.5) * 100),
                 "max_drawdown": float((c / c.cummax() - 1).tail(252).min() * 100),
                 "up_days": float((daily > 0).mean() * 100)},
        "in_watchlist": sym in db.get_watchlist(),
    })


@app.get("/api/signals", dependencies=[Depends(auth)])
def signals():
    src = ready()
    sig = db.get_signals()
    if sig.empty:
        return ok({"signals": [], "summary": {}})
    snap = src.snapshot(sorted(sig["symbol"].unique()))
    perf = engine.signal_performance(sig, dict(zip(snap.symbol, snap.price)))
    calls = perf[perf["result_pct"].notna()]
    summary = {"total": len(perf), "calls": len(calls),
               "right_pct": float((calls.result_pct > 0).mean() * 100) if len(calls) else None,
               "avg_result": float(calls.result_pct.mean()) if len(calls) else None}
    return ok({"signals": perf.to_dict("records"), "summary": summary})


class Symbols(BaseModel):
    symbols: list[str]


@app.get("/api/watchlist", dependencies=[Depends(auth)])
def get_watchlist():
    ready()
    return ok({"watchlist": db.get_watchlist()})


@app.post("/api/watchlist", dependencies=[Depends(auth)])
def add_watchlist(body: Symbols):
    ready()
    db.add_symbols([s.strip().upper() for s in body.symbols if s.strip()][:50])
    return ok({"watchlist": db.get_watchlist()})


@app.delete("/api/watchlist/{symbol}", dependencies=[Depends(auth)])
def remove_watchlist(symbol: str):
    ready()
    db.remove_symbol(symbol.upper())
    return ok({"watchlist": db.get_watchlist()})


class Alert(BaseModel):
    symbol: str
    kind: str
    value: float | None = None


@app.get("/api/alerts", dependencies=[Depends(auth)])
def get_alerts():
    ready()
    al = db.get_alerts()
    al["description"] = [engine.describe_alert(k, v) for k, v in zip(al.kind, al.value)] if len(al) else []
    return ok({"alerts": al.to_dict("records")})


@app.post("/api/alerts", dependencies=[Depends(auth)])
def add_alert(a: Alert):
    ready()
    kinds = {"price_above", "price_below", "rsi_above", "rsi_below", "strong_signal"}
    if a.kind not in kinds:
        raise HTTPException(400, "Unknown alert type")
    if a.kind != "strong_signal" and (a.value is None or a.value <= 0):
        raise HTTPException(400, "Enter a value above 0")
    db.add_alert(a.symbol.upper(), a.kind, None if a.kind == "strong_signal" else a.value)
    return get_alerts()


@app.delete("/api/alerts/{alert_id}", dependencies=[Depends(auth)])
def remove_alert(alert_id: int):
    ready()
    db.delete_alert(alert_id)
    return get_alerts()


@app.get("/api/prices", dependencies=[Depends(auth)])
def prices(symbol: str | None = None):
    ready()
    if symbol:
        return ok({"prices": db.get_saved_prices(symbol.upper())
                   .sort_values("date", ascending=False).to_dict("records")})
    return ok({"stats": db.price_history_stats().to_dict("records")})
