# Assassin's Creed Identity — Custom Mission Specification & Archetype Reference

This document defines the schema, archetypes, and configuration options for authoring custom missions in Assassin's Creed Identity (v2.8.2).

---

## 1. Complete Mission JSON Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ACIdentityCustomMission",
  "type": "object",
  "required": ["missionId", "title", "campaign", "location", "mood", "objective", "playerSpawn", "targets"],
  "properties": {
    "missionId": {
      "type": "string",
      "description": "Unique mission identifier. Must match the target slot (e.g. story_crows_01 or story_custom_01)."
    },
    "title": {
      "type": "string",
      "description": "Display title in Animus UI and mission results screen."
    },
    "campaign": {
      "type": "string",
      "enum": [
        "Animus VR Training",
        "Italy - A Murder of Crows",
        "Forlì - A Crimson Sunset",
        "Bonus Contracts"
      ]
    },
    "location": {
      "type": "string",
      "enum": [
        "Firenze_Palazzo",
        "Firenze_SantaCroce",
        "Roma_Coloseum",
        "Monteriggioni",
        "Forli"
      ]
    },
    "mood": {
      "type": "string",
      "enum": ["Daytime", "Nighttime", "Dusk", "Foggy", "Sunny", "Siege"]
    },
    "briefing": {
      "type": "string",
      "description": "Narrative backstory shown on the briefing card."
    },
    "objective": {
      "type": "string",
      "description": "Main HUD objective display string."
    },
    "playerSpawn": {
      "type": "object",
      "required": ["x", "y", "z", "rot"],
      "properties": {
        "x": { "type": "number" },
        "y": { "type": "number" },
        "z": { "type": "number" },
        "rot": { "type": "number" }
      }
    },
    "targets": {
      "type": "array",
      "items": {
        "$ref": "#/definitions/Target"
      }
    },
    "rewards": {
      "type": "object",
      "properties": {
        "coins": { "type": "integer", "default": 500 },
        "xp": { "type": "integer", "default": 1000 }
      }
    }
  },
  "definitions": {
    "Target": {
      "type": "object",
      "required": ["archetype", "x", "y", "z"],
      "properties": {
        "archetype": {
          "type": "string",
          "enum": [
            "Guard_Melee_01",
            "Guard_Agile_01",
            "Guard_Brute_01",
            "Guard_Crossbow_01",
            "Special_Captain_01",
            "Special_Doctor_01",
            "NPC_Leonardo_01",
            "NPC_Civilian_01"
          ]
        },
        "x": { "type": "number" },
        "y": { "type": "number" },
        "z": { "type": "number" },
        "rot": { "type": "number", "default": 0.0 },
        "isTarget": { "type": "boolean", "default": true }
      }
    }
  }
}
```

---

## 2. Location & Atmosphere Matrix

| Location Key | In-Engine Scene Name | Supported Moods | Safe Player Spawn (X, Y, Z, Rot) |
| :--- | :--- | :--- | :--- |
| `Firenze_Palazzo` | `Firenze_Palazzo_day` / `Firenze_Palazzo_night` | `Daytime`, `Nighttime` | `(-6.0, 0.5, -6.0, 0.0)` |
| `Firenze_SantaCroce` | `Firenze_SantaCroce_sunny` | `Sunny`, `Daytime` | `(0.0, 0.5, 0.0, 0.0)` |
| `Roma_Coloseum` | `Roma_Coloseum_dusk` / `Roma_Coloseum_fog` | `Dusk`, `Foggy`, `Afternoon` | `(10.0, 1.0, -10.0, 0.0)` |
| `Monteriggioni` | `Monteriggioni_day` / `Monteriggioni_night` | `Daytime`, `Nighttime` | `(0.0, 1.0, -20.0, 0.0)` |
| `Forli` | `Forli_dusk` / `Forli_siege` / `Forli_night` | `Dusk`, `Siege`, `Night` | `(5.0, 0.5, -5.0, 0.0)` |

---

## 3. Character & Enemy Archetypes

| Archetype Name | Combat Role | Health / Armor | Behavior |
| :--- | :--- | :--- | :--- |
| `Guard_Melee_01` | Standard Patrol Guard | Low | Standard sword attacks, alert whistles. |
| `Guard_Agile_01` | Fast Runner / Dodger | Medium | Dodges heavy attacks, pursues quickly across rooftops. |
| `Guard_Brute_01` | Heavy Armor Axe-Wielder | High | Unblockable heavy swings, requires parrying or smoke bombs. |
| `Guard_Crossbow_01` | Rooftop Sniper | Low | Ranged attacks from elevated positions; alert lines of sight. |
| `Special_Captain_01` | Borgia Officer / Target | High | High damage, commands nearby guards to attack in formation. |
| `Special_Doctor_01` | Plague Doctor / Informant | Low | Fleeing target, carries secrets or poison flasks. |
| `NPC_Leonardo_01` | Escort Ally | Medium | Follows player path; triggers mission failure if health drops to 0. |

---

## 4. Example Missions

### Example A: Elimination in Florence
```json
{
  "missionId": "story_crows_01",
  "title": "A Hunt in Florence",
  "campaign": "Italy - A Murder of Crows",
  "location": "Firenze_Palazzo",
  "mood": "Daytime",
  "briefing": "Corrupt Borgia mercenaries have taken over the Piazza della Signoria. Eliminate their commander and his bodyguards.",
  "objective": "Eliminate 3 Borgia Guards in the Piazza.",
  "playerSpawn": { "x": -6.0, "y": 0.5, "z": -6.0, "rot": 0.0 },
  "targets": [
    { "archetype": "Special_Captain_01", "x": 0.0, "y": 0.5, "z": 8.0, "rot": 180.0, "isTarget": true },
    { "archetype": "Guard_Melee_01", "x": -3.0, "y": 0.5, "z": 6.0, "rot": 140.0, "isTarget": true },
    { "archetype": "Guard_Brute_01", "x": 3.0, "y": 0.5, "z": 6.0, "rot": 220.0, "isTarget": true }
  ],
  "rewards": { "coins": 650, "xp": 1200 }
}
```

### Example B: Nighttime Colosseum Infiltration
```json
{
  "missionId": "story_crows_04",
  "title": "Shadows of the Colosseum",
  "campaign": "Italy - A Murder of Crows",
  "location": "Roma_Coloseum",
  "mood": "Dusk",
  "briefing": "Infiltrate the ancient ruins of the Flavian Amphitheatre. Eliminate the Crow courier before he transmits the stolen cipher.",
  "objective": "Infiltrate the Colosseum ruins and eliminate the contact.",
  "playerSpawn": { "x": 10.0, "y": 1.0, "z": -10.0, "rot": 0.0 },
  "targets": [
    { "archetype": "Special_Doctor_01", "x": 15.0, "y": 1.0, "z": 5.0, "rot": 90.0, "isTarget": true },
    { "archetype": "Guard_Agile_01", "x": 12.0, "y": 1.0, "z": 0.0, "rot": 45.0, "isTarget": true }
  ],
  "rewards": { "coins": 900, "xp": 1800 }
}
```

---

## 5. Deployment Options

### Method A: Through Companion App (In-Game UI)
1. In the **AC Identity Server** companion app, tap **Game Access & Level Manager**.
2. Tap **Upload Custom Mission**.
3. Select your `.bin` or `.json` file, enter the target slot (`story_crows_01`, etc.), and tap **Save & Deploy**.
4. Tap **Open Assassin's Creed Identity** — the new mission will load automatically when entering the mission in-game.

### Method B: Via ADB Terminal
```bash
# Push directly into the custom missions storage directory
adb shell mkdir -p /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/custom_missions
adb push openMission_story_crows_01.bin /sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/custom_missions/
```
The companion server dynamically checks this folder first when `openMission` requests are received and serves the custom definition seamlessly.

