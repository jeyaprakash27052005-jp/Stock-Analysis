package com.example.data.remote

import androidx.annotation.Keep
import com.google.firebase.Timestamp
import com.google.firebase.firestore.DocumentId
import com.google.firebase.firestore.IgnoreExtraProperties
import com.google.firebase.firestore.ServerTimestamp

@Keep
@IgnoreExtraProperties
data class UserProfileDoc(
    @DocumentId val id: String = "",
    val userId: String = "",
    val playerTag: String = "",
    val nickname: String = "Dropper1001",
    val region: String = "IND",
    val level: Int = 1,
    val xp: Int = 0,
    val gold: Int = 5000,
    val diamonds: Int = 50,
    val brRankPoints: Int = 0,
    val csRankPoints: Int = 0,
    val equippedCharacterId: String = "char_kael",
    val equippedPetId: String? = null,
    val equippedParachuteId: String? = "chute_classic_blue",
    val equippedWeaponSkinId: String? = null,
    val guildId: String? = null,
    val loginStreakDay: Int = 0,
    val lastLoginClaimDate: String? = null,
    val doubleXpUntilMs: Long = 0L,
    val authProvider: String = "google",
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class OwnedItemDoc(
    @DocumentId val id: String = "",
    val itemId: String = "",
    val quantity: Int = 1,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class MissionProgressDoc(
    @DocumentId val id: String = "",
    val missionId: String = "",
    val progress: Int = 0,
    val claimed: Boolean = false,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class AchievementProgressDoc(
    @DocumentId val id: String = "",
    val achievementId: String = "",
    val progress: Int = 0,
    val claimed: Boolean = false,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class PassProgressDoc(
    @DocumentId val id: String = "",
    val seasonId: String = "s1",
    val passXp: Int = 0,
    val premiumUnlocked: Boolean = false,
    val claimedKeys: String = "",
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class MatchHistoryDoc(
    @DocumentId val id: String = "",
    val modeId: String = "",
    val ranked: Boolean = false,
    val won: Boolean = false,
    val placement: Int = 1,
    val totalTeams: Int = 1,
    val kills: Int = 0,
    val damage: Int = 0,
    val headshots: Int = 0,
    val survivalSeconds: Int = 0,
    val rankDelta: Int = 0,
    val xpGained: Int = 0,
    val goldGained: Int = 0,
    @ServerTimestamp val createdAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class FriendDoc(
    @DocumentId val id: String = "",
    val playerTag: String = "",
    val nickname: String = "",
    val level: Int = 1,
    val brRankPoints: Int = 0,
    val status: String = "accepted",
    val online: Boolean = false,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class LeaderboardDoc(
    @DocumentId val id: String = "",
    val userId: String = "",
    val playerTag: String = "",
    val nickname: String = "",
    val region: String = "IND",
    val level: Int = 1,
    val brRankPoints: Int = 0,
    val csRankPoints: Int = 0,
    val guildTag: String? = null,
    val publicEntry: Boolean = true,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class GuildDoc(
    @DocumentId val id: String = "",
    val name: String = "",
    val tag: String = "",
    val description: String = "",
    val ownerId: String = "",
    val memberUids: List<String> = emptyList(),
    val level: Int = 1,
    val glory: Long = 100L,
    val memberCount: Int = 1,
    val maxMembers: Int = 30,
    val minLevel: Int = 1,
    val publicGuild: Boolean = true,
    @ServerTimestamp val createdAt: Timestamp? = null,
    @ServerTimestamp val updatedAt: Timestamp? = null,
)

@Keep
@IgnoreExtraProperties
data class GuildMessageDoc(
    @DocumentId val id: String = "",
    val guildId: String = "",
    val senderId: String = "",
    val senderName: String = "",
    val body: String = "",
    val memberUids: List<String> = emptyList(),
    @ServerTimestamp val createdAt: Timestamp? = null,
)
