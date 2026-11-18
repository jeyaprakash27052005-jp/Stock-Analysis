// Loads every module and checks pure helpers without needing a database.
import assert from "node:assert";
import app from "../src/app.js";
import { brRankDelta, rankInfo } from "../src/lib/ranks.js";
import { weeklyKey } from "../src/lib/periods.js";

assert.equal(rankInfo(0).label, "Bronze IV");
assert.equal(rankInfo(999).label, "Bronze I");
assert.equal(rankInfo(2900).label, "Heroic");
assert.equal(rankInfo(5000).tier, "Grandmaster");
assert.ok(brRankDelta({ points: 1500, placement: 1, totalTeams: 50, kills: 5, assists: 1 }) > 0);
assert.ok(brRankDelta({ points: 1500, placement: 50, totalTeams: 50, kills: 0, assists: 0 }) < 0);
assert.equal(brRankDelta({ points: 0, placement: 50, totalTeams: 50, kills: 0, assists: 0 }), 0);
assert.equal(weeklyKey(new Date("2026-11-18T12:00:00Z")), "2026-W47");
assert.equal(typeof app, "function");
console.log("smoke ok");
