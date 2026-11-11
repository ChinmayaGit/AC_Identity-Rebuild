#!/usr/bin/env bash
set -e

echo "================================================="
echo " Assassin's Creed Identity - 100% Offline Bundle"
echo "================================================="

if ! command -v adb &> /dev/null; then
    echo "[-] Error: 'adb' command not found. Please install Android Platform Tools."
    exit 1
fi

DEVICE_COUNT=$(adb devices | grep -v "List" | grep "device$" | wc -l | tr -d ' ')
if [ "$DEVICE_COUNT" -eq 0 ]; then
    echo "[-] No Android device detected via ADB."
    echo "    Please connect your phone via USB with 'USB Debugging' enabled."
    exit 1
fi

echo "[+] Connected device detected."
echo "[+] 1/3: Installing AC Identity Companion Server APK..."
adb install -r "ACIdentity_CompanionServer.apk"

echo "[+] 2/3: Installing AC Identity Game APK..."
adb install -r --bypass-low-target-sdk-block "ACIdentity_Game.apk" 2>/dev/null || adb install -r "ACIdentity_Game.apk"

echo "[+] 3/4: Pushing Game Data Cache (1.5 GB)..."
adb shell "mkdir -p /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache"
adb push "GameData_Cache/." "/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache/"

echo "[+] 4/4: Initializing Unlocked Profile Storage..."
adb push "localstorage_unlocked.json" "/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/localstorage.json"

echo "================================================="
echo "[✓] Installation Complete!"
echo "HOW TO PLAY:"
echo "1. On your phone, open the 'AC Identity Server' app."
echo "2. Confirm the green status card says 'SERVER ONLINE'."
echo "3. Tap 'Launch Game' (or open 'AC Identity' from home screen)."
echo "4. Enjoy playing Assassin's Creed Identity 100% offline!"
echo "================================================="

