// Ranked tiers. Each tier below Heroic has 4 divisions (IV → I).
export const TIERS = [
  { name: "Bronze",      min: 0 },
  { name: "Silver",      min: 1000 },
  { name: "Gold",        min: 1300 },
  { name: "Platinum",    min: 1700 },
  { name: "Diamond",     min: 2200 },
  { name: "Heroic",      min: 2800 },
  { name: "Grandmaster", min: 3400 },
];

export function rankInfo(points) {
  let i = TIERS.length - 1;
  while (i > 0 && points < TIERS[i].min) i--;
  const tier = TIERS[i];
  const next = TIERS[i + 1];
  let division = null;
  if (next && i < 5) {
    const step = (next.min - tier.min) / 4;
    division = 4 - Math.min(3, Math.floor((points - tier.min) / step)); // 4,3,2,1
  }
  return { tier: tier.name, division, label: division ? `${tier.name} ${["", "I", "II", "III", "IV"][division]}` : tier.name,
           points, nextTierAt: next ? next.min : null, tierIndex: i };
}

/**
 * Rank points change for one battle royale match.
 * Higher tiers pay a bigger "entry cost", so climbing gets harder.
 */
export function brRankDelta({ points, placement, totalTeams, kills, assists }) {
  const tierIndex = rankInfo(points).tierIndex;
  const entryCost = [0, 4, 8, 12, 16, 20, 24][tierIndex];
  const pct = totalTeams > 1 ? 1 - (placement - 1) / (totalTeams - 1) : 1;   // 1 = winner, 0 = first out
  const placementPts = Math.round(pct * pct * 40) + (placement === 1 ? 15 : 0);
  const killPts = Math.min(kills, 10) * 3 + Math.min(assists, 10);
  const delta = placementPts + killPts - entryCost;
  return points + delta < 0 ? -points : delta;
}

export function csRankDelta({ points, won, kills }) {
  const tierIndex = rankInfo(points).tierIndex;
  const delta = (won ? 20 : -12) + Math.min(kills, 8) - tierIndex;
  return points + delta < 0 ? -points : delta;
}

export const xpForNextLevel = (level) => 400 + level * 150;
