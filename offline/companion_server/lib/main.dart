import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show MethodChannel, rootBundle;

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

  const MissionMeta({
    required this.id,
    required this.campaign,
    required this.title,
    required this.location,
    required this.objective,
  });
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
    objective: 'Eliminate merchant guards and secure confiscated supply manifests.',
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
    objective: 'Infiltrate the crowded market and dispatch the corrupt magistrate.',
  ),
  MissionMeta(
    id: 'story_crows_08',
    campaign: 'Italy - A Murder of Crows',
    title: 'Blood on the Tiber',
    location: 'Roma Colosseo - Dusk',
    objective: 'Track Borgia captains through the Roman ruins and silence them.',
  ),
  MissionMeta(
    id: 'story_crows_09',
    campaign: 'Italy - A Murder of Crows',
    title: 'The Shadow of Rome',
    location: 'Roma Colosseo - Nighttime',
    objective: 'Infiltrate the Borgia encampment and assassinate their inner counsel.',
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
    objective: 'Infiltrate the fortified watchtower and eliminate Borgia commanders.',
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

  GameAccessState({
    this.mode = 'fresh',
    Set<String>? unlockedMissions,
    Set<String>? completedMissions,
    Set<String>? unlockedCampaigns,
  })  : unlockedMissions = unlockedMissions ?? {'story_tutorial_01'},
        completedMissions = completedMissions ?? {},
        unlockedCampaigns = unlockedCampaigns ?? {'campaign_1'};

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
    unlockedCampaigns = {'campaign_1', 'campaign_2', 'campaign_3'};
  }

  Map<String, dynamic> toJson() => {
        'mode': mode,
        'unlockedMissions': unlockedMissions.toList(),
        'completedMissions': completedMissions.toList(),
        'unlockedCampaigns': unlockedCampaigns.toList(),
      };

  factory GameAccessState.fromJson(Map<String, dynamic> json) => GameAccessState(
        mode: json['mode'] as String? ?? 'fresh',
        unlockedMissions: (json['unlockedMissions'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {'story_tutorial_01'},
        completedMissions: (json['completedMissions'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {},
        unlockedCampaigns: (json['unlockedCampaigns'] as List<dynamic>?)
                ?.map((e) => e.toString())
                .toSet() ??
            {'campaign_1'},
      );
}

class ServerScreen extends StatefulWidget {
  const ServerScreen({super.key});

  @override
  State<ServerScreen> createState() => _ServerScreenState();
}

class _ServerScreenState extends State<ServerScreen> {
  static const _gameChannel = MethodChannel('com.acid.server/game');
  HttpServer? _server;
  bool _isRunning = false;
  final List<String> _logs = [];
  Map<String, dynamic> _catalogue = {};
  String _activeMissionId = 'story_tutorial_01';
  GameAccessState _gameState = GameAccessState();

  static const List<String> _candidateCacheDirs = [
    '/sdcard/Android/data/com.acid.server.companion_server/files/cache',
    '/sdcard/ACIdentity_Cache',
    '/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache',
  ];

  static const List<String> _candidateCustomDirs = [
    '/sdcard/Android/data/com.acid.server.companion_server/files/custom_missions',
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
    _loadGameAccessState().then((_) {
      _initAndStart();
    });
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
    for (final p in _candidateStatePaths) {
      try {
        final f = File(p);
        if (await f.exists()) {
          final text = await f.readAsString();
          final map = jsonDecode(text) as Map<String, dynamic>;
          _gameState = GameAccessState.fromJson(map);
          await _log('Loaded game access profile: ${_gameState.mode} (${_gameState.completedMissions.length} completed)');
          return;
        }
      } catch (_) {}
    }
    _gameState = GameAccessState();
    await _log('Initialized default story game access state (Tutorial 1 unlocked)');
  }

  Future<void> _saveGameAccessState() async {
    final jsonStr = const JsonEncoder.withIndent('  ').convert(_gameState.toJson());
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
          }
      },
      'entitlement': {
        'ntt_c350c141': true,
        if (_gameState.unlockedCampaigns.contains('campaign_2')) 'ntt_5572b4db': true,
        if (_gameState.unlockedCampaigns.contains('campaign_3')) 'ntt_0dd81737': true,
        if (_gameState.completedMissions.contains('story_crows_10')) ...{
          'ntt_bfc7ed30': true,
          'ntt_offline_rank1': true,
        },
        for (final mid in _gameState.completedMissions) ...{
          'ntt_story_reward_$mid': true,
          'ntt_story_challenge_$mid': true,
        }
      }
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
    int tagPos = -1;
    for (int idx = 0; idx < data.length - 1; idx++) {
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
    if (i + f32Len > data.length) return data;
    final f32Data = data.sublist(i, i + f32Len);
    final suffix = data.sublist(i + f32Len);

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
    while (k < data.length && (data[k] & 0x80) != 0) {
      k++;
    }
    k++;

    final profileBeforeF32 = data.sublist(k, tagPos);
    final newProfile = <int>[
      ...profileBeforeF32,
      ...newF32,
      ...suffix,
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

    return Uint8List.fromList([...f4Prefix, ...plVar, ...newProfile]);
  }

  Future<Uint8List?> _getActiveAuthResponse() async {
    final unlockedBytes = await _loadAsset('assets/responses/auth_unlocked.bin') ??
        await _loadAsset('assets/responses/auth.bin');
    if (unlockedBytes == null) {
      return await _loadAsset('assets/responses/auth_fresh.bin');
    }
    if (_gameState.mode == 'unlocked' &&
        _gameState.completedMissions.length >= kAllMissions.length) {
      return unlockedBytes;
    }
    return _filterAuthBytes(
      unlockedBytes,
      _gameState.completedMissions,
      _gameState.unlockedCampaigns,
    );
  }

  Future<void> _initAndStart() async {
    try {
      final catBytes = await _loadAsset('assets/responses/catalogue.json');
      if (catBytes != null) {
        final catJson = utf8.decode(catBytes);
        _catalogue = jsonDecode(catJson) as Map<String, dynamic>;
        await _log('Catalogue loaded: ${_catalogue.length} CDN entries indexed');
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
    if (!_isRunning || _server == null) return;
    await _server!.close(force: true);
    _server = null;
    setState(() => _isRunning = false);
    await _log('Server stopped');
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
        _activeMissionId = mid;
        reqType = 'GetMissionInfo($mid)';
        respAsset = 'assets/responses/getMissionInfo_$mid.bin';
      } else if (_containsSequence(bodyBytes, [0xC2, 0x0C])) {
        String mid = 'default';
        for (final m in allMissionIds) {
          if (bodyStr.contains(m)) {
            mid = m;
            break;
          }
        }
        _activeMissionId = mid;
        reqType = 'OpenMission($mid)';

        // Check if custom uploaded mission exists on device storage
        for (final customDir in _candidateCustomDirs) {
          final customBin = File('$customDir/openMission_$mid.bin');
          if (await customBin.exists()) {
            try {
              final customBytes = await customBin.readAsBytes();
              request.response
                ..statusCode = HttpStatus.ok
                ..headers.contentType = ContentType('application', 'x-protobuf')
                ..headers.contentLength = customBytes.length
                ..add(customBytes);
              await request.response.close();
              await _log('$method OpenMission($mid) -> Served Custom File (${customBytes.length}b)');
              return;
            } catch (_) {}
          }
        }

        respAsset = 'assets/responses/openMission_$mid.bin';
      } else if (_containsSequence(bodyBytes, [0xB2, 0x06]) ||
          _containsSequence(bodyBytes, [0xA2, 0x06]) ||
          bodyStr.contains('assassin') ||
          bodyStr.contains('auth')) {
        reqType = 'AuthAccount';
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
        if (respBytes == null && reqType.startsWith('EndMission')) {
          respBytes = await _loadAsset('assets/responses/endMission.bin');
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

    final fileId = entry['id'] as String? ?? entry['relative_path'] as String? ?? relative;
    final zipped = entry['zipped'] as bool? ?? false;

    File? foundFile;
    for (final dir in _candidateCacheDirs) {
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
    await _log('CDN 200: $relative (${bytes.length}b${zipped ? ", zlib" : ""})');
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
      final isInstalled = await _gameChannel.invokeMethod<bool>('isGameInstalled') ?? false;
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
      final launched = await _gameChannel.invokeMethod<bool>('openGame') ?? false;
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
              child: const Icon(
                Icons.hub,
                color: Color(0xFF00E5FF),
                size: 20,
              ),
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
                              color: (_isRunning
                                      ? const Color(0xFF00E676)
                                      : const Color(0xFFFF5252))
                                  .withOpacity(0.5),
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
                              color: Colors.white.withOpacity(0.6),
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
                      activeColor: const Color(0xFF00E676),
                      activeTrackColor: const Color(0xFF00E676).withOpacity(0.3),
                      inactiveThumbColor: const Color(0xFFFF5252),
                      inactiveTrackColor: const Color(0xFFFF5252).withOpacity(0.3),
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
            padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 12.0),
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
                      style: TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
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
                            color: Colors.white.withOpacity(0.7),
                          ),
                        ),
                      ],
                    ),
                    style: OutlinedButton.styleFrom(
                      side: const BorderSide(color: Color(0xFF00E5FF), width: 1.5),
                      padding: const EdgeInsets.symmetric(vertical: 10, horizontal: 8),
                      shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(8),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),

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
              margin: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 6.0),
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
                        if (log.contains('200 OK') || log.contains('online') || log.contains('completed')) {
                          color = const Color(0xFF81C784);
                        } else if (log.contains('404') || log.contains('error') || log.contains('Failed')) {
                          color = const Color(0xFFE57373);
                        } else if (log.contains('OpenMission') || log.contains('EndMission')) {
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

class _GameAccessScreenState extends State<GameAccessScreen> {
  String _selectedCampaignFilter = 'All';

  void _resetAllMissions() {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Reset All Level Progress?'),
        content: const Text(
          'This will lock all campaigns and reset story progress back to initial default (only Tutorial 1 unlocked).\n\nAll missions will be playable in natural story progression.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            onPressed: () async {
              Navigator.of(ctx).pop();
              setState(() {
                widget.gameState.resetAll();
              });
              await widget.onStateChanged(widget.gameState);
              try {
                await widget.gameChannel.invokeMethod('deleteGameLocalStorage');
              } catch (_) {}
              await widget.onLog('Game progress reset: Only Tutorial 1 is unlocked.');
              if (mounted) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Level progress reset! Only Tutorial 1 unlocked.'),
                    backgroundColor: Colors.green,
                  ),
                );
              }
            },
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFFF5252)),
            child: const Text('Reset All'),
          ),
        ],
      ),
    );
  }

  void _unlockAllMissions() async {
    setState(() {
      widget.gameState.unlockAll();
    });
    await widget.onStateChanged(widget.gameState);
    await widget.onLog('Game progress unlocked: All 24 missions and campaigns unlocked.');
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('All campaigns and 24 missions unlocked!'),
          backgroundColor: Colors.green,
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

  void _openUploadMissionDialog() {
    final missionIdController = TextEditingController(text: 'custom_mission_01');
    final titleController = TextEditingController();
    final objController = TextEditingController(text: 'Eliminate the designated targets.');
    String selectedRegion = 'Firenze_Palazzo';
    String selectedMood = 'Daytime';

    showDialog(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (context, setDlgState) => AlertDialog(
          title: const Text('Upload / Register Custom Mission'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: missionIdController,
                  decoration: const InputDecoration(
                    labelText: 'Mission ID',
                    hintText: 'e.g. custom_mission_01',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: titleController,
                  decoration: const InputDecoration(
                    labelText: 'Mission Title',
                    hintText: 'e.g. Shadows of Florence',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  value: selectedRegion,
                  decoration: const InputDecoration(
                    labelText: 'Location / Region',
                    border: OutlineInputBorder(),
                  ),
                  items: const [
                    DropdownMenuItem(value: 'Firenze_Palazzo', child: Text('Firenze Palazzo')),
                    DropdownMenuItem(value: 'Firenze_SantaCroce', child: Text('Firenze Santa Croce')),
                    DropdownMenuItem(value: 'Roma_Coloseum', child: Text('Roma Colosseo')),
                    DropdownMenuItem(value: 'Monteriggioni', child: Text('Monteriggioni')),
                    DropdownMenuItem(value: 'Forli', child: Text('Forlì')),
                  ],
                  onChanged: (val) {
                    if (val != null) setDlgState(() => selectedRegion = val);
                  },
                ),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  value: selectedMood,
                  decoration: const InputDecoration(
                    labelText: 'Time of Day / Atmosphere',
                    border: OutlineInputBorder(),
                  ),
                  items: const [
                    DropdownMenuItem(value: 'Daytime', child: Text('Daytime')),
                    DropdownMenuItem(value: 'Nighttime', child: Text('Nighttime')),
                    DropdownMenuItem(value: 'Dusk', child: Text('Dusk')),
                    DropdownMenuItem(value: 'Foggy', child: Text('Foggy')),
                    DropdownMenuItem(value: 'Sunny', child: Text('Sunny')),
                  ],
                  onChanged: (val) {
                    if (val != null) setDlgState(() => selectedMood = val);
                  },
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: objController,
                  maxLines: 2,
                  decoration: const InputDecoration(
                    labelText: 'Primary Objective',
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text('Cancel'),
            ),
            ElevatedButton(
              onPressed: () async {
                final mid = missionIdController.text.trim();
                final title = titleController.text.trim().isEmpty ? 'Custom Mission' : titleController.text.trim();
                final obj = objController.text.trim();

                final customDef = {
                  'id': mid,
                  'title': title,
                  'region': selectedRegion,
                  'mood': selectedMood,
                  'objective': obj,
                  'timestamp': DateTime.now().toIso8601String(),
                };

                final jsonStr = jsonEncode(customDef);
                await widget.gameChannel.invokeMethod('saveCustomMission', {
                  'filename': '$mid.json',
                  'text': jsonStr,
                });

                setState(() {
                  widget.gameState.mode = 'custom';
                  widget.gameState.unlockedMissions.add(mid);
                });
                await widget.onStateChanged(widget.gameState);

                if (mounted) {
                  Navigator.of(ctx).pop();
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('Registered custom mission "$title"!'),
                      backgroundColor: Colors.green,
                    ),
                  );
                }
              },
              style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFF00E5FF), foregroundColor: Colors.black),
              child: const Text('Save & Register'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final filteredMissions = _selectedCampaignFilter == 'All'
        ? kAllMissions
        : kAllMissions.where((m) => m.campaign.contains(_selectedCampaignFilter)).toList();

    final unlockedCount = widget.gameState.unlockedMissions.length;
    final completedCount = widget.gameState.completedMissions.length;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Game Access & Mission Manager'),
        backgroundColor: const Color(0xFF101722),
        actions: [
          IconButton(
            tooltip: 'Upload Custom Mission',
            icon: const Icon(Icons.file_upload, color: Color(0xFF00E5FF)),
            onPressed: _openUploadMissionDialog,
          ),
        ],
      ),
      body: Column(
        children: [
          // Mode Status Banner
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            color: const Color(0xFF162130),
            child: Row(
              children: [
                Icon(
                  widget.gameState.mode == 'unlocked'
                      ? Icons.lock_open
                      : (widget.gameState.mode == 'fresh' ? Icons.timeline : Icons.tune),
                  color: const Color(0xFF00E5FF),
                  size: 20,
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    widget.gameState.mode == 'unlocked'
                        ? 'MODE: SANDBOX (ALL MISSIONS UNLOCKED)'
                        : (widget.gameState.mode == 'fresh'
                            ? 'MODE: STORY PROGRESSION (NATURAL UNLOCKS)'
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
                        DropdownMenuItem(value: 'All', child: Text('All Campaigns (24 Missions)')),
                        DropdownMenuItem(value: 'Montreal', child: Text('Montreal - VR Training')),
                        DropdownMenuItem(value: 'Murder of Crows', child: Text('Italy - A Murder of Crows')),
                        DropdownMenuItem(value: 'Crimson Sunset', child: Text('Forlì - A Crimson Sunset')),
                        DropdownMenuItem(value: 'Bonus Contracts', child: Text('Bonus Contracts')),
                      ],
                      onChanged: (val) {
                        if (val != null) setState(() => _selectedCampaignFilter = val);
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
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                  ),
                  child: const Text('Unlock All', style: TextStyle(fontSize: 11)),
                ),
                const SizedBox(width: 6),
                ElevatedButton(
                  onPressed: _resetAllMissions,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFE53935),
                    foregroundColor: Colors.white,
                    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
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
              itemCount: filteredMissions.length,
              itemBuilder: (ctx, idx) {
                final m = filteredMissions[idx];
                final isUnlocked = widget.gameState.isUnlocked(m.id);
                final isCompleted = widget.gameState.isCompleted(m.id);

                return Card(
                  margin: const EdgeInsets.symmetric(vertical: 6),
                  color: isCompleted
                      ? const Color(0xFF0F231D)
                      : (isUnlocked ? const Color(0xFF121B27) : const Color(0xFF0A0F15)),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(8),
                    side: BorderSide(
                      color: isCompleted
                          ? const Color(0xFF00E676).withOpacity(0.5)
                          : (isUnlocked
                              ? const Color(0xFF00E5FF).withOpacity(0.4)
                              : Colors.white10),
                      width: 1,
                    ),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Container(
                              margin: const EdgeInsets.only(top: 2, right: 8),
                              child: isCompleted
                                  ? const Icon(Icons.check_circle, color: Color(0xFF00E676), size: 18)
                                  : Icon(
                                      isUnlocked ? Icons.play_circle_outline : Icons.lock_outline,
                                      color: isUnlocked ? const Color(0xFF00E5FF) : Colors.white30,
                                      size: 18,
                                    ),
                            ),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    m.title,
                                    style: TextStyle(
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                      color: isUnlocked ? Colors.white : Colors.white54,
                                    ),
                                  ),
                                  Text(
                                    '${m.campaign} • ${m.location}',
                                    style: const TextStyle(fontSize: 11, color: Colors.white54),
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
                            color: Colors.white.withOpacity(0.75),
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
                                    color: isUnlocked ? Colors.white70 : Colors.white38,
                                  ),
                                ),
                                Switch(
                                  value: isUnlocked,
                                  activeColor: const Color(0xFF00E5FF),
                                  onChanged: (val) => _toggleMissionUnlocked(m.id, val),
                                ),
                              ],
                            ),
                            Row(
                              children: [
                                Text(
                                  'Completed:',
                                  style: TextStyle(
                                    fontSize: 12,
                                    color: isCompleted ? const Color(0xFF00E676) : Colors.white38,
                                  ),
                                ),
                                Switch(
                                  value: isCompleted,
                                  activeColor: const Color(0xFF00E676),
                                  onChanged: (val) => _toggleMissionCompleted(m.id, val),
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
      ),
    );
  }
}
