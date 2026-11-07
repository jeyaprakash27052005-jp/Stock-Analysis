"""
Signal engine: combines indicators into one score from -100 (strong sell) to +100 (strong buy).
"""
from __future__ import annotations

import pandas as pd

import indicators as ind


def analyze(symbol: str, df: pd.DataFrame, live_price: float | None = None) -> dict:
    if live_price:
        df = df.copy()
        last = df.index[-1]
        df.loc[last, "close"] = live_price
        df.loc[last, "high"] = max(df.loc[last, "high"], live_price)
        df.loc[last, "low"] = min(df.loc[last, "low"], live_price)

    d = ind.add_all(df)
    x, p = d.iloc[-1], d.iloc[-2]
    score, reasons = 0, []

    def add(points: int, text: str):
        nonlocal score
        score += points
        reasons.append(f"{'+' if points > 0 else ''}{points}  {text}")

    # Trend (max ±40)
    if x.close > x.ema200:
        add(10, "Price above 200 EMA (long-term uptrend)")
    else:
        add(-10, "Price below 200 EMA (long-term downtrend)")
    if x.ema20 > x.ema50:
        add(10, "20 EMA above 50 EMA")
    else:
        add(-10, "20 EMA below 50 EMA")
    if p.ema20 <= p.ema50 and x.ema20 > x.ema50:
        add(10, "Fresh golden crossover (20/50 EMA)")
    elif p.ema20 >= p.ema50 and x.ema20 < x.ema50:
        add(-10, "Fresh death crossover (20/50 EMA)")
    add(10 if x.supertrend > 0 else -10,
        "Supertrend is bullish" if x.supertrend > 0 else "Supertrend is bearish")

    # Momentum (max ±30)
    if x.rsi < 30:
        add(15, f"RSI {x.rsi:.0f}: oversold, bounce possible")
    elif x.rsi > 70:
        add(-15, f"RSI {x.rsi:.0f}: overbought, pullback possible")
    elif 50 < x.rsi <= 70:
        add(5, f"RSI {x.rsi:.0f}: healthy bullish momentum")
    elif 30 <= x.rsi < 50:
        add(-5, f"RSI {x.rsi:.0f}: weak momentum")
    if p.macd_hist <= 0 < x.macd_hist:
        add(15, "MACD bullish crossover")
    elif p.macd_hist >= 0 > x.macd_hist:
        add(-15, "MACD bearish crossover")
    elif x.macd_hist > 0:
        add(5, "MACD above signal line")
    else:
        add(-5, "MACD below signal line")

    # Volatility / breakout (max ±30)
    if x.close < x.bb_lower:
        add(10, "Closed below lower Bollinger Band")
    elif x.close > x.bb_upper:
        add(-5, "Closed above upper Bollinger Band (stretched)")
    vol_ratio = x.volume / x.vol_avg20 if x.vol_avg20 else 1
    day_up = x.close > p.close
    if vol_ratio > 1.8:
        add(10 if day_up else -10,
            f"Volume {vol_ratio:.1f}x average on {'up' if day_up else 'down'} move")
    if x.close >= 0.98 * x.high_52w:
        add(10, "Near 52-week high (breakout zone)")
    elif x.close <= 1.02 * x.low_52w:
        add(-10, "Near 52-week low")

    # Weaker signals in a sideways market (ADX < 20)
    if x.adx < 20:
        score = round(score * 0.7)
        reasons.append(f"x0.7  ADX {x.adx:.0f}: weak trend, signals less reliable")

    score = max(-100, min(100, score))
    if score >= 60:
        verdict = "STRONG BUY"
    elif score >= 25:
        verdict = "BUY"
    elif score <= -60:
        verdict = "STRONG SELL"
    elif score <= -25:
        verdict = "SELL"
    else:
        verdict = "NEUTRAL"

    # ATR-based levels for planning risk
    if score >= 0:
        stop, target = x.close - 2 * x.atr, x.close + 3 * x.atr
    else:
        stop, target = x.close + 2 * x.atr, x.close - 3 * x.atr

    return {
        "Symbol": symbol,
        "Price": round(float(x.close), 2),
        "Change %": round(float((x.close / p.close - 1) * 100), 2),
        "Score": int(score),
        "Signal": verdict,
        "RSI": round(float(x.rsi), 1),
        "ADX": round(float(x.adx), 1),
        "Trend": "Up" if x.supertrend > 0 else "Down",
        "Vol x Avg": round(float(vol_ratio), 2),
        "Stop Loss": round(float(stop), 2),
        "Target": round(float(target), 2),
        "Reasons": reasons,
        "_data": d,
    }
