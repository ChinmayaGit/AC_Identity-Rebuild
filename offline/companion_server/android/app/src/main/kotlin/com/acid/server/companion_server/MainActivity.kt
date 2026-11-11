package com.acid.server.companion_server

import android.Manifest
import android.app.Activity
import android.content.ComponentName
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.graphics.Paint
import android.graphics.pdf.PdfDocument
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.os.Environment
import android.provider.OpenableColumns
import android.provider.Settings
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import androidx.documentfile.provider.DocumentFile
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.BufferedInputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.InputStream
import java.util.zip.ZipInputStream
import kotlin.concurrent.thread

class MainActivity : FlutterActivity() {
    private val CHANNEL = "com.acid.server/game"
    private val REQUEST_PICK_FILE = 9001
    private val REQUEST_STORAGE_PERMISSION = 9002
    private val REQUEST_PICK_CACHE_ZIP = 9003
    private val REQUEST_PICK_CACHE_FOLDER = 9004
    private var pendingFileResult: MethodChannel.Result? = null
    private var pendingPermissionResult: MethodChannel.Result? = null
    private var methodChannel: MethodChannel? = null

    private val gameFilesDir = File(Environment.getExternalStorageDirectory(), "Android/data/com.ubisoft.assassinscreed.identity/files")
    private val gameCacheDir = File(Environment.getExternalStorageDirectory(), "Android/data/com.ubisoft.assassinscreed.identity/files/cache")
    private val ownFilesDir by lazy { getExternalFilesDir(null) ?: filesDir }
    private val ownCacheDir by lazy { File(ownFilesDir, "cache") }
    private val sharedDir = File(Environment.getExternalStorageDirectory(), "ACIdentity")
    private val sharedCacheDir = File(Environment.getExternalStorageDirectory(), "ACIdentity_Cache")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val intent = Intent(this, CompanionForegroundService::class.java)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            startForegroundService(intent)
        } else {
            startService(intent)
        }
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == REQUEST_PICK_FILE) {
            val pending = pendingFileResult
            pendingFileResult = null
            if (resultCode == Activity.RESULT_OK && data?.data != null) {
                try {
                    val uri: Uri = data.data!!
                    var name = "custom_mission.json"
                    try {
                        contentResolver.query(uri, null, null, null, null)?.use { cursor ->
                            val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                            if (nameIndex != -1 && cursor.moveToFirst()) {
                                name = cursor.getString(nameIndex) ?: name
                            }
                        }
                    } catch (_: Exception) {}
                    val bytes = contentResolver.openInputStream(uri)?.use { it.readBytes() }
                    val text = bytes?.let {
                        try { String(it, Charsets.UTF_8) } catch (_: Exception) { null }
                    }
                    pending?.success(mapOf(
                        "name" to name,
                        "bytes" to bytes,
                        "text" to text
                    ))
                } catch (e: Exception) {
                    pending?.error("PICK_ERROR", e.message, null)
                }
            } else {
                pending?.success(null)
            }
        } else if (requestCode == REQUEST_PICK_CACHE_ZIP) {
            if (resultCode == Activity.RESULT_OK && data?.data != null) {
                installCacheFromZipUri(data.data!!)
            } else {
                sendCacheProgress("idle", 0, 104, 0, null, null)
            }
        } else if (requestCode == REQUEST_PICK_CACHE_FOLDER) {
            if (resultCode == Activity.RESULT_OK && data?.data != null) {
                val uri = data.data!!
                try {
                    val flags = Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                    contentResolver.takePersistableUriPermission(uri, flags)
                } catch (_: Exception) {}
                installCacheFromFolderUri(uri)
            } else {
                sendCacheProgress("idle", 0, 104, 0, null, null)
            }
        }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == REQUEST_STORAGE_PERMISSION) {
            val granted = grantResults.isNotEmpty() && grantResults[0] == PackageManager.PERMISSION_GRANTED
            pendingPermissionResult?.success(granted)
            pendingPermissionResult = null
        }
    }

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        methodChannel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL)
        methodChannel!!.setMethodCallHandler { call, result ->
            when (call.method) {
                "startForegroundService" -> {
                    try {
                        val intent = Intent(this, CompanionForegroundService::class.java)
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                            startForegroundService(intent)
                        } else {
                            startService(intent)
                        }
                        result.success(true)
                    } catch (e: Exception) {
                        result.error("SERVICE_ERROR", e.message, null)
                    }
                }
                "stopForegroundService" -> {
                    try {
                        val intent = Intent(this, CompanionForegroundService::class.java)
                        stopService(intent)
                        result.success(true)
                    } catch (e: Exception) {
                        result.error("SERVICE_ERROR", e.message, null)
                    }
                }
                "openGame" -> {
                    try {
                        val fallback = packageManager.getLaunchIntentForPackage("com.ubisoft.assassinscreed.identity")
                        if (fallback != null) {
                            fallback.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED)
                            startActivity(fallback)
                            result.success(true)
                        } else {
                            val launchIntent = Intent(Intent.ACTION_MAIN).apply {
                                addCategory(Intent.CATEGORY_LAUNCHER)
                                component = ComponentName(
                                    "com.ubisoft.assassinscreed.identity",
                                    "com.acid.plugins.activity.BridgeActivity"
                                )
                                flags = Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_RESET_TASK_IF_NEEDED
                            }
                            startActivity(launchIntent)
                            result.success(true)
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
                        val dirs = listOf(
                            File(gameFilesDir, "custom_missions"),
                            File(ownFilesDir, "custom_missions"),
                            File(sharedDir, "custom_missions")
                        )
                        val names = mutableSetOf<String>()
                        for (dir in dirs) {
                            if (dir.exists()) {
                                dir.listFiles()?.forEach { names.add(it.name) }
                            }
                        }
                        result.success(names.toList())
                    } catch (e: Exception) {
                        result.error("LIST_ERROR", e.message, null)
                    }
                }
                "readCustomMissionFile" -> {
                    try {
                        val filename = call.argument<String>("filename")
                        if (filename != null) {
                            val dirs = listOf(
                                File(ownFilesDir, "custom_missions"),
                                File(gameFilesDir, "custom_missions"),
                                File(sharedDir, "custom_missions")
                            )
                            var content: String? = null
                            for (dir in dirs) {
                                val f = File(dir, filename)
                                if (f.exists() && f.canRead()) {
                                    content = f.readText()
                                    break
                                }
                            }
                            result.success(content)
                        } else {
                            result.error("ARG_ERROR", "Filename required", null)
                        }
                    } catch (e: Exception) {
                        result.error("READ_ERROR", e.message, null)
                    }
                }
                "deleteCustomMission" -> {
                    try {
                        val filename = call.argument<String>("filename")
                        if (filename != null) {
                            val dirs = listOf(
                                File(ownFilesDir, "custom_missions"),
                                File(gameFilesDir, "custom_missions"),
                                File(sharedDir, "custom_missions")
                            )
                            var deleted = false
                            for (dir in dirs) {
                                val f = File(dir, filename)
                                if (f.exists()) deleted = f.delete() || deleted
                                val fJson = File(dir, "$filename.json")
                                if (fJson.exists()) deleted = fJson.delete() || deleted
                                val fBin = File(dir, "openMission_$filename.bin")
                                if (fBin.exists()) deleted = fBin.delete() || deleted
                                val fInfo = File(dir, "getMissionInfo_$filename.bin")
                                if (fInfo.exists()) deleted = fInfo.delete() || deleted
                                val fEnd = File(dir, "endMission_$filename.bin")
                                if (fEnd.exists()) deleted = fEnd.delete() || deleted
                            }
                            result.success(deleted)
                        } else {
                            result.error("ARG_ERROR", "Filename required", null)
                        }
                    } catch (e: Exception) {
                        result.error("DELETE_ERROR", e.message, null)
                    }
                }
                "readGameState" -> {
                    try {
                        val dirs = listOf(ownFilesDir, gameFilesDir, sharedDir)
                        var content: String? = null
                        for (dir in dirs) {
                            val f = File(dir, "game_access_state.json")
                            if (f.exists() && f.canRead()) {
                                content = f.readText()
                                break
                            }
                        }
                        result.success(content)
                    } catch (e: Exception) {
                        result.error("READ_STATE_ERROR", e.message, null)
                    }
                }
                "writeGameState" -> {
                    try {
                        val content = call.argument<String>("content")
                        if (content != null) {
                            val dirs = listOf(ownFilesDir, gameFilesDir, sharedDir)
                            for (dir in dirs) {
                                try {
                                    if (!dir.exists()) dir.mkdirs()
                                    File(dir, "game_access_state.json").writeText(content)
                                } catch (_: Exception) {}
                            }
                            result.success(true)
                        } else {
                            result.error("ARG_ERROR", "Content required", null)
                        }
                    } catch (e: Exception) {
                        result.error("WRITE_STATE_ERROR", e.message, null)
                    }
                }
                "pickMissionFile" -> {
                    pendingFileResult = result
                    try {
                        val pickIntent = Intent(Intent.ACTION_GET_CONTENT).apply {
                            type = "*/*"
                            addCategory(Intent.CATEGORY_OPENABLE)
                            putExtra(Intent.EXTRA_MIME_TYPES, arrayOf("application/json", "application/octet-stream", "text/*"))
                        }
                        startActivityForResult(Intent.createChooser(pickIntent, "Select Mission File"), REQUEST_PICK_FILE)
                    } catch (e: Exception) {
                        pendingFileResult = null
                        result.error("PICK_LAUNCH_ERROR", e.message, null)
                    }
                }
                "requestStoragePermission" -> {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
                        val granted = ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
                        if (granted) {
                            result.success(true)
                        } else {
                            pendingPermissionResult = result
                            ActivityCompat.requestPermissions(
                                this,
                                arrayOf(Manifest.permission.WRITE_EXTERNAL_STORAGE, Manifest.permission.READ_EXTERNAL_STORAGE),
                                REQUEST_STORAGE_PERMISSION
                            )
                        }
                    } else {
                        result.success(true)
                    }
                }
                "exportSkillFiles" -> {
                    try {
                        val skillMd = call.argument<String>("skillMd") ?: ""
                        val instructionsMd = call.argument<String>("instructionsMd") ?: ""
                        val specMd = call.argument<String>("specMd") ?: ""
                        val sampleJson = call.argument<String>("sampleJson") ?: ""

                        val appDownloadDir = File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "ACIdentity_Mission_Skill")
                        if (!appDownloadDir.exists()) appDownloadDir.mkdirs()

                        File(appDownloadDir, "SKILL.md").writeText(skillMd)
                        File(appDownloadDir, "INSTRUCTIONS.md").writeText(instructionsMd)
                        File(appDownloadDir, "CUSTOM_MISSION_SPEC.md").writeText(specMd)
                        File(appDownloadDir, "sample_mission.json").writeText(sampleJson)

                        val pdfFile = File(appDownloadDir, "AC_IDENTITY_ASSETS_GUIDE.pdf")
                        generateAssetGuidePdf(pdfFile)

                        var publicExported = false
                        val publicDir = File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS), "ACIdentity_Mission_Skill")
                        try {
                            if (!publicDir.exists()) publicDir.mkdirs()
                            File(publicDir, "SKILL.md").writeText(skillMd)
                            File(publicDir, "INSTRUCTIONS.md").writeText(instructionsMd)
                            File(publicDir, "CUSTOM_MISSION_SPEC.md").writeText(specMd)
                            File(publicDir, "sample_mission.json").writeText(sampleJson)
                            generateAssetGuidePdf(File(publicDir, "AC_IDENTITY_ASSETS_GUIDE.pdf"))
                            publicExported = true
                        } catch (_: Exception) {}

                        try {
                            val sharedSkillDir = File(sharedDir, "Skill")
                            if (!sharedSkillDir.exists()) sharedSkillDir.mkdirs()
                            File(sharedSkillDir, "SKILL.md").writeText(skillMd)
                            File(sharedSkillDir, "INSTRUCTIONS.md").writeText(instructionsMd)
                            File(sharedSkillDir, "CUSTOM_MISSION_SPEC.md").writeText(specMd)
                            File(sharedSkillDir, "sample_mission.json").writeText(sampleJson)
                            generateAssetGuidePdf(File(sharedSkillDir, "AC_IDENTITY_ASSETS_GUIDE.pdf"))
                        } catch (_: Exception) {}

                        val finalPath = if (publicExported) publicDir.absolutePath else appDownloadDir.absolutePath
                        result.success(mapOf(
                            "path" to finalPath,
                            "pdfPath" to pdfFile.absolutePath,
                            "publicExported" to publicExported,
                            "files" to listOf(
                                "SKILL.md",
                                "INSTRUCTIONS.md",
                                "CUSTOM_MISSION_SPEC.md",
                                "sample_mission.json",
                                "AC_IDENTITY_ASSETS_GUIDE.pdf"
                            )
                        ))
                    } catch (e: Exception) {
                        result.error("EXPORT_ERROR", e.message, null)
                    }
                }
                "shareSkillFiles" -> {
                    try {
                        val appDownloadDir = File(getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), "ACIdentity_Mission_Skill")
                        val files = listOf(
                            File(appDownloadDir, "SKILL.md"),
                            File(appDownloadDir, "INSTRUCTIONS.md"),
                            File(appDownloadDir, "CUSTOM_MISSION_SPEC.md"),
                            File(appDownloadDir, "sample_mission.json"),
                            File(appDownloadDir, "AC_IDENTITY_ASSETS_GUIDE.pdf")
                        ).filter { it.exists() }

                        val uris = ArrayList<Uri>()
                        for (f in files) {
                            val uri = FileProvider.getUriForFile(this, "${packageName}.fileprovider", f)
                            uris.add(uri)
                        }

                        val shareIntent = Intent(Intent.ACTION_SEND_MULTIPLE).apply {
                            type = "*/*"
                            putParcelableArrayListExtra(Intent.EXTRA_STREAM, uris)
                            addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                        }
                        startActivity(Intent.createChooser(shareIntent, "Share AC Identity Mission Skill Package"))
                        result.success(true)
                    } catch (e: Exception) {
                        result.error("SHARE_ERROR", e.message, null)
                    }
                }
                "getCacheStatus" -> {
                    try {
                        val forceScan = call.argument<Boolean>("forceScan") ?: false
                        val ownFiles = if (ownCacheDir.exists()) ownCacheDir.listFiles()?.filter { !it.name.startsWith(".") && it.isFile && it.length() > 0 } ?: emptyList() else emptyList()
                        val gameFiles = if (gameCacheDir.exists()) gameCacheDir.listFiles()?.filter { !it.name.startsWith(".") && it.isFile && it.length() > 0 } ?: emptyList() else emptyList()
                        val sharedFiles = if (sharedCacheDir.exists()) sharedCacheDir.listFiles()?.filter { !it.name.startsWith(".") && it.isFile && it.length() > 0 } ?: emptyList() else emptyList()

                        val effectiveCount = maxOf(ownFiles.size, gameFiles.size, sharedFiles.size)
                        val effectiveBytes = maxOf(ownFiles.sumOf { it.length() }, gameFiles.sumOf { it.length() }, sharedFiles.sumOf { it.length() })
                        val installed = effectiveCount >= 100

                        var canWrite = false
                        try {
                            if (!gameCacheDir.exists()) {
                                gameFilesDir.mkdirs()
                                gameCacheDir.mkdirs()
                            }
                            val probe = File(gameCacheDir, ".probe_${System.currentTimeMillis()}")
                            probe.writeText("ok")
                            canWrite = probe.exists()
                            probe.delete()
                        } catch (_: Exception) {}

                        // If already installed, skip expensive storage scans unless explicitly requested
                        val autoDetected = if (installed && !forceScan) null else findAutoDetectedCacheSource()
                        val hasManageStorage = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                            Environment.isExternalStorageManager()
                        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                            ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
                        } else {
                            true
                        }

                        result.success(mapOf(
                            "installed" to installed,
                            "fileCount" to effectiveCount,
                            "gameFileCount" to gameFiles.size,
                            "ownFileCount" to ownFiles.size,
                            "totalBytes" to effectiveBytes,
                            "targetPath" to gameCacheDir.absolutePath,
                            "ownCachePath" to ownCacheDir.absolutePath,
                            "canWrite" to canWrite,
                            "autoDetected" to autoDetected,
                            "hasManageStorage" to hasManageStorage
                        ))
                    } catch (e: Exception) {
                        result.error("CACHE_STATUS_ERROR", e.message, null)
                    }
                }
                "checkManageStoragePermission" -> {
                    val has = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        Environment.isExternalStorageManager()
                    } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                        ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
                    } else {
                        true
                    }
                    result.success(has)
                }
                "requestManageStoragePermission" -> {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        try {
                            val intent = Intent(Settings.ACTION_MANAGE_APP_ALL_FILES_ACCESS_PERMISSION).apply {
                                data = Uri.parse("package:$packageName")
                            }
                            startActivity(intent)
                            result.success(true)
                        } catch (_: Exception) {
                            try {
                                val intent = Intent(Settings.ACTION_MANAGE_ALL_FILES_ACCESS_PERMISSION)
                                startActivity(intent)
                                result.success(true)
                            } catch (e2: Exception) {
                                result.error("PERMISSION_ERROR", e2.message, null)
                            }
                        }
                    } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                        val granted = ContextCompat.checkSelfPermission(this, Manifest.permission.WRITE_EXTERNAL_STORAGE) == PackageManager.PERMISSION_GRANTED
                        if (!granted) {
                            pendingPermissionResult = result
                            ActivityCompat.requestPermissions(
                                this,
                                arrayOf(Manifest.permission.WRITE_EXTERNAL_STORAGE, Manifest.permission.READ_EXTERNAL_STORAGE),
                                REQUEST_STORAGE_PERMISSION
                            )
                        } else {
                            result.success(true)
                        }
                    } else {
                        result.success(true)
                    }
                }
                "pickCacheZip" -> {
                    try {
                        val pickIntent = Intent(Intent.ACTION_GET_CONTENT).apply {
                            type = "*/*"
                            addCategory(Intent.CATEGORY_OPENABLE)
                            putExtra(Intent.EXTRA_MIME_TYPES, arrayOf("application/zip", "application/x-zip-compressed", "application/x-zip", "application/octet-stream", "*/*"))
                        }
                        startActivityForResult(Intent.createChooser(pickIntent, "Select GameData_Cache ZIP"), REQUEST_PICK_CACHE_ZIP)
                        result.success(true)
                    } catch (e: Exception) {
                        result.error("PICK_ZIP_ERROR", e.message, null)
                    }
                }
                "pickCacheFolder" -> {
                    try {
                        val pickIntent = Intent(Intent.ACTION_OPEN_DOCUMENT_TREE).apply {
                            flags = Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION
                        }
                        startActivityForResult(Intent.createChooser(pickIntent, "Select GameData_Cache Folder"), REQUEST_PICK_CACHE_FOLDER)
                        result.success(true)
                    } catch (e: Exception) {
                        result.error("PICK_FOLDER_ERROR", e.message, null)
                    }
                }
                "installCacheFromPath" -> {
                    try {
                        val path = call.argument<String>("path")
                        val isZip = call.argument<Boolean>("isZip") ?: false
                        if (path != null) {
                            installCacheFromLocalPath(path, isZip)
                            result.success(true)
                        } else {
                            result.error("ARG_ERROR", "Path cannot be null", null)
                        }
                    } catch (e: Exception) {
                        result.error("INSTALL_ERROR", e.message, null)
                    }
                }
                else -> result.notImplemented()
            }
        }
    }

    private fun generateAssetGuidePdf(destFile: File) {
        val pdfDocument = PdfDocument()

        // Page 1: Overview, Locations & Moods
        run {
            val pageInfo = PdfDocument.PageInfo.Builder(595, 842, 1).create()
            val page = pdfDocument.startPage(pageInfo)
            val canvas = page.canvas

            val paint = Paint()
            paint.color = Color.rgb(18, 28, 42)
            canvas.drawRect(0f, 0f, 595f, 60f, paint)

            paint.color = Color.WHITE
            paint.textSize = 20f
            paint.isFakeBoldText = true
            canvas.drawText("ASSASSIN'S CREED IDENTITY", 30f, 38f, paint)

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(0, 229, 255)
            canvas.drawText("OFFICIAL ASSETS & MISSION DESIGN ENCYCLOPEDIA", 30f, 52f, paint)

            var y = 90f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 14f
            paint.isFakeBoldText = true
            canvas.drawText("1. LOCATIONS & HISTORICAL MAPS", 30f, y, paint)
            y += 20f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val places = listOf(
                "• Firenze - Palazzo della Signoria (Firenze_Palazzo): Open civic plaza, Palazzo Vecchio, alleys & high roofs.",
                "• Firenze - Santa Croce (Firenze_SantaCroce): Basilica piazza, open markets, cloister arcades & bell towers.",
                "• Roma - Colosseo (Roma_Coloseum): Ancient Roman amphitheater, ruins, gladiatorial arena & sunlit arches.",
                "• Monteriggioni - Villa Auditore (Monteriggioni): Tuscan fortress village, ramparts & Renaissance training grounds.",
                "• Forli - Romagna Stronghold (Forli): Waterways, marshland moat, imposing citadel towers & foggy ramparts."
            )
            for (p in places) {
                canvas.drawText(p, 30f, y, paint)
                y += 18f
            }

            y += 10f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 14f
            paint.isFakeBoldText = true
            canvas.drawText("2. LIGHTING & ATMOSPHERE (MOODS)", 30f, y, paint)
            y += 20f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val moods = listOf(
                "• Daytime: Bright Italian sunlight, high guard visibility, vibrant crowds.",
                "• Dusk: Golden sunset hour, lengthening shadows, lantern lighting in piazzas.",
                "• Nighttime: Deep shadows, torchlit alleyways, optimal for stealth assassinations.",
                "• Foggy: Reduced line of sight, mist rising from canals/streets, tense stealth atmosphere."
            )
            for (m in moods) {
                canvas.drawText(m, 30f, y, paint)
                y += 18f
            }

            y += 10f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 14f
            paint.isFakeBoldText = true
            canvas.drawText("3. TRAVERSAL, RIDES & INTERACTIVE PROPS", 30f, y, paint)
            y += 20f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val props = listOf(
                "• Haystacks: Leap of Faith landing zones, instant stealth hiding spots.",
                "• Sitting Benches: Blend among Florentine citizens to drop guard suspicion.",
                "• Wooden Beams & Ziplines: Rooftop highway traversal between buildings.",
                "• Scaffolding & Catwalks: High vantage points for aerial assassinations.",
                "• Horses & Wagons: Street obstacles, carts, and stable areas.",
                "• Pigeons & Birds: Perched on wooden vantage beams signaling Leap of Faith points."
            )
            for (pr in props) {
                canvas.drawText(pr, 30f, y, paint)
                y += 18f
            }

            pdfDocument.finishPage(page)
        }

        // Page 2: Persons, Characters & Animals
        run {
            val pageInfo = PdfDocument.PageInfo.Builder(595, 842, 2).create()
            val page = pdfDocument.startPage(pageInfo)
            val canvas = page.canvas

            val paint = Paint()
            paint.color = Color.rgb(18, 28, 42)
            canvas.drawRect(0f, 0f, 595f, 40f, paint)
            paint.color = Color.WHITE
            paint.textSize = 14f
            paint.isFakeBoldText = true
            canvas.drawText("CHARACTERS, PERSONS, ROLES & ANIMALS", 30f, 26f, paint)

            var y = 65f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("A. VIPs, CIVILIANS & ALLIES", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val allies = listOf(
                "• King / Sovereign / Duke: High-ranking nobility requiring royal protection or escort.",
                "• Maid / Servant: Domestic staff carrying secret keys or messages through hostile villas.",
                "• Florentine Noble: Wealthy Renaissance patron or target of extortion.",
                "• Courtesan: Seductive allies who distract Templar guards on patrol.",
                "• Thief / Informant: Agile rooftop runner who provides Borgia intelligence.",
                "• Friar / Monk: Group of holy men walking in procession; perfect for crowd blending.",
                "• Street Urchin / Courier: Fast messengers delivering sensitive correspondence."
            )
            for (a in allies) {
                canvas.drawText(a, 30f, y, paint)
                y += 17f
            }

            y += 12f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("B. ENEMIES, TEMPLARS & GUARDS", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val enemies = listOf(
                "• Borgia Captain (Special_Captain_01): Heavy plate armor, commanding aura, requires parry or stealth.",
                "• Templar Knight (Guard_Melee_01): Broadsword and shield combatant, aggressive in melee.",
                "• Crossbow Sniper (Guard_Ranged_01): Stationed on rooftops; spots assassin from long distance.",
                "• Heavy Brute (Guard_Brute_01): Two-handed battle-axe; immune to standard frontal attacks.",
                "• Agile Scout (Guard_Agile_01): Fast pursuer equipped with stiletto daggers; climbs roofs rapidly.",
                "• Corrupt Inquisitor (Special_Doctor_01): Secretive Templar mastermind directing local plots."
            )
            for (e in enemies) {
                canvas.drawText(e, 30f, y, paint)
                y += 17f
            }

            y += 12f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("C. ANIMALS & WILDLIFE", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val animals = listOf(
                "• Guard Dogs: Sniff out hidden assassins in haystacks; bark to alert nearby patrols.",
                "• Italian Horses: Mounts tethered near gates, stables, and carriages.",
                "• Flocks of Pigeons: Scatter when the assassin runs near, warning nearby sentries."
            )
            for (an in animals) {
                canvas.drawText(an, 30f, y, paint)
                y += 17f
            }

            pdfDocument.finishPage(page)
        }

        // Page 3: Mission Types, Rooftop Tailing & AI Workflow
        run {
            val pageInfo = PdfDocument.PageInfo.Builder(595, 842, 3).create()
            val page = pdfDocument.startPage(pageInfo)
            val canvas = page.canvas

            val paint = Paint()
            paint.color = Color.rgb(18, 28, 42)
            canvas.drawRect(0f, 0f, 595f, 40f, paint)
            paint.color = Color.WHITE
            paint.textSize = 14f
            paint.isFakeBoldText = true
            canvas.drawText("MISSION TYPES, TAILING RULES & AI GENERATION", 30f, 26f, paint)

            var y = 65f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("1. MISSION TYPES & OBJECTIVES", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val types = listOf(
                "• Assassinate / Eliminate: Infiltrate and take down high-value Templar targets.",
                "• Protect / Escort: Keep a King, Maid, or Ally alive while fighting off hostile ambushes.",
                "• Tail / Follow / Spy (Rooftop & Street): Follow target discreetly without raising suspicion.",
                "• Infiltrate & Steal: Enter fortified zone undetected to retrieve stolen Borgia codex pages."
            )
            for (t in types) {
                canvas.drawText(t, 30f, y, paint)
                y += 17f
            }

            y += 12f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("2. ROOFTOP TAILING & DETECTION RULES", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val tailingRules = listOf(
                "• Maintain Distance: Stay between 15 meters (too close, spotted) and 40 meters (too far, lost).",
                "• Rooftop Traversal: Leap across beams and chimneys to maintain sight while remaining elevated.",
                "• Detection Contingency: If a guard's indicator turns YELLOW/RED:",
                "    a) Break line of sight and dive into a haystack or sit on a bench.",
                "    b) Or perform a rapid silent assassination on the alerted sentry before the alarm sounds!"
            )
            for (r in tailingRules) {
                canvas.drawText(r, 30f, y, paint)
                y += 17f
            }

            y += 12f
            paint.color = Color.rgb(30, 40, 60)
            paint.textSize = 13f
            paint.isFakeBoldText = true
            canvas.drawText("3. HOW TO BUILD WITH AI (CHATGPT, CLAUDE, GEMINI)", 30f, y, paint)
            y += 18f

            paint.textSize = 10f
            paint.isFakeBoldText = false
            paint.color = Color.rgb(60, 60, 60)
            val aiSteps = listOf(
                "1. Upload SKILL.md and INSTRUCTIONS.md to your AI chat.",
                "2. The AI will interview you step-by-step: Slot -> Location -> Mood -> Objective -> Characters.",
                "3. The AI outputs a verified mission.json file.",
                "4. In the AC Identity Server app -> 'Upload' tab -> Enter ID, Title, and select your file.",
                "5. The server compiles it and links it to Contracts & Italy -> Open the game and enjoy!"
            )
            for (s in aiSteps) {
                canvas.drawText(s, 30f, y, paint)
                y += 17f
            }

            pdfDocument.finishPage(page)
        }

        try {
            if (!destFile.parentFile.exists()) destFile.parentFile.mkdirs()
            FileOutputStream(destFile).use { out ->
                pdfDocument.writeTo(out)
            }
        } finally {
            pdfDocument.close()
        }
    }

    private fun sendCacheProgress(status: String, count: Int, total: Int, bytes: Long, fileName: String?, error: String?) {
        runOnUiThread {
            val data = HashMap<String, Any?>().apply {
                put("status", status)
                put("count", count)
                put("total", total)
                put("bytes", bytes)
                put("fileName", fileName)
                put("error", error)
            }
            methodChannel?.invokeMethod("onCacheProgress", data)
        }
    }

    private fun findAutoDetectedCacheSource(): Map<String, Any>? {
        val downloadDir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
        val sdcard = Environment.getExternalStorageDirectory()

        val searchRoots = listOfNotNull(
            downloadDir,
            File(sdcard, "Download"),
            File(sdcard, "Downloads"),
            sdcard,
            File(sdcard, "ACIdentity")
        )

        for (root in searchRoots) {
            try {
                if (!root.exists() || !root.isDirectory) continue

                // Check directories up to 2 levels deep
                val candidateDirs = mutableListOf<File>()
                candidateDirs.add(root)
                root.listFiles()?.filter { it.isDirectory && !it.name.startsWith(".") }?.forEach { sub ->
                    candidateDirs.add(sub)
                    sub.listFiles()?.filter { it.isDirectory && !it.name.startsWith(".") }?.forEach { sub2 ->
                        candidateDirs.add(sub2)
                    }
                }

                for (dir in candidateDirs) {
                    val files = dir.listFiles()?.filter { it.isFile && !it.name.startsWith(".") && it.length() > 500 } ?: emptyList()
                    if (files.size >= 10) {
                        val hasGuid = files.any { it.name.contains("-") && it.name.length >= 30 }
                        val isCacheNamed = dir.name.contains("cache", ignoreCase = true) || dir.name.contains("data", ignoreCase = true) || dir.name.contains("identity", ignoreCase = true)
                        if (hasGuid || isCacheNamed) {
                            return mapOf(
                                "path" to dir.absolutePath,
                                "isZip" to false,
                                "count" to files.size,
                                "size" to files.sumOf { it.length() },
                                "name" to dir.name
                            )
                        }
                    }
                }

                // Check zip files in root and subdirectories
                val zips = mutableListOf<File>()
                root.listFiles()?.filter { it.isFile && it.name.endsWith(".zip", ignoreCase = true) && it.length() > 20 * 1024 * 1024 }?.forEach { zips.add(it) }
                root.listFiles()?.filter { it.isDirectory && !it.name.startsWith(".") }?.forEach { sub ->
                    sub.listFiles()?.filter { it.isFile && it.name.endsWith(".zip", ignoreCase = true) && it.length() > 20 * 1024 * 1024 }?.forEach { zips.add(it) }
                }

                val bestZip = zips.find { it.name.contains("cache", ignoreCase = true) || it.name.contains("identity", ignoreCase = true) } ?: zips.firstOrNull()
                if (bestZip != null) {
                    return mapOf(
                        "path" to bestZip.absolutePath,
                        "isZip" to true,
                        "count" to 104,
                        "size" to bestZip.length(),
                        "name" to bestZip.name
                    )
                }
            } catch (_: Exception) {}
        }
        return null
    }

    private fun installCacheFromZipUri(uri: Uri) {
        thread(name = "CacheZipInstaller") {
            var count = 0
            var totalBytes = 0L
            val estimatedTotal = 104
            try {
                sendCacheProgress("starting", 0, estimatedTotal, 0, null, null)
                if (!ownCacheDir.exists()) ownCacheDir.mkdirs()
                var directGameWriteOk = true
                try {
                    if (!gameCacheDir.exists()) {
                        gameFilesDir.mkdirs()
                        gameCacheDir.mkdirs()
                    }
                } catch (_: Exception) {
                    directGameWriteOk = false
                }

                contentResolver.openInputStream(uri)?.use { inStream ->
                    val zis = java.util.zip.ZipInputStream(java.io.BufferedInputStream(inStream))
                    var entry = zis.nextEntry
                    val buffer = ByteArray(128 * 1024)

                    while (entry != null) {
                        if (!entry.isDirectory) {
                            val rawName = entry.name
                            val fileName = rawName.substringAfterLast('/').substringAfterLast('\\')
                            if (fileName.isNotEmpty() && !fileName.startsWith(".") && !rawName.contains("__MACOSX")) {
                                // 1. Always extract into ownCacheDir (GUARANTEED)
                                val ownOutFile = File(ownCacheDir, fileName)
                                java.io.FileOutputStream(ownOutFile).use { fos ->
                                    var len = zis.read(buffer)
                                    while (len > 0) {
                                        fos.write(buffer, 0, len)
                                        totalBytes += len
                                        len = zis.read(buffer)
                                    }
                                }

                                // 2. Also try direct copy into gameCacheDir
                                if (directGameWriteOk) {
                                    try {
                                        val gameOutFile = File(gameCacheDir, fileName)
                                        java.io.FileInputStream(ownOutFile).use { fis ->
                                            java.io.FileOutputStream(gameOutFile).use { fos ->
                                                val copyBuf = ByteArray(64 * 1024)
                                                var cLen = fis.read(copyBuf)
                                                while (cLen > 0) {
                                                    fos.write(copyBuf, 0, cLen)
                                                    cLen = fis.read(copyBuf)
                                                }
                                            }
                                        }
                                    } catch (_: Exception) {
                                        directGameWriteOk = false
                                    }
                                }

                                count++
                                sendCacheProgress("copying", count, estimatedTotal, totalBytes, fileName, null)
                            }
                        }
                        zis.closeEntry()
                        entry = zis.nextEntry
                    }
                }
                sendCacheProgress("completed", count, count, totalBytes, null, null)
            } catch (e: Exception) {
                sendCacheProgress("error", count, estimatedTotal, totalBytes, null, e.message ?: "Extraction failed")
            }
        }
    }

    private fun installCacheFromFolderUri(treeUri: Uri) {
        thread(name = "CacheFolderInstaller") {
            var count = 0
            var totalBytes = 0L
            try {
                sendCacheProgress("starting", 0, 104, 0, null, null)
                if (!ownCacheDir.exists()) ownCacheDir.mkdirs()
                var directGameWriteOk = true
                try {
                    if (!gameCacheDir.exists()) {
                        gameFilesDir.mkdirs()
                        gameCacheDir.mkdirs()
                    }
                } catch (_: Exception) {
                    directGameWriteOk = false
                }

                val docTree = androidx.documentfile.provider.DocumentFile.fromTreeUri(this, treeUri)
                val targetDocDir = docTree?.findFile("GameData_Cache")
                    ?: docTree?.findFile("cache")
                    ?: docTree

                val docFiles: List<androidx.documentfile.provider.DocumentFile> = targetDocDir?.listFiles()?.filter { it.isFile && !it.name.orEmpty().startsWith(".") } ?: emptyList()
                val total = if (docFiles.isNotEmpty()) docFiles.size else 104
                val buffer = ByteArray(128 * 1024)

                for (docFile in docFiles) {
                    val fileName = docFile.name ?: continue
                    val ownOutFile = File(ownCacheDir, fileName)
                    contentResolver.openInputStream(docFile.uri)?.use { inStream ->
                        java.io.FileOutputStream(ownOutFile).use { fos ->
                            var len = inStream.read(buffer)
                            while (len > 0) {
                                fos.write(buffer, 0, len)
                                totalBytes += len
                                len = inStream.read(buffer)
                            }
                        }
                    }

                    if (directGameWriteOk) {
                        try {
                            val gameOutFile = File(gameCacheDir, fileName)
                            java.io.FileInputStream(ownOutFile).use { fis ->
                                java.io.FileOutputStream(gameOutFile).use { fos ->
                                    val copyBuf = ByteArray(64 * 1024)
                                    var cLen = fis.read(copyBuf)
                                    while (cLen > 0) {
                                        fos.write(copyBuf, 0, cLen)
                                        cLen = fis.read(copyBuf)
                                    }
                                }
                            }
                        } catch (_: Exception) {
                            directGameWriteOk = false
                        }
                    }

                    count++
                    sendCacheProgress("copying", count, total, totalBytes, fileName, null)
                }
                sendCacheProgress("completed", count, count, totalBytes, null, null)
            } catch (e: Exception) {
                sendCacheProgress("error", count, 104, totalBytes, null, e.message ?: "Folder copy failed")
            }
        }
    }

    private fun installCacheFromLocalPath(path: String, isZip: Boolean) {
        thread(name = "CacheLocalInstaller") {
            var count = 0
            var totalBytes = 0L
            val estimatedTotal = 104
            try {
                sendCacheProgress("starting", 0, estimatedTotal, 0, null, null)
                if (!ownCacheDir.exists()) ownCacheDir.mkdirs()
                var directGameWriteOk = true
                try {
                    if (!gameCacheDir.exists()) {
                        gameFilesDir.mkdirs()
                        gameCacheDir.mkdirs()
                    }
                } catch (_: Exception) {
                    directGameWriteOk = false
                }

                val srcFile = File(path)
                val buffer = ByteArray(128 * 1024)
                if (isZip) {
                    java.io.FileInputStream(srcFile).use { inStream ->
                        val zis = java.util.zip.ZipInputStream(java.io.BufferedInputStream(inStream))
                        var entry = zis.nextEntry
                        while (entry != null) {
                            if (!entry.isDirectory) {
                                val rawName = entry.name
                                val fileName = rawName.substringAfterLast('/').substringAfterLast('\\')
                                if (fileName.isNotEmpty() && !fileName.startsWith(".") && !rawName.contains("__MACOSX")) {
                                    val ownOutFile = File(ownCacheDir, fileName)
                                    java.io.FileOutputStream(ownOutFile).use { fos ->
                                        var len = zis.read(buffer)
                                        while (len > 0) {
                                            fos.write(buffer, 0, len)
                                            totalBytes += len
                                            len = zis.read(buffer)
                                        }
                                    }

                                    if (directGameWriteOk) {
                                        try {
                                            val gameOutFile = File(gameCacheDir, fileName)
                                            java.io.FileInputStream(ownOutFile).use { fis ->
                                                java.io.FileOutputStream(gameOutFile).use { fos ->
                                                    val copyBuf = ByteArray(64 * 1024)
                                                    var cLen = fis.read(copyBuf)
                                                    while (cLen > 0) {
                                                        fos.write(copyBuf, 0, cLen)
                                                        cLen = fis.read(copyBuf)
                                                    }
                                                }
                                            }
                                        } catch (_: Exception) {
                                            directGameWriteOk = false
                                        }
                                    }

                                    count++
                                    sendCacheProgress("copying", count, estimatedTotal, totalBytes, fileName, null)
                                }
                            }
                            zis.closeEntry()
                            entry = zis.nextEntry
                        }
                    }
                } else {
                    val targetDir = if (File(srcFile, "GameData_Cache").isDirectory) File(srcFile, "GameData_Cache")
                                    else if (File(srcFile, "cache").isDirectory) File(srcFile, "cache")
                                    else srcFile
                    val files: List<File> = targetDir.listFiles()?.filter { it.isFile && !it.name.startsWith(".") } ?: emptyList()
                    val total = if (files.isNotEmpty()) files.size else 104
                    for (f in files) {
                        val ownOutFile = File(ownCacheDir, f.name)
                        java.io.FileInputStream(f).use { fis ->
                            java.io.FileOutputStream(ownOutFile).use { fos ->
                                var len = fis.read(buffer)
                                while (len > 0) {
                                    fos.write(buffer, 0, len)
                                    totalBytes += len
                                    len = fis.read(buffer)
                                }
                            }
                        }

                        if (directGameWriteOk) {
                            try {
                                val gameOutFile = File(gameCacheDir, f.name)
                                java.io.FileInputStream(ownOutFile).use { fis ->
                                    java.io.FileOutputStream(gameOutFile).use { fos ->
                                        val copyBuf = ByteArray(64 * 1024)
                                        var cLen = fis.read(copyBuf)
                                        while (cLen > 0) {
                                            fos.write(copyBuf, 0, cLen)
                                            cLen = fis.read(copyBuf)
                                        }
                                    }
                                }
                            } catch (_: Exception) {
                                directGameWriteOk = false
                            }
                        }

                        count++
                        sendCacheProgress("copying", count, total, totalBytes, f.name, null)
                    }
                }
                sendCacheProgress("completed", count, count, totalBytes, null, null)
            } catch (e: Exception) {
                val rootOk = tryRootFallback(path, isZip)
                if (rootOk) {
                    val currentCount = gameCacheDir.listFiles()?.filter { !it.name.startsWith(".") && it.isFile }?.size ?: 104
                    sendCacheProgress("completed", currentCount, currentCount, totalBytes, null, null)
                } else {
                    sendCacheProgress("error", count, estimatedTotal, totalBytes, null, e.message ?: "Local copy failed")
                }
            }
        }
    }

    private fun tryRootFallback(sourcePath: String, isZip: Boolean): Boolean {
        return try {
            val targetPath = "/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache"
            val cmd = if (isZip) {
                "mkdir -p $targetPath && unzip -o -j \"$sourcePath\" -d $targetPath && chmod -R 777 $targetPath"
            } else {
                "mkdir -p $targetPath && cp -rf \"$sourcePath\"/* $targetPath/ && chmod -R 777 $targetPath"
            }
            val p = Runtime.getRuntime().exec(arrayOf("su", "-c", cmd))
            p.waitFor() == 0
        } catch (_: Exception) {
            false
        }
    }
}
