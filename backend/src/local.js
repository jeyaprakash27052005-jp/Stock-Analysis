// Local development server: npm run dev → http://localhost:3000 (website + /api)
import path from "node:path";
import { fileURLToPath } from "node:url";
import express from "express";
import app from "./app.js";

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), "..", "public");
const server = express();
server.use(express.static(root, { extensions: ["html"] }));
server.use(app);
const port = Number(process.env.PORT || 3000);
server.listen(port, () => console.log(`Stormdrop backend on http://localhost:${port}`));
