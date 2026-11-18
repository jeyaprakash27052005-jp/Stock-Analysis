# Stormdrop Royale Lite — full-screen landscape WebView that runs the game from assets/game/.
.class public Lcom/stormdrop/royale/lite/MainActivity;
.super Landroid/app/Activity;
.source "MainActivity.smali"

.field private web:Landroid/webkit/WebView;

.method public constructor <init>()V
    .registers 1
    invoke-direct {p0}, Landroid/app/Activity;-><init>()V
    return-void
.end method

.method protected onCreate(Landroid/os/Bundle;)V
    .registers 6
    invoke-super {p0, p1}, Landroid/app/Activity;->onCreate(Landroid/os/Bundle;)V

    # keep the screen on + fullscreen (FLAG_KEEP_SCREEN_ON 0x80 | FLAG_FULLSCREEN 0x400)
    invoke-virtual {p0}, Lcom/stormdrop/royale/lite/MainActivity;->getWindow()Landroid/view/Window;
    move-result-object v0
    const/16 v1, 0x480
    invoke-virtual {v0, v1}, Landroid/view/Window;->addFlags(I)V

    new-instance v0, Landroid/webkit/WebView;
    invoke-direct {v0, p0}, Landroid/webkit/WebView;-><init>(Landroid/content/Context;)V
    iput-object v0, p0, Lcom/stormdrop/royale/lite/MainActivity;->web:Landroid/webkit/WebView;

    invoke-virtual {v0}, Landroid/webkit/WebView;->getSettings()Landroid/webkit/WebSettings;
    move-result-object v1
    const/4 v2, 0x1
    const/4 v3, 0x0
    invoke-virtual {v1, v2}, Landroid/webkit/WebSettings;->setJavaScriptEnabled(Z)V
    invoke-virtual {v1, v2}, Landroid/webkit/WebSettings;->setDomStorageEnabled(Z)V
    invoke-virtual {v1, v2}, Landroid/webkit/WebSettings;->setAllowFileAccess(Z)V
    invoke-virtual {v1, v2}, Landroid/webkit/WebSettings;->setAllowUniversalAccessFromFileURLs(Z)V
    invoke-virtual {v1, v3}, Landroid/webkit/WebSettings;->setMediaPlaybackRequiresUserGesture(Z)V

    invoke-direct {p0}, Lcom/stormdrop/royale/lite/MainActivity;->immersive()V

    const-string v1, "file:///android_asset/game/index.html"
    invoke-virtual {v0, v1}, Landroid/webkit/WebView;->loadUrl(Ljava/lang/String;)V
    invoke-virtual {p0, v0}, Lcom/stormdrop/royale/lite/MainActivity;->setContentView(Landroid/view/View;)V
    return-void
.end method

# Hide status + navigation bars (sticky immersive: 0x1706)
.method private immersive()V
    .registers 3
    iget-object v0, p0, Lcom/stormdrop/royale/lite/MainActivity;->web:Landroid/webkit/WebView;
    if-eqz v0, :done
    const/16 v1, 0x1706
    invoke-virtual {v0, v1}, Landroid/webkit/WebView;->setSystemUiVisibility(I)V
    :done
    return-void
.end method

.method public onWindowFocusChanged(Z)V
    .registers 2
    invoke-super {p0, p1}, Landroid/app/Activity;->onWindowFocusChanged(Z)V
    if-eqz p1, :done
    invoke-direct {p0}, Lcom/stormdrop/royale/lite/MainActivity;->immersive()V
    :done
    return-void
.end method

.method protected onPause()V
    .registers 2
    invoke-super {p0}, Landroid/app/Activity;->onPause()V
    iget-object v0, p0, Lcom/stormdrop/royale/lite/MainActivity;->web:Landroid/webkit/WebView;
    if-eqz v0, :done
    invoke-virtual {v0}, Landroid/webkit/WebView;->onPause()V
    :done
    return-void
.end method

.method protected onResume()V
    .registers 2
    invoke-super {p0}, Landroid/app/Activity;->onResume()V
    iget-object v0, p0, Lcom/stormdrop/royale/lite/MainActivity;->web:Landroid/webkit/WebView;
    if-eqz v0, :done
    invoke-virtual {v0}, Landroid/webkit/WebView;->onResume()V
    :done
    return-void
.end method

.method public onBackPressed()V
    .registers 3
    iget-object v0, p0, Lcom/stormdrop/royale/lite/MainActivity;->web:Landroid/webkit/WebView;
    if-eqz v0, :exit
    invoke-virtual {v0}, Landroid/webkit/WebView;->canGoBack()Z
    move-result v1
    if-eqz v1, :exit
    invoke-virtual {v0}, Landroid/webkit/WebView;->goBack()V
    return-void
    :exit
    invoke-super {p0}, Landroid/app/Activity;->onBackPressed()V
    return-void
.end method
