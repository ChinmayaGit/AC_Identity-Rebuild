using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "Response")]
public class Response : IExtensible
{
	private string _sessionToken = "";

	private Error _error;

	private string _userProfileHash = "";

	private UserProfile _userProfile;

	private string _requestToken = "";

	private EventTrigger _eventTrigger;

	private Events _events;

	private long _serverTime;

	private RegisterUserResponse _registerUserResponse;

	private AuthAccountResponse _authAccountResponse;

	private GetUserProfileResponse _getUserProfileResponse;

	private ModifyProfileNameResponse _modifyProfileNameResponse;

	private FBStatusResponse _fbStatusResponse;

	private FBStatusByIdResponse _fbStatusByIdResponse;

	private ModifyUserSettingsResponse _modifyUserSettingsResponse;

	private GetQualitySettingsResponse _getQualitySettingsResponse;

	private DiscoveryServiceResponse _discoveryServiceResponse;

	private GetTwitterTimelineResponse _getTwitterTimelineResponse;

	private OpenMissionResponse _openMissionResponse;

	private EndMissionResponse _endMissionResponse;

	private GetMissionInfoResponse _getMissionInfoResponse;

	private ShareMissionResponse _shareMissionResponse;

	private RefreshMissionListResponse _refreshMissionListResponse;

	private ReviveResponse _reviveResponse;

	private EquipElementResponse _equipElementResponse;

	private UnequipElementResponse _unequipElementResponse;

	private SellItemResponse _sellItemResponse;

	private CreateAssassinResponse _createAssassinResponse;

	private TrainAssassinResponse _trainAssassinResponse;

	private TrainAssassinDuringTutorialResponse _trainAssassinDuringTutorialResponse;

	private ResetSkillTreeResponse _resetSkillTreeResponse;

	private SetExtendedClassTypeResponse _setExtendedClassTypeResponse;

	private UpdateSkillWheelResponse _updateSkillWheelResponse;

	private QuickFinishTrainingResponse _quickFinishTrainingResponse;

	private AbortTrainingResponse _abortTrainingResponse;

	private UnlockTierResponse _unlockTierResponse;

	private UnlockPassiveSkillResponse _unlockPassiveSkillResponse;

	private RemoveHirelingAndMissionResponse _removeHirelingAndMissionResponse;

	private DeleteAssassinResponse _deleteAssassinResponse;

	private CheatResponse _cheatResponse;

	private UpdateStaticsResponse _updateStaticsResponse;

	private SimulateLootRollResponse _simulateLootRollResponse;

	private GetStaticsResponse _getStaticsResponse;

	private GetStaticsMissionGeneratorResponse _getStaticsMissionGeneratorResponse;

	private CheckVersionResponse _checkVersionResponse;

	private BuyResourcesResponse _buyResourcesResponse;

	private GetProductIdentifierResponse _getProductIdentifierResponse;

	private VerifyReceiptResponse _verifyReceiptResponse;

	private VerifyGoogleReceiptResponse _verifyGoogleReceiptResponse;

	private PurchaseInGameItemResponse _purchaseInGameItemResponse;

	private PurchaseAssassinSlotResponse _purchaseAssassinSlotResponse;

	private PurchaseInventorySlotResponse _purchaseInventorySlotResponse;

	private EarlyAccessClassResponse _earlyAccessClassResponse;

	private PurchaseEnergyResponse _purchaseEnergyResponse;

	private RerollQuestRewardResponse _rerollQuestRewardResponse;

	private RegenerateRankQuestsResponse _regenerateRankQuestsResponse;

	private GetMailHeadersResponse _getMailHeadersResponse;

	private GetMailBodyResponse _getMailBodyResponse;

	private SendMailResponse _sendMailResponse;

	private DeleteMailsResponse _deleteMailsResponse;

	private SendFriendRequestResponse _sendFriendRequestResponse;

	private ProcessMailResponse _processMailResponse;

	private GetMyFriendsResponse _getMyFriendsResponse;

	private RemoveFriendResponse _removeFriendResponse;

	private ReadAchievementResponse _readAchievementResponse;

	private ClaimAchievementResponse _claimAchievementResponse;

	private ReachNewAchievementsResponse _reachNewAchievementsResponse;

	private GetDailyRewardInfoResponse _getDailyRewardInfoResponse;

	private HandleDailyRewardResponse _handleDailyRewardResponse;

	private BuyBackDailyRewardResponse _buyBackDailyRewardResponse;

	private SetTutorialStepResponse _setTutorialStepResponse;

	private SkipTutorialResponse _skipTutorialResponse;

	private SetTutorialFinishedResponse _setTutorialFinishedResponse;

	private OpenMysteryBoxResponse _openMysteryBoxResponse;

	private ClaimMysteryBoxResponse _claimMysteryBoxResponse;

	private GetMysteryBoxInfoResponse _getMysteryBoxInfoResponse;

	private ForgeResponse _forgeResponse;

	private ForgeRerollResponse _forgeRerollResponse;

	private ForgeDuringTutorialResponse _forgeDuringTutorialResponse;

	private ForgeRerollDuringTutorialResponse _forgeRerollDuringTutorialResponse;

	private VerifyAppResponse _verifyAppResponse;

	private ClaimQuestRewardResponse _claimQuestRewardResponse;

	private ClaimRankQuestCachedRewardResponse _claimRankQuestCachedRewardResponse;

	private ClaimWeeklyEventRewardResponse _claimWeeklyEventRewardResponse;

	private CollectAdRewardsResponse _collectAdRewardsResponse;

	private GetLeaderBoardsResponse _getLeaderBoardsResponse;

	private GetForeignAssassinResponse _getForeignAssassinResponse;

	private GetResetProfileResponse _getResetProfileResponse;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = false, Name = "sessionToken", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string sessionToken
	{
		get
		{
			return _sessionToken;
		}
		set
		{
			_sessionToken = value;
		}
	}

	[ProtoMember(2, IsRequired = false, Name = "error", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Error error
	{
		get
		{
			return _error;
		}
		set
		{
			_error = value;
		}
	}

	[ProtoMember(3, IsRequired = false, Name = "userProfileHash", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string userProfileHash
	{
		get
		{
			return _userProfileHash;
		}
		set
		{
			_userProfileHash = value;
		}
	}

	[ProtoMember(4, IsRequired = false, Name = "userProfile", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UserProfile userProfile
	{
		get
		{
			return _userProfile;
		}
		set
		{
			_userProfile = value;
		}
	}

	[ProtoMember(5, IsRequired = false, Name = "requestToken", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string requestToken
	{
		get
		{
			return _requestToken;
		}
		set
		{
			_requestToken = value;
		}
	}

	[ProtoMember(7, IsRequired = false, Name = "eventTrigger", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EventTrigger eventTrigger
	{
		get
		{
			return _eventTrigger;
		}
		set
		{
			_eventTrigger = value;
		}
	}

	[ProtoMember(8, IsRequired = false, Name = "events", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Events events
	{
		get
		{
			return _events;
		}
		set
		{
			_events = value;
		}
	}

	[ProtoMember(9, IsRequired = false, Name = "serverTime", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long serverTime
	{
		get
		{
			return _serverTime;
		}
		set
		{
			_serverTime = value;
		}
	}

	[ProtoMember(100, IsRequired = false, Name = "registerUserResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RegisterUserResponse registerUserResponse
	{
		get
		{
			return _registerUserResponse;
		}
		set
		{
			_registerUserResponse = value;
		}
	}

	[ProtoMember(102, IsRequired = false, Name = "authAccountResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public AuthAccountResponse authAccountResponse
	{
		get
		{
			return _authAccountResponse;
		}
		set
		{
			_authAccountResponse = value;
		}
	}

	[ProtoMember(103, IsRequired = false, Name = "getUserProfileResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetUserProfileResponse getUserProfileResponse
	{
		get
		{
			return _getUserProfileResponse;
		}
		set
		{
			_getUserProfileResponse = value;
		}
	}

	[ProtoMember(105, IsRequired = false, Name = "modifyProfileNameResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ModifyProfileNameResponse modifyProfileNameResponse
	{
		get
		{
			return _modifyProfileNameResponse;
		}
		set
		{
			_modifyProfileNameResponse = value;
		}
	}

	[ProtoMember(106, IsRequired = false, Name = "fbStatusResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public FBStatusResponse fbStatusResponse
	{
		get
		{
			return _fbStatusResponse;
		}
		set
		{
			_fbStatusResponse = value;
		}
	}

	[ProtoMember(108, IsRequired = false, Name = "fbStatusByIdResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public FBStatusByIdResponse fbStatusByIdResponse
	{
		get
		{
			return _fbStatusByIdResponse;
		}
		set
		{
			_fbStatusByIdResponse = value;
		}
	}

	[ProtoMember(109, IsRequired = false, Name = "modifyUserSettingsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ModifyUserSettingsResponse modifyUserSettingsResponse
	{
		get
		{
			return _modifyUserSettingsResponse;
		}
		set
		{
			_modifyUserSettingsResponse = value;
		}
	}

	[ProtoMember(110, IsRequired = false, Name = "getQualitySettingsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetQualitySettingsResponse getQualitySettingsResponse
	{
		get
		{
			return _getQualitySettingsResponse;
		}
		set
		{
			_getQualitySettingsResponse = value;
		}
	}

	[ProtoMember(150, IsRequired = false, Name = "discoveryServiceResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DiscoveryServiceResponse discoveryServiceResponse
	{
		get
		{
			return _discoveryServiceResponse;
		}
		set
		{
			_discoveryServiceResponse = value;
		}
	}

	[ProtoMember(151, IsRequired = false, Name = "getTwitterTimelineResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetTwitterTimelineResponse getTwitterTimelineResponse
	{
		get
		{
			return _getTwitterTimelineResponse;
		}
		set
		{
			_getTwitterTimelineResponse = value;
		}
	}

	[ProtoMember(200, IsRequired = false, Name = "openMissionResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public OpenMissionResponse openMissionResponse
	{
		get
		{
			return _openMissionResponse;
		}
		set
		{
			_openMissionResponse = value;
		}
	}

	[ProtoMember(201, IsRequired = false, Name = "endMissionResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EndMissionResponse endMissionResponse
	{
		get
		{
			return _endMissionResponse;
		}
		set
		{
			_endMissionResponse = value;
		}
	}

	[ProtoMember(202, IsRequired = false, Name = "getMissionInfoResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMissionInfoResponse getMissionInfoResponse
	{
		get
		{
			return _getMissionInfoResponse;
		}
		set
		{
			_getMissionInfoResponse = value;
		}
	}

	[ProtoMember(204, IsRequired = false, Name = "shareMissionResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ShareMissionResponse shareMissionResponse
	{
		get
		{
			return _shareMissionResponse;
		}
		set
		{
			_shareMissionResponse = value;
		}
	}

	[ProtoMember(206, IsRequired = false, Name = "refreshMissionListResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RefreshMissionListResponse refreshMissionListResponse
	{
		get
		{
			return _refreshMissionListResponse;
		}
		set
		{
			_refreshMissionListResponse = value;
		}
	}

	[ProtoMember(207, IsRequired = false, Name = "reviveResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReviveResponse reviveResponse
	{
		get
		{
			return _reviveResponse;
		}
		set
		{
			_reviveResponse = value;
		}
	}

	[ProtoMember(300, IsRequired = false, Name = "equipElementResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EquipElementResponse equipElementResponse
	{
		get
		{
			return _equipElementResponse;
		}
		set
		{
			_equipElementResponse = value;
		}
	}

	[ProtoMember(302, IsRequired = false, Name = "unequipElementResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnequipElementResponse unequipElementResponse
	{
		get
		{
			return _unequipElementResponse;
		}
		set
		{
			_unequipElementResponse = value;
		}
	}

	[ProtoMember(303, IsRequired = false, Name = "sellItemResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SellItemResponse sellItemResponse
	{
		get
		{
			return _sellItemResponse;
		}
		set
		{
			_sellItemResponse = value;
		}
	}

	[ProtoMember(400, IsRequired = false, Name = "createAssassinResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CreateAssassinResponse createAssassinResponse
	{
		get
		{
			return _createAssassinResponse;
		}
		set
		{
			_createAssassinResponse = value;
		}
	}

	[ProtoMember(401, IsRequired = false, Name = "trainAssassinResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public TrainAssassinResponse trainAssassinResponse
	{
		get
		{
			return _trainAssassinResponse;
		}
		set
		{
			_trainAssassinResponse = value;
		}
	}

	[ProtoMember(402, IsRequired = false, Name = "trainAssassinDuringTutorialResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public TrainAssassinDuringTutorialResponse trainAssassinDuringTutorialResponse
	{
		get
		{
			return _trainAssassinDuringTutorialResponse;
		}
		set
		{
			_trainAssassinDuringTutorialResponse = value;
		}
	}

	[ProtoMember(403, IsRequired = false, Name = "resetSkillTreeResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ResetSkillTreeResponse resetSkillTreeResponse
	{
		get
		{
			return _resetSkillTreeResponse;
		}
		set
		{
			_resetSkillTreeResponse = value;
		}
	}

	[ProtoMember(404, IsRequired = false, Name = "setExtendedClassTypeResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetExtendedClassTypeResponse setExtendedClassTypeResponse
	{
		get
		{
			return _setExtendedClassTypeResponse;
		}
		set
		{
			_setExtendedClassTypeResponse = value;
		}
	}

	[ProtoMember(405, IsRequired = false, Name = "updateSkillWheelResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UpdateSkillWheelResponse updateSkillWheelResponse
	{
		get
		{
			return _updateSkillWheelResponse;
		}
		set
		{
			_updateSkillWheelResponse = value;
		}
	}

	[ProtoMember(406, IsRequired = false, Name = "quickFinishTrainingResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public QuickFinishTrainingResponse quickFinishTrainingResponse
	{
		get
		{
			return _quickFinishTrainingResponse;
		}
		set
		{
			_quickFinishTrainingResponse = value;
		}
	}

	[ProtoMember(407, IsRequired = false, Name = "abortTrainingResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public AbortTrainingResponse abortTrainingResponse
	{
		get
		{
			return _abortTrainingResponse;
		}
		set
		{
			_abortTrainingResponse = value;
		}
	}

	[ProtoMember(408, IsRequired = false, Name = "unlockTierResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnlockTierResponse unlockTierResponse
	{
		get
		{
			return _unlockTierResponse;
		}
		set
		{
			_unlockTierResponse = value;
		}
	}

	[ProtoMember(409, IsRequired = false, Name = "unlockPassiveSkillResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnlockPassiveSkillResponse unlockPassiveSkillResponse
	{
		get
		{
			return _unlockPassiveSkillResponse;
		}
		set
		{
			_unlockPassiveSkillResponse = value;
		}
	}

	[ProtoMember(410, IsRequired = false, Name = "removeHirelingAndMissionResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RemoveHirelingAndMissionResponse removeHirelingAndMissionResponse
	{
		get
		{
			return _removeHirelingAndMissionResponse;
		}
		set
		{
			_removeHirelingAndMissionResponse = value;
		}
	}

	[ProtoMember(411, IsRequired = false, Name = "deleteAssassinResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DeleteAssassinResponse deleteAssassinResponse
	{
		get
		{
			return _deleteAssassinResponse;
		}
		set
		{
			_deleteAssassinResponse = value;
		}
	}

	[ProtoMember(500, IsRequired = false, Name = "cheatResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CheatResponse cheatResponse
	{
		get
		{
			return _cheatResponse;
		}
		set
		{
			_cheatResponse = value;
		}
	}

	[ProtoMember(501, IsRequired = false, Name = "updateStaticsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UpdateStaticsResponse updateStaticsResponse
	{
		get
		{
			return _updateStaticsResponse;
		}
		set
		{
			_updateStaticsResponse = value;
		}
	}

	[ProtoMember(502, IsRequired = false, Name = "simulateLootRollResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SimulateLootRollResponse simulateLootRollResponse
	{
		get
		{
			return _simulateLootRollResponse;
		}
		set
		{
			_simulateLootRollResponse = value;
		}
	}

	[ProtoMember(603, IsRequired = false, Name = "getStaticsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetStaticsResponse getStaticsResponse
	{
		get
		{
			return _getStaticsResponse;
		}
		set
		{
			_getStaticsResponse = value;
		}
	}

	[ProtoMember(604, IsRequired = false, Name = "getStaticsMissionGeneratorResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetStaticsMissionGeneratorResponse getStaticsMissionGeneratorResponse
	{
		get
		{
			return _getStaticsMissionGeneratorResponse;
		}
		set
		{
			_getStaticsMissionGeneratorResponse = value;
		}
	}

	[ProtoMember(700, IsRequired = false, Name = "checkVersionResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CheckVersionResponse checkVersionResponse
	{
		get
		{
			return _checkVersionResponse;
		}
		set
		{
			_checkVersionResponse = value;
		}
	}

	[ProtoMember(701, IsRequired = false, Name = "buyResourcesResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public BuyResourcesResponse buyResourcesResponse
	{
		get
		{
			return _buyResourcesResponse;
		}
		set
		{
			_buyResourcesResponse = value;
		}
	}

	[ProtoMember(800, IsRequired = false, Name = "getProductIdentifierResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetProductIdentifierResponse getProductIdentifierResponse
	{
		get
		{
			return _getProductIdentifierResponse;
		}
		set
		{
			_getProductIdentifierResponse = value;
		}
	}

	[ProtoMember(801, IsRequired = false, Name = "verifyReceiptResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyReceiptResponse verifyReceiptResponse
	{
		get
		{
			return _verifyReceiptResponse;
		}
		set
		{
			_verifyReceiptResponse = value;
		}
	}

	[ProtoMember(802, IsRequired = false, Name = "verifyGoogleReceiptResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyGoogleReceiptResponse verifyGoogleReceiptResponse
	{
		get
		{
			return _verifyGoogleReceiptResponse;
		}
		set
		{
			_verifyGoogleReceiptResponse = value;
		}
	}

	[ProtoMember(900, IsRequired = false, Name = "purchaseInGameItemResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseInGameItemResponse purchaseInGameItemResponse
	{
		get
		{
			return _purchaseInGameItemResponse;
		}
		set
		{
			_purchaseInGameItemResponse = value;
		}
	}

	[ProtoMember(901, IsRequired = false, Name = "purchaseAssassinSlotResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseAssassinSlotResponse purchaseAssassinSlotResponse
	{
		get
		{
			return _purchaseAssassinSlotResponse;
		}
		set
		{
			_purchaseAssassinSlotResponse = value;
		}
	}

	[ProtoMember(902, IsRequired = false, Name = "purchaseInventorySlotResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseInventorySlotResponse purchaseInventorySlotResponse
	{
		get
		{
			return _purchaseInventorySlotResponse;
		}
		set
		{
			_purchaseInventorySlotResponse = value;
		}
	}

	[ProtoMember(905, IsRequired = false, Name = "earlyAccessClassResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EarlyAccessClassResponse earlyAccessClassResponse
	{
		get
		{
			return _earlyAccessClassResponse;
		}
		set
		{
			_earlyAccessClassResponse = value;
		}
	}

	[ProtoMember(906, IsRequired = false, Name = "purchaseEnergyResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseEnergyResponse purchaseEnergyResponse
	{
		get
		{
			return _purchaseEnergyResponse;
		}
		set
		{
			_purchaseEnergyResponse = value;
		}
	}

	[ProtoMember(907, IsRequired = false, Name = "rerollQuestRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RerollQuestRewardResponse rerollQuestRewardResponse
	{
		get
		{
			return _rerollQuestRewardResponse;
		}
		set
		{
			_rerollQuestRewardResponse = value;
		}
	}

	[ProtoMember(908, IsRequired = false, Name = "regenerateRankQuestsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RegenerateRankQuestsResponse regenerateRankQuestsResponse
	{
		get
		{
			return _regenerateRankQuestsResponse;
		}
		set
		{
			_regenerateRankQuestsResponse = value;
		}
	}

	[ProtoMember(1000, IsRequired = false, Name = "getMailHeadersResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMailHeadersResponse getMailHeadersResponse
	{
		get
		{
			return _getMailHeadersResponse;
		}
		set
		{
			_getMailHeadersResponse = value;
		}
	}

	[ProtoMember(1001, IsRequired = false, Name = "getMailBodyResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMailBodyResponse getMailBodyResponse
	{
		get
		{
			return _getMailBodyResponse;
		}
		set
		{
			_getMailBodyResponse = value;
		}
	}

	[ProtoMember(1004, IsRequired = false, Name = "sendMailResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SendMailResponse sendMailResponse
	{
		get
		{
			return _sendMailResponse;
		}
		set
		{
			_sendMailResponse = value;
		}
	}

	[ProtoMember(1005, IsRequired = false, Name = "deleteMailsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DeleteMailsResponse deleteMailsResponse
	{
		get
		{
			return _deleteMailsResponse;
		}
		set
		{
			_deleteMailsResponse = value;
		}
	}

	[ProtoMember(1006, IsRequired = false, Name = "sendFriendRequestResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SendFriendRequestResponse sendFriendRequestResponse
	{
		get
		{
			return _sendFriendRequestResponse;
		}
		set
		{
			_sendFriendRequestResponse = value;
		}
	}

	[ProtoMember(1007, IsRequired = false, Name = "processMailResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ProcessMailResponse processMailResponse
	{
		get
		{
			return _processMailResponse;
		}
		set
		{
			_processMailResponse = value;
		}
	}

	[ProtoMember(1100, IsRequired = false, Name = "getMyFriendsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMyFriendsResponse getMyFriendsResponse
	{
		get
		{
			return _getMyFriendsResponse;
		}
		set
		{
			_getMyFriendsResponse = value;
		}
	}

	[ProtoMember(1101, IsRequired = false, Name = "removeFriendResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RemoveFriendResponse removeFriendResponse
	{
		get
		{
			return _removeFriendResponse;
		}
		set
		{
			_removeFriendResponse = value;
		}
	}

	[ProtoMember(1300, IsRequired = false, Name = "readAchievementResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReadAchievementResponse readAchievementResponse
	{
		get
		{
			return _readAchievementResponse;
		}
		set
		{
			_readAchievementResponse = value;
		}
	}

	[ProtoMember(1301, IsRequired = false, Name = "claimAchievementResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimAchievementResponse claimAchievementResponse
	{
		get
		{
			return _claimAchievementResponse;
		}
		set
		{
			_claimAchievementResponse = value;
		}
	}

	[ProtoMember(1302, IsRequired = false, Name = "reachNewAchievementsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReachNewAchievementsResponse reachNewAchievementsResponse
	{
		get
		{
			return _reachNewAchievementsResponse;
		}
		set
		{
			_reachNewAchievementsResponse = value;
		}
	}

	[ProtoMember(1400, IsRequired = false, Name = "getDailyRewardInfoResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetDailyRewardInfoResponse getDailyRewardInfoResponse
	{
		get
		{
			return _getDailyRewardInfoResponse;
		}
		set
		{
			_getDailyRewardInfoResponse = value;
		}
	}

	[ProtoMember(1401, IsRequired = false, Name = "handleDailyRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public HandleDailyRewardResponse handleDailyRewardResponse
	{
		get
		{
			return _handleDailyRewardResponse;
		}
		set
		{
			_handleDailyRewardResponse = value;
		}
	}

	[ProtoMember(1402, IsRequired = false, Name = "buyBackDailyRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public BuyBackDailyRewardResponse buyBackDailyRewardResponse
	{
		get
		{
			return _buyBackDailyRewardResponse;
		}
		set
		{
			_buyBackDailyRewardResponse = value;
		}
	}

	[ProtoMember(1600, IsRequired = false, Name = "setTutorialStepResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetTutorialStepResponse setTutorialStepResponse
	{
		get
		{
			return _setTutorialStepResponse;
		}
		set
		{
			_setTutorialStepResponse = value;
		}
	}

	[ProtoMember(1601, IsRequired = false, Name = "skipTutorialResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SkipTutorialResponse skipTutorialResponse
	{
		get
		{
			return _skipTutorialResponse;
		}
		set
		{
			_skipTutorialResponse = value;
		}
	}

	[ProtoMember(1602, IsRequired = false, Name = "setTutorialFinishedResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetTutorialFinishedResponse setTutorialFinishedResponse
	{
		get
		{
			return _setTutorialFinishedResponse;
		}
		set
		{
			_setTutorialFinishedResponse = value;
		}
	}

	[ProtoMember(1700, IsRequired = false, Name = "openMysteryBoxResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public OpenMysteryBoxResponse openMysteryBoxResponse
	{
		get
		{
			return _openMysteryBoxResponse;
		}
		set
		{
			_openMysteryBoxResponse = value;
		}
	}

	[ProtoMember(1701, IsRequired = false, Name = "claimMysteryBoxResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimMysteryBoxResponse claimMysteryBoxResponse
	{
		get
		{
			return _claimMysteryBoxResponse;
		}
		set
		{
			_claimMysteryBoxResponse = value;
		}
	}

	[ProtoMember(1702, IsRequired = false, Name = "getMysteryBoxInfoResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMysteryBoxInfoResponse getMysteryBoxInfoResponse
	{
		get
		{
			return _getMysteryBoxInfoResponse;
		}
		set
		{
			_getMysteryBoxInfoResponse = value;
		}
	}

	[ProtoMember(1800, IsRequired = false, Name = "forgeResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeResponse forgeResponse
	{
		get
		{
			return _forgeResponse;
		}
		set
		{
			_forgeResponse = value;
		}
	}

	[ProtoMember(1801, IsRequired = false, Name = "forgeRerollResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeRerollResponse forgeRerollResponse
	{
		get
		{
			return _forgeRerollResponse;
		}
		set
		{
			_forgeRerollResponse = value;
		}
	}

	[ProtoMember(1802, IsRequired = false, Name = "forgeDuringTutorialResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeDuringTutorialResponse forgeDuringTutorialResponse
	{
		get
		{
			return _forgeDuringTutorialResponse;
		}
		set
		{
			_forgeDuringTutorialResponse = value;
		}
	}

	[ProtoMember(1803, IsRequired = false, Name = "forgeRerollDuringTutorialResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeRerollDuringTutorialResponse forgeRerollDuringTutorialResponse
	{
		get
		{
			return _forgeRerollDuringTutorialResponse;
		}
		set
		{
			_forgeRerollDuringTutorialResponse = value;
		}
	}

	[ProtoMember(1900, IsRequired = false, Name = "verifyAppResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyAppResponse verifyAppResponse
	{
		get
		{
			return _verifyAppResponse;
		}
		set
		{
			_verifyAppResponse = value;
		}
	}

	[ProtoMember(2100, IsRequired = false, Name = "claimQuestRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimQuestRewardResponse claimQuestRewardResponse
	{
		get
		{
			return _claimQuestRewardResponse;
		}
		set
		{
			_claimQuestRewardResponse = value;
		}
	}

	[ProtoMember(2101, IsRequired = false, Name = "claimRankQuestCachedRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimRankQuestCachedRewardResponse claimRankQuestCachedRewardResponse
	{
		get
		{
			return _claimRankQuestCachedRewardResponse;
		}
		set
		{
			_claimRankQuestCachedRewardResponse = value;
		}
	}

	[ProtoMember(2150, IsRequired = false, Name = "claimWeeklyEventRewardResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimWeeklyEventRewardResponse claimWeeklyEventRewardResponse
	{
		get
		{
			return _claimWeeklyEventRewardResponse;
		}
		set
		{
			_claimWeeklyEventRewardResponse = value;
		}
	}

	[ProtoMember(2200, IsRequired = false, Name = "collectAdRewardsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CollectAdRewardsResponse collectAdRewardsResponse
	{
		get
		{
			return _collectAdRewardsResponse;
		}
		set
		{
			_collectAdRewardsResponse = value;
		}
	}

	[ProtoMember(2300, IsRequired = false, Name = "getLeaderBoardsResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetLeaderBoardsResponse getLeaderBoardsResponse
	{
		get
		{
			return _getLeaderBoardsResponse;
		}
		set
		{
			_getLeaderBoardsResponse = value;
		}
	}

	[ProtoMember(2400, IsRequired = false, Name = "getForeignAssassinResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetForeignAssassinResponse getForeignAssassinResponse
	{
		get
		{
			return _getForeignAssassinResponse;
		}
		set
		{
			_getForeignAssassinResponse = value;
		}
	}

	[ProtoMember(2500, IsRequired = false, Name = "getResetProfileResponse", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetResetProfileResponse getResetProfileResponse
	{
		get
		{
			return _getResetProfileResponse;
		}
		set
		{
			_getResetProfileResponse = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
