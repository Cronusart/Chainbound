package com.signaljam.solana.mwa

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.util.Base64
import androidx.activity.ComponentActivity
import com.solana.mobilewalletadapter.clientlib.ActivityResultSender
import com.solana.mobilewalletadapter.clientlib.ConnectionIdentity
import com.solana.mobilewalletadapter.clientlib.MobileWalletAdapter
import com.solana.mobilewalletadapter.clientlib.TransactionResult
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import org.json.JSONObject
import java.util.concurrent.atomic.AtomicBoolean

interface SignalJamMwaCallback {
    fun onSuccess(payloadJson: String)
    fun onError(code: String, message: String)
}

/**
 * Private ComponentActivity boundary required by the official MWA ActivityResultSender.
 * Request data remains in process memory and is never serialized into this Activity's Intent.
 */
class SignalJamMwaActivity : ComponentActivity() {
    private val operationScope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val sender = ActivityResultSender(this)
        SignalJamMwaBridge.runPending(this, sender, operationScope)
    }

    override fun onDestroy() {
        operationScope.cancel("MWA host activity was destroyed")
        super.onDestroy()
    }
}

/**
 * Thin official-MWA bridge for Unity. It accepts only public payload bytes and returns
 * authorization metadata, detached signatures, or transaction signatures. No private key
 * material enters Unity, this bridge, or an Android Intent.
 */
object SignalJamMwaBridge {
    private const val MAX_AUTH_TOKEN_LENGTH = 16_384
    private const val MAX_MESSAGE_BYTES = 8_192
    private const val MAX_TRANSACTION_BYTES = 1_232

    private enum class Operation {
        CONNECT,
        DISCONNECT,
        SIGN_MESSAGE,
        SIGN_AND_SEND_TRANSACTION,
    }

    private class PendingRequest(
        val operation: Operation,
        val identityUri: String,
        val iconUri: String,
        val identityName: String,
        val authToken: String,
        val payload: ByteArray?,
        private val callback: SignalJamMwaCallback,
    ) {
        private val completed = AtomicBoolean(false)

        fun success(payloadJson: String) {
            if (completed.compareAndSet(false, true)) callback.onSuccess(payloadJson)
        }

        fun error(code: String, message: String) {
            if (completed.compareAndSet(false, true)) callback.onError(code, message)
        }

        fun clearPayload() {
            payload?.fill(0)
        }
    }

    private val requestLock = Any()
    private var pendingRequest: PendingRequest? = null
    private var activeRequest: PendingRequest? = null
    private var activeActivity: SignalJamMwaActivity? = null
    private var requestInFlight = false

    @JvmStatic
    fun isAvailable(): Boolean = Build.VERSION.SDK_INT >= Build.VERSION_CODES.N

    @JvmStatic
    fun cancelActiveRequest() {
        val (pending, active, activity) = synchronized(requestLock) {
            val targets = Triple(pendingRequest, activeRequest, activeActivity)
            pendingRequest = null
            targets
        }

        pending?.let {
            it.clearPayload()
            it.error("USER_CANCELLED", "Wallet request cancelled.")
        }
        active?.error("USER_CANCELLED", "Wallet request cancelled.")
        activity?.runOnUiThread {
            if (!activity.isFinishing) activity.finish()
        }
    }

    @JvmStatic
    fun connect(
        activity: Activity,
        identityUri: String,
        iconUri: String,
        identityName: String,
        authToken: String,
        callback: SignalJamMwaCallback,
    ) = enqueue(
        activity,
        PendingRequest(
            Operation.CONNECT,
            identityUri,
            iconUri,
            identityName,
            authToken,
            null,
            callback,
        ),
    )

    @JvmStatic
    fun disconnect(
        activity: Activity,
        identityUri: String,
        iconUri: String,
        identityName: String,
        authToken: String,
        callback: SignalJamMwaCallback,
    ) = enqueue(
        activity,
        PendingRequest(
            Operation.DISCONNECT,
            identityUri,
            iconUri,
            identityName,
            authToken,
            null,
            callback,
        ),
    )

    @JvmStatic
    fun signMessage(
        activity: Activity,
        identityUri: String,
        iconUri: String,
        identityName: String,
        authToken: String,
        messageBase64: String,
        callback: SignalJamMwaCallback,
    ) {
        val message = decodePayload(messageBase64, MAX_MESSAGE_BYTES, "message", callback) ?: return
        enqueue(
            activity,
            PendingRequest(
                Operation.SIGN_MESSAGE,
                identityUri,
                iconUri,
                identityName,
                authToken,
                message,
                callback,
            ),
        )
    }

    @JvmStatic
    fun signAndSendTransaction(
        activity: Activity,
        identityUri: String,
        iconUri: String,
        identityName: String,
        authToken: String,
        transactionBase64: String,
        callback: SignalJamMwaCallback,
    ) {
        val transaction = decodePayload(
            transactionBase64,
            MAX_TRANSACTION_BYTES,
            "transaction",
            callback,
        ) ?: return
        enqueue(
            activity,
            PendingRequest(
                Operation.SIGN_AND_SEND_TRANSACTION,
                identityUri,
                iconUri,
                identityName,
                authToken,
                transaction,
                callback,
            ),
        )
    }

    internal fun runPending(
        activity: SignalJamMwaActivity,
        sender: ActivityResultSender,
        scope: CoroutineScope,
    ) {
        val request = synchronized(requestLock) {
            val claimed = pendingRequest
            pendingRequest = null
            if (claimed != null) {
                requestInFlight = true
                activeRequest = claimed
                activeActivity = activity
            }
            claimed
        }

        if (request == null) {
            activity.finish()
            return
        }

        scope.launch {
            try {
                execute(request, sender)
            } catch (_: CancellationException) {
                request.error("USER_CANCELLED", "Wallet request cancelled.")
            } catch (t: Throwable) {
                request.error("MWA_EXCEPTION", safeMessage(t))
            } finally {
                request.clearPayload()
                synchronized(requestLock) {
                    if (activeRequest === request) activeRequest = null
                    if (activeActivity === activity) activeActivity = null
                    requestInFlight = false
                }
                if (!activity.isFinishing) activity.finish()
            }
        }
    }

    private fun enqueue(activity: Activity, request: PendingRequest) {
        val validationError = validate(request)
        if (validationError != null) {
            request.clearPayload()
            request.error("INVALID_REQUEST", validationError)
            return
        }

        activity.runOnUiThread {
            val accepted = synchronized(requestLock) {
                if (pendingRequest != null || requestInFlight) {
                    false
                } else {
                    pendingRequest = request
                    true
                }
            }
            if (!accepted) {
                request.clearPayload()
                request.error("REQUEST_IN_PROGRESS", "Another wallet request is already active.")
                return@runOnUiThread
            }

            try {
                activity.startActivity(Intent(activity, SignalJamMwaActivity::class.java))
            } catch (t: Throwable) {
                synchronized(requestLock) {
                    if (pendingRequest === request) pendingRequest = null
                }
                request.clearPayload()
                request.error("ACTIVITY_LAUNCH_FAILED", safeMessage(t))
            }
        }
    }

    private suspend fun execute(request: PendingRequest, sender: ActivityResultSender) {
        val adapter = createAdapter(request)
        when (request.operation) {
            Operation.CONNECT -> connect(adapter, sender, request)
            Operation.DISCONNECT -> disconnect(adapter, sender, request)
            Operation.SIGN_MESSAGE -> signMessage(adapter, sender, request)
            Operation.SIGN_AND_SEND_TRANSACTION -> signAndSend(adapter, sender, request)
        }
    }

    private suspend fun connect(
        adapter: MobileWalletAdapter,
        sender: ActivityResultSender,
        request: PendingRequest,
    ) {
        when (val result = adapter.connect(sender)) {
            is TransactionResult.Success -> {
                val account = result.authResult.accounts.firstOrNull()
                    ?: return request.error("NO_ACCOUNT", "Wallet authorization returned no account.")
                request.success(
                    JSONObject()
                        .put("publicKeyBase64", encode(account.publicKey))
                        .put("authToken", adapter.authToken ?: "")
                        .toString(),
                )
            }
            is TransactionResult.NoWalletFound -> request.error("NO_WALLET_FOUND", result.message)
            is TransactionResult.Failure -> request.error("AUTHORIZE_FAILED", failureMessage(result))
        }
    }

    private suspend fun disconnect(
        adapter: MobileWalletAdapter,
        sender: ActivityResultSender,
        request: PendingRequest,
    ) {
        when (val result = adapter.disconnect(sender)) {
            is TransactionResult.Success -> request.success("{}")
            is TransactionResult.NoWalletFound -> request.error("NO_WALLET_FOUND", result.message)
            is TransactionResult.Failure -> request.error("DEAUTHORIZE_FAILED", failureMessage(result))
        }
    }

    private suspend fun signMessage(
        adapter: MobileWalletAdapter,
        sender: ActivityResultSender,
        request: PendingRequest,
    ) {
        val message = requireNotNull(request.payload)
        when (val result = adapter.transact(sender) { authResult ->
            val account = authResult.accounts.firstOrNull()
                ?: throw IllegalStateException("Wallet authorization returned no account.")
            signMessagesDetached(arrayOf(message), arrayOf(account.publicKey))
        }) {
            is TransactionResult.Success -> {
                val signature = result.payload.messages
                    .firstOrNull()
                    ?.signatures
                    ?.firstOrNull()
                    ?: return request.error("EMPTY_SIGNATURE", "Wallet returned no message signature.")
                request.success(JSONObject().put("signatureBase64", encode(signature)).toString())
            }
            is TransactionResult.NoWalletFound -> request.error("NO_WALLET_FOUND", result.message)
            is TransactionResult.Failure -> request.error("SIGN_MESSAGE_FAILED", failureMessage(result))
        }
    }

    private suspend fun signAndSend(
        adapter: MobileWalletAdapter,
        sender: ActivityResultSender,
        request: PendingRequest,
    ) {
        val transaction = requireNotNull(request.payload)
        when (val result = adapter.transact(sender) {
            signAndSendTransactions(arrayOf(transaction))
        }) {
            is TransactionResult.Success -> {
                val signature = result.payload.signatures.firstOrNull()
                    ?: return request.error("EMPTY_SIGNATURE", "Wallet returned no transaction signature.")
                request.success(JSONObject().put("signatureBase64", encode(signature)).toString())
            }
            is TransactionResult.NoWalletFound -> request.error("NO_WALLET_FOUND", result.message)
            is TransactionResult.Failure -> request.error("SIGN_AND_SEND_FAILED", failureMessage(result))
        }
    }

    private fun createAdapter(request: PendingRequest): MobileWalletAdapter {
        return MobileWalletAdapter(
            connectionIdentity = ConnectionIdentity(
                identityUri = Uri.parse(request.identityUri),
                iconUri = Uri.parse(request.iconUri),
                identityName = request.identityName,
            ),
        ).also { adapter ->
            if (request.authToken.isNotBlank()) adapter.authToken = request.authToken
        }
    }

    private fun validate(request: PendingRequest): String? {
        val identity = Uri.parse(request.identityUri)
        if (identity.scheme != "https" || identity.host.isNullOrBlank()) {
            return "Identity URI must be an absolute HTTPS URL."
        }
        val icon = Uri.parse(request.iconUri)
        if (!icon.isRelative || icon.scheme != null || icon.authority != null) {
            return "Icon URI must be relative to the identity URI."
        }
        if (icon.pathSegments.any { it == ".." }) return "Icon URI cannot traverse parent paths."
        if (request.identityName.isBlank() || request.identityName.length > 128) {
            return "Identity name must contain between 1 and 128 characters."
        }
        if (request.authToken.length > MAX_AUTH_TOKEN_LENGTH) return "Authorization token is too large."
        return null
    }

    private fun decodePayload(
        value: String,
        maxBytes: Int,
        label: String,
        callback: SignalJamMwaCallback,
    ): ByteArray? {
        return try {
            val payload = Base64.decode(value, Base64.NO_WRAP)
            if (payload.isEmpty() || payload.size > maxBytes) {
                callback.onError("INVALID_PAYLOAD", "$label payload has an invalid size.")
                null
            } else {
                payload
            }
        } catch (_: IllegalArgumentException) {
            callback.onError("INVALID_PAYLOAD", "$label payload is not valid base64.")
            null
        }
    }

    private fun failureMessage(result: TransactionResult.Failure<*>): String {
        return result.message.ifBlank { safeMessage(result.e) }
    }

    private fun encode(bytes: ByteArray): String = Base64.encodeToString(bytes, Base64.NO_WRAP)
    private fun safeMessage(t: Throwable): String = t.message ?: t.javaClass.simpleName
}
