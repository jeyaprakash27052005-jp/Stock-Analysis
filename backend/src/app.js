// Stormdrop Royale backend (Express 5 — async errors are forwarded to the error handler automatically).
import cors from "cors";
import express from "express";
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

const api = express.Router();
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

// eslint-disable-next-line no-unused-vars
app.use((err, _req, res, _next) => {
  const status = err.status || (err.type === "entity.parse.failed" ? 400 : 500);
  if (status >= 500) console.error(err);
  res.status(status).json({ error: status >= 500 && !err.status ? "Server error" : err.message });
});

export default app;
