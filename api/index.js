// Used when Vercel deploys from the repository root (no Root Directory setting needed):
// every request is rewritten here (see /vercel.json) and handled by the backend Express app.
import app from "../backend/src/app.js";
export default app;
