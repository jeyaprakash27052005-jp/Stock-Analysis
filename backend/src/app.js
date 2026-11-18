// Stormdrop Royale backend (Express 5 — async errors are forwarded to the error handler automatically).
import cors from "cors";
import express from "express";
import { ensureSchema } from "./db.js";
import { pages } from "./generated/assets.js";
import authRoutes from "./routes/auth.js";
import matchmakingRoutes from "./routes/matchmaking.js";
import miscRoutes from "./routes/misc.js";
import profileRoutes from "./routes/profile.js";
import progressionRoutes from "./routes/progression.js";
import serverRoutes from "./routes/server.js";
import socialRoutes from "./routes/social.js";
import storeRoutes from "./routes/store.js";

const app = express();
app.disable("x-powered-by");
app.set("trust proxy", true);
app.use(cors());
app.use(express.json({ limit: "256kb" }));
app.use((req, _res, next) => { req.body ??= {}; next(); });

// ---------------------------------------------------------------- website (served by Express itself,
// so it works whether Vercel runs this as an Express app or as a function behind a rewrite)
const PAGE_ROUTES = { "/": "index.html", "/privacy": "privacy.html", "/terms": "terms.html", "/delete-account": "delete-account.html" };
for (const [route, file] of Object.entries(PAGE_ROUTES)) {
  app.get([route, route === "/" ? "/index.html" : `${route}.html`], (_req, res) =>
    res.set("Cache-Control", "public, max-age=300").type("html").send(pages[file]));
}
app.get("/style.css", (_req, res) => res.set("Cache-Control", "public, max-age=3600").type("css").send(pages["style.css"]));

// APK download: set APK_URL in the hosting env vars, otherwise the latest GitHub release asset is used.
export const apkUrl = () => process.env.APK_URL ||
  "https://github.com/jeyaprakash27052005-jp/Stormdrop-Royal/releases/latest/download/stormdrop.apk";
app.get("/download", (_req, res) => res.redirect(302, apkUrl()));

const api = express.Router();
// Make sure the game tables exist before any API call (first request after deploy creates them).
api.use(async (req, _res, next) => {
  if (req.path === "/health") return next();
  try { await ensureSchema(); } catch (e) { console.error("schema setup failed:", e.message); }
  next();
});
api.use("/auth", authRoutes);
api.use("/me", profileRoutes);
api.use("/store", storeRoutes);
api.use("/progression", progressionRoutes);
api.use("/social", socialRoutes);
api.use("/matchmaking", matchmakingRoutes);
api.use("/server", serverRoutes);
api.use("/", miscRoutes);
app.use("/api", api);

app.use("/api", (_req, res) => res.status(404).json({ error: "Not found" }));
app.use((_req, res) => res.status(404).type("html").send(pages["index.html"]));

// eslint-disable-next-line no-unused-vars
app.use((err, _req, res, _next) => {
  const status = err.status || (err.type === "entity.parse.failed" ? 400 : 500);
  if (status >= 500) console.error(err);
  res.status(status).json({ error: status >= 500 && !err.status ? "Server error" : err.message });
});

export default app;
