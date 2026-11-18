package com.example.ui

import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.data.CloudHealthState
import com.example.data.SpinWinResult
import com.example.data.StormdropRepository
import com.example.data.local.PlayerSettingsState
import com.example.data.model.CatalogItem
import com.example.data.model.DiamondPack
import com.example.data.model.GameCatalog
import com.example.data.model.GameMode
import com.example.data.model.LeaderboardEntry
import com.example.data.model.MatchResultSummary
import com.example.data.remote.AchievementProgressDoc
import com.example.data.remote.FriendDoc
import com.example.data.remote.GuildDoc
import com.example.data.remote.GuildMessageDoc
import com.example.data.remote.LeaderboardDoc
import com.example.data.remote.MatchHistoryDoc
import com.example.data.remote.MissionProgressDoc
import com.example.data.remote.OwnedItemDoc
import com.example.data.remote.PassProgressDoc
import com.example.data.remote.UiState
import com.example.data.remote.UserProfileDoc
import com.google.firebase.firestore.FirebaseFirestoreException
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

@OptIn(ExperimentalCoroutinesApi::class)
class StormdropViewModel(
    private val repository: StormdropRepository,
    val currentUserId: String,
) : ViewModel() {

    private fun <T> Flow<T>.toUiState(label: String): StateFlow<UiState<T>> =
        map<T, UiState<T>> { UiState.Success(it) }
            .catch { error ->
                val firestoreError = generateSequence(error) { it.cause }
                    .filterIsInstance<FirebaseFirestoreException>()
                    .firstOrNull()
                if (firestoreError == null && error is CancellationException) throw error
                val rootError = firestoreError ?: error
                Log.w(TAG, "Error observing $label", rootError)
                emit(UiState.Error(rootError.localizedMessage ?: "Failed to load $label"))
            }
            .stateIn(
                scope = viewModelScope,
                started = SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS),
                initialValue = UiState.Loading,
            )

    val profileState: StateFlow<UiState<UserProfileDoc?>> =
        repository.observeProfile(currentUserId).toUiState("profile")

    val profile: StateFlow<UserProfileDoc?> = profileState
        .map { (it as? UiState.Success)?.data }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), null)

    val ownedItemsState: StateFlow<UiState<List<OwnedItemDoc>>> =
        repository.observeOwnedItems(currentUserId).toUiState("owned items")

    val ownedItems: StateFlow<List<OwnedItemDoc>> = ownedItemsState
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val missionsState: StateFlow<UiState<List<MissionProgressDoc>>> =
        repository.observeMissions(currentUserId).toUiState("missions")

    val missions: StateFlow<List<MissionProgressDoc>> = missionsState
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val achievementsState: StateFlow<UiState<List<AchievementProgressDoc>>> =
        repository.observeAchievements(currentUserId).toUiState("achievements")

    val achievements: StateFlow<List<AchievementProgressDoc>> = achievementsState
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val passProgress: StateFlow<PassProgressDoc?> = repository.observePass(currentUserId)
        .toUiState("storm pass")
        .map { (it as? UiState.Success)?.data }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), null)

    val matchHistory: StateFlow<List<MatchHistoryDoc>> = repository.observeMatchHistory(currentUserId)
        .toUiState("match history")
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val friends: StateFlow<List<FriendDoc>> = repository.observeFriends(currentUserId)
        .toUiState("friends")
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val remoteLeaderboard: StateFlow<List<LeaderboardDoc>> = repository.observeLeaderboard(currentUserId)
        .toUiState("leaderboard")
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val guilds: StateFlow<List<GuildDoc>> = repository.observeGuilds(currentUserId)
        .toUiState("guilds")
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    val settings: StateFlow<PlayerSettingsState> = repository.settingsFlow
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), PlayerSettingsState())

    val guildChat: StateFlow<List<GuildMessageDoc>> = profile
        .flatMapLatest { p ->
            val gid = p?.guildId
            if (gid.isNullOrBlank()) flowOf(emptyList())
            else repository.observeGuildChat(currentUserId, gid)
        }
        .toUiState("guild chat")
        .map { (it as? UiState.Success)?.data ?: emptyList() }
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MILLIS), emptyList())

    private val _selectedMode = MutableStateFlow(GameMode.BR_SOLO)
    val selectedMode: StateFlow<GameMode> = _selectedMode.asStateFlow()

    private val _isRanked = MutableStateFlow(false)
    val isRanked: StateFlow<Boolean> = _isRanked.asStateFlow()

    private val _partyMembers = MutableStateFlow<List<String>>(emptyList())
    val partyMembers: StateFlow<List<String>> = _partyMembers.asStateFlow()

    private val _cloudHealth = MutableStateFlow(CloudHealthState())
    val cloudHealth: StateFlow<CloudHealthState> = _cloudHealth.asStateFlow()

    private val _lastResult = MutableStateFlow<MatchResultSummary?>(null)
    val lastResult: StateFlow<MatchResultSummary?> = _lastResult.asStateFlow()

    private val _spinResults = MutableStateFlow<List<SpinWinResult>?>(null)
    val spinResults: StateFlow<List<SpinWinResult>?> = _spinResults.asStateFlow()

    private val _toastEvents = MutableSharedFlow<String>(extraBufferCapacity = 8)
    val toastEvents = _toastEvents.asSharedFlow()

    init {
        viewModelScope.launch {
            repository.ensureUserInitialized(currentUserId)
                .onSuccess {
                    _cloudHealth.value = CloudHealthState(
                        firebaseConnected = true,
                        summary = "Firebase Cloud Sync Active"
                    )
                }
                .onFailure { err ->
                    _cloudHealth.value = CloudHealthState(
                        firebaseConnected = false,
                        summary = err.localizedMessage ?: "Cloud Sync Pending"
                    )
                    emitToast(err.localizedMessage ?: "Failed to sync profile")
                }
        }
    }

    fun emitToast(msg: String) {
        _toastEvents.tryEmit(msg)
    }

    fun deleteAccount(onDone: () -> Unit) {
        viewModelScope.launch {
            repository.deleteAccount(currentUserId)
                .onSuccess {
                    emitToast("Account permanently deleted")
                    onDone()
                }
                .onFailure { emitToast(it.message ?: "Failed to delete account") }
        }
    }

    fun selectMode(mode: GameMode) {
        _selectedMode.value = mode
        if (mode == GameMode.TRAINING || mode == GameMode.TEAM_DEATHMATCH) {
            _isRanked.value = false
        }
    }

    fun toggleRanked(): Boolean {
        val lvl = profile.value?.level ?: 1
        if (!_isRanked.value && lvl < 6) {
            emitToast("Ranked unlocks at Level 6 (currently Lv $lvl)")
            return false
        }
        val mode = _selectedMode.value
        if (mode == GameMode.TRAINING || mode == GameMode.TEAM_DEATHMATCH) {
            emitToast("Ranked is only available in BR or Clash Squad")
            return false
        }
        _isRanked.value = !_isRanked.value
        return true
    }

    fun cycleCharacter(direction: Int) {
        viewModelScope.launch { repository.cycleCharacter(direction) }
    }

    fun buyItem(item: CatalogItem, currency: String) {
        viewModelScope.launch {
            repository.buyItem(item, currency)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Purchase failed") }
        }
    }

    fun purchaseDiamondPack(pack: DiamondPack) {
        emitToast(repository.requestDiamondPackPlaceholder(pack))
    }

    fun equipItem(item: CatalogItem) {
        viewModelScope.launch {
            repository.equipItem(item)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Unable to equip") }
        }
    }

    fun spinFortuneDrop(count: Int) {
        viewModelScope.launch {
            repository.spinFortuneDrop(count)
                .onSuccess { _spinResults.value = it }
                .onFailure { emitToast(it.message ?: "Spin failed") }
        }
    }

    fun dismissSpinResults() {
        _spinResults.value = null
    }

    fun unlockPremiumPass() {
        viewModelScope.launch {
            repository.unlockPremiumPass()
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Failed") }
        }
    }

    fun claimPassReward(level: Int, track: String) {
        viewModelScope.launch {
            repository.claimPassReward(level, track)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Failed") }
        }
    }

    fun claimMission(missionId: String) {
        viewModelScope.launch {
            repository.claimMission(missionId)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Failed") }
        }
    }

    fun claimAchievement(achievementId: String) {
        viewModelScope.launch {
            repository.claimAchievement(achievementId)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Failed") }
        }
    }

    fun canClaimDailyLogin(): Boolean = repository.canClaimDailyLogin(profile.value)

    fun claimDailyLogin() {
        viewModelScope.launch {
            repository.claimDailyLoginReward()
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Already claimed") }
        }
    }

    fun setRegion(region: String) {
        viewModelScope.launch {
            repository.setRegion(region)
                .onFailure { emitToast(it.message ?: "Failed to update region") }
        }
    }

    fun renameNickname(name: String) {
        viewModelScope.launch {
            repository.renameNickname(name)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Invalid name") }
        }
    }

    fun updateSettings(transform: (PlayerSettingsState) -> PlayerSettingsState) {
        viewModelScope.launch { repository.updateSettings(transform) }
    }

    fun inviteToParty(nickname: String) {
        val current = _partyMembers.value
        if (current.size >= 3) {
            emitToast("Party is full (max 4)")
            return
        }
        if (nickname !in current) {
            _partyMembers.value = current + nickname
            emitToast("$nickname joined your squad")
        }
    }

    fun leaveParty() {
        _partyMembers.value = emptyList()
        emitToast("Left squad")
    }

    fun addFriend(tag: String) {
        viewModelScope.launch {
            repository.addFriendByTag(tag)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Could not add friend") }
        }
    }

    fun respondFriend(tag: String, accept: Boolean) {
        viewModelScope.launch {
            repository.respondFriend(tag, accept)
                .onSuccess { emitToast(if (accept) "Friend accepted" else "Request declined") }
                .onFailure { emitToast(it.message ?: "Action failed") }
        }
    }

    fun createGuild(name: String, tag: String, desc: String) {
        viewModelScope.launch {
            repository.createGuild(name, tag, desc)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Could not create guild") }
        }
    }

    fun joinGuild(guildId: String) {
        viewModelScope.launch {
            repository.joinGuild(guildId)
                .onSuccess { emitToast(it) }
                .onFailure { emitToast(it.message ?: "Failed to join") }
        }
    }

    fun leaveGuild() {
        viewModelScope.launch {
            repository.leaveGuild()
                .onSuccess { emitToast("Left guild") }
                .onFailure { emitToast(it.message ?: "Failed to leave guild") }
        }
    }

    fun sendGuildMessage(body: String) {
        viewModelScope.launch {
            repository.sendGuildMessage(body)
                .onFailure { emitToast(it.message ?: "Message failed") }
        }
    }

    fun buildLeaderboard(region: String): List<LeaderboardEntry> {
        val byTag = linkedMapOf<String, LeaderboardEntry>()
        for (bot in GameCatalog.defaultBotsLeaderboard) {
            byTag[bot.playerTag] = bot
        }
        for (doc in remoteLeaderboard.value) {
            byTag[doc.playerTag] = LeaderboardEntry(
                rank = 0,
                playerTag = doc.playerTag,
                nickname = doc.nickname,
                level = doc.level,
                brRankPoints = doc.brRankPoints,
                guildTag = doc.guildTag,
                region = doc.region,
                isSelf = doc.userId == currentUserId,
            )
        }
        return byTag.values
            .filter { region == "ALL" || it.region == region }
            .sortedByDescending { it.brRankPoints }
            .mapIndexed { index, entry -> entry.copy(rank = index + 1) }
    }

    fun completeMatch(
        mode: GameMode,
        isRanked: Boolean,
        won: Boolean,
        placement: Int,
        totalTeams: Int,
        kills: Int,
        damage: Int,
        headshots: Int,
        survivalSeconds: Int,
        onSettled: () -> Unit,
    ) {
        viewModelScope.launch {
            repository.recordMatchResult(
                mode = mode,
                isRanked = isRanked,
                won = won,
                placement = placement,
                totalTeams = totalTeams,
                kills = kills,
                damage = damage,
                headshots = headshots,
                survivalSeconds = survivalSeconds,
            ).onSuccess { summary ->
                _lastResult.value = summary
                onSettled()
            }.onFailure { err ->
                emitToast(err.message ?: "Failed to save match stats")
                onSettled()
            }
        }
    }

    private companion object {
        const val TAG = "StormdropVM"
        const val STOP_TIMEOUT_MILLIS = 5000L
    }
}
