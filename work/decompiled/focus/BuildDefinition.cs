using System;
using System.IO;
using System.Text;
using AcierProtocol;
using Assets.Scripts.Native;
using Assets.Scripts.UI.Utils.Extensions;
using SharedBaseLib.Serialization;
using UnityEngine;

public class BuildDefinition
{
	public enum EUriType
	{
		LocalFileSystem,
		ServerIOS,
		ServerIOS_Trunk2,
		ServerIOS_Stable,
		ServerIOS_Submission,
		ServerAndroid,
		ServerAndroid_Trunk2,
		ServerAndroid_Stable,
		ServerAndroid_Submission,
		ServerWebPlayer,
		Custom
	}

	private const string CdataStartStr = "<![CDATA[";

	private const string CdataEndStr = "]>";

	public const string IntroMission = "Missions/tutorial_0001.bin";

	public static readonly MissionFile AnimusMission = SetupAnimusFile();

	private EUriType m_uriType;

	private string m_cachedUriUrl = string.Empty;

	private string m_url = string.Empty;

	private string m_Avatar;

	private string m_Hireling;

	private static string Filename => "BuildDefintion";

	private static string RelativeFilename => $"Resources/{Filename}.xml";

	public string ChangeList { get; private set; }

	public string BuildName { get; private set; }

	public string BuildNumber { get; private set; }

	public string MissionURL { get; set; }

	public bool ShowConsole { get; set; }

	public bool EditorMode { get; set; }

	public bool DontStartLogic { get; set; }

	public bool AudioEnabled { get; set; }

	public bool TagTranslatedText { get; set; }

	public bool UseLongestString { get; set; }

	public bool EnableCollectionTracker { get; set; }

	public string FlurryAPIKey { get; set; }

	public bool FlurryEnabled { get; set; }

	public bool IncludeWarmUpCache { get; set; }

	public EServerEnvironment ServerEnvironment { get; set; }

	public ELanguages Language { get; set; }

	public ELanguages PreferredLanguage { get; private set; }

	public string Avatar
	{
		get
		{
			return m_Avatar;
		}
		set
		{
			if (!AvatarFixed)
			{
				m_Avatar = value;
			}
		}
	}

	public string Hireling
	{
		get
		{
			return m_Hireling;
		}
		set
		{
			if (!AvatarFixed)
			{
				m_Hireling = value;
			}
		}
	}

	public string AvatarDefinitions { get; set; }

	public bool AvatarFixed { get; set; }

	public bool HasUsedDebugAvatarSelection { get; set; }

	public int AnimusLastMissionMenu { get; set; }

	public bool ShowBaseName { get; set; }

	public EUriType UriType
	{
		get
		{
			return m_uriType;
		}
		set
		{
			m_uriType = value;
			switch (m_uriType)
			{
			case EUriType.LocalFileSystem:
			{
				string path = PathExt.Combine(Application.dataPath, "..", "..", "AcierData");
				m_cachedUriUrl = "file://" + new DirectoryInfo(path).FullName;
				break;
			}
			case EUriType.ServerIOS:
			case EUriType.ServerIOS_Trunk2:
			case EUriType.ServerIOS_Stable:
			case EUriType.ServerIOS_Submission:
			case EUriType.ServerAndroid:
			case EUriType.ServerAndroid_Trunk2:
			case EUriType.ServerAndroid_Stable:
			case EUriType.ServerAndroid_Submission:
				m_cachedUriUrl = "http://acid-dev.ubisoft.org:8330/discovery-1.0-SNAPSHOT-ha-group-trunk/acier/v1/api";
				break;
			case EUriType.ServerWebPlayer:
				m_cachedUriUrl = "http://10.26.4.63/AcierData";
				break;
			case EUriType.Custom:
				break;
			}
		}
	}

	public string Url
	{
		get
		{
			return (m_uriType != EUriType.Custom) ? m_cachedUriUrl : m_url;
		}
		set
		{
			m_url = value;
		}
	}

	public BuildDefinition()
	{
		UriType = EUriType.LocalFileSystem;
		Debug.Log("BuildDefinition constructor");
		TextAsset textAsset = UnityEngine.Resources.Load(Filename) as TextAsset;
		if (textAsset == null)
		{
			return;
		}
		Debug.Log("BuildDefinition constructor - 2: Fileame = " + Filename);
		AudioEnabled = true;
		try
		{
			using Stream stream = new MemoryStream(textAsset.bytes);
			using DataSerializerBase dataSerializerBase = new DataXmlSerializer(stream, EMode.EMode_Read);
			dataSerializerBase.SerializeBeginNode("root");
			ChangeList = dataSerializerBase.SerializeString("changelist", ChangeList);
			BuildName = dataSerializerBase.SerializeString("buildname", BuildName);
			Debug.Log("BuildDefinition constructor - 3 - BuildName = " + BuildName);
			BuildNumber = dataSerializerBase.SerializeString("buildnumber", BuildNumber);
			UriType = (EUriType)dataSerializerBase.SerializeEnum("uriType", UriType.GetType(), (int)UriType);
			Url = dataSerializerBase.SerializeString("url", Url);
			MissionURL = dataSerializerBase.SerializeString("mission", MissionURL);
			ShowConsole = dataSerializerBase.SerializeBool("showConsole", ShowConsole);
			EditorMode = dataSerializerBase.SerializeBool("editorMode", EditorMode);
			DontStartLogic = dataSerializerBase.SerializeBool("dontStartLogic", DontStartLogic);
			Avatar = dataSerializerBase.SerializeString("avatar", Avatar);
			Hireling = dataSerializerBase.SerializeString("hireling", Hireling);
			AvatarDefinitions = dataSerializerBase.SerializeString("avatarDefinitions", string.Format("{0}{1}{2}", "<![CDATA[", AvatarDefinitions, "]>"));
			AvatarFixed = dataSerializerBase.SerializeBool("avatarFixed", AvatarFixed);
			AudioEnabled = dataSerializerBase.SerializeBool("audioEnabled", AudioEnabled);
			TagTranslatedText = dataSerializerBase.SerializeBool("tagTranslatedText", TagTranslatedText);
			UseLongestString = dataSerializerBase.SerializeBool("useLongestString", UseLongestString);
			Language = (ELanguages)dataSerializerBase.SerializeEnum("languageName", Language.GetType(), (int)Language);
			EnableCollectionTracker = dataSerializerBase.SerializeBool("enableCollectionTracker", EnableCollectionTracker);
			FlurryAPIKey = dataSerializerBase.SerializeString("flurryAPIKey", FlurryAPIKey);
			FlurryEnabled = dataSerializerBase.SerializeBool("flurryEnabled", FlurryEnabled);
			ServerEnvironment = (EServerEnvironment)dataSerializerBase.SerializeEnum("serverEnvironment", ServerEnvironment.GetType(), (int)ServerEnvironment);
			IncludeWarmUpCache = dataSerializerBase.SerializeBool("includeWarmUpCache", IncludeWarmUpCache);
			dataSerializerBase.SerializeEndNode();
		}
		catch (Exception)
		{
			throw;
		}
		AvatarDefinitions = AvatarDefinitions ?? string.Empty;
		if (AvatarDefinitions.StartsWith("<![CDATA["))
		{
			AvatarDefinitions = AvatarDefinitions.Substring("<![CDATA[".Length, AvatarDefinitions.Length - "<![CDATA[".Length);
			AvatarDefinitions = AvatarDefinitions.Substring(0, AvatarDefinitions.Length - "]>".Length);
		}
		PreferredLanguage = NativeTools.GetPreferredLanguage();
	}

	private static MissionFile SetupAnimusFile()
	{
		SerializedNode serializedNode = new SerializedNode();
		serializedNode.Components = serializedNode.Components.Add(new SerializedNodeComponent
		{
			Type = SerializedNodeComponent.NodeType.NodeType_MissionRootContent,
			MissionRootContent = new MissionRootContent
			{
				MissionType = EMissionType.Animus_EMissionType,
				Scene = "AnimusGlobe",
				MissionUrl = string.Empty,
				MissionBaseContent = new MissionBaseContent()
			}
		});
		MissionFile missionFile = new MissionFile();
		missionFile.RootNode = serializedNode;
		return missionFile;
	}

	public new string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("BuildDefinition \n");
		stringBuilder.AppendFormat("ChangeList: {0} \n", ChangeList);
		stringBuilder.AppendFormat("BuildName: {0} \n", BuildName);
		stringBuilder.AppendFormat("BuildNumber: {0} \n", BuildNumber);
		stringBuilder.AppendFormat("UriType: {0} \n", UriType);
		stringBuilder.AppendFormat("Url: {0} \n", Url);
		stringBuilder.AppendFormat("Mission: {0} \n", MissionURL);
		stringBuilder.AppendFormat("ShowConsole: {0} \n", ShowConsole);
		stringBuilder.AppendFormat("EditorMode: {0} \n", EditorMode);
		stringBuilder.AppendFormat("DontStartLogic: {0} \n", DontStartLogic);
		stringBuilder.AppendFormat("Avatar: {0} \n", Avatar);
		stringBuilder.AppendFormat("Hireling: {0} \n", Hireling);
		stringBuilder.AppendFormat("AvatarFixed: {0} \n", AvatarFixed);
		stringBuilder.AppendFormat("AudioEnabled: {0} \n", AudioEnabled);
		stringBuilder.AppendFormat("TagTranslatedText : {0} \n", TagTranslatedText);
		stringBuilder.AppendFormat("UseLongestString : {0} \n", UseLongestString);
		stringBuilder.AppendFormat("Language : {0} \n", Language);
		stringBuilder.AppendFormat("Collection Tracker Enabled: {0} \n", EnableCollectionTracker);
		stringBuilder.AppendFormat("Flurry API Key: {0} \n", FlurryAPIKey);
		stringBuilder.AppendFormat("Flurry enabled: {0} \n", FlurryEnabled);
		stringBuilder.AppendFormat("ServerEnvironment: {0} \n", ServerEnvironment);
		stringBuilder.AppendFormat("Include WarmUp Cache: {0} \n", IncludeWarmUpCache);
		return stringBuilder.ToString();
	}
}
