using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using AcierProtocol;
using Assets.Scripts.Native;
using Assets.Scripts.Network.Error;
using Assets.Scripts.Network.Model;
using Assets.Scripts.Network.Session;
using Assets.Scripts.Network.Shop;
using Assets.Scripts.Tracking;
using Assets.Scripts.UI.State;
using BB.Mobile.Net;
using BB.Mobile.Net.Http;
using InControl;
using MiniJSON;
using UnityEngine;

namespace Assets.Scripts.Network;

public class NetworkDataStore : SharedNetworkDataStore, INetworkDataStore, IShopDataStore, INativeShopMessageHandler
{
	private const string SERVER_ADDR_PROTOBUF = "http://10.26.5.76:8330/rest-1.0-SNAPSHOT-ha-group-trunk/acier/v1/api";

	private const int c_shopResultSuccessful = 1;

	private const int c_shopStatePurchase = 4;

	private const int c_shopStateRestore = 5;

	private const int c_shopRestoreResponse = 6;

	public static readonly string s_NetworkCacheDir = Main.LoadSavedDataPath() + "/network/";

	private static readonly string m_SessionKeyName = "session.token";

	private static readonly string m_CredentialsKeyName = "credentials";

	private Credentials m_credentials;

	private readonly Action<Exception> m_versionCheckErrorCallback;

	private EShopPortal m_ShopPortal;

	private ProtobufReport m_pendingReport;

	private IShop m_ShopBase;

	private int requestTxId;

	private bool m_FirstAuth = true;

	private Dictionary<int, IAsyncDataResult<string>> queue = new Dictionary<int, IAsyncDataResult<string>>();

	public event Action<bool> RestoreShopResponseCallback;

	public NetworkDataStore(string _serverAddress, bool _checkVersion = false, Action<Exception> _versionCheckErrorCallback = null)
		: base(_serverAddress)
	{
		m_ShopPortal = EShopPortal.Google;
		if (Application.platform == RuntimePlatform.Android)
		{
			m_ShopBase = new ShopAndroid();
		}
		if (_checkVersion)
		{
			m_versionCheckErrorCallback = _versionCheckErrorCallback;
			CheckVersion(OnCheckVersion);
		}
	}

	public ESessionState State()
	{
		return m_Sentinel.State;
	}

	private void OnCheckVersion(IAsyncResult _result)
	{
		CheckVersionResultNeedUpdate(_result);
	}

	private void CheckVersionResultNeedUpdate(IAsyncResult _result)
	{
		try
		{
			IAsyncDataResult<CheckVersionResponse> asyncDataResult = (IAsyncDataResult<CheckVersionResponse>)_result;
			CheckVersionResponse checkVersionResponse = asyncDataResult.Result();
			if (checkVersionResponse.needClientUpdate)
			{
				string text = ((checkVersionResponse.serverVersion <= 59) ? "Please ask the backend developers to update the server pointing to http://10.26.5.76:8330/rest-1.0-SNAPSHOT-ha-group-trunk/acier/v1/api!" : "Please update your client to the most recent version!");
				string message = "Server-Version: " + checkVersionResponse.serverVersion + "\nClient-Version: " + 59 + "\n" + text;
				BBAlertViewMgr.ShowAlertView("Network Protocol Version mismatch!", message, _showAlert: true);
				throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.NetworkProtocolVersionMismatch, ErrorCategory.AcierStartupErrors), new Exception(message));
			}
		}
		catch (Exception obj)
		{
			if (m_versionCheckErrorCallback != null)
			{
				m_versionCheckErrorCallback(obj);
			}
			throw;
		}
	}

	public IAsyncDataResult<FBStatusResponse> QueryFBStatus(string _accessToken, ESocialNetwork _socialNetwork, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.fbStatusRequest = new FBStatusRequest();
		request.fbStatusRequest.accessToken = _accessToken;
		request.fbStatusRequest.socialNetwork = _socialNetwork;
		return SendGenericRequest(request, (Response response) => response.fbStatusResponse);
	}

	public IAsyncDataResult<FBStatusByIdResponse> QueryFBStatusById(string _facebookId, ESocialNetwork _socialNetwork, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.fbStatusByIsRequest = new FBStatusByIdRequest();
		request.fbStatusByIsRequest.facebookId = _facebookId;
		request.fbStatusByIsRequest.socialNetwork = _socialNetwork;
		return SendGenericRequest(request, (Response response) => response.fbStatusByIdResponse);
	}

	public IAsyncDataResult<VerifyAppResponse> VerifyApp(string _appReceipt, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.verifyAppRequest = new VerifyAppRequest
		{
			appReceipt = _appReceipt
		};
		return SendGenericRequest(request, (Response response) => response.verifyAppResponse);
	}

	public IAsyncDataResult<CheatResponse> UserProfileCheat(CheatRequest.CheatType _cheatType, string _assassinId = "", CheatParameters _params = null)
	{
		Request request = CreateRequest();
		request.cheatRequest = new CheatRequest();
		request.cheatRequest.type = _cheatType;
		request.cheatRequest.param1 = _params?.Param1 ?? 0;
		request.cheatRequest.param2 = _params?.Param2 ?? 0;
		request.cheatRequest.param3 = _params?.Param3;
		request.cheatRequest.param4 = _params?.Param4;
		request.cheatRequest.param5 = _params?.Param5;
		request.cheatRequest.assassinId = _assassinId;
		return SendGenericRequest(request, (Response response) => response.cheatResponse);
	}

	public IAsyncDataResult<CheatResponse> GetMissionByName(string _name, bool _isElite)
	{
		Request request = CreateRequest();
		request.cheatRequest = new CheatRequest
		{
			type = CheatRequest.CheatType.GET_MISSION,
			param3 = _name,
			param1 = (_isElite ? 1 : 0)
		};
		return SendGenericRequest(request, (Response response) => response.cheatResponse);
	}

	public IAsyncDataResult<SimulateLootRollResponse> SimulateLootRoll(string _rollCode, bool _giveLoot, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.simulateLootRollRequest = new SimulateLootRollRequest();
		request.simulateLootRollRequest.rollCode = _rollCode;
		request.simulateLootRollRequest.giveLoot = _giveLoot;
		return SendGenericRequest(request, (Response response) => response.simulateLootRollResponse);
	}

	public bool AbandonUserProfile()
	{
		return NativeTools.DeleteStringFromKeyChain(m_CredentialsKeyName);
	}

	public IAsyncDataResult<SellItemResponse> SellItem(string _itemId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.sellItemRequest = new SellItemRequest();
		request.sellItemRequest.itemId = _itemId;
		return SendGenericRequest(request, (Response response) => response.sellItemResponse, _callback, _scheduler);
	}

	private Credentials LoadCredentials()
	{
		string stringFromKeyChain = NativeTools.GetStringFromKeyChain(m_CredentialsKeyName);
		if (stringFromKeyChain != null)
		{
			Credentials credentials = new Credentials();
			credentials.ReadExternal(stringFromKeyChain);
			if (!string.IsNullOrEmpty(credentials.Username) && !string.IsNullOrEmpty(credentials.Password))
			{
				return credentials;
			}
		}
		return null;
	}

	private Credentials GenereateCredentials()
	{
		Credentials credentials = new Credentials();
		byte[] array = new byte[256];
		RNGCryptoServiceProvider rNGCryptoServiceProvider = new RNGCryptoServiceProvider();
		rNGCryptoServiceProvider.GetBytes(array);
		credentials.Password = Convert.ToBase64String(array);
		credentials.Username = Guid.NewGuid().ToString();
		return credentials;
	}

	public void FirstLogin()
	{
		if (m_Sentinel.State == ESessionState.SessionStateCreated)
		{
			m_Sentinel.TryTransition(ESessionState.SessionStateOpening, ESessionState.SessionStateCreated);
			m_credentials = LoadCredentials();
			if (m_credentials == null)
			{
				m_credentials = GenereateCredentials();
				Register(Main.Instance.SignedData, Main.Instance.Signature, m_credentials, null, m_dispatcher);
			}
			else
			{
				Authenticate(Main.Instance.SignedData, Main.Instance.Signature, m_credentials);
			}
		}
	}

	private void Register(string _signedData, string _signature, Credentials _credentials, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<RegisterUserResponse> asyncDataResult = new AsyncDataResult<RegisterUserResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		try
		{
			m_pendingReport = TrackingManager.GetFullReport();
			Request request = CreateRequest();
			request.registerUserRequest = new RegisterUserRequest();
			request.registerUserRequest.username = _credentials.Username;
			request.registerUserRequest.password = _credentials.Password;
			request.registerUserRequest.ubiFederatedId = NativeTools.GetStringFromKeyChain("federated_id");
			request.registerUserRequest.ubiPushToken = NativeTools.GetStringFromKeyChain("ubimobi_access_token");
			request.registerUserRequest.trackingDeviceInfo = DeviceManager.CreateTrackingDeviceInfo();
			request.registerUserRequest.trackingAppInfo = DeviceManager.CreateTrackingAppInfo();
			request.registerUserRequest.signedData = _signedData;
			request.registerUserRequest.signature = _signature;
			if (m_pendingReport != null)
			{
				request.registerUserRequest.trackingEvents = m_pendingReport.Events;
			}
			AcierPayload requestPayload = new AcierPayload(request);
			BeginCreate(m_ServerPath, null, requestPayload, new AcierPayload(), OnRegistration, asyncDataResult, m_dispatcher);
		}
		catch (AcierErrorException exception)
		{
			asyncDataResult.Complete(null, null, exception, _synchronous: true);
		}
	}

	private void OnRegistration(IAsyncResult _asyncResult)
	{
		AsyncDataResult<RegisterUserResponse> asyncDataResult = (AsyncDataResult<RegisterUserResponse>)_asyncResult.AsyncState;
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			Response response = CheckError(((AcierPayload)httpResult.Payload).response);
			if (httpResult.Succeeded)
			{
				m_token = response.sessionToken;
				if (m_token != null)
				{
					Singleton<StateManager>.Instance.ProfileManager.ForceMerge(response.userProfile);
					RegisterUserResponse registerUserResponse = response.registerUserResponse;
					m_credentials.Username = registerUserResponse.identity;
					m_FirstAuth = false;
					m_Sentinel.TryTransition(ESessionState.SessionStateOpen, ESessionState.SessionStateOpening);
					asyncDataResult.Complete(null, null, null, _asyncResult.CompletedSynchronously);
					OnCredentials(_asyncResult);
				}
				if (m_pendingReport != null)
				{
					TrackingManager.Clear(m_pendingReport);
					m_pendingReport = null;
				}
			}
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnknownCode, ErrorCategory.UnknownCategory));
		}
		catch (AcierErrorException ex)
		{
			if (ex.Error.ErrorCode == ErrorCode.LICENSE_CHECK_FAILED_GO_TO_STORE)
			{
				m_Sentinel.TryTransition(ESessionState.SessionLcf, ESessionState.SessionStateOpening);
			}
			else
			{
				m_Sentinel.TryTransition(ESessionState.SessionStateClosedLoginFailed, ESessionState.SessionStateOpening);
			}
			asyncDataResult.Complete(null, null, ex, _asyncResult.CompletedSynchronously);
		}
	}

	private void OnCredentials(IAsyncResult _result)
	{
		if (m_Sentinel.State == ESessionState.SessionStateOpen)
		{
			NativeTools.SetStringInKeyChain(m_CredentialsKeyName, "{\"u\":\"" + m_credentials.Username + "\",\"p\":\"" + m_credentials.Password + "\"}");
		}
	}

	private void Authenticate(string _signedData, string _signature, Credentials _credentials, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<AuthAccountResponse> asyncDataResult = new AsyncDataResult<AuthAccountResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		try
		{
			m_pendingReport = TrackingManager.GetFullReport();
			Request request = CreateRequest();
			request.authAccountRequest = new AuthAccountRequest();
			request.authAccountRequest.username = _credentials.Username;
			request.authAccountRequest.password = _credentials.Password;
			request.authAccountRequest.ubiFederatedId = NativeTools.GetStringFromKeyChain("federated_id");
			request.authAccountRequest.ubiPushToken = NativeTools.GetStringFromKeyChain("ubimobi_access_token");
			request.authAccountRequest.trackingDeviceInfo = DeviceManager.CreateTrackingDeviceInfo();
			request.authAccountRequest.trackingAppInfo = DeviceManager.CreateTrackingAppInfo();
			request.authAccountRequest.firstAuth = m_FirstAuth;
			request.authAccountRequest.signedData = _signedData;
			request.authAccountRequest.signature = _signature;
			if (m_pendingReport != null)
			{
				request.authAccountRequest.trackingEvents = m_pendingReport.Events;
			}
			AcierPayload requestPayload = new AcierPayload(request);
			BeginCreate(m_ServerPath, null, requestPayload, new AcierPayload(), (IAsyncResult ar) =>
			{
				OnAuthenticate(ar, _callback);
			}, asyncDataResult, m_dispatcher);
		}
		catch (AcierErrorException exception)
		{
			asyncDataResult.Complete(null, null, exception, _synchronous: true);
		}
	}

	private void OnAuthenticate(IAsyncResult _asyncResult, AsyncCallback _callback = null)
	{
		AsyncDataResult<AuthAccountResponse> asyncDataResult = (AsyncDataResult<AuthAccountResponse>)_asyncResult.AsyncState;
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, m_dispatcher));
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			Response response = CheckError(((AcierPayload)httpResult.Payload).response);
			if (httpResult.Succeeded)
			{
				m_token = response.sessionToken;
				if (m_token != null)
				{
					m_FirstAuth = false;
					Singleton<StateManager>.Instance.ProfileManager.ForceMerge(response.userProfile);
					AuthAccountResponse authAccountResponse = response.authAccountResponse;
					m_Sentinel.TryTransition(ESessionState.SessionStateOpen, ESessionState.SessionStateOpening);
					asyncDataResult.Complete(null, null, null, _asyncResult.CompletedSynchronously);
					if (m_credentials.Username != authAccountResponse.identity)
					{
						m_credentials.Username = authAccountResponse.identity;
						OnCredentials(_asyncResult);
					}
				}
				if (m_pendingReport != null)
				{
					TrackingManager.Clear(m_pendingReport);
					m_pendingReport = null;
				}
				return;
			}
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnknownCode, ErrorCategory.UnknownCategory));
		}
		catch (AcierErrorException ex)
		{
			if (ex.Error.ErrorCode == ErrorCode.LICENSE_CHECK_FAILED_GO_TO_STORE)
			{
				m_Sentinel.TryTransition(ESessionState.SessionLcf, ESessionState.SessionStateOpening);
			}
			else
			{
				m_Sentinel.TryTransition(ESessionState.SessionStateClosedLoginFailed, ESessionState.SessionStateOpening);
			}
			asyncDataResult.Complete(null, null, ex, _asyncResult.CompletedSynchronously);
		}
	}

	public IAsyncDataResult<DiscoveryServiceResponse> GetDiscoveryService(EServerEnvironment _serverEnvironment, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.discoveryServiceRequest = new DiscoveryServiceRequest();
		request.discoveryServiceRequest.deviceInfoTrack = DeviceManager.CreateTrackingDeviceInfo();
		request.discoveryServiceRequest.environment = _serverEnvironment;
		request.discoveryServiceRequest.appInfoTrack = DeviceManager.CreateTrackingAppInfo();
		Debug.Log("Main.Instance.BuildDefinition.BuildName：" + Main.Instance.BuildDefinition.BuildName);
		return SendGenericRequest(request, (Response response) => response.discoveryServiceResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetQualitySettingsResponse> GetQualitySettings(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getQualitySettingsRequest = new GetQualitySettingsRequest();
		request.getQualitySettingsRequest.deviceInfoTrack = DeviceManager.CreateTrackingDeviceInfo();
		request.getQualitySettingsRequest.appInfoTrack = DeviceManager.CreateTrackingAppInfo();
		request.getQualitySettingsRequest.deviceIdentifier = QualitySettingsHelper.GetDeviceIdentifier();
		return SendGenericRequest(request, (Response response) => response.getQualitySettingsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetTwitterTimelineResponse> GetTwitterTimeline(ELanguages _language, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getTwitterTimelineRequest = new GetTwitterTimelineRequest();
		request.getTwitterTimelineRequest.language = _language;
		return SendGenericRequest(request, (Response response) => response.getTwitterTimelineResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetUserProfileResponse> GetPlayerProfile(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getUserProfileRequest = new GetUserProfileRequest();
		request.getUserProfileRequest.needInventory = true;
		request.getUserProfileRequest.needAssassins = true;
		request.getUserProfileRequest.needResources = true;
		request.getUserProfileRequest.needMissions = true;
		request.getUserProfileRequest.needCounter = true;
		request.getUserProfileRequest.needAchievements = true;
		request.getUserProfileRequest.needTutorial = true;
		request.getUserProfileRequest.needEntitlements = true;
		request.getUserProfileRequest.needUserSettings = true;
		request.getUserProfileRequest.needProgressionQuests = true;
		return SendGenericRequest(request, (Response response) => response.getUserProfileResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetUserProfileResponse> GetPlayerProfile(bool _needInventory, bool _needAssassins, bool _needResources, bool _needMissions, bool _needCounter, bool _needAchievements, bool _needTutorial, bool _needEntitlements, bool _needUserSettings, bool _needProgressionQuests, bool _needEventsProgress, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getUserProfileRequest = new GetUserProfileRequest();
		request.getUserProfileRequest.needInventory = _needInventory;
		request.getUserProfileRequest.needAssassins = _needAssassins;
		request.getUserProfileRequest.needResources = _needResources;
		request.getUserProfileRequest.needMissions = _needMissions;
		request.getUserProfileRequest.needCounter = _needCounter;
		request.getUserProfileRequest.needAchievements = _needAchievements;
		request.getUserProfileRequest.needTutorial = _needTutorial;
		request.getUserProfileRequest.needEntitlements = _needEntitlements;
		request.getUserProfileRequest.needUserSettings = _needUserSettings;
		request.getUserProfileRequest.needProgressionQuests = _needProgressionQuests;
		return SendGenericRequest(request, (Response response) => response.getUserProfileResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ModifyUserSettingsResponse> ModifyUserSettings(UserSettings _userSettings, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.modifyUserSettingsRequest = new ModifyUserSettingsRequest();
		request.modifyUserSettingsRequest.pushNotificationSettings = _userSettings.pushNotificationSettings;
		return SendGenericRequest(request, (Response response) => response.modifyUserSettingsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ModifyProfileNameResponse> ModifyPlayerProfileName(string _profileName, ESocialNetwork _socialNetwork, string _accessToken, EModuleOrigin _origin, bool _forceLinkToCurrent, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.modifyProfileNameRequest = new ModifyProfileNameRequest();
		request.modifyProfileNameRequest.username = _profileName;
		request.modifyProfileNameRequest.socialNetwork = _socialNetwork;
		request.modifyProfileNameRequest.fbAccessToken = _accessToken;
		request.modifyProfileNameRequest.origin = _origin;
		request.modifyProfileNameRequest.forceFacebookLinkToCurrent = _forceLinkToCurrent;
		return SendGenericRequest(request, (Response response) => response.modifyProfileNameResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetStaticsResponse> GetStatics(long _cachedStaticsVersion = -1, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getStaticsRequest = new GetStaticsRequest();
		request.getStaticsRequest.shopPortalId = m_ShopPortal;
		if (_cachedStaticsVersion >= 0)
		{
			request.getStaticsRequest.staticsVersion = _cachedStaticsVersion;
			m_clientStaticsVersion = _cachedStaticsVersion;
		}
		return SendGenericRequest(request, (Response response) => response.getStaticsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<BuyResourcesResponse> BuyResources(Dictionary<int, long> _resourceAmounts, EModuleOrigin _origin, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.buyResourcesRequest = new BuyResourcesRequest();
		foreach (int key in _resourceAmounts.Keys)
		{
			BuyResourcesRequest.Entry entry = new BuyResourcesRequest.Entry();
			entry.resType = (EResourceType)key;
			entry.amount = _resourceAmounts[key];
			request.buyResourcesRequest.entries = request.buyResourcesRequest.entries.Add(entry);
		}
		request.buyResourcesRequest.origin = _origin;
		return SendGenericRequest(request, (Response response) => response.buyResourcesResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<TrainAssassinResponse> TrainAssassin(string _assassinId, AssassinSkillInTraining.SkillClass _skillTierClass, int _tierId, AssassinSkillInTraining.SkillType _tierSkillType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.trainAssassinRequest = new TrainAssassinRequest();
		request.trainAssassinRequest.assassinId = _assassinId;
		request.trainAssassinRequest.skillClass = _skillTierClass;
		request.trainAssassinRequest.tierId = _tierId;
		request.trainAssassinRequest.skillType = _tierSkillType;
		return SendGenericRequest(request, (Response response) => response.trainAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<TrainAssassinDuringTutorialResponse> TrainAssassinDuringTutorial(string _assassinId, AssassinSkillInTraining.SkillClass _skillTierClass, int _tierId, AssassinSkillInTraining.SkillType _tierSkillType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.trainAssassinDuringTutorialRequest = new TrainAssassinDuringTutorialRequest();
		request.trainAssassinDuringTutorialRequest.assassinId = _assassinId;
		request.trainAssassinDuringTutorialRequest.skillClass = _skillTierClass;
		request.trainAssassinDuringTutorialRequest.tierId = _tierId;
		request.trainAssassinDuringTutorialRequest.skillType = _tierSkillType;
		return SendGenericRequest(request, (Response response) => response.trainAssassinDuringTutorialResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<UnlockPassiveSkillResponse> UnlockPassiveSkill(string _assassinId, int _tierId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.unlockPassiveSkillRequest = new UnlockPassiveSkillRequest();
		request.unlockPassiveSkillRequest.assassinId = _assassinId;
		request.unlockPassiveSkillRequest.tierId = _tierId;
		request.unlockPassiveSkillRequest.skillClass = _skillClass;
		return SendGenericRequest(request, (Response response) => response.unlockPassiveSkillResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<UnlockTierResponse> UnlockTier(string _assassinId, int _tierId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.unlockTierRequest = new UnlockTierRequest();
		request.unlockTierRequest.assassinId = _assassinId;
		request.unlockTierRequest.tierId = _tierId;
		request.unlockTierRequest.skillClass = _skillClass;
		return SendGenericRequest(request, (Response response) => response.unlockTierResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<EquipElementResponse> EquipElement(string _assassinId, string _elementId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.equipElementRequest = new EquipElementRequest();
		request.equipElementRequest.assassinId = _assassinId;
		request.equipElementRequest.elementId = _elementId;
		return SendGenericRequest(request, (Response response) => response.equipElementResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<UnequipElementResponse> UnequipElement(string _assassinId, string _elementId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.unequipElementRequest = new UnequipElementRequest();
		request.unequipElementRequest.assassinId = _assassinId;
		request.unequipElementRequest.elementId = _elementId;
		return SendGenericRequest(request, (Response response) => response.unequipElementResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ResetSkillTreeResponse> ResetSkillTree(string _assassinId, AssassinSkillInTraining.SkillClass _skillClass, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.resetSkillTreeRequest = new ResetSkillTreeRequest();
		request.resetSkillTreeRequest.assassinId = _assassinId;
		request.resetSkillTreeRequest.skillClass = _skillClass;
		return SendGenericRequest(request, (Response response) => response.resetSkillTreeResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SetExtendedClassTypeResponse> SetExtendedClass(string _assassinId, Assassin.EClassTypeId _classType, EResourceType _resourceType, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.setExtendedClassTypeRequest = new SetExtendedClassTypeRequest();
		request.setExtendedClassTypeRequest.assassinId = _assassinId;
		request.setExtendedClassTypeRequest.extendedClassType = _classType;
		request.setExtendedClassTypeRequest.costType = _resourceType;
		return SendGenericRequest(request, (Response response) => response.setExtendedClassTypeResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<UpdateSkillWheelResponse> UpdateSkillWheel(string _assassinId, SkillWheel _skillWheel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.updateSkillWheelRequest = new UpdateSkillWheelRequest();
		request.updateSkillWheelRequest.assassinId = _assassinId;
		request.updateSkillWheelRequest.skillWheel = _skillWheel;
		return SendGenericRequest(request, (Response response) => response.updateSkillWheelResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetMissionInfoResponse> GetMissionInfo(string _missionGuid, string _assassinId, string _hirelingId, string _campaignId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getMissionInfoRequest = new GetMissionInfoRequest();
		request.getMissionInfoRequest.assassinId = _assassinId;
		request.getMissionInfoRequest.hirelingId = _hirelingId;
		request.getMissionInfoRequest.missionId = _missionGuid;
		request.getMissionInfoRequest.campaignId = _campaignId;
		request.getMissionInfoRequest.rank = _rank;
		return SendGenericRequest(request, (Response response) => response.getMissionInfoResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<OpenMissionResponse> OpenMission(string _missionGuid, string _assassinId, string _hirelingId, string _campaignId, int _rank, bool _isRetry, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.openMissionRequest = new OpenMissionRequest();
		request.openMissionRequest.assassinId = _assassinId;
		request.openMissionRequest.hirelingId = _hirelingId;
		request.openMissionRequest.missionId = _missionGuid;
		request.openMissionRequest.campaignId = _campaignId;
		request.openMissionRequest.rank = _rank;
		request.openMissionRequest.retry = _isRetry;
		return SendGenericRequest(request, (Response response) => response.openMissionResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ShareMissionResponse> ShareMission(string _missionId, string _receiverId, string _missionFullName, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.shareMissionRequest = new ShareMissionRequest();
		request.shareMissionRequest.missionId = _missionId;
		request.shareMissionRequest.receiverId = _receiverId;
		request.shareMissionRequest.missionFullName = _missionFullName;
		return SendGenericRequest(request, (Response response) => response.shareMissionResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<RefreshMissionListResponse> RefreshMissionList(string _assassinId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.refreshMissionListRequest = new RefreshMissionListRequest();
		request.refreshMissionListRequest.assassinId = _assassinId;
		request.refreshMissionListRequest.rank = _rank;
		return SendGenericRequest(request, (Response response) => response.refreshMissionListResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ReviveResponse> Revive(string _missionToken, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.reviveRequest = new ReviveRequest();
		request.reviveRequest.missionToken = _missionToken;
		request.reviveRequest.paymentResource = _paymentResource;
		return SendGenericRequest(request, (Response response) => response.reviveResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<EndMissionResponse> EndMission(MissionResult _missionResult, string _ticket, bool _won, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.endMissionRequest = new EndMissionRequest();
		request.endMissionRequest.won = _won;
		request.endMissionRequest.missionToken = _ticket;
		request.endMissionRequest.missionResult = _missionResult;
		return SendGenericRequest(request, (Response response) => response.endMissionResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<RemoveHirelingAndMissionResponse> RemoveHirelingAndMission(string _campaignId, string _missionId, string _assassinId, int _rank, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.removeHirelingAndMissionRequest = new RemoveHirelingAndMissionRequest();
		request.removeHirelingAndMissionRequest.campaignId = _campaignId;
		request.removeHirelingAndMissionRequest.missionId = _missionId;
		request.removeHirelingAndMissionRequest.assassinId = _assassinId;
		request.removeHirelingAndMissionRequest.rankIdx = _rank;
		return SendGenericRequest(request, (Response response) => response.removeHirelingAndMissionResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<CreateAssassinResponse> CreateAssassin(Assassin.EClassTypeId _avatarType, string _headGuid, string _customName, int _firstNameLocaId, int _lastNameLocaID, int _numAssassinsBefore, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.createAssassinRequest = new CreateAssassinRequest();
		request.createAssassinRequest.classType = _avatarType;
		request.createAssassinRequest.headGuid = _headGuid;
		if (!string.IsNullOrEmpty(_customName))
		{
			request.createAssassinRequest.customName = _customName;
		}
		request.createAssassinRequest.firstNameLocaId = _firstNameLocaId;
		request.createAssassinRequest.lastNameLocaId = _lastNameLocaID;
		request.createAssassinRequest.numAssassins = _numAssassinsBefore;
		return SendGenericRequest(request, (Response response) => response.createAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<DeleteAssassinResponse> DeleteAssassin(string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.deleteAssassinRequest = new DeleteAssassinRequest();
		request.deleteAssassinRequest.assassinId = _assassinId;
		return SendGenericRequest(request, (Response response) => response.deleteAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetForeignAssassinResponse> GetForeignAssassinByAssassinId(string _userIdHash, string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getForeignAssassinRequest = new GetForeignAssassinRequest();
		request.getForeignAssassinRequest.userIdHash = _userIdHash;
		request.getForeignAssassinRequest.assassinId = _assassinId;
		request.getForeignAssassinRequest.type = EForeignAssassinType.SpecificAssassin;
		return SendGenericRequest(request, (Response response) => response.getForeignAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetForeignAssassinResponse> GetForeignAssassinByHirelingId(string _hirelingId, string _assassinId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getForeignAssassinRequest = new GetForeignAssassinRequest();
		request.getForeignAssassinRequest.hirelingId = _hirelingId;
		request.getForeignAssassinRequest.assassinId = _assassinId;
		request.getForeignAssassinRequest.type = EForeignAssassinType.Hireling;
		return SendGenericRequest(request, (Response response) => response.getForeignAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetForeignAssassinResponse> GetForeignAssassinByUserId(string _userId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getForeignAssassinRequest = new GetForeignAssassinRequest();
		request.getForeignAssassinRequest.userId = _userId;
		request.getForeignAssassinRequest.type = EForeignAssassinType.StrongestAssassin;
		return SendGenericRequest(request, (Response response) => response.getForeignAssassinResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<PurchaseInGameItemResponse> PurchaseInGameShopItem(AcierProtocol.Resources _costs, string _shopItemId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.purchaseInGameItemRequest = new PurchaseInGameItemRequest();
		request.purchaseInGameItemRequest.itemId = _shopItemId;
		request.purchaseInGameItemRequest.costs = _costs;
		return SendGenericRequest(request, (Response response) => response.purchaseInGameItemResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<PurchaseAssassinSlotResponse> PurchaseAssassinSlot(int _numAlreadyPurchasedSlots, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.purchaseAssassinSlotRequest = new PurchaseAssassinSlotRequest();
		request.purchaseAssassinSlotRequest.alreadyPurchasedAssassinSlots = _numAlreadyPurchasedSlots;
		request.purchaseAssassinSlotRequest.paymentResource = _paymentResource;
		return SendGenericRequest(request, (Response response) => response.purchaseAssassinSlotResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<PurchaseInventorySlotResponse> PurchaseInventorySlot(int _numSlotPurchases, EResourceType _paymentResource, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.purchaseInventorySlotRequest = new PurchaseInventorySlotRequest();
		request.purchaseInventorySlotRequest.alreadyPurchasedInventorySlots = _numSlotPurchases;
		request.purchaseInventorySlotRequest.paymentResource = _paymentResource;
		return SendGenericRequest(request, (Response response) => response.purchaseInventorySlotResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<EarlyAccessClassResponse> EarlyAccessClass(Assassin.EClassTypeId _classType, EResourceType _currency, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.earlyAccessClassRequest = new EarlyAccessClassRequest();
		request.earlyAccessClassRequest.classType = _classType;
		return SendGenericRequest(request, (Response response) => response.earlyAccessClassResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetMailHeadersResponse> GetMailHeaders(long _lastTimeStamp, int _maxHeaders = 0, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getMailHeadersRequest = new GetMailHeadersRequest();
		request.getMailHeadersRequest.lastTimestamp = _lastTimeStamp;
		request.getMailHeadersRequest.maxHeaders = _maxHeaders;
		return SendGenericRequest(request, (Response response) => response.getMailHeadersResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetMailBodyResponse> GetMailBody(string _headerId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getMailBodyRequest = new GetMailBodyRequest();
		request.getMailBodyRequest.mailId = _headerId;
		return SendGenericRequest(request, (Response response) => response.getMailBodyResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ProcessMailResponse> ProcessMail(string _headerId, bool _accept, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.processMailRequest = new ProcessMailRequest();
		request.processMailRequest.mailId = _headerId;
		request.processMailRequest.accept = _accept;
		return SendGenericRequest(request, (Response response) => response.processMailResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SendMailResponse> SendMail(string _receiverId, string _subject, string _body, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.sendMailRequest = new SendMailRequest();
		request.sendMailRequest.receiverId = _receiverId;
		request.sendMailRequest.subject = _subject;
		request.sendMailRequest.body = _body;
		return SendGenericRequest(request, (Response response) => response.sendMailResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<DeleteMailsResponse> DeleteMails(List<string> _headerIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.deleteMailsRequest = new DeleteMailsRequest();
		request.deleteMailsRequest.mailIds = _headerIds.ToArray();
		return SendGenericRequest(request, (Response response) => response.deleteMailsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SendFriendRequestResponse> SendFriendRequest(string _userIdHash, EModuleOrigin _origin, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.sendFriendRequestRequest = new SendFriendRequestRequest();
		request.sendFriendRequestRequest.userIdHash = _userIdHash;
		request.sendFriendRequestRequest.origin = _origin;
		return SendGenericRequest(request, (Response response) => response.sendFriendRequestResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetMyFriendsResponse> GetMyFriends(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getMyFriendsRequest = new GetMyFriendsRequest();
		return SendGenericRequest(request, (Response response) => response.getMyFriendsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<RemoveFriendResponse> RemoveFriend(string _userId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.removeFriendRequest = new RemoveFriendRequest();
		request.removeFriendRequest.friendId = _userId;
		return SendGenericRequest(request, (Response response) => response.removeFriendResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ReadAchievementResponse> ReadAchievement(string _achievmentId, int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.readAchievementRequest = new ReadAchievementRequest();
		request.readAchievementRequest.achievementId = _achievmentId;
		request.readAchievementRequest.rankIdx = _rankIdx;
		return SendGenericRequest(request, (Response response) => response.readAchievementResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ClaimAchievementResponse> ClaimAchievement(string _achievmentId, int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.claimAchievementRequest = new ClaimAchievementRequest();
		request.claimAchievementRequest.achievementId = _achievmentId;
		request.claimAchievementRequest.rankIdx = _rankIdx;
		return SendGenericRequest(request, (Response response) => response.claimAchievementResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ReachNewAchievementsResponse> ReachNewAchievements(List<string> _achievementIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.reachNewAchievementsRequest = new ReachNewAchievementsRequest();
		request.reachNewAchievementsRequest.ids = _achievementIds.ToArray();
		return SendGenericRequest(request, (Response response) => response.reachNewAchievementsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ClaimQuestRewardResponse> ClaimQuestReward(int _rankIdx, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.claimQuestRewardRequest = new ClaimQuestRewardRequest();
		request.claimQuestRewardRequest.rankIdx = _rankIdx;
		return SendGenericRequest(request, (Response response) => response.claimQuestRewardResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ClaimWeeklyEventRewardResponse> ClaimWeeklyEventReward(string _challengeId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.claimWeeklyEventRewardRequest = new ClaimWeeklyEventRewardRequest();
		request.claimWeeklyEventRewardRequest.challengeId = _challengeId;
		return SendGenericRequest(request, (Response response) => response.claimWeeklyEventRewardResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetLeaderBoardsResponse> GetLeaderBoards(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getLeaderBoardsRequest = new GetLeaderBoardsRequest();
		return SendGenericRequest(request, (Response response) => response.getLeaderBoardsResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetResetProfileResponse> GetResetProfile(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getResetProfileRequest = new GetResetProfileRequest();
		return SendGenericRequest(request, (Response response) => response.getResetProfileResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SetTutorialStepResponse> SetTutorialStep(ETutorialCategory _category, int _counter, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.setTutorialStepRequest = new SetTutorialStepRequest();
		request.setTutorialStepRequest.category = _category;
		request.setTutorialStepRequest.counter = _counter;
		return SendGenericRequest(request, (Response response) => response.setTutorialStepResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SkipTutorialResponse> SkipTutorial(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.skipTutorialRequest = new SkipTutorialRequest();
		return SendGenericRequest(request, (Response response) => response.skipTutorialResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<SetTutorialFinishedResponse> SetTutorialFinished(ETutorialCategory _category, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.setTutorialFinishedRequest = new SetTutorialFinishedRequest();
		request.setTutorialFinishedRequest.category = _category;
		return SendGenericRequest(request, (Response response) => response.setTutorialFinishedResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<OpenMysteryBoxResponse> OpenMysteryBox(Quality.EQuality _quality, EMysteryBoxType _mysteryBoxType, string _assassinId, string _itemId, bool _startChain, bool _overspending, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.openMysteryBoxRequest = new OpenMysteryBoxRequest();
		request.openMysteryBoxRequest.quality = _quality;
		request.openMysteryBoxRequest.mysteryBoxType = _mysteryBoxType;
		request.openMysteryBoxRequest.itemId = _itemId;
		request.openMysteryBoxRequest.startChain = _startChain;
		request.openMysteryBoxRequest.overspending = _overspending;
		request.openMysteryBoxRequest.assassinId = _assassinId;
		return SendGenericRequest(request, (Response response) => response.openMysteryBoxResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ClaimMysteryBoxResponse> ClaimMysteryBox(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.claimMysteryBoxRequest = new ClaimMysteryBoxRequest();
		return SendGenericRequest(request, (Response response) => response.claimMysteryBoxResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<GetMysteryBoxInfoResponse> GetMysteryBoxInfo(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.getMysteryBoxInfoRequest = new GetMysteryBoxInfoRequest();
		return SendGenericRequest(request, (Response response) => response.getMysteryBoxInfoResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ForgeResponse> Forge(List<string> _elementIds, Resource _expectedCost, int _expectedItemLevel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.forgeRequest = new ForgeRequest();
		request.forgeRequest.elementIds = _elementIds.ToArray();
		request.forgeRequest.expectedCost = _expectedCost;
		request.forgeRequest.expectedItemLevel = _expectedItemLevel;
		return SendGenericRequest(request, (Response response) => response.forgeResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ForgeRerollResponse> ForgeReroll(Resource _expectedCost, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.forgeRerollRequest = new ForgeRerollRequest();
		request.forgeRerollRequest.expectedCost = _expectedCost;
		return SendGenericRequest(request, (Response response) => response.forgeRerollResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ForgeDuringTutorialResponse> ForgeDuringTutorial(List<string> _elementIds, Resource _expectedCost, int _expectedItemLevel, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.forgeDuringTutorialRequest = new ForgeDuringTutorialRequest();
		request.forgeDuringTutorialRequest.elementIds = _elementIds.ToArray();
		request.forgeDuringTutorialRequest.expectedCost = _expectedCost;
		request.forgeDuringTutorialRequest.expectedItemLevel = _expectedItemLevel;
		return SendGenericRequest(request, (Response response) => response.forgeDuringTutorialResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ForgeRerollDuringTutorialResponse> ForgeRerollDuringTutorial(Resource _expectedCost, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.forgeRerollDuringTutorialRequest = new ForgeRerollDuringTutorialRequest();
		request.forgeRerollDuringTutorialRequest.expectedCost = _expectedCost;
		return SendGenericRequest(request, (Response response) => response.forgeRerollDuringTutorialResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<CollectAdRewardsResponse> CollectAdRewards(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.collectAdRewardsRequest = new CollectAdRewardsRequest();
		return SendGenericRequest(request, (Response response) => response.collectAdRewardsResponse, _callback, _scheduler);
	}

	public void SetGameObjectName(string _gameObjectName)
	{
		if (m_ShopBase != null)
		{
			m_ShopBase.SetNativeGameObjectName(_gameObjectName);
		}
	}

	public void RecoverPurchases()
	{
		if (m_ShopBase != null)
		{
			m_ShopBase.RecoverPurchases();
		}
	}

	public void SetApplicationUsername(string _applicationUsername)
	{
		if (m_ShopBase != null)
		{
			m_ShopBase.SetApplicationUsername(_applicationUsername);
		}
	}

	public void FinishTransaction(string _transactionId)
	{
		if (m_ShopBase != null)
		{
			m_ShopBase.RemoveTransactionFromQueue(_transactionId);
		}
	}

	public void RestoreCompletedTransactions()
	{
		if (m_ShopBase != null)
		{
			m_ShopBase.RestoreCompletedTransactions();
		}
	}

	public void OnNativeShopMessage(string _jsonMessage)
	{
		InputManager.ClearInputState();
		if (m_ShopBase == null)
		{
			return;
		}
		try
		{
			Dictionary<string, object> dictionary = Json.Deserialize(_jsonMessage) as Dictionary<string, object>;
			int num = int.Parse((string)dictionary["request-token"]);
			if (num != -1)
			{
				IAsyncDataResult<string> asyncDataResult = null;
				lock (queue)
				{
					if (queue.ContainsKey(num))
					{
						asyncDataResult = queue[num];
						asyncDataResult.Complete(null, _jsonMessage, null, _synchronous: false);
						queue.Remove(num);
					}
					return;
				}
			}
			int num2 = int.Parse((string)dictionary["current-state"]);
			int num3 = int.Parse((string)dictionary["status"]);
			string json = (string)dictionary["message"];
			if (num2 == 6 && RestoreShopResponseCallback != null)
			{
				RestoreShopResponseCallback(num3 == 1);
			}
			if (num3 != 1 || (num2 != 4 && num2 != 5))
			{
				return;
			}
			string empty = string.Empty;
			string empty2 = string.Empty;
			Dictionary<string, object> dictionary2 = Json.Deserialize(json) as Dictionary<string, object>;
			if (dictionary2.ContainsKey("receipt-data") && dictionary2.ContainsKey("transactionId"))
			{
				string transactionId = dictionary2["transactionId"] as string;
				string originalTransactionId = string.Empty;
				if (dictionary2.ContainsKey("originalTransactionId"))
				{
					originalTransactionId = dictionary2["originalTransactionId"] as string;
				}
				string receiptData = dictionary2["receipt-data"] as string;
				string formattedPrice = dictionary2["formattedPrice"] as string;
				string productId = dictionary2["productId"] as string;
				empty = dictionary2["rawPrice"] as string;
				empty2 = dictionary2["currencyCode"] as string;
				SendReceipt(productId, transactionId, originalTransactionId, receiptData, empty2, empty, formattedPrice);
			}
			else if (dictionary2.ContainsKey("purchaseData") && dictionary2.ContainsKey("dataSignature"))
			{
				string productId2 = dictionary2["productId"] as string;
				string receiptData2 = dictionary2["purchaseData"] as string;
				string receiptSignature = dictionary2["dataSignature"] as string;
				string formattedPrice2 = dictionary2["formattedPrice"] as string;
				empty = dictionary2["rawPrice"] as string;
				empty2 = dictionary2["currencyCode"] as string;
				SendGoogleReceipt(productId2, receiptSignature, receiptData2, empty2, empty, formattedPrice2);
			}
		}
		catch (Exception)
		{
		}
	}

	public void SendReceipt(string _productId, string _transactionId, string _originalTransactionId, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Singleton<StateManager>.Instance.ProfileManager.VerifyReceipt(_productId, _transactionId, _originalTransactionId, _receiptData, _currencyCode, _cost, _formattedPrice, _callback, _scheduler);
	}

	public void SendGoogleReceipt(string _productId, string _receiptSignature, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Singleton<StateManager>.Instance.ProfileManager.VerifyGoogleReceipt(_productId, _receiptSignature, _receiptData, _currencyCode, _cost, _formattedPrice, _callback, _scheduler);
	}

	public IAsyncDataResult<VerifyReceiptResponse> VerifyReceipt(string _transactionId, string _originalTransactionId, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.verifyReceiptRequest = new VerifyReceiptRequest();
		request.verifyReceiptRequest.receiptData = _receiptData;
		request.verifyReceiptRequest.transactionId = _transactionId;
		request.verifyReceiptRequest.originalTransactionId = _originalTransactionId;
		request.verifyReceiptRequest.currencyCode = _currencyCode;
		request.verifyReceiptRequest.costDecimal = _cost;
		request.verifyReceiptRequest.formattedPrice = _formattedPrice;
		request.verifyReceiptRequest.deviceId = NativeTools.GetUserIdMd5Sum();
		if (!string.IsNullOrEmpty(DeviceManager.StoreCountry))
		{
			request.verifyReceiptRequest.storeCountry = DeviceManager.StoreCountry;
		}
		return SendGenericRequest(request, (Response response) => response.verifyReceiptResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<VerifyGoogleReceiptResponse> VerifyGoogleReceipt(string _receiptSignature, string _receiptData, string _currencyCode, string _cost, string _formattedPrice, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		Request request = CreateRequest();
		request.verifyGoogleReceiptRequest = new VerifyGoogleReceiptRequest();
		request.verifyGoogleReceiptRequest.inAppPurchaseData = _receiptData;
		request.verifyGoogleReceiptRequest.inAppPurchaseSignature = _receiptSignature;
		request.verifyGoogleReceiptRequest.currencyCode = _currencyCode;
		request.verifyGoogleReceiptRequest.costDecimal = _cost;
		request.verifyGoogleReceiptRequest.formattedPrice = _formattedPrice;
		request.verifyGoogleReceiptRequest.deviceId = NativeTools.GetUserIdMd5Sum();
		if (!string.IsNullOrEmpty(DeviceManager.StoreCountry))
		{
			request.verifyGoogleReceiptRequest.storeCountry = DeviceManager.StoreCountry;
		}
		return SendGenericRequest(request, (Response response) => response.verifyGoogleReceiptResponse, _callback, _scheduler);
	}

	public IAsyncDataResult<ProductPricings> GetProductPricings(string[] _productIds, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<ProductPricings> asyncDataResult = new AsyncDataResult<ProductPricings>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		if (m_ShopBase != null)
		{
			int num = Interlocked.Increment(ref requestTxId);
			AsyncDataResult<string> asyncDataResult2 = new AsyncDataResult<string>(asyncDataResult);
			asyncDataResult2.AddScheduledCallback(new ScheduledCallback(OnProductPricingsResult, m_dispatcher));
			lock (queue)
			{
				queue.Add(num, asyncDataResult2);
				m_ShopBase.RequestProductFromStore(num, _productIds, _productIds.Length);
			}
		}
		else
		{
			ProductPricings productPricings = new ProductPricings();
			ProductPricing productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.htpack01";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "69,99$";
			productPricing.productId = "com.ubisoft.acier.htpack02";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.bluebyte.acier.HCPack01";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "9,99$";
			productPricing.productId = "com.bluebyte.acier.HCPack02";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "24,99$";
			productPricing.productId = "com.bluebyte.acier.HCPack03";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "49,99$";
			productPricing.productId = "com.bluebyte.acier.HCPack04";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "89,99$";
			productPricing.productId = "com.bluebyte.acier.HCPack05";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "1,99$";
			productPricing.productId = "com.ubisoft.acier.campaign02forlinonconsumable";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier01";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier02";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier03";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier04";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier05";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier06";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.outfittier07";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "1,99$";
			productPricing.productId = "com.ubisoft.acier.starterpack01berserker";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.heroicpack01berserker";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "1,99$";
			productPricing.productId = "com.ubisoft.acier.starterpack02shadowblade";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.heroicpack02shadowblade";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "1,99$";
			productPricing.productId = "com.ubisoft.acier.starterpack03trickster";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.heroicpack03trickster";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "1,99$";
			productPricing.productId = "com.ubisoft.acier.starterpack04thief";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "4,99$";
			productPricing.productId = "com.ubisoft.acier.heroicpack04thief";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack01aguilar";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack02altair";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack03duelist";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack04kenway";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack05ezio";
			productPricings.prices = productPricings.prices.Add(productPricing);
			productPricing = new ProductPricing();
			productPricing.productPrice = "19,99$";
			productPricing.productId = "com.ubisoft.acier.legacypack06revelations";
			productPricings.prices = productPricings.prices.Add(productPricing);
			asyncDataResult.Complete(null, productPricings, null, _synchronous: false);
		}
		return asyncDataResult;
	}

	protected void OnProductPricingsResult(IAsyncResult _asyncResult)
	{
		AsyncDataResult<string> asyncDataResult = _asyncResult as AsyncDataResult<string>;
		AsyncDataResult<ProductPricings> asyncDataResult2 = _asyncResult.AsyncState as AsyncDataResult<ProductPricings>;
		string text = null;
		try
		{
			if (asyncDataResult != null)
			{
				text = asyncDataResult.Result();
				Dictionary<string, object> dictionary = Json.Deserialize(text) as Dictionary<string, object>;
				int num = int.Parse((string)dictionary["current-state"]);
				int num2 = int.Parse((string)dictionary["status"]);
				string text2 = (string)dictionary["message"];
				if (num2 != 1)
				{
					if (asyncDataResult2 != null)
					{
						int num3;
						try
						{
							num3 = int.Parse(text2);
						}
						catch (Exception)
						{
							num3 = -1;
						}
						ErrorCategory errorCategory = ErrorCategory.CLIENT_HC_SHOP;
						ErrorCode errorCode;
						switch (num3)
						{
						case 202:
							errorCode = ErrorCode.HC_SHOP_INVALID_PARAMATERS;
							break;
						case 204:
							errorCode = ErrorCode.HC_SHOP_ITUNES_CONNECTION_FAILED;
							break;
						case 501:
							errorCode = ErrorCode.HC_SHOP_BINDING_FAILED;
							break;
						case 3:
						case 502:
							errorCode = ErrorCode.HC_SHOP_BILLING_UNAVAILABLE;
							break;
						case 503:
							errorCode = ErrorCode.HC_SHOP_PRODUCTS_REQUEST_FAILED;
							break;
						case 504:
							errorCode = ErrorCode.HC_SHOP_PRODUCTS_REQUEST_EMPTY;
							break;
						default:
							errorCode = ErrorCode.HC_SHOP_ERROR;
							break;
						}
						asyncDataResult2.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(errorCode, errorCategory)), _synchronous: false);
					}
					return;
				}
				List<object> list = Json.Deserialize(text2) as List<object>;
				ProductPricings productPricings = new ProductPricings();
				foreach (Dictionary<string, object> item in list)
				{
					string value = string.Empty;
					string value2 = string.Empty;
					if (item.TryGetValue<string>("m_id", out value) && item.TryGetValue<string>("m_price", out value2))
					{
						ProductPricing productPricing = new ProductPricing();
						productPricing.productId = value;
						productPricing.productPrice = value2;
						productPricings.prices = productPricings.prices.Add(productPricing);
					}
				}
				asyncDataResult2?.Complete(null, productPricings, null, _synchronous: false);
			}
			else
			{
				asyncDataResult2?.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.HC_SHOP_ERROR, ErrorCategory.CLIENT_HC_SHOP)), _synchronous: false);
			}
		}
		catch (Exception)
		{
			asyncDataResult2?.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.HC_SHOP_ERROR, ErrorCategory.CLIENT_HC_SHOP)), _synchronous: false);
		}
	}

	public IAsyncDataResult<VerifyReceiptResponse> BuyProduct(string _productId, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<VerifyReceiptResponse> asyncDataResult = new AsyncDataResult<VerifyReceiptResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		if (m_ShopBase != null)
		{
			int num = Interlocked.Increment(ref requestTxId);
			AsyncDataResult<string> asyncDataResult2 = new AsyncDataResult<string>(asyncDataResult);
			asyncDataResult2.AddScheduledCallback(new ScheduledCallback(OnBuyProductResult, m_dispatcher));
			lock (queue)
			{
				queue.Add(num, asyncDataResult2);
				m_ShopBase.BuyProduct(num, _productId);
			}
		}
		else
		{
			asyncDataResult.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.HC_SHOP_NOT_SUPPORTED_ON_THIS_PLATFORM, ErrorCategory.CLIENT_HC_SHOP)), _synchronous: true);
		}
		return asyncDataResult;
	}

	protected void OnBuyProductResult(IAsyncResult _asyncResult)
	{
		AsyncDataResult<string> asyncDataResult = _asyncResult as AsyncDataResult<string>;
		AsyncDataResult<VerifyReceiptResponse> asyncDataResult2 = _asyncResult.AsyncState as AsyncDataResult<VerifyReceiptResponse>;
		string text = null;
		try
		{
			if (asyncDataResult != null)
			{
				text = asyncDataResult.Result();
				Dictionary<string, object> dictionary = Json.Deserialize(text) as Dictionary<string, object>;
				int num = int.Parse((string)dictionary["current-state"]);
				int num2 = int.Parse((string)dictionary["status"]);
				string s = (string)dictionary["message"];
				if (num2 != 1)
				{
					if (asyncDataResult2 != null)
					{
						int num3;
						try
						{
							num3 = int.Parse(s);
						}
						catch (Exception)
						{
							num3 = -1;
						}
						ErrorCategory errorCategory = ErrorCategory.CLIENT_HC_SHOP;
						ErrorCode errorCode;
						switch (num3)
						{
						case 1:
							errorCode = ErrorCode.HC_SHOP_USER_CANCELLED;
							break;
						case 2:
							errorCode = ErrorCode.HC_SHOP_SERVICE_UNAVAILABLE;
							break;
						case 3:
						case 502:
							errorCode = ErrorCode.HC_SHOP_BILLING_UNAVAILABLE;
							break;
						case 8:
							errorCode = ErrorCode.HC_SHOP_ITEM_NOT_OWNED;
							break;
						case 500:
							errorCode = ErrorCode.HC_SHOP_ACTIVITY_NOT_FOUND;
							break;
						case 501:
							errorCode = ErrorCode.HC_SHOP_BINDING_FAILED;
							break;
						case 503:
							errorCode = ErrorCode.HC_SHOP_PRODUCTS_REQUEST_FAILED;
							break;
						case 504:
							errorCode = ErrorCode.HC_SHOP_PRODUCTS_REQUEST_EMPTY;
							break;
						case 505:
							errorCode = ErrorCode.HC_SHOP_PURCHASE_FAILED;
							break;
						case 506:
							errorCode = ErrorCode.HC_SHOP_PURCHASE_CANCELLED;
							break;
						case 507:
							errorCode = ErrorCode.HC_SHOP_CONSUME_FAILED;
							break;
						case 508:
							errorCode = ErrorCode.HC_SHOP_RECOVER_FAILED;
							break;
						case 300:
							errorCode = ErrorCode.HC_SHOP_IAP_DISABLED;
							break;
						case 351:
							errorCode = ErrorCode.HC_SHOP_CLIENT_INVALID;
							break;
						case 352:
							errorCode = ErrorCode.HC_SHOP_PAYMENT_CANCELLED;
							break;
						case 353:
							errorCode = ErrorCode.HC_SHOP_PAYMENT_INVALID;
							break;
						case 354:
							errorCode = ErrorCode.HC_SHOP_PAYMENT_NOT_ALLOWED;
							break;
						case 4:
						case 355:
							errorCode = ErrorCode.HC_SHOP_PRODUCT_UNAVAILABLE;
							break;
						case 402:
							errorCode = ErrorCode.HC_SHOP_RECEIPT_ERROR;
							break;
						case 7:
							errorCode = ErrorCode.HC_SHOP_ITEM_ALREADY_OWNED;
							break;
						default:
							errorCode = ErrorCode.HC_SHOP_ERROR;
							break;
						}
						asyncDataResult2.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(errorCode, errorCategory)), _synchronous: false);
					}
				}
				else if (asyncDataResult2 != null)
				{
					VerifyReceiptResponse result = new VerifyReceiptResponse();
					asyncDataResult2.Complete(null, result, null, _synchronous: false);
				}
			}
			else
			{
				asyncDataResult2?.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.HC_SHOP_ERROR, ErrorCategory.CLIENT_HC_SHOP)), _synchronous: false);
			}
		}
		catch (Exception)
		{
			asyncDataResult2?.Complete(null, null, new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.HC_SHOP_ERROR, ErrorCategory.CLIENT_HC_SHOP)), _synchronous: false);
		}
	}

	public void ReAuthenticate(AsyncCallback _callback)
	{
		CheckVersion((IAsyncResult response) =>
		{
			if (((AsyncErrorResult)response).Exception != null)
			{
				_callback(response);
				return;
			}
			((AsyncErrorResult)response).AddScheduledCallback(new ScheduledCallback(_callback, m_dispatcher));
			try
			{
				CheckVersionResultNeedUpdate(response);
				Authenticate(Main.Instance.SignedData, Main.Instance.Signature, m_credentials, _callback);
			}
			catch (Exception exception)
			{
				((AsyncErrorResult)response).Complete(exception, _synchronous: true);
			}
		});
	}

	virtual void INetworkDataStore.UpdateStaticsVersion(long _version)
	{
		UpdateStaticsVersion(_version);
	}

	virtual void INetworkDataStore.Update()
	{
		Update();
	}

	virtual void IShopDataStore.Update()
	{
		Update();
	}
}
