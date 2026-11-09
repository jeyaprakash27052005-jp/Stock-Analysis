"""
Prediction & risk tools (Monte Carlo simulation, Value at Risk, correlation).

Based on the classic "Stock Market Analysis and Prediction" notebook:
moving averages, daily-return distribution, return correlation between stocks,
risk vs expected return, quantile (bootstrap) VaR and Monte Carlo VaR.

The Monte Carlo model is geometric Brownian motion using the stock's own
daily drift and volatility. It shows a *range of possible outcomes*, not a
forecast — real prices do not follow this model exactly.
"""
from __future__ import annotations

import numpy as np
import pandas as pd

TRADING_DAYS = 252


def _returns(close: pd.Series, lookback: int = TRADING_DAYS) -> pd.Series:
    return close.pct_change().dropna().tail(lookback)


def monte_carlo(close: pd.Series, days: int = 60, runs: int = 5000,
                lookback: int = TRADING_DAYS, seed: int | None = None) -> dict:
    """Simulate `runs` price paths for `days` trading days ahead."""
    days = int(min(max(days, 5), 252))
    runs = int(min(max(runs, 100), 20000))
    rets = _returns(close, lookback)
    if len(rets) < 30:
        raise ValueError("not enough history for a simulation")

    log_r = np.log1p(rets.values)
    mu, sigma = float(log_r.mean()), float(log_r.std(ddof=1))
    start = float(close.iloc[-1])

    rng = np.random.default_rng(seed)
    shocks = rng.normal(mu, sigma, size=(runs, days))
    paths = start * np.exp(np.cumsum(shocks, axis=1))
    paths = np.hstack([np.full((runs, 1), start), paths])     # day 0 = today
    final = paths[:, -1]

    bands = {f"p{q}": np.percentile(paths, q, axis=0).round(2).tolist()
             for q in (5, 25, 50, 75, 95)}
    q1, q5 = np.percentile(final, [1, 5])
    counts, edges = np.histogram(final, bins=60)

    return {
        "start_price": start, "days": days, "runs": runs, "lookback": len(rets),
        "daily_drift_pct": float(rets.mean() * 100),
        "daily_vol_pct": float(rets.std(ddof=1) * 100),
        "annual_vol_pct": float(rets.std(ddof=1) * np.sqrt(TRADING_DAYS) * 100),
        "expected_price": float(final.mean()),
        "median_price": float(np.median(final)),
        "best_5pct": float(np.percentile(final, 95)),
        "worst_5pct": float(q5),
        "worst_1pct": float(q1),
        "prob_gain_pct": float((final > start).mean() * 100),
        "prob_up10_pct": float((final > start * 1.10).mean() * 100),
        "prob_down10_pct": float((final < start * 0.90).mean() * 100),
        "var95": float(start - q5), "var99": float(start - q1),
        "bands": bands,
        "sample_paths": paths[:25].round(2).tolist(),
        "histogram": {"x": ((edges[:-1] + edges[1:]) / 2).round(2).tolist(),
                      "y": counts.tolist()},
    }


def history_stats(df: pd.DataFrame, lookback: int = TRADING_DAYS) -> dict:
    """Moving averages, daily-return distribution and historical VaR."""
    c = df["close"]
    d = df.tail(lookback)
    rets = _returns(c, lookback)
    counts, edges = np.histogram(rets * 100, bins=60)
    out = {
        "dates": [x.strftime("%Y-%m-%d") for x in d.index],
        "close": d["close"].round(2).tolist(),
        "volume": d["volume"].tolist(),
        "returns_hist": {"x": ((edges[:-1] + edges[1:]) / 2).round(3).tolist(),
                         "y": counts.tolist()},
        "daily_returns": (rets * 100).round(3).tolist(),
        "hist_var95_pct": float(rets.quantile(0.05) * 100),
        "hist_var99_pct": float(rets.quantile(0.01) * 100),
        "best_day_pct": float(rets.max() * 100),
        "worst_day_pct": float(rets.min() * 100),
    }
    for n in (10, 20, 50, 100):
        out[f"ma{n}"] = c.rolling(n).mean().tail(lookback).round(2).tolist()
    return out


def compare(closes: dict[str, pd.Series], lookback: int = TRADING_DAYS) -> dict:
    """Correlation, risk vs return and VaR for several stocks."""
    px = pd.DataFrame(closes).sort_index().ffill().dropna().tail(lookback + 1)
    if len(px) < 30:
        raise ValueError("not enough overlapping history to compare")
    rets = px.pct_change().dropna()
    syms = list(px.columns)
    growth = (px / px.iloc[0] * 100).round(2)
    rows = []
    for s in syms:
        r = rets[s]
        ann_ret = float(r.mean() * TRADING_DAYS * 100)
        ann_vol = float(r.std(ddof=1) * np.sqrt(TRADING_DAYS) * 100)
        rows.append({
            "symbol": s,
            "mean_daily_pct": float(r.mean() * 100),
            "risk_daily_pct": float(r.std(ddof=1) * 100),
            "annual_return_pct": ann_ret, "annual_vol_pct": ann_vol,
            "sharpe": ann_ret / ann_vol if ann_vol else None,
            "var95_pct": float(r.quantile(0.05) * 100),
            "var99_pct": float(r.quantile(0.01) * 100),
            "period_return_pct": float((px[s].iloc[-1] / px[s].iloc[0] - 1) * 100),
            "max_drawdown_pct": float((px[s] / px[s].cummax() - 1).min() * 100),
        })
    return {
        "symbols": syms, "days": len(rets),
        "correlation": rets.corr().round(3).values.tolist(),
        "stats": rows,
        "growth": {"dates": [d.strftime("%Y-%m-%d") for d in growth.index],
                   **{s: growth[s].tolist() for s in syms}},
    }
