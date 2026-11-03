using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "Request")]
public class Request : IExtensible
{
	private string _sessionToken = "";

	private string _userProfileHash = "";

	private string _requestToken = "";

	private long _staticsVersion;

	private EventTrigger _eventTrigger;

	private RegisterUserRequest _registerUserRequest;

	private AuthAccountRequest _authAccountRequest;

	private GetUserProfileRequest _getUserProfileRequest;

	private ModifyProfileNameRequest _modifyProfileNameRequest;

	private FBStatusRequest _fbStatusRequest;

	private FBStatusByIdRequest _fbStatusByIsRequest;

	private ModifyUserSettingsRequest _modifyUserSettingsRequest;

	private GetQualitySettingsRequest _getQualitySettingsRequest;

	private DiscoveryServiceRequest _discoveryServiceRequest;

	private GetTwitterTimelineRequest _getTwitterTimelineRequest;

	private OpenMissionRequest _openMissionRequest;

	private EndMissionRequest _endMissionRequest;

	private GetMissionInfoRequest _getMissionInfoRequest;

	private ShareMissionRequest _shareMissionRequest;

	private RefreshMissionListRequest _refreshMissionListRequest;

	private ReviveRequest _reviveRequest;

	private EquipElementRequest _equipElementRequest;

	private UnequipElementRequest _unequipElementRequest;

	private SellItemRequest _sellItemRequest;

	private CreateAssassinRequest _createAssassinRequest;

	private TrainAssassinRequest _trainAssassinRequest;

	private TrainAssassinDuringTutorialRequest _trainAssassinDuringTutorialRequest;

	private ResetSkillTreeRequest _resetSkillTreeRequest;

	private SetExtendedClassTypeRequest _setExtendedClassTypeRequest;

	private UpdateSkillWheelRequest _updateSkillWheelRequest;

	private QuickFinishTrainingRequest _quickFinishTrainingRequest;

	private AbortTrainingRequest _abortTrainingRequest;

	private UnlockTierRequest _unlockTierRequest;

	private UnlockPassiveSkillRequest _unlockPassiveSkillRequest;

	private RemoveHirelingAndMissionRequest _removeHirelingAndMissionRequest;

	private DeleteAssassinRequest _deleteAssassinRequest;

	private CheatRequest _cheatRequest;

	private UpdateStaticsRequest _updateStaticsRequest;

	private SimulateLootRollRequest _simulateLootRollRequest;

	private ClearStoryMissionsRequest _clearStoryMissionsRequest;

	private GetStaticsRequest _getStaticsRequest;

	private GetStaticsMissionGeneratorRequest _getStaticsMissionGeneratorRequest;

	private CheckVersionRequest _checkVersionRequest;

	private BuyResourcesRequest _buyResourcesRequest;

	private GetProductIdentifierRequest _getProductIdentifierRequest;

	private VerifyReceiptRequest _verifyReceiptRequest;

	private VerifyGoogleReceiptRequest _verifyGoogleReceiptRequest;

	private PurchaseInGameItemRequest _purchaseInGameItemRequest;

	private PurchaseAssassinSlotRequest _purchaseAssassinSlotRequest;

	private PurchaseInventorySlotRequest _purchaseInventorySlotRequest;

	private EarlyAccessClassRequest _earlyAccessClassRequest;

	private PurchaseEnergyRequest _purchaseEnergyRequest;

	private RerollQuestRewardRequest _rerollQuestRewardRequest;

	private RegenerateRankQuestsRequest _regenerateRankQuestsRequest;

	private GetMailHeadersRequest _getMailHeadersRequest;

	private GetMailBodyRequest _getMailBodyRequest;

	private SendMailRequest _sendMailRequest;

	private DeleteMailsRequest _deleteMailsRequest;

	private SendFriendRequestRequest _sendFriendRequestRequest;

	private ProcessMailRequest _processMailRequest;

	private GetMyFriendsRequest _getMyFriendsRequest;

	private RemoveFriendRequest _removeFriendRequest;

	private ReadAchievementRequest _readAchievementRequest;

	private ClaimAchievementRequest _claimAchievementRequest;

	private ReachNewAchievementsRequest _reachNewAchievementsRequest;

	private GetDailyRewardInfoRequest _getDailyRewardInfoRequest;

	private HandleDailyRewardRequest _handleDailyRewardRequest;

	private BuyBackDailyRewardRequest _buyBackDailyRewardRequest;

	private SetTutorialStepRequest _setTutorialStepRequest;

	private SkipTutorialRequest _skipTutorialRequest;

	private SetTutorialFinishedRequest _setTutorialFinishedRequest;

	private OpenMysteryBoxRequest _openMysteryBoxRequest;

	private ClaimMysteryBoxRequest _claimMysteryBoxRequest;

	private GetMysteryBoxInfoRequest _getMysteryBoxInfoRequest;

	private ForgeRequest _forgeRequest;

	private ForgeRerollRequest _forgeRerollRequest;

	private ForgeDuringTutorialRequest _forgeDuringTutorialRequest;

	private ForgeRerollDuringTutorialRequest _forgeRerollDuringTutorialRequest;

	private VerifyAppRequest _verifyAppRequest;

	private ClaimQuestRewardRequest _claimQuestRewardRequest;

	private ClaimRankQuestCachedRewardRequest _claimRankQuestCachedRewardRequest;

	private ClaimWeeklyEventRewardRequest _claimWeeklyEventRewardRequest;

	private CollectAdRewardsRequest _collectAdRewardsRequest;

	private GetLeaderBoardsRequest _getLeaderBoardsRequest;

	private GetForeignAssassinRequest _getForeignAssassinRequest;

	private GetResetProfileRequest _getResetProfileRequest;

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

	[ProtoMember(2, IsRequired = false, Name = "userProfileHash", DataFormat = DataFormat.Default)]
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

	[ProtoMember(3, IsRequired = false, Name = "requestToken", DataFormat = DataFormat.Default)]
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

	[ProtoMember(4, IsRequired = false, Name = "staticsVersion", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public long staticsVersion
	{
		get
		{
			return _staticsVersion;
		}
		set
		{
			_staticsVersion = value;
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

	[ProtoMember(100, IsRequired = false, Name = "registerUserRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RegisterUserRequest registerUserRequest
	{
		get
		{
			return _registerUserRequest;
		}
		set
		{
			_registerUserRequest = value;
		}
	}

	[ProtoMember(102, IsRequired = false, Name = "authAccountRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public AuthAccountRequest authAccountRequest
	{
		get
		{
			return _authAccountRequest;
		}
		set
		{
			_authAccountRequest = value;
		}
	}

	[ProtoMember(103, IsRequired = false, Name = "getUserProfileRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetUserProfileRequest getUserProfileRequest
	{
		get
		{
			return _getUserProfileRequest;
		}
		set
		{
			_getUserProfileRequest = value;
		}
	}

	[ProtoMember(105, IsRequired = false, Name = "modifyProfileNameRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ModifyProfileNameRequest modifyProfileNameRequest
	{
		get
		{
			return _modifyProfileNameRequest;
		}
		set
		{
			_modifyProfileNameRequest = value;
		}
	}

	[ProtoMember(106, IsRequired = false, Name = "fbStatusRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public FBStatusRequest fbStatusRequest
	{
		get
		{
			return _fbStatusRequest;
		}
		set
		{
			_fbStatusRequest = value;
		}
	}

	[ProtoMember(108, IsRequired = false, Name = "fbStatusByIsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public FBStatusByIdRequest fbStatusByIsRequest
	{
		get
		{
			return _fbStatusByIsRequest;
		}
		set
		{
			_fbStatusByIsRequest = value;
		}
	}

	[ProtoMember(109, IsRequired = false, Name = "modifyUserSettingsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ModifyUserSettingsRequest modifyUserSettingsRequest
	{
		get
		{
			return _modifyUserSettingsRequest;
		}
		set
		{
			_modifyUserSettingsRequest = value;
		}
	}

	[ProtoMember(110, IsRequired = false, Name = "getQualitySettingsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetQualitySettingsRequest getQualitySettingsRequest
	{
		get
		{
			return _getQualitySettingsRequest;
		}
		set
		{
			_getQualitySettingsRequest = value;
		}
	}

	[ProtoMember(150, IsRequired = false, Name = "discoveryServiceRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DiscoveryServiceRequest discoveryServiceRequest
	{
		get
		{
			return _discoveryServiceRequest;
		}
		set
		{
			_discoveryServiceRequest = value;
		}
	}

	[ProtoMember(151, IsRequired = false, Name = "getTwitterTimelineRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetTwitterTimelineRequest getTwitterTimelineRequest
	{
		get
		{
			return _getTwitterTimelineRequest;
		}
		set
		{
			_getTwitterTimelineRequest = value;
		}
	}

	[ProtoMember(200, IsRequired = false, Name = "openMissionRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public OpenMissionRequest openMissionRequest
	{
		get
		{
			return _openMissionRequest;
		}
		set
		{
			_openMissionRequest = value;
		}
	}

	[ProtoMember(201, IsRequired = false, Name = "endMissionRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EndMissionRequest endMissionRequest
	{
		get
		{
			return _endMissionRequest;
		}
		set
		{
			_endMissionRequest = value;
		}
	}

	[ProtoMember(202, IsRequired = false, Name = "getMissionInfoRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMissionInfoRequest getMissionInfoRequest
	{
		get
		{
			return _getMissionInfoRequest;
		}
		set
		{
			_getMissionInfoRequest = value;
		}
	}

	[ProtoMember(204, IsRequired = false, Name = "shareMissionRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ShareMissionRequest shareMissionRequest
	{
		get
		{
			return _shareMissionRequest;
		}
		set
		{
			_shareMissionRequest = value;
		}
	}

	[ProtoMember(206, IsRequired = false, Name = "refreshMissionListRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RefreshMissionListRequest refreshMissionListRequest
	{
		get
		{
			return _refreshMissionListRequest;
		}
		set
		{
			_refreshMissionListRequest = value;
		}
	}

	[ProtoMember(207, IsRequired = false, Name = "reviveRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReviveRequest reviveRequest
	{
		get
		{
			return _reviveRequest;
		}
		set
		{
			_reviveRequest = value;
		}
	}

	[ProtoMember(300, IsRequired = false, Name = "equipElementRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EquipElementRequest equipElementRequest
	{
		get
		{
			return _equipElementRequest;
		}
		set
		{
			_equipElementRequest = value;
		}
	}

	[ProtoMember(302, IsRequired = false, Name = "unequipElementRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnequipElementRequest unequipElementRequest
	{
		get
		{
			return _unequipElementRequest;
		}
		set
		{
			_unequipElementRequest = value;
		}
	}

	[ProtoMember(303, IsRequired = false, Name = "sellItemRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SellItemRequest sellItemRequest
	{
		get
		{
			return _sellItemRequest;
		}
		set
		{
			_sellItemRequest = value;
		}
	}

	[ProtoMember(400, IsRequired = false, Name = "createAssassinRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CreateAssassinRequest createAssassinRequest
	{
		get
		{
			return _createAssassinRequest;
		}
		set
		{
			_createAssassinRequest = value;
		}
	}

	[ProtoMember(401, IsRequired = false, Name = "trainAssassinRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public TrainAssassinRequest trainAssassinRequest
	{
		get
		{
			return _trainAssassinRequest;
		}
		set
		{
			_trainAssassinRequest = value;
		}
	}

	[ProtoMember(402, IsRequired = false, Name = "trainAssassinDuringTutorialRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public TrainAssassinDuringTutorialRequest trainAssassinDuringTutorialRequest
	{
		get
		{
			return _trainAssassinDuringTutorialRequest;
		}
		set
		{
			_trainAssassinDuringTutorialRequest = value;
		}
	}

	[ProtoMember(403, IsRequired = false, Name = "resetSkillTreeRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ResetSkillTreeRequest resetSkillTreeRequest
	{
		get
		{
			return _resetSkillTreeRequest;
		}
		set
		{
			_resetSkillTreeRequest = value;
		}
	}

	[ProtoMember(404, IsRequired = false, Name = "setExtendedClassTypeRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetExtendedClassTypeRequest setExtendedClassTypeRequest
	{
		get
		{
			return _setExtendedClassTypeRequest;
		}
		set
		{
			_setExtendedClassTypeRequest = value;
		}
	}

	[ProtoMember(405, IsRequired = false, Name = "updateSkillWheelRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UpdateSkillWheelRequest updateSkillWheelRequest
	{
		get
		{
			return _updateSkillWheelRequest;
		}
		set
		{
			_updateSkillWheelRequest = value;
		}
	}

	[ProtoMember(406, IsRequired = false, Name = "quickFinishTrainingRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public QuickFinishTrainingRequest quickFinishTrainingRequest
	{
		get
		{
			return _quickFinishTrainingRequest;
		}
		set
		{
			_quickFinishTrainingRequest = value;
		}
	}

	[ProtoMember(407, IsRequired = false, Name = "abortTrainingRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public AbortTrainingRequest abortTrainingRequest
	{
		get
		{
			return _abortTrainingRequest;
		}
		set
		{
			_abortTrainingRequest = value;
		}
	}

	[ProtoMember(408, IsRequired = false, Name = "unlockTierRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnlockTierRequest unlockTierRequest
	{
		get
		{
			return _unlockTierRequest;
		}
		set
		{
			_unlockTierRequest = value;
		}
	}

	[ProtoMember(409, IsRequired = false, Name = "unlockPassiveSkillRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UnlockPassiveSkillRequest unlockPassiveSkillRequest
	{
		get
		{
			return _unlockPassiveSkillRequest;
		}
		set
		{
			_unlockPassiveSkillRequest = value;
		}
	}

	[ProtoMember(410, IsRequired = false, Name = "removeHirelingAndMissionRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RemoveHirelingAndMissionRequest removeHirelingAndMissionRequest
	{
		get
		{
			return _removeHirelingAndMissionRequest;
		}
		set
		{
			_removeHirelingAndMissionRequest = value;
		}
	}

	[ProtoMember(411, IsRequired = false, Name = "deleteAssassinRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DeleteAssassinRequest deleteAssassinRequest
	{
		get
		{
			return _deleteAssassinRequest;
		}
		set
		{
			_deleteAssassinRequest = value;
		}
	}

	[ProtoMember(500, IsRequired = false, Name = "cheatRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CheatRequest cheatRequest
	{
		get
		{
			return _cheatRequest;
		}
		set
		{
			_cheatRequest = value;
		}
	}

	[ProtoMember(501, IsRequired = false, Name = "updateStaticsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public UpdateStaticsRequest updateStaticsRequest
	{
		get
		{
			return _updateStaticsRequest;
		}
		set
		{
			_updateStaticsRequest = value;
		}
	}

	[ProtoMember(502, IsRequired = false, Name = "simulateLootRollRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SimulateLootRollRequest simulateLootRollRequest
	{
		get
		{
			return _simulateLootRollRequest;
		}
		set
		{
			_simulateLootRollRequest = value;
		}
	}

	[ProtoMember(503, IsRequired = false, Name = "clearStoryMissionsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClearStoryMissionsRequest clearStoryMissionsRequest
	{
		get
		{
			return _clearStoryMissionsRequest;
		}
		set
		{
			_clearStoryMissionsRequest = value;
		}
	}

	[ProtoMember(603, IsRequired = false, Name = "getStaticsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetStaticsRequest getStaticsRequest
	{
		get
		{
			return _getStaticsRequest;
		}
		set
		{
			_getStaticsRequest = value;
		}
	}

	[ProtoMember(604, IsRequired = false, Name = "getStaticsMissionGeneratorRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetStaticsMissionGeneratorRequest getStaticsMissionGeneratorRequest
	{
		get
		{
			return _getStaticsMissionGeneratorRequest;
		}
		set
		{
			_getStaticsMissionGeneratorRequest = value;
		}
	}

	[ProtoMember(700, IsRequired = false, Name = "checkVersionRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CheckVersionRequest checkVersionRequest
	{
		get
		{
			return _checkVersionRequest;
		}
		set
		{
			_checkVersionRequest = value;
		}
	}

	[ProtoMember(701, IsRequired = false, Name = "buyResourcesRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public BuyResourcesRequest buyResourcesRequest
	{
		get
		{
			return _buyResourcesRequest;
		}
		set
		{
			_buyResourcesRequest = value;
		}
	}

	[ProtoMember(800, IsRequired = false, Name = "getProductIdentifierRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetProductIdentifierRequest getProductIdentifierRequest
	{
		get
		{
			return _getProductIdentifierRequest;
		}
		set
		{
			_getProductIdentifierRequest = value;
		}
	}

	[ProtoMember(801, IsRequired = false, Name = "verifyReceiptRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyReceiptRequest verifyReceiptRequest
	{
		get
		{
			return _verifyReceiptRequest;
		}
		set
		{
			_verifyReceiptRequest = value;
		}
	}

	[ProtoMember(802, IsRequired = false, Name = "verifyGoogleReceiptRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyGoogleReceiptRequest verifyGoogleReceiptRequest
	{
		get
		{
			return _verifyGoogleReceiptRequest;
		}
		set
		{
			_verifyGoogleReceiptRequest = value;
		}
	}

	[ProtoMember(900, IsRequired = false, Name = "purchaseInGameItemRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseInGameItemRequest purchaseInGameItemRequest
	{
		get
		{
			return _purchaseInGameItemRequest;
		}
		set
		{
			_purchaseInGameItemRequest = value;
		}
	}

	[ProtoMember(901, IsRequired = false, Name = "purchaseAssassinSlotRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseAssassinSlotRequest purchaseAssassinSlotRequest
	{
		get
		{
			return _purchaseAssassinSlotRequest;
		}
		set
		{
			_purchaseAssassinSlotRequest = value;
		}
	}

	[ProtoMember(902, IsRequired = false, Name = "purchaseInventorySlotRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseInventorySlotRequest purchaseInventorySlotRequest
	{
		get
		{
			return _purchaseInventorySlotRequest;
		}
		set
		{
			_purchaseInventorySlotRequest = value;
		}
	}

	[ProtoMember(905, IsRequired = false, Name = "earlyAccessClassRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public EarlyAccessClassRequest earlyAccessClassRequest
	{
		get
		{
			return _earlyAccessClassRequest;
		}
		set
		{
			_earlyAccessClassRequest = value;
		}
	}

	[ProtoMember(906, IsRequired = false, Name = "purchaseEnergyRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public PurchaseEnergyRequest purchaseEnergyRequest
	{
		get
		{
			return _purchaseEnergyRequest;
		}
		set
		{
			_purchaseEnergyRequest = value;
		}
	}

	[ProtoMember(907, IsRequired = false, Name = "rerollQuestRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RerollQuestRewardRequest rerollQuestRewardRequest
	{
		get
		{
			return _rerollQuestRewardRequest;
		}
		set
		{
			_rerollQuestRewardRequest = value;
		}
	}

	[ProtoMember(908, IsRequired = false, Name = "regenerateRankQuestsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RegenerateRankQuestsRequest regenerateRankQuestsRequest
	{
		get
		{
			return _regenerateRankQuestsRequest;
		}
		set
		{
			_regenerateRankQuestsRequest = value;
		}
	}

	[ProtoMember(1000, IsRequired = false, Name = "getMailHeadersRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMailHeadersRequest getMailHeadersRequest
	{
		get
		{
			return _getMailHeadersRequest;
		}
		set
		{
			_getMailHeadersRequest = value;
		}
	}

	[ProtoMember(1001, IsRequired = false, Name = "getMailBodyRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMailBodyRequest getMailBodyRequest
	{
		get
		{
			return _getMailBodyRequest;
		}
		set
		{
			_getMailBodyRequest = value;
		}
	}

	[ProtoMember(1004, IsRequired = false, Name = "sendMailRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SendMailRequest sendMailRequest
	{
		get
		{
			return _sendMailRequest;
		}
		set
		{
			_sendMailRequest = value;
		}
	}

	[ProtoMember(1005, IsRequired = false, Name = "deleteMailsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public DeleteMailsRequest deleteMailsRequest
	{
		get
		{
			return _deleteMailsRequest;
		}
		set
		{
			_deleteMailsRequest = value;
		}
	}

	[ProtoMember(1006, IsRequired = false, Name = "sendFriendRequestRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SendFriendRequestRequest sendFriendRequestRequest
	{
		get
		{
			return _sendFriendRequestRequest;
		}
		set
		{
			_sendFriendRequestRequest = value;
		}
	}

	[ProtoMember(1007, IsRequired = false, Name = "processMailRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ProcessMailRequest processMailRequest
	{
		get
		{
			return _processMailRequest;
		}
		set
		{
			_processMailRequest = value;
		}
	}

	[ProtoMember(1100, IsRequired = false, Name = "getMyFriendsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMyFriendsRequest getMyFriendsRequest
	{
		get
		{
			return _getMyFriendsRequest;
		}
		set
		{
			_getMyFriendsRequest = value;
		}
	}

	[ProtoMember(1101, IsRequired = false, Name = "removeFriendRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public RemoveFriendRequest removeFriendRequest
	{
		get
		{
			return _removeFriendRequest;
		}
		set
		{
			_removeFriendRequest = value;
		}
	}

	[ProtoMember(1300, IsRequired = false, Name = "readAchievementRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReadAchievementRequest readAchievementRequest
	{
		get
		{
			return _readAchievementRequest;
		}
		set
		{
			_readAchievementRequest = value;
		}
	}

	[ProtoMember(1301, IsRequired = false, Name = "claimAchievementRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimAchievementRequest claimAchievementRequest
	{
		get
		{
			return _claimAchievementRequest;
		}
		set
		{
			_claimAchievementRequest = value;
		}
	}

	[ProtoMember(1302, IsRequired = false, Name = "reachNewAchievementsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ReachNewAchievementsRequest reachNewAchievementsRequest
	{
		get
		{
			return _reachNewAchievementsRequest;
		}
		set
		{
			_reachNewAchievementsRequest = value;
		}
	}

	[ProtoMember(1400, IsRequired = false, Name = "getDailyRewardInfoRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetDailyRewardInfoRequest getDailyRewardInfoRequest
	{
		get
		{
			return _getDailyRewardInfoRequest;
		}
		set
		{
			_getDailyRewardInfoRequest = value;
		}
	}

	[ProtoMember(1401, IsRequired = false, Name = "handleDailyRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public HandleDailyRewardRequest handleDailyRewardRequest
	{
		get
		{
			return _handleDailyRewardRequest;
		}
		set
		{
			_handleDailyRewardRequest = value;
		}
	}

	[ProtoMember(1402, IsRequired = false, Name = "buyBackDailyRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public BuyBackDailyRewardRequest buyBackDailyRewardRequest
	{
		get
		{
			return _buyBackDailyRewardRequest;
		}
		set
		{
			_buyBackDailyRewardRequest = value;
		}
	}

	[ProtoMember(1600, IsRequired = false, Name = "setTutorialStepRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetTutorialStepRequest setTutorialStepRequest
	{
		get
		{
			return _setTutorialStepRequest;
		}
		set
		{
			_setTutorialStepRequest = value;
		}
	}

	[ProtoMember(1601, IsRequired = false, Name = "skipTutorialRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SkipTutorialRequest skipTutorialRequest
	{
		get
		{
			return _skipTutorialRequest;
		}
		set
		{
			_skipTutorialRequest = value;
		}
	}

	[ProtoMember(1602, IsRequired = false, Name = "setTutorialFinishedRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public SetTutorialFinishedRequest setTutorialFinishedRequest
	{
		get
		{
			return _setTutorialFinishedRequest;
		}
		set
		{
			_setTutorialFinishedRequest = value;
		}
	}

	[ProtoMember(1700, IsRequired = false, Name = "openMysteryBoxRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public OpenMysteryBoxRequest openMysteryBoxRequest
	{
		get
		{
			return _openMysteryBoxRequest;
		}
		set
		{
			_openMysteryBoxRequest = value;
		}
	}

	[ProtoMember(1701, IsRequired = false, Name = "claimMysteryBoxRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimMysteryBoxRequest claimMysteryBoxRequest
	{
		get
		{
			return _claimMysteryBoxRequest;
		}
		set
		{
			_claimMysteryBoxRequest = value;
		}
	}

	[ProtoMember(1702, IsRequired = false, Name = "getMysteryBoxInfoRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetMysteryBoxInfoRequest getMysteryBoxInfoRequest
	{
		get
		{
			return _getMysteryBoxInfoRequest;
		}
		set
		{
			_getMysteryBoxInfoRequest = value;
		}
	}

	[ProtoMember(1800, IsRequired = false, Name = "forgeRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeRequest forgeRequest
	{
		get
		{
			return _forgeRequest;
		}
		set
		{
			_forgeRequest = value;
		}
	}

	[ProtoMember(1801, IsRequired = false, Name = "forgeRerollRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeRerollRequest forgeRerollRequest
	{
		get
		{
			return _forgeRerollRequest;
		}
		set
		{
			_forgeRerollRequest = value;
		}
	}

	[ProtoMember(1802, IsRequired = false, Name = "forgeDuringTutorialRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeDuringTutorialRequest forgeDuringTutorialRequest
	{
		get
		{
			return _forgeDuringTutorialRequest;
		}
		set
		{
			_forgeDuringTutorialRequest = value;
		}
	}

	[ProtoMember(1803, IsRequired = false, Name = "forgeRerollDuringTutorialRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ForgeRerollDuringTutorialRequest forgeRerollDuringTutorialRequest
	{
		get
		{
			return _forgeRerollDuringTutorialRequest;
		}
		set
		{
			_forgeRerollDuringTutorialRequest = value;
		}
	}

	[ProtoMember(1900, IsRequired = false, Name = "verifyAppRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public VerifyAppRequest verifyAppRequest
	{
		get
		{
			return _verifyAppRequest;
		}
		set
		{
			_verifyAppRequest = value;
		}
	}

	[ProtoMember(2100, IsRequired = false, Name = "claimQuestRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimQuestRewardRequest claimQuestRewardRequest
	{
		get
		{
			return _claimQuestRewardRequest;
		}
		set
		{
			_claimQuestRewardRequest = value;
		}
	}

	[ProtoMember(2101, IsRequired = false, Name = "claimRankQuestCachedRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimRankQuestCachedRewardRequest claimRankQuestCachedRewardRequest
	{
		get
		{
			return _claimRankQuestCachedRewardRequest;
		}
		set
		{
			_claimRankQuestCachedRewardRequest = value;
		}
	}

	[ProtoMember(2150, IsRequired = false, Name = "claimWeeklyEventRewardRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ClaimWeeklyEventRewardRequest claimWeeklyEventRewardRequest
	{
		get
		{
			return _claimWeeklyEventRewardRequest;
		}
		set
		{
			_claimWeeklyEventRewardRequest = value;
		}
	}

	[ProtoMember(2200, IsRequired = false, Name = "collectAdRewardsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public CollectAdRewardsRequest collectAdRewardsRequest
	{
		get
		{
			return _collectAdRewardsRequest;
		}
		set
		{
			_collectAdRewardsRequest = value;
		}
	}

	[ProtoMember(2300, IsRequired = false, Name = "getLeaderBoardsRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetLeaderBoardsRequest getLeaderBoardsRequest
	{
		get
		{
			return _getLeaderBoardsRequest;
		}
		set
		{
			_getLeaderBoardsRequest = value;
		}
	}

	[ProtoMember(2400, IsRequired = false, Name = "getForeignAssassinRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetForeignAssassinRequest getForeignAssassinRequest
	{
		get
		{
			return _getForeignAssassinRequest;
		}
		set
		{
			_getForeignAssassinRequest = value;
		}
	}

	[ProtoMember(2500, IsRequired = false, Name = "getResetProfileRequest", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public GetResetProfileRequest getResetProfileRequest
	{
		get
		{
			return _getResetProfileRequest;
		}
		set
		{
			_getResetProfileRequest = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
