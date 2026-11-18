const {
  initializeTestEnvironment,
  assertFails,
  assertSucceeds,
} = require("@firebase/rules-unit-testing");
const { test, before, after, beforeEach } = require("node:test");
const fs = require("node:fs");

let testEnv;
const PROJECT_ID = process.env.GCP_PROJECT || "demo-no-project";
const ALICE_UID = "alice_uid_123";
const BOB_UID = "bob_uid_456";

const [emulatorHost, emulatorPortStr] = (
  process.env.FIRESTORE_EMULATOR_HOST || "127.0.0.1:8085"
).split(":");
const emulatorPort = parseInt(emulatorPortStr, 10);

function ts(offsetMs = -1000) {
  return new Date(Date.now() + offsetMs);
}

function validUserProfile(uid, now = ts(-1000)) {
  return {
    userId: uid,
    playerTag: "9012345678",
    nickname: "Valkyrie99",
    region: "IND",
    level: 1,
    xp: 0,
    gold: 5000,
    diamonds: 50,
    brRankPoints: 0,
    csRankPoints: 0,
    equippedCharacterId: "char_kael",
    equippedParachuteId: "chute_classic_blue",
    loginStreakDay: 0,
    doubleXpUntilMs: 0,
    authProvider: "google",
    createdAt: now,
    updatedAt: now,
  };
}

before(async () => {
  const rules = fs.readFileSync("./firestore.rules", "utf8");
  testEnv = await initializeTestEnvironment({
    projectId: PROJECT_ID,
    firestore: {
      rules,
      host: emulatorHost,
      port: emulatorPort,
    },
  });
});

after(async () => {
  if (testEnv) {
    await testEnv.cleanup();
  }
});

beforeEach(async () => {
  if (testEnv) {
    await testEnv.clearFirestore();
  }
});

test("Unauthenticated user: denied access to users, leaderboard, and guilds", async () => {
  const unauth = testEnv.unauthenticatedContext().firestore();
  await assertFails(unauth.collection("users").doc(ALICE_UID).get());
  await assertFails(
    unauth.collection("users").doc(ALICE_UID).set(validUserProfile(ALICE_UID))
  );
  await assertFails(
    unauth.collection("leaderboard").where("publicEntry", "==", true).get()
  );
  await assertFails(
    unauth.collection("guilds").where("publicGuild", "==", true).get()
  );
});

test("User profile: owner create, partial update, immutable createdAt, and anti-ghost/ID guards", async () => {
  const aliceDb = testEnv.authenticatedContext(ALICE_UID).firestore();
  const created = ts(-10000);

  // Valid create (no @DocumentId `id` key)
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).set(validUserProfile(ALICE_UID, created))
  );

  // Valid partial update
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).update({
      gold: 4000,
      nickname: "ApexKael",
      updatedAt: ts(-500),
    })
  );

  // Reject ghost field ("isAdmin")
  await assertFails(
    aliceDb.collection("users").doc(ALICE_UID).update({
      isAdmin: true,
      updatedAt: ts(-200),
    })
  );

  // Reject immutable createdAt overwrite
  await assertFails(
    aliceDb.collection("users").doc(ALICE_UID).update({
      createdAt: ts(-100),
      updatedAt: ts(-100),
    })
  );

  // Reject future timestamp (> request.time)
  await assertFails(
    aliceDb.collection("users").doc(ALICE_UID).update({
      gold: 9999,
      updatedAt: ts(60000),
    })
  );

  // Reject extra `id` field on create
  const bobDb = testEnv.authenticatedContext(BOB_UID).firestore();
  await assertFails(
    bobDb
      .collection("users")
      .doc(BOB_UID)
      .set({ ...validUserProfile(BOB_UID, created), id: BOB_UID })
  );
});

test("Cross-user isolation: Bob cannot read or mutate Alice profile or subcollections", async () => {
  const aliceDb = testEnv.authenticatedContext(ALICE_UID).firestore();
  const bobDb = testEnv.authenticatedContext(BOB_UID).firestore();
  const now = ts(-5000);

  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).set(validUserProfile(ALICE_UID, now))
  );
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("owned_items").doc("char_kael").set({
      itemId: "char_kael",
      quantity: 1,
      createdAt: now,
      updatedAt: now,
    })
  );

  await assertFails(bobDb.collection("users").doc(ALICE_UID).get());
  await assertFails(
    bobDb.collection("users").doc(ALICE_UID).update({ gold: 0, updatedAt: ts(-100) })
  );
  await assertFails(
    bobDb.collection("users").doc(ALICE_UID).collection("owned_items").get()
  );
  await assertFails(
    bobDb.collection("users").doc(ALICE_UID).collection("missions").get()
  );
});

test("Subcollections: owned_items, missions, pass, match_history, friends match Kotlin payloads", async () => {
  const aliceDb = testEnv.authenticatedContext(ALICE_UID).firestore();
  const now = ts(-5000);

  // List queries on empty/populated user subcollections succeed
  await assertSucceeds(aliceDb.collection("users").doc(ALICE_UID).collection("missions").get());
  await assertSucceeds(aliceDb.collection("users").doc(ALICE_UID).collection("match_history").get());

  // Mission create & update
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("missions").doc("d_play_3").set({
      missionId: "d_play_3",
      progress: 0,
      claimed: false,
      createdAt: now,
      updatedAt: now,
    })
  );
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("missions").doc("d_play_3").update({
      progress: 3,
      claimed: true,
      updatedAt: ts(-500),
    })
  );

  // Pass create & update
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("pass").doc("s1").set({
      seasonId: "s1",
      passXp: 0,
      premiumUnlocked: false,
      claimedKeys: "",
      createdAt: now,
      updatedAt: now,
    })
  );
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("pass").doc("s1").update({
      passXp: 1200,
      premiumUnlocked: true,
      claimedKeys: "1:free,1:premium",
      updatedAt: ts(-500),
    })
  );

  // Match history create succeeds, update fails (immutable)
  await assertSucceeds(
    aliceDb.collection("users").doc(ALICE_UID).collection("match_history").doc("m_1").set({
      modeId: "br_solo",
      ranked: true,
      won: true,
      placement: 1,
      totalTeams: 20,
      kills: 7,
      damage: 1420,
      headshots: 3,
      survivalSeconds: 420,
      rankDelta: 45,
      xpGained: 320,
      goldGained: 229,
      createdAt: now,
    })
  );
  await assertFails(
    aliceDb.collection("users").doc(ALICE_UID).collection("match_history").doc("m_1").update({
      kills: 99,
    })
  );
});

test("Leaderboard: filtered public query succeeds; unfiltered query and cross-user writes fail", async () => {
  const aliceDb = testEnv.authenticatedContext(ALICE_UID).firestore();
  const bobDb = testEnv.authenticatedContext(BOB_UID).firestore();
  const now = ts(-5000);

  await assertSucceeds(
    aliceDb.collection("leaderboard").doc(ALICE_UID).set({
      userId: ALICE_UID,
      playerTag: "9012345678",
      nickname: "Valkyrie99",
      region: "IND",
      level: 12,
      brRankPoints: 1450,
      csRankPoints: 900,
      publicEntry: true,
      createdAt: now,
      updatedAt: now,
    })
  );

  // Filtered query matching Kotlin repository succeeds
  await assertSucceeds(
    bobDb.collection("leaderboard").where("publicEntry", "==", true).get()
  );

  // Unfiltered list query fails
  await assertFails(bobDb.collection("leaderboard").get());

  // Bob cannot update Alice's leaderboard entry
  await assertFails(
    bobDb.collection("leaderboard").doc(ALICE_UID).update({
      brRankPoints: 9999,
      updatedAt: ts(-100),
    })
  );
});

test("Guilds & Guild Chat: create, public list, join action, scoped chat query, and non-member denial", async () => {
  const aliceDb = testEnv.authenticatedContext(ALICE_UID).firestore();
  const bobDb = testEnv.authenticatedContext(BOB_UID).firestore();
  const charlieDb = testEnv.authenticatedContext("charlie_789").firestore();
  const now = ts(-5000);

  await assertSucceeds(
    aliceDb.collection("guilds").doc("g_apex").set({
      name: "Apex Drops",
      tag: "APEX",
      description: "Stormdrop squad ready to deploy.",
      ownerId: ALICE_UID,
      memberUids: [ALICE_UID],
      level: 1,
      glory: 100,
      memberCount: 1,
      maxMembers: 30,
      minLevel: 1,
      publicGuild: true,
      createdAt: now,
      updatedAt: now,
    })
  );

  // Public filtered query succeeds; unfiltered fails
  await assertSucceeds(
    bobDb.collection("guilds").where("publicGuild", "==", true).get()
  );
  await assertFails(bobDb.collection("guilds").get());

  // Non-member Charlie cannot post chat before joining
  await assertFails(
    charlieDb.collection("guilds").doc("g_apex").collection("messages").doc("msg0").set({
      guildId: "g_apex",
      senderId: "charlie_789",
      senderName: "Intruder",
      body: "Hi",
      memberUids: ["charlie_789"],
      createdAt: ts(-500),
    })
  );

  // Bob joins guild cleanly via Action 2 rule
  await assertSucceeds(
    bobDb.collection("guilds").doc("g_apex").update({
      memberUids: [ALICE_UID, BOB_UID],
      memberCount: 2,
      updatedAt: ts(-400),
    })
  );

  // Bob sends message & reads member-scoped messages
  await assertSucceeds(
    bobDb.collection("guilds").doc("g_apex").collection("messages").doc("msg1").set({
      guildId: "g_apex",
      senderId: BOB_UID,
      senderName: "BobFighter",
      body: "Ready to drop Ironhaven!",
      memberUids: [ALICE_UID, BOB_UID],
      createdAt: ts(-200),
    })
  );

  await assertSucceeds(
    aliceDb
      .collection("guilds")
      .doc("g_apex")
      .collection("messages")
      .where("memberUids", "array-contains", ALICE_UID)
      .get()
  );

  // Unfiltered message list fails for non-member Charlie
  await assertFails(
    charlieDb.collection("guilds").doc("g_apex").collection("messages").get()
  );

  // Charlie cannot directly read msg1; Alice can
  await assertFails(
    charlieDb.collection("guilds").doc("g_apex").collection("messages").doc("msg1").get()
  );
  await assertSucceeds(
    aliceDb.collection("guilds").doc("g_apex").collection("messages").doc("msg1").get()
  );
});
