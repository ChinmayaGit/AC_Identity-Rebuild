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

    private static int Main(string[] args)
    {
        string root = FindProjectRoot();
        _cacheDir = Path.Combine(root, "Resorces", "com.ubisoft.assassinscreed.identity", "files", "cache");
        _saveDir = Path.Combine(root, "offline", "save");
        int port = 8080;
        bool selfTest = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cache" && i + 1 < args.Length) _cacheDir = args[++i];
            else if (args[i] == "--save" && i + 1 < args.Length) _saveDir = args[++i];
            else if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
            else if (args[i] == "--host" && i + 1 < args.Length) Host = args[++i];
            else if (args[i] == "--self-test") selfTest = true;
        }
        CdnUrl = "http://" + Host + ":" + port + "/cdn/ACID_2.8.2_373";
        ApiUrl = "http://" + Host + ":" + port + "/acier/v1/api";

        LoadProtocol(root);
        LoadCatalogue();
        Directory.CreateDirectory(_saveDir);
        LoadOrCreateProfile();
        RepairProfile();
        Statics();

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
                Set(ticket, "missionId", "story_0015");
                Set(ticket, "assassinId", "acid-offline-assassin");
                Set(ticket, "hirelingId", "hireling-1");
                Set(ticket, "missionFile", MissionFile());
                Set(ticket, "missionGoals", MissionGoals());
                Set(ticket, "eliteMission", false);
                Set(opened, "missionToken", ticket);
                Set(response, "openMissionResponse", opened);
                // The profile is already on the phone. Sending it again runs the
                // profile timer update, which fails the open and surfaces as error 3.
                break;
            case "endMissionRequest":
                object ended = New("AcierProtocol.EndMissionResponse");
                object endReward = New("AcierProtocol.MissionReward");
                Set(endReward, "baseSc", 50L);
                Set(endReward, "finalSc", 50L);
                Set(endReward, "scWithoutChallenge", 50L);
                Set(endReward, "scFromHireling", 0L);
                Set(ended, "missionReward", endReward);
                Set(response, "endMissionResponse", ended);
                // Same as open: the profile is already loaded, and sending it
                // again throws while the Animus spinner is up.
                break;
            case "getMissionInfoRequest":
                object missionInfo = New("AcierProtocol.GetMissionInfoResponse");
                object reward = New("AcierProtocol.MissionReward");
                Set(reward, "baseSc", 50L);
                Set(reward, "finalSc", 50L);
                Set(reward, "scWithoutChallenge", 50L);
                Set(reward, "scFromHireling", 50L);
                Set(missionInfo, "missionRewardPreview", reward);
                Set(missionInfo, "missionGoals", MissionGoals());
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

    // An empty goal list makes the details screen throw while filling the objective
    // line. That throw happens before the loading spinner is removed, so Continue
    // never accepts a tap.
    private static object MissionGoals()
    {
        object goals = New("AcierProtocol.MissionGoals");
        Type goalType = Nested("MissionGoals", "MissionGoal");
        object goal = Activator.CreateInstance(goalType);
        Set(goal, "goalType", Enum.Parse(TypeOf("AcierProtocol.EMissionGoal"), "Objective_EMissionGoal"));
        Set(goal, "id", 1);
        object description = New("AcierProtocol.LangKey");
        Set(description, "neutral", 8395);
        Set(goal, "description", description);
        PropertyInfo mapping = goalType.GetProperty("mapping");
        Set(goal, "mapping", Array.CreateInstance(mapping.PropertyType.GetElementType(), 0));
        Array list = Array.CreateInstance(goalType, 1);
        list.SetValue(goal, 0);
        Set(goals, "missionGoals", list);
        Set(goals, "missionLevel", 1);
        return goals;
    }

    // The loader builds the playable mission from RootNode. A missing node throws
    // while the Animus spinner is up, which the game reports as error 3. The scene
    // name has to be a key already in the phone's scene dictionary.
    private static object MissionFile()
    {
        object file = New("AcierProtocol.MissionFile");
        object root = New("AcierProtocol.SerializedNode");
        Set(root, "Name", "story_0015");
        Type componentType = TypeOf("AcierProtocol.SerializedNodeComponent");
        object component = Activator.CreateInstance(componentType);
        Set(component, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionRootContent"));
        object content = New("AcierProtocol.MissionRootContent");
        Set(content, "MissionBaseContent", New("AcierProtocol.MissionBaseContent"));
        Set(content, "Age", Enum.Parse(TypeOf("AcierProtocol.EEra"), "Italy"));
        Set(content, "Region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), "Forli"));
        Set(content, "Mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), "Dusk"));
        Set(content, "MissionType", Enum.Parse(TypeOf("AcierProtocol.EMissionType"), "Mission_EMissionType"));
        Set(content, "BaseName", "story_0015");
        Set(content, "Name", "Forli");
        Set(content, "Scene", "Forli_dusk");
        Set(content, "DifficultyLevel", 1);
        Set(content, "Rank", 1);
        Set(component, "MissionRootContent", content);
        // The Forlì scene builds haystacks as soon as the camera exists. That
        // lookup needs a hotspot component on the mission root; without it the
        // load stops on this screen.
        object hotspots = Activator.CreateInstance(componentType);
        Set(hotspots, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionHotspotsContent"));
        object hotspotContent = New("AcierProtocol.MissionHotspotsContent");
        object hotspotBase = New("AcierProtocol.MissionBaseContent");
        Set(hotspotBase, "ID", 2L);
        Set(hotspotContent, "MissionBaseContent", hotspotBase);
        Set(hotspots, "MissionHotspotsContent", hotspotContent);
        // With no objective the mission is already complete, so the contract
        // ends the moment it starts. Staying alive also ends it: that objective
        // starts finished, and the game treats the player dying on the first
        // frame as a failed contract, which drops straight into the Animus.
        // A long timer starts unfinished and does not watch the player's health.
        object stay = Activator.CreateInstance(componentType);
        Set(stay, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_ObjectiveTimerContent"));
        object stayContent = New("AcierProtocol.ObjectiveTimerContent");
        object objective = New("AcierProtocol.ObjectiveBaseContent");
        Set(objective, "MissionBaseContent", ActiveBase(3));
        Set(objective, "IsPrimaryObjective", true);
        Set(objective, "OnSuccessId", 1L);
        Set(objective, "OnFailId", 1L);
        Set(objective, "HideDescription", true);
        Set(stayContent, "ObjectiveBaseContent", objective);
        Set(stayContent, "TimeInSeconds", 86400);
        Set(stayContent, "InitialResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stayContent, "CompletionResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stayContent, "TerminationResult", ObjectiveResult("Undefined_EObjectiveResult", "InProgress_EObjectiveProgress"));
        Set(stay, "ObjectiveTimerContent", stayContent);
        Array components = Array.CreateInstance(componentType, 3);
        components.SetValue(component, 0);
        components.SetValue(hotspots, 1);
        components.SetValue(stay, 2);
        Set(root, "Components", components);
        // Combat setup reads the player during mission init. The player is created
        // only when a spawn node is started, and a node starts only when its
        // activate id is the sentinel -2. An omitted id arrives as 0 and is skipped.
        object spawn = New("AcierProtocol.SerializedNode");
        Set(spawn, "Name", "PlayerSpawn");
        // Low streets put the camera under the pavement. The high roofs look
        // out over the buildings. This spot is the open pavement at wall
        // height, with the houses beside it and no roof overhead.
        Set(spawn, "PositionX", -2f);
        Set(spawn, "PositionY", 8.6f);
        Set(spawn, "PositionZ", -44f);
        // +90 looks away from the houses into the empty side of the square.
        // The nearest walls are the other way, at eye height.
        Set(spawn, "EulerAnglesY", -150f);
        if (Math.Abs((float)Get(spawn, "EulerAnglesY") + 150f) > 0.01f) throw new InvalidOperationException("spawn facing was not stored");
        object spawnComponent = Activator.CreateInstance(componentType);
        Set(spawnComponent, "Type", Enum.Parse(componentType.GetNestedType("NodeType"), "NodeType_MissionPlayerSpawnContent"));
        object spawnContent = New("AcierProtocol.MissionPlayerSpawnContent");
        Set(spawnContent, "MissionBaseContent", ActiveBase(1));
        Set(spawnComponent, "MissionPlayerSpawnContent", spawnContent);
        Array spawnComponents = Array.CreateInstance(componentType, 1);
        spawnComponents.SetValue(spawnComponent, 0);
        Set(spawn, "Components", spawnComponents);
        Array children = Array.CreateInstance(root.GetType(), 1);
        children.SetValue(spawn, 0);
        Set(root, "Children", children);
        Set(file, "RootNode", root);
        return file;
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

    private static Array RankRewards()
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
        for (int i = 0; i < ranks; i++)
        {
            object entitlement = Activator.CreateInstance(entitlementType);
            Set(entitlement, "gameIdGuid", "ntt_offline_unearned");
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
        string[] missions = { "story_0015", "story_0001", "story_caribbean", "story_tutorial" };
        Array entries = Array.CreateInstance(entryType, missions.Length);
        Array ranks = RankRewards();
        Array challengeRanks = RankRewards();
        for (int i = 0; i < missions.Length; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "name", missions[i]);
            // A missing rank row makes the details screen throw while drawing rewards,
            // which leaves Continue disabled under the loading spinner.
            // The unowned entitlement keeps the mission marked incomplete.
            Set(entry, "completionRewards", ranks);
            Set(entry, "challengeRewards", challengeRanks);
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
        string[] ids = { "ntt_0dd81737", "ntt_5572b4db", "ntt_f9626611", "ntt_c350c141" };
        string[] names = { "Forli", "The Rise of the Borgia", "Monteriggioni", "Tutorial" };
        string[] eras = { "Italy", "Italy", "Italy", "Abstergo" };
        string[] regions = { "Forli", "Firenze_Palazzo", "Monteriggioni", "Montreal" };
        string[] moods = { "Dusk", "Daytime", "Daytime", "Daytime" };
        string[] missions = { "story_0015", "story_0001", "story_monteriggioni", "story_tutorial" };
        int[] titles = { 8395, 6919, 5423, 6735 };
        Type campaignType = Nested("CampaignCatalog", "Campaign");
        Type groupType = TypeOf("AcierProtocol.MissionsAtLocation");
        Type infoType = TypeOf("AcierProtocol.MissionInfo");
        Type locationType = TypeOf("AcierProtocol.LocationSet");
        Array campaigns = Array.CreateInstance(campaignType, ids.Length);
        string[] none = Array.Empty<string>();
        for (int i = 0; i < ids.Length; i++)
        {
            object campaign = Activator.CreateInstance(campaignType);
            Set(campaign, "id", ids[i]);
            Set(campaign, "name", names[i]);
            Set(campaign, "sortIndex", i);
            Set(campaign, "hidden", false);
            Set(campaign, "reqEntitlements", none);
            object location = Activator.CreateInstance(locationType);
            Set(location, "era", Enum.Parse(TypeOf("AcierProtocol.EEra"), eras[i]));
            Set(location, "region", Enum.Parse(TypeOf("AcierProtocol.ERegion"), regions[i]));
            Set(location, "mood", Enum.Parse(TypeOf("AcierProtocol.EMood"), moods[i]));
            object info = Activator.CreateInstance(infoType);
            FillNulls(info, 2);
            Set(info, "baseName", missions[i]);
            object title = Get(info, "objectiveLangKey");
            if (title == null)
            {
                title = New("AcierProtocol.LangKey");
                Set(info, "objectiveLangKey", title);
            }
            Set(title, "neutral", titles[i]);
            Set(info, "reqEntitlements", none);
            Set(info, "sortIndex", 0);
            Set(info, "rank", 1);
            Set(info, "difficulty", 1);
            Array infos = Array.CreateInstance(infoType, 1);
            infos.SetValue(info, 0);
            object group = Activator.CreateInstance(groupType);
            Set(group, "location", location);
            Set(group, "missions", infos);
            Array groups = Array.CreateInstance(groupType, 1);
            groups.SetValue(group, 0);
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
        Type entryType = Nested("EntitlementInventory", "Entry");
        Array entries = Array.CreateInstance(entryType, ProfileEntitlements.Length);
        for (int i = 0; i < ProfileEntitlements.Length; i++)
        {
            object entry = Activator.CreateInstance(entryType);
            Set(entry, "gameIdGuid", ProfileEntitlements[i]);
            Set(entry, "unlockDate", 1L);
            entries.SetValue(entry, i);
        }
        Set(inventory, "entries", entries);
        Console.WriteLine("entitlements " + entries.Length);
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

    private static void SelfTest()
    {
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
        if (components == null || components.Length != 3) throw new InvalidOperationException("mission root component missing");
        object content = Get(components.GetValue(0), "MissionRootContent");
        if (Get(components.GetValue(1), "MissionHotspotsContent") == null) throw new InvalidOperationException("mission hotspots missing");
        if (Get(components.GetValue(2), "ObjectiveTimerContent") == null) throw new InvalidOperationException("timer objective missing");
        if ((string)Get(content, "Scene") != "Forli_dusk") throw new InvalidOperationException("mission scene missing");
        if (Get(content, "MissionBaseContent") == null) throw new InvalidOperationException("mission base missing");
        if (Get(openBack, "error") != null) throw new InvalidOperationException("open mission response has an error");
        Array children = (Array)Get(root, "Children");
        if (children == null || children.Length != 1) throw new InvalidOperationException("mission player spawn missing");
        Array spawnComponents = (Array)Get(children.GetValue(0), "Components");
        object spawnContent = Get(spawnComponents.GetValue(0), "MissionPlayerSpawnContent");
        object spawnBase = Get(spawnContent, "MissionBaseContent");
        if ((long)Get(spawnBase, "ActivateAtId") != -2L) throw new InvalidOperationException("player spawn is not active");
        Console.WriteLine("mission file " + openPacked.Length + " bytes, component " + Get(components.GetValue(0), "Type"));
        try
        {
            string managed = Path.Combine(FindProjectRoot(), "work", "apk", "assets", "bin", "Data", "Managed");
            AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(_protocol);
            Assembly game = context.LoadFromAssemblyPath(Path.Combine(managed, "Assembly-CSharp.dll"));
            Type nodeType = game.GetType("Assets.Scripts.Network.Missions.Mission.Serialization.SerializedNode");
            object runtime = Activator.CreateInstance(nodeType, root);
            object list = nodeType.GetMethod("ComponentsRecursive").Invoke(runtime, null);
            int count = (int)list.GetType().GetProperty("Count").GetValue(list);
            if (count != 4) throw new InvalidOperationException("mission runtime components " + count);
            bool stayOk = false;
            foreach (object item in (System.Collections.IEnumerable)list)
            {
                object data = item.GetType().GetProperty("NodeData").GetValue(item);
                if (data == null || data.GetType().Name != "ObjectiveTimerData") continue;
                long activate = (long)data.GetType().GetField("ActivateAtId").GetValue(data);
                long success = (long)data.GetType().GetField("OnSuccessId").GetValue(data);
                long fail = (long)data.GetType().GetField("OnFailId").GetValue(data);
                bool primary = (bool)data.GetType().GetField("IsPrimaryObjective").GetValue(data);
                int seconds = (int)data.GetType().GetField("TimeInSeconds").GetValue(data);
                object initial = data.GetType().GetField("InitialResultWrapper").GetValue(data);
                object result = initial.GetType().GetField("Result").GetValue(initial);
                Console.WriteLine("timer activate " + activate + " success " + success + " fail " + fail + " primary " + primary + " seconds " + seconds + " result " + result);
                if (activate != -2L || success == -2L || fail == -2L || !primary || seconds < 3600 || result.ToString().IndexOf("Undefined", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("timer would end the contract");
                stayOk = true;
            }
            if (!stayOk) throw new InvalidOperationException("timer objective was not loaded");
            Console.WriteLine("mission runtime components " + count);
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
