using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AcierOffline;

internal static class Program
{
    private const string CdnRoot = "/cdn/ACID_2.8.2_373/";
    private const string CataloguePrefix = "http://static-ww.cdn.ubi.com/0040/assets/ACID_2.8.2_373/";
    // Must match the discovery host patched into the APK.
    private static string Host = "192.168.1.2";
    private static string CdnUrl = "";
    private static string ApiUrl = "";

    private static Assembly _protocol;
    private static object _serializer;
    private static MethodInfo _serialize;
    private static MethodInfo _deserialize;
    private static readonly Dictionary<string, CacheEntry> Catalogue = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
    private static string _cacheDir;
    private static string _saveDir;
    private static object _profile;
    private static string _sessionToken = "offline-session";
    private static string _activeMissionId = "story_0015";
    private static string _activeCampaignId = "ntt_0dd81737";
    private static int _contractCounter = 1;

    private static int Main(string[] args)
    {
        string root = FindProjectRoot();
        _cacheDir = Path.Combine(root, "Resorces", "com.ubisoft.assassinscreed.identity", "files", "cache");
        _saveDir = Path.Combine(root, "offline", "save");
        int port = 8080;
        bool selfTest = false;
        string exportDir = null;
        string buildMissionJson = null;
        string buildMissionOut = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cache" && i + 1 < args.Length) _cacheDir = args[++i];
            else if (args[i] == "--save" && i + 1 < args.Length) _saveDir = args[++i];
            else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
            else if (args[i] == "--host" && i + 1 < args.Length) Host = args[++i];
            else if (args[i] == "--self-test") selfTest = true;
            else if (args[i] == "--export-companion" && i + 1 < args.Length) exportDir = args[++i];
            else if (args[i] == "--build-mission" && i + 1 < args.Length)
            {
                buildMissionJson = args[++i];
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    buildMissionOut = args[++i];
                }
            }
        }
        CdnUrl = "http://" + Host + ":" + port + "/cdn/ACID_2.8.2_373";
        ApiUrl = "http://" + Host + ":" + port + "/acier/v1/api";

        LoadProtocol(root);
        LoadCatalogue();
        Directory.CreateDirectory(_saveDir);
        LoadOrCreateProfile();
        RepairProfile();
        Statics();

        if (!string.IsNullOrEmpty(buildMissionJson))
        {
            string outDir = buildMissionOut ?? Path.GetDirectoryName(buildMissionJson);
            if (string.IsNullOrEmpty(outDir)) outDir = ".";
            BuildCustomMissionFromJson(buildMissionJson, outDir);
            return 0;
        }

        if (!string.IsNullOrEmpty(exportDir))
        {
            ExportCompanion(exportDir);
            Console.WriteLine("Companion export completed to: " + exportDir);
            return 0;
        }

        if (selfTest)
        {
            SelfTest();
            Console.WriteLine("self-test ok, catalogue files " + Catalogue.Count);
            return 0;
        }

        var listener = new HttpListener();
        listener.Prefixes.Add("http://+:" + port + "/");
        listener.Start();
        Console.WriteLine("Assassin's Creed Identity offline server on http://" + Host + ":" + port + "/");
        Console.WriteLine("Phone discovery URL must be http://" + Host + ":" + port + "/discovery/acier/v1/x");
        Console.WriteLine("Serving " + Catalogue.Count + " cached files from " + _cacheDir);
        Console.WriteLine("The phone must be on the same Wi-Fi as this computer.");
        while (true)
        {
            HttpListenerContext context = listener.GetContext();
            ThreadPool.QueueUserWorkItem(Handle, context);
        }
    }

    private static void Handle(object state)
    {
        var context = (HttpListenerContext)state;
        try
        {
            string path = Uri.UnescapeDataString(context.Request.Url.AbsolutePath);
            if (path.StartsWith(CdnRoot, StringComparison.Ordinal))
            {
                ServeFile(context, path.Substring(CdnRoot.Length));
                return;
            }

            byte[] body;
            using (var ms = new MemoryStream())
            {
                context.Request.InputStream.CopyTo(ms);
                body = ms.ToArray();
            }
            object request = _deserialize.Invoke(_serializer, new object[] { new MemoryStream(body), null, TypeOf("AcierProtocol.Request") });
            string kind = NestedRequest(request);
            Console.WriteLine(context.Request.HttpMethod + " " + path + " " + body.Length + " bytes " + kind);
            object response = BuildResponse(request, kind);
            var output = new MemoryStream();
            _serialize.Invoke(_serializer, new object[] { output, response });
            byte[] payload = output.ToArray();
            Console.WriteLine("response " + payload.Length + " bytes");
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/x-protobuf";
            context.Response.ContentLength64 = payload.Length;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine("request failed: " + ex);
            try { context.Response.StatusCode = 500; } catch { }
        }
        finally
        {
            try { context.Response.OutputStream.Close(); } catch { }
        }
    }

    private static void ServeFile(HttpListenerContext context, string relative)
    {
        if (!Catalogue.TryGetValue(relative, out CacheEntry entry))
        {
            string fallback = relative.Replace("AndroidDXT", "AndroidETC2").Replace("AndroidGeneric", "AndroidETC2");
            if (!Catalogue.TryGetValue(fallback, out entry))
            {
                Console.WriteLine("missing asset " + relative);
                context.Response.StatusCode = 404;
                context.Response.OutputStream.Close();
                return;
            }
            Console.WriteLine("falling back asset " + relative + " -> " + fallback);
        }
        byte[] bytes = File.ReadAllBytes(Path.Combine(_cacheDir, entry.Id));
        if (entry.Zipped)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(bytes, 0, bytes.Length);
            }
            bytes = output.ToArray();
        }
        string range = context.Request.Headers["Range"];
        Console.WriteLine(context.Request.HttpMethod + " " + relative + " " + bytes.Length
            + (entry.Zipped ? " zlib" : "")
            + (string.IsNullOrEmpty(range) ? "" : " range=" + range));
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/octet-stream";
        context.Response.SendChunked = false;
        context.Response.ContentLength64 = bytes.Length;
        if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }
        context.Response.OutputStream.Close();
    }

    private static object BuildResponse(object request, string kind)
    {
        object response = New("AcierProtocol.Response");
        Set(response, "serverTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Set(response, "requestToken", "offline");
        Set(response, "sessionToken", _sessionToken);

        switch (kind)
        {
            case "discoveryServiceRequest":
                Set(response, "discoveryServiceResponse", Discovery());
                break;
            case "checkVersionRequest":
                object version = New("AcierProtocol.CheckVersionResponse");
                Set(version, "needClientUpdate", false);
                Set(version, "serverVersion", 59u);
                Set(response, "checkVersionResponse", version);
                break;
            case "registerUserRequest":
            case "authAccountRequest":
                string identity = StringProp(Get(request, kind), "username");
                if (string.IsNullOrEmpty(identity)) identity = "assassin";
                object account = New(kind == "registerUserRequest" ? "AcierProtocol.RegisterUserResponse" : "AcierProtocol.AuthAccountResponse");
                Set(account, "identity", identity);
                Set(response, kind.Replace("Request", "Response"), account);
                Set(response, "userProfile", _profile);
                Set(response, "userProfileHash", "offline");
                SaveProfile();
                break;
            case "getStaticsRequest":
                object staticsResponse = New("AcierProtocol.GetStaticsResponse");
                Set(staticsResponse, "statics", Statics());
                Set(response, "getStaticsResponse", staticsResponse);
                Set(response, "userProfile", _profile);
                break;
            case "getQualitySettingsRequest":
                object quality = New("AcierProtocol.GetQualitySettingsResponse");
                Set(quality, "qualitySettingsName", "Low");
                Set(quality, "qualitySettings", Quality());
                Set(response, "getQualitySettingsResponse", quality);
                break;
            case "getTwitterTimelineRequest":
                Set(response, "getTwitterTimelineResponse", New("AcierProtocol.GetTwitterTimelineResponse"));
                break;
            case "openMissionRequest":
                object opened = New("AcierProtocol.OpenMissionResponse");
                object ticket = New("AcierProtocol.MissionToken");
                Set(ticket, "token", "offline-mission");
                object openReq = Get(request, "openMissionRequest");
                string reqMissionId = StringProp(openReq, "missionId");
                if (string.IsNullOrEmpty(reqMissionId)) reqMissionId = "story_0015";
                string reqCampaignId = StringProp(openReq, "campaignId");
                int reqRank = 1;
                object rankObj = Get(openReq, "rank");
                if (rankObj != null) reqRank = (int)rankObj;

                _activeMissionId = reqMissionId;
                _activeCampaignId = reqCampaignId;
                Console.WriteLine($"openMissionRequest: missionId={reqMissionId}, campaign={reqCampaignId}, rank={reqRank}");

                var (file, goals) = BuildMissionFileAndGoals(reqMissionId, reqRank);
                Set(ticket, "missionId", reqMissionId);
                Set(ticket, "assassinId", "acid-offline-assassin");
                Set(ticket, "hirelingId", "hireling-1");
                Set(ticket, "missionFile", file);
                Set(ticket, "missionGoals", goals);
                Set(ticket, "eliteMission", false);
                Set(opened, "missionToken", ticket);
                Set(response, "openMissionResponse", opened);
                break;
            case "endMissionRequest":
                object endReq = Get(request, "endMissionRequest");
                bool won = endReq != null && (bool)Get(endReq, "won");
                string activeMid = _activeMissionId ?? "story_0015";
                Console.WriteLine($"endMissionRequest: won={won}, activeMission={activeMid}");

                object ended = New("AcierProtocol.EndMissionResponse");
                object endReward = MissionReward(500L, 1000L);
                if (won)
                {
                    GrantStoryCompletion(activeMid);
                    AddProfileReward(500, 1000);
                    SaveProfile();
                    Set(endReward, "entitlements", new string[] { $"ntt_story_reward_{activeMid}", $"ntt_story_challenge_{activeMid}" });
                }
                Set(ended, "missionReward", endReward);
                Set(response, "endMissionResponse", ended);
                Set(response, "userProfile", _profile);
                break;
            case "removeHirelingAndMissionRequest":
                object rhmReq = Get(request, "removeHirelingAndMissionRequest");
                string rhmMid = StringProp(rhmReq, "missionId");
                int rhmRank = 0;
                object rhmRankObj = Get(rhmReq, "rankIdx");
                if (rhmRankObj != null) rhmRank = (int)rhmRankObj;
                Console.WriteLine($"removeHirelingAndMissionRequest: missionId={rhmMid}, rank={rhmRank}");

                RotateContractMission(rhmMid, rhmRank);
                SaveProfile();

                object rhmResp = New("AcierProtocol.RemoveHirelingAndMissionResponse");
                Set(response, "removeHirelingAndMissionResponse", rhmResp);
                Set(response, "userProfile", _profile);
                break;
            case "getMissionInfoRequest":
                object missionInfo = New("AcierProtocol.GetMissionInfoResponse");
                object gmiReq = Get(request, "getMissionInfoRequest");
                string gmiMid = StringProp(gmiReq, "missionId");
                int gmiRank = 1;
                object gmiRankObj = Get(gmiReq, "rank");
                if (gmiRankObj != null) gmiRank = (int)gmiRankObj;
                Console.WriteLine($"getMissionInfoRequest: missionId={gmiMid}, rank={gmiRank}");

                Set(missionInfo, "missionRewardPreview", MissionReward(500L, 1000L));
                var (_, gmiGoals) = BuildMissionFileAndGoals(gmiMid, gmiRank);
                Set(missionInfo, "missionGoals", gmiGoals);
                Set(response, "getMissionInfoResponse", missionInfo);
                break;
            default:
                if (kind != null && kind.EndsWith("Request", StringComparison.Ordinal))
                {
                    string responseName = "AcierProtocol." + char.ToUpperInvariant(kind[0]) + kind.Substring(1).Replace("Request", "Response");
                    // Property names are camelCase and match the response property, not the type name.
                    string property = kind.Substring(0, kind.Length - "Request".Length) + "Response";
                    Type responseType = _protocol.GetType("AcierProtocol." + char.ToUpperInvariant(property[0]) + property.Substring(1));
                    if (responseType == null)
                    {
                        responseType = FindResponseType(property);
                    }
                    if (responseType != null)
                    {
                        Set(response, property, Activator.CreateInstance(responseType));
                    }
                    else
                    {
                        Console.WriteLine("no response type for " + kind + " (" + responseName + ")");
                    }
                }
                if (_profile != null) Set(response, "userProfile", _profile);
                break;
        }
        return response;
    }

    private static object Discovery()
    {
        object discovery = New("AcierProtocol.DiscoveryServiceResponse");
        Set(discovery, "cdnUrl", CdnUrl);
        Set(discovery, "serverUrl", ApiUrl);
        Set(discovery, "userGeoIPCountry", "US");
        Set(discovery, "qualitySettingsName", "Low");
        Set(discovery, "qualitySettings", Quality());
        Set(discovery, "showLowEndDeviceWarning", false);
        Set(discovery, "fbEnabled", false);
        Set(discovery, "fbAchievementsEnabled", false);
        Set(discovery, "supportEnabled", false);
        Set(discovery, "adColonyEnabled", false);
        Set(discovery, "uplayEnabled", false);
        Set(discovery, "weiboEnabled", false);
        Set(discovery, "everplayEnabled", false);
        return discovery;
    }

    private static object Quality()
    {
        object quality = New("AcierProtocol.QualitySettings");
        Set(quality, "assetBundleQuality", Enum.Parse(_protocol.GetType("AcierProtocol.EQualityAssetBundle"), "Low"));
        Type texture = _protocol.GetType("AcierProtocol.EQualityTextureType");
        if (texture != null && Enum.IsDefined(texture, "ETC2"))
        {
            Set(quality, "textureType", Enum.Parse(texture, "ETC2"));
        }
        Set(quality, "qualityLevel", 2);
        Type shaderLod = _protocol.GetType("AcierProtocol.EQualityShaderLOD");
        if (shaderLod != null && Enum.IsDefined(shaderLod, "Maximum_500"))
        {
            Set(quality, "renderingLOD", Enum.Parse(shaderLod, "Maximum_500"));
        }
        Set(quality, "crowdQuality", 0.5f);
        Set(quality, "downScaling", false);
        return quality;
    }

    private static object Statics()
    {
        object statics = New("AcierProtocol.Statics");
        Fill(statics, 1);
        Set(statics, "staticsVersion", 1L);
        object globals = Get(statics, "globals");
        if (globals != null)
        {
            Set(globals, "initialInventorySize", 40);
            Set(globals, "initialSkillpoints", 1);
            Set(globals, "initialSoftCurrency", 5000);
            Set(globals, "numHirelings", 1);
            Set(globals, "maxAssassinLevel", 50);
            Set(globals, "maxSkillLevel", 10);
            foreach (PropertyInfo property in globals.GetType().GetProperties())
            {
                if (property.PropertyType.Name.IndexOf("Energy", StringComparison.Ordinal) < 0 || !property.CanWrite) continue;
                object energy = Activator.CreateInstance(property.PropertyType);
                Set(energy, "baseMaxEnergy", 10u);
                Set(energy, "initalMaxEnergy", 10u);
                Set(energy, "energyIncrementStep", 1);
                Set(energy, "missionEnergyCost", 1);
                Set(energy, "timeIncrementStepInSeconds", 300ul);
                Set(energy, "minimumAllowedEnergy", 0);
                property.SetValue(globals, energy);
            }
        }
        RepairTutorialMapping(statics);
        RepairCampaignCatalog(statics);
        RepairHeroicRanks(statics);
        RepairStoryRewards(statics);
        RepairAvatarClasses(statics);
        RepairMissionLevels(statics);
        RepairLocationUnlocks(statics);
        RepairXpPerLevelTable(statics);
        RepairLevelSkillPointMapping(statics);
        return statics;
    }

    private static void RepairXpPerLevelTable(object statics)
    {
        object table = New("AcierProtocol.XpPerLevelTable");
        Type entryType = Nested("XpPerLevelTable", "Entry");
        const int count = 51;
        Array entries = Array.CreateInstance(entryType, count);
        long xp = 0;
        for (int i = 0; i < count; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "level", i + 1);
            Set(entry, "xp", xp);
            entries.SetValue(entry, i);
            xp += (long)((i + 1) * 500);
        }
        Set(table, "entries", entries);
        Set(statics, "xpPerLevelTable", table);
        Console.WriteLine("xp per level entries " + count);
    }

    private static void RepairLevelSkillPointMapping(object statics)
    {
        object mapping = New("AcierProtocol.LevelSkillPointMapping");
        Type entryType = Nested("LevelSkillPointMapping", "LevelSkillPointEntry");
        const int count = 51;
        Array entries = Array.CreateInstance(entryType, count);
        for (int i = 0; i < count; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "level", i + 1);
            Set(entry, "skillPoint", (i + 1) * 2);
            entries.SetValue(entry, i);
        }
        Set(mapping, "skillPointsPerLevel", entries);
        Set(statics, "levelSkillPointMapping", mapping);
        Console.WriteLine("level skill point entries " + count);
    }

    // The mission screen looks up the mission's difficulty in this table. An empty
    // table throws, and that failure is shown as an internet error over the Continue button.
    private static void RepairMissionLevels(object statics)
    {
        object globals = Get(statics, "globals");
        Type mappingType = Nested("Globals", "MissionLevelMapping");
        const int count = 51;
        Array entries = Array.CreateInstance(mappingType, count);
        for (int i = 0; i < count; i++)
        {
            object entry = Activator.CreateInstance(mappingType);
            Set(entry, "missionLevel", (uint)i);
            Set(entry, "powRecommendation", (uint)(i == 0 ? 10 : i * 15));
            entries.SetValue(entry, i);
        }
        Set(globals, "missionLevelMapping", entries);
        Console.WriteLine("mission levels " + count);
    }

    // Start looks up the location's scene. With no row, that lookup is null and the
    // tap returns to the hire screen without asking the server to open the mission.
    private static void RepairLocationUnlocks(object statics)
    {
        object mapping = New("AcierProtocol.LocationUnlockMappings");
        Type entryType = Nested("LocationUnlockMappings", "Entry");
        Type locationType = TypeOf("AcierProtocol.LocationSet");

        var map = new Dictionary<(string era, string region, string mood), string>
        {
            { ("Italy", "Forli", "Dusk"), "Forli_dusk" },
            { ("Italy", "Forli", "Siege"), "Forli_siege" },
            { ("Italy", "Firenze_Palazzo", "Daytime"), "Firenze_palazzo_daytime" },
            { ("Italy", "Firenze_Palazzo", "Nighttime"), "Firenze_palazzo_nighttime" },
            { ("Italy", "Firenze_SantaCroce", "Daytime"), "Firenze_santacroce_Tutorial" },
            { ("Italy", "Firenze_SantaCroce", "Sunny"), "Firenze_santacroce_sunny" },
            { ("Italy", "Firenze_SantaCroce", "Stormy"), "Firenze_santacroce_stormy" },
            { ("Italy", "Monteriggioni", "Daytime"), "MonteRiggioni_DayTime" },
            { ("Italy", "Monteriggioni", "Nighttime"), "MonteRiggioni_NightTime" },
            { ("Italy", "Roma_SanAngelo", "Overcast"), "Roma_Castello_SantAngelo_Overcast" },
            { ("Italy", "Roma_SanAngelo", "Stormy"), "Roma_Castello_SantAngelo_stormy" },
            { ("Italy", "Roma_Coloseum", "Afternoon"), "Roma_Coloseum_Afternoon" },
            { ("Italy", "Roma_Coloseum", "Foggy"), "Roma_Coloseum_Foggy" },
            { ("Abstergo", "Animus", "Daytime"), "AnimusGlobe" },
            { ("Abstergo", "Montreal", "Daytime"), "TutorialScene_01" },
            { ("Abstergo", "Montreal", "Nighttime"), "TutorialScene_02" },
            { ("Abstergo", "Montreal", "Sunny"), "TutorialScene_03" },
            { ("Caribbean", "Havana", "Daytime"), "Forli_dusk" },
            { ("Caribbean", "Nassau", "Daytime"), "Forli_dusk" },
            { ("Caribbean", "Tortuga", "Daytime"), "Forli_dusk" },
        };

        Type eraType = TypeOf("AcierProtocol.EEra");
        Type regionType = TypeOf("AcierProtocol.ERegion");
        Type moodType = TypeOf("AcierProtocol.EMood");

        string[] eras = Enum.GetNames(eraType);
        string[] regions = Enum.GetNames(regionType);
        string[] moods = Enum.GetNames(moodType);

        var allEntries = new List<object>();
        string[] none = Array.Empty<string>();

        foreach (string era in eras)
        {
            foreach (string reg in regions)
            {
                foreach (string mood in moods)
                {
                    string scene;
                    if (!map.TryGetValue((era, reg, mood), out scene))
                    {
                        scene = "Forli_dusk";
                    }

                    object entry = Activator.CreateInstance(entryType);
                    object location = Activator.CreateInstance(locationType);
                    Set(location, "era", Enum.Parse(eraType, era));
                    Set(location, "region", Enum.Parse(regionType, reg));
                    Set(location, "mood", Enum.Parse(moodType, mood));
                    Set(entry, "location", location);
                    Set(entry, "sceneName", scene);
                    Set(entry, "reqEntitlements", none);
                    allEntries.Add(entry);
                }
            }
        }

        Array entries = Array.CreateInstance(entryType, allEntries.Count);
        for (int i = 0; i < allEntries.Count; i++)
        {
            entries.SetValue(allEntries[i], i);
        }

        Set(mapping, "entries", entries);
        Set(statics, "locationUnlockMappings", mapping);
        Console.WriteLine("location unlocks " + entries.Length);
    }

    // The hire screen only shows its start button after it can match the hired
    // assassin's class. An empty class list makes that match fail and the button stays hidden.
    private static void RepairAvatarClasses(object statics)
    {
        object definitions = Get(statics, "avatarClassDefinitions");
        if (definitions == null)
        {
            definitions = New("AcierProtocol.AvatarClassDefinitions");
            Set(statics, "avatarClassDefinitions", definitions);
        }
        Type avatarClass = Nested("AvatarClassDefinitions", "AvatarClass");
        Type headSettings = null;
        foreach (Type type in _protocol.GetTypes())
        {
            if (type.Name == "AvatarHeadSettings" && type.DeclaringType == avatarClass)
            {
                headSettings = type;
                break;
            }
        }
        if (headSettings == null) throw new TypeLoadException("AvatarClass.AvatarHeadSettings");
        Type classEnum = TypeOf("AcierProtocol.Assassin").GetNestedType("EClassTypeId");
        // These are the GameDB humanoid ids. The Animus loader asks for a bundle per class,
        // and an empty id makes that lookup throw before the hub appears.
        string[] names = { "Berserker", "Shadowblade", "Trickster", "Thief" };
        string[] bodies =
        {
            "de4ea1c3-3976-48e9-aee4-1e48ad8cb249",
            "0aa60bae-8439-4e40-a687-245872ceeb7c",
            "4f8b5b58-5074-4e93-a205-46088283ab16",
            "3d436e0d-3528-4aac-bc15-613341cb39cb"
        };
        // HeadDef ids from GameDB. A humanoid id is not a head, and the portrait
        // constructor dereferences a missing head before the Animus can finish loading.
        string[] headGuids =
        {
            "77d5f907-1c42-41bd-a94e-283d1a274fff",
            "5bba50cd-294b-4ce4-9399-2ea68fc4e885",
            "599683ea-e330-4080-81ea-805a96af7f96",
            "6c30e2a3-466c-47ec-a0d9-b188cb1061a8"
        };
        Array classes = Array.CreateInstance(avatarClass, names.Length);
        for (int i = 0; i < names.Length; i++)
        {
            object entry = Activator.CreateInstance(avatarClass);
            Set(entry, "classTypeId", Enum.Parse(classEnum, names[i]));
            Set(entry, "humanoidGuid", bodies[i]);
            Set(entry, "weaponGuid", bodies[i]);
            Set(entry, "hirelingAbilityGuids", new[] { "" });
            object head = Activator.CreateInstance(headSettings);
            Set(head, "headGuid", headGuids[i]);
            Set(entry, "heads", head);
            classes.SetValue(entry, i);
        }
        Set(definitions, "classes", classes);
        Console.WriteLine("avatar classes " + classes.Length);
    }

    private static object CreateMissionGoals(int targetCount, int descriptionNeutral = 3430, string objectiveType = "Kill")
    {
        object goals = New("AcierProtocol.MissionGoals");
        Type goalType = Nested("MissionGoals", "MissionGoal");
        object goal = Activator.CreateInstance(goalType);
        Set(goal, "goalType", Enum.Parse(TypeOf("AcierProtocol.EMissionGoal"), "Objective_EMissionGoal"));
        Set(goal, "objectiveId", 3L);
        Set(goal, "id", 1);
        Set(goal, "type", Enum.Parse(TypeOf("AcierProtocol.EObjective"), objectiveType));
        object description = New("AcierProtocol.LangKey");
        Set(description, "neutral", descriptionNeutral); // Loca description
        Set(goal, "description", description);

        Type entryType = Nested("MissionGoal", "MappingEntry");
        object entry = Activator.CreateInstance(entryType);
        Set(entry, "key", Enum.Parse(TypeOf("AcierProtocol.EObjectiveBoundWildcards"), "Count_EObjectiveBoundWildcards"));
        Set(entry, "valueInt", targetCount);
        Set(entry, "valueString", targetCount.ToString());
        Array mappingArray = Array.CreateInstance(entryType, 1);
        mappingArray.SetValue(entry, 0);
        Set(goal, "mapping", mappingArray);

        Array list = Array.CreateInstance(goalType, 1);
        list.SetValue(goal, 0);
        Set(goals, "missionGoals", list);
        Set(goals, "missionLevel", 1);
        return goals;
    }

    private static object CreateNpcSpawnNode(string name, long id, string typeGuid, float x, float y, float z, float rotY, bool isEnemy = true)
    {
        object node = New("AcierProtocol.SerializedNode");
        Set(node, "Name", name);
        Set(node, "PositionX", x);
        Set(node, "PositionY", y);
        Set(node, "PositionZ", z);
        Set(node, "EulerAnglesY", rotY);

        Type componentType = TypeOf("AcierProtocol.SerializedNodeComponent");

        // 1. Formation component (required by NpcStatsHelper.InitializeMutable to avoid NullReferenceException)
        object formComp = Activator.CreateInstance(componentType);
        Set(formComp, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionNpcFormationContent"));
        object formContent = New("AcierProtocol.MissionNpcFormationContent");
        object formBase = New("AcierProtocol.MissionFormationBaseContent");
        Set(formBase, "MissionBaseContent", ActiveBase(id + 1000L));
        Set(formBase, "Type", Enum.Parse(TypeOf("AcierProtocol.EPatrolType"), isEnemy ? "Military" : "CevilianWide"));
        Set(formBase, "IterationDirection", Enum.Parse(TypeOf("AcierProtocol.EFormationIterationDirection"), "Down"));
        Set(formBase, "IterationType", Enum.Parse(TypeOf("AcierProtocol.EFormationIterationType"), "BackAndForth"));
        Set(formBase, "Radius", 5.0f);
        Set(formContent, "MissionFormationBaseContent", formBase);
        Set(formComp, "MissionNpcFormationContent", formContent);

        // 2. Spawn component
        object spawnComp = Activator.CreateInstance(componentType);
        Set(spawnComp, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionNpcSpawnContent"));
        object spawnContent = New("AcierProtocol.MissionNpcSpawnContent");
        object entityContent = New("AcierProtocol.MissionSpawnEntityContent");
        Set(entityContent, "MissionBaseContent", ActiveBase(id));
        Set(entityContent, "IsMimic", false);
        Set(entityContent, "FadeMode", Enum.Parse(TypeOf("AcierProtocol.EFadeMode"), "Simple_EFadeMode"));
        Set(entityContent, "FadeDuration", 1.0f);
        Set(spawnContent, "MissionSpawnEntityContent", entityContent);
        Set(spawnContent, "TypeGuid", typeGuid);
        Set(spawnContent, "ScalingMultiplier", 1.0f);
        Set(spawnComp, "MissionNpcSpawnContent", spawnContent);

        Array comps = Array.CreateInstance(componentType, 2);
        comps.SetValue(formComp, 0);
        comps.SetValue(spawnComp, 1);
        Set(node, "Components", comps);

        return node;
    }

    private static object CreateObjectiveInteractionComponent(long objId, long[] targetSpawnIds, int descriptionLocaId)
    {
        Type componentType = TypeOf("AcierProtocol.SerializedNodeComponent");
        object comp = Activator.CreateInstance(componentType);
        Set(comp, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_ObjectiveInteractionContent"));

        object interactionContent = New("AcierProtocol.ObjectiveInteractionContent");
        object targetContent = New("AcierProtocol.ObjectiveTargetContent");
        object objBase = New("AcierProtocol.ObjectiveBaseContent");
        object objMissionBase = ActiveBase(objId);
        Set(objMissionBase, "MapIcon", Enum.Parse(TypeOf("AcierProtocol.EMapIconType"), "Doctor_EMapIconType"));
        Set(objBase, "MissionBaseContent", objMissionBase);
        Set(objBase, "IsPrimaryObjective", true);
        Set(objBase, "HideDescription", false);
        Set(objBase, "Description", descriptionLocaId.ToString());
        Set(objBase, "ShortDescription", descriptionLocaId.ToString());
        Set(objBase, "OnSuccessId", -2L);
        Set(objBase, "OnFailId", -2L);
        Set(objBase, "IsVisibleInEagleVision", true);
        Set(objBase, "TrackerIcon", Enum.Parse(TypeOf("AcierProtocol.EObjectiveType"), "Tutorial_EObjectiveType"));

        Set(targetContent, "ObjectiveBaseContent", objBase);
        Set(targetContent, "Targets", targetSpawnIds);

        Set(interactionContent, "ObjectiveTargetContent", targetContent);
        Set(comp, "ObjectiveInteractionContent", interactionContent);
        return comp;
    }

    private static object CreateObjectiveKillComponent(long objId, long[] targetSpawnIds, int descriptionLocaId = 3430, int targetCount = -1)
    {
        if (targetCount <= 0) targetCount = targetSpawnIds.Length;
        Type componentType = TypeOf("AcierProtocol.SerializedNodeComponent");
        object comp = Activator.CreateInstance(componentType);
        Set(comp, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_ObjectiveKillContent"));

        object killContent = New("AcierProtocol.ObjectiveKillContent");
        object targetContent = New("AcierProtocol.ObjectiveTargetContent");
        object objBase = New("AcierProtocol.ObjectiveBaseContent");
        object objMissionBase = ActiveBase(objId);
        Set(objMissionBase, "MapIcon", Enum.Parse(TypeOf("AcierProtocol.EMapIconType"), "Assassinate_EMapIconType"));
        Set(objBase, "MissionBaseContent", objMissionBase);
        Set(objBase, "IsPrimaryObjective", true);
        Set(objBase, "HideDescription", false);
        Set(objBase, "Description", descriptionLocaId.ToString());
        Set(objBase, "ShortDescription", descriptionLocaId.ToString());
        Set(objBase, "OnSuccessId", -2L);
        Set(objBase, "OnFailId", -2L);
        Set(objBase, "IsVisibleInEagleVision", true);
        Set(objBase, "TrackerIcon", Enum.Parse(TypeOf("AcierProtocol.EObjectiveType"), "Assasinate_EObjectiveType"));

        Set(targetContent, "ObjectiveBaseContent", objBase);
        Set(targetContent, "Targets", targetSpawnIds);

        Set(killContent, "ObjectiveTargetContent", targetContent);
        Set(killContent, "Minimum", targetCount);
        Set(killContent, "DoNotKill", false);

        Set(comp, "ObjectiveKillContent", killContent);
        return comp;
    }

    private static object AssembleMissionFile(
        string missionId,
        string era,
        string region,
        string mood,
        string scene,
        string locationName,
        float playerX,
        float playerY,
        float playerZ,
        float playerRot,
        long[] targetIds,
        (string name, long id, string guid, float x, float y, float z, float rot, bool isEnemy)[] npcs,
        int descriptionLocaId = 3430,
        string objectiveKind = "Kill",
        int targetCount = -1)
    {
        if (targetCount <= 0) targetCount = targetIds.Length;
        object file = New("AcierProtocol.MissionFile");
        object root = New("AcierProtocol.SerializedNode");
        Set(root, "Name", missionId);
        Type componentType = TypeOf("AcierProtocol.SerializedNodeComponent");
        object component = Activator.CreateInstance(componentType);
        Set(component, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionRootContent"));
        object content = New("AcierProtocol.MissionRootContent");
        Set(content, "MissionBaseContent", New("AcierProtocol.MissionBaseContent"));
        Set(content, "MissionType", Enum.Parse(TypeOf("AcierProtocol.EMissionType"), "Mission_EMissionType"));
        Set(content, "BaseName", missionId);
        Set(content, "DifficultyLevel", 1);
        Set(content, "Rank", 1);

        Set(content, "Age", Enum.Parse(TypeOf("AcierProtocol.EEra"), era));
        Set(content, "Region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), region));
        Set(content, "Mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), mood));
        Set(content, "Name", locationName);
        Set(content, "Scene", scene);
        Set(component, "MissionRootContent", content);

        object hotspots = Activator.CreateInstance(componentType);
        Set(hotspots, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionHotspotsContent"));
        object hotspotContent = New("AcierProtocol.MissionHotspotsContent");
        object hotspotBase = New("AcierProtocol.MissionBaseContent");
        Set(hotspotBase, "ID", 2L);
        Set(hotspotContent, "MissionBaseContent", hotspotBase);
        Set(hotspots, "MissionHotspotsContent", hotspotContent);

        object mainObjective = objectiveKind == "Interaction"
            ? CreateObjectiveInteractionComponent(3L, targetIds, descriptionLocaId)
            : CreateObjectiveKillComponent(3L, targetIds, descriptionLocaId, targetCount);

        // Backup timer objective (hidden, non-primary, optional so it does not block completion)
        object stay = Activator.CreateInstance(componentType);
        Set(stay, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_ObjectiveTimerContent"));
        object stayContent = New("AcierProtocol.ObjectiveTimerContent");
        object objective = New("AcierProtocol.ObjectiveBaseContent");
        Set(objective, "MissionBaseContent", ActiveBase(4));
        Set(objective, "IsPrimaryObjective", false);
        Set(objective, "IsOptionalObjective", true);
        Set(objective, "OnSuccessId", 1L);
        Set(objective, "OnFailId", 1L);
        Set(objective, "HideDescription", true);
        Set(stayContent, "ObjectiveBaseContent", objective);
        Set(stayContent, "TimeInSeconds", 86400);
        Set(stayContent, "InitialResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stayContent, "CompletionResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stayContent, "TerminationResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stay, "ObjectiveTimerContent", stayContent);

        Array components = Array.CreateInstance(componentType, 4);
        components.SetValue(component, 0);
        components.SetValue(hotspots, 1);
        components.SetValue(mainObjective, 2);
        components.SetValue(stay, 3);
        Set(root, "Components", components);
        components.SetValue(stay, 3);
        Set(root, "Components", components);

        // 1. Player Spawn
        object spawn = New("AcierProtocol.SerializedNode");
        Set(spawn, "Name", "PlayerSpawn");
        Set(spawn, "PositionX", playerX);
        Set(spawn, "PositionY", playerY);
        Set(spawn, "PositionZ", playerZ);
        Set(spawn, "EulerAnglesY", playerRot);
        object spawnComponent = Activator.CreateInstance(componentType);
        Set(spawnComponent, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionPlayerSpawnContent"));
        object spawnContent = New("AcierProtocol.MissionPlayerSpawnContent");
        Set(spawnContent, "MissionBaseContent", ActiveBase(1));
        Set(spawnComponent, "MissionPlayerSpawnContent", spawnContent);
        Array spawnComponents = Array.CreateInstance(componentType, 1);
        spawnComponents.SetValue(spawnComponent, 0);
        Set(spawn, "Components", spawnComponents);

        // 2. NPC Spawns
        Array children = Array.CreateInstance(root.GetType(), npcs.Length + 1);
        children.SetValue(spawn, 0);
        for (int i = 0; i < npcs.Length; i++)
        {
            var npc = npcs[i];
            object npcNode = CreateNpcSpawnNode(npc.name, npc.id, npc.guid, npc.x, npc.y, npc.z, npc.rot, npc.isEnemy);
            children.SetValue(npcNode, i + 1);
        }
        Set(root, "Children", children);

        Set(file, "RootNode", root);
        return file;
    }

    private static (object file, object goals) BuildMissionFileAndGoals(string missionId, int rank)
    {
        missionId = missionId ?? "story_0015";
        string lower = missionId.ToLowerInvariant();

        string era = "Italy";
        string region = "Forli";
        string mood = "Dusk";
        string scene = "Forli_dusk";
        string locationName = "Forli";

        float playerX = -2.0f, playerY = 8.6f, playerZ = -40.0f, playerRot = 0f;
        int targetCount = 3;
        int objectiveNeutral = 3430;
        string objectiveKind = "Kill";
        string goalType = "Kill";
        long[] targetIds = new long[] { 101L };

        (string name, long id, string guid, float x, float y, float z, float rot, bool isEnemy)[] npcs;

        // Tutorial 01: Interaction - Approach the Doctor
        if (lower.Contains("tutorial_01") || (lower.Contains("tutorial") && lower.Contains("interaction")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = 0.0f; playerY = 0.5f; playerZ = -8.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6910; // Approach the Doctor
            objectiveKind = "Interaction";
            goalType = "NpcInteraction";
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Doctor_Interact", 101L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 0.0f, 0.5f, 4.0f, 180f, false),
                ("Town_Civilian_01", 102L, "ntt_44fd8a65", -4.0f, 0.5f, -2.0f, 90f, false),
                ("Town_Civilian_02", 103L, "ntt_2cccad34", 4.0f, 0.5f, -2.0f, -90f, false)
            };
        }
        // Tutorial 02: Free Running
        else if (lower.Contains("tutorial_02") || (lower.Contains("tutorial") && lower.Contains("running")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = -12.0f; playerY = 8.5f; playerZ = -12.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6916; // Tutorial: Free Running
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Rooftop_Target", 101L, "ntt_bd49bc5f", -12.0f, 8.5f, 12.0f, 180f, true),
                ("VR_Street_Civilian", 102L, "ntt_44fd8a65", 0.0f, 0.5f, 0.0f, 0f, false)
            };
        }
        // Tutorial 03: Combat
        else if (lower.Contains("tutorial_03") || (lower.Contains("tutorial") && lower.Contains("combat")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = 0.0f; playerY = 0.5f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6917; // Tutorial: Combat
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Sparring_Partner", 101L, "ntt_0c585da4", 0.0f, 0.5f, 4.0f, 180f, true),
                ("VR_Combat_Instructor", 102L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -5.0f, 0.5f, -1.0f, 90f, false)
            };
        }
        // Tutorial 04: Skills
        else if (lower.Contains("tutorial_04") || (lower.Contains("tutorial") && lower.Contains("skills")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = 0.0f; playerY = 0.5f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6918; // Tutorial: Skills
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Skill_Brute", 101L, "ntt_6d68817b", 0.0f, 0.5f, 5.0f, 180f, true),
                ("VR_Skill_Instructor", 102L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -5.0f, 0.5f, -1.0f, 90f, false)
            };
        }
        // Tutorial 05: Eagle Vision
        else if (lower.Contains("tutorial_05") || (lower.Contains("tutorial") && lower.Contains("eagle")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = 0.0f; playerY = 0.5f; playerZ = -12.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6694; // Tutorial: Eagle Vision
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Undercover_Spy", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 6.0f, 180f, true),
                ("Crowd_01", 102L, "ntt_44fd8a65", -2.0f, 0.5f, 6.0f, 45f, false),
                ("Crowd_02", 103L, "ntt_2cccad34", 2.0f, 0.5f, 5.0f, -45f, false),
                ("Crowd_03", 104L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", -1.0f, 0.5f, 8.0f, 135f, false),
                ("Crowd_04", 105L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 1.0f, 0.5f, 8.0f, -135f, false)
            };
        }
        // Tutorial 06: Assassinations (Air Assassination from Rooftop Ledge)
        else if (lower.Contains("tutorial_06") || (lower.Contains("tutorial") && lower.Contains("assassination")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            // Player perched on roof ledge looking down
            playerX = -3.5f; playerY = 8.6f; playerZ = -18.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6696; // Tutorial: Assassinations
            targetIds = new long[] { 101L };
            // Target directly 4m below ledge facing away (rot 0f) for Air Assassination
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Ledge_Target", 101L, "ntt_0c585da4", -3.5f, 0.5f, -14.0f, 0f, true),
                ("VR_Street_Civilian", 102L, "ntt_44fd8a65", 1.0f, 0.5f, -14.0f, 0f, false)
            };
        }
        // Tutorial 07: Stealth Assassinations
        else if (lower.Contains("tutorial_07") || (lower.Contains("tutorial") && lower.Contains("stealth")))
        {
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = -6.0f; playerY = 0.5f; playerZ = -10.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6695; // Tutorial: Stealth Assassinations
            targetIds = new long[] { 101L };
            // Guard patrolling with back turned towards player
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("VR_Patrol_Guard", 101L, "ntt_0c585da4", -6.0f, 0.5f, 0.0f, 0f, true),
                ("VR_Street_Civilian", 102L, "ntt_44fd8a65", 2.0f, 0.5f, -5.0f, 90f, false)
            };
        }
        // Campaign 1: A Murder of Crows (10 Story Missions)
        else if (lower == "story_crows_01" || lower.Contains("corrupted") || lower.Contains("monteriggioni_01"))
        {
            // Mission 1: The Corrupted (Monteriggioni - Daytime)
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = 0.0f; playerY = 1.0f; playerZ = -10.0f; playerRot = 0f;
            targetCount = 3;
            objectiveNeutral = 2066; // Find and Kill the Mercenaries.
            targetIds = new long[] { 101L, 102L, 103L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Tax_Collector_Mercenary_01", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 6.0f, 180f, true),
                ("Tax_Collector_Mercenary_02", 102L, "ntt_0c585da4", -3.0f, 0.5f, 10.0f, 180f, true),
                ("Tax_Collector_Mercenary_03", 103L, "ntt_0c585da4", 3.0f, 0.5f, 10.0f, 180f, true),
                ("Enemy_Crossbow_01", 104L, "ntt_ed443896", 2.0f, 8.0f, 14.0f, 180f, true),
                ("Enemy_Brute_01", 105L, "ntt_6d68817b", -2.0f, 0.5f, 16.0f, 180f, true),
                ("Monteriggioni_Citizen_Doctor", 106L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -5.0f, 0.5f, 0.0f, 90f, false),
                ("Monteriggioni_Citizen_Courtesan", 107L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", 5.0f, 0.5f, 0.0f, -90f, false),
                ("Monteriggioni_Citizen_Male", 108L, "ntt_44fd8a65", -4.0f, 0.5f, -5.0f, 90f, false),
                ("Monteriggioni_Citizen_Female", 109L, "ntt_2cccad34", 4.0f, 0.5f, -5.0f, -90f, false)
            };
        }
        else if (lower == "story_crows_02" || lower.Contains("healer") || lower.Contains("monteriggioni_02"))
        {
            // Mission 2: A Healer's Blood (Monteriggioni - Nighttime) - Pietro de Galencia
            region = "Monteriggioni"; mood = "Nighttime"; scene = "MonteRiggioni_NightTime"; locationName = "Monteriggioni";
            playerX = 8.0f; playerY = 1.0f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 2067; // Find Pietro de Galencia and kill him.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Pietro_de_Galencia", 101L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 8.0f, 0.5f, 10.0f, 180f, true),
                ("Pietro_Bodyguard_01", 102L, "ntt_0c585da4", 5.0f, 0.5f, 12.0f, 180f, true),
                ("Pietro_Bodyguard_02", 103L, "ntt_0c585da4", 11.0f, 0.5f, 12.0f, 180f, true),
                ("Borgia_Crossbow_01", 104L, "ntt_ed443896", 7.0f, 8.0f, 15.0f, 180f, true),
                ("Borgia_Brute_01", 105L, "ntt_6d68817b", 8.0f, 0.5f, 18.0f, 180f, true),
                ("Special_Courtesan_01", 106L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", 2.0f, 0.5f, 0.0f, -90f, false),
                ("Civilian_Male_01", 107L, "ntt_44fd8a65", 4.0f, 0.5f, -2.0f, 90f, false),
                ("Civilian_Female_01", 108L, "ntt_2cccad34", 12.0f, 0.5f, -2.0f, -90f, false)
            };
        }
        else if (lower == "story_crows_03" || lower.Contains("vengeful") || lower.Contains("palazzo_01"))
        {
            // Mission 3: Vengeful Eyes (Firenze Palazzo - Daytime) - Defending Leonardo da Vinci
            region = "Firenze_Palazzo"; mood = "Daytime"; scene = "Firenze_palazzo_daytime"; locationName = "Firenze";
            // Safe open piazza coordinates in front of Palazzo Vecchio (not inside townhouse geometry!)
            playerX = 0.0f; playerY = 0.5f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 2;
            objectiveNeutral = 2068; // Protect Leonardo and Escort him to safety.
            targetIds = new long[] { 101L, 102L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Borgia_Watcher_Leader", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 6.0f, 180f, true),
                ("Borgia_Watcher_Left", 102L, "ntt_ed443896", -4.0f, 0.5f, 10.0f, 180f, true),
                ("Leonardo_da_Vinci", 106L, "ntt_44fd8a65", 2.0f, 0.5f, -2.0f, 90f, false),
                ("Enemy_Militia_01", 104L, "ntt_0c585da4", 4.0f, 0.5f, 10.0f, 180f, true),
                ("Enemy_Brute_01", 105L, "ntt_6d68817b", 0.0f, 0.5f, 14.0f, 180f, true),
                ("Special_Courtesan_01", 107L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", -5.0f, 0.5f, 0.0f, -90f, false),
                ("Special_Doctor_01", 108L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 5.0f, 0.5f, 0.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_04" || lower.Contains("requiem") || lower.Contains("coloseum_01"))
        {
            // Mission 4: An Assassin's Requiem (Roma Colosseum - Afternoon)
            region = "Roma_Coloseum"; mood = "Afternoon"; scene = "Roma_Coloseum_Afternoon"; locationName = "Roma";
            playerX = 0.0f; playerY = 1.0f; playerZ = -15.0f; playerRot = 0f;
            targetCount = 2;
            objectiveNeutral = 3332; // Recover the contents of the chest and kill the Borgia Agents, then flee the area.
            targetIds = new long[] { 101L, 102L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Borgia_Desecrator_Leader", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 2.0f, 180f, true),
                ("Borgia_Catacomb_Agent_01", 102L, "ntt_6d68817b", -4.0f, 0.5f, 6.0f, 180f, true),
                ("Borgia_Catacomb_Agent_02", 103L, "ntt_0c585da4", 4.0f, 0.5f, 6.0f, 180f, true),
                ("Catacomb_Scout", 105L, "ntt_44fd8a65", -5.0f, 1.0f, -10.0f, 90f, false),
                ("Special_Doctor_01", 106L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 6.0f, 1.0f, -5.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_05" || lower.Contains("broken_chains") || lower.Contains("chains") || lower.Contains("santacroce"))
        {
            // Mission 5: Broken Chains (Firenze Santa Croce - Sunny) - Rescuing Demetrio
            region = "Firenze_SantaCroce"; mood = "Sunny"; scene = "Firenze_santacroce_sunny"; locationName = "Firenze";
            playerX = 0.0f; playerY = 1.0f; playerZ = -10.0f; playerRot = 0f;
            targetCount = 2;
            objectiveNeutral = 3333; // Rescue Demetrio and Escort him to safety.
            targetIds = new long[] { 101L, 102L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Slave_Driver_Captain", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 10.0f, 180f, true),
                ("Slave_Driver_Brute", 102L, "ntt_6d68817b", -5.0f, 0.5f, 14.0f, 180f, true),
                ("Slave_Driver_Guard", 103L, "ntt_0c585da4", 5.0f, 0.5f, 14.0f, 180f, true),
                ("Demetrio_The_Inventor", 106L, "ntt_44fd8a65", -4.0f, 0.5f, -4.0f, 90f, false),
                ("Special_Courtesan_01", 105L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", -6.0f, 0.5f, 2.0f, -90f, false),
                ("Special_Doctor_01", 107L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 6.0f, 0.5f, 2.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_06" || lower.Contains("storm_of_crows") || lower.Contains("storm"))
        {
            // Mission 6: A Storm of Crows (Roma Colosseum - Foggy)
            region = "Roma_Coloseum"; mood = "Foggy"; scene = "Roma_Coloseum_Foggy"; locationName = "Roma";
            playerX = 10.0f; playerY = 1.0f; playerZ = -10.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 4423; // Investigate the Borgia Camp.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Captain_Matteo", 101L, "ntt_bd49bc5f", 10.0f, 0.5f, 8.0f, 180f, true),
                ("Crow_Infiltrator_01", 102L, "ntt_bd49bc5f", 6.0f, 0.5f, 14.0f, 180f, true),
                ("Crow_Infiltrator_02", 103L, "ntt_bd49bc5f", 14.0f, 0.5f, 14.0f, 180f, true),
                ("Sirus_Favero", 106L, "ntt_44fd8a65", 8.0f, 0.5f, -5.0f, 90f, false),
                ("Special_Doctor_01", 107L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 4.0f, 0.5f, -2.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_07" || lower.Contains("burning_pages") || lower.Contains("burning"))
        {
            // Mission 7: Burning Pages (Firenze Palazzo - Daytime) - The Bookkeeper
            region = "Firenze_Palazzo"; mood = "Daytime"; scene = "Firenze_palazzo_daytime"; locationName = "Firenze";
            playerX = -6.0f; playerY = 0.5f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 2086; // Scout the area in order to identify the Bookkeeper.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("The_Bookkeeper", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 8.0f, 180f, true),
                ("Arsonist_Guard_01", 102L, "ntt_0c585da4", -4.0f, 0.5f, 12.0f, 180f, true),
                ("Arsonist_Guard_02", 103L, "ntt_0c585da4", 4.0f, 0.5f, 12.0f, 180f, true),
                ("Special_Doctor_01", 106L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -6.0f, 0.5f, 0.0f, 90f, false),
                ("Library_Scholar_01", 107L, "ntt_44fd8a65", 6.0f, 0.5f, 0.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_08" || lower.Contains("red_sun") || lower.Contains("redsun"))
        {
            // Mission 8: Under the Red Sun (Monteriggioni - Daytime)
            region = "Monteriggioni"; mood = "Daytime"; scene = "MonteRiggioni_DayTime"; locationName = "Monteriggioni";
            playerX = -5.0f; playerY = 1.0f; playerZ = -15.0f; playerRot = 0f;
            targetCount = 2;
            objectiveNeutral = 4497; // Help Sirus locate the hideout of the Crows.
            targetIds = new long[] { 101L, 102L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Ambushing_Crow_Leader", 101L, "ntt_bd49bc5f", -5.0f, 0.5f, 4.0f, 180f, true),
                ("Ambushing_Crow_Left", 102L, "ntt_6d68817b", -10.0f, 0.5f, 8.0f, 180f, true),
                ("Ambushing_Crow_Right", 103L, "ntt_0c585da4", 0.0f, 0.5f, 8.0f, 180f, true),
                ("Sirus_Favero", 106L, "ntt_44fd8a65", -8.0f, 0.5f, -12.0f, 90f, false),
                ("Special_Doctor_01", 105L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -12.0f, 0.5f, -8.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_09" || lower.Contains("dangerous_trade") || lower.Contains("trade"))
        {
            // Mission 9: A Dangerous Trade (Firenze Palazzo - Nighttime)
            region = "Firenze_Palazzo"; mood = "Nighttime"; scene = "Firenze_palazzo_nighttime"; locationName = "Firenze";
            playerX = 6.0f; playerY = 0.5f; playerZ = -6.0f; playerRot = 0f;
            targetCount = 2;
            objectiveNeutral = 4543; // DELIVER the letter to Sirus
            targetIds = new long[] { 101L, 102L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Crow_Messenger", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 8.0f, 180f, true),
                ("Templar_Agent_Receiver", 102L, "ntt_6d68817b", -4.0f, 0.5f, 12.0f, 180f, true),
                ("Arms_Dealer_Guard", 103L, "ntt_ed443896", 4.0f, 0.5f, 12.0f, 180f, true),
                ("Civilian_Male_01", 107L, "ntt_44fd8a65", 0.0f, 0.5f, -2.0f, 90f, false)
            };
        }
        else if (lower == "story_crows_10" || lower.Contains("saviors_of_roma") || lower.Contains("saviors"))
        {
            // Mission 10: Saviors of Roma (Roma Colosseum - Afternoon) - Il Corvo!
            region = "Roma_Coloseum"; mood = "Afternoon"; scene = "Roma_Coloseum_Afternoon"; locationName = "Roma";
            playerX = 0.0f; playerY = 1.0f; playerZ = -20.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 4498; // Fight your way to Il Corvo and kill him.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Il_Corvo", 101L, "ntt_bd49bc5f", 0.0f, 0.5f, 5.0f, 180f, true),
                ("Borgia_High_Commander", 102L, "ntt_bd49bc5f", -6.0f, 0.5f, 10.0f, 180f, true),
                ("Elite_Crow_Enforcer", 103L, "ntt_6d68817b", 6.0f, 0.5f, 10.0f, 180f, true),
                ("Niccolo_Machiavelli", 107L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 8.0f, 1.0f, -10.0f, 90f, false),
                ("Special_Courtesan_01", 108L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", -8.0f, 1.0f, -10.0f, -90f, false)
            };
        }
        // Campaign 2: Forli - A Crimson Sunset (4 Story Missions)
        else if (lower == "story_forli_02" || lower.Contains("story_0016") || lower.Contains("nest_of_the_bird") || lower.Contains("forli_r1_2"))
        {
            // Mission 2: The Nest of the Bird (Forli - Courtyard Fortress)
            region = "Forli"; mood = "Dusk"; scene = "Forli_dusk"; locationName = "Forli";
            playerX = 0.0f; playerY = -1.5f; playerZ = 55.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 6514; // KILL the remaining Crows - {Current}/{Count}
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Crow_Nest_Commander", 101L, "ntt_bd49bc5f", 0.0f, -1.9f, 74.0f, 180f, true),
                ("Crow_Nest_Archer_01", 102L, "ntt_ed443896", -10.0f, -1.9f, 68.0f, 90f, true),
                ("Crow_Nest_Archer_02", 103L, "ntt_ed443896", 10.0f, -1.9f, 68.0f, -90f, true),
                ("Crow_Nest_Brute", 104L, "ntt_6d68817b", 2.0f, -1.9f, 65.0f, 180f, true),
                ("Special_Doctor_01", 106L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -6.0f, -1.9f, 58.0f, 90f, false),
                ("Civilian_Male_01", 108L, "ntt_44fd8a65", -4.0f, -1.9f, 56.0f, 0f, false)
            };
        }
        else if (lower == "story_forli_03" || lower.Contains("grey_sky") || lower.Contains("greysky") || lower.Contains("siege"))
        {
            // Mission 3: A Grey Sky (Forli - Siege)
            region = "Forli"; mood = "Siege"; scene = "Forli_siege"; locationName = "Forli";
            playerX = 10.0f; playerY = 1.0f; playerZ = -30.0f; playerRot = 0f;
            targetCount = 1;
            objectiveNeutral = 8357; // Be careful not to be detected by the guards, or else they might execute their prisoners.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Siege_Inquisitor_Captain", 101L, "ntt_bd49bc5f", 10.0f, 0.5f, -15.0f, 180f, true),
                ("Borgia_Siege_Brute", 102L, "ntt_6d68817b", 5.0f, 0.5f, -10.0f, 180f, true),
                ("Borgia_Siege_Soldier", 103L, "ntt_0c585da4", 15.0f, 0.5f, -10.0f, 180f, true),
                ("Captured_Assassin_Spy", 106L, "ntt_44fd8a65", 18.0f, 0.5f, -25.0f, -90f, false),
                ("Special_Doctor_01", 105L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 2.0f, 0.5f, -25.0f, 90f, false)
            };
        }
        else if (lower == "story_forli_04" || lower.Contains("story_0017") || lower.Contains("one_for_all") || lower.Contains("forli_r1_3"))
        {
            // Mission 4: One for All (Forli - East Square Market)
            region = "Forli"; mood = "Dusk"; scene = "Forli_dusk"; locationName = "Forli";
            playerX = 25.0f; playerY = 1.0f; playerZ = -16.0f; playerRot = 90f;
            targetCount = 1;
            objectiveNeutral = 5660; // Meet with Niccolò Machiavelli and Escort him to his spy.
            targetIds = new long[] { 101L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Borgia_Lighthouse_Commander", 101L, "ntt_bd49bc5f", 38.0f, 1.0f, -16.0f, -90f, true),
                ("Borgia_Perimeter_Guard_01", 102L, "ntt_6d68817b", 44.0f, 1.0f, -12.0f, 180f, true),
                ("Niccolo_Machiavelli", 106L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", 30.0f, 1.0f, -10.0f, 90f, false),
                ("Ezio_Auditore", 108L, "ntt_bd49bc5f", 34.0f, 1.0f, -14.0f, 90f, false),
                ("Special_Courtesan_01", 107L, "f2ca13ef-9d66-49c2-8734-eecd97f595a4", 46.0f, 1.0f, -20.0f, -90f, false)
            };
        }
        else // Default / story_forli_01 / story_0015: An Unexpected Summon
        {
            region = "Forli"; mood = "Dusk"; scene = "Forli_dusk"; locationName = "Forli";
            playerX = -2.0f; playerY = 8.6f; playerZ = -40.0f; playerRot = 0f;
            targetCount = 3;
            objectiveNeutral = 6507; // The Crows are hiding somewhere inside Forlì. You have to find all of them.
            targetIds = new long[] { 101L, 102L, 103L };
            npcs = new (string, long, string, float, float, float, float, bool)[]
            {
                ("Hidden_Crow_Leader", 101L, "ntt_bd49bc5f", -2.0f, 0.5f, -35.0f, 180f, true),
                ("Undercover_Crow_01", 102L, "ntt_0c585da4", 1.0f, 0.5f, -28.0f, 180f, true),
                ("Undercover_Crow_02", 103L, "ntt_0c585da4", -1.0f, 0.5f, -20.0f, 180f, true),
                ("Enemy_Crossbow_01", 104L, "ntt_ed443896", -3.5f, 8.6f, -24.0f, 180f, true),
                ("Enemy_Brute_01", 105L, "ntt_6d68817b", 2.0f, 0.5f, -18.0f, 180f, true),
                ("Civilian_Male_01", 106L, "ntt_44fd8a65", -4.0f, 0.5f, -32.0f, 90f, false),
                ("Special_Doctor_01", 109L, "8047c14a-4ff1-4963-83a5-e900baa60dd5", -4.0f, 0.5f, -24.0f, 90f, false)
            };
        }

        object file = AssembleMissionFile(missionId, era, region, mood, scene, locationName, playerX, playerY, playerZ, playerRot, targetIds, npcs, objectiveNeutral, objectiveKind, targetCount);
        object goals = CreateMissionGoals(targetCount, objectiveNeutral, goalType);
        return (file, goals);
    }

    private static void ResetProfileToFresh()
    {
        object inventory = Get(_profile, "entitlementInventory");
        if (inventory == null) return;
        Type entryType = Nested("EntitlementInventory", "Entry");
        var freshList = new List<object>();
        foreach (string ent in ProfileEntitlements)
        {
            object newEntry = Activator.CreateInstance(entryType);
            Set(newEntry, "gameIdGuid", ent);
            Set(newEntry, "unlockDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            freshList.Add(newEntry);
        }
        object c1Entry = Activator.CreateInstance(entryType);
        Set(c1Entry, "gameIdGuid", "ntt_c350c141"); // Campaign 1: Animus VR Training
        Set(c1Entry, "unlockDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        freshList.Add(c1Entry);

        Array updated = Array.CreateInstance(entryType, freshList.Count);
        for (int i = 0; i < freshList.Count; i++) updated.SetValue(freshList[i], i);
        Set(inventory, "entries", updated);
        Set(_profile, "level", 26);
        Set(_profile, "id", "acid-offline-unlocked");
    }

    private static void AddEntitlement(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return;
        object inventory = Get(_profile, "entitlementInventory");
        if (inventory == null) return;
        Array existing = (Array)Get(inventory, "entries");
        Type entryType = Nested("EntitlementInventory", "Entry");
        if (existing != null)
        {
            for (int i = 0; i < existing.Length; i++)
            {
                object ent = existing.GetValue(i);
                string g = StringProp(ent, "gameIdGuid");
                if (string.Equals(g, guid, StringComparison.OrdinalIgnoreCase)) return;
            }
        }
        int oldCount = existing == null ? 0 : existing.Length;
        Array updated = Array.CreateInstance(entryType, oldCount + 1);
        if (existing != null) Array.Copy(existing, updated, oldCount);
        object newEntry = Activator.CreateInstance(entryType);
        Set(newEntry, "gameIdGuid", guid);
        Set(newEntry, "unlockDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        updated.SetValue(newEntry, oldCount);
        Set(inventory, "entries", updated);
        Console.WriteLine($"Granted entitlement: {guid}");
    }

    private static void GrantStoryCompletion(string missionId)
    {
        if (string.IsNullOrEmpty(missionId)) return;
        if (missionId.StartsWith("story_", StringComparison.OrdinalIgnoreCase))
        {
            AddEntitlement($"ntt_story_reward_{missionId}");
            AddEntitlement($"ntt_story_challenge_{missionId}");

            // Compatibility aliases between old story IDs and new canonical IDs
            if (missionId == "story_forli_01") AddEntitlement("ntt_story_reward_story_0015");
            if (missionId == "story_forli_02") AddEntitlement("ntt_story_reward_story_0016");
            if (missionId == "story_forli_03") AddEntitlement("ntt_story_reward_story_0017");
            if (missionId == "story_0015") AddEntitlement("ntt_story_reward_story_forli_01");
            if (missionId == "story_0016") AddEntitlement("ntt_story_reward_story_forli_02");
            if (missionId == "story_0017") AddEntitlement("ntt_story_reward_story_forli_03");

            if (missionId == "story_crows_10")
            {
                AddEntitlement("ntt_bfc7ed30"); // Completed Campaign Italy 01
                AddEntitlement("ntt_offline_rank1");
                Console.WriteLine("Granted Heroic Rank 1 unlock entitlement!");
            }

            Console.WriteLine($"Granted story mission completion: {missionId}");
        }
    }

    private static void AddProfileReward(long sc, long xp)
    {
        object wallet = Get(_profile, "resources");
        if (wallet != null)
        {
            Array resources = (Array)Get(wallet, "resources");
            if (resources != null)
            {
                for (int i = 0; i < resources.Length; i++)
                {
                    object res = resources.GetValue(i);
                    string t = Get(res, "type")?.ToString() ?? "";
                    if (t.Contains("SoftCurrency"))
                    {
                        long cur = (long)Get(res, "amount");
                        Set(res, "amount", cur + sc);
                        Console.WriteLine($"SoftCurrency updated: {cur} -> {cur + sc}");
                    }
                }
            }
        }
        object assassinsObj = Get(_profile, "assassins");
        Array assassins = assassinsObj is Array directArr ? directArr : (Array)Get(assassinsObj, "assassins");
        if (assassins != null && assassins.Length > 0)
        {
            object ass = assassins.GetValue(0);
            long curXp = (long)Get(ass, "xp");
            Set(ass, "xp", curXp + xp);
            int curLvl = (int)Get(ass, "level");
            if (curXp + xp >= curLvl * 1000L && curLvl < 50)
            {
                Set(ass, "level", curLvl + 1);
                Set(_profile, "level", curLvl + 1);
                Console.WriteLine($"Assassin leveled up to {curLvl + 1}!");
            }
        }
    }

    private static void RotateContractMission(string completedMissionId, int rankIdx)
    {
        object mc = Get(_profile, "missionCollection");
        if (mc == null) return;
        Array ams = (Array)Get(mc, "assassinMissions");
        if (ams == null || ams.Length == 0) return;
        object am = ams.GetValue(0);
        Array rms = (Array)Get(am, "rankMissions");
        if (rms == null || rankIdx < 0 || rankIdx >= rms.Length) return;
        object rm = rms.GetValue(rankIdx);
        Array ms = (Array)Get(rm, "missions");
        if (ms == null) return;

        Type mType = TypeOf("AcierProtocol.Mission");
        Type locType = TypeOf("AcierProtocol.LocationSet");
        Type langType = TypeOf("AcierProtocol.LangKey");

        string[] regions = { "Forli", "Firenze_Palazzo", "Monteriggioni", "Roma_Coloseum" };
        string[] moods = { "Dusk", "Daytime", "Daytime", "Afternoon" };
        string[] cityNames = { "forli", "firenze", "monteriggioni", "roma" };
        int[] titles = { 8395, 6919, 5423, 6735 };

        _contractCounter++;
        for (int i = 0; i < ms.Length; i++)
        {
            object m = ms.GetValue(i);
            string id = StringProp(m, "id");
            if (string.IsNullOrEmpty(completedMissionId) || string.Equals(id, completedMissionId, StringComparison.OrdinalIgnoreCase))
            {
                int cityIdx = (_contractCounter + i) % 4;
                string newId = $"contract_{cityNames[cityIdx]}_r{rankIdx + 1}_{_contractCounter}";
                Set(m, "id", newId);
                Set(m, "baseName", newId);
                Set(m, "wasWon", false);
                Set(m, "rank", rankIdx + 1);
                Set(m, "difficulty", (rankIdx % 5) + 1);

                object loc = Activator.CreateInstance(locType);
                Set(loc, "era", Enum.Parse(TypeOf("AcierProtocol.EEra"), "Italy"));
                Set(loc, "region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), regions[cityIdx]));
                Set(loc, "mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), moods[cityIdx]));
                Set(m, "locationSet", loc);

                object lk = Activator.CreateInstance(langType);
                Set(lk, "neutral", titles[cityIdx]);
                Set(m, "objectiveLangKey", lk);
                Set(m, "missionDescription", lk);
                Set(m, "missionFlavorTextLoca", lk);
                Console.WriteLine($"Rotated contract slot {i}: {id} -> {newId} ({cityNames[cityIdx]})");
                break;
            }
        }
    }

    // The debrief reads challengeLoots and resources immediately. A null list
    // throws while the Animus sync overlay is up, so the mission never ends
    // and the next one stays locked.
    private static object MissionReward(long sc, long xp)
    {
        object reward = New("AcierProtocol.MissionReward");
        Set(reward, "baseSc", sc);
        Set(reward, "finalSc", sc);
        Set(reward, "baseXp", xp);
        Set(reward, "finalXp", xp);
        Set(reward, "scWithoutChallenge", sc);
        Set(reward, "xpWithoutChallenge", xp);
        Set(reward, "scFromHireling", 50L);
        Set(reward, "elements", Array.CreateInstance(TypeOf("AcierProtocol.InventoryElement"), 0));
        Set(reward, "resources", Array.CreateInstance(TypeOf("AcierProtocol.ResourceFromTemplate"), 0));
        Set(reward, "challengeLoots", Array.CreateInstance(TypeOf("AcierProtocol.MissionGoalLoot"), 0));
        Set(reward, "entitlements", Array.Empty<string>());
        return reward;
    }

    private static object ObjectiveResult(string result, string tracker)
    {
        object value = New("AcierProtocol.ObjectiveResult");
        Set(value, "Result", Enum.Parse(TypeOf("AcierProtocol.EObjectiveResult"), result));
        Set(value, "Tracker", Enum.Parse(TypeOf("AcierProtocol.EObjectiveProgress"), tracker));
        return value;
    }

    private static object ActiveBase(long id)
    {
        object basis = New("AcierProtocol.MissionBaseContent");
        Set(basis, "ID", id);
        Set(basis, "ActivateAtId", -2L);
        Set(basis, "RemoveAtId", -2L);
        return basis;
    }

    private static Array RankRewards(string missionName, bool isChallenge)
    {
        Type rankType = null;
        foreach (Type type in _protocol.GetTypes())
        {
            if (type.Name == "RankEntry"
                && type.DeclaringType != null
                && type.DeclaringType.Name == "Entry"
                && type.DeclaringType.DeclaringType != null
                && type.DeclaringType.DeclaringType.Name == "StoryMissionRewardMapping")
            {
                rankType = type;
                break;
            }
        }
        if (rankType == null) throw new TypeLoadException("StoryMissionRewardMapping.Entry.RankEntry");
        Type lootType = TypeOf("AcierProtocol.LootTemplate");
        Type entitlementType = lootType.GetNestedType("EntitlementTemplate");
        const int ranks = 6;
        Array entries = Array.CreateInstance(rankType, ranks);
        string rewardGuid = isChallenge
            ? $"ntt_story_challenge_{missionName}"
            : $"ntt_story_reward_{missionName}";

        for (int i = 0; i < ranks; i++)
        {
            object entitlement = Activator.CreateInstance(entitlementType);
            Set(entitlement, "gameIdGuid", rewardGuid);
            object loot = Activator.CreateInstance(lootType);
            Set(loot, "entitlementTemplate", entitlement);
            Array loots = Array.CreateInstance(lootType, 1);
            loots.SetValue(loot, 0);
            object rank = Activator.CreateInstance(rankType);
            Set(rank, "lootTemplates", loots);
            entries.SetValue(rank, i);
        }
        return entries;
    }

    // Opening a campaign asks, for each mission, whether its reward was already
    // collected. A missing reward entry is a null dereference, so the list never appears.
    private static void RepairStoryRewards(object statics)
    {
        object mapping = New("AcierProtocol.StoryMissionRewardMapping");
        Type entryType = null;
        foreach (Type type in _protocol.GetTypes())
        {
            if (type.Name == "Entry" && type.DeclaringType != null && type.DeclaringType.Name == "StoryMissionRewardMapping")
            {
                entryType = type;
                break;
            }
        }
        if (entryType == null) throw new TypeLoadException("StoryMissionRewardMapping.Entry");
        string[] missions = {
            "story_tutorial_01", "story_tutorial_02", "story_tutorial_03", "story_tutorial_04",
            "story_tutorial_05", "story_tutorial_06", "story_tutorial_07",
            "story_crows_01", "story_crows_02", "story_crows_03", "story_crows_04", "story_crows_05",
            "story_crows_06", "story_crows_07", "story_crows_08", "story_crows_09", "story_crows_10",
            "story_forli_01", "story_forli_02", "story_forli_03", "story_forli_04",
            "story_0015", "story_0016", "story_0017",
            "story_0001", "story_0002", "story_0003",
            "story_monteriggioni_01", "story_monteriggioni_02", "story_monteriggioni_03",
            "story_roma_01", "story_roma_02", "story_roma_03",
            "story_caribbean", "story_tutorial"
        };
        Array entries = Array.CreateInstance(entryType, missions.Length);
        for (int i = 0; i < missions.Length; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "name", missions[i]);
            Set(entry, "completionRewards", RankRewards(missions[i], false));
            Set(entry, "challengeRewards", RankRewards(missions[i], true));
            entries.SetValue(entry, i);
        }
        Set(mapping, "entries", entries);
        Set(statics, "storyMissionRewardMapping", mapping);
        Console.WriteLine("story rewards " + entries.Length);
    }

    // An empty rank table makes "is rank 1 unlocked?" come back true, and the mission
    // screen then crashes while building a rank dropdown with no rows. That click is
    // what happens when a campaign is selected.
    private static void RepairHeroicRanks(object statics)
    {
        object mapping = New("AcierProtocol.HeroicRankMapping");
        Type entryType = Nested("HeroicRankMapping", "Entry");
        const int count = 6;
        Array entries = Array.CreateInstance(entryType, count);
        for (int i = 0; i < count; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "rankIdx", i);
            Set(entry, "entitlement", i == 0 ? "ntt_offline_rank0" : "ntt_offline_rank" + i);
            Set(entry, "missionsScalingMultiplier", 1f);
            Set(entry, "multiplierXp", 1d);
            Set(entry, "multiplierSc", 1d);
            Set(entry, "multiplierHt", 1d);
            entries.SetValue(entry, i);
        }
        Set(mapping, "entries", entries);
        Set(statics, "heroicRankMapping", mapping);
        Console.WriteLine("heroic ranks " + count);
    }

    // These ids are the client's own feature flags. Empty reqEntitlements count as
    // already owned, which is what locks Contracts, Assassins, Shop, Codex and Social.
    private static readonly string[] ProfileEntitlements =
    {
        "ntt_b76f57c1", "ntt_7392aea9", "ntt_87a7298a", "ntt_fde08cf7", "ntt_daa20406",
        "ntt_5bf19ee0", "ntt_7558c49b", "ntt_9d39e06b", "ntt_b8a0d37a",
        "ntt_e26dbe51", "ntt_82f04a94", "ntt_6d0f5b96", "ntt_1600f228", "ntt_6d0c3cd8",
        "ntt_6259b445", "ntt_c7f34488", "ntt_9a6abbfe", "ntt_f272ec1b", "ntt_e7772d93",
        "ntt_79558add", "ntt_04d2fa6f", "ntt_8650c665", "ntt_9412f34d",
        "ntt_cf5a1228", "ntt_f73b0a1b", "ntt_4db01d88",
        "ntt_bc427202", "ntt_de7f3cd7", "ntt_27b8e39d", "ntt_fcca8eae",
        "ntt_8151d3ac", "ntt_dd54e90f", "ntt_b05b0231", "ntt_01ba0a9f",
        "ntt_933dcc14", "ntt_32a9f9b0", "ntt_1cbb2640", "ntt_329b219e",
        "ntt_bfc7ed30",
        "ntt_offline_rank0"
    };

    private static void RepairCampaignCatalog(object statics)
    {
        object catalog = New("AcierProtocol.CampaignCatalog");

        Type campaignType = Nested("CampaignCatalog", "Campaign");
        Type groupType = TypeOf("AcierProtocol.MissionsAtLocation");
        Type infoType = TypeOf("AcierProtocol.MissionInfo");
        Type locationType = TypeOf("AcierProtocol.LocationSet");

        var campaignsDef = new (string id, string name, (string baseName, int titleId, int objectiveId, int briefingId, string era, string region, string mood, string objType, string kind)[] missions)[]
        {
            (
                "ntt_c350c141",
                "Montreal - Animus VR Training",
                new (string, int, int, int, string, string, string, string, string)[]
                {
                    ("story_tutorial_01", 6914, 6910, 6913, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_02", 6916, 6916, 6913, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_03", 6917, 6917, 6922, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_04", 6918, 6918, 6922, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_05", 6694, 6694, 6913, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_06", 6696, 6696, 6913, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                    ("story_tutorial_07", 6695, 6695, 6913, "Italy", "Monteriggioni", "Daytime", "Tutorial_EObjectiveType", "ContractKill_EMissionKind"),
                }
            ),
            (
                "ntt_5572b4db",
                "Italy - A Murder of Crows",
                new (string, int, int, int, string, string, string, string, string)[]
                {
                    ("story_crows_01", 2063, 2066, 2069, "Italy", "Monteriggioni", "Daytime", "Story_EObjectiveType", "Kill_EMissionKind"),
                    ("story_crows_02", 2064, 2067, 2070, "Italy", "Monteriggioni", "Nighttime", "Story_EObjectiveType", "Kill_EMissionKind"),
                    ("story_crows_03", 2065, 2068, 2071, "Italy", "Firenze_Palazzo", "Daytime", "Story_EObjectiveType", "Escort_EMissionKind"),
                    ("story_crows_04", 3330, 3332, 3334, "Italy", "Roma_Coloseum", "Afternoon", "Story_EObjectiveType", "Kill_EMissionKind"),
                    ("story_crows_05", 3331, 3333, 3335, "Italy", "Firenze_SantaCroce", "Sunny", "Story_EObjectiveType", "FreeCaptives_EMissionKind"),
                    ("story_crows_06", 4425, 4423, 4421, "Italy", "Roma_Coloseum", "Foggy", "Story_EObjectiveType", "Kill_EMissionKind"),
                    ("story_crows_07", 3617, 2086, 3615, "Italy", "Firenze_Palazzo", "Daytime", "Story_EObjectiveType", "IdentifyKill_EMissionKind"),
                    ("story_crows_08", 4501, 4497, 4495, "Italy", "Monteriggioni", "Daytime", "Story_EObjectiveType", "Kill_EMissionKind"),
                    ("story_crows_09", 3618, 4543, 3727, "Italy", "Firenze_Palazzo", "Nighttime", "Story_EObjectiveType", "Deliver_EMissionKind"),
                    ("story_crows_10", 4502, 4498, 4490, "Italy", "Roma_Coloseum", "Afternoon", "Story_EObjectiveType", "Kill_EMissionKind"),
                }
            ),
            (
                "ntt_0dd81737",
                "Forlì - A Crimson Sunset",
                new (string, int, int, int, string, string, string, string, string)[]
                {
                    ("story_forli_01", 5259, 6507, 5646, "Italy", "Forli", "Dusk", "Story_EObjectiveType", "CrowHunt_EMissionKind"),
                    ("story_forli_02", 5261, 6514, 6529, "Italy", "Forli", "Dusk", "Story_EObjectiveType", "CrowHunt_EMissionKind"),
                    ("story_forli_03", 8168, 8357, 8362, "Italy", "Forli", "Siege", "Story_EObjectiveType", "FreeCaptives_EMissionKind"),
                    ("story_forli_04", 8169, 5660, 8363, "Italy", "Forli", "Dusk", "Story_EObjectiveType", "Escort_EMissionKind"),
                }
            )
        };

        Array campaigns = Array.CreateInstance(campaignType, campaignsDef.Length);
        string[] none = Array.Empty<string>();

        for (int i = 0; i < campaignsDef.Length; i++)
        {
            var cDef = campaignsDef[i];
            object campaign = Activator.CreateInstance(campaignType);
            Set(campaign, "id", cDef.id);
            Set(campaign, "name", cDef.name);
            Set(campaign, "sortIndex", i);
            Set(campaign, "hidden", false);
            Set(campaign, "reqEntitlements", none);

            Array groups = Array.CreateInstance(groupType, cDef.missions.Length);
            for (int m = 0; m < cDef.missions.Length; m++)
            {
                var mDef = cDef.missions[m];

                object location = Activator.CreateInstance(locationType);
                Set(location, "era", Enum.Parse(TypeOf("AcierProtocol.EEra"), mDef.era));
                Set(location, "region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), mDef.region));
                Set(location, "mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), mDef.mood));

                object info = Activator.CreateInstance(infoType);
                FillNulls(info, 2);
                Set(info, "baseName", mDef.baseName);

                object title = Get(info, "objectiveLangKey");
                if (title == null)
                {
                    title = New("AcierProtocol.LangKey");
                    Set(info, "objectiveLangKey", title);
                }
                Set(title, "neutral", mDef.titleId);
                Set(info, "missionDescription", mDef.objectiveId);
                Set(info, "missionFlavorTextLoca", mDef.briefingId);
                Set(info, "objectiveBaseType", Enum.Parse(TypeOf("AcierProtocol.EObjectiveType"), mDef.objType));
                Set(info, "missionKind", Enum.Parse(TypeOf("AcierProtocol.EMissionKind"), mDef.kind));

                // All story missions unlocked for offline play so user can freely play any story mission
                Set(info, "reqEntitlements", none);
                Set(info, "sortIndex", m);
                Set(info, "rank", 1);
                Set(info, "difficulty", m + 1);

                Array infos = Array.CreateInstance(infoType, 1);
                infos.SetValue(info, 0);

                object group = Activator.CreateInstance(groupType);
                Set(group, "location", location);
                Set(group, "missions", infos);
                groups.SetValue(group, m);
            }

            Set(campaign, "missions", groups);
            campaigns.SetValue(campaign, i);
        }

        Set(catalog, "campaigns", campaigns);
        Set(statics, "campaignCatalog", catalog);
        Console.WriteLine("campaigns " + campaigns.Length);
    }

    // InitFromProfile looks up every tutorial category. An empty mapping makes that lookup
    // return null, and the next property read throws, which leaves the Connecting screen up.
    private static void RepairTutorialMapping(object statics)
    {
        object mapping = Get(statics, "tutorialCategoryMapping");
        if (mapping == null)
        {
            mapping = New("AcierProtocol.TutorialCategoryMapping");
            Set(statics, "tutorialCategoryMapping", mapping);
        }
        Type entryType = Nested("TutorialCategoryMapping", "Entry");
        Type categoryEnum = TypeOf("AcierProtocol.ETutorialCategory");
        Array values = Enum.GetValues(categoryEnum);
        Array entries = Array.CreateInstance(entryType, values.Length);
        // Empty granted entitlements count as already owned, so the intro mission
        // (Missions/tutorial_0001.bin, which is not in this cache) is skipped and the
        // built-in Animus scene loads instead.
        string[] finished = Array.Empty<string>();
        for (int i = 0; i < values.Length; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "tutorialCategory", values.GetValue(i));
            Set(entry, "grantedEntitlements", finished);
            Set(entry, "reqEntitlements", finished);
            entries.SetValue(entry, i);
        }
        Set(mapping, "entries", entries);
        Console.WriteLine("tutorial mapping entries " + entries.Length);
    }

    private static void RepairProfile()
    {
        FillNulls(_profile, 6);
        object tutorial = Get(_profile, "tutorial");
        Type categoryType = Nested("Tutorial", "Category");
        Type categoryEnum = TypeOf("AcierProtocol.ETutorialCategory");
        Array values = Enum.GetValues(categoryEnum);
        Array categories = Array.CreateInstance(categoryType, values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            object category = Activator.CreateInstance(categoryType);
            Set(category, "category", values.GetValue(i));
            Set(category, "counter", 0);
            categories.SetValue(category, i);
        }
        Set(tutorial, "categories", categories);
        GrantEntitlements();
        GrantCurrency();
        GrantAssassin();
        GrantAssassin();
        GrantMissionCollection();
        object inventory = Get(_profile, "Inventory");
        if (inventory == null)
        {
            inventory = New("AcierProtocol.Inventory");
            Set(_profile, "Inventory", inventory);
        }
        // Free space under 3 makes Start stop on the "inventory is full" warning.
        Set(inventory, "maxSize", 50);
        Set(_profile, "userIdHash", "acid-offline-player");
        Console.WriteLine("profile tutorial categories " + categories.Length);
        SaveProfile();
    }

    private static void GrantEntitlements()
    {
        object inventory = Get(_profile, "entitlementInventory");
        if (inventory == null)
        {
            inventory = New("AcierProtocol.EntitlementInventory");
            Set(_profile, "entitlementInventory", inventory);
        }
        foreach (string ent in ProfileEntitlements)
        {
            AddEntitlement(ent);
        }
    }

    // The hire screen hides its start button until a real assassin and hireling exist.
    // With neither, it stays on the sample card and there is nothing to press.
    private static void GrantAssassin()
    {
        Type assassinType = TypeOf("AcierProtocol.Assassin");
        Type classEnum = assassinType.GetNestedType("EClassTypeId");
        Type hirelingType = TypeOf("AcierProtocol.HirelingAssassin");
        const string body = "de4ea1c3-3976-48e9-aee4-1e48ad8cb249";
        const string head = "77d5f907-1c42-41bd-a94e-283d1a274fff";
        // The hidden-blade id already stored in the game database. A missing weapon
        // id throws while the mission open is applying the assassin.
        const string weapon = "7d63cb2a-eecd-4e8c-bf8d-cdfe658d3faf";
        // Portrait lookup ignores the body and head unless a color id is present too.
        const string color = "e48f7215-6267-4e70-b49d-6ece221c89db";
        object classId = Enum.Parse(classEnum, "Berserker");
        object noSecondClass = Enum.Parse(classEnum, "None");

        object assassin = Activator.CreateInstance(assassinType);
        Set(assassin, "id", "acid-offline-assassin");
        Set(assassin, "classTypeId", classId);
        // The default class value is not None, and a second class with no skill
        // list throws while the mission is being prepared.
        Set(assassin, "extendedClassTypeId", noSecondClass);
        Set(assassin, "level", 1);
        // The contract copies these straight into the assassin's health and combat
        // stats. Left at zero, he is already dead when the intro ends, so the
        // stay-alive check fails the mission and the city never appears.
        Set(assassin, "hitPoints", 120L);
        Set(assassin, "strength", 25L);
        Set(assassin, "precision", 25L);
        Set(assassin, "baseWeapon", 23L);
        Set(assassin, "armoreRating", 13L);
        Set(assassin, "incognito", 20L);
        Set(assassin, "evasion", 10L);
        Set(assassin, "itemPower", 10L);
        Set(assassin, "humanoidGuid", body);
        Set(assassin, "headGuid", head);
        Set(assassin, "weaponGuid", weapon);
        Set(assassin, "colorSetting", color);
        Set(assassin, "equippedElements", Array.CreateInstance(TypeOf("AcierProtocol.EquippedElement"), 0));
        object assassinName = New("AcierProtocol.AssassinName");
        Set(assassinName, "overrideName", "Assassin");
        Set(assassin, "assassinName", assassinName);

        object hireling = Activator.CreateInstance(hirelingType);
        Set(hireling, "hirelingId", "hireling-1");
        Set(hireling, "hirelingType", Enum.Parse(hirelingType.GetNestedType("EHirelingType"), "RandomGenerated"));
        Set(hireling, "classTypeId", classId);
        Set(hireling, "userName", "Offline");
        Set(hireling, "itemPower", 10);
        Set(hireling, "level", 1);
        Set(hireling, "humanoidGuid", body);
        Set(hireling, "headGuid", head);
        Set(hireling, "weaponGuid", weapon);
        Set(hireling, "colorSetting", color);
        object hirelingName = New("AcierProtocol.AssassinName");
        Set(hirelingName, "overrideName", "Alcee d'Estee");
        Set(hireling, "assassinName", hirelingName);
        object hirelings = New("AcierProtocol.HirelingAssassins");
        Array hirelingList = Array.CreateInstance(hirelingType, 1);
        hirelingList.SetValue(hireling, 0);
        Set(hirelings, "hirelingAssassins", hirelingList);
        Set(assassin, "hirelingAssassins", hirelings);

        object roster = New("AcierProtocol.Assassins");
        Array assassins = Array.CreateInstance(assassinType, 1);
        assassins.SetValue(assassin, 0);
        Set(roster, "assassins", assassins);
        Set(_profile, "assassins", roster);
        Set(_profile, "lastUserAssassionId", "acid-offline-assassin");
        if ((long)Get(assassin, "hitPoints") < 1L) throw new InvalidOperationException("assassin has no health");
        Console.WriteLine("assassin 1 hirelings 1 health " + Get(assassin, "hitPoints"));
    }

    private static void GrantMissionCollection()
    {
        object mc = New("AcierProtocol.MissionCollection");
        Type amType = Nested("MissionCollection", "AssassinMissions");
        Type rmType = Nested("MissionCollection", "RankMissions");
        Type mType = TypeOf("AcierProtocol.Mission");
        Type locType = TypeOf("AcierProtocol.LocationSet");
        Type langType = TypeOf("AcierProtocol.LangKey");

        object am = Activator.CreateInstance(amType);
        Set(am, "assassinId", "acid-offline-assassin");

        string[] regions = { "Forli", "Firenze_Palazzo", "Monteriggioni", "Roma_Coloseum" };
        string[] moods = { "Dusk", "Daytime", "Daytime", "Afternoon" };
        string[] cityNames = { "forli", "firenze", "monteriggioni", "roma" };
        int[] titles = { 8395, 6919, 5423, 6735 };

        Array rms = Array.CreateInstance(rmType, 6);
        for (int r = 0; r < 6; r++)
        {
            object rm = Activator.CreateInstance(rmType);
            Set(rm, "rankIdx", r);

            Array ms = Array.CreateInstance(mType, 4);
            for (int i = 0; i < 4; i++)
            {
                object m = Activator.CreateInstance(mType);
                string mid = $"contract_{cityNames[i]}_r{r + 1}_{i + 1}";
                Set(m, "id", mid);
                Set(m, "baseName", mid);
                Set(m, "rank", r + 1);
                Set(m, "difficulty", (r % 5) + 1);
                Set(m, "type", Enum.Parse(TypeOf("AcierProtocol.ERuntimeMissionType"), "RandomMission"));
                Set(m, "objectiveBaseType", Enum.Parse(TypeOf("AcierProtocol.EObjectiveType"), "Kill_EObjectiveType"));
                Set(m, "objectiveType", "Kill");
                Set(m, "missionKind", Enum.Parse(TypeOf("AcierProtocol.EMissionKind"), "ContractKill_EMissionKind"));
                Set(m, "wasWon", false);
                Set(m, "eliteMission", false);

                object loc = Activator.CreateInstance(locType);
                Set(loc, "era", Enum.Parse(TypeOf("AcierProtocol.EEra"), "Italy"));
                Set(loc, "region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), regions[i]));
                Set(loc, "mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), moods[i]));
                Set(m, "locationSet", loc);

                object lk = Activator.CreateInstance(langType);
                Set(lk, "neutral", titles[i]);
                Set(m, "objectiveLangKey", lk);
                Set(m, "missionDescription", lk);
                Set(m, "missionFlavorTextLoca", lk);

                ms.SetValue(m, i);
            }
            Set(rm, "missions", ms);
            rms.SetValue(rm, r);
        }

        Set(am, "rankMissions", rms);
        Array ams = Array.CreateInstance(amType, 1);
        ams.SetValue(am, 0);
        Set(mc, "assassinMissions", ams);
        Set(_profile, "missionCollection", mc);
        Console.WriteLine("mission collection granted (6 ranks x 4 contracts)");
    }

    private static void GrantCurrency()
    {
        object wallet = Get(_profile, "resources");
        Type resourceType = TypeOf("AcierProtocol.Resource");
        Type kind = TypeOf("AcierProtocol.EResourceType");
        Array items = Array.CreateInstance(resourceType, 2);
        items.SetValue(MakeResource(resourceType, kind, "SoftCurrency", 5000), 0);
        items.SetValue(MakeResource(resourceType, kind, "HardCurrency", 100), 1);
        Set(wallet, "resources", items);
    }

    private static object MakeResource(Type resourceType, Type kind, string name, long amount)
    {
        object resource = Activator.CreateInstance(resourceType);
        Set(resource, "type", Enum.Parse(kind, name));
        Set(resource, "amount", amount);
        return resource;
    }

    private static Type Nested(string parent, string name)
    {
        foreach (Type type in _protocol.GetTypes())
        {
            if (type.Name == name && type.DeclaringType != null && type.DeclaringType.Name == parent)
            {
                return type;
            }
        }
        throw new TypeLoadException(parent + "." + name);
    }

    private static void FillNulls(object value, int depth)
    {
        if (value == null || depth < 0) return;
        foreach (PropertyInfo property in value.GetType().GetProperties())
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0) continue;
            Type type = property.PropertyType;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum) continue;
            object current;
            try { current = property.GetValue(value); }
            catch { continue; }
            if (type.IsArray)
            {
                if (current == null) property.SetValue(value, Array.CreateInstance(type.GetElementType(), 0));
            }
            else if (type.IsClass && type.Name != "ExtensionObject" && type.GetConstructor(Type.EmptyTypes) != null)
            {
                if (current == null)
                {
                    current = Activator.CreateInstance(type);
                    property.SetValue(value, current);
                }
                if (depth > 0) FillNulls(current, depth - 1);
            }
        }
    }

    private static void LoadOrCreateProfile()
    {
        string path = Path.Combine(_saveDir, "profile.bin");
        Type profileType = TypeOf("AcierProtocol.UserProfile");
        if (File.Exists(path))
        {
            _profile = _deserialize.Invoke(_serializer, new object[] { File.OpenRead(path), null, profileType });
            _sessionToken = File.Exists(Path.Combine(_saveDir, "session.txt"))
                ? File.ReadAllText(Path.Combine(_saveDir, "session.txt")).Trim()
                : _sessionToken;
            return;
        }
        _profile = New("AcierProtocol.UserProfile");
        Fill(_profile, 1);
        Set(_profile, "version", 1d);
        Set(_profile, "id", Guid.NewGuid().ToString("N"));
        Set(_profile, "userName", "Assassin");
        Set(_profile, "level", 1);
        Set(_profile, "pirate", false);
        Set(_profile, "maxFriends", 10);
        SaveProfile();
    }

    private static void SaveProfile()
    {
        var output = new MemoryStream();
        _serialize.Invoke(_serializer, new object[] { output, _profile });
        File.WriteAllBytes(Path.Combine(_saveDir, "profile.bin"), output.ToArray());
        File.WriteAllText(Path.Combine(_saveDir, "session.txt"), _sessionToken);
    }

    private static void ExportCompanion(string outDir)
    {
        Directory.CreateDirectory(outDir);
        Host = "127.0.0.1";
        CdnUrl = "http://127.0.0.1:8080/cdn/ACID_2.8.2_373";
        ApiUrl = "http://127.0.0.1:8080/acier/v1/api";

        void SaveBin(string name, object resp)
        {
            var ms = new MemoryStream();
            _serialize.Invoke(_serializer, new object[] { ms, resp });
            File.WriteAllBytes(Path.Combine(outDir, name), ms.ToArray());
            Console.WriteLine($"Exported {name} ({ms.Length} bytes)");
        }

        // 1. Core Service Responses
        object dReq = New("AcierProtocol.Request");
        Set(dReq, "discoveryServiceRequest", New("AcierProtocol.DiscoveryServiceRequest"));
        SaveBin("discovery.bin", BuildResponse(dReq, "discoveryServiceRequest"));

        object vReq = New("AcierProtocol.Request");
        Set(vReq, "checkVersionRequest", New("AcierProtocol.CheckVersionRequest"));
        SaveBin("checkVersion.bin", BuildResponse(vReq, "checkVersionRequest"));

        // 1A. Export FRESH Profile (Story Mode - Only Tutorial 1 Unlocked)
        ResetProfileToFresh();
        object aReqFresh = New("AcierProtocol.Request");
        Set(aReqFresh, "authAccountRequest", New("AcierProtocol.AuthAccountRequest"));
        SaveBin("auth_fresh.bin", BuildResponse(aReqFresh, "authAccountRequest"));

        object eReqFresh = New("AcierProtocol.Request");
        Set(eReqFresh, "endMissionRequest", New("AcierProtocol.EndMissionRequest"));
        SaveBin("endMission_fresh.bin", BuildResponse(eReqFresh, "endMissionRequest"));

        // 1B. Export UNLOCKED Profile (All Campaigns & All Missions Unlocked)
        AddEntitlement("ntt_5572b4db"); // A Murder of Crows
        AddEntitlement("ntt_0dd81737"); // Forli - A Crimson Sunset
        AddEntitlement("ntt_bfc7ed30"); // Completed Campaign Italy 01
        AddEntitlement("ntt_offline_rank1"); // Heroic Rank 1
        string[] allStoryMissions = new string[]
        {
            "story_tutorial_01", "story_tutorial_02", "story_tutorial_03",
            "story_tutorial_04", "story_tutorial_05", "story_tutorial_06", "story_tutorial_07",
            "story_crows_01", "story_crows_02", "story_crows_03", "story_crows_04", "story_crows_05",
            "story_crows_06", "story_crows_07", "story_crows_08", "story_crows_09", "story_crows_10",
            "story_forli_01", "story_forli_02", "story_forli_03", "story_forli_04",
            "story_0015", "story_0016", "story_0017"
        };
        foreach (var sm in allStoryMissions)
        {
            AddEntitlement($"ntt_story_reward_{sm}");
            AddEntitlement($"ntt_story_challenge_{sm}");
        }

        Set(_profile, "id", "acid-offline-unlocked");
        Set(_profile, "level", 26);
        object aReq = New("AcierProtocol.Request");
        Set(aReq, "authAccountRequest", New("AcierProtocol.AuthAccountRequest"));
        object authUnlocked = BuildResponse(aReq, "authAccountRequest");
        SaveBin("auth_unlocked.bin", authUnlocked);
        SaveBin("auth.bin", authUnlocked);

        object sReq = New("AcierProtocol.Request");
        Set(sReq, "getStaticsRequest", New("AcierProtocol.GetStaticsRequest"));
        SaveBin("statics.bin", BuildResponse(sReq, "getStaticsRequest"));

        object qReq = New("AcierProtocol.Request");
        Set(qReq, "getQualitySettingsRequest", New("AcierProtocol.GetQualitySettingsRequest"));
        SaveBin("quality.bin", BuildResponse(qReq, "getQualitySettingsRequest"));

        object tReq = New("AcierProtocol.Request");
        Set(tReq, "getTwitterTimelineRequest", New("AcierProtocol.GetTwitterTimelineRequest"));
        SaveBin("twitter.bin", BuildResponse(tReq, "getTwitterTimelineRequest"));

        object eReq = New("AcierProtocol.Request");
        Set(eReq, "endMissionRequest", New("AcierProtocol.EndMissionRequest"));
        object endUnlocked = BuildResponse(eReq, "endMissionRequest");
        SaveBin("endMission_unlocked.bin", endUnlocked);
        SaveBin("endMission.bin", endUnlocked);

        // 2. Missions
        string[] missions = new string[]
        {
            "story_tutorial_01", "story_tutorial_02", "story_tutorial_03",
            "story_tutorial_04", "story_tutorial_05", "story_tutorial_06", "story_tutorial_07",
            "story_crows_01", "story_crows_02", "story_crows_03", "story_crows_04", "story_crows_05",
            "story_crows_06", "story_crows_07", "story_crows_08", "story_crows_09", "story_crows_10",
            "story_forli_01", "story_forli_02", "story_forli_03", "story_forli_04",
            "story_0015", "story_0016", "story_0017", "default"
        };

        foreach (string m in missions)
        {
            string actualMid = m == "default" ? "story_0015" : m;
            // GetMissionInfo
            object mInfoReq = New("AcierProtocol.Request");
            object infoReqObj = New("AcierProtocol.GetMissionInfoRequest");
            Set(infoReqObj, "missionId", actualMid);
            Set(mInfoReq, "getMissionInfoRequest", infoReqObj);
            SaveBin($"getMissionInfo_{m}.bin", BuildResponse(mInfoReq, "getMissionInfoRequest"));

            // OpenMission
            object mOpenReq = New("AcierProtocol.Request");
            object openReqObj = New("AcierProtocol.OpenMissionRequest");
            Set(openReqObj, "missionId", actualMid);
            Set(mOpenReq, "openMissionRequest", openReqObj);
            SaveBin($"openMission_{m}.bin", BuildResponse(mOpenReq, "openMissionRequest"));

            // EndMission (won = true)
            object mEndReq = New("AcierProtocol.Request");
            object endReqObj = New("AcierProtocol.EndMissionRequest");
            Set(endReqObj, "won", true);
            Set(mEndReq, "endMissionRequest", endReqObj);
            _activeMissionId = actualMid;
            SaveBin($"endMission_{m}.bin", BuildResponse(mEndReq, "endMissionRequest"));
        }

        // 3. Catalogue JSON
        var catJson = new StringBuilder();
        catJson.AppendLine("{");
        int idx = 0;
        foreach (var kvp in Catalogue)
        {
            idx++;
            string comma = idx < Catalogue.Count ? "," : "";
            catJson.AppendLine($"  \"{kvp.Key}\": {{\"id\": \"{kvp.Value.Id}\", \"zipped\": {(kvp.Value.Zipped ? "true" : "false")}}}{comma}");
        }
        catJson.AppendLine("}");
        File.WriteAllText(Path.Combine(outDir, "catalogue.json"), catJson.ToString());
        Console.WriteLine($"Exported catalogue.json with {Catalogue.Count} assets");
    }

    private static void BuildCustomMissionFromJson(string jsonPath, string outDir)
    {
        if (!File.Exists(jsonPath))
        {
            Console.WriteLine($"Error: Mission JSON not found at {jsonPath}");
            return;
        }

        string json = File.ReadAllText(jsonPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        string missionId = root.TryGetProperty("missionId", out var midEl) ? midEl.GetString() : "story_custom_001";
        string era = root.TryGetProperty("era", out var eraEl) ? eraEl.GetString() : "Italy";
        string region = root.TryGetProperty("region", out var regEl) ? regEl.GetString() : "Monteriggioni";
        string mood = root.TryGetProperty("mood", out var moodEl) ? moodEl.GetString() : "Daytime";
        string scene = root.TryGetProperty("scene", out var scEl) ? scEl.GetString() : "MonteRiggioni_DayTime";
        string locationName = root.TryGetProperty("locationName", out var locEl) ? locEl.GetString() : "Monteriggioni";

        Type eraType = TypeOf("AcierProtocol.EEra");
        Type regionType = TypeOf("AcierProtocol.ERegion");
        Type moodType = TypeOf("AcierProtocol.EMood");

        if (!Enum.IsDefined(eraType, era)) era = "Italy";
        if (!Enum.IsDefined(regionType, region)) region = "Monteriggioni";
        if (!Enum.IsDefined(moodType, mood)) mood = "Daytime";

        float playerX = 0f, playerY = 0.5f, playerZ = 0f, playerRot = 0f;
        if (root.TryGetProperty("playerSpawn", out var spEl))
        {
            if (spEl.TryGetProperty("x", out var xEl)) playerX = xEl.GetSingle();
            if (spEl.TryGetProperty("y", out var yEl)) playerY = yEl.GetSingle();
            if (spEl.TryGetProperty("z", out var zEl)) playerZ = zEl.GetSingle();
            if (spEl.TryGetProperty("rot", out var rEl)) playerRot = rEl.GetSingle();
        }

        string objectiveKind = "Kill";
        string goalType = "Kill";
        int descriptionLocaId = 3430;
        if (root.TryGetProperty("objectiveKind", out var objKindEl))
        {
            objectiveKind = objKindEl.GetString();
            goalType = objectiveKind == "Interaction" ? "NpcInteraction" : "Kill";
        }

        var npcList = new List<(string name, long id, string guid, float x, float y, float z, float rot, bool isEnemy)>();
        var targetIdList = new List<long>();
        long currentId = 101L;

        if (root.TryGetProperty("targets", out var targetsEl) && targetsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var t in targetsEl.EnumerateArray())
            {
                string name = t.TryGetProperty("name", out var nEl) ? nEl.GetString() : $"Target_{currentId}";
                string guid = t.TryGetProperty("guid", out var gEl) ? gEl.GetString() : "ntt_bd49bc5f";
                float x = t.TryGetProperty("x", out var xEl) ? xEl.GetSingle() : 0f;
                float y = t.TryGetProperty("y", out var yEl) ? yEl.GetSingle() : 0.5f;
                float z = t.TryGetProperty("z", out var zEl) ? zEl.GetSingle() : 10f;
                float rot = t.TryGetProperty("rot", out var rEl) ? rEl.GetSingle() : 180f;
                bool isTarget = !t.TryGetProperty("isTarget", out var itEl) || itEl.GetBoolean();

                npcList.Add((name, currentId, guid, x, y, z, rot, isTarget));
                if (isTarget)
                {
                    targetIdList.Add(currentId);
                }
                currentId++;
            }
        }

        if (targetIdList.Count == 0)
        {
            targetIdList.Add(101L);
            npcList.Add(("Default_Target", 101L, "ntt_bd49bc5f", 0f, 0.5f, 10f, 180f, true));
        }

        int targetCount = targetIdList.Count;
        long[] targetIds = targetIdList.ToArray();
        var npcs = npcList.ToArray();

        object file = AssembleMissionFile(missionId, era, region, mood, scene, locationName, playerX, playerY, playerZ, playerRot, targetIds, npcs, descriptionLocaId, objectiveKind, targetCount);
        object goals = CreateMissionGoals(targetCount, descriptionLocaId, goalType);

        Directory.CreateDirectory(outDir);

        // 1. openMission_{missionId}.bin
        object openResp = New("AcierProtocol.Response");
        object opened = New("AcierProtocol.OpenMissionResponse");
        object ticket = New("AcierProtocol.MissionToken");
        Set(ticket, "token", "offline-mission");
        Set(ticket, "missionId", missionId);
        Set(ticket, "assassinId", "acid-offline-assassin");
        Set(ticket, "hirelingId", "hireling-1");
        Set(ticket, "missionFile", file);
        Set(ticket, "missionGoals", goals);
        Set(ticket, "eliteMission", false);
        Set(opened, "missionToken", ticket);
        Set(openResp, "openMissionResponse", opened);
        using (var fs = File.Create(Path.Combine(outDir, $"openMission_{missionId}.bin")))
        {
            _serialize.Invoke(_serializer, new object[] { fs, openResp });
        }

        // 2. getMissionInfo_{missionId}.bin
        long coins = 800L, xp = 1500L;
        if (root.TryGetProperty("rewards", out var rewEl))
        {
            if (rewEl.TryGetProperty("coins", out var cEl)) coins = cEl.GetInt64();
            if (rewEl.TryGetProperty("xp", out var xEl)) xp = xEl.GetInt64();
        }
        object infoResp = New("AcierProtocol.Response");
        object info = New("AcierProtocol.GetMissionInfoResponse");
        Set(info, "missionRewardPreview", MissionReward(coins, xp));
        Set(info, "missionGoals", goals);
        Set(infoResp, "getMissionInfoResponse", info);
        using (var fs = File.Create(Path.Combine(outDir, $"getMissionInfo_{missionId}.bin")))
        {
            _serialize.Invoke(_serializer, new object[] { fs, infoResp });
        }

        // 3. endMission_{missionId}.bin
        object endRespObj = New("AcierProtocol.Response");
        object ended = New("AcierProtocol.EndMissionResponse");
        object endReward = MissionReward(coins, xp);
        Set(endReward, "entitlements", new string[] { $"ntt_story_reward_{missionId}", $"ntt_story_challenge_{missionId}" });
        Set(ended, "missionReward", endReward);
        Set(endRespObj, "endMissionResponse", ended);
        Set(endRespObj, "userProfile", _profile);
        using (var fs = File.Create(Path.Combine(outDir, $"endMission_{missionId}.bin")))
        {
            _serialize.Invoke(_serializer, new object[] { fs, endRespObj });
        }

        Console.WriteLine($"Successfully built custom mission '{missionId}' into {outDir}!");
    }

    private static void SelfTest()
    {
        object statics = Statics();
        object catalog = Get(statics, "campaignCatalog");
        Array campaigns = (Array)Get(catalog, "campaigns");
        Console.WriteLine($"SelfTest Campaigns: {campaigns.Length}");
        for (int i = 0; i < campaigns.Length; i++)
        {
            object c = campaigns.GetValue(i);
            string cName = (string)Get(c, "name");
            Array groups = (Array)Get(c, "missions");
            Console.WriteLine($"Campaign {i}: {cName} ({groups.Length} missions)");
            for (int m = 0; m < groups.Length; m++)
            {
                object g = groups.GetValue(m);
                Array infos = (Array)Get(g, "missions");
                object info = infos.GetValue(0);
                string baseName = (string)Get(info, "baseName");
                object title = Get(info, "objectiveLangKey");
                int titleLoca = title != null ? (int)Get(title, "neutral") : 0;
                int descLoca = (int)Get(info, "missionDescription");
                int briefLoca = (int)Get(info, "missionFlavorTextLoca");
                object kind = Get(info, "missionKind");
                object objType = Get(info, "objectiveBaseType");
                if (m < 2 || m == groups.Length - 1)
                {
                    Console.WriteLine($"  [{m}] {baseName}: Title={titleLoca}, Obj={descLoca}, Brief={briefLoca}, Kind={kind}, Type={objType}");
                }
            }
        }
        object c1Req = New("AcierProtocol.Request");
        object c1InfoReq = New("AcierProtocol.GetMissionInfoRequest");
        Set(c1InfoReq, "missionId", "story_crows_01");
        Set(c1Req, "getMissionInfoRequest", c1InfoReq);
        object c1Resp = BuildResponse(c1Req, "getMissionInfoRequest");
        object c1Goals = Get(Get(c1Resp, "getMissionInfoResponse"), "missionGoals");
        Array c1GoalList = (Array)Get(c1Goals, "missionGoals");
        object c1G0 = c1GoalList.GetValue(0);
        object c1Desc = Get(c1G0, "description");
        int c1Neutral = (int)Get(c1Desc, "neutral");
        Console.WriteLine($"SelfTest story_crows_01 Goal Loca: {c1Neutral} (Expected: 2066)");
        if (c1Neutral != 2066) throw new InvalidOperationException("Goal neutral mismatch!");
        object endRequest = New("AcierProtocol.Request");
        Set(endRequest, "endMissionRequest", New("AcierProtocol.EndMissionRequest"));
        object endResponse = BuildResponse(endRequest, "endMissionRequest");
        object checkedReward = Get(Get(endResponse, "endMissionResponse"), "missionReward");
        if (Get(checkedReward, "challengeLoots") == null || Get(checkedReward, "resources") == null || Get(checkedReward, "elements") == null)
            throw new InvalidOperationException("mission reward lists are null");
        if (!Catalogue.ContainsKey("FirstContact_Android.xml")) throw new InvalidOperationException("catalogue missing FirstContact");
        if (!Catalogue.ContainsKey("GameDB_AndroidETC2.bin")) throw new InvalidOperationException("catalogue missing GameDB");
        object request = New("AcierProtocol.Request");
        Set(request, "discoveryServiceRequest", New("AcierProtocol.DiscoveryServiceRequest"));
        var bytes = new MemoryStream();
        _serialize.Invoke(_serializer, new object[] { bytes, request });
        object decoded = _deserialize.Invoke(_serializer, new object[] { new MemoryStream(bytes.ToArray()), null, TypeOf("AcierProtocol.Request") });
        if (NestedRequest(decoded) != "discoveryServiceRequest") throw new InvalidOperationException("request roundtrip failed");
        object response = BuildResponse(decoded, "discoveryServiceRequest");
        var packed = new MemoryStream();
        _serialize.Invoke(_serializer, new object[] { packed, response });
        object back = _deserialize.Invoke(_serializer, new object[] { new MemoryStream(packed.ToArray()), null, TypeOf("AcierProtocol.Response") });
        object discovery = Get(back, "discoveryServiceResponse");
        if ((string)Get(discovery, "cdnUrl") != CdnUrl) throw new InvalidOperationException("cdn url mismatch");
        if ((string)Get(discovery, "serverUrl") != ApiUrl) throw new InvalidOperationException("api url mismatch");
        object versionRequest = New("AcierProtocol.Request");
        Set(versionRequest, "checkVersionRequest", New("AcierProtocol.CheckVersionRequest"));
        object versionResponse = BuildResponse(versionRequest, "checkVersionRequest");
        if ((bool)Get(Get(versionResponse, "checkVersionResponse"), "needClientUpdate")) throw new InvalidOperationException("version wants an update");
        object missionRequest = New("AcierProtocol.Request");
        Set(missionRequest, "getMissionInfoRequest", New("AcierProtocol.GetMissionInfoRequest"));
        object missionResponse = BuildResponse(missionRequest, "getMissionInfoRequest");
        object goals = Get(Get(missionResponse, "getMissionInfoResponse"), "missionGoals");
        if (Get(goals, "missionGoals") == null) throw new InvalidOperationException("mission goals missing");
        object openRequest = New("AcierProtocol.Request");
        Set(openRequest, "openMissionRequest", New("AcierProtocol.OpenMissionRequest"));
        object openResponse = BuildResponse(openRequest, "openMissionRequest");
        var openPacked = new MemoryStream();
        _serialize.Invoke(_serializer, new object[] { openPacked, openResponse });
        object openBack = _deserialize.Invoke(_serializer, new object[] { new MemoryStream(openPacked.ToArray()), null, TypeOf("AcierProtocol.Response") });
        object opened = Get(Get(Get(openBack, "openMissionResponse"), "missionToken"), "missionFile");
        object root = Get(opened, "RootNode");
        Array components = (Array)Get(root, "Components");
        if (components == null || components.Length < 3) throw new InvalidOperationException("mission root component missing");
        object content = Get(components.GetValue(0), "MissionRootContent");
        if (Get(components.GetValue(1), "MissionHotspotsContent") == null) throw new InvalidOperationException("mission hotspots missing");
        if ((string)Get(content, "Scene") != "Forli_dusk") throw new InvalidOperationException("mission scene missing");
        if (Get(content, "MissionBaseContent") == null) throw new InvalidOperationException("mission base missing");
        if (Get(openBack, "error") != null) throw new InvalidOperationException("open mission response has an error");
        Array children = (Array)Get(root, "Children");
        if (children == null || children.Length < 1) throw new InvalidOperationException("mission player spawn missing");
        Array spawnComponents = (Array)Get(children.GetValue(0), "Components");
        object spawnContent = Get(spawnComponents.GetValue(0), "MissionPlayerSpawnContent");
        object spawnBase = Get(spawnContent, "MissionBaseContent");
        if ((long)Get(spawnBase, "ActivateAtId") != -2L) throw new InvalidOperationException("player spawn is not active");
        Console.WriteLine("mission file " + openPacked.Length + " bytes, components " + components.Length + ", children " + children.Length);
        try
        {
            string managed = Path.Combine(FindProjectRoot(), "work", "apk", "assets", "bin", "Data", "Managed");
            AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(_protocol);
            Assembly game = context.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
            Type nodeType = game.GetType("Assets.Scripts.Network.Missions.Mission.Serialization.SerializedNode");
            object runtime = Activator.CreateInstance(nodeType, root);
            object list = nodeType.GetMethod("ComponentsRecursive").Invoke(runtime, null);
            int count = (int)list.GetType().GetProperty("Count").GetValue(list);
            Console.WriteLine("mission runtime components " + count);
            bool killOk = false;
            foreach (object item in (System.Collections.IEnumerable)list)
            {
                object data = item.GetType().GetProperty("NodeData").GetValue(item);
                if (data != null && data.GetType().Name == "ObjectiveKillData")
                {
                    killOk = true;
                    Console.WriteLine("found ObjectiveKillData!");
                }
            }
            if (!killOk) throw new InvalidOperationException("kill objective was not loaded");

            object tut1Request = New("AcierProtocol.Request");
            object tut1Open = New("AcierProtocol.OpenMissionRequest");
            Set(tut1Open, "missionId", "story_tutorial_01");
            Set(tut1Request, "openMissionRequest", tut1Open);
            object tut1Response = BuildResponse(tut1Request, "openMissionRequest");
            object tut1Opened = Get(Get(Get(tut1Response, "openMissionResponse"), "missionToken"), "missionFile");
            object tut1Root = Get(tut1Opened, "RootNode");
            object tut1Runtime = Activator.CreateInstance(nodeType, tut1Root);
            object tut1List = nodeType.GetMethod("ComponentsRecursive").Invoke(tut1Runtime, null);
            bool tut1InteractionOk = false;
            foreach (object item in (System.Collections.IEnumerable)tut1List)
            {
                object data = item.GetType().GetProperty("NodeData").GetValue(item);
                if (data != null && data.GetType().Name == "ObjectiveInteractionData")
                {
                    tut1InteractionOk = true;
                    Console.WriteLine("found ObjectiveInteractionData for Tutorial 1!");
                }
            }
            if (!tut1InteractionOk) throw new InvalidOperationException("interaction objective was not loaded for Tutorial 1");
        }
        catch (Exception ex)
        {
            Console.WriteLine("mission runtime load failed: " + ex.GetBaseException());
            throw;
        }
    }

    private static void LoadProtocol(string root)
    {
        string managed = Path.Combine(root, "work", "apk", "assets", "bin", "Data", "Managed");
        if (!File.Exists(Path.Combine(managed, "AcierProtocol.dll")))
        {
            throw new FileNotFoundException("Extract the APK managed assemblies into work/apk first. Missing " + managed);
        }
        var context = new AssemblyLoadContext("acier", true);
        context.Resolving += (loadContext, name) =>
        {
            string candidate = Path.Combine(managed, name.Name + ".dll");
            return File.Exists(candidate) ? loadContext.LoadFromAssemblyPath(candidate) : null;
        };
        context.LoadFromAssemblyPath(Path.Combine(managed, "protobuf-net.dll"));
        _protocol = context.LoadFromAssemblyPath(Path.Combine(managed, "AcierProtocol.dll"));
        Assembly serializerAssembly = context.LoadFromAssemblyPath(Path.Combine(managed, "AcierProtocolSerializer.dll"));
        _serializer = Activator.CreateInstance(serializerAssembly.GetType("AcierProtocolSerializer"));
        _serialize = _serializer.GetType().GetMethod("Serialize", new[] { typeof(Stream), typeof(object) });
        _deserialize = _serializer.GetType().GetMethod("Deserialize", new[] { typeof(Stream), typeof(object), typeof(Type) });
        if (_serialize == null || _deserialize == null) throw new MissingMethodException("protobuf TypeModel serialize/deserialize");
    }

    private static void LoadCatalogue()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(_cacheDir, "catalogue.bin"));
        byte[] prefix = Encoding.ASCII.GetBytes(CataloguePrefix);
        int index = 0;
        while (true)
        {
            int at = IndexOf(data, prefix, index);
            if (at < 0) break;
            int length = ReadLength(data, at);
            if (length < prefix.Length || at + length > data.Length || !StartsWith(data, at, prefix))
            {
                index = at + prefix.Length;
                continue;
            }
            string relative = Encoding.ASCII.GetString(data, at + prefix.Length, length - prefix.Length);
            int window = Math.Min(data.Length, at + length + 180);
            Match id = Regex.Match(Encoding.ASCII.GetString(data, at + length, window - (at + length)), "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}");
            int look = Math.Max(0, at - 40);
            string before = Encoding.ASCII.GetString(data, look, at - look);
            bool zipped = before.Contains("ZippedItem");
            if (id.Success && File.Exists(Path.Combine(_cacheDir, id.Value)))
            {
                Catalogue[relative] = new CacheEntry { Id = id.Value, Zipped = zipped };
            }
            index = at + length;
        }
        if (Catalogue.Count < 50) throw new InvalidOperationException("catalogue parsed " + Catalogue.Count + " files");
    }

    private static int ReadLength(byte[] data, int urlStart)
    {
        if (urlStart >= 2 && (data[urlStart - 2] & 0x80) != 0)
        {
            return (data[urlStart - 2] & 0x7f) | (data[urlStart - 1] << 7);
        }
        return data[urlStart - 1];
    }

    private static string NestedRequest(object request)
    {
        foreach (PropertyInfo property in request.GetType().GetProperties())
        {
            if (!property.Name.EndsWith("Request", StringComparison.Ordinal)) continue;
            if (property.GetValue(request) != null) return property.Name;
        }
        return null;
    }

    private static Type FindResponseType(string property)
    {
        string suffix = char.ToUpperInvariant(property[0]) + property.Substring(1);
        foreach (Type type in _protocol.GetTypes())
        {
            if (type.Name == suffix) return type;
        }
        return null;
    }

    private static void Fill(object value, int depth)
    {
        if (value == null || depth < 0) return;
        foreach (PropertyInfo property in value.GetType().GetProperties())
        {
            if (!property.CanWrite || property.GetIndexParameters().Length != 0) continue;
            Type type = property.PropertyType;
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum) continue;
            try
            {
                if (type.IsArray)
                {
                    property.SetValue(value, Array.CreateInstance(type.GetElementType(), 0));
                }
                else if (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null && type.Name != "ExtensionObject")
                {
                    object child = Activator.CreateInstance(type);
                    property.SetValue(value, child);
                    if (depth > 0) Fill(child, depth - 1);
                }
            }
            catch
            {
            }
        }
    }

    private static object New(string name) => Activator.CreateInstance(TypeOf(name));
    private static Type TypeOf(string name) => _protocol.GetType(name) ?? throw new TypeLoadException(name);
    private static void Set(object target, string property, object value)
    {
        PropertyInfo info = target.GetType().GetProperty(property);
        if (info == null || !info.CanWrite) return;
        info.SetValue(target, value);
    }
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static string StringProp(object target, string property)
    {
        object value = target == null ? null : Get(target, property);
        return value as string;
    }

    private static int IndexOf(byte[] data, byte[] needle, int start)
    {
        for (int i = start; i + needle.Length <= data.Length; i++)
        {
            if (StartsWith(data, i, needle)) return i;
        }
        return -1;
    }

    private static bool StartsWith(byte[] data, int at, byte[] needle)
    {
        if (at + needle.Length > data.Length) return false;
        for (int i = 0; i < needle.Length; i++) if (data[at + i] != needle[i]) return false;
        return true;
    }

    public static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Resorces"))) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private sealed class CacheEntry
    {
        public string Id;
        public bool Zipped;
    }
}
