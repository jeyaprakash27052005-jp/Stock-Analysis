package com.example.data

import android.content.Context
import com.example.R
import com.example.data.local.PlayerSettingsState
import com.example.data.local.SettingsDataStore
import com.example.data.model.CatalogItem
import com.example.data.model.DiamondPack
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.model.ItemType
import com.example.data.model.MatchResultSummary
import com.example.data.model.RankCalculator
import com.example.data.remote.AchievementProgressDoc
import com.example.data.remote.FriendDoc
import com.example.data.remote.GuildDoc
import com.example.data.remote.GuildMessageDoc
import com.example.data.remote.LeaderboardDoc
import com.example.data.remote.MatchHistoryDoc
import com.example.data.remote.MissionProgressDoc
import com.example.data.remote.OperationType
import com.example.data.remote.OwnedItemDoc
import com.example.data.remote.PassProgressDoc
import com.example.data.remote.UserProfileDoc
import com.example.data.remote.handleFirestoreError
import com.google.firebase.Firebase
import com.google.firebase.Timestamp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.auth
import com.google.firebase.firestore.DocumentSnapshot
import com.google.firebase.firestore.FieldValue
import com.google.firebase.firestore.FirebaseFirestore
import com.google.firebase.firestore.FirebaseFirestoreException
import com.google.firebase.firestore.snapshots
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.tasks.await
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.UUID
import kotlin.random.Random

data class SpinWinResult(
    val item: CatalogItem,
    val duplicateConvertedGold: Int = 0,
)

data class CloudHealthState(
    val firebaseConnected: Boolean = true,
    val summary: String = "Firebase Cloud Sync Active",
)

class StormdropRepository(
    private val db: FirebaseFirestore,
    private val settingsStore: SettingsDataStore,
    private val auth: FirebaseAuth = Firebase.auth,
) {
    constructor(context: Context) : this(
        FirebaseFirestore.getInstance(
            context.applicationContext.getString(R.string.firestore_database_id)
        ),
        SettingsDataStore(context.applicationContext),
    )

    val settingsFlow: Flow<PlayerSettingsState> = settingsStore.settingsFlow

    private fun requireUserId(): String {
        return auth.currentUser?.uid
            ?: throw IllegalStateException("User must be signed in with Google before accessing Firestore.")
    }

    private fun userDocRef(userId: String) = db.collection("users").document(userId)

    private fun <T> Flow<T>.logFirestoreErrors(
        uid: String,
        operationType: OperationType,
        path: String,
    ): Flow<T> = catch { error ->
        val firestoreError = generateSequence(error) { it.cause }
            .filterIsInstance<FirebaseFirestoreException>()
            .firstOrNull()
        if (firestoreError == null && error is CancellationException) throw error
        val exceptionToLog = firestoreError ?: (error as? Exception)
        if (exceptionToLog != null && auth.currentUser?.uid == uid) {
            handleFirestoreError(exceptionToLog, operationType, path)
        }
        throw error
    }

    fun observeProfile(userId: String): Flow<UserProfileDoc?> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId"
        return userDocRef(userId)
            .snapshots()
            .map { snapshot ->
                snapshot.toObject(
                    UserProfileDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.GET, path)
    }

    fun observeOwnedItems(userId: String): Flow<List<OwnedItemDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/owned_items"
        return userDocRef(userId).collection("owned_items")
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    OwnedItemDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeMissions(userId: String): Flow<List<MissionProgressDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/missions"
        return userDocRef(userId).collection("missions")
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    MissionProgressDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeAchievements(userId: String): Flow<List<AchievementProgressDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/achievements"
        return userDocRef(userId).collection("achievements")
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    AchievementProgressDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observePass(userId: String): Flow<PassProgressDoc?> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/pass/${GameCatalog.passSeasonId}"
        return userDocRef(userId).collection("pass").document(GameCatalog.passSeasonId)
            .snapshots()
            .map { snapshot ->
                snapshot.toObject(
                    PassProgressDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.GET, path)
    }

    fun observeMatchHistory(userId: String): Flow<List<MatchHistoryDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/match_history"
        return userDocRef(userId).collection("match_history")
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    MatchHistoryDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                ).sortedByDescending { it.createdAt ?: Timestamp(0, 0) }
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeFriends(userId: String): Flow<List<FriendDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "users/$userId/friends"
        return userDocRef(userId).collection("friends")
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    FriendDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                ).sortedByDescending { it.online }
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeLeaderboard(userId: String): Flow<List<LeaderboardDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "leaderboard"
        return db.collection("leaderboard")
            .whereEqualTo("publicEntry", true)
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    LeaderboardDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                )
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeGuilds(userId: String): Flow<List<GuildDoc>> {
        require(userId.isNotBlank()) { "userId must not be blank" }
        val path = "guilds"
        return db.collection("guilds")
            .whereEqualTo("publicGuild", true)
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    GuildDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                ).sortedByDescending { it.glory }
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    fun observeGuildChat(userId: String, guildId: String): Flow<List<GuildMessageDoc>> {
        require(userId.isNotBlank() && guildId.isNotBlank())
        val path = "guilds/$guildId/messages"
        return db.collection("guilds").document(guildId).collection("messages")
            .whereArrayContains("memberUids", userId)
            .snapshots()
            .map { snapshot ->
                snapshot.toObjects(
                    GuildMessageDoc::class.java,
                    DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
                ).sortedBy { it.createdAt ?: Timestamp(0, 0) }
            }
            .logFirestoreErrors(userId, OperationType.LIST, path)
    }

    suspend fun updateSettings(transform: (PlayerSettingsState) -> PlayerSettingsState) {
        settingsStore.update(transform)
    }

    private fun sanitizeInitialNickname(raw: String?): String {
        val cleaned = (raw ?: "")
            .replace(Regex("[^\\p{L}\\p{N}_ .-]"), "")
            .trim()
            .take(16)
            .trim()
        return if (cleaned.length >= 3) cleaned else "Dropper${Random.nextInt(1000, 9999)}"
    }

    suspend fun ensureUserInitialized(userId: String): Result<Unit> {
        var currentPath = "users/$userId"
        return try {
            val uid = requireUserId()
            check(uid == userId) { "Signed-in user mismatch" }

            val userSnap = userDocRef(uid).get().await()
            val batch = db.batch()
            var hasWrites = false

            val existingProfile = userSnap.toObject(
                UserProfileDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            )
            if (existingProfile == null) {
                val tag = (1000000000L + Random.nextLong(8999999999L)).toString()
                val nickname = sanitizeInitialNickname(auth.currentUser?.displayName)
                val profilePayload = mapOf(
                    "userId" to uid,
                    "playerTag" to tag,
                    "nickname" to nickname,
                    "region" to "IND",
                    "level" to 1,
                    "xp" to 0,
                    "gold" to 5000,
                    "diamonds" to 50,
                    "brRankPoints" to 0,
                    "csRankPoints" to 0,
                    "equippedCharacterId" to "char_kael",
                    "equippedParachuteId" to "chute_classic_blue",
                    "loginStreakDay" to 0,
                    "doubleXpUntilMs" to 0L,
                    "authProvider" to "google",
                    "createdAt" to FieldValue.serverTimestamp(),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
                batch.set(userDocRef(uid), profilePayload)

                val lbPayload = mapOf(
                    "userId" to uid,
                    "playerTag" to tag,
                    "nickname" to nickname,
                    "region" to "IND",
                    "level" to 1,
                    "brRankPoints" to 0,
                    "csRankPoints" to 0,
                    "publicEntry" to true,
                    "createdAt" to FieldValue.serverTimestamp(),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
                batch.set(db.collection("leaderboard").document(uid), lbPayload)

                listOf("char_kael", "avatar_default", "banner_default").forEach { itemId ->
                    batch.set(
                        userDocRef(uid).collection("owned_items").document(itemId),
                        mapOf(
                            "itemId" to itemId,
                            "quantity" to 1,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    )
                }

                listOf(
                    Triple("8401928371", "MiraSquad", 14 to 1420),
                    Triple("8401928372", "TorvinWall", 9 to 1080),
                    Triple("8401928373", "SukiDash", 18 to 1790),
                ).forEachIndexed { idx, (friendTag, nick, stats) ->
                    val status = if (idx == 2) "incoming" else "accepted"
                    val online = idx != 1
                    batch.set(
                        userDocRef(uid).collection("friends").document(friendTag),
                        mapOf(
                            "playerTag" to friendTag,
                            "nickname" to nick,
                            "level" to stats.first,
                            "brRankPoints" to stats.second,
                            "status" to status,
                            "online" to online,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    )
                }
                hasWrites = true
            }

            currentPath = "users/$uid/missions"
            val existingMissionIds = userDocRef(uid).collection("missions").get().await()
                .documents.map { it.id }.toSet()
            for (m in GameCatalog.missions) {
                if (m.id !in existingMissionIds) {
                    batch.set(
                        userDocRef(uid).collection("missions").document(m.id),
                        mapOf(
                            "missionId" to m.id,
                            "progress" to 0,
                            "claimed" to false,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    )
                    hasWrites = true
                }
            }

            currentPath = "users/$uid/achievements"
            val existingAchIds = userDocRef(uid).collection("achievements").get().await()
                .documents.map { it.id }.toSet()
            for (a in GameCatalog.achievements) {
                if (a.id !in existingAchIds) {
                    batch.set(
                        userDocRef(uid).collection("achievements").document(a.id),
                        mapOf(
                            "achievementId" to a.id,
                            "progress" to 0,
                            "claimed" to false,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    )
                    hasWrites = true
                }
            }

            currentPath = "users/$uid/pass/${GameCatalog.passSeasonId}"
            val passSnap = userDocRef(uid).collection("pass").document(GameCatalog.passSeasonId).get().await()
            if (!passSnap.exists()) {
                batch.set(
                    userDocRef(uid).collection("pass").document(GameCatalog.passSeasonId),
                    mapOf(
                        "seasonId" to GameCatalog.passSeasonId,
                        "passXp" to 0,
                        "premiumUnlocked" to false,
                        "claimedKeys" to "",
                        "createdAt" to FieldValue.serverTimestamp(),
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                )
                hasWrites = true
            }

            if (hasWrites) {
                currentPath = "users/$uid (batch init)"
                batch.commit().await()
            }
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.WRITE, currentPath)
            Result.failure(e)
        }
    }

    private suspend fun getProfileOnce(uid: String): UserProfileDoc? {
        return userDocRef(uid).get().await().toObject(
            UserProfileDoc::class.java,
            DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
        )
    }

    private suspend fun syncLeaderboardProjection(
        uid: String,
        profile: UserProfileDoc,
        guildTag: String? = null,
        clearGuildTag: Boolean = false,
    ) {
        val lbRef = db.collection("leaderboard").document(uid)
        val snap = lbRef.get().await()
        if (!snap.exists()) {
            val payload = mapOf(
                "userId" to uid,
                "playerTag" to profile.playerTag,
                "nickname" to profile.nickname,
                "region" to profile.region,
                "level" to profile.level,
                "brRankPoints" to profile.brRankPoints,
                "csRankPoints" to profile.csRankPoints,
                "guildTag" to guildTag,
                "publicEntry" to true,
                "createdAt" to FieldValue.serverTimestamp(),
                "updatedAt" to FieldValue.serverTimestamp(),
            ).filterValues { it != null }
            lbRef.set(payload).await()
        } else {
            val updates = mutableMapOf<String, Any>(
                "nickname" to profile.nickname,
                "region" to profile.region,
                "level" to profile.level,
                "brRankPoints" to profile.brRankPoints,
                "csRankPoints" to profile.csRankPoints,
                "updatedAt" to FieldValue.serverTimestamp(),
            )
            if (clearGuildTag) {
                updates["guildTag"] = FieldValue.delete()
            } else if (guildTag != null) {
                updates["guildTag"] = guildTag
            }
            lbRef.update(updates).await()
        }
    }

    suspend fun deleteAccount(userId: String): Result<Unit> {
        var path = "users/$userId"
        return try {
            val uid = requireUserId()
            check(uid == userId) { "Signed-in user mismatch" }

            val profile = getProfileOnce(uid)
            val guildId = profile?.guildId
            if (!guildId.isNullOrBlank()) {
                leaveGuildInternal(uid, guildId)
            }

            val subcollections = listOf(
                "owned_items", "missions", "achievements", "pass", "match_history", "friends"
            )
            for (sub in subcollections) {
                path = "users/$uid/$sub"
                val docs = userDocRef(uid).collection(sub).get().await().documents
                for (doc in docs) {
                    doc.reference.delete().await()
                }
            }

            path = "leaderboard/$uid"
            db.collection("leaderboard").document(uid).delete().await()

            path = "users/$uid"
            userDocRef(uid).delete().await()

            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.DELETE, path)
            Result.failure(e)
        }
    }

    suspend fun buyItem(item: CatalogItem, currency: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            if (!item.inStore) return Result.failure(IllegalArgumentException("Not sold in store"))
            val price = (if (currency == "gold") item.priceGold else item.priceDiamonds)
                ?: return Result.failure(IllegalArgumentException("Cannot buy with $currency"))

            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("Profile not loaded"))
            val itemRef = userDocRef(uid).collection("owned_items").document(item.id)
            val existingItem = itemRef.get().await().toObject(
                OwnedItemDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            )
            if (item.type != ItemType.CONSUMABLE && existingItem != null) {
                return Result.failure(IllegalStateException("You already own ${item.name}"))
            }

            val walletUpdate = if (currency == "gold") {
                if (p.gold < price) return Result.failure(IllegalStateException("Not enough Gold"))
                mapOf("gold" to (p.gold - price), "updatedAt" to FieldValue.serverTimestamp())
            } else {
                if (p.diamonds < price) return Result.failure(IllegalStateException("Not enough Diamonds"))
                mapOf("diamonds" to (p.diamonds - price), "updatedAt" to FieldValue.serverTimestamp())
            }

            val batch = db.batch()
            batch.update(userDocRef(uid), walletUpdate)
            if (existingItem == null) {
                batch.set(
                    itemRef,
                    mapOf(
                        "itemId" to item.id,
                        "quantity" to 1,
                        "createdAt" to FieldValue.serverTimestamp(),
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                )
            } else {
                batch.update(
                    itemRef,
                    mapOf(
                        "quantity" to (existingItem.quantity + 1),
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                )
            }
            path = "users/$uid/owned_items/${item.id}"
            batch.commit().await()
            Result.success("${item.name} added to Vault!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.WRITE, path)
            Result.failure(e)
        }
    }

    // TODO: Integrate Google Play Billing Library & server receipt verification for production IAP.
    fun requestDiamondPackPlaceholder(pack: DiamondPack): String {
        return "Google Play Billing required for ${pack.diamonds} Diamonds (${pack.priceDisplay}). Configure Play Console product '${pack.productId}'."
    }

    suspend fun equipItem(item: CatalogItem): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid/owned_items/${item.id}"
            val owned = userDocRef(uid).collection("owned_items").document(item.id).get().await()
            if (!owned.exists()) return Result.failure(IllegalStateException("Item not owned"))

            val field = when (item.type) {
                ItemType.CHARACTER -> "equippedCharacterId"
                ItemType.PET -> "equippedPetId"
                ItemType.PARACHUTE -> "equippedParachuteId"
                ItemType.WEAPON_SKIN -> "equippedWeaponSkinId"
                else -> return Result.success("${item.name} equipped")
            }
            path = "users/$uid"
            userDocRef(uid).update(
                mapOf(field to item.id, "updatedAt" to FieldValue.serverTimestamp())
            ).await()
            Result.success("Equipped ${item.name}")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun cycleCharacter(direction: Int): Result<String?> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val p = getProfileOnce(uid) ?: return Result.success(null)
            val ownedChars = userDocRef(uid).collection("owned_items").get().await()
                .toObjects(OwnedItemDoc::class.java, DocumentSnapshot.ServerTimestampBehavior.ESTIMATE)
                .mapNotNull { GameCatalog.itemById(it.itemId) }
                .filter { it.type == ItemType.CHARACTER }
                .map { it.id }
                .ifEmpty { listOf("char_kael") }
            val idx = ownedChars.indexOf(p.equippedCharacterId).coerceAtLeast(0)
            val nextId = ownedChars[(idx + direction + ownedChars.size) % ownedChars.size]
            userDocRef(uid).update(
                mapOf("equippedCharacterId" to nextId, "updatedAt" to FieldValue.serverTimestamp())
            ).await()
            Result.success(nextId)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun spinFortuneDrop(count: Int): Result<List<SpinWinResult>> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            val cost = if (count == 10) 180 else 20
            if (p.diamonds < cost) return Result.failure(IllegalStateException("Need $cost Diamonds"))

            var curGold = p.gold
            var curDiamonds = p.diamonds - cost
            var curDoubleXpUntil = p.doubleXpUntilMs

            val ownedMap = userDocRef(uid).collection("owned_items").get().await()
                .toObjects(OwnedItemDoc::class.java, DocumentSnapshot.ServerTimestampBehavior.ESTIMATE)
                .associateBy { it.itemId }
                .toMutableMap()

            val pool = GameCatalog.spinRewards
            val totalWeight = pool.sumOf { it.weight }
            val wins = mutableListOf<SpinWinResult>()
            val newItemGrants = mutableMapOf<String, Int>()
            val incItemGrants = mutableMapOf<String, Int>()

            repeat(if (count == 10) 10 else 1) {
                var roll = Random.nextInt(totalWeight)
                var pick = pool.first()
                for (entry in pool) {
                    roll -= entry.weight
                    if (roll < 0) {
                        pick = entry
                        break
                    }
                }
                val item = GameCatalog.itemById(pick.itemId) ?: return@repeat
                val alreadyOwned = item.type != ItemType.CONSUMABLE &&
                    item.type != ItemType.CURRENCY &&
                    ownedMap.containsKey(item.id)
                if (alreadyOwned) {
                    curGold += 1000
                    wins.add(SpinWinResult(item, duplicateConvertedGold = 1000))
                } else {
                    when (item.type) {
                        ItemType.CURRENCY -> {
                            val amount = item.metaAmount * pick.quantity
                            if (item.metaCurrency == "gold") curGold += amount
                            else if (item.metaCurrency == "diamonds") curDiamonds += amount
                        }
                        ItemType.CONSUMABLE -> {
                            if (item.id == "card_xp_double") {
                                val base = maxOf(System.currentTimeMillis(), curDoubleXpUntil)
                                curDoubleXpUntil = base + 3_600_000L * pick.quantity
                            }
                            val ex = ownedMap[item.id]
                            if (ex == null) {
                                newItemGrants[item.id] = (newItemGrants[item.id] ?: 0) + pick.quantity
                                ownedMap[item.id] = OwnedItemDoc(itemId = item.id, quantity = pick.quantity)
                            } else {
                                val nextQty = ex.quantity + pick.quantity
                                incItemGrants[item.id] = nextQty
                                ownedMap[item.id] = ex.copy(quantity = nextQty)
                            }
                        }
                        else -> {
                            newItemGrants[item.id] = 1
                            ownedMap[item.id] = OwnedItemDoc(itemId = item.id, quantity = 1)
                        }
                    }
                    wins.add(SpinWinResult(item))
                }
            }

            val batch = db.batch()
            batch.update(
                userDocRef(uid),
                mapOf(
                    "gold" to curGold,
                    "diamonds" to curDiamonds,
                    "doubleXpUntilMs" to curDoubleXpUntil,
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            )
            for ((itemId, qty) in newItemGrants) {
                batch.set(
                    userDocRef(uid).collection("owned_items").document(itemId),
                    mapOf(
                        "itemId" to itemId,
                        "quantity" to qty,
                        "createdAt" to FieldValue.serverTimestamp(),
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                )
            }
            for ((itemId, qty) in incItemGrants) {
                batch.update(
                    userDocRef(uid).collection("owned_items").document(itemId),
                    mapOf(
                        "quantity" to qty,
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                )
            }
            batch.commit().await()
            Result.success(wins)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.WRITE, path)
            Result.failure(e)
        }
    }

    private suspend fun grantRewardItem(
        uid: String,
        profile: UserProfileDoc,
        itemId: String,
        quantity: Int = 1,
    ): UserProfileDoc {
        val item = GameCatalog.itemById(itemId) ?: return profile
        var updatedProfile = profile
        when (item.type) {
            ItemType.CURRENCY -> {
                val amount = item.metaAmount * quantity
                if (item.metaCurrency == "gold") {
                    updatedProfile = profile.copy(gold = profile.gold + amount)
                    userDocRef(uid).update(
                        mapOf("gold" to updatedProfile.gold, "updatedAt" to FieldValue.serverTimestamp())
                    ).await()
                } else if (item.metaCurrency == "diamonds") {
                    updatedProfile = profile.copy(diamonds = profile.diamonds + amount)
                    userDocRef(uid).update(
                        mapOf("diamonds" to updatedProfile.diamonds, "updatedAt" to FieldValue.serverTimestamp())
                    ).await()
                }
            }
            ItemType.PASS_XP -> addPassXpInternal(uid, item.metaAmount * quantity)
            ItemType.CONSUMABLE -> {
                if (item.id == "card_xp_double") {
                    val base = maxOf(System.currentTimeMillis(), profile.doubleXpUntilMs)
                    val until = base + 3_600_000L * quantity
                    updatedProfile = profile.copy(doubleXpUntilMs = until)
                    userDocRef(uid).update(
                        mapOf("doubleXpUntilMs" to until, "updatedAt" to FieldValue.serverTimestamp())
                    ).await()
                }
                val ref = userDocRef(uid).collection("owned_items").document(itemId)
                val snap = ref.get().await()
                if (snap.exists()) {
                    val prevQty = (snap.getLong("quantity") ?: 0L).toInt()
                    ref.update(
                        mapOf("quantity" to (prevQty + quantity), "updatedAt" to FieldValue.serverTimestamp())
                    ).await()
                } else {
                    ref.set(
                        mapOf(
                            "itemId" to itemId,
                            "quantity" to quantity,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    ).await()
                }
            }
            else -> {
                val ref = userDocRef(uid).collection("owned_items").document(itemId)
                if (!ref.get().await().exists()) {
                    ref.set(
                        mapOf(
                            "itemId" to itemId,
                            "quantity" to 1,
                            "createdAt" to FieldValue.serverTimestamp(),
                            "updatedAt" to FieldValue.serverTimestamp(),
                        )
                    ).await()
                }
            }
        }
        return updatedProfile
    }

    private suspend fun addXpInternal(uid: String, amount: Int): Pair<UserProfileDoc?, List<Int>> {
        val p = getProfileOnce(uid) ?: return null to emptyList()
        var lvl = p.level
        var curXp = p.xp + amount
        var goldBonus = 0
        var diaBonus = 0
        val gained = mutableListOf<Int>()
        while (lvl < 100 && curXp >= RankCalculator.xpForNextLevel(lvl)) {
            curXp -= RankCalculator.xpForNextLevel(lvl)
            lvl++
            gained.add(lvl)
            goldBonus += 200
            if (lvl % 5 == 0) diaBonus += 10
        }
        val next = p.copy(
            level = lvl,
            xp = curXp,
            gold = p.gold + goldBonus,
            diamonds = p.diamonds + diaBonus,
        )
        userDocRef(uid).update(
            mapOf(
                "level" to next.level,
                "xp" to next.xp,
                "gold" to next.gold,
                "diamonds" to next.diamonds,
                "updatedAt" to FieldValue.serverTimestamp(),
            )
        ).await()
        syncLeaderboardProjection(uid, next)
        return next to gained
    }

    private suspend fun addPassXpInternal(uid: String, amount: Int) {
        if (amount <= 0) return
        val ref = userDocRef(uid).collection("pass").document(GameCatalog.passSeasonId)
        val snap = ref.get().await()
        val maxXp = GameCatalog.passXpPerLevel * GameCatalog.passMaxLevel
        if (snap.exists()) {
            val cur = (snap.getLong("passXp") ?: 0L).toInt()
            ref.update(
                mapOf(
                    "passXp" to (cur + amount).coerceAtMost(maxXp),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
        } else {
            ref.set(
                mapOf(
                    "seasonId" to GameCatalog.passSeasonId,
                    "passXp" to amount.coerceAtMost(maxXp),
                    "premiumUnlocked" to false,
                    "claimedKeys" to "",
                    "createdAt" to FieldValue.serverTimestamp(),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
        }
    }

    suspend fun unlockPremiumPass(): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid/pass/${GameCatalog.passSeasonId}"
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            val passRef = userDocRef(uid).collection("pass").document(GameCatalog.passSeasonId)
            val pass = passRef.get().await().toObject(
                PassProgressDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: PassProgressDoc(seasonId = GameCatalog.passSeasonId)
            if (pass.premiumUnlocked) return Result.failure(IllegalStateException("Premium Pass already active"))
            if (p.diamonds < GameCatalog.passPremiumPriceDiamonds) {
                return Result.failure(IllegalStateException("Need ${GameCatalog.passPremiumPriceDiamonds} Diamonds"))
            }
            val batch = db.batch()
            batch.update(
                userDocRef(uid),
                mapOf(
                    "diamonds" to (p.diamonds - GameCatalog.passPremiumPriceDiamonds),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            )
            batch.update(
                passRef,
                mapOf(
                    "premiumUnlocked" to true,
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            )
            batch.commit().await()
            Result.success("Storm Pass Premium unlocked!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.WRITE, path)
            Result.failure(e)
        }
    }

    suspend fun claimPassReward(level: Int, track: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid/pass/${GameCatalog.passSeasonId}"
            val passRef = userDocRef(uid).collection("pass").document(GameCatalog.passSeasonId)
            val pass = passRef.get().await().toObject(
                PassProgressDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: return Result.failure(IllegalStateException("No season"))
            val curLevel = (pass.passXp / GameCatalog.passXpPerLevel).coerceAtMost(GameCatalog.passMaxLevel)
            if (curLevel < level) return Result.failure(IllegalStateException("Reach Storm Pass Lv $level first"))
            if (track == "premium" && !pass.premiumUnlocked) {
                return Result.failure(IllegalStateException("Premium track locked"))
            }
            val key = "$level:$track"
            val claimedSet = pass.claimedKeys.split(",").filter { it.isNotBlank() }.toMutableSet()
            if (claimedSet.contains(key)) return Result.failure(IllegalStateException("Already claimed"))
            val reward = GameCatalog.passRewards.firstOrNull { it.level == level && it.track == track }
                ?: return Result.failure(IllegalStateException("No reward"))
            val profile = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            grantRewardItem(uid, profile, reward.itemId, reward.quantity)
            claimedSet.add(key)
            passRef.update(
                mapOf(
                    "claimedKeys" to claimedSet.joinToString(","),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            val itemName = GameCatalog.itemById(reward.itemId)?.name ?: reward.itemId
            Result.success("Claimed $itemName!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun claimMission(missionId: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid/missions/$missionId"
            val def = GameCatalog.missions.firstOrNull { it.id == missionId }
                ?: return Result.failure(IllegalStateException("Mission not found"))
            val mRef = userDocRef(uid).collection("missions").document(missionId)
            val prog = mRef.get().await().toObject(
                MissionProgressDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: return Result.failure(IllegalStateException("Mission not tracked"))
            if (prog.claimed) return Result.failure(IllegalStateException("Already claimed"))
            if (prog.progress < def.target) return Result.failure(IllegalStateException("Not complete yet"))

            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            mRef.update(mapOf("claimed" to true, "updatedAt" to FieldValue.serverTimestamp())).await()
            userDocRef(uid).update(
                mapOf(
                    "gold" to (p.gold + def.rewardGold),
                    "diamonds" to (p.diamonds + def.rewardDiamonds),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            addPassXpInternal(uid, def.rewardPassXp)
            addXpInternal(uid, def.rewardXp)
            Result.success("Claimed +${def.rewardGold} Gold!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun claimAchievement(achievementId: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid/achievements/$achievementId"
            val def = GameCatalog.achievements.firstOrNull { it.id == achievementId }
                ?: return Result.failure(IllegalStateException("Achievement not found"))
            val aRef = userDocRef(uid).collection("achievements").document(achievementId)
            val prog = aRef.get().await().toObject(
                AchievementProgressDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: return Result.failure(IllegalStateException("Not tracked"))
            if (prog.claimed) return Result.failure(IllegalStateException("Already claimed"))
            if (prog.progress < def.target) return Result.failure(IllegalStateException("Not unlocked yet"))

            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            aRef.update(mapOf("claimed" to true, "updatedAt" to FieldValue.serverTimestamp())).await()
            userDocRef(uid).update(
                mapOf(
                    "gold" to (p.gold + def.rewardGold),
                    "diamonds" to (p.diamonds + def.rewardDiamonds),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            Result.success("Achievement claimed!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    fun todayKey(): String = SimpleDateFormat("yyyy-MM-dd", Locale.US).format(Date())

    fun canClaimDailyLogin(profile: UserProfileDoc?): Boolean {
        if (profile == null) return false
        return profile.lastLoginClaimDate != todayKey()
    }

    suspend fun claimDailyLoginReward(): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            val today = todayKey()
            if (p.lastLoginClaimDate == today) return Result.failure(IllegalStateException("Already claimed today"))
            val nextDay = (p.loginStreakDay % 7) + 1
            val def = GameCatalog.loginRewards.first { it.day == nextDay }
            val updated = p.copy(loginStreakDay = nextDay, lastLoginClaimDate = today)
            userDocRef(uid).update(
                mapOf(
                    "loginStreakDay" to nextDay,
                    "lastLoginClaimDate" to today,
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            grantRewardItem(uid, updated, def.itemId, def.quantity)
            val name = GameCatalog.itemById(def.itemId)?.name ?: def.itemId
            Result.success("Day $nextDay reward: $name!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun setRegion(region: String): Result<Unit> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            if (region !in GameCatalog.regions) {
                return Result.failure(IllegalArgumentException("Unsupported region"))
            }
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            userDocRef(uid).update(
                mapOf("region" to region, "updatedAt" to FieldValue.serverTimestamp())
            ).await()
            syncLeaderboardProjection(uid, p.copy(region = region))
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun renameNickname(newName: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val trimmed = newName.trim()
            if (!Regex("^[\\p{L}\\p{N}_ .-]{3,16}$").matches(trimmed)) {
                return Result.failure(IllegalArgumentException("Name must be 3–16 letters, numbers, _ . -"))
            }
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            val cardRef = userDocRef(uid).collection("owned_items").document("card_name_change")
            val cardSnap = cardRef.get().await()
            val cardQty = (cardSnap.getLong("quantity") ?: 0L).toInt()
            if (cardSnap.exists() && cardQty > 0) {
                if (cardQty == 1) cardRef.delete().await()
                else cardRef.update(mapOf("quantity" to (cardQty - 1), "updatedAt" to FieldValue.serverTimestamp())).await()
                userDocRef(uid).update(
                    mapOf("nickname" to trimmed, "updatedAt" to FieldValue.serverTimestamp())
                ).await()
                syncLeaderboardProjection(uid, p.copy(nickname = trimmed))
                return Result.success("Nickname changed using Name Card")
            }
            if (p.diamonds < 99) return Result.failure(IllegalStateException("Need a Name Card or 99 Diamonds"))
            userDocRef(uid).update(
                mapOf(
                    "nickname" to trimmed,
                    "diamonds" to (p.diamonds - 99),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            syncLeaderboardProjection(uid, p.copy(nickname = trimmed, diamonds = p.diamonds - 99))
            Result.success("Nickname updated to $trimmed")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun addFriendByTag(tag: String): Result<String> {
        var path = "users"
        return try {
            val uid = requireUserId()
            val clean = tag.trim()
            if (!Regex("^[a-zA-Z0-9_\\-]{4,20}$").matches(clean)) {
                return Result.failure(IllegalArgumentException("Enter a valid Player ID (4–20 chars)"))
            }
            val p = getProfileOnce(uid)
            if (clean == p?.playerTag) return Result.failure(IllegalArgumentException("That's your own ID"))

            // Try finding real player in public leaderboard first
            val realMatch = db.collection("leaderboard")
                .whereEqualTo("publicEntry", true)
                .whereEqualTo("playerTag", clean)
                .limit(1)
                .get()
                .await()
                .documents
                .firstOrNull()
                ?.toObject(LeaderboardDoc::class.java, DocumentSnapshot.ServerTimestampBehavior.ESTIMATE)

            path = "users/$uid/friends/$clean"
            val friendRef = userDocRef(uid).collection("friends").document(clean)
            friendRef.set(
                mapOf(
                    "playerTag" to clean,
                    "nickname" to (realMatch?.nickname ?: "Fighter_${clean.takeLast(4)}"),
                    "level" to (realMatch?.level ?: Random.nextInt(3, 25)),
                    "brRankPoints" to (realMatch?.brRankPoints ?: Random.nextInt(400, 2100)),
                    "status" to "accepted",
                    "online" to true,
                    "createdAt" to FieldValue.serverTimestamp(),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            Result.success(if (realMatch != null) "Added ${realMatch.nickname}" else "Friend added")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.CREATE, path)
            Result.failure(e)
        }
    }

    suspend fun respondFriend(tag: String, accept: Boolean): Result<Unit> {
        var path = "users"
        return try {
            val uid = requireUserId()
            require(tag.isNotBlank()) { "Friend tag required" }
            path = "users/$uid/friends/$tag"
            val ref = userDocRef(uid).collection("friends").document(tag)
            if (!accept) {
                ref.delete().await()
            } else {
                ref.update(
                    mapOf(
                        "status" to "accepted",
                        "online" to true,
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                ).await()
            }
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun createGuild(name: String, tag: String, description: String): Result<String> {
        var path = "guilds"
        return try {
            val uid = requireUserId()
            val cleanName = name.trim()
            val cleanTag = tag.trim().uppercase(Locale.US)
            if (cleanName.length !in 3..20) return Result.failure(IllegalArgumentException("Guild name must be 3–20 chars"))
            if (!Regex("^[A-Z0-9]{2,5}$").matches(cleanTag)) {
                return Result.failure(IllegalArgumentException("Tag must be 2–5 letters/numbers"))
            }
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            if (!p.guildId.isNullOrBlank()) return Result.failure(IllegalStateException("Leave your current guild first"))
            if (p.gold < 2000) return Result.failure(IllegalStateException("Need 2,000 Gold to create a guild"))

            val gid = "g_${UUID.randomUUID().toString().replace("-", "").take(10)}"
            path = "guilds/$gid"
            val cleanDesc = description.trim().take(200).ifBlank { "Stormdrop squad ready to deploy." }

            val batch = db.batch()
            batch.set(
                db.collection("guilds").document(gid),
                mapOf(
                    "name" to cleanName,
                    "tag" to cleanTag,
                    "description" to cleanDesc,
                    "ownerId" to uid,
                    "memberUids" to listOf(uid),
                    "level" to 1,
                    "glory" to 100L,
                    "memberCount" to 1,
                    "maxMembers" to 30,
                    "minLevel" to 1,
                    "publicGuild" to true,
                    "createdAt" to FieldValue.serverTimestamp(),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            )
            batch.update(
                userDocRef(uid),
                mapOf(
                    "gold" to (p.gold - 2000),
                    "guildId" to gid,
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            )
            batch.commit().await()

            syncLeaderboardProjection(uid, p.copy(guildId = gid), guildTag = cleanTag)

            db.collection("guilds").document(gid).collection("messages").document().set(
                mapOf(
                    "guildId" to gid,
                    "senderId" to uid,
                    "senderName" to p.nickname,
                    "body" to "Guild [$cleanTag] created!",
                    "memberUids" to listOf(uid),
                    "createdAt" to FieldValue.serverTimestamp(),
                )
            ).await()

            Result.success("Created [$cleanTag] $cleanName!")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.CREATE, path)
            Result.failure(e)
        }
    }

    suspend fun joinGuild(guildId: String): Result<String> {
        var path = "guilds/$guildId"
        return try {
            val uid = requireUserId()
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            if (!p.guildId.isNullOrBlank()) return Result.failure(IllegalStateException("Leave your current guild first"))

            val guildRef = db.collection("guilds").document(guildId)
            val guild = guildRef.get().await().toObject(
                GuildDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: return Result.failure(IllegalStateException("Guild not found"))

            if (uid in guild.memberUids) return Result.failure(IllegalStateException("Already in this guild"))
            if (guild.memberUids.size >= guild.maxMembers) {
                return Result.failure(IllegalStateException("Guild is full (${guild.maxMembers}/${guild.maxMembers})"))
            }

            val updatedMembers = (guild.memberUids + uid).distinct()
            guildRef.update(
                mapOf(
                    "memberUids" to FieldValue.arrayUnion(uid),
                    "memberCount" to FieldValue.increment(1),
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()

            userDocRef(uid).update(
                mapOf("guildId" to guildId, "updatedAt" to FieldValue.serverTimestamp())
            ).await()
            syncLeaderboardProjection(uid, p.copy(guildId = guildId), guildTag = guild.tag)

            guildRef.collection("messages").document().set(
                mapOf(
                    "guildId" to guildId,
                    "senderId" to uid,
                    "senderName" to p.nickname,
                    "body" to "${p.nickname} joined [${guild.tag}].",
                    "memberUids" to updatedMembers,
                    "createdAt" to FieldValue.serverTimestamp(),
                )
            ).await()

            Result.success("Joined [${guild.tag}] ${guild.name}")
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    private suspend fun leaveGuildInternal(uid: String, guildId: String) {
        val guildRef = db.collection("guilds").document(guildId)
        val guild = guildRef.get().await().toObject(
            GuildDoc::class.java,
            DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
        )
        if (guild != null) {
            if (guild.ownerId == uid && guild.memberCount <= 1) {
                val msgs = guildRef.collection("messages").whereArrayContains("memberUids", uid).get().await()
                for (m in msgs.documents) {
                    if (m.getString("senderId") == uid) m.reference.delete().await()
                }
                guildRef.delete().await()
            } else if (uid in guild.memberUids && guild.memberUids.size > 1) {
                guildRef.update(
                    mapOf(
                        "memberUids" to FieldValue.arrayRemove(uid),
                        "memberCount" to FieldValue.increment(-1),
                        "updatedAt" to FieldValue.serverTimestamp(),
                    )
                ).await()
            }
        }
    }

    suspend fun leaveGuild(): Result<Unit> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val p = getProfileOnce(uid) ?: return Result.success(Unit)
            val gid = p.guildId ?: return Result.success(Unit)
            path = "guilds/$gid"
            leaveGuildInternal(uid, gid)
            userDocRef(uid).update(
                mapOf("guildId" to FieldValue.delete(), "updatedAt" to FieldValue.serverTimestamp())
            ).await()
            syncLeaderboardProjection(uid, p.copy(guildId = null), clearGuildTag = true)
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.UPDATE, path)
            Result.failure(e)
        }
    }

    suspend fun sendGuildMessage(text: String): Result<Unit> {
        var path = "guilds"
        return try {
            val uid = requireUserId()
            val body = text.trim().take(200)
            if (body.isEmpty()) return Result.success(Unit)
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            val gid = p.guildId ?: return Result.failure(IllegalStateException("Not in a guild"))
            path = "guilds/$gid/messages"
            val guild = db.collection("guilds").document(gid).get().await().toObject(
                GuildDoc::class.java,
                DocumentSnapshot.ServerTimestampBehavior.ESTIMATE
            ) ?: return Result.failure(IllegalStateException("Guild not found"))
            val members = guild.memberUids.ifEmpty { listOf(uid) }
            db.collection("guilds").document(gid).collection("messages").document().set(
                mapOf(
                    "guildId" to gid,
                    "senderId" to uid,
                    "senderName" to p.nickname.take(30),
                    "body" to body,
                    "memberUids" to members,
                    "createdAt" to FieldValue.serverTimestamp(),
                )
            ).await()
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.CREATE, path)
            Result.failure(e)
        }
    }

    suspend fun recordMatchResult(
        mode: GameMode,
        isRanked: Boolean,
        won: Boolean,
        placement: Int,
        totalTeams: Int,
        kills: Int,
        damage: Int,
        headshots: Int,
        survivalSeconds: Int,
    ): Result<MatchResultSummary> {
        var path = "users"
        return try {
            val uid = requireUserId()
            path = "users/$uid"
            val p = getProfileOnce(uid) ?: return Result.failure(IllegalStateException("No profile"))
            if (mode == GameMode.TRAINING) {
                return Result.success(
                    MatchResultSummary(
                        mode = mode,
                        isRanked = false,
                        won = won,
                        placement = placement,
                        totalTeams = totalTeams,
                        kills = kills,
                        damage = damage,
                        headshots = headshots,
                        survivalSeconds = survivalSeconds,
                        xpGained = 0,
                        goldGained = 0,
                        passXpGained = 0,
                        rankDelta = 0,
                        levelsGained = emptyList(),
                    )
                )
            }

            val doubleXp = System.currentTimeMillis() < p.doubleXpUntilMs
            val baseGold = (25 + kills * 12 + if (won) 120 else if (placement <= 10) 40 else 0).coerceAtMost(400)
            val baseXp = (60 + kills * 20 + survivalSeconds / 6 + if (won) 120 else 0) * (if (doubleXp) 2 else 1)
            val passXp = 120 + kills * 25 + if (won) 200 else 0

            val rankDelta = when {
                !isRanked -> 0
                mode.isBattleRoyale -> RankCalculator.brRankDelta(p.brRankPoints, placement, totalTeams, kills)
                mode == GameMode.CLASH_SQUAD -> RankCalculator.csRankDelta(p.csRankPoints, won, kills)
                else -> 0
            }

            val nextBr = if (isRanked && mode.isBattleRoyale) (p.brRankPoints + rankDelta).coerceAtLeast(0) else p.brRankPoints
            val nextCs = if (isRanked && mode == GameMode.CLASH_SQUAD) (p.csRankPoints + rankDelta).coerceAtLeast(0) else p.csRankPoints

            userDocRef(uid).update(
                mapOf(
                    "gold" to (p.gold + baseGold),
                    "brRankPoints" to nextBr,
                    "csRankPoints" to nextCs,
                    "updatedAt" to FieldValue.serverTimestamp(),
                )
            ).await()

            val (_, levelsGained) = addXpInternal(uid, baseXp)
            addPassXpInternal(uid, passXp)

            val mins = (survivalSeconds / 60).coerceAtLeast(if (survivalSeconds >= 20) 1 else 0)
            val existingMissions = userDocRef(uid).collection("missions").get().await()
                .toObjects(MissionProgressDoc::class.java, DocumentSnapshot.ServerTimestampBehavior.ESTIMATE)
                .associateBy { it.missionId }

            val existingAchievements = userDocRef(uid).collection("achievements").get().await()
                .toObjects(AchievementProgressDoc::class.java, DocumentSnapshot.ServerTimestampBehavior.ESTIMATE)
                .associateBy { it.achievementId }

            val batch = db.batch()
            for (def in GameCatalog.missions) {
                val cur = existingMissions[def.id]
                val add = when (def.metric) {
                    "matches" -> 1
                    "kills" -> kills
                    "damage" -> damage
                    "survival_minutes" -> mins
                    "top10" -> if (placement <= 10) 1 else 0
                    "wins" -> if (won) 1 else 0
                    "headshots" -> headshots
                    else -> 0
                }
                if (add > 0) {
                    val mRef = userDocRef(uid).collection("missions").document(def.id)
                    if (cur == null) {
                        batch.set(
                            mRef,
                            mapOf(
                                "missionId" to def.id,
                                "progress" to add,
                                "claimed" to false,
                                "createdAt" to FieldValue.serverTimestamp(),
                                "updatedAt" to FieldValue.serverTimestamp(),
                            )
                        )
                    } else {
                        batch.update(
                            mRef,
                            mapOf(
                                "progress" to (cur.progress + add),
                                "updatedAt" to FieldValue.serverTimestamp(),
                            )
                        )
                    }
                }
            }

            for (def in GameCatalog.achievements) {
                val cur = existingAchievements[def.id]
                val add = when (def.metric) {
                    "matches" -> 1
                    "kills" -> kills
                    "wins" -> if (won) 1 else 0
                    else -> 0
                }
                if (add > 0) {
                    val aRef = userDocRef(uid).collection("achievements").document(def.id)
                    if (cur == null) {
                        batch.set(
                            aRef,
                            mapOf(
                                "achievementId" to def.id,
                                "progress" to add,
                                "claimed" to false,
                                "createdAt" to FieldValue.serverTimestamp(),
                                "updatedAt" to FieldValue.serverTimestamp(),
                            )
                        )
                    } else {
                        batch.update(
                            aRef,
                            mapOf(
                                "progress" to (cur.progress + add),
                                "updatedAt" to FieldValue.serverTimestamp(),
                            )
                        )
                    }
                }
            }

            path = "users/$uid/match_history"
            batch.set(
                userDocRef(uid).collection("match_history").document(),
                mapOf(
                    "modeId" to mode.apiId,
                    "ranked" to isRanked,
                    "won" to won,
                    "placement" to placement.coerceIn(1, 100),
                    "totalTeams" to totalTeams.coerceIn(1, 100),
                    "kills" to kills.coerceIn(0, 100),
                    "damage" to damage.coerceIn(0, 100000),
                    "headshots" to headshots.coerceIn(0, 100),
                    "survivalSeconds" to survivalSeconds.coerceIn(0, 7200),
                    "rankDelta" to rankDelta.coerceIn(-200, 200),
                    "xpGained" to baseXp.coerceAtLeast(0),
                    "goldGained" to baseGold.coerceAtLeast(0),
                    "createdAt" to FieldValue.serverTimestamp(),
                )
            )
            batch.commit().await()

            Result.success(
                MatchResultSummary(
                    mode = mode,
                    isRanked = isRanked,
                    won = won,
                    placement = placement,
                    totalTeams = totalTeams,
                    kills = kills,
                    damage = damage,
                    headshots = headshots,
                    survivalSeconds = survivalSeconds,
                    xpGained = baseXp,
                    goldGained = baseGold,
                    passXpGained = passXp,
                    rankDelta = rankDelta,
                    levelsGained = levelsGained,
                )
            )
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            handleFirestoreError(e, OperationType.WRITE, path)
            Result.failure(e)
        }
    }
}
