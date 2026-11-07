"""
Market data: Yahoo Finance (real NSE data) or simulated demo data.
"""
from __future__ import annotations

import datetime as dt
import hashlib

import numpy as np
import pandas as pd

import config


class DataError(Exception):
    pass


def _clean(df: pd.DataFrame) -> pd.DataFrame:
    df = df.rename(columns=str.lower)
    cols = ["open", "high", "low", "close", "volume"]
    if any(c not in df.columns for c in cols):
        raise DataError("price data missing columns")
    df = df[cols].apply(pd.to_numeric, errors="coerce").dropna(subset=["close"])
    df.index = pd.to_datetime(df.index)
    if df.index.tz is not None:
        df.index = df.index.tz_localize(None)
    return df.sort_index()


def ticker(symbol: str) -> str:
    return symbol if symbol.startswith("^") or "." in symbol else f"{symbol}.NS"


class YahooSource:
    name = "Yahoo Finance (real NSE data, ~15 min delay)"

    def __init__(self):
        import yfinance as yf
        self.yf = yf

    def history(self, symbol: str, days: int = config.HISTORY_DAYS) -> pd.DataFrame:
        df = self.yf.Ticker(ticker(symbol)).history(period=f"{days}d", auto_adjust=False)
        if df is None or df.empty:
            raise DataError(f"{symbol}: no data (check the symbol)")
        return _clean(df)

    def bulk_history(self, symbols: list[str], days: int = config.HISTORY_DAYS) -> dict[str, pd.DataFrame]:
        """One request for many stocks (much faster on serverless)."""
        if not symbols:
            return {}
        tick = [ticker(s) for s in symbols]
        raw = self.yf.download(tick, period=f"{days}d", group_by="ticker", progress=False,
                               auto_adjust=False, threads=True)
        out = {}
        for s, t in zip(symbols, tick):
            try:
                sub = raw[t] if isinstance(raw.columns, pd.MultiIndex) else raw
                df = _clean(sub)
                if len(df):
                    out[s] = df
            except Exception:
                continue
        return out

    def snapshot(self, symbols: list[str]) -> pd.DataFrame:
        hist = self.bulk_history(symbols, days=10)
        rows = []
        for s, df in hist.items():
            c = df["close"]
            if len(c) >= 2:
                rows.append({"symbol": s, "price": float(c.iloc[-1]), "prev": float(c.iloc[-2]),
                             "change_pct": float((c.iloc[-1] / c.iloc[-2] - 1) * 100)})
        return pd.DataFrame(rows, columns=["symbol", "price", "prev", "change_pct"])

    def fundamentals(self, symbol: str) -> dict:
        t = self.yf.Ticker(ticker(symbol))
        info = t.info or {}
        if not info.get("longName") and not info.get("shortName"):
            raise DataError(f"{symbol}: fundamentals not available")
        keys = ["longName", "shortName", "sector", "industry", "marketCap", "trailingPE",
                "forwardPE", "priceToBook", "returnOnEquity", "returnOnAssets", "debtToEquity",
                "profitMargins", "operatingMargins", "revenueGrowth", "earningsGrowth",
                "dividendYield", "currentRatio", "pegRatio", "beta", "heldPercentInsiders",
                "heldPercentInstitutions", "longBusinessSummary", "website"]
        out = {k: info.get(k) for k in keys}
        out["longName"] = out["longName"] or out["shortName"]
        try:
            inc = t.income_stmt
            if inc is not None and not inc.empty:
                keep = [r for r in ["Total Revenue", "Net Income"] if r in inc.index]
                tbl = inc.loc[keep].T.sort_index()
                tbl.index = [d.year for d in pd.to_datetime(tbl.index)]
                out["yearly"] = tbl
        except Exception:
            pass
        return out


class DemoSource:
    name = "Demo data (simulated, NOT real prices)"

    @staticmethod
    def _rng(key: str):
        return np.random.default_rng(int(hashlib.md5(key.encode()).hexdigest()[:8], 16))

    def history(self, symbol: str, days: int = config.HISTORY_DAYS) -> pd.DataFrame:
        rng = self._rng(symbol)
        n = max(int(days * 0.69), 260)
        dates = pd.bdate_range(end=pd.Timestamp.today().normalize(), periods=n)
        rets = rng.normal(rng.uniform(-0.0005, 0.0012), rng.uniform(0.008, 0.02), n)
        for _ in range(3):
            a = rng.integers(0, n - 40)
            rets[a:a + 40] += rng.uniform(-0.004, 0.004)
        close = rng.uniform(200, 4000) * np.exp(np.cumsum(rets))
        open_ = close * (1 + rng.normal(0, 0.004, n))
        high = np.maximum(open_, close) * (1 + np.abs(rng.normal(0, 0.006, n)))
        low = np.minimum(open_, close) * (1 - np.abs(rng.normal(0, 0.006, n)))
        vol = rng.lognormal(14, 0.4, n).astype(int)
        return pd.DataFrame({"open": open_, "high": high, "low": low, "close": close,
                             "volume": vol}, index=dates)

    def bulk_history(self, symbols, days=config.HISTORY_DAYS):
        return {s: self.history(s, days) for s in symbols}

    def snapshot(self, symbols):
        rows = []
        for s in symbols:
            c = self.history(s, 30)["close"]
            rows.append({"symbol": s, "price": float(c.iloc[-1]), "prev": float(c.iloc[-2]),
                         "change_pct": float((c.iloc[-1] / c.iloc[-2] - 1) * 100)})
        return pd.DataFrame(rows)

    def fundamentals(self, symbol):
        r = self._rng(symbol + "f")
        rev, g = r.uniform(5e10, 9e11), r.uniform(-0.05, 0.25)
        yr = dt.date.today().year
        return {
            "longName": f"{symbol.title()} Ltd (demo)", "sector": "Demo sector",
            "industry": "Demo industry", "marketCap": r.uniform(2e11, 2e13),
            "trailingPE": r.uniform(8, 70), "forwardPE": r.uniform(8, 55),
            "priceToBook": r.uniform(0.8, 12), "returnOnEquity": r.uniform(0.03, 0.35),
            "returnOnAssets": r.uniform(0.01, 0.15), "debtToEquity": r.uniform(0, 180),
            "profitMargins": r.uniform(0.03, 0.3), "operatingMargins": r.uniform(0.05, 0.35),
            "revenueGrowth": g, "earningsGrowth": r.uniform(-0.2, 0.4),
            "dividendYield": r.uniform(0, 3.5), "currentRatio": r.uniform(0.6, 3),
            "pegRatio": r.uniform(0.5, 3), "beta": r.uniform(0.5, 1.6),
            "heldPercentInsiders": r.uniform(0, 0.7), "heldPercentInstitutions": r.uniform(0.1, 0.6),
            "longBusinessSummary": "Simulated company profile for demo mode.",
            "yearly": pd.DataFrame({
                "Total Revenue": [rev * (1 + g) ** i for i in range(4)],
                "Net Income": [rev * 0.12 * (1 + g) ** i for i in range(4)],
            }, index=range(yr - 4, yr)),
        }


_source = None


def get_source():
    global _source
    if _source is None:
        _source = DemoSource() if str(config.DATA_MODE).lower() == "demo" else YahooSource()
    return _source
