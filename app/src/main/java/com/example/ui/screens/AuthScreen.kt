package com.example.ui.screens

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudDone
import androidx.compose.material.icons.filled.GppGood
import androidx.compose.material.icons.filled.Login
import androidx.compose.material.icons.filled.SportsEsports
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.credentials.CredentialManager
import com.example.R
import com.example.data.remote.attemptAutoSignIn
import com.example.data.remote.onGoogleSignInClicked
import com.example.ui.theme.StormdropColors

@Composable
fun AuthScreen(
    onAuthSuccess: () -> Unit,
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val credentialManager = remember(context) { CredentialManager.create(context) }

    var isCheckingAuto by remember { mutableStateOf(true) }
    var isSigningIn by remember { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(Unit) {
        attemptAutoSignIn(
            context = context,
            credentialManager = credentialManager,
            onAuthSuccess = {
                isCheckingAuto = false
                onAuthSuccess()
            },
            onUnauthenticated = {
                isCheckingAuto = false
            },
            scope = scope,
        )
    }

    Box(
        modifier = Modifier
            .fillMaxSize()
            .background(StormdropColors.BgDeep),
    ) {
        Image(
            painter = painterResource(id = R.drawable.img_hero_island),
            contentDescription = "Isle Verdant battleground",
            contentScale = ContentScale.Crop,
            modifier = Modifier.fillMaxSize(),
            alpha = 0.42f,
        )
        Box(
            modifier = Modifier
                .fillMaxSize()
                .background(
                    Brush.verticalGradient(
                        listOf(
                            StormdropColors.BgDeep.copy(alpha = 0.45f),
                            StormdropColors.BgDeep.copy(alpha = 0.85f),
                            StormdropColors.BgDeep,
                        )
                    )
                ),
        )

        Column(
            modifier = Modifier
                .fillMaxSize()
                .windowInsetsPadding(WindowInsets.safeDrawing)
                .verticalScroll(rememberScrollState())
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.SpaceBetween,
        ) {
            Spacer(Modifier.height(16.dp))

            Column(
                modifier = Modifier.widthIn(max = 520.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Surface(
                    color = StormdropColors.AccentOrange.copy(alpha = 0.16f),
                    shape = RoundedCornerShape(24.dp),
                    border = BorderStroke(1.dp, StormdropColors.AccentOrange.copy(alpha = 0.55f)),
                ) {
                    Row(
                        modifier = Modifier.padding(horizontal = 14.dp, vertical = 6.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(
                            imageVector = Icons.Filled.SportsEsports,
                            contentDescription = null,
                            tint = StormdropColors.AccentOrange,
                            modifier = Modifier.size(16.dp),
                        )
                        Spacer(Modifier.width(6.dp))
                        Text(
                            text = "SEASON 1 · ISLE VERDANT",
                            style = MaterialTheme.typography.labelMedium,
                            color = StormdropColors.AccentOrange,
                        )
                    }
                }

                Spacer(Modifier.height(18.dp))

                Text(
                    text = "STORMDROP ROYALE",
                    style = MaterialTheme.typography.displaySmall,
                    color = StormdropColors.TextMain,
                    textAlign = TextAlign.Center,
                )
                Spacer(Modifier.height(8.dp))
                Text(
                    text = stringResource(id = R.string.tagline),
                    style = MaterialTheme.typography.bodyLarge,
                    color = StormdropColors.TextDim,
                    textAlign = TextAlign.Center,
                )
            }

            Surface(
                modifier = Modifier
                    .widthIn(max = 460.dp)
                    .fillMaxWidth()
                    .padding(vertical = 24.dp),
                color = StormdropColors.BgSurface.copy(alpha = 0.94f),
                shape = RoundedCornerShape(22.dp),
                border = BorderStroke(1.dp, StormdropColors.Divider),
            ) {
                Column(
                    modifier = Modifier.padding(22.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Icon(
                            imageVector = Icons.Filled.CloudDone,
                            contentDescription = "Firebase Sync",
                            tint = StormdropColors.AccentCyan,
                            modifier = Modifier.size(20.dp),
                        )
                        Text(
                            text = "Firebase Cloud Account",
                            style = MaterialTheme.typography.titleMedium,
                            color = StormdropColors.TextMain,
                        )
                    }

                    Spacer(Modifier.height(10.dp))

                    Text(
                        text = "Sign in with Google to save your rank, Storm Pass, Vault items, guilds, and match stats to Firebase.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = StormdropColors.TextDim,
                        textAlign = TextAlign.Center,
                    )

                    if (!errorMessage.isNullOrBlank()) {
                        Spacer(Modifier.height(14.dp))
                        Surface(
                            color = StormdropColors.Crimson.copy(alpha = 0.16f),
                            shape = RoundedCornerShape(12.dp),
                            border = BorderStroke(1.dp, StormdropColors.Crimson.copy(alpha = 0.5f)),
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Text(
                                text = errorMessage!!,
                                style = MaterialTheme.typography.bodySmall,
                                color = StormdropColors.TextMain,
                                modifier = Modifier.padding(12.dp),
                                textAlign = TextAlign.Center,
                            )
                        }
                    }

                    Spacer(Modifier.height(20.dp))

                    Button(
                        onClick = {
                            errorMessage = null
                            isSigningIn = true
                            onGoogleSignInClicked(
                                context = context,
                                credentialManager = credentialManager,
                                onAuthSuccess = {
                                    isSigningIn = false
                                    onAuthSuccess()
                                },
                                onAuthError = { msg ->
                                    isSigningIn = false
                                    errorMessage = msg
                                },
                                scope = scope,
                                onAuthCancelled = {
                                    isSigningIn = false
                                },
                            )
                        },
                        enabled = !isSigningIn && !isCheckingAuto,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = StormdropColors.AccentOrange,
                            contentColor = Color(0xFF140B00),
                        ),
                        shape = RoundedCornerShape(14.dp),
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(54.dp)
                            .testTag("google_sign_in_button"),
                    ) {
                        if (isSigningIn || isCheckingAuto) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(22.dp),
                                color = Color(0xFF140B00),
                                strokeWidth = 2.5.dp,
                            )
                            Spacer(Modifier.width(10.dp))
                            Text(
                                text = if (isCheckingAuto) "Checking session..." else "Signing in...",
                                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                            )
                        } else {
                            Icon(
                                imageVector = Icons.Filled.Login,
                                contentDescription = "Sign in with Google",
                                modifier = Modifier.size(20.dp),
                            )
                            Spacer(Modifier.width(10.dp))
                            Text(
                                text = "Sign in with Google",
                                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                            )
                        }
                    }

                    Spacer(Modifier.height(14.dp))

                    Row(
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.Center,
                    ) {
                        Icon(
                            imageVector = Icons.Filled.GppGood,
                            contentDescription = null,
                            tint = StormdropColors.Emerald,
                            modifier = Modifier.size(16.dp),
                        )
                        Spacer(Modifier.width(6.dp))
                        Text(
                            text = "Protected by Firebase Auth & Firestore Rules",
                            style = MaterialTheme.typography.labelSmall,
                            color = StormdropColors.TextDim,
                        )
                    }
                }
            }

            Column(
                modifier = Modifier.widthIn(max = 480.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    text = stringResource(id = R.string.legal_notice),
                    style = MaterialTheme.typography.bodySmall,
                    color = StormdropColors.TextMuted,
                    textAlign = TextAlign.Center,
                )
            }
        }
    }
}
