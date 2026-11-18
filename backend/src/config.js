const env = process.env;

export const config = {
  databaseUrl: (env.DATABASE_URL || "").replace(/^postgres:\/\//, "postgresql://"),
  jwtSecret: env.JWT_SECRET || "",
  serverSecret: env.SERVER_SECRET || "",
  cronSecret: env.CRON_SECRET || "",
  google: { clientId: env.GOOGLE_WEB_CLIENT_ID || "", clientSecret: env.GOOGLE_WEB_CLIENT_SECRET || "" },
  facebook: { appId: env.FACEBOOK_APP_ID || "", appSecret: env.FACEBOOK_APP_SECRET || "" },
  androidPackage: env.ANDROID_PACKAGE_NAME || "com.yourstudio.stormdrop",
  playServiceAccount: env.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON || "",
  supportEmail: env.SUPPORT_EMAIL || "support@example.com",

  // Gameplay / economy tuning (kept on the server so it can't be tampered with)
  startingGold: 5000,
  startingDiamonds: 50,
  guildCreateCostGold: 2000,
  renameCostDiamonds: 99,
  regions: ["IND", "SEA", "ME", "EU", "NA", "SA"],
  modes: {
    br_solo:     { teamSize: 1, maxPlayers: 50, minPlayers: 10 },
    br_duo:      { teamSize: 2, maxPlayers: 50, minPlayers: 10 },
    br_squad:    { teamSize: 4, maxPlayers: 48, minPlayers: 12 },
    clash_squad: { teamSize: 4, maxPlayers: 8,  minPlayers: 8 },
    tdm:         { teamSize: 4, maxPlayers: 8,  minPlayers: 8 },
    training:    { teamSize: 1, maxPlayers: 1,  minPlayers: 1 },
  },
  // Wait this long for a full lobby before starting with minPlayers
  // Accounts with no app activity for this many days are permanently deleted (daily job).
  inactiveDeleteDays: Number(env.INACTIVE_DELETE_DAYS || 30),
  matchmakingFillSeconds: Number(env.MM_FILL_SECONDS || 45),
  // Diamond packs sold through Google Play (product ids must match Play Console → Monetize → In-app products)
  diamondPacks: {
    diamonds_100:  100,
    diamonds_310:  310,
    diamonds_520:  520,
    diamonds_1060: 1060,
    diamonds_2180: 2180,
    diamonds_5600: 5600,
  },
};
