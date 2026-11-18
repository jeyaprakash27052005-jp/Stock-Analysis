package com.example

import android.app.Application
import com.example.data.StormdropRepository
import com.example.data.local.SettingsDataStore
import com.google.firebase.firestore.FirebaseFirestore

class StormdropApplication : Application() {
    val firestore: FirebaseFirestore by lazy {
        FirebaseFirestore.getInstance(getString(R.string.firestore_database_id))
    }
    val settingsStore: SettingsDataStore by lazy { SettingsDataStore(this) }
    val repository: StormdropRepository by lazy { StormdropRepository(firestore, settingsStore) }
}
