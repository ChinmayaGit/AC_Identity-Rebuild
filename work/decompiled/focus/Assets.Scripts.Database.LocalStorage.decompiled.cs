using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using AcierProtocol;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.Utils.Extensions;
using Database;
using MiniJSON;
using SharedBaseLib.Core;
using UbiCore.Utils;

namespace Assets.Scripts.Database;

public class LocalStorage : UbiCore.Utils.Singleton<LocalStorage>
{
	private class CodexEntry
	{
		public GameID GameID { get; private set; }

		public bool Read { get; set; }

		public CodexEntry(GameID _gameID, bool _read)
		{
			GameID = _gameID ?? GameID.Invalid;
			Read = _read;
		}
	}

	public interface IMissionProgressData
	{
		bool WasUnlockShown { get; }

		bool WasCompletedShown { get; }
	}

	private class MissionProgressData : IMissionProgressData
	{
		public bool WasUnlockShown { get; set; }

		public bool WasCompletedShown { get; set; }
	}

	public interface ICTAMissionData
	{
		int Died { get; }

		bool UsedDoctor { get; }

		bool UsedSkill { get; }

		bool LowDamage { get; }

		bool LowHealth { get; }
	}

	private class CTAMissionData : ICTAMissionData
	{
		public int Died { get; set; }

		public bool UsedDoctor { get; set; }

		public bool UsedSkill { get; set; }

		public bool LowDamage { get; set; }

		public bool LowHealth { get; set; }
	}

	public delegate void EntryUnlocked(GameID _id);

	public delegate void EntryRead(GameID _id);

	public delegate void ItemReceived(GameID _id);

	public delegate void ItemSeen(GameID _id);

	public delegate void GPChanged(string _key, object _oldValue, object _newValue);

	private const int CurrentVersion = 7;

	private const string VersionKey = "version";

	private const string CodexKey = "codex";

	private const string ItemsKey = "items";

	private const string DecryptionKey = "decryption";

	private const string ModulesKey = "modules";

	private const string AchievementsKey = "achievements";

	private const string TrackingKey = "tracking";

	private const string GeneralPurposeKey = "generalpurpose";

	private const string ShopKey = "shop";

	private const string CTAKey = "cta";

	private const string MissionProgressKey = "missionprogress";

	private const string EntitlementKey = "entitlement";

	private const string CTAMissions = "missions";

	private const string CTACounter = "counter";

	private const int CTAStoredMissions = 10;

	public EntryRead OnCodexEntryRead;

	public ItemSeen OnItemSeen;

	public ItemReceived OnItemReceived;

	public GPChanged OnGPChanged;

	private static readonly string DataFile = Main.LoadSavedDataPath() + "/localstorage.json";

	private static readonly string TmpFile = DataFile + ".tmp";

	private Dictionary<string, object> m_data;

	private Dictionary<string, object> m_codexData;

	private Dictionary<string, object> m_itemsData;

	private Dictionary<string, object> m_decryptionData;

	private Dictionary<string, object> m_modulesData;

	private Dictionary<string, object> m_achievementsData;

	private Dictionary<string, object> m_generalPurposeData;

	private Dictionary<string, object> m_shopData;

	private Dictionary<string, object> m_ctaData;

	private Dictionary<string, object> m_missionProgressData;

	private Dictionary<string, object> m_entitlementNotificationData;

	private bool m_shouldSave;

	private bool m_groupEdit;

	private bool m_haveBuildIndex;

	private readonly Dictionary<GameID, CodexEntry> m_codexIndex = new Dictionary<GameID, CodexEntry>();

	private readonly BBList<ICTAMissionData> m_ctaMissionIndex = new BBList<ICTAMissionData>(10);

	private readonly Dictionary<string, MissionProgressData> m_missionProgressIndex = new Dictionary<string, MissionProgressData>();

	public static string GPKeyModuleScreenVisited = "GPKeyModuleScreenVisited";

	public static string GPKeyAppRatingChangeList = "GPKeyAppRatingChangeList";

	public static string GPKeyAppRatingMissions = "GPKeyAppRatingMissions";

	public static string GPKeyAppRatingDisplayed = "GPKeyAppRatingDisplayed";

	public static string GPKeyMysteryBoxSlotCount = "MysteryBoxSlotCount";

	public static string GPKeyAssetBundlesQuality = "AssetBundlesQuality";

	public static string GPKeyQualityDeviceProfileName = "QualityDeviceProfileName";

	private string GPKeyCheckAppSignatureForLicenseCheck = "GPKeyCheckAppSignatureForLicenseCheck";

	public static readonly string CTACounterMission = "Mission";

	public static readonly string CTACounterMissionLastCTA = "MissionLastCTA";

	public static readonly string CTACounterUsedSkillsOutOfTutorial = "UsedSkillsOutOfTutorial";

	public static readonly string CTACounterReviveNotPossible = "ReviveNotPossible";

	public static readonly string CTACounterExceedingRankCurrent = "ExceedingRankCurrent";

	public static readonly string CTACounterExceedingRankNextAvail = "ExceedingRankNextAvail";

	public static readonly string CTACounterHeroicRank = "HeroicRankShown";

	public static readonly string CTACounterLastShopEvent = "LastShopEvent";

	public static readonly string CTACounterLastLeaderboardShown = "LastLeaderboardShown";

	public static readonly string CTACounterLastReachedLeaderboardRank = "LastReachedLeaderboardRank";

	public static readonly string CTACounterLastUnlockCampaign = "LastUnlockCampaign";

	public static readonly string CTACounterLastAdColony = "LastAdColony";

	public bool VersionIncrease { get; private set; }

	public bool ModuleUnseen
	{
		get
		{
			foreach (KeyValuePair<string, object> modulesDatum in m_modulesData)
			{
				if (GetBool(modulesDatum))
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool DecryptionItemUnseen
	{
		get
		{
			foreach (KeyValuePair<string, object> decryptionDatum in m_decryptionData)
			{
				if (GetBool(decryptionDatum))
				{
					return true;
				}
			}
			return false;
		}
	}

	public bool ItemUnseen
	{
		get
		{
			foreach (KeyValuePair<string, object> itemsDatum in m_itemsData)
			{
				if (!GetBool(itemsDatum))
				{
					return true;
				}
			}
			return false;
		}
	}

	public int ItemCount => m_itemsData.Count;

	public bool CodexUnreadEntries
	{
		get
		{
			foreach (KeyValuePair<GameID, CodexEntry> item in m_codexIndex)
			{
				if (!item.Value.Read)
				{
					return true;
				}
			}
			return false;
		}
	}

	public EQualityAssetBundle? AssetBundlesQuality
	{
		get
		{
			if (GPKeyExists(GPKeyAssetBundlesQuality))
			{
				return (EQualityAssetBundle)(int)Enum.Parse(typeof(EQualityAssetBundle), GPGetValue(GPKeyAssetBundlesQuality).ToString());
			}
			return null;
		}
		set
		{
			GPSetValue(GPKeyAssetBundlesQuality, (value.HasValue ? value.Value : EQualityAssetBundle.High).ToString());
		}
	}

	public string QualityDeviceProfileName
	{
		get
		{
			if (GPKeyExists(GPKeyQualityDeviceProfileName))
			{
				return GPGetValue(GPKeyQualityDeviceProfileName).ToString();
			}
			return string.Empty;
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				m_generalPurposeData.Remove(GPKeyQualityDeviceProfileName);
			}
			else
			{
				GPSetValue(GPKeyQualityDeviceProfileName, value);
			}
		}
	}

	public int MysteryBoxSlotCount
	{
		get
		{
			if (GPKeyExists(GPKeyMysteryBoxSlotCount))
			{
				int result = 0;
				if (int.TryParse(GPGetValue(GPKeyMysteryBoxSlotCount).ToString(), out result))
				{
					return result;
				}
			}
			return -1;
		}
		set
		{
			GPSetValue(GPKeyMysteryBoxSlotCount, value);
		}
	}

	public bool CheckAppSignature
	{
		get
		{
			if (GPKeyExists(GPKeyCheckAppSignatureForLicenseCheck))
			{
				return (bool)GPGetValue(GPKeyCheckAppSignatureForLicenseCheck);
			}
			GPSetValue(GPKeyCheckAppSignatureForLicenseCheck, true);
			return true;
		}
		set
		{
			GPSetValue(GPKeyCheckAppSignatureForLicenseCheck, value);
		}
	}

	public event EntryUnlocked OnCodexEntryUnlocked;

	public LocalStorage()
	{
		Load();
		Singleton<StateManager>.Instance.ProfileManager.OnProfileUpdate.Add(OnProfileUpdated);
		if (Main.Instance.IsNetworkEnabled)
		{
			UpdateItemNotificationStatusOnPlayerProfileUpdated();
		}
	}

	public void BeginGroupEdit()
	{
		m_groupEdit = true;
	}

	public void EndGroupEditAndSave()
	{
		m_groupEdit = false;
		if (m_shouldSave)
		{
			SaveInternal();
		}
	}

	public void ValidateStorageForOldUserOnStartUp()
	{
		if (m_achievementsData.Count == 0 && m_entitlementNotificationData.Count == 0)
		{
			Singleton<StateManager>.Instance.ProfileManager.Profile.UpdateLocalStorageForNewProfile();
		}
	}

	public void Clear()
	{
		try
		{
			if (File.Exists(DataFile))
			{
				File.Delete(DataFile);
			}
			if (File.Exists(TmpFile))
			{
				File.Delete(TmpFile);
			}
		}
		catch (Exception)
		{
		}
		Load();
		BuildIndices();
	}

	public void BuildIndices()
	{
		m_codexIndex.Clear();
		GameDB gameDB = Main.Instance.GameDB;
		BBList<string> bBList = new BBList<string>(0);
		foreach (KeyValuePair<string, object> codexDatum in m_codexData)
		{
			string key = codexDatum.Key;
			GameID _gameId = GameID.Invalid;
			if (!gameDB.TryFindGameIdFromGuid(key, out _gameId))
			{
				int result = 0;
				if (int.TryParse(key, out result))
				{
					gameDB.FindGameIdFromGuidHash(result, ref _gameId);
				}
			}
			if (!GameID.IsValid(_gameId) || gameDB.GetItem<CodexDefinition>(_gameId).StructureType != CodexDefinition.EStructureType.Entry)
			{
				bBList.Add(key);
			}
			else
			{
				m_codexIndex[_gameId] = new CodexEntry(_gameId, GetBool(codexDatum));
			}
		}
		for (int i = 0; i < bBList.Count; i++)
		{
			m_codexData.Remove(bBList[i]);
			m_shouldSave = true;
		}
		List<object> list;
		if (m_ctaData.TryGetValue("missions", out var value) && value is List<object>)
		{
			list = value as List<object>;
		}
		else
		{
			list = (List<object>)(m_ctaData["missions"] = new List<object>());
			m_shouldSave = true;
		}
		m_ctaMissionIndex.Clear();
		for (int j = 0; j < list.Count; j++)
		{
			if (list[j] is Dictionary<string, object> dictionary)
			{
				CTAMissionData cTAMissionData = new CTAMissionData();
				cTAMissionData.Died = GetInt(GetOrNull(dictionary, "Died"));
				cTAMissionData.UsedDoctor = GetBool(GetOrNull(dictionary, "UsedDoctor"), _default: true);
				cTAMissionData.UsedSkill = GetBool(GetOrNull(dictionary, "UsedSkill"), _default: true);
				cTAMissionData.LowDamage = GetBool(GetOrNull(dictionary, "LowDamage"), _default: false);
				cTAMissionData.LowHealth = GetBool(GetOrNull(dictionary, "LowHealth"), _default: false);
				CTAMissionData item = cTAMissionData;
				m_ctaMissionIndex.Add(item);
			}
		}
		m_missionProgressIndex.Clear();
		foreach (KeyValuePair<string, object> missionProgressDatum in m_missionProgressData)
		{
			if (missionProgressDatum.Value is Dictionary<string, object> dictionary2)
			{
				MissionProgressData missionProgressData = new MissionProgressData();
				missionProgressData.WasCompletedShown = GetBool(dictionary2["WasCompletedShown"]);
				missionProgressData.WasUnlockShown = GetBool(dictionary2["WasUnlockShown"]);
				MissionProgressData value2 = missionProgressData;
				m_missionProgressIndex.Add(missionProgressDatum.Key, value2);
			}
		}
		m_haveBuildIndex = true;
		if (m_shouldSave)
		{
			SaveInternal();
		}
	}

	public void ModuleAdd(string _id, string _enum)
	{
		if (!m_modulesData.ContainsKey(_id))
		{
			m_modulesData.Add(_id, _enum);
			Save();
		}
	}

	public bool ModuleRemove(string _id)
	{
		m_modulesData.Remove(_id);
		Save();
		return false;
	}

	public bool SpecificModuleUnseen(string _id)
	{
		foreach (KeyValuePair<string, object> modulesDatum in m_modulesData)
		{
			if (modulesDatum.Key == _id)
			{
				return true;
			}
		}
		return false;
	}

	public void ModulesRemoveAll()
	{
		m_modulesData.Clear();
		Save();
	}

	public void DecryptionResourceAdd(string _id, string _enum)
	{
		if (!m_decryptionData.ContainsKey(_id))
		{
			m_decryptionData.Add(_id, _enum);
			Save();
		}
	}

	public bool SpecificDecryptionItemUnseen(string _id)
	{
		foreach (KeyValuePair<string, object> decryptionDatum in m_decryptionData)
		{
			if (decryptionDatum.Key == _id)
			{
				return true;
			}
		}
		return false;
	}

	public bool DecryptionResourceRemove(string _id)
	{
		m_decryptionData.Remove(_id);
		Save();
		return false;
	}

	public void DecryptionRemoveAll()
	{
		m_decryptionData.Clear();
		Save();
	}

	public void ItemAdd(string _id)
	{
		if (!m_itemsData.ContainsKey(_id))
		{
			m_itemsData[_id] = false;
			Save();
		}
	}

	public void ItemRemove(string _id)
	{
		m_itemsData.Remove(_id);
		Save();
	}

	public void ItemRemoveAll()
	{
		m_itemsData.Clear();
		Save();
	}

	public void ItemSetSeen(string _item, bool _seen = true)
	{
		m_itemsData[_item] = _seen;
		Save();
	}

	public bool ItemIsSeen(string _id)
	{
		object value;
		return !m_itemsData.TryGetValue(_id, out value) || GetBool(value);
	}

	public void CodexUnlockEntry(string _guid)
	{
		if (!m_haveBuildIndex)
		{
			return;
		}
		GameDB gameDB = Main.Instance.GameDB;
		GameID gameID = gameDB.FindGameIdFromGuid(_guid);
		if (!GameID.IsValid(gameID))
		{
			return;
		}
		CodexDefinition item = gameDB.GetItem<CodexDefinition>(gameID);
		if (item.StructureType == CodexDefinition.EStructureType.Entry && !CodexEntryIsUnlocked(gameID))
		{
			string key = gameID.GetHashCode().ToString();
			m_codexData[key] = false;
			m_codexIndex[gameID] = new CodexEntry(gameID, _read: false);
			Save();
			if (OnCodexEntryUnlocked != null)
			{
				OnCodexEntryUnlocked(gameID);
			}
		}
	}

	public void CodexUnlockEntry(GameID _gameId)
	{
		if (!m_haveBuildIndex)
		{
			return;
		}
		GameDB gameDB = Main.Instance.GameDB;
		CodexDefinition item = gameDB.GetItem<CodexDefinition>(_gameId);
		if (!CodexEntryIsUnlocked(_gameId))
		{
			string key = _gameId.GetHashCode().ToString();
			m_codexData[key] = false;
			m_codexIndex[_gameId] = new CodexEntry(_gameId, _read: false);
			Save();
			if (OnCodexEntryUnlocked != null)
			{
				OnCodexEntryUnlocked(_gameId);
			}
		}
	}

	public bool CodexEntryIsUnlocked(GameID _gameID)
	{
		return m_haveBuildIndex && m_codexIndex.ContainsKey(_gameID);
	}

	public bool CodexEntryIsUnlocked(GameID _gameID, out bool _read)
	{
		if (m_haveBuildIndex && m_codexIndex.TryGetValue(_gameID, out var value))
		{
			_read = value.Read;
			return true;
		}
		_read = true;
		return false;
	}

	public void CodexSetEntryReadFlag(GameID _gameID, bool _read = true)
	{
		if (m_haveBuildIndex && m_codexIndex.TryGetValue(_gameID, out var value))
		{
			string key = _gameID.Guid.ToString();
			Dictionary<string, object> codexData = m_codexData;
			bool flag = (value.Read = _read);
			codexData[key] = flag;
			OnCodexEntryRead(_gameID);
			Save();
		}
	}

	private string BuildAchievementKey(string _id, int _rankdIdx)
	{
		return _id + "_" + _rankdIdx;
	}

	public void SetAchievementRead(string _id, int _rankdIdx)
	{
		string key = BuildAchievementKey(_id, _rankdIdx);
		m_achievementsData[key] = true;
		Save();
	}

	public bool IsAchievementRead(string _id, int _rankdIdx)
	{
		string key = BuildAchievementKey(_id, _rankdIdx);
		object value = null;
		if (m_achievementsData.TryGetValue(key, out value))
		{
			return GetBool(value);
		}
		return false;
	}

	public void AchievementRemoveAll()
	{
		m_achievementsData.Clear();
		Save();
	}

	public void SetEntitlementShown(string _guid, bool _shown = true)
	{
		m_entitlementNotificationData[_guid] = _shown;
		Save();
	}

	public bool WasEntitlementShown(string _guid)
	{
		object value;
		return m_entitlementNotificationData.TryGetValue(_guid, out value) && GetBool(value);
	}

	public void ResetAllEntitlementShown()
	{
		m_entitlementNotificationData.Clear();
	}

	public void GPSetValue(string _key, object _value)
	{
		object value = null;
		m_generalPurposeData.TryGetValue(_key, out value);
		m_generalPurposeData[_key] = _value;
		if (OnGPChanged != null && value != _value)
		{
			OnGPChanged(_key, value, _value);
		}
		Save();
	}

	public bool GPKeyExists(string _key)
	{
		return m_generalPurposeData.ContainsKey(_key);
	}

	public object GPGetValue(string _key)
	{
		object value = null;
		m_generalPurposeData.TryGetValue(_key, out value);
		return value;
	}

	public void GPRemoveAll()
	{
		m_generalPurposeData.Clear();
		Save();
	}

	public void SHOPAddValue(EEventType _eventType, uint _activeEventCRC)
	{
		string key = _eventType.ToString();
		string text = string.Format("#{0};", _activeEventCRC.ToString("D"));
		if (!m_shopData.ContainsKey(key))
		{
			m_shopData[key] = text;
		}
		else
		{
			string text2 = (string)m_shopData[key];
			if (!text2.Contains(text))
			{
				m_shopData[key] = $"{text2}{text}";
			}
		}
		Save();
	}

	public bool SHOPGetValue(EEventType _eventType, uint _activeEventCRC)
	{
		string key = _eventType.ToString();
		string value = string.Format("#{0};", _activeEventCRC.ToString("D"));
		if (!m_shopData.ContainsKey(key))
		{
			return false;
		}
		string text = (string)m_shopData[key];
		if (text.Contains(value))
		{
			return true;
		}
		return false;
	}

	public void SHOPRemoveAll()
	{
		m_shopData.Clear();
		Save();
	}

	public IMissionProgressData GetMissionProgressData(string _id)
	{
		MissionProgressData value = null;
		m_missionProgressIndex.TryGetValue(_id, out value);
		return value;
	}

	public void SetMissionProgressUnlocked(string _missionId, bool _wasUnlockShown)
	{
		MissionProgressData missionProgressData = AddOrGetMissionProgressData(_missionId);
		missionProgressData.WasUnlockShown = _wasUnlockShown;
		UpdateMissionProgressDate(_missionId, missionProgressData);
	}

	public void SetMissionProgressCompleted(string _missionId, bool _wasCompletedShown)
	{
		MissionProgressData missionProgressData = AddOrGetMissionProgressData(_missionId);
		missionProgressData.WasCompletedShown = _wasCompletedShown;
		UpdateMissionProgressDate(_missionId, missionProgressData);
	}

	private void UpdateMissionProgressDate(string _missionId, MissionProgressData _missionData)
	{
		if (m_haveBuildIndex)
		{
			if (!m_missionProgressIndex.ContainsKey(_missionId))
			{
				m_missionProgressIndex.Add(_missionId, _missionData);
			}
			object value = null;
			Dictionary<string, object> dictionary = null;
			if (!m_missionProgressData.TryGetValue(_missionId, out value))
			{
				dictionary = new Dictionary<string, object>();
				m_missionProgressData.Add(_missionId, dictionary);
			}
			else
			{
				dictionary = (Dictionary<string, object>)value;
			}
			dictionary["WasCompletedShown"] = _missionData.WasCompletedShown;
			dictionary["WasUnlockShown"] = _missionData.WasUnlockShown;
			Save();
		}
	}

	private MissionProgressData AddOrGetMissionProgressData(string _id)
	{
		MissionProgressData value = null;
		if (!m_missionProgressIndex.TryGetValue(_id, out value))
		{
			value = new MissionProgressData();
			m_missionProgressIndex.Add(_id, value);
		}
		return value;
	}

	public void CTAMissionDataAdd(int _died, bool _usedDoctor, bool _usedSkill, bool _lowDamage, bool _lowHealth)
	{
		CTAMissionData cTAMissionData = new CTAMissionData();
		cTAMissionData.Died = _died;
		cTAMissionData.UsedDoctor = _usedDoctor;
		cTAMissionData.UsedSkill = _usedSkill;
		cTAMissionData.LowDamage = _lowDamage;
		cTAMissionData.LowHealth = _lowHealth;
		CTAMissionData missionData = cTAMissionData;
		CTAMissionDataAdd(missionData);
	}

	private void CTAMissionDataAdd(ICTAMissionData _missionData)
	{
		if (m_haveBuildIndex)
		{
			List<object> list = (List<object>)m_ctaData["missions"];
			if (m_ctaMissionIndex.Count >= 10)
			{
				m_ctaMissionIndex.RemoveRange(0, m_ctaMissionIndex.Count - 10 + 1);
			}
			m_ctaMissionIndex.Add(_missionData);
			if (list.Count >= 10)
			{
				list.RemoveRange(0, list.Count - 10 + 1);
			}
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary["Died"] = (long)_missionData.Died;
			dictionary["UsedDoctor"] = _missionData.UsedDoctor;
			dictionary["UsedSkill"] = _missionData.UsedSkill;
			dictionary["LowDamage"] = _missionData.LowDamage;
			dictionary["LowHealth"] = _missionData.LowHealth;
			list.Add(dictionary);
			Save();
		}
	}

	public ReadOnlyCollection<ICTAMissionData> CTAMissionDataGet()
	{
		return m_ctaMissionIndex.AsReadOnly();
	}

	public int CTACounterGet(string _key, int _default = 0)
	{
		if (m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>)
		{
			Dictionary<string, object> dictionary = value as Dictionary<string, object>;
			if (dictionary.TryGetValue(_key, out var value2) && value2 is long)
			{
				return (int)(long)value2;
			}
			return _default;
		}
		return _default;
	}

	public void CTACounterSet(string _key, int _value)
	{
		Dictionary<string, object> dictionary = (Dictionary<string, object>)((m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>) ? (value as Dictionary<string, object>) : (m_ctaData["counter"] = new Dictionary<string, object>()));
		dictionary[_key] = (long)_value;
		Save();
	}

	public int CTACounterAdd(string _key, int _value, int _default = 0)
	{
		Dictionary<string, object> dictionary = (Dictionary<string, object>)((m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>) ? (value as Dictionary<string, object>) : (m_ctaData["counter"] = new Dictionary<string, object>()));
		long num = _default;
		if (dictionary.TryGetValue(_key, out var value2) && value2 is long)
		{
			num = (long)value2;
		}
		num = CTAClampToInt(num + _value);
		dictionary[_key] = num;
		Save();
		return (int)num;
	}

	public void CTACounterSetTimestamp(string _key)
	{
		Dictionary<string, object> dictionary = (Dictionary<string, object>)((m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>) ? (value as Dictionary<string, object>) : (m_ctaData["counter"] = new Dictionary<string, object>()));
		dictionary[_key] = CTAUtcTimestamp();
		Save();
	}

	public long GetCTACounterCheckTimeLeft(string _key, long _timeInSeconds)
	{
		if (m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>)
		{
			Dictionary<string, object> dictionary = value as Dictionary<string, object>;
			if (dictionary.TryGetValue(_key, out var value2) && value2 is long)
			{
				return _timeInSeconds - (CTAUtcTimestamp() - (long)value2);
			}
			return -1L;
		}
		return -1L;
	}

	public bool CTACounterCheckTimeDiff(string _key, long _timeInSeconds)
	{
		if (m_ctaData.TryGetValue("counter", out var value) && value is Dictionary<string, object>)
		{
			Dictionary<string, object> dictionary = value as Dictionary<string, object>;
			if (dictionary.TryGetValue(_key, out var value2) && value2 is long)
			{
				return CTAUtcTimestamp() - (long)value2 > _timeInSeconds;
			}
			return true;
		}
		return true;
	}

	private static long CTAClampToInt(long _value)
	{
		if (_value > int.MaxValue)
		{
			_value = 2147483647L;
		}
		else if (_value < int.MinValue)
		{
			_value = -2147483648L;
		}
		return _value;
	}

	private static long CTAUtcTimestamp()
	{
		return (long)(DateTime.UtcNow - ServerTimeManager.UnixTimeReference).TotalSeconds;
	}

	private void Load()
	{
		m_shouldSave = false;
		if (File.Exists(TmpFile) && Load(TmpFile, _tryLoad: true))
		{
			m_shouldSave = true;
		}
		else if (Load(DataFile, _tryLoad: false))
		{
		}
		if (m_shouldSave)
		{
			SaveInternal();
		}
	}

	private void OnProfileUpdated(params object[] _params)
	{
		UpdateItemNotificationStatusOnPlayerProfileUpdated();
	}

	private void UpdateItemNotificationStatusOnPlayerProfileUpdated()
	{
		UserProfile profile = Singleton<StateManager>.Instance.ProfileManager.Profile;
		bool flag = false;
		Dictionary<string, object> dictionary = new BBDictionary<string, object>(m_itemsData.Count);
		object value;
		for (int i = 0; i < profile.Inventory.elements.Length; i++)
		{
			string id = profile.Inventory.elements[i].id;
			if (!m_itemsData.TryGetValue(id, out value))
			{
				value = false;
				flag = true;
			}
			dictionary.Add(id, value);
		}
		for (int j = 0; j < profile.assassins.assassins.Length; j++)
		{
			EquippedElement[] equippedElements = profile.assassins.assassins[j].equippedElements;
			for (int k = 0; k < equippedElements.Length; k++)
			{
				string id2 = equippedElements[k].element.id;
				flag |= !m_itemsData.TryGetValue(id2, out value) || !GetBool(value);
				dictionary.Add(id2, true);
			}
		}
		if (flag | (m_itemsData.Count != dictionary.Count))
		{
			m_itemsData = dictionary;
			m_data["items"] = m_itemsData;
			Save();
		}
	}

	private bool Load(string _file, bool _tryLoad)
	{
		m_data = null;
		m_codexData = null;
		m_itemsData = null;
		m_generalPurposeData = null;
		m_shopData = null;
		m_ctaData = null;
		m_haveBuildIndex = false;
		m_codexIndex.Clear();
		bool flag = true;
		try
		{
			if (File.Exists(_file))
			{
				string json = File.ReadAllText(_file);
				m_data = Json.Deserialize(json) as Dictionary<string, object>;
			}
		}
		catch (Exception)
		{
			m_data = null;
		}
		if (m_data == null)
		{
			if (_tryLoad)
			{
				return false;
			}
			flag = false;
			m_shouldSave = true;
			m_data = new BBDictionary<string, object>();
		}
		int num = -1;
		if (m_data.TryGetValue("version", out var value) && value is long)
		{
			num = (int)(long)value;
		}
		if (num != 7)
		{
			VersionIncrease = true;
			m_data["version"] = 7;
			m_shouldSave = true;
		}
		flag &= LoadDataSection("codex", out m_codexData, _tryLoad);
		flag &= LoadDataSection("items", out m_itemsData, _tryLoad);
		flag &= LoadDataSection("decryption", out m_decryptionData, _tryLoad);
		flag &= LoadDataSection("modules", out m_modulesData, _tryLoad);
		flag &= LoadDataSection("achievements", out m_achievementsData, _tryLoad);
		flag &= LoadDataSection("generalpurpose", out m_generalPurposeData, _tryLoad);
		flag &= LoadDataSection("shop", out m_shopData, _tryLoad);
		flag &= LoadDataSection("cta", out m_ctaData, _tryLoad);
		flag &= LoadDataSection("missionprogress", out m_missionProgressData, _tryLoad);
		return flag & LoadDataSection("entitlement", out m_entitlementNotificationData, _tryLoad);
	}

	private bool LoadDataSection(string _dataKey, out Dictionary<string, object> _dictionary, bool _tryLoad)
	{
		if (m_data.TryGetValue(_dataKey, out var value))
		{
			_dictionary = value as Dictionary<string, object>;
		}
		else
		{
			_dictionary = null;
		}
		if (_dictionary != null)
		{
			return true;
		}
		if (_tryLoad)
		{
			return false;
		}
		m_shouldSave = true;
		_dictionary = new BBDictionary<string, object>();
		m_data[_dataKey] = _dictionary;
		return true;
	}

	private void Save()
	{
		m_shouldSave = true;
		SaveInternal();
	}

	private void SaveInternal()
	{
		if (m_groupEdit)
		{
			return;
		}
		bool flag = true;
		try
		{
			string contents = Json.Serialize(m_data);
			File.WriteAllText(TmpFile, contents);
		}
		catch (Exception)
		{
			flag = false;
		}
		if (!flag)
		{
			return;
		}
		try
		{
			if (File.Exists(DataFile))
			{
				File.Delete(DataFile);
			}
			File.Move(TmpFile, DataFile);
			m_shouldSave = false;
		}
		catch (Exception)
		{
			flag = false;
		}
	}

	private static bool GetBool(KeyValuePair<string, object> _entry)
	{
		return GetBool(_entry.Value);
	}

	private static bool GetBool(object _value)
	{
		return !(_value is bool) || (bool)_value;
	}

	private static bool GetBool(object _value, bool _default)
	{
		return (!(_value is bool)) ? _default : ((bool)_value);
	}

	private static int GetInt(object _value, int _default = 0)
	{
		return (int)((!(_value is long)) ? _default : ((long)_value));
	}

	private static object GetOrNull(Dictionary<string, object> _dictionary, string _key)
	{
		if (_dictionary.TryGetValue(_key, out var value))
		{
			return value;
		}
		return null;
	}
}
