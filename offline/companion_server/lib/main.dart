import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart'
    show MethodChannel, rootBundle, Clipboard, ClipboardData;

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const AcierServerApp());
}

class AcierServerApp extends StatelessWidget {
  const AcierServerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'AC Identity Offline Server',
      debugShowCheckedModeBanner: false,
      theme: ThemeData.dark(useMaterial3: true).copyWith(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF00E5FF),
          brightness: Brightness.dark,
        ),
        scaffoldBackgroundColor: const Color(0xFF0B1017),
      ),
      home: const ServerScreen(),
    );
  }
}

class MissionMeta {
  final String id;
  final String campaign;
  final String title;
  final String location;
  final String objective;
  final bool isCustom;
  final String targetSlot;

  const MissionMeta({
    required this.id,
    required this.campaign,
    required this.title,
    required this.location,
    required this.objective,
    this.isCustom = false,
    this.targetSlot = 'story_0015',
  });
}

class PbWriter {
  final BytesBuilder _b = BytesBuilder();

  void writeVarint(int field, int value) {
    _writeTag(field, 0);
    _writeVarintValue(value);
  }

  void _writeTag(int field, int wire) {
    _writeVarintValue((field << 3) | wire);
  }

  void _writeVarintValue(int value) {
    if (value < 0) {
      if (value == -2) {
        _b.add(const [
          0xFE,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0x01,
        ]);
        return;
      }
      if (value == -1) {
        _b.add(const [
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0xFF,
          0x01,
        ]);
        return;
      }
      int v = value;
      for (int i = 0; i < 9; i++) {
        _b.addByte((v & 0x7F) | 0x80);
        v >>= 7;
      }
      _b.addByte(1);
      return;
    }
    int v = value;
    while (v >= 0x80) {
      _b.addByte((v & 0x7F) | 0x80);
      v >>= 7;
    }
    _b.addByte(v & 0x7F);
  }

  void writeFloat(int field, double value) {
    _writeTag(field, 5);
    final bd = ByteData(4)..setFloat32(0, value, Endian.little);
    _b.add(bd.buffer.asUint8List());
  }

  void writeString(int field, String value) {
    writeBytes(field, utf8.encode(value));
  }

  void writeBytes(int field, List<int> bytes) {
    _writeTag(field, 2);
    _writeVarintValue(bytes.length);
    _b.add(bytes);
  }

  void writeMsg(int field, PbWriter sub) {
    writeBytes(field, sub.toBytes());
  }

  Uint8List toBytes() => _b.toBytes();
}

Uint8List buildCustomMissionBinary(Map<String, dynamic> json) {
  final mid =
      json['missionId'] as String? ??
      json['id'] as String? ??
      'custom_mission_01';
  final regionStr = json['region'] as String? ?? 'Firenze_Palazzo';
  final moodStr = json['mood'] as String? ?? 'Daytime';

  double px = -6.0, py = 0.5, pz = -6.0;
  if (json['playerSpawn'] is Map) {
    final sp = json['playerSpawn'] as Map;
    if (sp['x'] != null) px = (sp['x'] as num).toDouble();
    if (sp['y'] != null) py = (sp['y'] as num).toDouble();
    if (sp['z'] != null) pz = (sp['z'] as num).toDouble();
  }

  int regionVal = 2; // Firenze_Palazzo
  String locName = 'Firenze';
  String scene = 'Firenze_palazzo_daytime';
  if (regionStr.contains('Roma')) {
    regionVal = 3;
    locName = 'Roma';
    scene = 'Roma_coloseum_afternoon';
  } else if (regionStr.contains('SantaCroce')) {
    regionVal = 2;
    locName = 'Firenze';
    scene = 'Firenze_santacroce_sunny';
  } else if (regionStr.contains('Monteriggioni')) {
    regionVal = 1;
    locName = 'Monteriggioni';
    scene = 'MonteRiggioni_DayTime';
  } else if (regionStr.contains('Forli')) {
    regionVal = 4;
    locName = 'Forli';
    scene = 'Forli_dusk';
  }

  int moodVal = 1; // Daytime
  if (moodStr == 'Nighttime') {
    moodVal = 2;
    scene = scene
        .replaceAll('daytime', 'nighttime')
        .replaceAll('DayTime', 'Nighttime');
  } else if (moodStr == 'Dusk') {
    moodVal = 3;
  } else if (moodStr == 'Foggy') {
    moodVal = 4;
  } else if (moodStr == 'Sunny') {
    moodVal = 5;
  }

  // Component 0: MissionRootContent
  final rootContent = PbWriter()
    ..writeVarint(5, 1)
    ..writeVarint(6, 1)
    ..writeString(8, mid)
    ..writeVarint(11, 1)
    ..writeVarint(12, regionVal)
    ..writeString(13, locName)
    ..writeVarint(15, moodVal)
    ..writeString(16, scene)
    ..writeVarint(19, 1)
    ..writeMsg(20, PbWriter());

  final comp0 = PbWriter()
    ..writeMsg(6, rootContent)
    ..writeVarint(1000, 6);

  // Component 1: Hotspots
  final comp1 = PbWriter()
    ..writeMsg(4, PbWriter()..writeMsg(10, PbWriter()..writeVarint(9, 2)))
    ..writeVarint(1000, 4);

  // Targets
  final targetsList = <Map<String, dynamic>>[];
  if (json['targets'] is List && (json['targets'] as List).isNotEmpty) {
    for (final t in json['targets']) {
      if (t is Map<String, dynamic>) targetsList.add(t);
    }
  } else {
    targetsList.add({
      'name': 'Borgia_Infiltrator_01',
      'guid': 'ntt_bd49bc5f',
      'x': px + 8.0,
      'y': py,
      'z': pz + 18.0,
      'rot': 180.0,
    });
  }

  // Component 2: Kill Objective
  final objBase = PbWriter()
    ..writeString(4, '3430')
    ..writeString(5, '3430')
    ..writeVarint(6, -2)
    ..writeVarint(7, -2)
    ..writeVarint(10, 1)
    ..writeVarint(13, 6)
    ..writeMsg(
      20,
      PbWriter()
        ..writeVarint(1, -2)
        ..writeVarint(2, -2)
        ..writeVarint(3, 2)
        ..writeVarint(9, 3),
    );

  final targetContent = PbWriter()
    ..writeVarint(9, 101)
    ..writeMsg(10, objBase);

  final killContent = PbWriter()
    ..writeVarint(8, targetsList.length)
    ..writeMsg(10, targetContent);

  final comp2 = PbWriter()
    ..writeMsg(30, killContent)
    ..writeVarint(1000, 30);

  // Component 3: Timer backup
  final comp3 = PbWriter()
    ..writeMsg(
      42,
      PbWriter()
        ..writeMsg(
          1,
          PbWriter()
            ..writeVarint(1, 1)
            ..writeVarint(2, 1)
            ..writeVarint(6, 1)
            ..writeVarint(7, 1)
            ..writeMsg(
              20,
              PbWriter()
                ..writeVarint(1, -2)
                ..writeVarint(2, -2)
                ..writeVarint(9, 4),
            ),
        )
        ..writeVarint(2, 86400)
        ..writeMsg(3, PbWriter()..writeVarint(2, 4))
        ..writeMsg(4, PbWriter()..writeVarint(2, 4))
        ..writeMsg(5, PbWriter()..writeVarint(2, 4)),
    )
    ..writeVarint(1000, 42);

  // Player Spawn node
  final spawnNode = PbWriter()
    ..writeMsg(
      2,
      PbWriter()
        ..writeMsg(
          5,
          PbWriter()..writeMsg(
            10,
            PbWriter()
              ..writeVarint(1, -2)
              ..writeVarint(2, -2)
              ..writeVarint(9, 1),
          ),
        )
        ..writeVarint(1000, 5),
    )
    ..writeString(6, 'PlayerSpawn')
    ..writeFloat(7, px)
    ..writeFloat(8, py)
    ..writeFloat(9, pz);

  // Children: spawn + targets
  final children = <PbWriter>[spawnNode];
  int currentId = 101;
  for (final t in targetsList) {
    final name = t['name'] as String? ?? 'Target_$currentId';
    final guid = t['guid'] as String? ?? 'ntt_bd49bc5f';
    final x = (t['x'] as num?)?.toDouble() ?? (px + 6.0);
    final y = (t['y'] as num?)?.toDouble() ?? py;
    final z = (t['z'] as num?)?.toDouble() ?? (pz + 12.0);
    final rot = (t['rot'] as num?)?.toDouble() ?? 180.0;

    final npcNode = PbWriter()
      ..writeMsg(
        2,
        PbWriter()
          ..writeMsg(
            12,
            PbWriter()..writeMsg(
              10,
              PbWriter()
                ..writeFloat(4, 5.0)
                ..writeMsg(
                  10,
                  PbWriter()
                    ..writeVarint(1, -2)
                    ..writeVarint(2, -2)
                    ..writeVarint(9, 1000 + currentId),
                ),
            ),
          )
          ..writeVarint(1000, 12),
      )
      ..writeMsg(
        2,
        PbWriter()
          ..writeMsg(
            13,
            PbWriter()
              ..writeString(9, guid)
              ..writeMsg(
                10,
                PbWriter()
                  ..writeFloat(7, 1.0)
                  ..writeMsg(
                    10,
                    PbWriter()
                      ..writeVarint(1, -2)
                      ..writeVarint(2, -2)
                      ..writeVarint(9, currentId),
                  ),
              ),
          )
          ..writeVarint(1000, 13),
      )
      ..writeFloat(4, rot)
      ..writeString(6, name)
      ..writeFloat(7, x)
      ..writeFloat(8, y)
      ..writeFloat(9, z);

    children.add(npcNode);
    currentId++;
  }

  // Root Node
  final rootNode = PbWriter();
  for (final ch in children) {
    rootNode.writeMsg(1, ch);
  }
  rootNode
    ..writeMsg(2, comp0)
    ..writeMsg(2, comp1)
    ..writeMsg(2, comp2)
    ..writeMsg(2, comp3)
    ..writeString(6, mid);

  // Mission Goals
  final goals = PbWriter()
    ..writeMsg(
      1,
      PbWriter()
        ..writeVarint(3, 3)
        ..writeVarint(4, 1)
        ..writeMsg(8, PbWriter()..writeVarint(1, 3430))
        ..writeMsg(
          9,
          PbWriter()
            ..writeString(2, '${targetsList.length}')
            ..writeVarint(3, targetsList.length),
        )
        ..writeVarint(10, 3),
    )
    ..writeVarint(2, 1);

  // Mission Token
  final token = PbWriter()
    ..writeString(1, 'offline-mission')
    ..writeString(2, mid)
    ..writeString(4, 'acid-offline-assassin')
    ..writeString(6, 'hireling-1')
    ..writeMsg(10, PbWriter()..writeMsg(2, rootNode))
    ..writeMsg(11, goals);

  // Response (Field 200 = openMissionResponse)
  final openResp = PbWriter()..writeMsg(1, token);
  final resp = PbWriter()..writeMsg(200, openResp);

  return resp.toBytes();
}

const List<MissionMeta> kAllMissions = [
  // Montreal VR Training
  MissionMeta(
    id: 'story_tutorial_01',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Movement & Eagle Vision',
    location: 'Monteriggioni - Daytime',
    objective: 'Reach the Animus waypoint and calibrate sync.',
  ),
  MissionMeta(
    id: 'story_tutorial_02',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Basic Combat',
    location: 'Monteriggioni - Daytime',
    objective: 'Defeat the holographic combat dummies.',
  ),
  MissionMeta(
    id: 'story_tutorial_03',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Silent Assassination',
    location: 'Monteriggioni - Daytime',
    objective: 'Approach silently and eliminate high-profile target.',
  ),
  MissionMeta(
    id: 'story_tutorial_04',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Leap of Faith',
    location: 'Monteriggioni - Daytime',
    objective: 'Scale the tower and execute a Leap of Faith.',
  ),
  MissionMeta(
    id: 'story_tutorial_05',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Stealth Maneuvers',
    location: 'Monteriggioni - Daytime',
    objective: 'Bypass patrolling guards without detection.',
  ),
  MissionMeta(
    id: 'story_tutorial_06',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Social Stealth & Blending',
    location: 'Monteriggioni - Daytime',
    objective: 'Blend into crowds and track suspicious persons.',
  ),
  MissionMeta(
    id: 'story_tutorial_07',
    campaign: 'Montreal - VR Training',
    title: 'VR Training: Field Graduation Exam',
    location: 'Monteriggioni - Daytime',
    objective: 'Complete all objectives within the simulation window.',
  ),

  // Italy - A Murder of Crows
  MissionMeta(
    id: 'story_crows_01',
    campaign: 'Italy - A Murder of Crows',
    title: 'The Corrupted',
    location: 'Monteriggioni - Daytime',
    objective: 'Assassinate the Corrupted Officer in the villa grounds.',
  ),
  MissionMeta(
    id: 'story_crows_02',
    campaign: 'Italy - A Murder of Crows',
    title: "A Healer's Blood",
    location: 'Monteriggioni - Nighttime',
    objective: 'Eliminate the tainted doctor before poison is spread.',
  ),
  MissionMeta(
    id: 'story_crows_03',
    campaign: 'Italy - A Murder of Crows',
    title: 'Vengeful Eyes',
    location: 'Monteriggioni - Foggy',
    objective: 'Hunt down Borgia scouts spying on the villa perimeter.',
  ),
  MissionMeta(
    id: 'story_crows_04',
    campaign: 'Italy - A Murder of Crows',
    title: 'A Smuggler’s Request',
    location: 'Firenze Palazzo - Daytime',
    objective:
        'Eliminate merchant guards and secure confiscated supply manifests.',
  ),
  MissionMeta(
    id: 'story_crows_05',
    campaign: 'Italy - A Murder of Crows',
    title: 'Dark Cargo',
    location: 'Firenze Santa Croce - Dusk',
    objective: 'Ambush the Borgia weapons transport convoy at Santa Croce.',
  ),
  MissionMeta(
    id: 'story_crows_06',
    campaign: 'Italy - A Murder of Crows',
    title: 'A Traitor in Florence',
    location: 'Firenze Palazzo - Nighttime',
    objective: 'Unmask and execute the traitorous liaison in Florence.',
  ),
  MissionMeta(
    id: 'story_crows_07',
    campaign: 'Italy - A Murder of Crows',
    title: 'In Plain Sight',
    location: 'Firenze Santa Croce - Sunny',
    objective:
        'Infiltrate the crowded market and dispatch the corrupt magistrate.',
  ),
  MissionMeta(
    id: 'story_crows_08',
    campaign: 'Italy - A Murder of Crows',
    title: 'Blood on the Tiber',
    location: 'Roma Colosseo - Dusk',
    objective:
        'Track Borgia captains through the Roman ruins and silence them.',
  ),
  MissionMeta(
    id: 'story_crows_09',
    campaign: 'Italy - A Murder of Crows',
    title: 'The Shadow of Rome',
    location: 'Roma Colosseo - Nighttime',
    objective:
        'Infiltrate the Borgia encampment and assassinate their inner counsel.',
  ),
  MissionMeta(
    id: 'story_crows_10',
    campaign: 'Italy - A Murder of Crows',
    title: 'Crows of the Borgia',
    location: 'Roma Colosseo - Storm',
    objective: 'Grand Finale: Assassinate Grand Inquisitor Corvo and shatter the Crows.',
  ),

  // Forlì - A Crimson Sunset
  MissionMeta(
    id: 'story_forli_01',
    campaign: 'Forlì - A Crimson Sunset',
    title: 'An Unexpected Summon',
    location: 'Forlì - Dusk',
    objective: 'Meet with Niccolò Machiavelli and escort him to his spy.',
  ),
  MissionMeta(
    id: 'story_forli_02',
    campaign: 'Forlì - A Crimson Sunset',
    title: 'A Dangerous Alliance',
    location: 'Forlì - Foggy',
    objective:
        'Infiltrate the fortified watchtower and eliminate Borgia commanders.',
  ),
  MissionMeta(
    id: 'story_forli_03',
    campaign: 'Forlì - A Crimson Sunset',
    title: 'Siege of Forlì',
    location: 'Forlì Castle - Siege',
    objective: 'Defend the fortress gates against waves of attackers.',
  ),
  MissionMeta(
    id: 'story_forli_04',
    campaign: 'Forlì - A Crimson Sunset',
    title: 'Escape from Forlì',
    location: 'Forlì Marshes - Night',
    objective: 'Escort Caterina Sforza through enemy lines to safety.',
  ),

  // Bonus / Special
  MissionMeta(
    id: 'story_0015',
    campaign: 'Bonus Contracts',
    title: 'Medici Guard Interception',
    location: 'Firenze - Daytime',
    objective: 'Neutralize Medici deserters in the piazza.',
  ),
  MissionMeta(
    id: 'story_0016',
    campaign: 'Bonus Contracts',
    title: 'The Borgia Courier',
    location: 'Roma - Dusk',
    objective: 'Chase down and tackle the swift Borgia courier.',
  ),
  MissionMeta(
    id: 'story_0017',
    campaign: 'Bonus Contracts',
    title: 'Sanctuary Infiltration',
    location: 'Monteriggioni - Nighttime',
    objective: 'Recover stolen Assassin codex pages from the vault.',
  ),
];

class GameAccessState {
  String mode; // 'fresh', 'unlocked', 'custom'
  Set<String> unlockedMissions;
  Set<String> completedMissions;
  Set<String> unlockedCampaigns;
  String? activeCustomMissionId;
  String activeSlot; // default 'story_0015' (Contracts)
  Map<String, Map<String, dynamic>> customMissions;

  GameAccessState({
    this.mode = 'fresh',
    Set<String>? unlockedMissions,
    Set<String>? completedMissions,
    Set<String>? unlockedCampaigns,
    this.activeCustomMissionId,
    this.activeSlot = 'story_0015',
    Map<String, Map<String, dynamic>>? customMissions,
  }) : unlockedMissions = unlockedMissions ?? {'story_tutorial_01'},
       completedMissions = completedMissions ?? {},
       unlockedCampaigns = unlockedCampaigns ?? {'campaign_1'},
       customMissions = customMissions ?? {};

  bool isUnlocked(String mid) => unlockedMissions.contains(mid);
  bool isCompleted(String mid) => completedMissions.contains(mid);

  void resetAll() {
    mode = 'fresh';
    completedMissions.clear();
    unlockedMissions = {'story_tutorial_01'};
    unlockedCampaigns = {'campaign_1'};
  }

  void unlockAll() {
    mode = 'unlocked';
    for (final m in kAllMissions) {
      unlockedMissions.add(m.id);
      completedMissions.add(m.id);
    }
    for (final cm in customMissions.keys) {
      unlockedMissions.add(cm);
    }
    unlockedCampaigns = {'campaign_1', 'campaign_2', 'campaign_3'};
  }

  Map<String, dynamic> toJson() => {
    'mode': mode,
    'unlockedMissions': unlockedMissions.toList(),
    'completedMissions': completedMissions.toList(),
    'unlockedCampaigns': unlockedCampaigns.toList(),
    'activeCustomMissionId': activeCustomMissionId,
    'activeSlot': activeSlot,
    'customMissions': customMissions,
  };

  factory GameAccessState.fromJson(Map<String, dynamic> json) =>
      GameAccessState(
        mode: json['mode'] as String? ?? 'fresh',
        unlockedMissions:
            (json['unlockedMissions'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {'story_tutorial_01'},
        completedMissions:
            (json['completedMissions'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {},
        unlockedCampaigns:
            (json['unlockedCampaigns'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {'campaign_1'},
        activeCustomMissionId: json['activeCustomMissionId'] as String?,
        activeSlot: json['activeSlot'] as String? ?? 'story_0015',
        customMissions:
            (json['customMissions'] as Map<String, dynamic>?)?.map(
              (k, v) => MapEntry(k, Map<String, dynamic>.from(v as Map)),
            ) ??
            {},
      );
}

class ServerScreen extends StatefulWidget {
  const ServerScreen({super.key});

  @override
  State<ServerScreen> createState() => _ServerScreenState();
}

class _ServerScreenState extends State<ServerScreen>
    with WidgetsBindingObserver {
  static const _gameChannel = MethodChannel('com.acid.server/game');
  HttpServer? _server;
  bool _isRunning = false;
  final List<String> _logs = [];
  Map<String, dynamic> _catalogue = {};
  String _activeMissionId = 'story_tutorial_01';
  GameAccessState _gameState = GameAccessState();

  Map<String, dynamic>? _cacheStatus;
  bool _isInstallingCache = false;
  String _cacheInstallMessage = '';
  double _cacheInstallProgress = 0.0;
  int _cacheFilesCopied = 0;
  int _cacheTotalFiles = 104;

  static const List<String> _candidateCacheDirs = [
    '/sdcard/Android/data/com.acid.server.companion_server/files/cache',
    '/sdcard/ACIdentity_Cache',
    '/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache',
  ];

  List<String> get _allCandidateCacheDirs {
    final list = <String>[];
    final own = _cacheStatus?['ownCachePath'] as String?;
    final target = _cacheStatus?['targetPath'] as String?;
    if (own != null && own.isNotEmpty) list.add(own);
    if (target != null && target.isNotEmpty) list.add(target);
    list.addAll([
      '/sdcard/Android/data/com.acid.server.companion_server/files/cache',
      '/storage/emulated/0/Android/data/com.acid.server.companion_server/files/cache',
      '/sdcard/ACIdentity_Cache',
      '/storage/emulated/0/ACIdentity_Cache',
      '/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache',
      '/storage/emulated/0/Android/data/com.ubisoft.assassinscreed.identity/files/cache',
    ]);
    return list.toSet().toList();
  }

  static const List<String> _candidateCustomDirs = [
    '/sdcard/Android/data/com.acid.server.companion_server/files/custom_missions',
    '/sdcard/ACIdentity/custom_missions',
    '/sdcard/ACIdentity_Custom',
    '/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/custom_missions',
  ];

  static const List<String> _candidateStatePaths = [
    '/sdcard/Android/data/com.acid.server.companion_server/files/game_access_state.json',
    '/sdcard/ACIdentity/game_access_state.json',
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _setupMethodCallHandler();
    _refreshCacheStatus();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _checkAndPromptStoragePermission();
    });
    _loadGameAccessState().then((_) {
      _scanCustomMissionsFromDisk().then((_) {
        _initAndStart();
      });
    });
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _refreshCacheStatus();
    }
  }

  void _setupMethodCallHandler() {
    _gameChannel.setMethodCallHandler((call) async {
      if (call.method == 'onCacheProgress') {
        final args = Map<String, dynamic>.from(call.arguments as Map);
        final status = args['status'] as String? ?? '';
        final count = args['count'] as int? ?? 0;
        final total = args['total'] as int? ?? 104;
        final bytes = (args['bytes'] as num?)?.toInt() ?? 0;
        final fileName = args['fileName'] as String?;
        final error = args['error'] as String?;

        if (mounted) {
          setState(() {
            _cacheFilesCopied = count;
            _cacheTotalFiles = total;
            if (status == 'starting') {
              _isInstallingCache = true;
              _cacheInstallProgress = 0.0;
              _cacheInstallMessage =
                  'Preparing installation to /Android/data/...';
            } else if (status == 'copying') {
              _isInstallingCache = true;
              _cacheInstallProgress = total > 0
                  ? (count / total).clamp(0.0, 1.0)
                  : 0.0;
              final mb = (bytes / (1024 * 1024)).toStringAsFixed(1);
              final shortName = (fileName != null && fileName.length > 12)
                  ? '${fileName.substring(0, 8)}...'
                  : (fileName ?? '');
              _cacheInstallMessage =
                  'Installing file $count of $total: $shortName ($mb MB)';
            } else if (status == 'completed') {
              _isInstallingCache = false;
              _cacheInstallProgress = 1.0;
              final mb = (bytes / (1024 * 1024)).toStringAsFixed(1);
              _cacheInstallMessage =
                  'Cache installed successfully! ($count files, $mb MB)';
              _refreshCacheStatus();
            } else if (status == 'error') {
              _isInstallingCache = false;
              _cacheInstallMessage =
                  'Error: ${error ?? "Unknown copy failure"}';
              _refreshCacheStatus();
            } else if (status == 'idle') {
              _isInstallingCache = false;
            }
          });
        }
      }
    });
  }

  Future<void> _refreshCacheStatus({bool forceScan = false}) async {
    try {
      final status = await _gameChannel.invokeMapMethod<String, dynamic>(
        'getCacheStatus',
        {'forceScan': forceScan},
      );
      if (mounted) {
        setState(() {
          _cacheStatus = status;
        });
      }
    } catch (e) {
      print('Error refreshing cache status: $e');
    }
  }

  Future<void> _checkAndPromptStoragePermission() async {
    try {
      // If cache is already installed, skip storage permission prompt completely
      if (_cacheStatus != null && (_cacheStatus!['installed'] as bool? ?? false)) {
        return;
      }
      final status = await _gameChannel.invokeMapMethod<String, dynamic>(
        'getCacheStatus',
        {'forceScan': false},
      );
      if (status != null && (status['installed'] as bool? ?? false)) {
        return;
      }
      final hasPermission =
          await _gameChannel.invokeMethod<bool>(
            'checkManageStoragePermission',
          ) ??
          false;
      if (!hasPermission && mounted) {
        showDialog(
          context: context,
          barrierDismissible: false,
          builder: (ctx) => AlertDialog(
            backgroundColor: const Color(0xFF141C28),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
              side: const BorderSide(color: Color(0xFF00E5FF), width: 1.5),
            ),
            title: const Row(
              children: [
                Icon(Icons.folder_shared, color: Color(0xFF00E5FF)),
                SizedBox(width: 10),
                Text(
                  'Storage Permission',
                  style: TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ],
            ),
            content: const Text(
              'To scan your Downloads folder and install the 1.5 GB Game Data Cache, AC Identity Server needs All Files Access permission.\n\nPlease tap "Grant Access" and toggle the switch on the next screen.',
              style: TextStyle(
                color: Colors.white70,
                fontSize: 13,
                height: 1.4,
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(ctx).pop(),
                child: const Text(
                  'Later',
                  style: TextStyle(color: Colors.white54),
                ),
              ),
              ElevatedButton(
                onPressed: () {
                  Navigator.of(ctx).pop();
                  _requestManageStoragePermission();
                },
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF00E5FF),
                  foregroundColor: Colors.black,
                ),
                child: const Text(
                  'Grant Access',
                  style: TextStyle(fontWeight: FontWeight.bold),
                ),
              ),
            ],
          ),
        );
      }
    } catch (e) {
      print('Permission check error: $e');
    }
  }

  Future<void> _pickCacheZip() async {
    final hasPerm =
        await _gameChannel.invokeMethod<bool>('checkManageStoragePermission') ??
        false;
    if (!hasPerm) {
      await _requestManageStoragePermission();
      return;
    }
    try {
      setState(() {
        _isInstallingCache = true;
        _cacheInstallMessage = 'Opening ZIP file picker...';
      });
      await _gameChannel.invokeMethod('pickCacheZip');
    } catch (e) {
      if (mounted) {
        setState(() {
          _isInstallingCache = false;
          _cacheInstallMessage = 'Error selecting ZIP: $e';
        });
      }
    }
  }

  Future<void> _pickCacheFolder() async {
    final hasPerm =
        await _gameChannel.invokeMethod<bool>('checkManageStoragePermission') ??
        false;
    if (!hasPerm) {
      await _requestManageStoragePermission();
      return;
    }
    try {
      setState(() {
        _isInstallingCache = true;
        _cacheInstallMessage = 'Opening folder picker...';
      });
      await _gameChannel.invokeMethod('pickCacheFolder');
    } catch (e) {
      if (mounted) {
        setState(() {
          _isInstallingCache = false;
          _cacheInstallMessage = 'Error selecting folder: $e';
        });
      }
    }
  }

  Future<void> _installDetectedCache(String path, bool isZip) async {
    final hasPerm =
        await _gameChannel.invokeMethod<bool>('checkManageStoragePermission') ??
        false;
    if (!hasPerm) {
      await _requestManageStoragePermission();
      return;
    }
    try {
      setState(() {
        _isInstallingCache = true;
        _cacheInstallMessage = 'Installing detected cache...';
      });
      await _gameChannel.invokeMethod('installCacheFromPath', {
        'path': path,
        'isZip': isZip,
      });
    } catch (e) {
      if (mounted) {
        setState(() {
          _isInstallingCache = false;
          _cacheInstallMessage = 'Error installing detected cache: $e';
        });
      }
    }
  }

  Future<void> _requestManageStoragePermission() async {
    try {
      await _gameChannel.invokeMethod('requestManageStoragePermission');
      await Future.delayed(const Duration(seconds: 1));
      await _refreshCacheStatus();
    } catch (e) {
      print('Permission error: $e');
    }
  }

  Future<void> _scanCustomMissionsFromDisk() async {
    try {
      final names = await _gameChannel.invokeListMethod<String>(
        'listCustomMissions',
      );
      if (names == null || names.isEmpty) return;

      bool changed = false;
      for (final name in names) {
        if (name.endsWith('.json')) {
          final content = await _gameChannel.invokeMethod<String>(
            'readCustomMissionFile',
            {'filename': name},
          );
          if (content != null && content.isNotEmpty) {
            try {
              final map = jsonDecode(content) as Map<String, dynamic>;
              final mid =
                  map['missionId'] as String? ??
                  map['id'] as String? ??
                  name.replaceAll('.json', '');
              if (!_gameState.customMissions.containsKey(mid)) {
                _gameState.customMissions[mid] = map;
                _gameState.unlockedMissions.add(mid);
                changed = true;
              }
            } catch (_) {}
          }
        }
      }
      if (_gameState.activeCustomMissionId == null &&
          _gameState.customMissions.isNotEmpty) {
        _gameState.activeCustomMissionId = _gameState.customMissions.keys.first;
        changed = true;
      }
      if (_gameState.activeCustomMissionId != null) {
        final activeMid = _gameState.activeCustomMissionId!;
        final map = _gameState.customMissions[activeMid];
        if (map != null) {
          final bin = buildCustomMissionBinary(map);
          try {
            await _gameChannel.invokeMethod('saveCustomMission', {
              'filename': 'openMission_story_0015.bin',
              'bytes': bin,
            });
            await _gameChannel.invokeMethod('saveCustomMission', {
              'filename': 'openMission_$activeMid.bin',
              'bytes': bin,
            });
          } catch (_) {}
        }
      }
      if (changed) {
        await _saveGameAccessState();
        if (mounted) setState(() {});
      }
    } catch (_) {}
  }

  Future<void> _log(String message) async {
    final time = DateTime.now().toIso8601String().substring(11, 19);
    final entry = '[$time] $message';
    print('[ACID_SERVER] $entry');
    if (mounted) {
      setState(() {
        _logs.insert(0, entry);
        if (_logs.length > 300) _logs.removeLast();
      });
    }
  }

  Future<void> _loadGameAccessState() async {
    try {
      final nativeContent = await _gameChannel.invokeMethod<String>(
        'readGameState',
      );
      if (nativeContent != null && nativeContent.isNotEmpty) {
        final map = jsonDecode(nativeContent) as Map<String, dynamic>;
        _gameState = GameAccessState.fromJson(map);
        if (_gameState.activeCustomMissionId == null &&
            _gameState.customMissions.isNotEmpty) {
          _gameState.activeCustomMissionId =
              _gameState.customMissions.keys.first;
        }
        await _log(
          'Loaded game access profile: ${_gameState.mode} (${_gameState.completedMissions.length} completed)',
        );
        return;
      }
    } catch (_) {}

    for (final p in _candidateStatePaths) {
      try {
        final f = File(p);
        if (await f.exists()) {
          final text = await f.readAsString();
          final map = jsonDecode(text) as Map<String, dynamic>;
          _gameState = GameAccessState.fromJson(map);
          if (_gameState.activeCustomMissionId == null &&
              _gameState.customMissions.isNotEmpty) {
            _gameState.activeCustomMissionId =
                _gameState.customMissions.keys.first;
          }
          await _log(
            'Loaded game access profile: ${_gameState.mode} (${_gameState.completedMissions.length} completed)',
          );
          return;
        }
      } catch (_) {}
    }
    _gameState = GameAccessState();
    await _log(
      'Initialized default story game access state (Tutorial 1 unlocked)',
    );
  }

  Future<void> _saveGameAccessState() async {
    final jsonStr = const JsonEncoder.withIndent('  ')
        .convert(_gameState.toJson());
    try {
      await _gameChannel.invokeMethod('writeGameState', {'content': jsonStr});
    } catch (_) {}
    for (final p in _candidateStatePaths) {
      try {
        final f = File(p);
        if (!await f.parent.exists()) await f.parent.create(recursive: true);
        await f.writeAsString(jsonStr);
      } catch (_) {}
    }
    // Also sync localstorage.json mirror
    final lsData = {
      'version': 7,
      'missionprogress': {
        for (final m in kAllMissions)
          m.id: {
            'WasUnlockShown': _gameState.isUnlocked(m.id),
            'WasCompletedShown': _gameState.isCompleted(m.id),
          },
        for (final cm in _gameState.customMissions.keys)
          cm: {
            'WasUnlockShown': _gameState.isUnlocked(cm),
            'WasCompletedShown': _gameState.isCompleted(cm),
          },
      },
      'entitlement': {
        'ntt_c350c141': true,
        if (_gameState.unlockedCampaigns.contains('campaign_2'))
          'ntt_5572b4db': true,
        if (_gameState.unlockedCampaigns.contains('campaign_3'))
          'ntt_0dd81737': true,
        if (_gameState.completedMissions.contains('story_crows_10')) ...{
          'ntt_bfc7ed30': true,
          'ntt_offline_rank1': true,
        },
        for (final mid in _gameState.completedMissions) ...{
          'ntt_story_reward_$mid': true,
          'ntt_story_challenge_$mid': true,
        },
      },
    };
    try {
      final lsStr = const JsonEncoder.withIndent('  ').convert(lsData);
      await _gameChannel.invokeMethod('writeLocalStorage', {'content': lsStr});
    } catch (_) {}
  }

  Uint8List _filterAuthBytes(
    Uint8List data,
    Set<String> completedMissions,
    Set<String> unlockedCampaigns,
  ) {
    int f4Tag = -1;
    for (int k = 0; k < data.length; k++) {
      if (data[k] == 0x22) {
        f4Tag = k;
        break;
      }
    }
    if (f4Tag == -1) return data;
    final f4Prefix = data.sublist(0, f4Tag + 1);
    int k = f4Tag + 1;
    int f4Len = 0;
    int shift4 = 0;
    while (k < data.length) {
      final b = data[k++];
      f4Len |= (b & 0x7F) << shift4;
      if ((b & 0x80) == 0) break;
      shift4 += 7;
    }
    final profileStart = k;
    final profileEnd = k + f4Len;
    if (profileEnd > data.length) return data;

    int tagPos = -1;
    for (int idx = profileStart; idx < profileEnd - 1; idx++) {
      if (data[idx] == 0x82 && data[idx + 1] == 0x02) {
        tagPos = idx;
        break;
      }
    }
    if (tagPos == -1) return data;

    int i = tagPos + 2;
    int f32Len = 0;
    int shift = 0;
    while (i < data.length) {
      final b = data[i++];
      f32Len |= (b & 0x7F) << shift;
      if ((b & 0x80) == 0) break;
      shift += 7;
    }
    final f32End = i + f32Len;
    if (f32End > profileEnd) return data;

    final f32Data = data.sublist(i, f32End);
    final profileBeforeF32 = data.sublist(profileStart, tagPos);
    final profileAfterF32 = data.sublist(f32End, profileEnd);
    final postProfile = data.sublist(profileEnd);

    int j = 0;
    final keptEntries = <List<int>>[];
    while (j < f32Data.length) {
      if (f32Data[j] != 0x0a) break;
      j++;
      int entryLen = 0;
      int s = 0;
      while (j < f32Data.length) {
        final b = f32Data[j++];
        entryLen |= (b & 0x7F) << s;
        if ((b & 0x80) == 0) break;
        s += 7;
      }
      if (j + entryLen > f32Data.length) break;
      final entryBytes = f32Data.sublist(j, j + entryLen);
      j += entryLen;

      bool keep = true;
      if (entryBytes.length > 2 && entryBytes[0] == 0x0a) {
        final glen = entryBytes[1];
        if (entryBytes.length >= 2 + glen) {
          final guid = ascii.decode(entryBytes.sublist(2, 2 + glen));
          if (guid.startsWith('ntt_story_reward_')) {
            final mid = guid.substring('ntt_story_reward_'.length);
            if (!completedMissions.contains(mid)) keep = false;
          } else if (guid.startsWith('ntt_story_challenge_')) {
            final mid = guid.substring('ntt_story_challenge_'.length);
            if (!completedMissions.contains(mid)) keep = false;
          } else if (guid == 'ntt_5572b4db') {
            if (!unlockedCampaigns.contains('campaign_2')) keep = false;
          } else if (guid == 'ntt_0dd81737') {
            if (!unlockedCampaigns.contains('campaign_3')) keep = false;
          } else if (guid == 'ntt_bfc7ed30' || guid == 'ntt_offline_rank1') {
            if (!completedMissions.contains('story_crows_10')) keep = false;
          }
        }
      }

      if (keep) {
        final el = entryBytes.length;
        final elVar = <int>[];
        int tmp = el;
        while (true) {
          final tw = tmp & 0x7F;
          tmp >>= 7;
          if (tmp > 0) {
            elVar.add(tw | 0x80);
          } else {
            elVar.add(tw);
            break;
          }
        }
        keptEntries.add([0x0a, ...elVar, ...entryBytes]);
      }
    }

    final newF32Content = <int>[];
    for (final e in keptEntries) {
      newF32Content.addAll(e);
    }

    final f32LenVar = <int>[];
    int tmpL = newF32Content.length;
    while (true) {
      final tw = tmpL & 0x7F;
      tmpL >>= 7;
      if (tmpL > 0) {
        f32LenVar.add(tw | 0x80);
      } else {
        f32LenVar.add(tw);
        break;
      }
    }
    final newF32 = <int>[0x82, 0x02, ...f32LenVar, ...newF32Content];
    final newProfile = <int>[
      ...profileBeforeF32,
      ...newF32,
      ...profileAfterF32,
    ];

    final plVar = <int>[];
    int tmpPl = newProfile.length;
    while (true) {
      final tw = tmpPl & 0x7F;
      tmpPl >>= 7;
      if (tmpPl > 0) {
        plVar.add(tw | 0x80);
      } else {
        plVar.add(tw);
        break;
      }
    }

    return Uint8List.fromList([
      ...f4Prefix,
      ...plVar,
      ...newProfile,
      ...postProfile,
    ]);
  }

  Future<Uint8List?> _getActiveAuthResponse() async {
    final unlockedBytes =
        await _loadAsset('assets/responses/auth_unlocked.bin') ??
        await _loadAsset('assets/responses/auth.bin');
    if (unlockedBytes != null) {
      return _ensureDualAuth(unlockedBytes);
    }
    final freshBytes = await _loadAsset('assets/responses/auth_fresh.bin');
    if (freshBytes != null) {
      return _ensureDualAuth(freshBytes);
    }
    return null;
  }

  Uint8List _ensureDualAuth(Uint8List data) {
    var out = data;
    if (!_containsSequence(out, [0xA2, 0x06])) {
      out = Uint8List.fromList([
        ...out,
        0xA2,
        0x06,
        0x0A,
        0x0A,
        0x08,
        0x61,
        0x73,
        0x73,
        0x61,
        0x73,
        0x73,
        0x69,
        0x6E,
      ]);
    }
    if (!_containsSequence(out, [0xB2, 0x06])) {
      out = Uint8List.fromList([
        ...out,
        0xB2,
        0x06,
        0x0A,
        0x0A,
        0x08,
        0x61,
        0x73,
        0x73,
        0x61,
        0x73,
        0x73,
        0x69,
        0x6E,
      ]);
    }
    return out;
  }

  Future<void> _initAndStart() async {
    try {
      final catBytes = await _loadAsset('assets/responses/catalogue.json');
      if (catBytes != null) {
        final catJson = utf8.decode(catBytes);
        _catalogue = jsonDecode(catJson) as Map<String, dynamic>;
        await _log(
          'Catalogue loaded: ${_catalogue.length} CDN entries indexed',
        );
      }
    } catch (e) {
      await _log('Warning: could not load catalogue.json: $e');
    }

    await _startServer();
  }

  Future<Uint8List?> _loadAsset(String path) async {
    try {
      final bd = await rootBundle.load(path);
      return bd.buffer.asUint8List();
    } catch (_) {
      return null;
    }
  }

  Future<void> _startServer() async {
    if (_isRunning) return;
    try {
      _server = await HttpServer.bind(InternetAddress.anyIPv4, 8080);
      setState(() => _isRunning = true);
      await _log('Offline server listening on http://127.0.0.1:8080');

      try {
        await _gameChannel.invokeMethod('startForegroundService');
      } catch (_) {}

      _server!.listen(
        _handleHttpRequest,
        onError: (e) => _log('Server error: $e'),
        onDone: () => setState(() => _isRunning = false),
      );
    } catch (e) {
      await _log('Failed to start server: $e');
    }
  }

  Future<void> _stopServer() async {
    if (!_isRunning && _server == null) {
      try {
        await _gameChannel.invokeMethod('stopForegroundService');
      } catch (_) {}
      return;
    }
    if (_server != null) {
      await _server!.close(force: true);
      _server = null;
    }
    setState(() => _isRunning = false);
    try {
      await _gameChannel.invokeMethod('stopForegroundService');
    } catch (_) {}
    await _log('Server stopped & foreground service terminated');
  }

  Future<Uint8List?> _getCustomMissionBinary(String mid) async {
    for (final dir in _candidateCustomDirs) {
      final f = File('$dir/openMission_$mid.bin');
      if (await f.exists()) {
        try {
          return await f.readAsBytes();
        } catch (_) {}
      }
    }
    for (final dir in _candidateCustomDirs) {
      final f = File('$dir/$mid.json');
      if (await f.exists()) {
        try {
          final text = await f.readAsString();
          final map = jsonDecode(text) as Map<String, dynamic>;
          final bin = buildCustomMissionBinary(map);
          await f.parent.create(recursive: true);
          await File('$dir/openMission_$mid.bin').writeAsBytes(bin);
          return bin;
        } catch (_) {}
      }
    }
    if (_gameState.customMissions.containsKey(mid)) {
      try {
        final map = _gameState.customMissions[mid]!;
        final bin = buildCustomMissionBinary(map);
        return bin;
      } catch (_) {}
    }
    return null;
  }

  Future<void> _handleHttpRequest(HttpRequest request) async {
    final path = request.uri.path;
    final method = request.method;

    try {
      if (path.startsWith('/cdn/')) {
        const cdnPrefix = '/cdn/ACID_2.8.2_373/';
        final relative = path.startsWith(cdnPrefix)
            ? path.substring(cdnPrefix.length)
            : path.substring('/cdn/'.length);
        await _serveCdnFile(request, relative);
        return;
      }

      final bodyBytes = await request.fold<List<int>>(
        [],
        (prev, elem) => prev..addAll(elem),
      );
      final bodyStr = utf8.decode(bodyBytes, allowMalformed: true);

      String? respAsset;
      Uint8List? directBytes;
      String reqType = 'Unknown';

      final allMissionIds = kAllMissions.map((m) => m.id).toList();

      // Check request payloads by opcode first:
      if (_containsSequence(bodyBytes, [0xE2, 0x2B])) {
        reqType = 'CheckVersion';
        respAsset = 'assets/responses/checkVersion.bin';
      } else if (_containsSequence(bodyBytes, [0xDA, 0x25])) {
        reqType = 'GetStatics';
        respAsset = 'assets/responses/statics.bin';
      } else if (_containsSequence(bodyBytes, [0xF2, 0x06])) {
        reqType = 'GetQualitySettings';
        respAsset = 'assets/responses/quality.bin';
      } else if (_containsSequence(bodyBytes, [0xBA, 0x09])) {
        reqType = 'GetTwitterTimeline';
        respAsset = 'assets/responses/twitter.bin';
      } else if (_containsSequence(bodyBytes, [0xCA, 0x0C])) {
        final mid = _activeMissionId;
        reqType = 'EndMission($mid)';
        respAsset = 'assets/responses/endMission_$mid.bin';

        // Story progression handling:
        if (mid != 'default') {
          _gameState.completedMissions.add(mid);
          if (_gameState.activeCustomMissionId != null) {
            _gameState.completedMissions.add(_gameState.activeCustomMissionId!);
          }
          final idx = kAllMissions.indexWhere((m) => m.id == mid);
          if (idx >= 0 && idx + 1 < kAllMissions.length) {
            final nextMid = kAllMissions[idx + 1].id;
            _gameState.unlockedMissions.add(nextMid);
            if (mid == 'story_tutorial_07') {
              _gameState.unlockedCampaigns.add('campaign_2');
            } else if (mid == 'story_crows_10') {
              _gameState.unlockedCampaigns.add('campaign_3');
            }
          }
          await _saveGameAccessState();
          await _log('Mission $mid completed! Next mission unlocked.');
        }
      } else if (_containsSequence(bodyBytes, [0xD2, 0x0C])) {
        String mid = 'default';
        for (final m in allMissionIds) {
          if (bodyStr.contains(m)) {
            mid = m;
            break;
          }
        }
        for (final cm in _gameState.customMissions.keys) {
          if (bodyStr.contains(cm)) {
            mid = cm;
            break;
          }
        }
        _activeMissionId = mid;
        reqType = 'GetMissionInfo($mid)';

        // Check if active custom mission has custom info
        final activeCustomId =
            _gameState.activeCustomMissionId ??
            (_gameState.customMissions.isNotEmpty
                ? _gameState.customMissions.keys.first
                : null);
        final targetSlot = _gameState.activeSlot;
        if (activeCustomId != null &&
            (mid == targetSlot ||
                targetSlot == 'all' ||
                targetSlot == 'both' ||
                mid == 'default' ||
                mid == 'story_0015' ||
                mid == 'story_crows_01' ||
                mid == activeCustomId)) {
          for (final dir in _candidateCustomDirs) {
            final f = File('$dir/getMissionInfo_$activeCustomId.bin');
            if (await f.exists()) {
              try {
                final b = await f.readAsBytes();
                request.response
                  ..statusCode = HttpStatus.ok
                  ..headers.contentType = ContentType(
                    'application',
                    'x-protobuf',
                  )
                  ..headers.contentLength = b.length
                  ..add(b);
                await request.response.close();
                await _log(
                  '$method GetMissionInfo($mid) -> Served Custom Info (${b.length}b)',
                );
                return;
              } catch (_) {}
            }
          }
        }

        respAsset = 'assets/responses/getMissionInfo_$mid.bin';
      } else if (_containsSequence(bodyBytes, [0xC2, 0x0C])) {
        String mid = 'default';
        for (final m in allMissionIds) {
          if (bodyStr.contains(m)) {
            mid = m;
            break;
          }
        }
        for (final cm in _gameState.customMissions.keys) {
          if (bodyStr.contains(cm)) {
            mid = cm;
            break;
          }
        }
        _activeMissionId = mid;
        reqType = 'OpenMission($mid)';

        // 1. Check if an active custom mission overrides this slot or any mission
        final activeCustomId =
            _gameState.activeCustomMissionId ??
            (_gameState.customMissions.isNotEmpty
                ? _gameState.customMissions.keys.first
                : null);
        final targetSlot = _gameState.activeSlot;

        if (activeCustomId != null &&
            (mid == targetSlot ||
                targetSlot == 'all' ||
                targetSlot == 'both' ||
                mid == 'default' ||
                mid == 'story_0015' ||
                mid == 'story_crows_01' ||
                mid == activeCustomId)) {
          final customBytes = await _getCustomMissionBinary(activeCustomId);
          if (customBytes != null) {
            request.response
              ..statusCode = HttpStatus.ok
              ..headers.contentType = ContentType('application', 'x-protobuf')
              ..headers.contentLength = customBytes.length
              ..add(customBytes);
            await request.response.close();
            await _log(
              '$method OpenMission($mid) -> Served Active Custom Mission "$activeCustomId" (${customBytes.length}b)',
            );
            return;
          }
        }

        // 2. Check if a custom binary exists for the requested mission ID directly
        final customBytes = await _getCustomMissionBinary(mid);
        if (customBytes != null) {
          request.response
            ..statusCode = HttpStatus.ok
            ..headers.contentType = ContentType('application', 'x-protobuf')
            ..headers.contentLength = customBytes.length
            ..add(customBytes);
          await request.response.close();
          await _log(
            '$method OpenMission($mid) -> Served Custom File (${customBytes.length}b)',
          );
          return;
        }

        respAsset = 'assets/responses/openMission_$mid.bin';
      } else if (_containsSequence(bodyBytes, [0xB2, 0x06]) ||
          _containsSequence(bodyBytes, [0xA2, 0x06]) ||
          bodyStr.contains('assassin') ||
          bodyStr.contains('auth')) {
        final isRegister = _containsSequence(bodyBytes, [0xA2, 0x06]);
        reqType = isRegister ? 'RegisterUser' : 'AuthAccount';
        directBytes = await _getActiveAuthResponse();
      } else if (_containsSequence(bodyBytes, [0xB2, 0x09])) {
        reqType = 'Discovery';
        respAsset = 'assets/responses/discovery.bin';
      } else if (path.contains('/discovery')) {
        reqType = 'Discovery';
        respAsset = 'assets/responses/discovery.bin';
      } else {
        reqType = 'DefaultAuth';
        directBytes = await _getActiveAuthResponse();
      }

      Uint8List? respBytes = directBytes;
      if (respBytes == null && respAsset != null) {
        respBytes = await _loadAsset(respAsset);
        if (respBytes == null) {
          if (reqType.startsWith('OpenMission')) {
            respBytes = await _loadAsset(
              'assets/responses/openMission_default.bin',
            );
          } else if (reqType.startsWith('GetMissionInfo')) {
            respBytes = await _loadAsset(
              'assets/responses/getMissionInfo_default.bin',
            );
          } else if (reqType.startsWith('EndMission')) {
            respBytes = await _loadAsset(
              'assets/responses/endMission_default.bin',
            );
          }
        }
        respBytes ??= await _loadAsset('assets/responses/auth.bin');
      }

      if (respBytes != null) {
        request.response
          ..statusCode = HttpStatus.ok
          ..headers.contentType = ContentType('application', 'x-protobuf')
          ..headers.contentLength = respBytes.length
          ..add(respBytes);
        await request.response.close();
        await _log('$method $reqType (${respBytes.length}b)');
        return;
      }

      request.response
        ..statusCode = HttpStatus.notFound
        ..write('Not Found');
      await request.response.close();
      await _log('404 Not Found: $path');
    } catch (e, stack) {
      print('[ACID_SERVER_ERROR] $e\n$stack');
      await _log('Request error: $e');
      try {
        request.response.statusCode = HttpStatus.internalServerError;
        request.response.write('Internal Error: $e');
        await request.response.close();
      } catch (_) {}
    }
  }

  Future<void> _serveCdnFile(HttpRequest request, String relative) async {
    var entry = _catalogue[relative];
    if (entry == null) {
      final fallback = relative
          .replaceAll('AndroidDXT', 'AndroidETC2')
          .replaceAll('AndroidGeneric', 'AndroidETC2');
      entry = _catalogue[fallback];
    }

    if (entry == null) {
      request.response
        ..statusCode = HttpStatus.notFound
        ..write('CDN asset not found in catalogue');
      await request.response.close();
      await _log('CDN 404: $relative');
      return;
    }

    final fileId =
        entry['id'] as String? ?? entry['relative_path'] as String? ?? relative;
    final zipped = entry['zipped'] as bool? ?? false;

    File? foundFile;
    for (final dir in _allCandidateCacheDirs) {
      final f = File('$dir/$fileId');
      if (await f.exists()) {
        foundFile = f;
        break;
      }
      final fNamed = File('$dir/$relative');
      if (await fNamed.exists()) {
        foundFile = fNamed;
        break;
      }
    }

    if (foundFile == null) {
      request.response
        ..statusCode = HttpStatus.notFound
        ..write('Asset file missing on device storage');
      await request.response.close();
      await _log('CDN File Missing: $relative (id: $fileId)');
      return;
    }

    var bytes = await foundFile.readAsBytes();
    if (zipped) {
      bytes = Uint8List.fromList(zlib.encode(bytes));
    }

    request.response
      ..statusCode = HttpStatus.ok
      ..headers.contentType = ContentType('application', 'octet-stream')
      ..headers.set(HttpHeaders.acceptRangesHeader, 'bytes')
      ..headers.contentLength = bytes.length
      ..add(bytes);
    await request.response.close();
    await _log(
      'CDN 200: $relative (${bytes.length}b${zipped ? ", zlib" : ""})',
    );
  }

  bool _containsSequence(List<int> source, List<int> seq) {
    if (source.length < seq.length) return false;
    for (int i = 0; i <= source.length - seq.length; i++) {
      bool match = true;
      for (int j = 0; j < seq.length; j++) {
        if (source[i + j] != seq[j]) {
          match = false;
          break;
        }
      }
      if (match) return true;
    }
    return false;
  }

  Future<void> _launchGame() async {
    try {
      final isInstalled =
          await _gameChannel.invokeMethod<bool>('isGameInstalled') ?? false;
      if (!isInstalled) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Assassin\'s Creed Identity is not installed!'),
              backgroundColor: Colors.red,
            ),
          );
        }
        return;
      }

      await _log('Launching Assassin\'s Creed Identity...');
      final launched =
          await _gameChannel.invokeMethod<bool>('openGame') ?? false;
      if (!launched) {
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Could not launch game directly.'),
              backgroundColor: Colors.orange,
            ),
          );
        }
      }
    } catch (e) {
      await _log('Launch game error: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Error launching game: $e'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Widget _buildCacheInstallerCard() {
    final status = _cacheStatus;
    final fileCount = (status?['fileCount'] as num?)?.toInt() ?? 0;
    final isInstalled = status?['installed'] as bool? ?? false;
    final hasManageStorage = status?['hasManageStorage'] as bool? ?? true;
    final autoDetected = status?['autoDetected'] as Map?;
    final targetPath =
        status?['targetPath'] as String? ??
        '/Android/data/com.ubisoft.assassinscreed.identity/files/cache';

    Color cardBorder;
    Color accentColor;
    IconData statusIcon;
    String statusTitle;
    String statusSubtitle;

    if (_isInstallingCache) {
      cardBorder = const Color(0xFF00E5FF);
      accentColor = const Color(0xFF00E5FF);
      statusIcon = Icons.sync;
      statusTitle = 'INSTALLING GAME DATA CACHE...';
      statusSubtitle = _cacheInstallMessage;
    } else if (isInstalled) {
      cardBorder = const Color(0xFF00E676);
      accentColor = const Color(0xFF00E676);
      statusIcon = Icons.check_circle;
      statusTitle = 'GAME CACHE: READY ($fileCount/104 FILES)';
      statusSubtitle =
          'All asset bundles installed. 100% offline gameplay ready.';
    } else if (fileCount > 0) {
      cardBorder = const Color(0xFFFFB300);
      accentColor = const Color(0xFFFFB300);
      statusIcon = Icons.warning_amber_rounded;
      statusTitle = 'GAME CACHE: INCOMPLETE ($fileCount/104 FILES)';
      statusSubtitle =
          'Some asset files are missing. Select ZIP or folder to complete.';
    } else {
      cardBorder = const Color(0xFFFF5252);
      accentColor = const Color(0xFFFF5252);
      statusIcon = Icons.folder_zip_rounded;
      statusTitle = 'GAME CACHE: NOT INSTALLED (0/104 FILES)';
      statusSubtitle =
          'Select GameData_Cache ZIP or folder to copy data to game folder.';
    }

    return Container(
      margin: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 6.0),
      padding: const EdgeInsets.all(12.0),
      decoration: BoxDecoration(
        color: const Color(0xFF141C28),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(
          color: cardBorder.withValues(alpha: 0.6),
          width: 1.5,
        ),
        boxShadow: [
          BoxShadow(
            color: accentColor.withValues(alpha: 0.1),
            blurRadius: 8,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Header Row
          Row(
            children: [
              Icon(statusIcon, color: accentColor, size: 24),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      statusTitle,
                      style: TextStyle(
                        color: accentColor,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                        letterSpacing: 0.8,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      statusSubtitle,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        color: Colors.white.withValues(alpha: 0.75),
                        fontSize: 11,
                      ),
                    ),
                  ],
                ),
              ),
              IconButton(
                icon: const Icon(
                  Icons.refresh,
                  color: Colors.white54,
                  size: 18,
                ),
                tooltip: 'Refresh Cache Status',
                onPressed: () => _refreshCacheStatus(forceScan: true),
              ),
            ],
          ),

          // Progress indicator during install
          if (_isInstallingCache) ...[
            const SizedBox(height: 10),
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: _cacheInstallProgress > 0 ? _cacheInstallProgress : null,
                minHeight: 6,
                backgroundColor: const Color(0xFF101722),
                valueColor: const AlwaysStoppedAnimation<Color>(
                  Color(0xFF00E5FF),
                ),
              ),
            ),
            const SizedBox(height: 6),
            Text(
              'Target: $targetPath',
              style: const TextStyle(fontSize: 10, color: Colors.white38),
            ),
          ],

          // Installer buttons & scan banners only when cache is NOT installed
          if (!_isInstallingCache && !isInstalled) ...[
            const SizedBox(height: 10),
            // Auto-detected cache banner (if found on storage)
            if (autoDetected != null) ...[
              Container(
                margin: const EdgeInsets.only(bottom: 8),
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 8,
                ),
                decoration: BoxDecoration(
                  color: const Color(0xFF0D251C),
                  borderRadius: BorderRadius.circular(6),
                  border: Border.all(
                    color: const Color(0xFF00E676).withValues(alpha: 0.4),
                  ),
                ),
                child: Row(
                  children: [
                    const Icon(
                      Icons.auto_awesome,
                      color: Color(0xFF00E676),
                      size: 18,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Found: ${autoDetected['isZip'] == true ? 'Cache ZIP' : 'Cache Folder'}',
                            style: const TextStyle(
                              color: Color(0xFF00E676),
                              fontWeight: FontWeight.bold,
                              fontSize: 11,
                            ),
                          ),
                          Text(
                            autoDetected['path'] as String? ?? '',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Colors.white60,
                              fontSize: 10,
                            ),
                          ),
                        ],
                      ),
                    ),
                    ElevatedButton(
                      onPressed: () => _installDetectedCache(
                        autoDetected['path'] as String,
                        autoDetected['isZip'] as bool? ?? false,
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF00E676),
                        foregroundColor: Colors.black,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 6,
                        ),
                        minimumSize: Size.zero,
                        tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                      ),
                      child: const Text(
                        '1-Tap Install',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ],

            // Selection Buttons (Select ZIP or Select Folder)
            Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _pickCacheZip,
                    icon: const Icon(Icons.folder_zip, size: 16),
                    label: const Text(
                      'Select ZIP',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1E2D42),
                      foregroundColor: const Color(0xFF00E5FF),
                      side: const BorderSide(
                        color: Color(0xFF00E5FF),
                        width: 1,
                      ),
                      padding: const EdgeInsets.symmetric(vertical: 8),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(6),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _pickCacheFolder,
                    icon: const Icon(Icons.folder_open, size: 16),
                    label: const Text(
                      'Select Folder',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1E2D42),
                      foregroundColor: const Color(0xFFFFB300),
                      side: const BorderSide(
                        color: Color(0xFFFFB300),
                        width: 1,
                      ),
                      padding: const EdgeInsets.symmetric(vertical: 8),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(6),
                      ),
                    ),
                  ),
                ),
              ],
            ),

            // Permission alert if manage storage is missing on Android 11+
            if (!hasManageStorage) ...[
              const SizedBox(height: 8),
              InkWell(
                onTap: _requestManageStoragePermission,
                child: Container(
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  decoration: BoxDecoration(
                    color: const Color(0xFF2E1C0A),
                    borderRadius: BorderRadius.circular(4),
                    border: Border.all(
                      color: const Color(0xFFFF9100).withValues(alpha: 0.5),
                    ),
                  ),
                  child: const Row(
                    children: [
                      Icon(Icons.lock_open, color: Color(0xFFFF9100), size: 14),
                      SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          'Tap to grant All Files Access to write to Android/data',
                          style: TextStyle(
                            color: Color(0xFFFF9100),
                            fontSize: 10,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ] else if (!_isInstallingCache && isInstalled) ...[
            // When already installed, show a discreet reinstall option if ever needed
            const SizedBox(height: 4),
            Row(
              mainAxisAlignment: MainAxisAlignment.end,
              children: [
                TextButton.icon(
                  onPressed: _showReinstallCacheDialog,
                  icon: const Icon(
                    Icons.settings_backup_restore,
                    size: 13,
                    color: Colors.white38,
                  ),
                  label: const Text(
                    'Reinstall Cache...',
                    style: TextStyle(fontSize: 10, color: Colors.white38),
                  ),
                  style: TextButton.styleFrom(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    minimumSize: Size.zero,
                    tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  void _showReinstallCacheDialog() {
    showModalBottomSheet(
      context: context,
      backgroundColor: const Color(0xFF141C28),
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => Padding(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Game Cache Options',
              style: TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.bold,
                fontSize: 16,
              ),
            ),
            const SizedBox(height: 8),
            const Text(
              'Game Cache is currently installed (104 files). You can reinstall or overwrite using a ZIP file or folder.',
              style: TextStyle(color: Colors.white70, fontSize: 12),
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: () {
                      Navigator.pop(ctx);
                      _pickCacheZip();
                    },
                    icon: const Icon(Icons.folder_zip, size: 16),
                    label: const Text('Select ZIP'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1E2D42),
                      foregroundColor: const Color(0xFF00E5FF),
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: () {
                      Navigator.pop(ctx);
                      _pickCacheFolder();
                    },
                    icon: const Icon(Icons.folder_open, size: 16),
                    label: const Text('Select Folder'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF1E2D42),
                      foregroundColor: const Color(0xFFFFB300),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Center(
              child: TextButton.icon(
                onPressed: () {
                  Navigator.pop(ctx);
                  _refreshCacheStatus(forceScan: true);
                },
                icon: const Icon(Icons.search, size: 16, color: Color(0xFF00E676)),
                label: const Text(
                  'Scan Storage for Cache Source',
                  style: TextStyle(color: Color(0xFF00E676), fontSize: 12),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _openGameAccessManager() {
    Navigator.of(context).push(
      MaterialPageRoute(
        builder: (ctx) => GameAccessScreen(
          gameState: _gameState,
          onLog: _log,
          gameChannel: _gameChannel,
          onStateChanged: (newState) async {
            setState(() => _gameState = newState);
            await _saveGameAccessState();
          },
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final completedCount = _gameState.completedMissions.length;
    final totalCount = kAllMissions.length;

    return Scaffold(
      appBar: AppBar(
        title: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.all(4),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(color: const Color(0xFF00E5FF), width: 1.5),
              ),
              child: const Icon(Icons.hub, color: Color(0xFF00E5FF), size: 20),
            ),
            const SizedBox(width: 10),
            const Text(
              'AC Identity Companion',
              style: TextStyle(fontWeight: FontWeight.bold, letterSpacing: 0.5),
            ),
          ],
        ),
        centerTitle: true,
        backgroundColor: const Color(0xFF101722),
      ),
      body: Column(
        children: [
          // 80% Server Status + 20% Toggle Switch
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              color: _isRunning
                  ? const Color(0xFF0D251C)
                  : const Color(0xFF281313),
              border: Border(
                bottom: BorderSide(
                  color: _isRunning
                      ? const Color(0xFF00E676)
                      : const Color(0xFFFF5252),
                  width: 1.5,
                ),
              ),
            ),
            child: Row(
              children: [
                Expanded(
                  flex: 8,
                  child: Row(
                    children: [
                      Container(
                        width: 12,
                        height: 12,
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          color: _isRunning
                              ? const Color(0xFF00E676)
                              : const Color(0xFFFF5252),
                          boxShadow: [
                            BoxShadow(
                              color:
                                  (_isRunning
                                          ? const Color(0xFF00E676)
                                          : const Color(0xFFFF5252))
                                      .withValues(alpha: 0.5),
                              blurRadius: 8,
                              spreadRadius: 2,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 12),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _isRunning ? 'SERVER ONLINE' : 'SERVER STOPPED',
                            style: TextStyle(
                              color: _isRunning
                                  ? const Color(0xFF00E676)
                                  : const Color(0xFFFF5252),
                              fontWeight: FontWeight.bold,
                              fontSize: 14,
                              letterSpacing: 1.2,
                            ),
                          ),
                          Text(
                            _isRunning
                                ? 'http://127.0.0.1:8080 (Active)'
                                : 'Tap switch to start',
                            style: TextStyle(
                              color: Colors.white.withValues(alpha: 0.6),
                              fontSize: 12,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                Expanded(
                  flex: 2,
                  child: Align(
                    alignment: Alignment.centerRight,
                    child: Switch(
                      value: _isRunning,
                      activeThumbColor: const Color(0xFF00E676),
                      activeTrackColor: const Color(0xFF00E676)
                          .withValues(alpha: 0.3),
                      inactiveThumbColor: const Color(0xFFFF5252),
                      inactiveTrackColor: const Color(0xFFFF5252)
                          .withValues(alpha: 0.3),
                      onChanged: (val) {
                        if (val) {
                          _startServer();
                        } else {
                          _stopServer();
                        }
                      },
                    ),
                  ),
                ),
              ],
            ),
          ),

          // Primary Actions
          Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: 16.0,
              vertical: 12.0,
            ),
            child: Row(
              children: [
                // Open Game Button
                Expanded(
                  flex: 5,
                  child: ElevatedButton.icon(
                    onPressed: _launchGame,
                    icon: const Icon(Icons.sports_esports, color: Colors.white),
                    label: const Text(
                      'Launch Game',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 13,
                      ),
                    ),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFFC02A2A),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 10),
                // Game Access Manager Button
                Expanded(
                  flex: 5,
                  child: OutlinedButton.icon(
                    onPressed: _openGameAccessManager,
                    icon: const Icon(Icons.tune, color: Color(0xFF00E5FF)),
                    label: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Text(
                          'Missions & Unlocks',
                          style: TextStyle(
                            fontWeight: FontWeight.bold,
                            fontSize: 12,
                            color: Color(0xFF00E5FF),
                          ),
                        ),
                        Text(
                          '$completedCount / $totalCount Done (${_gameState.mode.toUpperCase()})',
                          style: TextStyle(
                            fontSize: 10,
                            color: Colors.white.withValues(alpha: 0.7),
                          ),
                        ),
                      ],
                    ),
                    style: OutlinedButton.styleFrom(
                      side: const BorderSide(
                        color: Color(0xFF00E5FF),
                        width: 1.5,
                      ),
                      padding: const EdgeInsets.symmetric(
                        vertical: 10,
                        horizontal: 8,
                      ),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),

          // Game Data Cache Installer Card
          _buildCacheInstallerCard(),

          // Live Activity Log Header
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16.0),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'Server Activity Stream',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 13,
                    color: Colors.white70,
                  ),
                ),
                TextButton(
                  onPressed: () => setState(() => _logs.clear()),
                  child: const Text('Clear', style: TextStyle(fontSize: 12)),
                ),
              ],
            ),
          ),

          // Activity Logs ListView
          Expanded(
            child: Container(
              margin: const EdgeInsets.symmetric(
                horizontal: 16.0,
                vertical: 6.0,
              ),
              padding: const EdgeInsets.all(8.0),
              decoration: BoxDecoration(
                color: const Color(0xFF080C12),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: Colors.white12),
              ),
              child: _logs.isEmpty
                  ? const Center(
                      child: Text(
                        'No requests captured yet.\nStart game to see offline telemetry.',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Colors.white38, fontSize: 13),
                      ),
                    )
                  : ListView.builder(
                      itemCount: _logs.length,
                      itemBuilder: (ctx, idx) {
                        final log = _logs[idx];
                        Color color = Colors.white70;
                        if (log.contains('200 OK') ||
                            log.contains('online') ||
                            log.contains('completed')) {
                          color = const Color(0xFF81C784);
                        } else if (log.contains('404') ||
                            log.contains('error') ||
                            log.contains('Failed')) {
                          color = const Color(0xFFE57373);
                        } else if (log.contains('OpenMission') ||
                            log.contains('EndMission')) {
                          color = const Color(0xFF4FC3F7);
                        }
                        return Padding(
                          padding: const EdgeInsets.symmetric(vertical: 2.0),
                          child: Text(
                            log,
                            style: TextStyle(
                              fontFamily: 'monospace',
                              fontSize: 11,
                              color: color,
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ),
        ],
      ),
    );
  }
}

class GameAccessScreen extends StatefulWidget {
  final GameAccessState gameState;
  final Future<void> Function(String) onLog;
  final MethodChannel gameChannel;
  final Future<void> Function(GameAccessState) onStateChanged;

  const GameAccessScreen({
    super.key,
    required this.gameState,
    required this.onLog,
    required this.gameChannel,
    required this.onStateChanged,
  });

  @override
  State<GameAccessScreen> createState() => _GameAccessScreenState();
}

class _GameAccessScreenState extends State<GameAccessScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  String _selectedCampaignFilter = 'All';

  final _uploadIdController = TextEditingController(text: 'story_custom_01');
  final _uploadTitleController = TextEditingController(
    text: 'Shadows over Florence',
  );

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 4, vsync: this);
    _scanAndLoadCustomMissions();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _uploadIdController.dispose();
    _uploadTitleController.dispose();
    super.dispose();
  }

  Future<void> _scanAndLoadCustomMissions() async {
    try {
      final names = await widget.gameChannel.invokeListMethod<String>(
        'listCustomMissions',
      );
      if (names == null || names.isEmpty) return;
      bool changed = false;
      for (final name in names) {
        if (name.endsWith('.json')) {
          final content = await widget.gameChannel.invokeMethod<String>(
            'readCustomMissionFile',
            {'filename': name},
          );
          if (content != null && content.isNotEmpty) {
            try {
              final map = jsonDecode(content) as Map<String, dynamic>;
              final mid =
                  map['missionId'] as String? ??
                  map['id'] as String? ??
                  name.replaceAll('.json', '');
              if (!widget.gameState.customMissions.containsKey(mid)) {
                widget.gameState.customMissions[mid] = map;
                widget.gameState.unlockedMissions.add(mid);
                changed = true;
              }
            } catch (_) {}
          }
        }
      }
      if (widget.gameState.activeCustomMissionId == null &&
          widget.gameState.customMissions.isNotEmpty) {
        widget.gameState.activeCustomMissionId =
            widget.gameState.customMissions.keys.first;
        changed = true;
      }
      if (changed && mounted) {
        setState(() {});
        await widget.onStateChanged(widget.gameState);
      }
    } catch (_) {}
  }

  Future<void> _setActiveCustomMission(
    String mid, [
    String slot = 'story_0015',
  ]) async {
    final map = widget.gameState.customMissions[mid];
    if (map != null) {
      final bin = buildCustomMissionBinary(map);
      await widget.gameChannel.invokeMethod('saveCustomMission', {
        'filename': 'openMission_story_0015.bin',
        'bytes': bin,
      });
      await widget.gameChannel.invokeMethod('saveCustomMission', {
        'filename': 'openMission_story_crows_01.bin',
        'bytes': bin,
      });
      if (slot != 'story_0015' && slot != 'story_crows_01') {
        await widget.gameChannel.invokeMethod('saveCustomMission', {
          'filename': 'openMission_$slot.bin',
          'bytes': bin,
        });
      }
      await widget.gameChannel.invokeMethod('saveCustomMission', {
        'filename': 'openMission_$mid.bin',
        'bytes': bin,
      });
    }

    setState(() {
      widget.gameState.activeCustomMissionId = mid;
      widget.gameState.activeSlot = slot;
      widget.gameState.unlockedMissions.add(mid);
      widget.gameState.unlockedMissions.add('story_0015');
      widget.gameState.unlockedMissions.add('story_crows_01');
      if (slot.isNotEmpty) widget.gameState.unlockedMissions.add(slot);
      widget.gameState.mode = 'custom';
    });
    await widget.onStateChanged(widget.gameState);

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Custom mission activated! Playable under CONTRACTS & ITALY (A Murder of Crows)!',
          ),
          backgroundColor: Colors.green,
          duration: Duration(seconds: 4),
        ),
      );
    }
  }

  Future<void> _deleteCustomMission(String mid) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: const Color(0xFF162130),
        title: const Text(
          'Delete Custom Mission?',
          style: TextStyle(color: Colors.white),
        ),
        content: Text(
          'Are you sure you want to remove "$mid"? This will delete its mission file from your device.',
          style: const TextStyle(color: Colors.white70),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text(
              'Cancel',
              style: TextStyle(color: Colors.white70),
            ),
          ),
          ElevatedButton(
            onPressed: () => Navigator.of(ctx).pop(true),
            style: ElevatedButton.styleFrom(backgroundColor: Colors.red),
            child: const Text('Delete'),
          ),
        ],
      ),
    );

    if (confirm != true) return;

    try {
      await widget.gameChannel.invokeMethod('deleteCustomMission', {
        'filename': mid,
      });
      setState(() {
        widget.gameState.customMissions.remove(mid);
        widget.gameState.unlockedMissions.remove(mid);
        if (widget.gameState.activeCustomMissionId == mid) {
          widget.gameState.activeCustomMissionId =
              widget.gameState.customMissions.isNotEmpty
              ? widget.gameState.customMissions.keys.first
              : null;
        }
      });
      await widget.onStateChanged(widget.gameState);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Custom mission "$mid" deleted.'),
            backgroundColor: Colors.orange,
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Delete error: $e'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _unlockAllMissions() async {
    setState(() {
      widget.gameState.mode = 'unlocked';
      for (final m in kAllMissions) {
        widget.gameState.unlockedMissions.add(m.id);
      }
      for (final cm in widget.gameState.customMissions.keys) {
        widget.gameState.unlockedMissions.add(cm);
      }
      widget.gameState.unlockedCampaigns.addAll([
        'campaign_1',
        'campaign_2',
        'campaign_3',
      ]);
    });
    await widget.onStateChanged(widget.gameState);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('All missions and campaigns unlocked!'),
          backgroundColor: Colors.green,
        ),
      );
    }
  }

  Future<void> _resetAllMissions() async {
    setState(() {
      widget.gameState.mode = 'fresh';
      widget.gameState.unlockedMissions.clear();
      widget.gameState.completedMissions.clear();
      widget.gameState.unlockedCampaigns.clear();
      widget.gameState.unlockedMissions.add('story_tutorial_01');
      widget.gameState.unlockedCampaigns.add('campaign_1');
    });
    await widget.onStateChanged(widget.gameState);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('All progress reset (Story Mode: Tutorial 1).'),
          backgroundColor: Colors.orange,
        ),
      );
    }
  }

  void _toggleMissionUnlocked(String mid, bool val) async {
    setState(() {
      widget.gameState.mode = 'custom';
      if (val) {
        widget.gameState.unlockedMissions.add(mid);
      } else {
        widget.gameState.unlockedMissions.remove(mid);
        widget.gameState.completedMissions.remove(mid);
      }
    });
    await widget.onStateChanged(widget.gameState);
  }

  void _toggleMissionCompleted(String mid, bool val) async {
    setState(() {
      widget.gameState.mode = 'custom';
      if (val) {
        widget.gameState.completedMissions.add(mid);
        widget.gameState.unlockedMissions.add(mid);
      } else {
        widget.gameState.completedMissions.remove(mid);
      }
    });
    await widget.onStateChanged(widget.gameState);
  }

  Future<void> _downloadSkillFiles() async {
    try {
      try {
        await widget.gameChannel.invokeMethod('requestStoragePermission');
      } catch (_) {}

      final skillMd = await rootBundle.loadString('assets/skill/SKILL.md');
      String instructionsMd = '';
      try {
        instructionsMd = await rootBundle.loadString(
          'assets/skill/INSTRUCTIONS.md',
        );
      } catch (_) {}
      final specMd = await rootBundle.loadString(
        'assets/skill/CUSTOM_MISSION_SPEC.md',
      );
      final sampleJson = await rootBundle.loadString(
        'assets/skill/sample_mission.json',
      );

      final res = await widget.gameChannel.invokeMapMethod<String, dynamic>(
        'exportSkillFiles',
        {
          'skillMd': skillMd,
          'instructionsMd': instructionsMd,
          'specMd': specMd,
          'sampleJson': sampleJson,
        },
      );

      final downloadPath =
          res?['path'] as String? ??
          '/sdcard/Download/ACIdentity_Mission_Skill';

      if (!mounted) return;

      const aiPromptText =
          "You are the Senior Mission Director for Assassin's Creed Identity (v2.8.2). "
          "Please read the attached SKILL.md and INSTRUCTIONS.md. "
          "Interview me step-by-step: ask me for the game slot, location, weather/mood, objective (kill, protect, follow noble/king/maid on rooftops), and enemy targets. "
          "Once we agree, output a valid custom mission JSON matching the specification.";

      showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          backgroundColor: const Color(0xFF162130),
          title: Row(
            children: const [
              Icon(Icons.check_circle, color: Color(0xFF00E676)),
              SizedBox(width: 8),
              Expanded(
                child: Text(
                  'Skill & Assets Package Exported',
                  style: TextStyle(fontSize: 16, color: Colors.white),
                ),
              ),
            ],
          ),
          content: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                  'Package exported to your device Downloads folder:',
                  style: TextStyle(color: Colors.white70, fontSize: 13),
                ),
                const SizedBox(height: 6),
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: Colors.black38,
                    borderRadius: BorderRadius.circular(6),
                    border: Border.all(color: Colors.white24),
                  ),
                  child: Text(
                    downloadPath,
                    style: const TextStyle(
                      color: Color(0xFF00E5FF),
                      fontFamily: 'monospace',
                      fontSize: 11,
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                const Text(
                  'Files included:\n'
                  '• SKILL.md (Autonomous Mission Designer Skill)\n'
                  '• INSTRUCTIONS.md (Interactive AI Interview Protocol)\n'
                  '• AC_IDENTITY_ASSETS_GUIDE.pdf (3-Page Printable Encyclopedia)\n'
                  '• CUSTOM_MISSION_SPEC.md (Coordinates & Archetypes)\n'
                  '• sample_mission.json (Working Example)',
                  style: TextStyle(color: Colors.white70, fontSize: 11),
                ),
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: () async {
                      try {
                        await widget.gameChannel.invokeMethod(
                          'shareSkillFiles',
                        );
                      } catch (e) {
                        if (mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(
                            SnackBar(content: Text('Share error: $e')),
                          );
                        }
                      }
                    },
                    icon: const Icon(Icons.share, size: 16),
                    label: const Text('Share / Open Package (PDF & Files)'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF00E676),
                      foregroundColor: Colors.black,
                    ),
                  ),
                ),
                const SizedBox(height: 8),
                SizedBox(
                  width: double.infinity,
                  child: ElevatedButton.icon(
                    onPressed: () {
                      Clipboard.setData(
                        const ClipboardData(text: aiPromptText),
                      );
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'AI Interview Prompt copied to clipboard!',
                          ),
                          backgroundColor: Colors.green,
                        ),
                      );
                    },
                    icon: const Icon(Icons.copy, size: 16),
                    label: const Text('Copy AI Prompt to Clipboard'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF00E5FF),
                      foregroundColor: Colors.black,
                    ),
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text(
                'Close',
                style: TextStyle(color: Colors.white70),
              ),
            ),
          ],
        ),
      );
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Error downloading skill: $e'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _pickAndUploadMission() async {
    try {
      final picked = await widget.gameChannel.invokeMapMethod<String, dynamic>(
        'pickMissionFile',
      );
      if (picked == null) return;

      final name = picked['name'] as String? ?? 'mission.json';
      final text = picked['text'] as String?;
      final bytes = picked['bytes'] as Uint8List?;

      String mid = _uploadIdController.text.trim();
      if (mid.isEmpty) {
        mid = name.replaceAll('.json', '').replaceAll('.bin', '');
        if (mid.startsWith('openMission_')) {
          mid = mid.substring('openMission_'.length);
        }
      }
      String title = _uploadTitleController.text.trim();
      if (title.isEmpty) {
        title = mid.replaceAll('_', ' ').toUpperCase();
      }

      if (name.endsWith('.bin') && bytes != null) {
        await widget.gameChannel.invokeMethod('saveCustomMission', {
          'filename': 'openMission_$mid.bin',
          'bytes': bytes,
        });
        await widget.gameChannel.invokeMethod('saveCustomMission', {
          'filename': 'openMission_story_0015.bin',
          'bytes': bytes,
        });
        await widget.gameChannel.invokeMethod('saveCustomMission', {
          'filename': 'openMission_story_crows_01.bin',
          'bytes': bytes,
        });
        final customDef = {
          'missionId': mid,
          'title': title,
          'region': 'Firenze_Palazzo',
          'mood': 'Daytime',
          'objective': 'Custom binary mission payload.',
          'targetSlot': 'both',
          'timestamp': DateTime.now().toIso8601String(),
        };
        await widget.gameChannel.invokeMethod('saveCustomMission', {
          'filename': '$mid.json',
          'text': jsonEncode(customDef),
        });

        setState(() {
          _uploadIdController.text = mid;
          _uploadTitleController.text = title;
          widget.gameState.customMissions[mid] = customDef;
          widget.gameState.activeCustomMissionId = mid;
          widget.gameState.activeSlot = 'story_0015';
          widget.gameState.unlockedMissions.add(mid);
          widget.gameState.unlockedMissions.add('story_0015');
          widget.gameState.unlockedMissions.add('story_crows_01');
          widget.gameState.mode = 'custom';
        });
        await widget.onStateChanged(widget.gameState);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(
                'Binary mission "$title" installed, activated & added to Custom: Mission!',
              ),
              backgroundColor: Colors.green,
              duration: const Duration(seconds: 4),
            ),
          );
          _tabController.animateTo(1);
        }
        return;
      }

      if (text != null && text.isNotEmpty) {
        try {
          final map = jsonDecode(text) as Map<String, dynamic>;
          final jsonMid = map['missionId'] as String? ?? map['id'] as String?;
          final jsonTitle = map['title'] as String?;
          if (jsonMid != null && jsonMid.isNotEmpty) mid = jsonMid;
          if (jsonTitle != null && jsonTitle.isNotEmpty) title = jsonTitle;

          _uploadIdController.text = mid;
          _uploadTitleController.text = title;

          map['missionId'] = mid;
          map['title'] = title;
          map['targetSlot'] = 'both';

          final bin = buildCustomMissionBinary(map);
          await widget.gameChannel.invokeMethod('saveCustomMission', {
            'filename': '$mid.json',
            'text': jsonEncode(map),
          });
          await widget.gameChannel.invokeMethod('saveCustomMission', {
            'filename': 'openMission_$mid.bin',
            'bytes': bin,
          });
          await widget.gameChannel.invokeMethod('saveCustomMission', {
            'filename': 'openMission_story_0015.bin',
            'bytes': bin,
          });
          await widget.gameChannel.invokeMethod('saveCustomMission', {
            'filename': 'openMission_story_crows_01.bin',
            'bytes': bin,
          });

          setState(() {
            widget.gameState.customMissions[mid] = map;
            widget.gameState.activeCustomMissionId = mid;
            widget.gameState.activeSlot = 'story_0015';
            widget.gameState.unlockedMissions.add(mid);
            widget.gameState.unlockedMissions.add('story_0015');
            widget.gameState.unlockedMissions.add('story_crows_01');
            widget.gameState.mode = 'custom';
          });
          await widget.onStateChanged(widget.gameState);

          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(
                  'Custom mission "$title" uploaded & activated in Contracts & Italy!',
                ),
                backgroundColor: Colors.green,
                duration: const Duration(seconds: 4),
              ),
            );
            _tabController.animateTo(1);
          }
        } catch (e) {
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text('Invalid mission JSON: $e'),
                backgroundColor: Colors.red,
              ),
            );
          }
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Upload error: $e'),
            backgroundColor: Colors.red,
          ),
        );
      }
    }
  }

  Future<void> _manualSaveAndActivate() async {
    final mid = _uploadIdController.text.trim();
    final title = _uploadTitleController.text.trim().isEmpty
        ? 'Custom Mission'
        : _uploadTitleController.text.trim();

    if (mid.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Please enter a Mission ID.'),
          backgroundColor: Colors.red,
        ),
      );
      return;
    }

    final customDef = {
      'missionId': mid,
      'title': title,
      'region': 'Firenze_Palazzo',
      'mood': 'Daytime',
      'objective': 'Eliminate designated targets.',
      'targetSlot': 'both',
      'timestamp': DateTime.now().toIso8601String(),
    };

    final jsonStr = jsonEncode(customDef);
    final bin = buildCustomMissionBinary(customDef);

    await widget.gameChannel.invokeMethod('saveCustomMission', {
      'filename': '$mid.json',
      'text': jsonStr,
    });
    await widget.gameChannel.invokeMethod('saveCustomMission', {
      'filename': 'openMission_$mid.bin',
      'bytes': bin,
    });
    await widget.gameChannel.invokeMethod('saveCustomMission', {
      'filename': 'openMission_story_0015.bin',
      'bytes': bin,
    });
    await widget.gameChannel.invokeMethod('saveCustomMission', {
      'filename': 'openMission_story_crows_01.bin',
      'bytes': bin,
    });

    setState(() {
      widget.gameState.customMissions[mid] = customDef;
      widget.gameState.activeCustomMissionId = mid;
      widget.gameState.activeSlot = 'story_0015';
      widget.gameState.mode = 'custom';
      widget.gameState.unlockedMissions.add(mid);
      widget.gameState.unlockedMissions.add('story_0015');
      widget.gameState.unlockedMissions.add('story_crows_01');
    });
    await widget.onStateChanged(widget.gameState);

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Custom mission "$title" saved & active in Contracts & Italy!',
          ),
          backgroundColor: Colors.green,
          duration: const Duration(seconds: 4),
        ),
      );
      _tabController.animateTo(1);
    }
  }

  @override
  Widget build(BuildContext context) {
    final customList = widget.gameState.customMissions.values.map((json) {
      final mid =
          json['missionId'] as String? ?? json['id'] as String? ?? 'custom';
      final title = json['title'] as String? ?? 'Custom Mission';
      final region = json['region'] as String? ?? 'Firenze_Palazzo';
      final mood = json['mood'] as String? ?? 'Daytime';
      final obj =
          json['objective'] as String? ??
          (json['briefing'] as String? ?? 'Eliminate targets');
      final slot = json['targetSlot'] as String? ?? widget.gameState.activeSlot;
      return MissionMeta(
        id: mid,
        campaign: '★ Custom Missions',
        title: title,
        location: '$region - $mood',
        objective: obj,
        isCustom: true,
        targetSlot: slot,
      );
    }).toList();

    final allMissions = [...customList, ...kAllMissions];

    final filteredCampaignMissions = _selectedCampaignFilter == 'All'
        ? kAllMissions
        : kAllMissions
              .where((m) => m.campaign.contains(_selectedCampaignFilter))
              .toList();

    return Scaffold(
      appBar: AppBar(
        title: const Text('Game Server'),
        backgroundColor: const Color(0xFF101722),
        actions: [
          IconButton(
            tooltip: 'Download Skill & Assets PDF',
            icon: const Icon(Icons.download, color: Color(0xFF00E5FF)),
            onPressed: _downloadSkillFiles,
          ),
          IconButton(
            tooltip: 'Share / Open Package',
            icon: const Icon(Icons.share, color: Color(0xFF00E676)),
            onPressed: () => widget.gameChannel.invokeMethod('shareSkillFiles'),
          ),
        ],
        bottom: TabBar(
          controller: _tabController,
          indicatorColor: const Color(0xFF00E5FF),
          labelColor: const Color(0xFF00E5FF),
          unselectedLabelColor: Colors.white60,
          labelStyle: const TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.bold,
          ),
          tabs: [
            const Tab(icon: Icon(Icons.shield, size: 18), text: 'Campaign'),
            Tab(
              icon: const Icon(Icons.star, size: 18),
              text: 'Custom: Mission (${customList.length})',
            ),
            const Tab(icon: Icon(Icons.file_upload, size: 18), text: 'Upload'),
            const Tab(
              icon: Icon(Icons.menu_book, size: 18),
              text: 'Guide & Assets',
            ),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [
          _buildCampaignTab(filteredCampaignMissions, allMissions.length),
          _buildCustomMissionTab(customList),
          _buildUploadTab(),
          _buildGuideTab(),
        ],
      ),
    );
  }

  Widget _buildCampaignTab(List<MissionMeta> missions, int totalMissionsCount) {
    final unlockedCount = widget.gameState.unlockedMissions.length;
    final completedCount = widget.gameState.completedMissions.length;

    return Column(
      children: [
        // Mode Banner
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          color: const Color(0xFF162130),
          child: Row(
            children: [
              Icon(
                widget.gameState.mode == 'unlocked'
                    ? Icons.lock_open
                    : (widget.gameState.mode == 'fresh'
                          ? Icons.timeline
                          : Icons.tune),
                color: const Color(0xFF00E5FF),
                size: 20,
              ),
              const SizedBox(width: 8),
              Expanded(
                child: Text(
                  widget.gameState.mode == 'unlocked'
                      ? 'MODE: SANDBOX (ALL UNLOCKED)'
                      : (widget.gameState.mode == 'fresh'
                            ? 'MODE: STORY PROGRESSION'
                            : 'MODE: CUSTOM OVERRIDE'),
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.bold,
                    letterSpacing: 0.8,
                    color: Color(0xFF00E5FF),
                  ),
                ),
              ),
              Text(
                '$unlockedCount Unlocked • $completedCount Done',
                style: const TextStyle(fontSize: 11, color: Colors.white70),
              ),
            ],
          ),
        ),

        // Campaign Filters & Actions
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          color: const Color(0xFF0C121A),
          child: Row(
            children: [
              Expanded(
                child: DropdownButtonHideUnderline(
                  child: DropdownButton<String>(
                    isExpanded: true,
                    value: _selectedCampaignFilter,
                    dropdownColor: const Color(0xFF162130),
                    style: const TextStyle(color: Colors.white, fontSize: 13),
                    items: const [
                      DropdownMenuItem(
                        value: 'All',
                        child: Text('All Campaign Missions'),
                      ),
                      DropdownMenuItem(
                        value: 'Montreal',
                        child: Text('Montreal - VR Training'),
                      ),
                      DropdownMenuItem(
                        value: 'Murder of Crows',
                        child: Text('Italy - A Murder of Crows'),
                      ),
                      DropdownMenuItem(
                        value: 'Crimson Sunset',
                        child: Text('Forlì - A Crimson Sunset'),
                      ),
                      DropdownMenuItem(
                        value: 'Bonus',
                        child: Text('Bonus Contracts'),
                      ),
                    ],
                    onChanged: (val) {
                      if (val != null) {
                        setState(() => _selectedCampaignFilter = val);
                      }
                    },
                  ),
                ),
              ),
              const SizedBox(width: 8),
              ElevatedButton(
                onPressed: _unlockAllMissions,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF00C853),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 8,
                  ),
                ),
                child: const Text('Unlock All', style: TextStyle(fontSize: 11)),
              ),
              const SizedBox(width: 6),
              ElevatedButton(
                onPressed: _resetAllMissions,
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFFE53935),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 8,
                  ),
                ),
                child: const Text('Reset All', style: TextStyle(fontSize: 11)),
              ),
            ],
          ),
        ),

        // Mission List
        Expanded(
          child: ListView.builder(
            padding: const EdgeInsets.all(10),
            itemCount: missions.length,
            itemBuilder: (ctx, idx) {
              final m = missions[idx];
              final isUnlocked = widget.gameState.isUnlocked(m.id);
              final isCompleted = widget.gameState.isCompleted(m.id);

              return Card(
                margin: const EdgeInsets.symmetric(vertical: 5),
                color: isCompleted
                    ? const Color(0xFF0F231D)
                    : (isUnlocked
                          ? const Color(0xFF121B27)
                          : const Color(0xFF0A0F15)),
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(8),
                  side: BorderSide(
                    color: isCompleted
                        ? const Color(0xFF00E676).withValues(alpha: 0.5)
                        : (isUnlocked
                              ? const Color(0xFF00E5FF).withValues(alpha: 0.4)
                              : Colors.white10),
                  ),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Icon(
                            isCompleted
                                ? Icons.check_circle
                                : (isUnlocked
                                      ? Icons.play_circle_outline
                                      : Icons.lock_outline),
                            color: isCompleted
                                ? const Color(0xFF00E676)
                                : (isUnlocked
                                      ? const Color(0xFF00E5FF)
                                      : Colors.white30),
                            size: 18,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  m.title,
                                  style: TextStyle(
                                    fontWeight: FontWeight.bold,
                                    fontSize: 14,
                                    color: isUnlocked
                                        ? Colors.white
                                        : Colors.white54,
                                  ),
                                ),
                                Text(
                                  '${m.campaign} • ${m.location}',
                                  style: const TextStyle(
                                    fontSize: 11,
                                    color: Colors.white54,
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(
                        m.objective,
                        style: TextStyle(
                          fontSize: 12,
                          color: Colors.white.withValues(alpha: 0.75),
                          fontStyle: FontStyle.italic,
                        ),
                      ),
                      const Divider(height: 16, color: Colors.white12),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Row(
                            children: [
                              Text(
                                'Unlocked:',
                                style: TextStyle(
                                  fontSize: 12,
                                  color: isUnlocked
                                      ? Colors.white70
                                      : Colors.white38,
                                ),
                              ),
                              Switch(
                                value: isUnlocked,
                                activeThumbColor: const Color(0xFF00E5FF),
                                onChanged: (val) =>
                                    _toggleMissionUnlocked(m.id, val),
                              ),
                            ],
                          ),
                          Row(
                            children: [
                              Text(
                                'Completed:',
                                style: TextStyle(
                                  fontSize: 12,
                                  color: isCompleted
                                      ? const Color(0xFF00E676)
                                      : Colors.white38,
                                ),
                              ),
                              Switch(
                                value: isCompleted,
                                activeThumbColor: const Color(0xFF00E676),
                                onChanged: (val) =>
                                    _toggleMissionCompleted(m.id, val),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildCustomMissionTab(List<MissionMeta> customList) {
    final activeId = widget.gameState.activeCustomMissionId;

    return Column(
      children: [
        // Dual-Slot Active Status Banner
        Container(
          padding: const EdgeInsets.all(12),
          color: const Color(0xFF162130),
          child: Row(
            children: [
              const Icon(Icons.star, color: Color(0xFF00E5FF), size: 22),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'CUSTOM MISSIONS (${customList.length})',
                      style: const TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFF00E5FF),
                        letterSpacing: 0.5,
                      ),
                    ),
                    const SizedBox(height: 2),
                    const Text(
                      '🎮 Active mission playable on CONTRACTS & ITALY (A Murder of Crows)!',
                      style: TextStyle(fontSize: 11, color: Color(0xFF00E676)),
                    ),
                  ],
                ),
              ),
              ElevatedButton.icon(
                onPressed: () => _tabController.animateTo(2),
                icon: const Icon(Icons.add, size: 14),
                label: const Text('Add New', style: TextStyle(fontSize: 11)),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF00E5FF),
                  foregroundColor: Colors.black,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: 4,
                  ),
                  minimumSize: Size.zero,
                ),
              ),
            ],
          ),
        ),

        // List or Empty View
        Expanded(
          child: customList.isEmpty
              ? Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(
                          Icons.sports_esports_outlined,
                          size: 64,
                          color: Colors.white24,
                        ),
                        const SizedBox(height: 16),
                        const Text(
                          'No Custom Missions Installed',
                          style: TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                            color: Colors.white70,
                          ),
                        ),
                        const SizedBox(height: 8),
                        const Text(
                          'Upload a custom mission JSON or use the Guide tab to build one with AI.',
                          textAlign: TextAlign.center,
                          style: TextStyle(fontSize: 12, color: Colors.white38),
                        ),
                        const SizedBox(height: 20),
                        ElevatedButton.icon(
                          onPressed: () => _tabController.animateTo(2),
                          icon: const Icon(Icons.file_upload),
                          label: const Text('Go to Upload Tab'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF00E5FF),
                            foregroundColor: Colors.black,
                          ),
                        ),
                      ],
                    ),
                  ),
                )
              : ListView.builder(
                  padding: const EdgeInsets.all(10),
                  itemCount: customList.length,
                  itemBuilder: (ctx, idx) {
                    final m = customList[idx];
                    final isActive = activeId == m.id;

                    return Card(
                      margin: const EdgeInsets.symmetric(vertical: 6),
                      color: isActive
                          ? const Color(0xFF102820)
                          : const Color(0xFF132233),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(10),
                        side: BorderSide(
                          color: isActive
                              ? const Color(0xFF00E676)
                              : const Color(0xFF00E5FF).withValues(alpha: 0.6),
                          width: isActive ? 2 : 1,
                        ),
                      ),
                      child: Padding(
                        padding: const EdgeInsets.all(14),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            // Header Row
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Row(
                                  children: [
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 6,
                                        vertical: 2,
                                      ),
                                      decoration: BoxDecoration(
                                        color: const Color(0xFF00E5FF)
                                            .withValues(alpha: 0.2),
                                        borderRadius: BorderRadius.circular(4),
                                        border: Border.all(
                                          color: const Color(0xFF00E5FF),
                                        ),
                                      ),
                                      child: const Text(
                                        '★ CUSTOM MISSION',
                                        style: TextStyle(
                                          fontSize: 10,
                                          fontWeight: FontWeight.bold,
                                          color: Color(0xFF00E5FF),
                                          letterSpacing: 0.5,
                                        ),
                                      ),
                                    ),
                                    if (isActive) ...[
                                      const SizedBox(width: 8),
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 6,
                                          vertical: 2,
                                        ),
                                        decoration: BoxDecoration(
                                          color: const Color(0xFF00E676)
                                              .withValues(alpha: 0.2),
                                          borderRadius: BorderRadius.circular(
                                            4,
                                          ),
                                          border: Border.all(
                                            color: const Color(0xFF00E676),
                                          ),
                                        ),
                                        child: const Text(
                                          'ACTIVE IN GAME',
                                          style: TextStyle(
                                            fontSize: 10,
                                            fontWeight: FontWeight.bold,
                                            color: Color(0xFF00E676),
                                            letterSpacing: 0.5,
                                          ),
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                                IconButton(
                                  icon: const Icon(
                                    Icons.delete_outline,
                                    color: Colors.redAccent,
                                    size: 20,
                                  ),
                                  tooltip: 'Delete Mission',
                                  constraints: const BoxConstraints(),
                                  padding: EdgeInsets.zero,
                                  onPressed: () => _deleteCustomMission(m.id),
                                ),
                              ],
                            ),
                            const SizedBox(height: 8),
                            Text(
                              m.title,
                              style: const TextStyle(
                                fontSize: 16,
                                fontWeight: FontWeight.bold,
                                color: Colors.white,
                              ),
                            ),
                            Text(
                              'ID: ${m.id} • ${m.location}',
                              style: const TextStyle(
                                fontSize: 11,
                                color: Colors.white54,
                              ),
                            ),
                            const SizedBox(height: 6),
                            Text(
                              m.objective,
                              style: TextStyle(
                                fontSize: 12,
                                color: Colors.white.withValues(alpha: 0.8),
                                fontStyle: FontStyle.italic,
                              ),
                            ),
                            const SizedBox(height: 12),

                            // Activation Control Box
                            Container(
                              padding: const EdgeInsets.all(10),
                              decoration: BoxDecoration(
                                color: isActive
                                    ? const Color(0xFF00E676)
                                          .withValues(alpha: 0.15)
                                    : Colors.white.withValues(alpha: 0.04),
                                borderRadius: BorderRadius.circular(6),
                                border: Border.all(
                                  color: isActive
                                      ? const Color(0xFF00E676)
                                      : Colors.white24,
                                ),
                              ),
                              child: Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Expanded(
                                    child: Text(
                                      isActive
                                          ? '👉 Ready! In AC Identity, tap CONTRACTS or open ITALY to play!'
                                          : 'Tap "ACTIVATE" to load this into game Contracts & Italy.',
                                      style: TextStyle(
                                        fontSize: 11,
                                        fontWeight: isActive
                                            ? FontWeight.bold
                                            : FontWeight.normal,
                                        color: isActive
                                            ? const Color(0xFF00E676)
                                            : Colors.white70,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  ElevatedButton(
                                    onPressed: () =>
                                        _setActiveCustomMission(m.id),
                                    style: ElevatedButton.styleFrom(
                                      backgroundColor: isActive
                                          ? const Color(0xFF00E676)
                                          : const Color(0xFF00E5FF),
                                      foregroundColor: Colors.black,
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 10,
                                        vertical: 6,
                                      ),
                                      minimumSize: Size.zero,
                                    ),
                                    child: Text(
                                      isActive
                                          ? 'PLAYING IN GAME'
                                          : 'ACTIVATE TO PLAY',
                                      style: const TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.bold,
                                      ),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }

  Widget _buildUploadTab() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Card(
            color: const Color(0xFF131D2A),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
              side: const BorderSide(color: Color(0xFF00E5FF), width: 1.5),
            ),
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: const [
                      Icon(
                        Icons.file_upload,
                        color: Color(0xFF00E5FF),
                        size: 24,
                      ),
                      SizedBox(width: 10),
                      Text(
                        'Upload Custom Mission',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Select your mission JSON or binary file to compile and activate in game.',
                    style: TextStyle(fontSize: 12, color: Colors.white70),
                  ),
                  const SizedBox(height: 20),

                  // 1. Mission ID
                  const Text(
                    'Mission ID:',
                    style: TextStyle(
                      color: Color(0xFF00E5FF),
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _uploadIdController,
                    style: const TextStyle(color: Colors.white),
                    decoration: InputDecoration(
                      hintText: 'e.g. story_custom_01',
                      hintStyle: const TextStyle(color: Colors.white30),
                      filled: true,
                      fillColor: Colors.black38,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: Colors.white24),
                      ),
                      focusedBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: Color(0xFF00E5FF)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // 2. Mission Title
                  const Text(
                    'Mission Title:',
                    style: TextStyle(
                      color: Color(0xFF00E5FF),
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                  const SizedBox(height: 6),
                  TextField(
                    controller: _uploadTitleController,
                    style: const TextStyle(color: Colors.white),
                    decoration: InputDecoration(
                      hintText: 'e.g. Shadows over Florence',
                      hintStyle: const TextStyle(color: Colors.white30),
                      filled: true,
                      fillColor: Colors.black38,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: Colors.white24),
                      ),
                      focusedBorder: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(8),
                        borderSide: const BorderSide(color: Color(0xFF00E5FF)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),

                  // 3. Upload File Button
                  SizedBox(
                    width: double.infinity,
                    height: 48,
                    child: ElevatedButton.icon(
                      onPressed: _pickAndUploadMission,
                      icon: const Icon(Icons.file_upload, size: 20),
                      label: const Text(
                        'Upload File',
                        style: TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                      style: ElevatedButton.styleFrom(
                        backgroundColor: const Color(0xFF00E5FF),
                        foregroundColor: Colors.black,
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),

                  // Optional Manual Save & Activate
                  SizedBox(
                    width: double.infinity,
                    height: 42,
                    child: OutlinedButton.icon(
                      onPressed: _manualSaveAndActivate,
                      icon: const Icon(Icons.save_outlined, size: 18),
                      label: const Text('Save & Activate Manually'),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: const Color(0xFF00E5FF),
                        side: const BorderSide(color: Color(0xFF00E5FF)),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildGuideTab() {
    const aiPromptText =
        "You are the Senior Mission Director for Assassin's Creed Identity (v2.8.2). "
        "Please read the attached SKILL.md and INSTRUCTIONS.md. "
        "Interview me step-by-step: ask me for the game slot, location, weather/mood, objective (kill, protect, follow noble/king/maid on rooftops), and enemy targets. "
        "Once we agree, output a valid custom mission JSON matching the specification.";

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Download Card
          Card(
            color: const Color(0xFF132233),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(10),
              side: const BorderSide(color: Color(0xFF00E5FF), width: 1.5),
            ),
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: const [
                      Icon(
                        Icons.download_for_offline,
                        color: Color(0xFF00E5FF),
                        size: 24,
                      ),
                      SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          'AI Skill, Instructions & Assets PDF Package',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Export the complete creator toolkit to your device Downloads folder: SKILL.md, INSTRUCTIONS.md, AC_IDENTITY_ASSETS_GUIDE.pdf, CUSTOM_MISSION_SPEC.md, and sample_mission.json.',
                    style: TextStyle(fontSize: 12, color: Colors.white70),
                  ),
                  const SizedBox(height: 14),
                  Row(
                    children: [
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: _downloadSkillFiles,
                          icon: const Icon(Icons.download, size: 16),
                          label: const Text(
                            'Download Package',
                            style: TextStyle(fontSize: 12),
                          ),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF00E5FF),
                            foregroundColor: Colors.black,
                            padding: const EdgeInsets.symmetric(vertical: 10),
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: ElevatedButton.icon(
                          onPressed: () => widget.gameChannel.invokeMethod(
                            'shareSkillFiles',
                          ),
                          icon: const Icon(Icons.share, size: 16),
                          label: const Text(
                            'Share / Open',
                            style: TextStyle(fontSize: 12),
                          ),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: const Color(0xFF00E676),
                            foregroundColor: Colors.black,
                            padding: const EdgeInsets.symmetric(vertical: 10),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton.icon(
                      onPressed: () {
                        Clipboard.setData(
                          const ClipboardData(text: aiPromptText),
                        );
                        ScaffoldMessenger.of(context).showSnackBar(
                          const SnackBar(
                            content: Text(
                              'AI Interview Prompt copied to clipboard!',
                            ),
                            backgroundColor: Colors.green,
                          ),
                        );
                      },
                      icon: const Icon(Icons.copy, size: 16),
                      label: const Text(
                        'Copy AI Prompt to Clipboard',
                        style: TextStyle(fontSize: 12),
                      ),
                      style: OutlinedButton.styleFrom(
                        foregroundColor: const Color(0xFF00E5FF),
                        side: const BorderSide(color: Color(0xFF00E5FF)),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),

          // How to Build Section
          _buildGuideSectionCard(
            title: 'HOW TO BUILD MISSIONS WITH AI',
            icon: Icons.psychology,
            color: const Color(0xFF00E5FF),
            items: const [
              '1. Upload SKILL.md and INSTRUCTIONS.md to your AI chat (ChatGPT, Claude, Gemini).',
              '2. The AI will interview you step-by-step: asks for slot, location, time/mood, and objectives.',
              '3. Objectives include Assassination, Protection (King, Noble, Maid), or Rooftop Tailing.',
              '4. The AI outputs a verified mission.json file.',
              '5. In the Upload tab, enter ID/Title and select the file -> Playable under Contracts & Italy!',
            ],
          ),
          const SizedBox(height: 12),

          // Section 1: Persons, Characters & Roles
          _buildGuideSectionCard(
            title: '1. PERSONS, CHARACTERS, VIPS & ENEMIES',
            icon: Icons.people,
            color: Colors.amberAccent,
            items: const [
              '• King / Sovereign: Royal monarch requiring escort or high-profile target.',
              '• Maid / Servant: Vulnerable domestic informant carrying secret keys.',
              '• Florentine Noble: Wealthy Renaissance patron or Borgia collaborator.',
              '• Courtesan: Distracts Templar guard sentries on street corners.',
              '• Thief / Informant: Agile runner who spots rooftop archers and routes.',
              '• Friar / Monk: Walking in procession; ideal for crowd blending.',
              '• Borgia Captain (Special_Captain_01): Heavy plate armor, broadsword combatant.',
              '• Templar Knight (Guard_Melee_01): Sword and shield frontline infantry.',
              '• Crossbow Sniper (Guard_Ranged_01): Stationed on rooftops with long-range sight.',
              '• Heavy Brute (Guard_Brute_01): Massive two-handed axe; blocks frontal attacks.',
              '• Agile Scout (Guard_Agile_01): Fast runner with stiletto daggers; scales walls.',
              '• Corrupt Inquisitor (Special_Doctor_01): Secretive Templar mastermind.',
            ],
          ),
          const SizedBox(height: 12),

          // Section 2: Animals, Traversal & Rides
          _buildGuideSectionCard(
            title: '2. ANIMALS, RIDES & TRAVERSAL PROPS',
            icon: Icons.pets,
            color: Colors.greenAccent,
            items: const [
              '• Italian Horses & Wagons: Street obstacles, stables, and carts in piazzas.',
              '• Guard Dogs: Sniff out hidden assassins in haystacks; bark to alert sentries.',
              '• Pigeons & Birds: Perched on wooden vantage beams for Leap of Faith points.',
              '• Haystacks: Leap of Faith landing zones and instant stealth hiding spots.',
              '• Sitting Benches: Sit among citizens to drop guard suspicion meter to zero.',
              '• Wooden Beams & Ziplines: Rooftop highway connections across buildings.',
              '• Scaffolding & Catwalks: Elevated vantage points for aerial assassinations.',
            ],
          ),
          const SizedBox(height: 12),

          // Section 3: Places & Historical Maps
          _buildGuideSectionCard(
            title: '3. HISTORICAL MAPS & LOCATIONS',
            icon: Icons.location_on,
            color: Colors.orangeAccent,
            items: const [
              '• Firenze Palazzo della Signoria (Firenze_Palazzo): Open civic plaza, Palazzo Vecchio, terracotta roofs.',
              '• Firenze Santa Croce (Firenze_SantaCroce): Church square, cloister arcades, bell tower, market stalls.',
              '• Roma Colosseo (Roma_Coloseum): Ancient Roman amphitheater, ruins, gladiatorial arena, sunlit arches.',
              '• Monteriggioni Villa Auditore (Monteriggioni): Tuscan fortress village, ramparts, courtyard gardens.',
              '• Forli Romagna Stronghold (Forli): Waterways, marshland moat, imposing stone citadel battlements.',
            ],
          ),
          const SizedBox(height: 12),

          // Section 4: Atmospheres & Moods
          _buildGuideSectionCard(
            title: '4. LIGHTING & ATMOSPHERES (MOODS)',
            icon: Icons.wb_sunny,
            color: Colors.yellowAccent,
            items: const [
              '• Daytime: Bright Italian sunlight, high guard visibility, bustling piazzas.',
              '• Dusk: Golden hour sunset, lengthening shadows, lantern-lit streets.',
              '• Nighttime: Deep shadows, torchlit alleys, optimal for stealth assassinations.',
              '• Foggy: Thick morning mist, reduced guard sightlines, tense atmosphere.',
            ],
          ),
          const SizedBox(height: 12),

          // Section 5: Mission Types & Rooftop Stealth
          _buildGuideSectionCard(
            title: '5. MISSION TYPES & ROOFTOP TAILING RULES',
            icon: Icons.sports_kabaddi,
            color: Colors.redAccent,
            items: const [
              '• Assassinate / Eliminate: Take down high-value Templar commanders and captains.',
              '• Protect / Escort: Safeguard a King, Noble, or Maid through hostile streets.',
              '• Rooftop Tailing Distance: Maintain 15 to 40 meters distance from target.',
              '• Rooftop Traversal: Leap across beams and chimneys to maintain sight from above.',
              '• Detection Contingency: If guard indicator turns YELLOW/RED:',
              '    a) Break line of sight and dive into a haystack or sit on a bench.',
              '    b) Or perform a rapid silent assassination on the alerted sentry before alarm rings!',
              '• Infiltrate & Steal: Sneak into guarded vaults undetected to retrieve Borgia codex letters.',
            ],
          ),
          const SizedBox(height: 20),
        ],
      ),
    );
  }

  Widget _buildGuideSectionCard({
    required String title,
    required IconData icon,
    required Color color,
    required List<String> items,
  }) {
    return Card(
      color: const Color(0xFF131D2A),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(8),
        side: BorderSide(color: color.withValues(alpha: 0.3)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: color, size: 20),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.bold,
                      color: color,
                      letterSpacing: 0.5,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            for (final item in items) ...[
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 2),
                child: Text(
                  item,
                  style: const TextStyle(
                    fontSize: 11,
                    color: Colors.white70,
                    height: 1.35,
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
