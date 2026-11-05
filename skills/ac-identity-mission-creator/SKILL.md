---
name: ac-identity-mission-creator
description: "Author, customize, and deploy new custom levels and missions for Assassin's Creed Identity offline. Enables AI agents and developers to generate playable missions via prompt or manual JSON/Protobuf parameters, inject them into the game catalog, configure objectives, enemy targets, spawn coordinates, and deploy directly to the companion server."
---

# Assassin's Creed Identity — Custom Mission & Level Creator

This skill provides an automated workflow and specification for creating custom story and contract missions in **Assassin's Creed Identity (v2.8.2)**.

Any AI agent or developer invoking this skill can create fully playable missions by specifying:
- **Campaign**: Montreal VR Training, Italy (A Murder of Crows), Forlì (A Crimson Sunset), or Custom Contracts.
- **Location & Environment**: City region, daytime/nighttime mood, and weather.
- **Player Spawn Point**: Collision-safe 3D coordinates and orientation.
- **Objectives & Targets**: Target type, counts, position, character archetype, and rewards.
- **Automatic Packaging & Deployment**: Generating `openMission_<id>.bin`, updating `localstorage.json`, and registering with the offline companion server.

---

## Mission Structure & Parameters

Every mission in Assassin's Creed Identity is composed of:
1. **Mission Identifier (`id`)**: A unique string slug (e.g. `story_crows_custom_01`, `contract_firenze_01`).
2. **Metadata**:
   - `title`: Display name in the mission list and briefing.
   - `briefing`: Narrative text displayed in the Animus loading sequence.
   - `objective`: Main objective (e.g., "Assassinate the corrupt captain", "Escort Leonardo").
3. **Environment**:
   - `era`: `Renaissance_EEra`
   - `region`: `Firenze_Palazzo`, `Firenze_SantaCroce`, `Roma_Coloseum`, `Monteriggioni`, `Forli`
   - `mood`: `Daytime`, `Nighttime`, `Dusk`, `Foggy`, `Sunny`, `Siege`
4. **Player Spawn Coordinates**:
   - `x`, `y`, `z`: 3D World position (Must avoid solid buildings to prevent collision glitching).
   - `rotation`: Facing angle in degrees.
5. **Target Enemies & NPCs**:
   - `archetype`: `Guard_Melee_01`, `Guard_Brute_01`, `Special_Captain_01`, `Special_Doctor_01`, etc.
   - `guid`: Character template GUID.
   - `position`: `(x, y, z)` and heading `rot`.
   - `isTarget`: `true` for primary assassination targets; `false` for escort/patrol.
6. **Rewards**: Coins and Experience Points (XP).

---

## Safe Spawn Coordinates (Collision-Free)

Always use verified coordinates from this table to avoid spawning inside building geometry:

| Location | Region Key | Safe Player (X, Y, Z, Rot) | Notes |
| :--- | :--- | :--- | :--- |
| **Firenze Piazza della Signoria** | `Firenze_Palazzo` | `(-6.0, 0.5, -6.0, 0)` | Open cobblestone plaza outside Palazzo Vecchio |
| **Firenze Santa Croce** | `Firenze_SantaCroce` | `(0.0, 0.5, 0.0, 0)` | Open church square |
| **Roma Colosseum** | `Roma_Coloseum` | `(10.0, 1.0, -10.0, 0)` | Open ground inside Colosseum outer ring |
| **Monteriggioni Villa** | `Monteriggioni` | `(0.0, 1.0, -20.0, 0)` | Villa Auditore courtyards and gardens |
| **Forlì Marshlands** | `Forli` | `(5.0, 0.5, -5.0, 0)` | Open field near the citadel gates |

---

## Creating a Mission via Prompt

When a user requests:
> *"Create a new night-time mission in Florence where I have to assassinate a Borgia spy near Palazzo Vecchio."*

Follow these steps:

### Step 1: Generate Mission JSON Definition
Create a JSON file named `<mission_id>.json`:

```json
{
  "missionId": "story_custom_firenze_spy",
  "title": "Shadows over the Signoria",
  "campaign": "Italy - A Murder of Crows",
  "location": "Firenze_Palazzo",
  "mood": "Nighttime",
  "briefing": "A Borgia spy has infiltrated Florence under cover of darkness to pass stolen secrets to Rome. Find and eliminate him before he escapes.",
  "objective": "Assassinate the Borgia Spy in the Piazza.",
  "playerSpawn": {
    "x": -6.0,
    "y": 0.5,
    "z": -6.0,
    "rot": 0.0
  },
  "targets": [
    {
      "archetype": "Special_Captain_01",
      "guid": "5f68b35a-8b65-4d7a-8b89-6e3e5e49aa1b",
      "x": 2.0,
      "y": 0.5,
      "z": 12.0,
      "rot": 180.0,
      "isTarget": true
    }
  ],
  "rewards": {
    "coins": 800,
    "xp": 1500
  }
}
```

### Step 2: Compile to Binary via AcierOfflineServer
Run the compilation tool:
```bash
dotnet run --project offline/AcierOfflineServer -- --build-mission <path_to_json>
```
This generates:
- `openMission_<missionId>.bin`
- `endMission_<missionId>.bin`
- Updates `statics.bin` (or registers the dynamic catalog entry)

### Step 3: Register in Companion Server Storage
Push the binary or JSON to the device:
```bash
adb push openMission_<missionId>.bin /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/custom_missions/
```
Update `/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/localstorage.json` to mark the mission as unlocked:
```json
"missionprogress": {
  "story_custom_firenze_spy": {
    "WasUnlockShown": true,
    "WasCompletedShown": false
  }
}
```

### Step 4: Launch & Play
Open the game via the Companion Server app or run:
```bash
adb shell am start -n com.ubisoft.assassinscreed.identity/com.acid.plugins.activity.BridgeActivity
```
Select the mission and tap **Explore → Start** to enter your custom mission!

---

## In-App Mission Creator / Uploader

The Companion Server app provides a built-in UI for uploading and managing custom missions:
1. Open **AC Identity Server**.
2. Tap **Missions & Unlocks**.
3. Tap the **Download Skill & AI Guide** icon (`↓`) in the top app bar to export the AI skill bundle directly to your phone's `/sdcard/Download/ACIdentity_Mission_Skill/` folder and copy ready-made AI prompts.
4. Tap the **Upload / Pick File** icon (`↑`) to browse and pick any `.json` or `.bin` mission file directly from your device storage, or enter custom mission parameters manually.
5. Tap **Open Assassin's Creed Identity** to launch the game and play your custom mission!

