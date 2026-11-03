@echo off
echo =================================================
echo  Assassin's Creed Identity - 100%% Offline Bundle
echo =================================================

where adb >nul 2>nul
if %errorlevel% neq 0 (
    echo [-] Error: 'adb' command not found. Please install Android Platform Tools.
    pause
    exit /b 1
)

echo [+] Connected device check...
adb devices
echo [+] 1/3: Installing AC Identity Companion Server APK...
adb install -r "ACIdentity_CompanionServer.apk"

echo [+] 2/3: Installing AC Identity Game APK...
adb install -r "ACIdentity_Game.apk"

echo [+] 3/3: Pushing Game Data Cache (1.5 GB)...
adb shell "mkdir -p /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache"
adb push "GameData_Cache/." "/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache/"

echo =================================================
echo [✓] Installation Complete!
echo HOW TO PLAY:
echo 1. On your phone, open the 'AC Identity Server' app.
echo 2. Confirm the green banner says 'SERVER RUNNING'.
echo 3. Open 'Assassin's Creed Identity' and enjoy full offline play anywhere!
echo =================================================
pause

