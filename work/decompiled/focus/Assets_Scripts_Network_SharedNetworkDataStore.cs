using System;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Network.Error;
using Assets.Scripts.Network.Session;
using Assets.Scripts.UI.State;
using BB.Mobile.Net;
using BB.Mobile.Net.Http;
using ProtoBuf;

namespace Assets.Scripts.Network;

public class SharedNetworkDataStore
{
	protected delegate T ResponseDelegate<out T>(Response response);

	protected delegate T RequestDelegate<out T>(Response response);

	public static bool s_LogNetworkResults;

	protected readonly string m_ServerPath;

	protected string m_token;

	protected string m_userProfileHash;

	protected string m_requestToken = string.Empty;

	protected long m_clientStaticsVersion = -1L;

	protected EventTrigger m_eventTrigger;

	protected EnumSentinel<ESessionState> m_Sentinel = new EnumSentinel<ESessionState>(ESessionState.SessionStateCreated);

	protected HttpClient m_httpClient;

	protected AsyncCallbackDispatcher m_dispatcher = new AsyncCallbackDispatcher();

	public string UserProfileHash
	{
		get
		{
			return m_userProfileHash;
		}
		set
		{
			m_userProfileHash = value;
		}
	}

	public AsyncCallbackDispatcher Dispatcher => m_dispatcher;

	public string RequestToken => m_requestToken;

	public string SessionToken => m_token;

	public bool VersionChecked { get; protected set; }

	public SharedNetworkDataStore(string _serverPath, bool _useSignatureVerification = true)
	{
		m_httpClient = new HttpClient(null, null, _useSignatureVerification, 60000, 1);
		m_ServerPath = _serverPath;
		VersionChecked = false;
	}

	public void UpdateEventTrigger(EventTrigger _eventTrigger)
	{
		m_eventTrigger = _eventTrigger;
	}

	public void UpdateStaticsVersion(long _version)
	{
		m_clientStaticsVersion = _version;
	}

	protected void OnGenericRequestComplete<T>(IAsyncResult _asyncResult, ResponseDelegate<T> complete) where T : IExtensible
	{
		AsyncDataResult<T> asyncDataResult = (AsyncDataResult<T>)_asyncResult.AsyncState;
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			if (httpResult.Succeeded)
			{
				Response response = CheckError(((AcierPayload)httpResult.Payload).response);
				if (response.userProfileHash != null && response.userProfileHash.Length > 0)
				{
					m_userProfileHash = response.userProfileHash;
				}
				asyncDataResult.Complete(response, complete(response), null, _asyncResult.CompletedSynchronously);
				return;
			}
			throw new AcierErrorException(Assets.Scripts.Network.Error.Error.DEFAULT);
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, default, exception, _asyncResult.CompletedSynchronously);
		}
	}

	protected IAsyncDataResult<T> SendGenericRequest<T>(Request _request, ResponseDelegate<T> _responseDelegate, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null) where T : IExtensible
	{
		if (m_eventTrigger != null)
		{
			_request.eventTrigger = m_eventTrigger;
		}
		AsyncDataResult<T> asyncDataResult = new AsyncDataResult<T>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		try
		{
			if (m_Sentinel.State == ESessionState.SessionStateOpen)
			{
				_request.userProfileHash = m_userProfileHash;
				_request.requestToken = m_requestToken;
			}
			BeginCreate(m_ServerPath, null, new AcierPayload(_request), new AcierPayload(), (IAsyncResult asyncResult) =>
			{
				OnGenericRequestComplete(asyncResult, _responseDelegate);
			}, asyncDataResult, m_dispatcher);
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, default, exception, _synchronous: true);
		}
		return asyncDataResult;
	}

	protected Request CreateRequest()
	{
		Request request = new Request();
		if (m_token != null && m_token.Length > 0)
		{
			request.sessionToken = m_token;
		}
		if (m_requestToken != null && m_requestToken.Length > 0)
		{
			request.requestToken = m_requestToken;
		}
		if (m_clientStaticsVersion > -1)
		{
			request.staticsVersion = m_clientStaticsVersion;
		}
		return request;
	}

	public void Update()
	{
		m_dispatcher.Dispatch();
	}

	protected Response CheckError(Response response)
	{
		if (!string.IsNullOrEmpty(response.requestToken))
		{
			m_requestToken = response.requestToken;
		}
		if (response != null && response.eventTrigger != null)
		{
			if (m_eventTrigger == null || (response.eventTrigger != null && m_eventTrigger.version != response.eventTrigger.version))
			{
				m_eventTrigger = response.eventTrigger;
			}
			if (response.events != null)
			{
				Singleton<StateManager>.Instance.TemplateManager.Events = response.events;
				Singleton<StateManager>.Instance.EventManager.OnUpdateEvents();
			}
		}
		if (response.serverTime != 0L)
		{
			Singleton<StateManager>.Instance.ServerTimeManager.Time = response.serverTime;
		}
		if (response.error != null)
		{
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(response.error.code, response.error.category));
		}
		return response;
	}

	protected IAsyncDataResult<CheckVersionResponse> CheckVersion(AsyncCallback _callback = null)
	{
		VersionChecked = false;
		AsyncDataResult<CheckVersionResponse> asyncDataResult = new AsyncDataResult<CheckVersionResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, m_dispatcher));
		try
		{
			Request request = CreateRequest();
			request.checkVersionRequest = new CheckVersionRequest();
			request.checkVersionRequest.protocolVersion = 59u;
			BeginCreate(m_ServerPath, null, new AcierPayload(request), new AcierPayload(), OnCheckVersionComplete, asyncDataResult, m_dispatcher);
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _synchronous: true);
		}
		return asyncDataResult;
	}

	protected void OnCheckVersionComplete(IAsyncResult _asyncResult)
	{
		AsyncDataResult<CheckVersionResponse> asyncDataResult = (AsyncDataResult<CheckVersionResponse>)_asyncResult.AsyncState;
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			if (httpResult.Succeeded)
			{
				Response response = CheckError(((AcierPayload)httpResult.Payload).response);
				asyncDataResult.Complete(response, response.checkVersionResponse, null, _synchronous: false);
				VersionChecked = true;
				return;
			}
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnknownCode, ErrorCategory.UnknownCategory));
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _asyncResult.CompletedSynchronously);
			VersionChecked = true;
		}
	}

	protected HttpResult GetHttpResult(IAsyncResult _result)
	{
		try
		{
			return m_httpClient.EndRequest(_result);
		}
		catch (MobileNetException ex)
		{
			ErrorCode errorCode = ErrorCode.UnknownCode;
			switch (ex.Status)
			{
			case MobileNetException.EStatus.CacheEntryNotFound:
				errorCode = ErrorCode.WESCacheEntryNotFound;
				break;
			case MobileNetException.EStatus.ConnectFailure:
				errorCode = ErrorCode.WESConnectFailure;
				break;
			case MobileNetException.EStatus.ConnectionClosed:
				errorCode = ErrorCode.WESConnectionClosed;
				break;
			case MobileNetException.EStatus.KeepAliveFailure:
				errorCode = ErrorCode.WESKeepAliveFailure;
				break;
			case MobileNetException.EStatus.MessageLengthLimitExceeded:
				errorCode = ErrorCode.WESMessageLengthLimitExceeded;
				break;
			case MobileNetException.EStatus.NameResolutionFailure:
				errorCode = ErrorCode.WESNameResolutionFailure;
				break;
			case MobileNetException.EStatus.Pending:
				errorCode = ErrorCode.WESPending;
				break;
			case MobileNetException.EStatus.PipelineFailure:
				errorCode = ErrorCode.WESPipelineFailure;
				break;
			case MobileNetException.EStatus.ProtocolError:
				errorCode = ErrorCode.WESProtocolError;
				break;
			case MobileNetException.EStatus.ProxyNameResolutionFailure:
				errorCode = ErrorCode.WESProxyNameResolutionFailure;
				break;
			case MobileNetException.EStatus.ReceiveFailure:
				errorCode = ErrorCode.WESReceiveFailure;
				break;
			case MobileNetException.EStatus.RequestCanceled:
				errorCode = ErrorCode.WESRequestCanceled;
				break;
			case MobileNetException.EStatus.RequestProhibitedByCachePolicy:
				errorCode = ErrorCode.WESRequestProhibitedByCachePolicy;
				break;
			case MobileNetException.EStatus.RequestProhibitedByProxy:
				errorCode = ErrorCode.WESRequestProhibitedByProxy;
				break;
			case MobileNetException.EStatus.SecureChannelFailure:
				errorCode = ErrorCode.WESSecureChannelFailure;
				break;
			case MobileNetException.EStatus.SendFailure:
				errorCode = ErrorCode.WESSendFailure;
				break;
			case MobileNetException.EStatus.ServerProtocolViolation:
				errorCode = ErrorCode.WESServerProtocolViolation;
				break;
			case MobileNetException.EStatus.Timeout:
				errorCode = ErrorCode.WESTimeout;
				break;
			case MobileNetException.EStatus.TrustFailure:
				errorCode = ErrorCode.WESTrustFailure;
				break;
			case MobileNetException.EStatus.UnknownError:
				errorCode = ErrorCode.WESUnknownError;
				break;
			case MobileNetException.EStatus.SignatureVerificationFailure:
				errorCode = ErrorCode.WESSignatureVerificationFailure;
				break;
			}
			AcierErrorException ex2 = new AcierErrorException(new Assets.Scripts.Network.Error.Error(errorCode, ErrorCategory.WebStatusException), ex);
			throw ex2;
		}
		catch (Exception innerException)
		{
			AcierErrorException ex3 = new AcierErrorException(Assets.Scripts.Network.Error.Error.DEFAULT, innerException);
			throw ex3;
		}
	}

	protected void BeginRead(string _resource, IEnumerable<IHttpRequestModifier> _modifiers, IPayload _responsePayload, AsyncCallback _callback, object _state, IAsyncCallbackScheduler _scheduler)
	{
		AcierErrorException ex = null;
		try
		{
			m_httpClient.BeginReadRequest(new Uri(_resource), _modifiers, _responsePayload, _callback, _state, _scheduler);
			return;
		}
		catch (ArgumentOutOfRangeException innerException)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException);
		}
		catch (ArgumentNullException innerException2)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException2);
		}
		catch (UriFormatException innerException3)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException3);
		}
		catch (InvalidOperationException innerException4)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException4);
		}
		throw ex;
	}

	protected void Send(Request request, AsyncCallback _callback, object _state)
	{
		BeginCreate(m_ServerPath, null, new AcierPayload(request), new AcierPayload(), _callback, _state, m_dispatcher);
	}

	protected void BeginCreate(string _resource, IEnumerable<IHttpRequestModifier> _modifiers, IPayload _requestPayload, IPayload _responsePayload, AsyncCallback _callback, object _state, IAsyncCallbackScheduler _scheduler)
	{
		AcierErrorException ex = null;
		try
		{
			m_httpClient.BeginCreateRequest(new Uri(_resource), _modifiers, _requestPayload, _responsePayload, _callback, _state, _scheduler);
			return;
		}
		catch (ArgumentOutOfRangeException innerException)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException);
		}
		catch (ArgumentNullException innerException2)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException2);
		}
		catch (UriFormatException innerException3)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException3);
		}
		catch (InvalidOperationException innerException4)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException4);
		}
		throw ex;
	}

	protected void BeginUpdate(string _resource, IEnumerable<IHttpRequestModifier> _modifiers, IPayload _requestPayload, IPayload _responsePayload, AsyncCallback _callback, object _state, IAsyncCallbackScheduler _scheduler)
	{
		AcierErrorException ex = null;
		try
		{
			m_httpClient.BeginUpdateRequest(new Uri(_resource), _modifiers, _requestPayload, _responsePayload, _callback, _state, _scheduler);
			return;
		}
		catch (ArgumentOutOfRangeException innerException)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException);
		}
		catch (ArgumentNullException innerException2)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException2);
		}
		catch (UriFormatException innerException3)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException3);
		}
		catch (InvalidOperationException innerException4)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException4);
		}
		throw ex;
	}

	protected void BeginDelete(string _resource, IEnumerable<IHttpRequestModifier> _modifiers, IPayload _responsePayload, AsyncCallback _callback, object _state, IAsyncCallbackScheduler _scheduler)
	{
		AcierErrorException ex = null;
		try
		{
			m_httpClient.BeginDeleteRequest(new Uri(_resource), _modifiers, _responsePayload, _callback, _state, _scheduler);
			return;
		}
		catch (ArgumentOutOfRangeException innerException)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException);
		}
		catch (ArgumentNullException innerException2)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException2);
		}
		catch (UriFormatException innerException3)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException3);
		}
		catch (InvalidOperationException innerException4)
		{
			ex = new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.URIMisformated, ErrorCategory.GeneralCategory), innerException4);
		}
		throw ex;
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateLootMasterTable(LootMasterTable _lootMasterTable, int _changelist)
	{
		return UpdateStatics(_changelist, _lootMasterTable);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMissionBudgetMapping(AssassinLevelToMissionBudget _assassinLevelToMissionBudget, int _changelist)
	{
		return UpdateStatics(_changelist, null, _assassinLevelToMissionBudget);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMissionConfigurations(MissionConfigurations _missionConfigurations, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, _missionConfigurations);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateAvatarClassDefinitionsConfigurations(AvatarClassDefinitions _classDefinitions, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, _classDefinitions);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateItemSellMapping(ItemSellMapping _itemSellMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, _itemSellMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateXpPerLevelTable(XpPerLevelTable _xpPerLevelTable, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, _xpPerLevelTable);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMaxFriendsPerLevelTable(MaxFriendsMapping _maxFriendsPerLevelTable, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, _maxFriendsPerLevelTable);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateLocationUnlockMappings(LocationUnlockMappings _locationUnlockMappings, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, _locationUnlockMappings);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateGlobals(Globals _globals, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, _globals);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateLevelSliceToRewardMapping(LevelSliceToRewardMapping _levelSliceToRewardMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, _levelSliceToRewardMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateTimeToHardCurrencyMapping(TimeToHardCurrencyMapping _timeToHardCurrencyMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, _timeToHardCurrencyMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateShopTable(ShopTable _shopTable, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, _shopTable);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateStoryMissionRewardMapping(StoryMissionRewardMapping _storyMissionRewardMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, _storyMissionRewardMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateInventorySlotCostsMapping(InventorySlotCostsMapping _inventorySlotCostsMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, _inventorySlotCostsMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateAssassinSlotCostsMapping(AssassinSlotCostsMapping _assassinSlotCostsMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _assassinSlotCostsMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMissionUnlockMapping(MissionUnlockMapping _missionUnlockMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _missionUnlockMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateAchievementCategoryMapping(AchievementCategoryMapping _achievementCategoryMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _achievementCategoryMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateAchievementMapping(AchievementMapping _achievementMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _achievementMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMailTemplates(MailTemplates _mailTemplates, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _mailTemplates);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateStoryMissions(MissionList _missionList, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _missionList);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateGameDbMapping(GameDbMapping _gameDbMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _gameDbMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateResourceCostMapping(ResourceCostMapping _resourceCostMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _resourceCostMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateChallengeRewardMapping(ChallengeRewardMultiplierMapping _challengeRewardMultiplierMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _challengeRewardMultiplierMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateSkillTrainingTimeMapping(SkillTrainingTimeMapping _skillTrainingTimeMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _skillTrainingTimeMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateRequiredSkillPointsMapping(RequiredSkillPointsMapping _requiredSkillPointsMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _requiredSkillPointsMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateEventTemplates(EventTemplates _eventTemplates, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _eventTemplates);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdatePushNotificationTemplates(PushNotificationTemplates _pushNotificationTemplates, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _pushNotificationTemplates);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateForgeMapping(ForgeMapping _forgeMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _forgeMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMysteryBoxMapping(MysteryBoxMapping _mysteryBoxMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _mysteryBoxMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateEntitlementMapping(EntitlementMapping _entitlementMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _entitlementMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateCampaignCatalog(CampaignCatalog _campaignCatalog, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _campaignCatalog);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateHeroicRankMapping(HeroicRankMapping _heroicRankMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _heroicRankMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMissionLootScalingMapping(MissionLootScalingMapping _missionLootScalingMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _missionLootScalingMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateQuestGenerationMapping(QuestGenerationMapping _questGenerationMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _questGenerationMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMissionTypeLootMapping(MissionTypeLootMapping _missionTypeLootMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _missionTypeLootMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateTutorialCategoryMapping(TutorialCategoryMapping _tutorialCategoryMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _tutorialCategoryMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateQualitySettingsMapping(QualitySettingsMapping _qualitySettingsMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _qualitySettingsMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateOverflowMapping(OverflowMapping _overflowMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _overflowMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateMailboxMapping(MailBoxMapping _mailBoxMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _mailBoxMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateLevelSkillPointMapping(LevelSkillPointMapping _levelSkillPointMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _levelSkillPointMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateSecondSkillTreeUnlockCostsMapping(SecondSkillTreeCostMapping _secondSkillTreeCostMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _secondSkillTreeCostMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateEventsUserFriendlyMapping(EventsUserFriendlyMapping _eventsUserFriendlyMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _eventsUserFriendlyMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateCounterTypeLocaKeyMapping(CounterTypeLocaKeyMapping _counterTypeLocaKeyMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _counterTypeLocaKeyMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateExclusiveContentMapping(ExclusiveContentMapping _exclusiveContentMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _exclusiveContentMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateResourcePropertiesMapping(ResourcePropertiesMapping _resourcePropertiesMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _resourcePropertiesMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateAdColonyRewardMapping(AdColonyRewardMapping _adColonyRewardMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _adColonyRewardMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateGeneratedLeaderboardMapping(GeneratedLeaderBoardMapping _generatedLeaderBoardMapping, int _changelist)
	{
		return UpdateStatics(_changelist, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, _generatedLeaderBoardMapping);
	}

	public IAsyncDataResult<UpdateStaticsResponse> UpdateStatics(int _changelist, LootMasterTable _lootMasterTable = null, AssassinLevelToMissionBudget _assassinLevelToMissionBudget = null, MissionConfigurations _missionConfigurations = null, AvatarClassDefinitions _classDefinitions = null, ItemSellMapping _itemSellMapping = null, XpPerLevelTable _xpPerLevelTable = null, MaxFriendsMapping _maxFriendsTable = null, LocationUnlockMappings _localUnlockMappings = null, Globals _globals = null, LevelSliceToRewardMapping _levelSliceToRewardMapping = null, TimeToHardCurrencyMapping _timeToHardCurrencyMapping = null, ShopTable _shopTable = null, StoryMissionRewardMapping _storyMissionRewardMapping = null, InventorySlotCostsMapping _inventorySlotCostsMapping = null, AssassinSlotCostsMapping _assassinSlotCostsMapping = null, MissionUnlockMapping _missionUnlockMapping = null, AchievementCategoryMapping _achievementCategoryMapping = null, AchievementMapping _achievementMapping = null, MailTemplates _mailTemplates = null, MissionList _missionList = null, GameDbMapping _gameDbMapping = null, ResourceCostMapping _resourceCostMapping = null, ChallengeRewardMultiplierMapping _challengeRewardMultiplierMapping = null, SkillTrainingTimeMapping _skillTrainingTimeMapping = null, RequiredSkillPointsMapping _requiredSkillPointsMapping = null, EventTemplates _eventTemplates = null, PushNotificationTemplates _pushNotificationTemplates = null, ForgeMapping _forgeMapping = null, MysteryBoxMapping _mysteryBoxMapping = null, EntitlementMapping _entitlementMapping = null, CampaignCatalog _campaignCatalog = null, HeroicRankMapping _heroicRankMapping = null, MissionLootScalingMapping _missionLootScalingMapping = null, QuestGenerationMapping _questGenerationMapping = null, MissionTypeLootMapping _missionTypeLootMapping = null, TutorialCategoryMapping _tutorialCategoryMapping = null, QualitySettingsMapping _qualitySettingsMapping = null, OverflowMapping _overflowMapping = null, MailBoxMapping _mailBoxMapping = null, LevelSkillPointMapping _levelSkillPointMapping = null, SecondSkillTreeCostMapping _secondSkillTreeCostMapping = null, EventsUserFriendlyMapping _eventsUserFriendlyMapping = null, CounterTypeLocaKeyMapping _counterTypeLocaKeyMapping = null, ExclusiveContentMapping _exclusiveContentMapping = null, ResourcePropertiesMapping _resourcePropertiesMapping = null, AdColonyRewardMapping _adColonyRewardMapping = null, GeneratedLeaderBoardMapping _generatedLeaderBoardMapping = null, AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<UpdateStaticsResponse> asyncDataResult = new AsyncDataResult<UpdateStaticsResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		try
		{
			Request request = new Request();
			request.updateStaticsRequest = new UpdateStaticsRequest();
			request.updateStaticsRequest.articyPerforceChangeList = _changelist;
			request.updateStaticsRequest.lootMasterTable = _lootMasterTable;
			request.updateStaticsRequest.assassinLevelToMissionBudget = _assassinLevelToMissionBudget;
			request.updateStaticsRequest.avatarClassDefinitions = _classDefinitions;
			request.updateStaticsRequest.missionConfigurations = _missionConfigurations;
			request.updateStaticsRequest.itemSellMapping = _itemSellMapping;
			request.updateStaticsRequest.xpPerLevelTable = _xpPerLevelTable;
			request.updateStaticsRequest.maxFriendsMapping = _maxFriendsTable;
			request.updateStaticsRequest.locationUnlockMappings = _localUnlockMappings;
			request.updateStaticsRequest.globals = _globals;
			request.updateStaticsRequest.levelSliceToRewardMapping = _levelSliceToRewardMapping;
			request.updateStaticsRequest.timeToHardCurrencyMapping = _timeToHardCurrencyMapping;
			request.updateStaticsRequest.shopTable = _shopTable;
			request.updateStaticsRequest.storyMissionRewardMapping = _storyMissionRewardMapping;
			request.updateStaticsRequest.missionUnlockMapping = _missionUnlockMapping;
			request.updateStaticsRequest.achievementCategoryMapping = _achievementCategoryMapping;
			request.updateStaticsRequest.achievementMapping = _achievementMapping;
			request.updateStaticsRequest.mailTemplates = _mailTemplates;
			request.updateStaticsRequest.missionList = _missionList;
			request.updateStaticsRequest.gameDbMapping = _gameDbMapping;
			request.updateStaticsRequest.resourceCostMapping = _resourceCostMapping;
			request.updateStaticsRequest.challengeRewardMultiplierMapping = _challengeRewardMultiplierMapping;
			request.updateStaticsRequest.skillTrainingTimeMapping = _skillTrainingTimeMapping;
			request.updateStaticsRequest.requiredSkillPointsMapping = _requiredSkillPointsMapping;
			request.updateStaticsRequest.eventTemplates = _eventTemplates;
			request.updateStaticsRequest.pushNotificationTemplates = _pushNotificationTemplates;
			request.updateStaticsRequest.forgeMapping = _forgeMapping;
			request.updateStaticsRequest.mysteryBoxMapping = _mysteryBoxMapping;
			request.updateStaticsRequest.entitlementMapping = _entitlementMapping;
			request.updateStaticsRequest.campaignCatalog = _campaignCatalog;
			request.updateStaticsRequest.heroicRankMapping = _heroicRankMapping;
			request.updateStaticsRequest.missionLootScalingMapping = _missionLootScalingMapping;
			request.updateStaticsRequest.questGenerationMapping = _questGenerationMapping;
			request.updateStaticsRequest.missionTypeLootMapping = _missionTypeLootMapping;
			request.updateStaticsRequest.tutorialCategoryMapping = _tutorialCategoryMapping;
			request.updateStaticsRequest.qualitySettingsMapping = _qualitySettingsMapping;
			request.updateStaticsRequest.overflowMapping = _overflowMapping;
			request.updateStaticsRequest.mailBoxMapping = _mailBoxMapping;
			request.updateStaticsRequest.levelSkillPointMapping = _levelSkillPointMapping;
			request.updateStaticsRequest.secondSkillTreeCostMapping = _secondSkillTreeCostMapping;
			request.updateStaticsRequest.eventsUserFriendlyMapping = _eventsUserFriendlyMapping;
			request.updateStaticsRequest.counterTypeLocaKeyMapping = _counterTypeLocaKeyMapping;
			request.updateStaticsRequest.exclusiveContentMapping = _exclusiveContentMapping;
			request.updateStaticsRequest.resourcePropertiesMapping = _resourcePropertiesMapping;
			request.updateStaticsRequest.assassinSlotCostsMapping = _assassinSlotCostsMapping;
			request.updateStaticsRequest.inventorySlotCostsMapping = _inventorySlotCostsMapping;
			request.updateStaticsRequest.adColonyRewardMapping = _adColonyRewardMapping;
			request.updateStaticsRequest.generatedLeaderBoardMapping = _generatedLeaderBoardMapping;
			BeginCreate(m_ServerPath, null, new AcierPayload(request), new AcierPayload(), OnUpdateStaticsComplete, asyncDataResult, m_dispatcher);
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _synchronous: true);
		}
		return asyncDataResult;
	}

	protected void OnUpdateStaticsComplete(IAsyncResult _asyncResult)
	{
		AsyncDataResult<UpdateStaticsResponse> asyncDataResult = (AsyncDataResult<UpdateStaticsResponse>)_asyncResult.AsyncState;
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			if (httpResult.Succeeded)
			{
				Response response = CheckError(((AcierPayload)httpResult.Payload).response);
				asyncDataResult.Complete(response, response.updateStaticsResponse, null, _synchronous: false);
				return;
			}
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnknownCode, ErrorCategory.UnknownCategory));
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _asyncResult.CompletedSynchronously);
		}
	}

	public IAsyncDataResult<UpdateStaticsResponse> ClearStoryMissions(AsyncCallback _callback = null, IAsyncCallbackScheduler _scheduler = null)
	{
		AsyncDataResult<UpdateStaticsResponse> asyncDataResult = new AsyncDataResult<UpdateStaticsResponse>();
		asyncDataResult.AddScheduledCallback(new ScheduledCallback(_callback, _scheduler));
		try
		{
			Request request = new Request();
			request.clearStoryMissionsRequest = new ClearStoryMissionsRequest();
			BeginCreate(m_ServerPath, null, new AcierPayload(request), new AcierPayload(), OnClearStoryMissionsComplete, asyncDataResult, m_dispatcher);
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _synchronous: true);
		}
		return asyncDataResult;
	}

	private void OnClearStoryMissionsComplete(IAsyncResult _asyncResult)
	{
		AsyncDataResult<UpdateStaticsResponse> asyncDataResult = (AsyncDataResult<UpdateStaticsResponse>)_asyncResult.AsyncState;
		try
		{
			HttpResult httpResult = GetHttpResult(_asyncResult);
			if (httpResult.Succeeded)
			{
				Response response = CheckError(((AcierPayload)httpResult.Payload).response);
				asyncDataResult.Complete(response, response.updateStaticsResponse, null, _synchronous: false);
				return;
			}
			throw new AcierErrorException(new Assets.Scripts.Network.Error.Error(ErrorCode.UnknownCode, ErrorCategory.UnknownCategory));
		}
		catch (Exception exception)
		{
			asyncDataResult.Complete(null, null, exception, _asyncResult.CompletedSynchronously);
		}
	}
}
