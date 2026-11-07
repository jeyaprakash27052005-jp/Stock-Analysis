"""
Fundamental analysis: turns company financials into a 0-100 quality score with reasons.
"""
from __future__ import annotations


def _pct(v):
    """Yahoo gives ratios like 0.18 for 18%."""
    return None if v is None else float(v) * 100


def _div_yield(v):
    if v is None:
        return None
    v = float(v)
    return v * 100 if v < 0.1 else v   # some yfinance versions already give percent


def analyze_fundamentals(f: dict) -> dict:
    checks = []   # (points_earned, points_possible, text, good/bad/neutral)

    def check(possible, earned, text, mood):
        checks.append((earned, possible, text, mood))

    pe, pb = f.get("trailingPE"), f.get("priceToBook")
    roe, roa = _pct(f.get("returnOnEquity")), _pct(f.get("returnOnAssets"))
    de = f.get("debtToEquity")
    de = None if de is None else float(de) / 100   # Yahoo gives 45.0 for 0.45x
    margin, opm = _pct(f.get("profitMargins")), _pct(f.get("operatingMargins"))
    rg, eg = _pct(f.get("revenueGrowth")), _pct(f.get("earningsGrowth"))
    dy = _div_yield(f.get("dividendYield"))
    peg = f.get("pegRatio")
    cr = f.get("currentRatio")

    # Valuation (25)
    if pe is not None and pe > 0:
        if pe < 15:
            check(15, 15, f"P/E {pe:.1f}: cheap valuation", "good")
        elif pe < 30:
            check(15, 10, f"P/E {pe:.1f}: fair valuation", "neutral")
        elif pe < 50:
            check(15, 5, f"P/E {pe:.1f}: expensive", "bad")
        else:
            check(15, 0, f"P/E {pe:.1f}: very expensive", "bad")
    elif pe is not None:
        check(15, 0, "Negative P/E: company is loss-making", "bad")
    if pb is not None:
        if pb < 3:
            check(10, 10, f"P/B {pb:.1f}: reasonable vs book value", "good")
        elif pb < 8:
            check(10, 5, f"P/B {pb:.1f}: premium to book value", "neutral")
        else:
            check(10, 0, f"P/B {pb:.1f}: very high premium", "bad")

    # Profitability (30)
    if roe is not None:
        if roe >= 18:
            check(15, 15, f"ROE {roe:.1f}%: excellent returns on capital", "good")
        elif roe >= 12:
            check(15, 10, f"ROE {roe:.1f}%: good", "neutral")
        elif roe > 0:
            check(15, 4, f"ROE {roe:.1f}%: weak", "bad")
        else:
            check(15, 0, f"ROE {roe:.1f}%: negative", "bad")
    if margin is not None:
        if margin >= 15:
            check(15, 15, f"Net margin {margin:.1f}%: high", "good")
        elif margin >= 7:
            check(15, 9, f"Net margin {margin:.1f}%: average", "neutral")
        else:
            check(15, 3, f"Net margin {margin:.1f}%: thin", "bad")

    # Growth (25)
    if rg is not None:
        if rg >= 15:
            check(10, 10, f"Revenue growth {rg:.1f}%: strong", "good")
        elif rg >= 5:
            check(10, 6, f"Revenue growth {rg:.1f}%: moderate", "neutral")
        else:
            check(10, 1, f"Revenue growth {rg:.1f}%: weak or falling", "bad")
    if eg is not None:
        if eg >= 15:
            check(15, 15, f"Profit growth {eg:.1f}%: strong", "good")
        elif eg >= 0:
            check(15, 8, f"Profit growth {eg:.1f}%: modest", "neutral")
        else:
            check(15, 0, f"Profit growth {eg:.1f}%: profits falling", "bad")

    # Financial health (20)
    if de is not None:
        if de < 0.5:
            check(12, 12, f"Debt/Equity {de:.2f}: low debt", "good")
        elif de < 1.5:
            check(12, 6, f"Debt/Equity {de:.2f}: moderate debt", "neutral")
        else:
            check(12, 0, f"Debt/Equity {de:.2f}: high debt", "bad")
    if cr is not None:
        if cr >= 1.5:
            check(4, 4, f"Current ratio {cr:.2f}: comfortable liquidity", "good")
        elif cr >= 1:
            check(4, 2, f"Current ratio {cr:.2f}: adequate liquidity", "neutral")
        else:
            check(4, 0, f"Current ratio {cr:.2f}: tight liquidity", "bad")
    if dy is not None and dy > 0:
        check(4, 4 if dy >= 1 else 2, f"Dividend yield {dy:.2f}%", "good" if dy >= 1 else "neutral")

    possible = sum(c[1] for c in checks)
    earned = sum(c[0] for c in checks)
    score = round(earned / possible * 100) if possible else None
    if score is None:
        rating = "NO DATA"
    elif score >= 75:
        rating = "EXCELLENT"
    elif score >= 60:
        rating = "GOOD"
    elif score >= 40:
        rating = "AVERAGE"
    else:
        rating = "WEAK"

    return {
        "score": score, "rating": rating,
        "checks": [(c[2], c[3]) for c in checks],
        "coverage": f"{len(checks)} checks",
        "metrics": {
            "P/E": pe, "Forward P/E": f.get("forwardPE"), "P/B": pb, "PEG": peg,
            "ROE %": roe, "ROA %": roa, "Net margin %": margin, "Operating margin %": opm,
            "Revenue growth %": rg, "Profit growth %": eg, "Debt/Equity": de,
            "Current ratio": cr, "Dividend yield %": dy, "Beta": f.get("beta"),
            "Promoter/insider %": _pct(f.get("heldPercentInsiders")),
            "Institutions %": _pct(f.get("heldPercentInstitutions")),
        },
    }


def combined_rating(tech_score: int, fund_score: int | None) -> tuple[int, str]:
    """Overall 0-100 rating: 50% technical (converted from -100..100) + 50% fundamental."""
    tech_0_100 = (tech_score + 100) / 2
    overall = round(tech_0_100 if fund_score is None else 0.5 * tech_0_100 + 0.5 * fund_score)
    if overall >= 70:
        label = "STRONG BUY"
    elif overall >= 58:
        label = "BUY"
    elif overall >= 42:
        label = "HOLD"
    elif overall >= 30:
        label = "SELL"
    else:
        label = "STRONG SELL"
    return overall, label
