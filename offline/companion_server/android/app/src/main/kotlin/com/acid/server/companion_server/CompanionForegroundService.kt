package com.acid.server.companion_server

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.AudioAttributes
import android.media.AudioFormat
import android.media.AudioTrack
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log

class CompanionForegroundService : Service() {
    private var wakeLock: PowerManager.WakeLock? = null
    private var audioTrack: AudioTrack? = null

    companion object {
        private const val TAG = "CompanionFG"
    }

    override fun onCreate() {
        super.onCreate()
        Log.i(TAG, "CompanionForegroundService onCreate")
        val channelId = "acier_companion_channel"
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                channelId,
                "AC Identity Server",
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = "Keeps the local AC Identity server active"
                setShowBadge(false)
            }
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(channel)
        }

        val notification: Notification = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            Notification.Builder(this, channelId)
                .setContentTitle("Assassin's Creed Identity Server")
                .setContentText("Local server active on 127.0.0.1:8080")
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setOngoing(true)
                .build()
        } else {
            @Suppress("DEPRECATION")
            Notification.Builder(this)
                .setContentTitle("Assassin's Creed Identity Server")
                .setContentText("Local server active on 127.0.0.1:8080")
                .setSmallIcon(android.R.drawable.ic_dialog_info)
                .setOngoing(true)
                .build()
        }

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(1337, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PLAYBACK)
        } else {
            startForeground(1337, notification)
        }

        // 1. Partial WakeLock
        val powerManager = getSystemService(Context.POWER_SERVICE) as PowerManager
        wakeLock = powerManager.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "ACIdentityServer::WakeLock")
        wakeLock?.acquire(24 * 60 * 60 * 1000L)

        // 2. Play continuous silent audio track.
        // On ColorOS/OxygenOS/RealmeUI, OplusHansManager monitors audio streams.
        // Any process actively playing audio is immune to the cgroup freezer (scene: LcdOn/LcdOff).
        startSilentAudio()
    }

    private fun startSilentAudio() {
        try {
            val sampleRate = 44100
            val minBufferSize = AudioTrack.getMinBufferSize(
                sampleRate,
                AudioFormat.CHANNEL_OUT_MONO,
                AudioFormat.ENCODING_PCM_16BIT
            )
            val bufferSize = if (minBufferSize > 0) minBufferSize else 4096
            val silentPcm = ByteArray(bufferSize) // All zeros = pure silence

            val track = AudioTrack.Builder()
                .setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_MEDIA)
                        .setContentType(AudioAttributes.CONTENT_TYPE_MUSIC)
                        .build()
                )
                .setAudioFormat(
                    AudioFormat.Builder()
                        .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                        .setSampleRate(sampleRate)
                        .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                        .build()
                )
                .setBufferSizeInBytes(bufferSize)
                .setTransferMode(AudioTrack.MODE_STATIC)
                .build()

            track.write(silentPcm, 0, silentPcm.size)
            track.setLoopPoints(0, silentPcm.size / 2, -1) // -1 = loop indefinitely
            track.play()
            audioTrack = track
            Log.i(TAG, "Silent audio track started (immune to Hans freeze)")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to start silent audio track", e)
        }
    }

    override fun onDestroy() {
        Log.i(TAG, "CompanionForegroundService onDestroy")
        try {
            audioTrack?.stop()
            audioTrack?.release()
            audioTrack = null
        } catch (e: Exception) {
            Log.e(TAG, "Error stopping audio track", e)
        }
        wakeLock?.let {
            if (it.isHeld) it.release()
        }
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null
}
