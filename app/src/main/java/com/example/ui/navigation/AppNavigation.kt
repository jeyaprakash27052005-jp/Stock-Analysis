package com.example.ui.navigation

import android.app.Application
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.credentials.CredentialManager
import androidx.lifecycle.ViewModelProvider.AndroidViewModelFactory.Companion.APPLICATION_KEY
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.lifecycle.viewmodel.initializer
import androidx.lifecycle.viewmodel.viewModelFactory
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.example.R
import com.example.StormdropApplication
import com.example.data.StormdropRepository
import com.example.data.local.SettingsDataStore
import com.example.data.remote.signOutFromGoogleAndFirebase
import com.example.ui.StormdropViewModel
import com.example.ui.match.MatchScreen
import com.example.ui.screens.AuthScreen
import com.example.ui.screens.LeaderboardScreen
import com.example.ui.screens.LobbyScreen
import com.example.ui.screens.MatchmakingScreen
import com.example.ui.screens.MissionsEventsScreen
import com.example.ui.screens.ModeSelectScreen
import com.example.ui.screens.PassScreen
import com.example.ui.screens.ResultsScreen
import com.example.ui.screens.SettingsScreen
import com.example.ui.screens.SocialScreen
import com.example.ui.screens.StoreScreen
import com.example.ui.screens.VaultScreen
import com.example.ui.theme.StormdropColors
import com.google.firebase.Firebase
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.auth.FirebaseUser
import com.google.firebase.auth.auth
import com.google.firebase.firestore.FirebaseFirestore
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow
import kotlinx.serialization.Serializable

sealed interface Route {
    @Serializable data object Lobby : Route
    @Serializable data object ModeSelect : Route
    @Serializable data object Matchmaking : Route
    @Serializable data object Match : Route
    @Serializable data object Results : Route
    @Serializable data object Store : Route
    @Serializable data object Vault : Route
    @Serializable data object Pass : Route
    @Serializable data object Missions : Route
    @Serializable data object Social : Route
    @Serializable data object Leaderboard : Route
    @Serializable data object Settings : Route
}

internal fun FirebaseAuth.authStateFlow(): Flow<FirebaseUser?> = callbackFlow {
    val listener = FirebaseAuth.AuthStateListener { firebaseAuth ->
        trySend(firebaseAuth.currentUser)
    }
    addAuthStateListener(listener)
    awaitClose { removeAuthStateListener(listener) }
}

@Composable
fun AppNavigation(auth: FirebaseAuth = Firebase.auth) {
    val currentUser by auth.authStateFlow().collectAsStateWithLifecycle(initialValue = auth.currentUser)
    val user = currentUser

    if (user == null) {
        AuthScreen(onAuthSuccess = {})
    } else {
        key(user.uid) {
            AuthenticatedNavHost(currentUserId = user.uid)
        }
    }
}

@Composable
private fun AuthenticatedNavHost(currentUserId: String) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val credentialManager = remember(context) { CredentialManager.create(context) }

    val viewModel: StormdropViewModel = viewModel(
        key = currentUserId,
        factory = viewModelFactory {
            initializer {
                val app = checkNotNull(this[APPLICATION_KEY]) as Application
                val databaseId = app.getString(R.string.firestore_database_id)
                val db = FirebaseFirestore.getInstance(databaseId)
                val settingsStore = (app as? StormdropApplication)?.settingsStore ?: SettingsDataStore(app)
                StormdropViewModel(
                    repository = StormdropRepository(db = db, settingsStore = settingsStore),
                    currentUserId = currentUserId,
                )
            }
        },
    )

    val navController = rememberNavController()
    val snackbarHostState = remember { SnackbarHostState() }

    val profile by viewModel.profile.collectAsStateWithLifecycle()
    val ownedItems by viewModel.ownedItems.collectAsStateWithLifecycle()
    val missions by viewModel.missions.collectAsStateWithLifecycle()
    val achievements by viewModel.achievements.collectAsStateWithLifecycle()
    val passProgress by viewModel.passProgress.collectAsStateWithLifecycle()
    val matchHistory by viewModel.matchHistory.collectAsStateWithLifecycle()
    val friends by viewModel.friends.collectAsStateWithLifecycle()
    val guilds by viewModel.guilds.collectAsStateWithLifecycle()
    val guildChat by viewModel.guildChat.collectAsStateWithLifecycle()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val selectedMode by viewModel.selectedMode.collectAsStateWithLifecycle()
    val isRanked by viewModel.isRanked.collectAsStateWithLifecycle()
    val partyMembers by viewModel.partyMembers.collectAsStateWithLifecycle()
    val cloudHealth by viewModel.cloudHealth.collectAsStateWithLifecycle()
    val lastResult by viewModel.lastResult.collectAsStateWithLifecycle()
    val spinResults by viewModel.spinResults.collectAsStateWithLifecycle()

    LaunchedEffect(viewModel) {
        viewModel.toastEvents.collect { message ->
            snackbarHostState.showSnackbar(message)
        }
    }

    Scaffold(
        containerColor = StormdropColors.BgDeep,
        contentWindowInsets = WindowInsets.safeDrawing,
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding),
        ) {
            NavHost(
                navController = navController,
                startDestination = Route.Lobby,
            ) {
                composable<Route.Lobby> {
                    LobbyScreen(
                        profile = profile,
                        guilds = guilds,
                        selectedMode = selectedMode,
                        isRanked = isRanked,
                        partyMembers = partyMembers,
                        cloudHealth = cloudHealth,
                        canClaimDailyLogin = viewModel.canClaimDailyLogin(),
                        onCycleCharacter = { viewModel.cycleCharacter(it) },
                        onClaimDailyLogin = { viewModel.claimDailyLogin() },
                        onToggleRanked = { viewModel.toggleRanked() },
                        onOpenModeSelect = { navController.navigate(Route.ModeSelect) },
                        onDeployMatch = {
                            if (selectedMode == com.example.data.model.GameMode.TRAINING) {
                                navController.navigate(Route.Match)
                            } else {
                                navController.navigate(Route.Matchmaking)
                            }
                        },
                        onLeaveParty = { viewModel.leaveParty() },
                        onNavigateStore = { navController.navigate(Route.Store) },
                        onNavigateVault = { navController.navigate(Route.Vault) },
                        onNavigatePass = { navController.navigate(Route.Pass) },
                        onNavigateMissions = { navController.navigate(Route.Missions) },
                        onNavigateSocial = { navController.navigate(Route.Social) },
                        onNavigateLeaderboard = { navController.navigate(Route.Leaderboard) },
                        onNavigateSettings = { navController.navigate(Route.Settings) },
                    )
                }

                composable<Route.ModeSelect> {
                    ModeSelectScreen(
                        selectedMode = selectedMode,
                        gold = profile?.gold ?: 0,
                        diamonds = profile?.diamonds ?: 0,
                        onSelectMode = { viewModel.selectMode(it) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Matchmaking> {
                    MatchmakingScreen(
                        mode = selectedMode,
                        isRanked = isRanked,
                        region = profile?.region ?: "IND",
                        partyCount = partyMembers.size,
                        onMatchReady = {
                            navController.navigate(Route.Match) {
                                popUpTo(Route.Matchmaking) { inclusive = true }
                            }
                        },
                        onCancel = { navController.popBackStack() },
                    )
                }

                composable<Route.Match> {
                    MatchScreen(
                        mode = selectedMode,
                        isRanked = isRanked,
                        profile = profile,
                        settings = settings,
                        onMatchFinished = { won, placement, totalTeams, kills, damage, headshots, survivalSeconds ->
                            viewModel.completeMatch(
                                mode = selectedMode,
                                isRanked = isRanked,
                                won = won,
                                placement = placement,
                                totalTeams = totalTeams,
                                kills = kills,
                                damage = damage,
                                headshots = headshots,
                                survivalSeconds = survivalSeconds,
                                onSettled = {
                                    navController.navigate(Route.Results) {
                                        popUpTo(Route.Match) { inclusive = true }
                                    }
                                },
                            )
                        },
                    )
                }

                composable<Route.Results> {
                    ResultsScreen(
                        summary = lastResult,
                        onPlayAgain = {
                            navController.navigate(Route.Matchmaking) {
                                popUpTo(Route.Results) { inclusive = true }
                            }
                        },
                        onReturnLobby = {
                            navController.popBackStack(Route.Lobby, inclusive = false)
                        },
                    )
                }

                composable<Route.Store> {
                    StoreScreen(
                        gold = profile?.gold ?: 0,
                        diamonds = profile?.diamonds ?: 0,
                        ownedItems = ownedItems,
                        spinResults = spinResults,
                        onBuyItem = { item, currency -> viewModel.buyItem(item, currency) },
                        onSpinFortune = { count -> viewModel.spinFortuneDrop(count) },
                        onDismissSpinResults = { viewModel.dismissSpinResults() },
                        onPurchaseDiamonds = { pack -> viewModel.purchaseDiamondPack(pack) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Vault> {
                    VaultScreen(
                        profile = profile,
                        ownedItems = ownedItems,
                        onEquipItem = { viewModel.equipItem(it) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Pass> {
                    PassScreen(
                        gold = profile?.gold ?: 0,
                        diamonds = profile?.diamonds ?: 0,
                        passProgress = passProgress,
                        onUnlockPremium = { viewModel.unlockPremiumPass() },
                        onClaimReward = { lvl, track -> viewModel.claimPassReward(lvl, track) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Missions> {
                    MissionsEventsScreen(
                        gold = profile?.gold ?: 0,
                        diamonds = profile?.diamonds ?: 0,
                        missions = missions,
                        achievements = achievements,
                        onClaimMission = { viewModel.claimMission(it) },
                        onClaimAchievement = { viewModel.claimAchievement(it) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Social> {
                    SocialScreen(
                        profile = profile,
                        friends = friends,
                        guilds = guilds,
                        guildChat = guildChat,
                        onAddFriend = { viewModel.addFriend(it) },
                        onRespondFriend = { tag, accept -> viewModel.respondFriend(tag, accept) },
                        onInviteParty = { viewModel.inviteToParty(it) },
                        onCreateGuild = { name, tag, desc -> viewModel.createGuild(name, tag, desc) },
                        onJoinGuild = { viewModel.joinGuild(it) },
                        onLeaveGuild = { viewModel.leaveGuild() },
                        onSendGuildMessage = { viewModel.sendGuildMessage(it) },
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Leaderboard> {
                    LeaderboardScreen(
                        gold = profile?.gold ?: 0,
                        diamonds = profile?.diamonds ?: 0,
                        buildLeaderboard = { reg -> viewModel.buildLeaderboard(reg) },
                        matchHistory = matchHistory,
                        onBack = { navController.popBackStack() },
                    )
                }

                composable<Route.Settings> {
                    SettingsScreen(
                        profile = profile,
                        settings = settings,
                        cloudHealth = cloudHealth,
                        onRenameNickname = { viewModel.renameNickname(it) },
                        onSetRegion = { viewModel.setRegion(it) },
                        onUpdateSettings = { viewModel.updateSettings(it) },
                        onSignOut = {
                            signOutFromGoogleAndFirebase(credentialManager = credentialManager, scope = scope)
                        },
                        onDeleteAccount = {
                            viewModel.deleteAccount {
                                signOutFromGoogleAndFirebase(credentialManager = credentialManager, scope = scope)
                            }
                        },
                        onBack = { navController.popBackStack() },
                    )
                }
            }
        }
    }
}
