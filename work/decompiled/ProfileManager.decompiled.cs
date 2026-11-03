using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AcierProtocol;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network;
using Assets.Scripts.Network.Error;
using Assets.Scripts.UI.Interfaces;
using Assets.Scripts.UI.Mail;
using Assets.Scripts.UI.Screens.ModuleScreen;
using Assets.Scripts.UI.Types;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using BB.Mobile.Net;
using ProtoBuf;
using SharedBaseLib.Core;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.UI.State.ProfileManager;

public sealed class ProfileManager : IInAppPurchaseManagerDataStore, IProfileManagerDataStore, IValidated
{
	private delegate IAsyncDataResult<T> DelegateAsyncResult<T>();

	private const int s_UpdateTimer = 600;

	private const string CachedStaticsKey = "Statics";

	private const string CachedStaticsVersionKey = "StaticsVersion";

	private const int FetchResultMaxRetryCount = 4;

	private const int FetchResultMaxRecursionDepth = 1;

	private const float RetryRequestDelay = 5f;

	private const float RetryRequestBase = 1.6f;

	private static readonly AcierProtocolSerializer g_serializer = new AcierProtocolSerializer();

	public SimpleDelegate OnAssassinChange;

	public SimpleDelegate OnAssassinDataChanged;

	public SimpleDelegate OnProfileUpdate;

	public SimpleDelegate OnProfileUserChanged;

	public SimpleDelegate OnEventProgressChanged;

	public List<EventDelegate> onForceExceptionChange = new List<EventDelegate>();

	public List<EventDelegate> onCountersUpdate = new List<EventDelegate>();

	public List<EventDelegate> onAchievementUpdate = new List<EventDelegate>();

	public List<EventDelegate> onNewModuleActivated = new List<EventDelegate>();

	public Action<string> onDirectPurchasedVerifyed;

	private Counters m_cachedCounters;

	private readonly UnityTimer m_timer = new UnityTimer(Main.Instance);

	private readonly List<PropertyInfo> m_flatProperties;

	private DateTime m_lastUpDateTime;

	private UserProfile m_profile;

	private readonly LastProfileData m_previousProfileData = new LastProfileData();

	private readonly LastProfileData m_lastProfileDataBeforeMission = new LastProfileData();

	private readonly EntitlementManager m_entitlementManager = new EntitlementManager();

	private UIAssasinId m_selectedAssasinId = string.Empty;

	private UIAssasinId m_lastSelectedHireling = UIAssasinId.Invalid;

	private bool m_allowUpdates = true;

	public EntitlementManager EntitlementManager => m_entitlementManager;

	public bool CanExpandInventory => Profile.CanExpandInventory();

	public DateTime LastUpdate
	{
		get
		{
			return m_lastUpDateTime;
		}
		private set
		{
			m_lastUpDateTime = value;
		}
	}

	public UserProfile Profile => m_profile;

	public string LastProfileUserId { get; private set; }

	public LastProfileData PreviousProfileData => m_previousProfileData;

	public LastProfileData LastProfileDataBeforeMission => m_lastProfileDataBeforeMission;

	public HirelingAssassin[] Hirelings => CurrentAssassin.hirelingAssassins.hirelingAssassins;

	public UIAssasinId SelectedAssassin
	{
		get
		{
			return m_selectedAssasinId;
		}
		set
		{
			if (m_selectedAssasinId != value)
			{
				m_selectedAssasinId = value;
				TriggerAction(OnAssassinChange);
			}
		}
	}

	public UIAssasinId CurrentAssassinId
	{
		get
		{
			if (!string.IsNullOrEmpty(SelectedAssassin))
			{
				return SelectedAssassin;
			}
			if (!string.IsNullOrEmpty(m_profile.lastUserAssassionId))
			{
				return m_profile.lastUserAssassionId;
			}
			if (AssassinCount > 0)
			{
				return m_profile.assassins.assassins[0].id;
			}
			return string.Empty;
		}
	}

	public int CurrentAssassinIndex
	{
		get
		{
			if (CurrentAssassinId == null)
			{
				return 0;
			}
			if (!Main.Instance.IsNetworkEnabled || m_profile.assassins.assassins == null || m_profile == null)
			{
				return 0;
			}
			for (int i = 0; i < m_profile.assassins.assassins.Length; i++)
			{
				if (m_profile.assassins.assassins[i].id == CurrentAssassinId)
				{
					return i;
				}
			}
			return 0;
		}
		set
		{
			if (Main.Instance.IsNetworkEnabled && m_profile.assassins.assassins != null && m_profile != null)
			{
				if (value < 0)
				{
					SelectedAssassin = null;
				}
				else
				{
					SelectedAssassin = m_profile.assassins.assassins[value].id;
				}
			}
		}
	}

	public Assassin CurrentAssassin
	{
		get
		{
			if (CurrentAssassinId == null)
			{
				return null;
			}
			if (Main.Instance.IsNetworkEnabled && m_profile.assassins.assassins != null)
			{
				for (int i = 0; i < m_profile.assassins.assassins.Length; i++)
				{
					if (m_profile.assassins.assassins[i].id == CurrentAssassinId)
					{
						return m_profile.assassins.assassins[i];
					}
				}
				if (m_profile.assassins.assassins.Length > 0)
				{
					return m_profile.assassins.assassins[0];
				}
			}
			return null;
		}
	}

	public UIAssasinId LastSelectedHirelingId
	{
		get
		{
			if (m_lastSelectedHireling == UIAssasinId.Invalid && EntitlementExtensions.IsHirelingUnlocked())
			{
				m_lastSelectedHireling = CurrentAssassin.GetFirstHireling();
			}
			return m_lastSelectedHireling;
		}
		set
		{
			m_lastSelectedHireling = value;
		}
	}

	public HirelingAssassin LastSelectedHireling
	{
		get
		{
			if (Main.Instance.IsNetworkEnabled && m_profile.assassins.assassins != null && LastSelectedHirelingId.Valid)
			{
				for (int i = 0; i < CurrentAssassin.hirelingAssassins.hirelingAssassins.Length; i++)
				{
					if (CurrentAssassin.hirelingAssassins.hirelingAssassins[i].hirelingId == LastSelectedHirelingId)
					{
						return CurrentAssassin.hirelingAssassins.hirelingAssassins[i];
					}
				}
			}
			return null;
		}
	}

	public MissionCollection.RankMissions CurrrentRankMissions
	{
		get
		{
			if (m_profile == null)
			{
				return null;
			}
			Assassin currentAssassin = CurrentAssassin;
			if (currentAssassin == null)
			{
				return null;
			}
			return m_profile.missionCollection.GetMissionsByAssassin(currentAssassin.id)?.GetMissionsByAssassinAndRank(currentAssassin.id, Singleton<StateManager>.Instance.MissionSelectionManager.ContractHeroicMissionRank);
		}
	}

	public int AssassinCount => Profile.AssassinCount();

	public bool EnableUpdates
	{
		get
		{
			return m_allowUpdates;
		}
		set
		{
			m_timer.Enabled = value;
			m_allowUpdates = value;
		}
	}

	public bool HasOverflowOfItemsInInventory => InventoryFreeSpace < 0;

	public int InventoryFreeSpace => InventorySize - InventoryUsage;

	public int InventoryUsage => (Profile.Inventory.elements != null) ? Profile.Inventory.elements.Length : 0;

	public int InventorySize => Profile.Inventory.maxSize;

	public int Level => m_profile.level;

	public UIMissionToken MissionTicket { get; set; }

	public MissionToken MissionToken { get; set; }

	public long FreeAssassinSlots => Profile.resources.resources.Find((Resource x) => x.type == EResourceType.AssassinSlot)?.amount ?? 0;

	public bool Valid => m_profile != null;

	public int ForceErrorCategory { get; private set; }

	public int ForceErrorCode { get; private set; }

	public int ForceErrorCount { get; private set; }

	public bool ForceErrorDuringSync { get; private set; }

	public string SessionToken => (DataStore == null) ? string.Empty : DataStore.SessionToken;

	public string RequestToken => (DataStore == null) ? string.Empty : DataStore.RequestToken;

	public NetworkDataStore DataStore { get; private set; }

	public event Action<InventoryElement[], InventoryElement[], int, int> OnInventoryUpdate;

	public event Action<int, int> OnLevelUp;

	public ProfileManager()
	{
		OnProfileUpdate = new SimpleDelegate(this, new Type[0]);
		OnAssassinDataChanged = new SimpleDelegate(this, new Type[0]);
		OnProfileUserChanged = new SimpleDelegate(this, new Type[0]);
		OnAssassinChange = new SimpleDelegate(this, new Type[0]);
		OnEventProgressChanged = new SimpleDelegate(this, new Type[0]);
		ForceErrorCategory = -1;
		ForceErrorCode = -1;
		ForceErrorCount = 0;
		ForceErrorDuringSync = false;
		UnityTimer timer = m_timer;
		timer.OnInteval = (UnityTimer.OnIntevalHandler)Delegate.Combine(timer.OnInteval, new UnityTimer.OnIntevalHandler(HandleTimerEvent));
		PropertyInfo[] properties = typeof(UserProfile).GetProperties(BindingFlags.Instance | BindingFlags.Public);
		m_flatProperties = new List<PropertyInfo>(properties.Length);
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (propertyInfo.PropertyType.GetInterface("IExtensible") == null)
			{
				m_flatProperties.Add(propertyInfo);
			}
		}
	}

	private void UpdateProfileTimers(UserProfile _profile)
	{
		m_timer.Stop();
		int num = 600;
		for (int i = 0; i < _profile.AssassinCount(); i++)
		{
			Assassin assassin = _profile.assassins.assassins[i];
			if (assassin.equippedElements == null)
			{
				continue;
			}
			for (int j = 0; j < assassin.equippedElements.Length; j++)
			{
				EquippedElement equippedElement = assassin.equippedElements[j];
				if (equippedElement.element.IsModule() && equippedElement.element.moduleData != null)
				{
					long num2 = equippedElement.element.moduleData.ComputeModuleExpireInMilliSeconds();
					if (num2 > 0)
					{
						num = Math.Min(Math.Max((int)num2 / 1000, 1), num);
					}
				}
			}
		}
		long lowestRemainingQuestTimeInMilliSeconds = Profile.GetLowestRemainingQuestTimeInMilliSeconds();
		if (lowestRemainingQuestTimeInMilliSeconds > 0)
		{
			int num3 = Math.Max((int)lowestRemainingQuestTimeInMilliSeconds / 1000, 1);
			if (num3 < num)
			{
				num = num3;
			}
		}
		m_timer.Interval = num;
		m_timer.Start();
	}

	private void Merge(Achievement[] _achievements)
	{
		for (int i = 0; i < _achievements.Length; i++)
		{
			Merge(_achievements[i], _fireEvent: false);
		}
		EventDelegate.Execute(onAchievementUpdate);
	}

	private void Merge(Achievement _achievement, bool _fireEvent = true)
	{
		for (int i = 0; i < Profile.achievements.entries.Length; i++)
		{
			if (Profile.achievements.entries[i].definitionId == _achievement.definitionId)
			{
				Profile.achievements.entries[i] = _achievement;
				if (_fireEvent)
				{
					EventDelegate.Execute(onAchievementUpdate);
				}
				return;
			}
		}
		Profile.achievements.entries = Profile.achievements.entries.Add(_achievement);
		if (_fireEvent)
		{
			EventDelegate.Execute(onAchievementUpdate);
		}
	}

	private void Merge(FriendProfiles _friends)
	{
		if (_friends != null)
		{
			Profile.friends = _friends;
		}
	}

	public void ForceMerge(UserProfile _userProfile)
	{
		CopyProfileDate(m_profile, m_previousProfileData);
		m_profile = null;
		SetUserProfile(_userProfile);
	}

	private void Merge(UserProfile _userProfile)
	{
		bool flag = false;
		if (m_profile == null)
		{
			m_profile = _userProfile;
			flag = !string.IsNullOrEmpty(LastProfileUserId) && m_profile.id != LastProfileUserId;
			LastProfileUserId = m_profile.id;
			Inventory inventory = new Inventory();
			if (m_profile.Inventory != null)
			{
				inventory = m_profile.Inventory;
			}
			List<EquippedElement> list = new List<EquippedElement>();
			if (Profile.assassins != null && Profile.assassins.assassins != null)
			{
				for (int i = 0; i < Profile.assassins.assassins.Length; i++)
				{
					Assassin assassin = Profile.assassins.assassins[i];
					for (int j = 0; j < assassin.equippedElements.Length; j++)
					{
						list.Add(assassin.equippedElements[j]);
					}
				}
			}
			if (flag)
			{
				TriggerAction(OnProfileUserChanged);
				m_profile.UpdateLocalStorageForNewProfile();
			}
			m_entitlementManager.UpdatGrantedeEntitlements(_userProfile.entitlementInventory);
			m_profile.counters = MergeCounters(_userProfile.counters);
			Singleton<StateManager>.Instance.ItemNotificationManager.SendUpdate(inventory, null, list);
			if (OnInventoryUpdate != null)
			{
				OnInventoryUpdate(inventory.elements, null, inventory.maxSize, 0);
			}
			if (m_profile.counters != null)
			{
				EventDelegate.Execute(onCountersUpdate);
			}
			if (m_profile.achievements != null)
			{
				EventDelegate.Execute(onAchievementUpdate);
			}
			if (m_profile.eventsProgress != null)
			{
				TriggerAction(OnEventProgressChanged);
			}
			if (m_profile.assassins != null)
			{
				TriggerAction(OnAssassinDataChanged);
			}
			return;
		}
		if (Profile.id != _userProfile.id)
		{
			if (DataStore != null)
			{
				DataStore.SetApplicationUsername(_userProfile.id);
			}
			LastProfileUserId = m_profile.id;
			flag = true;
		}
		if (m_profile.level < _userProfile.level && OnLevelUp != null)
		{
			OnLevelUp(m_profile.level, _userProfile.level);
		}
		foreach (PropertyInfo flatProperty in m_flatProperties)
		{
			flatProperty.SetValue(Profile, flatProperty.GetValue(_userProfile, null), null);
		}
		bool flag2 = false;
		CopyProfileDate(m_profile, m_previousProfileData);
		if (_userProfile.assassins != null)
		{
			Profile.assassins = _userProfile.assassins;
			TriggerAction(OnAssassinDataChanged);
		}
		if (_userProfile.resources != null)
		{
			Profile.resources = _userProfile.resources;
		}
		if (_userProfile.counters != null)
		{
			Profile.counters = MergeCounters(_userProfile.counters);
			flag2 = true;
		}
		if (_userProfile.friends != null)
		{
			Profile.friends = _userProfile.friends;
		}
		if (_userProfile.achievements != null)
		{
			Profile.achievements = _userProfile.achievements;
		}
		if (_userProfile.payUnlockedClasses != null)
		{
			Profile.payUnlockedClasses = _userProfile.payUnlockedClasses;
		}
		if (_userProfile.tutorial != null)
		{
			Profile.tutorial = _userProfile.tutorial;
		}
		if (_userProfile.entitlementInventory != null)
		{
			m_entitlementManager.UpdatGrantedeEntitlements(_userProfile.entitlementInventory);
			Profile.entitlementInventory = _userProfile.entitlementInventory;
		}
		if (_userProfile.missionCollection != null)
		{
			Profile.missionCollection = _userProfile.missionCollection;
		}
		if (_userProfile.progressionQuests != null)
		{
			Profile.progressionQuests = _userProfile.progressionQuests;
		}
		if (_userProfile.userSettings != null)
		{
			Profile.userSettings = _userProfile.userSettings;
		}
		if (_userProfile.eventsProgress != null)
		{
			Profile.eventsProgress = _userProfile.eventsProgress;
			TriggerAction(OnEventProgressChanged);
		}
		if (flag)
		{
			TriggerAction(OnProfileUserChanged);
			m_profile.UpdateLocalStorageForNewProfile();
		}
		if (flag2)
		{
			EventDelegate.Execute(onCountersUpdate);
		}
		if (_userProfile.dirtyFlags != 0)
		{
			if ((_userProfile.dirtyFlags & 2) != 0)
			{
				GetMyFriends(delegate
				{
					TriggerAction(OnProfileUpdate);
				});
			}
			if ((_userProfile.dirtyFlags & 8) != 0)
			{
				MailCache.Clear();
			}
			if ((_userProfile.dirtyFlags & 1) != 0)
			{
				Singleton<StateManager>.Instance.MailManager.GetMailHeaders(delegate
				{
					TriggerAction(OnProfileUpdate);
				});
			}
			if ((_userProfile.dirtyFlags & 4) != 0)
			{
				UpdateUserProfile(_needInventory: false, _needAssassins: false, _needResources: false, _needMissions: false, _needCounter: true, _needAchievements: false, _needTutorial: false, _needEntitlements: false, _needUserSettings: false, _needProgressionQuests: false, _needEventsProgress: false, delegate
				{
					TriggerAction(OnProfileUpdate);
					EventDelegate.Execute(onCountersUpdate);
				});
			}
		}
		if (_userProfile.Inventory == null)
		{
			return;
		}
		Inventory inventory2 = Profile.Inventory;
		Profile.Inventory = _userProfile.Inventory;
		Inventory inventory3 = Profile.Inventory;
		List<EquippedElement> list2 = new List<EquippedElement>();
		for (int num = 0; num < Profile.assassins.assassins.Length; num++)
		{
			for (int num2 = 0; num2 < Profile.assassins.assassins[num].equippedElements.Length; num2++)
			{
				list2.Add(Profile.assassins.assassins[num].equippedElements[num2]);
			}
		}
		Singleton<StateManager>.Instance.ItemNotificationManager.SendUpdate(inventory3, inventory2, list2);
		if (OnInventoryUpdate != null)
		{
			OnInventoryUpdate(inventory3.elements, inventory2.elements, inventory3.maxSize, inventory2.maxSize);
		}
	}

	private Counters MergeCounters(Counters _serverCounters)
	{
		if (m_cachedCounters == null)
		{
			ECounterType[] array = Enum.GetValues(typeof(ECounterType)).Cast<ECounterType[]>();
			int num = (int)(array[array.Length - 1] + 1);
			m_cachedCounters = new Counters
			{
				counters = new Counter[num]
			};
			for (int i = 0; i < m_cachedCounters.counters.Length; i++)
			{
				m_cachedCounters.counters[i] = new Counter
				{
					ECounterTypeOrdinal = i,
					num = 0L
				};
			}
		}
		int num2 = m_cachedCounters.counters.Length;
		for (int j = 0; j < num2; j++)
		{
			m_cachedCounters.counters[j].num = 0L;
		}
		for (int k = 0; k < _serverCounters.counters.Length; k++)
		{
			Counter counter = _serverCounters.counters[k];
			if (counter.ECounterTypeOrdinal < num2)
			{
				m_cachedCounters.counters[counter.ECounterTypeOrdinal].num = counter.num;
			}
		}
		return m_cachedCounters;
	}

	public void SaveLastProfileDateBeforeMission()
	{
		CopyProfileDate(Profile, LastProfileDataBeforeMission);
	}

	private void CopyProfileDate(UserProfile _profile, LastProfileData _target)
	{
		if (Main.Instance.IsNetworkEnabled)
		{
			_target.MissionUpdateShownToUse = false;
			_target.Assassins = _profile.assassins;
			_target.EntitlementInventory = _profile.entitlementInventory;
			_target.MissionCollection = _profile.missionCollection;
			_target.Counters = _profile.counters;
			_target.ProgressionQuests = _profile.progressionQuests;
			_target.EventsProgress = _profile.eventsProgress;
		}
	}

	private void TriggerAction(SimpleDelegate _action, params object[] _params)
	{
		_action?.Trigger(_params);
	}

	private void HandleTimerEvent(float _duration, int _loops)
	{
		m_timer.Stop();
		UpdateUserProfile();
	}

	private void GenericDataStoreCall<T>(DelegateAsyncResult<T> delegateAsyncResult, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, Action<T> delegateHandleUpdate = null, int _callDepth = 0) where T : IExtensible
	{
		Main.Instance.StartCoroutine(FetchGenericResponse(delegateAsyncResult, _callback, _scheduler, delegateHandleUpdate, _callDepth));
	}

	public void UpdateUserProfile(bool _needInventory, bool _needAssassins, bool _needResources, bool _needMissions, bool _needCounter, bool _needAchievements, bool _needTutorial, bool _needEntitlements, bool _needUserSettings, bool _needProgressionQuests, bool _needEventsProgress, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, int _callDepth = 0)
	{
		GenericDataStoreCall(() => DataStore.GetPlayerProfile(_needInventory, _needAssassins, _needResources, _needMissions, _needCounter, _needAchievements, _needTutorial, _needEntitlements, _needUserSettings, _needProgressionQuests, _needEventsProgress), _callback, _scheduler, null, _callDepth);
	}

	public void UpdateUserProfile(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, int _callDepth = 0)
	{
		GenericDataStoreCall(() => DataStore.GetPlayerProfile(), _callback, _scheduler, null, _callDepth);
	}

	public void VerifyReceipt(string _productId, string _transactionId, string _originalTransactionId, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.VerifyReceipt(_transactionId, _originalTransactionId, _receiptData, _currencyCode, _cost, _formattedPrice), _callback, _scheduler, (VerifyReceiptResponse response) =>
		{
			FinishReceiptValidation(_currencyCode, _cost, _productId, response.ubiTransactionId, response.transactionId);
		});
	}

	public void VerifyGoogleReceipt(string _productId, string _receiptSignature, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.VerifyGoogleReceipt(_receiptSignature, _receiptData, _currencyCode, _cost, _formattedPrice), _callback, _scheduler, (VerifyGoogleReceiptResponse response) =>
		{
			FinishReceiptValidation(_currencyCode, _cost, _productId, response.ubiTransactionId, response.purchaseToken);
		});
	}

	private void FinishReceiptValidation(string _currencyCode, string _cost, string _productId, string _ubiTransactionId, string _purchaseTokenReceipt)
	{
		DataStore.FinishTransaction(_purchaseTokenReceipt);
		if (!string.IsNullOrEmpty(_ubiTransactionId))
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary.Add("Money_Currency", _currencyCode);
			dictionary.Add("Money_IAP", _cost);
			dictionary.Add("Object_ID", _productId);
			dictionary.Add("Transaction_ID", _ubiTransactionId);
			FlurryAnalyticsManager.LogEventWithParameters(FlurryAnalyticsManager.LogEventIds.IAP, dictionary);
			Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
			dictionary2.Add("currencyCode", _currencyCode);
			dictionary2.Add("rawPrice", _cost);
			dictionary2.Add("productId", _productId);
			dictionary2.Add("transactionId", _ubiTransactionId);
			FacebookManager.LogPurchase(float.Parse(dictionary2["rawPrice"] as string), dictionary2["currencyCode"] as string, dictionary2);
			MATManager.LogPurchase(_productId, _ubiTransactionId, float.Parse(_cost), _currencyCode);
			if (onDirectPurchasedVerifyed != null)
			{
				onDirectPurchasedVerifyed(_productId);
			}
		}
	}

	public void ModifyUserSettings(UserSettings _userSettings, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ModifyUserSettings(_userSettings), _callback, _scheduler);
	}

	public void ModifyPlayerProfileName(string _profileName, ESocialNetwork _socialNetwork, string _accessToken = null, EModuleOrigin _origin = EModuleOrigin.ModuleOrigin_Invalid, bool _forceFacebookLinkToCurrent = false, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ModifyPlayerProfileName(_profileName, _socialNetwork, _accessToken, _origin, _forceFacebookLinkToCurrent), _callback, _scheduler);
	}

	public void QueryFbStatus(string _fbAccessToken, ESocialNetwork _socialNetwork, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.QueryFBStatus(_fbAccessToken, _socialNetwork), _callback, _scheduler);
	}

	public void QueryFbStatusById(string _facebookId, ESocialNetwork _socialNetwork, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.QueryFBStatusById(_facebookId, _socialNetwork), _callback, _scheduler);
	}

	public void GetStatics(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, int _callDepth = 0)
	{
		GenericDataStoreCall(() => DataStore.GetStatics(0L), _callback, _scheduler, (GetStaticsResponse response) =>
		{
			DataStore.UpdateStaticsVersion(response.statics.staticsVersion);
			updateTemplateManager(response.statics);
		}, _callDepth);
	}

	private void updateTemplateManager(Statics statics)
	{
		Singleton<StateManager>.Instance.TemplateManager.AvatarDefinition = statics.avatarClassDefinitions;
		Singleton<StateManager>.Instance.TemplateManager.XpPerLevelTable = statics.xpPerLevelTable;
		Singleton<StateManager>.Instance.TemplateManager.ItemSellMapping = statics.itemSellMapping;
		Singleton<StateManager>.Instance.TemplateManager.LocationUnlockMappings = statics.locationUnlockMappings;
		Singleton<StateManager>.Instance.TemplateManager.ShopTable = statics.shopTable;
		Singleton<StateManager>.Instance.TemplateManager.TimeToHardCurrencyMapping = statics.timeToHardCurrencyMapping;
		Singleton<StateManager>.Instance.TemplateManager.Globals = statics.globals;
		Singleton<StateManager>.Instance.TemplateManager.DirectPurchasesShopTable = statics.hcShopTable;
		Singleton<StateManager>.Instance.TemplateManager.AchievementCategoryMapping = statics.achievementCategoryMapping;
		Singleton<StateManager>.Instance.TemplateManager.AchievementMapping = statics.achievementMapping;
		Singleton<StateManager>.Instance.TemplateManager.InventorySlotsCostsMapping = statics.inventorySlotCostsMapping;
		Singleton<StateManager>.Instance.TemplateManager.AssassinSlotsCostsMapping = statics.assassinSlotCostsMapping;
		Singleton<StateManager>.Instance.TemplateManager.SkillTrainingTimeMapping = statics.skillTrainingTimeMapping;
		Singleton<StateManager>.Instance.TemplateManager.SecondSkillTreeCostMapping = statics.secondSkillTreeCostMapping;
		Singleton<StateManager>.Instance.TemplateManager.LevelSkillPointMapping = statics.levelSkillPointMapping;
		Singleton<StateManager>.Instance.TemplateManager.EventTemplates = statics.eventTemplates;
		Singleton<StateManager>.Instance.TemplateManager.EventsUserFriendlyMapping = statics.eventsUserFriendlyMapping;
		Singleton<StateManager>.Instance.TemplateManager.ForgeMapping = statics.forgeMapping;
		Singleton<StateManager>.Instance.TemplateManager.MysteryBoxMapping = statics.mysteryBoxMapping;
		Singleton<StateManager>.Instance.TemplateManager.CampaignCatalog = statics.campaignCatalog;
		Singleton<StateManager>.Instance.TemplateManager.StoryMissionRewardMapping = statics.storyMissionRewardMapping;
		Singleton<StateManager>.Instance.TemplateManager.MailBoxMapping = statics.mailBoxMapping;
		Singleton<StateManager>.Instance.TemplateManager.ResourcePropertiesMapping = statics.resourcePropertiesMapping;
		Singleton<StateManager>.Instance.TemplateManager.CounterTypeLocaKeyMapping = statics.counterTypeLocaKeyMapping;
		Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping = null;
		if (statics.heroicRankMapping != null)
		{
			Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping = new HeroicRankMapping.Entry[statics.heroicRankMapping.entries.Length];
			for (int i = 0; i < statics.heroicRankMapping.entries.Length; i++)
			{
				HeroicRankMapping.Entry entry = statics.heroicRankMapping.entries[i];
				Singleton<StateManager>.Instance.TemplateManager.HeroicRankMapping[entry.rankIdx] = entry;
			}
		}
		Singleton<StateManager>.Instance.TemplateManager.MissionLootScalingMapping = statics.missionLootScalingMapping;
		Singleton<StateManager>.Instance.TemplateManager.MissionTypeLootMapping = statics.missionTypeLootMapping;
		Singleton<StateManager>.Instance.TemplateManager.TutorialCategoryMapping = statics.tutorialCategoryMapping;
	}

	public void BuyResources(Dictionary<int, long> _resourceAmounts, EModuleOrigin _origin, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.BuyResources(_resourceAmounts, _origin), _callback, _scheduler);
	}

	public void TrainAssassin(string _assassinId, AssassinSkillInTraining.SkillClass _skillTierClass, int _tierId, AssassinSkillInTraining.SkillType _tierSkillType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.TrainAssassin(_assassinId, _skillTierClass, _tierId, _tierSkillType), _callback, _scheduler);
	}

	public void TrainAssassinDuringTutorial(string _assassinId, AssassinSkillInTraining.SkillClass _skillTierClass, int _tierId, AssassinSkillInTraining.SkillType _tierSkillType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.TrainAssassinDuringTutorial(_assassinId, _skillTierClass, _tierId, _tierSkillType), _callback, _scheduler);
	}

	public void UnlockTier(string _assassinId, int _tierId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.UnlockTier(_assassinId, _tierId, _skillClass), _callback, _scheduler);
	}

	public void UnlockPassiveSkill(string _assassinId, int _tierId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.UnlockPassiveSkill(_assassinId, _tierId, _skillClass), _callback, _scheduler);
	}

	public void CollectAdRewards(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.CollectAdRewards(), _callback, _scheduler);
	}

	public void EquipElement(string _assassinId, string _elementId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.EquipElement(_assassinId, _elementId, _callback, _scheduler), _callback, _scheduler);
	}

	public void UnequipElement(string _assassinId, string _elementId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.UnequipElement(_assassinId, _elementId), _callback, _scheduler);
	}

	public void SellItem(string _itemId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SellItem(_itemId), _callback, _scheduler);
	}

	public void HandleOpenMissionResponse(OpenMissionResponse response)
	{
		Singleton<StateManager>.Instance.ProfileManager.MissionTicket = response.missionToken.token;
		Singleton<StateManager>.Instance.ProfileManager.MissionToken = response.missionToken;
		UbiCore.Utils.Singleton<UiMain>.Instance.OnEnterNextMission();
		UbiCore.Utils.Singleton<AvatarData>.Instance.PrepareMission(Singleton<StateManager>.Instance.ProfileManager.Profile.GetAssassin(response.missionToken.assassinId), Singleton<StateManager>.Instance.ProfileManager.GetHireling(response.missionToken.hirelingId), response.missionToken.module);
	}

	public void OpenMission(string _missionGuid, string _assassinId, string _hirelingId, string _campaignId, int _rank, bool _isRetry, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		UbiCore.Utils.Singleton<Assets.Scripts.Mission.MissionManager>.Instance.Mute();
		GenericDataStoreCall(() => DataStore.OpenMission(_missionGuid, _assassinId, _hirelingId, _campaignId, _rank, _isRetry), _callback, _scheduler, HandleOpenMissionResponse);
	}

	public void EndMission(MissionResult _missionResult, string _ticket, bool _won, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.EndMission(_missionResult, _ticket, _won), _callback, _scheduler);
	}

	public void GetMissionInfo(string _missionGuid, string _assassinId, string _hirelingId, string _campaignId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMissionInfo(_missionGuid, _assassinId, _hirelingId, _campaignId, _rank), _callback, _scheduler);
	}

	public void RefreshMissionList(string _assassinId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.RefreshMissionList(_assassinId, _rank), _callback, _scheduler);
	}

	public void Revive(string _missionToken, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.Revive(_missionToken, _paymentResource), _callback, _scheduler);
	}

	public void RemoveHirelingAndMission(string _campaignId, string _missionId, string _assassinId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		UbiCore.Utils.Singleton<Assets.Scripts.Mission.MissionManager>.Instance.Mute();
		GenericDataStoreCall(() => DataStore.RemoveHirelingAndMission(_campaignId, _missionId, _assassinId, _rank), _callback, _scheduler);
	}

	public void CreateAssassin(Assassin.EClassTypeId _avatarType, string _headGuid, string _customName, int _firstNameLocaId, int _lastNameLocaId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.CreateAssassin(_avatarType, _headGuid, _customName, _firstNameLocaId, _lastNameLocaId, Profile.AssassinCount()), _callback, _scheduler);
	}

	public void DeleteAssassin(string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.DeleteAssassin(_assassinId), _callback, _scheduler);
	}

	public void GetForeignAssassinByAssassinId(string _userIdHash, string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetForeignAssassinByAssassinId(_userIdHash, _assassinId), _callback, _scheduler);
	}

	public void GetForeignAssassinByHirelingId(string _hirelingId, string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetForeignAssassinByHirelingId(_hirelingId, _assassinId), _callback, _scheduler);
	}

	public void GetForeignAssassinByUserId(string _userId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetForeignAssassinByUserId(_userId), _callback, _scheduler);
	}

	public void ResetSkillTree(string _assassinId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ResetSkillTree(_assassinId, _skillClass), _callback, _scheduler);
	}

	public void SetExtendedClass(string _assassinId, Assassin.EClassTypeId classType, EResourceType _resourceType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SetExtendedClass(_assassinId, classType, _resourceType), _callback, _scheduler);
	}

	public void UpdateSkillWheel(string _assassinId, SkillWheel skillWheel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.UpdateSkillWheel(_assassinId, skillWheel), _callback, _scheduler);
	}

	public void UserProfileCheat(CheatRequest.CheatType _cheatType, string _assassinId = "", CheatParameters _params = null, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.UserProfileCheat(_cheatType, _assassinId, _params), _callback, _scheduler, (CheatResponse response) =>
		{
		});
	}

	public void GetMissionByName(string _name, bool _isElite, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMissionByName(_name, _isElite), _callback, _scheduler);
	}

	public void SimulateLootRoll(string _rollCode, bool _giveLoot, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SimulateLootRoll(_rollCode, _giveLoot), _callback, _scheduler);
	}

	public void PurchaseInGameShopItem(AcierProtocol.Resources _costs, string _shopItemId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.PurchaseInGameShopItem(_costs, _shopItemId), _callback, _scheduler);
	}

	public void PurchaseAssassinSlot(int _numAlreadyPurchasedSlots, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.PurchaseAssassinSlot(_numAlreadyPurchasedSlots, _paymentResource), _callback, _scheduler);
	}

	public void PurchaseInventorySlot(int _numSlotPurchases, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.PurchaseInventorySlot(_numSlotPurchases, _paymentResource), _callback, _scheduler);
	}

	public void EarlyAccessClass(Assassin.EClassTypeId _classType, EResourceType _currency, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.EarlyAccessClass(_classType, _currency), _callback, _scheduler);
	}

	public void GetMailHeaders(long _lastTimeStamp, int _maxHeaders = 0, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMailHeaders(_lastTimeStamp, _maxHeaders), _callback, _scheduler);
	}

	public void GetMailBody(string _headerId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMailBody(_headerId), _callback, _scheduler);
	}

	public void ProcessMail(string _headerId, bool _accept, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ProcessMail(_headerId, _accept), _callback, _scheduler);
	}

	public void SendMail(string _receiverId, string _subject, string _body, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SendMail(_receiverId, _subject, _body), _callback, _scheduler);
	}

	public void DeleteMails(List<string> _headerIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.DeleteMails(_headerIds), _callback, _scheduler);
	}

	public void SendFriendRequest(string _userIdHash, EModuleOrigin _origin, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SendFriendRequest(_userIdHash, _origin), _callback, _scheduler);
	}

	public void GetMyFriends(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMyFriends(), _callback, _scheduler, (GetMyFriendsResponse response) =>
		{
			Merge(response.friends);
		});
	}

	public void RemoveFriend(string _userId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.RemoveFriend(_userId), _callback, _scheduler);
	}

	public void ReadAchievement(string _achievmentId, int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ReadAchievement(_achievmentId, _rankIdx), _callback, _scheduler, (ReadAchievementResponse response) =>
		{
			Merge(response.readAchievement);
		});
	}

	public void ClaimAchievement(string _achievmentId, int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ClaimAchievement(_achievmentId, _rankIdx), _callback, _scheduler, (ClaimAchievementResponse response) =>
		{
			Merge(response.claimedAchievement);
		});
	}

	public void ClaimQuestReward(int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ClaimQuestReward(_rankIdx), _callback, _scheduler);
	}

	public void ClaimWeeklyEventReward(string _challengeId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ClaimWeeklyEventReward(_challengeId), _callback, _scheduler);
	}

	public void GetLeaderBoards(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetLeaderBoards(), _callback, _scheduler);
	}

	public void GetResetProfile(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetResetProfile(), _callback, _scheduler);
	}

	public void OpenMysteryBox(Quality.EQuality _quality, EMysteryBoxType _mysteryBoxType, string _assassinId, string _itemId, bool _startChain, bool _overspending, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.OpenMysteryBox(_quality, _mysteryBoxType, _assassinId, _itemId, _startChain, _overspending), _callback, _scheduler);
	}

	public void ClaimMysteryBox(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ClaimMysteryBox(), _callback, _scheduler);
	}

	public void GetMysteryBoxInfo(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetMysteryBoxInfo(), _callback, _scheduler);
	}

	public void ReachNewAchievements(List<string> _achievementIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ReachNewAchievements(_achievementIds), _callback, _scheduler, (ReachNewAchievementsResponse response) =>
		{
			Merge(response.achievements.entries);
		});
	}

	public void SetTutorialStep(ETutorialCategory _category, int _counter, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SetTutorialStep(_category, _counter), _callback, _scheduler);
	}

	public void SkipTutorial(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SkipTutorial(), _callback, _scheduler);
	}

	public void SetTutorialFinished(ETutorialCategory _category, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.SetTutorialFinished(_category), _callback, _scheduler);
	}

	public void Forge(List<string> _elementIds, Resource _expectedCost, int _expectedItemLevel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.Forge(_elementIds, _expectedCost, _expectedItemLevel), _callback, _scheduler);
	}

	public void ForgeReroll(Resource _expectedCost, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ForgeReroll(_expectedCost), _callback, _scheduler);
	}

	public void ForgeDuringTutorial(List<string> _elementIds, Resource _expectedCost, int _expectedItemLevel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ForgeDuringTutorial(_elementIds, _expectedCost, _expectedItemLevel), _callback, _scheduler);
	}

	public void ForgeRerollDuringTutorial(Resource _expectedCost, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.ForgeRerollDuringTutorial(_expectedCost), _callback, _scheduler);
	}

	public void GetTwitterTimeline(ELanguages _language, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		GenericDataStoreCall(() => DataStore.GetTwitterTimeline(_language), _callback, _scheduler);
	}

	private IEnumerator FetchGenericResponse<T>(DelegateAsyncResult<T> delegateAsyncResult, AsyncCallback _callback, IAsyncCallbackScheduler _scheduler, Action<T> delegateHandleUpdate, int _callDepth = 0) where T : IExtensible
	{
		Exception error = null;
		int counter = 0;
		IAsyncDataResult<T> asyncResult;
		do
		{
			if ((_callDepth == 0 || ForceErrorDuringSync) && ForceErrorCode >= 0 && ForceErrorCount > 0)
			{
				asyncResult = null;
				yield return Main.Instance.StartCoroutine(Timer.WaitForSecondsUnscaled(0.1f));
			}
			else
			{
				asyncResult = delegateAsyncResult();
				yield return Main.Instance.StartCoroutine(WaitForCompleted(asyncResult));
			}
			error = UpdateProfileUsingResponseType(asyncResult, out var dataStoreRetryResult, delegateHandleUpdate);
			if (error is AcierErrorException acierErrorException)
			{
				if (acierErrorException.Error.ErrorCode == ErrorCode.WESTimeout)
				{
					ErrorState errorState = new ErrorState
					{
						ErrorCode = ErrorCode.UnknownCode,
						ErrorGroup = ErrorCategory.UnknownCategory
					};
					yield return Main.Instance.StartCoroutine(errorState.DetermineReachabilityError(acierErrorException));
					if (errorState.ErrorCode != ErrorCode.UnknownCode)
					{
						error = new AcierErrorException(new Assets.Scripts.Network.Error.Error(errorState), error);
						break;
					}
				}
				else if (acierErrorException.Error.ShouldNotfiyUser)
				{
					if (dataStoreRetryResult == Assets.Scripts.Network.Error.Error.DataStoreRetryResult.UpdateProfile && _callDepth < 1)
					{
						yield return Main.Instance.StartCoroutine(HandleDataStoreError(dataStoreRetryResult, _callDepth));
					}
					break;
				}
			}
			if (dataStoreRetryResult != Assets.Scripts.Network.Error.Error.DataStoreRetryResult.None && _callDepth < 1)
			{
				yield return Main.Instance.StartCoroutine(HandleDataStoreError(dataStoreRetryResult, _callDepth));
			}
			if (error != null && counter + 1 < 4)
			{
				float waitTime = 5f * (float)Math.Pow(1.600000023841858, counter);
				yield return Main.Instance.StartCoroutine(Timer.WaitForSecondsUnscaled(waitTime));
			}
			counter++;
		}
		while (error != null && counter < 4);
		if (error != null && counter >= 4)
		{
			error = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnableToRequest, ErrorCategory.GeneralCategory), error);
			ErrorState errorState2 = new ErrorState
			{
				ErrorCode = ErrorCode.UnknownCode,
				ErrorGroup = ErrorCategory.UnknownCategory
			};
			yield return Main.Instance.StartCoroutine(errorState2.DetermineReachabilityError((AcierErrorException)error));
			if (errorState2.ErrorCode != ErrorCode.UnknownCode)
			{
				error = new AcierErrorException(new Assets.Scripts.Network.Error.Error(errorState2), error);
			}
		}
		yield return new WaitForEndOfFrame();
		copyAsyncDataResultAndShedule(asyncResult, error, _callback, _scheduler);
	}

	private IEnumerator HandleDataStoreError(Assets.Scripts.Network.Error.Error.DataStoreRetryResult dataStoreRetryResult, int _callDepth)
	{
		WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();
		if (dataStoreRetryResult == Assets.Scripts.Network.Error.Error.DataStoreRetryResult.Reconnect)
		{
			bool autherizationFinished = false;
			DataStore.ReAuthenticate((IAsyncResult _ar) =>
			{
				autherizationFinished = true;
			});
			while (!autherizationFinished)
			{
				yield return waitForEndOfFrame;
			}
		}
		else
		{
			bool finishedProfile = false;
			UpdateUserProfile((IAsyncResult ar) =>
			{
				finishedProfile = true;
			}, null, _callDepth + 1);
			while (!finishedProfile)
			{
				yield return waitForEndOfFrame;
			}
		}
	}

	private void copyAsyncDataResultAndShedule<T>(IAsyncDataResult<T> _originResult, Exception _error, AsyncCallback _callback, IAsyncCallbackScheduler _scheduler) where T : IExtensible
	{
		AsyncDataResult<T> asyncDataResult = new AsyncDataResult<T>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		if (_error != null)
		{
			asyncDataResult.Complete(_error, _synchronous: true);
		}
		else
		{
			asyncDataResult.Complete(_originResult.OriginResponse, _originResult.Result(), null, _synchronous: true);
		}
		if (_scheduler == null)
		{
			_callback?.Invoke(asyncDataResult);
		}
	}

	public void Update()
	{
		DataStore.Update();
	}

	public bool AbandonUserProfile()
	{
		PlayerPrefs.DeleteAll();
		UbiCore.Utils.Singleton<LocalStorage>.Instance.Clear();
		return DataStore.AbandonUserProfile();
	}

	private Exception UpdateProfileUsingResponseType<T>(IAsyncDataResult<T> _result, out Assets.Scripts.Network.Error.Error.DataStoreRetryResult _dataStoreRetryResult, Action<T> delegateHandleUpdate)
	{
		_dataStoreRetryResult = Assets.Scripts.Network.Error.Error.DataStoreRetryResult.None;
		try
		{
			if (_result == null)
			{
				ForceErrorCount--;
				EventDelegate.Execute(onForceExceptionChange);
				throw new AcierErrorException(new Assets.Scripts.Network.Error.Error((ErrorCode)ForceErrorCode, (ErrorCategory)ForceErrorCategory));
			}
			if (delegateHandleUpdate == null)
			{
				_result.Result();
			}
			else
			{
				delegateHandleUpdate(_result.Result());
			}
			if (_result.OriginResponse != null && _result.OriginResponse.userProfile != null)
			{
				SetUserProfile(_result.OriginResponse.userProfile);
			}
		}
		catch (Exception ex)
		{
			AcierErrorException ex2 = ex as AcierErrorException;
			if (ex2 == null || !ErrorWhiteList.IsOnWhiteList(ex2.Error.ErrorCategory, ex2.Error.ErrorCode))
			{
			}
			if (ex2 != null)
			{
				_dataStoreRetryResult = ex2.Error.DataStoreResult;
			}
			if (_result == null || _result.OriginResponse == null || _result.OriginResponse.eventTrigger != null)
			{
			}
			return ex;
		}
		return null;
	}

	public void GetProductPricings(string[] _productIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, int _callDepth = 0)
	{
		GenericDataStoreCall(() => DataStore.GetProductPricings(_productIds), _callback, _scheduler);
	}

	public void PurchaseProductFromShop(string _productId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null, int _callDepth = 0)
	{
		GenericDataStoreCall(() => DataStore.BuyProduct(_productId), _callback, _scheduler);
	}

	private IEnumerator WaitForCompleted(IAsyncResult _ar)
	{
		while (!_ar.IsCompleted)
		{
			yield return new WaitForEndOfFrame();
		}
	}

	public HirelingAssassin GetHireling(UIAssasinId _id)
	{
		for (int i = 0; i < CurrentAssassin.hirelingAssassins.hirelingAssassins.Length; i++)
		{
			if (CurrentAssassin.hirelingAssassins.hirelingAssassins[i].hirelingId == _id)
			{
				return CurrentAssassin.hirelingAssassins.hirelingAssassins[i];
			}
		}
		return null;
	}

	public void SetDataStore(NetworkDataStore dataStore)
	{
		if (DataStore != null)
		{
			dataStore.UserProfileHash = DataStore.UserProfileHash;
		}
		DataStore = dataStore;
	}

	private void SetUserProfile(UserProfile _usr)
	{
		if (_usr != null)
		{
			Merge(_usr);
			TriggerAction(OnProfileUpdate);
			LastUpdate = DateTime.UtcNow;
			if (EnableUpdates)
			{
				UpdateProfileTimers(m_profile);
			}
		}
	}

	public void ForceException(int _errorCategory, int _errorCode, int _count, bool _forceErrorDuringSync)
	{
		if (ForceErrorCategory != _errorCategory || ForceErrorCode != _errorCode || ForceErrorCount != _count || _forceErrorDuringSync != ForceErrorDuringSync)
		{
			ForceErrorCategory = ((_errorCategory >= 0) ? _errorCategory : 0);
			ForceErrorCode = _errorCode;
			ForceErrorCount = _count;
			ForceErrorDuringSync = _forceErrorDuringSync;
			if (_count > 0)
			{
			}
			EventDelegate.Execute(onForceExceptionChange);
		}
	}
}
