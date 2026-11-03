# Assassin's Creed Identity - 100% Offline Standalone Release

Play the complete **Assassin's Creed Identity** (v2.8.2) anywhere, 100% locally on your Android device — **no PC, Mac, external server, or Wi-Fi required!**

---

## 📦 What's Inside This Bundle

| File / Folder | Description |
| :--- | :--- |
| **`ACIdentity_Game.apk`** | Game client pre-patched for on-device localhost (`127.0.0.1:8080`). Disables obsolete server signature checks, Play license checks, and forces ETC2 rendering. |
| **`ACIdentity_CompanionServer.apk`** | Lightweight Android companion app running a background foreground service that simulates the Ubisoft Acier server on `127.0.0.1:8080`. |
| **`GameData_Cache/`** | Complete 1.5 GB asset cache (all 104 game data files, high-res textures, Italian Renaissance maps, audio, and character models). |
| **`install.sh`** | 1-click automatic installer for macOS and Linux (via ADB). |
| **`install.bat`** | 1-click automatic installer for Windows (via ADB). |

---

## 🎯 Authentic Mission Objectives & Real Storyline

Unlike previous generic placeholders, every mission features authentic story setups, custom spawns, and distinct gameplay objectives:

### 🎓 Animus Training (Montreal / Monteriggioni)
1. **Tutorial 1 - Interaction**: Approach and talk to the Doctor in the town square. Non-combat interaction objective.
2. **Tutorial 2 - Free Running**: Rooftop parkour obstacle course across Monteriggioni rooftops.
3. **Tutorial 3 - Combat**: 1-on-1 sparring session with a training partner under instructor supervision.
4. **Tutorial 4 - Skills**: Combat skill dummy brute.
5. **Tutorial 5 - Eagle Vision**: Locate an undercover spy concealed within a crowd of civilians.
6. **Tutorial 6 - Air Assassination**: Perch on a high rooftop ledge directly above a sentry and trigger the Air Assassination jump takedown.
7. **Tutorial 7 - Stealth Assassination**: Approach a patrolling guard from behind cover for a silent backstab.

### 🦅 Campaign 1: A Murder of Crows (10 Missions)
- **Mission 1: The Corrupted**: Monteriggioni town square; eliminate the 3 corrupt tax collector mercenaries.
- **Mission 2: A Healer's Blood**: Hunt down high-value target *Pietro de Galencia* in his night alley clinic.
- **Mission 3: Vengeful Eyes**: Defend and escort *Leonardo da Vinci* from Borgia attackers in Firenze.
- **Mission 4: An Assassin's Requiem**: Clear Borgia agents from the Roman Colosseum Catacombs.
- **Mission 5: Broken Chains**: Rescue *Demetrio the Inventor* from slave drivers in Firenze Santa Croce.
- **Mission 6: A Storm of Crows**: Eliminate Borgia camp commander *Captain Matteo*.
- **Mission 7: Burning Pages**: Infiltrate Palazzo courtyard and assassinate *The Bookkeeper* burning historical manuscripts.
- **Mission 8: Under the Red Sun**: Repel a Crow ambush squad alongside *Sirus Favero*.
- **Mission 9: A Dangerous Trade**: Intercept a clandestine nighttime courier transaction in Firenze.
- **Mission 10: Saviors of Roma**: Final confrontation against the Crow leader *Il Corvo* at the Colosseum alongside *Niccolò Machiavelli*.

### 🏰 Campaign 2: Forlì - A Crimson Sunset (4 Missions)
- **Mission 1: An Unexpected Summon**: Track down 3 undercover Crows hiding in Forlì marketplace.
- **Mission 2: The Nest of the Bird**: Assassinate the Crow Nest Commander inside the fortress courtyard.
- **Mission 3: A Grey Sky (Siege)**: Infiltrate the siege camp without detection to stop prisoner executions.
- **Mission 4: One for All**: Rendezvous with *Niccolò Machiavelli* and *Ezio Auditore* to eliminate the Borgia Lighthouse Commander.

---

## 🚀 Quick Setup Instructions

### Option 1: Automatic 1-Click Install (Recommended)
1. Enable **USB Debugging** on your phone (*Settings > Developer Options > USB Debugging*).
2. Connect your phone to your computer via USB.
3. Run the installer script:
   - **macOS / Linux**: Open Terminal in this folder and run:
     ```bash
     ./install.sh
     ```
   - **Windows**: Double-click:
     ```bat
     install.bat
     ```

### Option 2: Manual Install
1. Install both APKs on your phone:
   ```bash
   adb install -r ACIdentity_CompanionServer.apk
   adb install -r ACIdentity_Game.apk
   ```
2. Copy the `GameData_Cache` files into the game cache directory:
   ```bash
   adb push GameData_Cache/. /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache/
   ```

---

## 🎮 How to Play

1. **Launch `AC Identity Server`** on your phone.
2. Confirm the green status card says **`SERVER RUNNING`**.
3. Tap **Open Assassin's Creed Identity** (or open the game from your home screen).
4. Enjoy playing Assassin's Creed Identity offline!

