import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;

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
      theme: ThemeData.dark(useMaterial3: true).copyWith(
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFFC02A2A),
          brightness: Brightness.dark,
        ),
      ),
      home: const ServerScreen(),
    );
  }
}

class ServerScreen extends StatefulWidget {
  const ServerScreen({super.key});

  @override
  State<ServerScreen> createState() => _ServerScreenState();
}

class _ServerScreenState extends State<ServerScreen> {
  HttpServer? _server;
  bool _isRunning = false;
  final List<String> _logs = [];
  Map<String, dynamic> _catalogue = {};
  final String _cacheDir =
      '/sdcard/Android/data/com.ubisoft.assassinscreed.identity/files/cache';

  @override
  void initState() {
    super.initState();
    _initAndStart();
  }

  Future<void> _log(String message) async {
    final time = DateTime.now().toIso8601String().substring(11, 19);
    setState(() {
      _logs.insert(0, '[$time] $message');
      if (_logs.length > 100) _logs.removeLast();
    });
  }

  Future<void> _initAndStart() async {
    try {
      final catStr =
          await rootBundle.loadString('assets/responses/catalogue.json');
      _catalogue = jsonDecode(catStr) as Map<String, dynamic>;
      await _log('Loaded catalogue with ${_catalogue.length} entries');
      await _startServer();
    } catch (e) {
      await _log('Init error: $e');
    }
  }

  Future<void> _startServer() async {
    if (_isRunning) return;
    try {
      _server = await HttpServer.bind(InternetAddress.anyIPv4, 8080);
      setState(() => _isRunning = true);
      await _log('Server listening on http://127.0.0.1:8080/');

      _server!.listen((HttpRequest request) async {
        await _handleRequest(request);
      });
    } catch (e) {
      await _log('Failed to start server: $e');
    }
  }

  Future<void> _stopServer() async {
    if (!_isRunning) return;
    await _server?.close(force: true);
    setState(() => _isRunning = false);
    await _log('Server stopped');
  }

  Future<Uint8List?> _loadAsset(String path) async {
    try {
      final bd = await rootBundle.load(path);
      return bd.buffer.asUint8List();
    } catch (e) {
      return null;
    }
  }

  bool _containsSequence(List<int> bytes, List<int> pattern) {
    if (pattern.isEmpty || bytes.length < pattern.length) return false;
    for (int i = 0; i <= bytes.length - pattern.length; i++) {
      bool match = true;
      for (int j = 0; j < pattern.length; j++) {
        if (bytes[i + j] != pattern[j]) {
          match = false;
          break;
        }
      }
      if (match) return true;
    }
    return false;
  }

  Future<void> _handleRequest(HttpRequest request) async {
    final path = Uri.decodeComponent(request.uri.path);
    final method = request.method;

    try {
      // 1. Discovery
      if (path.contains('/discovery')) {
        final bin = await _loadAsset('assets/responses/discovery.bin');
        if (bin != null) {
          request.response
            ..statusCode = HttpStatus.ok
            ..headers.contentType = ContentType('application', 'x-protobuf')
            ..headers.contentLength = bin.length
            ..add(bin);
          await request.response.close();
          await _log('$method $path -> Discovery (200 OK)');
          return;
        }
      }

      // 2. CDN Static Cache Assets
      const cdnPrefix = '/cdn/ACID_2.8.2_373/';
      if (path.startsWith(cdnPrefix)) {
        final rel = path.substring(cdnPrefix.length);
        await _serveCdnFile(request, rel);
        return;
      }

      // 3. API Requests
      if (path.contains('/api')) {
        final bodyBytes = await request.fold<List<int>>(
            [], (prev, elem) => prev..addAll(elem));
        final bodyStr = utf8.decode(bodyBytes, allowMalformed: true);

        String? respAsset;
        String reqType = 'Unknown';

        final allMissions = [
          'story_tutorial_01', 'story_tutorial_02', 'story_tutorial_03',
          'story_tutorial_04', 'story_tutorial_05', 'story_tutorial_06', 'story_tutorial_07',
          'story_crows_01', 'story_crows_02', 'story_crows_03', 'story_crows_04', 'story_crows_05',
          'story_crows_06', 'story_crows_07', 'story_crows_08', 'story_crows_09', 'story_crows_10',
          'story_forli_01', 'story_forli_02', 'story_forli_03', 'story_forli_04',
          'story_0015', 'story_0016', 'story_0017'
        ];

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
          reqType = 'EndMission';
          respAsset = 'assets/responses/endMission.bin';
        } else if (_containsSequence(bodyBytes, [0xD2, 0x0C])) {
          String mid = 'default';
          for (final m in allMissions) {
            if (bodyStr.contains(m)) {
              mid = m;
              break;
            }
          }
          reqType = 'GetMissionInfo($mid)';
          respAsset = 'assets/responses/getMissionInfo_$mid.bin';
        } else if (_containsSequence(bodyBytes, [0xC2, 0x0C])) {
          String mid = 'default';
          for (final m in allMissions) {
            if (bodyStr.contains(m)) {
              mid = m;
              break;
            }
          }
          reqType = 'OpenMission($mid)';
          respAsset = 'assets/responses/openMission_$mid.bin';
        } else if (_containsSequence(bodyBytes, [0xB2, 0x06]) ||
                   _containsSequence(bodyBytes, [0xA2, 0x06]) ||
                   bodyStr.contains('assassin') ||
                   bodyStr.contains('auth')) {
          reqType = 'AuthAccount';
          respAsset = 'assets/responses/auth.bin';
        }

        respAsset ??= 'assets/responses/auth.bin';
        var respBytes = await _loadAsset(respAsset);
        respBytes ??= await _loadAsset('assets/responses/auth.bin');

        if (respBytes != null) {
          request.response
            ..statusCode = HttpStatus.ok
            ..headers.contentType = ContentType('application', 'x-protobuf')
            ..headers.contentLength = respBytes.length
            ..add(respBytes);
          await request.response.close();
          await _log('$method $path -> $reqType (${respBytes.length}b)');
          return;
        }
      }

      // Default fallback
      request.response
        ..statusCode = HttpStatus.notFound
        ..write('Not Found');
      await request.response.close();
      await _log('404 Not Found: $path');
    } catch (e) {
      await _log('Request error: $e');
      try {
        request.response.statusCode = HttpStatus.internalServerError;
        await request.response.close();
      } catch (_) {}
    }
  }

  Future<void> _serveCdnFile(HttpRequest request, String relative) async {
    // 1. Check bootstrap assets first
    if (relative == 'FirstContact_Android.xml') {
      final b = await _loadAsset('assets/bootstrap/FirstContact_Android.xml');
      if (b != null) {
        request.response
          ..statusCode = HttpStatus.ok
          ..headers.contentType = ContentType('application', 'octet-stream')
          ..headers.contentLength = b.length
          ..add(b);
        await request.response.close();
        await _log('Served bootstrap FirstContact_Android.xml');
        return;
      }
    }
    if (relative == 'GameDB_AndroidETC2.bin') {
      final b = await _loadAsset('assets/bootstrap/GameDB_AndroidETC2.bin');
      if (b != null) {
        request.response
          ..statusCode = HttpStatus.ok
          ..headers.contentType = ContentType('application', 'octet-stream')
          ..headers.contentLength = b.length
          ..add(b);
        await request.response.close();
        await _log('Served bootstrap GameDB_AndroidETC2.bin');
        return;
      }
    }

    // 2. Lookup in catalogue
    final entry = _catalogue[relative] as Map<String, dynamic>?;
    if (entry == null) {
      request.response
        ..statusCode = HttpStatus.notFound
        ..write('Asset Not In Catalogue');
      await request.response.close();
      await _log('Missing asset: $relative');
      return;
    }

    final id = entry['id'] as String;
    final zipped = entry['zipped'] as bool;
    final cacheFile = File('$_cacheDir/$id');

    if (!await cacheFile.exists()) {
      request.response
        ..statusCode = HttpStatus.notFound
        ..write('Cache File Missing On Device');
      await request.response.close();
      await _log('Cache file missing: $id ($relative)');
      return;
    }

    var bytes = await cacheFile.readAsBytes();
    if (zipped) {
      bytes = Uint8List.fromList(zlib.decode(bytes));
    }

    request.response
      ..statusCode = HttpStatus.ok
      ..headers.contentType = ContentType('application', 'octet-stream')
      ..headers.contentLength = bytes.length;

    if (request.method != 'HEAD') {
      request.response.add(bytes);
    }
    await request.response.close();
    await _log('Served CDN asset: $relative (${bytes.length}b)');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('AC Identity Server'),
        centerTitle: true,
        actions: [
          IconButton(
            icon: Icon(_isRunning ? Icons.stop : Icons.play_arrow),
            color: _isRunning ? Colors.green : Colors.red,
            onPressed: () {
              if (_isRunning) {
                _stopServer();
              } else {
                _startServer();
              }
            },
          )
        ],
      ),
      body: Column(
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            color: _isRunning
                ? Colors.green.withOpacity(0.2)
                : Colors.red.withOpacity(0.2),
            child: Row(
              children: [
                Icon(
                  _isRunning ? Icons.check_circle : Icons.error,
                  color: _isRunning ? Colors.green : Colors.red,
                  size: 32,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        _isRunning ? 'SERVER RUNNING' : 'SERVER STOPPED',
                        style: const TextStyle(
                          fontWeight: FontWeight.bold,
                          fontSize: 16,
                        ),
                      ),
                      Text(
                        _isRunning
                            ? 'http://127.0.0.1:8080 (Localhost Active)'
                            : 'Tap play to start server',
                        style: TextStyle(color: Colors.grey.shade400),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(12.0),
            child: ElevatedButton.icon(
              onPressed: () {
                _log('Launch game: Open AC Identity on your phone');
              },
              icon: const Icon(Icons.videogame_asset),
              label: const Text('Open Assassin\'s Creed Identity'),
              style: ElevatedButton.styleFrom(
                minimumSize: const Size.fromHeight(48),
                backgroundColor: const Color(0xFFC02A2A),
                foregroundColor: Colors.white,
              ),
            ),
          ),
          const Divider(),
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: 16, vertical: 4),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Text(
                'Live Server Activity',
                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
              ),
            ),
          ),
          Expanded(
            child: Container(
              margin: const EdgeInsets.all(8),
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: Colors.black54,
                borderRadius: BorderRadius.circular(8),
              ),
              child: ListView.builder(
                itemCount: _logs.length,
                itemBuilder: (context, index) {
                  return Text(
                    _logs[index],
                    style: const TextStyle(
                      fontFamily: 'monospace',
                      fontSize: 12,
                      color: Colors.white70,
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
