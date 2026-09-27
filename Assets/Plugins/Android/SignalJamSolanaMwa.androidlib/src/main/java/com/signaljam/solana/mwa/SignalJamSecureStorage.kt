package com.signaljam.solana.mwa

import android.app.Activity
import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.nio.ByteBuffer
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Small Unity-facing encrypted preference store. Refresh tokens never leave this
 * class unencrypted on Android: ciphertext lives in SharedPreferences while the
 * AES key is non-exportable Android Keystore material.
 */
object SignalJamSecureStorage {
    private const val PREFS = "signaljam.secure.profile"
    private const val KEY_ALIAS = "signaljam.profile.refresh.v1"
    private const val TRANSFORMATION = "AES/GCM/NoPadding"
    private const val IV_BYTES = 12
    private const val TAG_BITS = 128

    @JvmStatic
    fun get(activity: Activity, key: String): String {
        if (key.isBlank()) return ""
        val encoded = activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getString(key, null) ?: return ""
        return try {
            val bytes = Base64.decode(encoded, Base64.NO_WRAP)
            if (bytes.size <= IV_BYTES) return ""
            val iv = bytes.copyOfRange(0, IV_BYTES)
            val payload = bytes.copyOfRange(IV_BYTES, bytes.size)
            val cipher = Cipher.getInstance(TRANSFORMATION)
            cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), GCMParameterSpec(TAG_BITS, iv))
            String(cipher.doFinal(payload), Charsets.UTF_8)
        } catch (_: Throwable) {
            delete(activity, key)
            ""
        }
    }

    @JvmStatic
    fun set(activity: Activity, key: String, value: String) {
        if (key.isBlank()) return
        try {
            val cipher = Cipher.getInstance(TRANSFORMATION)
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey())
            val encrypted = cipher.doFinal(value.toByteArray(Charsets.UTF_8))
            val packed = ByteBuffer.allocate(cipher.iv.size + encrypted.size)
                .put(cipher.iv)
                .put(encrypted)
                .array()
            activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString(key, Base64.encodeToString(packed, Base64.NO_WRAP))
                .apply()
        } catch (_: Throwable) {
            // The caller treats this as no cached session on the next launch.
        }
    }

    @JvmStatic
    fun delete(activity: Activity, key: String) {
        if (key.isNotBlank()) {
            activity.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().remove(key).apply()
        }
    }

    private fun getOrCreateKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }

        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build(),
        )
        return generator.generateKey()
    }
}
