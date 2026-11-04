package com.acid.server.companion_server

import android.content.ComponentName
import android.content.Intent
import android.os.Build
import android.os.Bundle
import android.os.Environment
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File

class MainActivity : FlutterActivity() {
    private val CHANNEL = "com.acid.server/game"
    private val gameFilesDir = File(Environment.getExternalStorageDirectory(), "Android/data/com.ubisoft.assassinscreed.identity/files")
    private val ownFilesDir by lazy { getExternalFilesDir(null) ?: filesDir }
    private val sharedDir = File(Environment.getExternalStorageDirectory(), "ACIdentity")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val intent = Intent(this, CompanionForegroundService::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(intent)
        } else {
            startService(intent)
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "openGame" -> {
                    try {
                        val launchIntent = Intent(Intent.ACTION_MAIN).apply {
                            addCategory(Intent.CATEGORY_LAUNCHER)
                            component = ComponentName(
                                "com.ubisoft.assassinscreed.identity",
                                "com.acid.plugins.activity.BridgeActivity"
                            )
                            flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED
                        }
                        try {
                            startActivity(launchIntent)
                            result.success(true)
                        } catch (e1: Exception) {
                            val fallback = packageManager.getLaunchIntentForPackage("com.ubisoft.assassinscreed.identity")
                            if (fallback != null) {
                                fallback.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                startActivity(fallback)
                                result.success(true)
                            } else {
                                result.error("LAUNCH_ERROR", e1.message, null)
                            }
                        }
                    } catch (e: Exception) {
                        result.error("LAUNCH_ERROR", e.message, null)
                    }
                }
                "isGameInstalled" -> {
                    try {
                        packageManager.getPackageInfo("com.ubisoft.assassinscreed.identity", 0)
                        result.success(true)
                    } catch (e: Exception) {
                        result.success(false)
                    }
                }
                "deleteGameLocalStorage" -> {
                    try {
                        var deleted = false
                        val f1 = File(gameFilesDir, "localstorage.json")
                        if (f1.exists()) deleted = f1.delete()
                        val f2 = File(ownFilesDir, "localstorage.json")
                        if (f2.exists()) f2.delete()
                        val f3 = File(sharedDir, "localstorage.json")
                        if (f3.exists()) f3.delete()
                        result.success(deleted)
                    } catch (e: Exception) {
                        result.error("DELETE_ERROR", e.message, null)
                    }
                }
                "readLocalStorage" -> {
                    try {
                        var file = File(gameFilesDir, "localstorage.json")
                        if (!file.exists() || !file.canRead()) {
                            file = File(ownFilesDir, "localstorage.json")
                        }
                        if (file.exists() && file.canRead()) {
                            result.success(file.readText())
                        } else {
                            result.success(null)
                        }
                    } catch (e: Exception) {
                        try {
                            val f2 = File(ownFilesDir, "localstorage.json")
                            if (f2.exists()) result.success(f2.readText()) else result.success(null)
                        } catch (e2: Exception) {
                            result.error("READ_ERROR", e.message, null)
                        }
                    }
                }
                "writeLocalStorage" -> {
                    try {
                        val content = call.argument<String>("content")
                        if (content != null) {
                            var written = false
                            try {
                                if (!gameFilesDir.exists()) gameFilesDir.mkdirs()
                                val file = File(gameFilesDir, "localstorage.json")
                                file.writeText(content)
                                written = true
                            } catch (_: Exception) {}

                            try {
                                if (!ownFilesDir.exists()) ownFilesDir.mkdirs()
                                val ownFile = File(ownFilesDir, "localstorage.json")
                                ownFile.writeText(content)
                                written = true
                            } catch (_: Exception) {}

                            result.success(written)
                        } else {
                            result.error("ARG_ERROR", "Content cannot be null", null)
                        }
                    } catch (e: Exception) {
                        result.error("WRITE_ERROR", e.message, null)
                    }
                }
                "saveCustomMission" -> {
                    try {
                        val filename = call.argument<String>("filename")
                        val bytes = call.argument<ByteArray>("bytes")
                        val text = call.argument<String>("text")
                        if (filename != null) {
                            val dirs = listOf(
                                File(ownFilesDir, "custom_missions"),
                                File(gameFilesDir, "custom_missions"),
                                File(sharedDir, "custom_missions")
                            )
                            for (dir in dirs) {
                                try {
                                    if (!dir.exists()) dir.mkdirs()
                                    val targetFile = File(dir, filename)
                                    if (bytes != null) targetFile.writeBytes(bytes)
                                    else if (text != null) targetFile.writeText(text)
                                } catch (_: Exception) {}
                            }
                            result.success(true)
                        } else {
                            result.error("ARG_ERROR", "Filename required", null)
                        }
                    } catch (e: Exception) {
                        result.error("SAVE_ERROR", e.message, null)
                    }
                }
                "listCustomMissions" -> {
                    try {
                        val customDir = File(gameFilesDir, "custom_missions")
                        if (customDir.exists()) {
                            val names = customDir.listFiles()?.map { it.name } ?: emptyList<String>()
                            result.success(names)
                        } else {
                            result.success(emptyList<String>())
                        }
                    } catch (e: Exception) {
                        result.error("LIST_ERROR", e.message, null)
                    }
                }
                else -> result.notImplemented()
            }
        }
    }
}
