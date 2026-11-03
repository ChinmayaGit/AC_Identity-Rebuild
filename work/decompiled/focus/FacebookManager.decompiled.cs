using System;
using System.Collections;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Achievements;
using Assets.Scripts.Database;
using Assets.Scripts.Native;
using Assets.Scripts.Network;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.Mail;
using Assets.Scripts.UI.Network;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Screens.Popup;
using Assets.Scripts.UI.Screens.Popup.ErrorPopup;
using Assets.Scripts.UI.Screens.Popup.FacebookInvitePopup;
using Assets.Scripts.UI.Screens.Popup.ThirdPartyAccountPopup;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.Types;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using SharedBaseLib.Core;
using UbiCore.Utils;
using UnityEngine;

public static class FacebookManager
{
	public enum EFBLoginFlowResult
	{
		NotLoggedIn,
		FailedOrCancelled,
		FacebookUnavailable,
		FacebookNotInitialized,
		FacebookUnreachable,
		AlreadyLoggedIn,
		LinkedToNewProfile,
		LoggedInSameAccount,
		RecoveredAccount,
		ResetAccount,
		OldProfileOverriden
	}

	public enum EFBLogoutFlowResult
	{
		FacebookUnavailable,
		FacebookNotInitialized,
		Failed,
		Succeeded
	}

	public enum EFBInviteFlowResult
	{
		FacebookUnavailable,
		FacebookNotInitialized,
		FacebookNotLoggedIn,
		Failed,
		Cancelled,
		Succeeded
	}

	public enum EFBShareItemFlowResult
	{
		FacebookUnavailable,
		FacebookNotInitialized,
		FacebookNotLoggedIn,
		FailedOrCancelled,
		Succeeded
	}

	public class InviteStatus
	{
		public IList<FacebookHandlerBase.FBFriendInfo> Friends = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public int ProcessedIndex;

		public IList<FacebookHandlerBase.FBFriendInfo> Invited = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public IList<FacebookHandlerBase.FBFriendInfo> AlreadyFriends = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public IList<FacebookHandlerBase.FBFriendInfo> DontHaveGame = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public IList<FacebookHandlerBase.FBFriendInfo> FriendListFull = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public IList<FacebookHandlerBase.FBFriendInfo> Failed = new BBList<FacebookHandlerBase.FBFriendInfo>();

		public IList<FriendProfile> AppFriends = new BBList<FriendProfile>();

		public int MaxFriends;
	}

	public delegate void FBLoginFlowCallback(bool _success, EFBLoginFlowResult _result);

	public delegate void FBLogoutFlowCallback(bool _success, EFBLogoutFlowResult _result);

	public delegate void FBAccountStatusCallback(bool _registered, UserProfile _userProfile);

	public delegate void FBInviteFlowCallback(bool _success, EFBInviteFlowResult _result);

	public delegate void FBShareItemFlowCallback(bool _success, EFBShareItemFlowResult _result);

	public delegate void QueryFbStatusByIdCallback(bool _fbHasProfile, UserProfile _newUserProfile);

	public delegate void BackKeyHandler();

	private delegate void FBProcessInvitesCallback(FBInviteFlowCallback _callback, InviteStatus _inviteStatus);

	private const string OfficialBundleIdentifier = "com.ubisoft.assassinscreed.identity";

	private const string AchievementPostFacebookPageUrl = "https://www.facebook.com/AssassinsCreedIdentity";

	private const string AchievementPostCDNUrl = "http://static.cdn.ubi.com/0040/fb/";

	private const string AchievementPostTrackingReference = "PostAchievement";

	private const string FacebookGameIdKey = "FacebookGameId";

	private const string FacebookProfileIdKey = "FacebookUserId";

	private const string FacebookProfileNameKey = "FacebookUserName";

	private const string FacebookLoggedInKey = "FacebookLoggedIn";

	private const string FacebookRegisteredKey = "FacebookRegistered";

	private const string FACEBOOK_APP_SCHEME = "fb://";

	private static BBList<string> AchievementPostRankMappingSuffix = new BBList<string> { "_bronze", "_silver", "_gold" };

	public static BackKeyHandler OnBackKey = null;

	private static FacebookHandlerBase m_facebookHandler;

	private static FBLoginFlowCallback m_FBLoginFlowCallback = null;

	private static bool m_isFacebookReachable = true;

	public static bool IsProfileLinkedToFacebook
	{
		get
		{
			UserProfile profile = Singleton<StateManager>.Instance.ProfileManager.Profile;
			string text = PlayerPrefs.GetString("FacebookGameId", null);
			string text2 = profile?.userIdHash;
			return text != null && text == text2;
		}
	}

	public static string LinkedFacebookUserName => (!IsProfileLinkedToFacebook) ? string.Empty : PlayerPrefs.GetString("FacebookUserName");

	public static string LinkedFacebookUserId => (!IsProfileLinkedToFacebook) ? string.Empty : PlayerPrefs.GetString("FacebookUserId");

	public static string LinkedFacebookGameId => (!IsProfileLinkedToFacebook) ? string.Empty : PlayerPrefs.GetString("FacebookGameId");

	public static bool IsAutoLoginInProgress { get; set; }

	public static bool OverrideIsFacebookAvailable { get; set; }

	public static bool IsEnabledForTesting { get; set; }

	public static bool IsAvailable
	{
		get
		{
			if (OverrideIsFacebookAvailable)
			{
				return IsEnabledForTesting;
			}
			return false;
		}
	}

	public static bool IsInitialized => FacebookHandler.IsInitialized;

	public static bool IsLoggedIn => FacebookHandler.IsLoggedIn;

	public static bool CanShareAchievements => IsAvailable && IsLoggedIn;

	public static string AccessToken => FacebookHandler.AccessToken;

	public static string UserId => FacebookHandler.UserId;

	public static string Name { get; private set; }

	public static FacebookHandlerBase FacebookHandler
	{
		get
		{
			if (m_facebookHandler == null)
			{
				m_facebookHandler = new FacebookHandlerMobile();
			}
			return m_facebookHandler;
		}
	}

	public static void AutoInit()
	{
		if (FacebookHandler.IsAvailable)
		{
			IsAutoLoginInProgress = false;
			Main.Instance.StartCoroutine(TryInit());
		}
	}

	public static void Logout(FBLogoutFlowCallback _callback = null)
	{
		ShowLoadingInitOverlay();
		if (!FacebookHandler.IsAvailable)
		{
			CompleteLogoutFlow(_success: false, EFBLogoutFlowResult.FacebookUnavailable, _callback);
			ShowFacebookUnavailableError();
			return;
		}
		if (!FacebookHandler.IsInitialized)
		{
			CompleteLogoutFlow(_success: false, EFBLogoutFlowResult.FacebookNotInitialized, _callback);
			ShowFacebookNotInitializedError();
			return;
		}
		if (!FacebookHandler.IsLoggedIn)
		{
			CompleteLogoutFlow(_success: true, EFBLogoutFlowResult.Succeeded, _callback);
			return;
		}
		FacebookHandler.FBLogout((bool logoutSuccess, FacebookHandlerBase.EFBRequestType logoutRequestType, FacebookHandlerBase.EFBReturnCode logoutResultCode) =>
		{
			if (!logoutSuccess)
			{
				CompleteLogoutFlow(_success: false, EFBLogoutFlowResult.Failed, _callback);
			}
			else
			{
				CompleteLogoutFlow(_success: true, EFBLogoutFlowResult.Succeeded, _callback);
			}
		});
	}

	public static void LogoutSilently(FBLogoutFlowCallback _callback = null)
	{
		if (!FacebookHandler.IsAvailable || !FacebookHandler.IsInitialized || !FacebookHandler.IsLoggedIn)
		{
			if (_callback != null)
			{
				_callback(_success: false, EFBLogoutFlowResult.Failed);
			}
			return;
		}
		FacebookHandler.FBLogout((bool logoutSuccess, FacebookHandlerBase.EFBRequestType logoutRequestType, FacebookHandlerBase.EFBReturnCode logoutResultCode) =>
		{
			if (_callback != null)
			{
				_callback(logoutSuccess, (!logoutSuccess) ? EFBLogoutFlowResult.Failed : EFBLogoutFlowResult.Succeeded);
			}
		});
	}

	public static void AutoLogin(FBLoginFlowCallback _callback = null)
	{
		IsAutoLoginInProgress = true;
		if (!IsProfileLinkedToFacebook || !PlayerPrefs.HasKey("FacebookLoggedIn") || PlayerPrefs.GetInt("FacebookLoggedIn") == 0)
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.NotLoggedIn, _callback);
		}
		else
		{
			Login(_callback);
		}
	}

	public static void Login(FBLoginFlowCallback _callback = null)
	{
		ShowLoadingInitOverlay();
		if (!FacebookHandler.IsAvailable)
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FacebookUnavailable, _callback);
			ShowFacebookUnavailableError();
			return;
		}
		if (!FacebookHandler.IsInitialized)
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FacebookNotInitialized, _callback);
			ShowFacebookNotInitializedError();
			return;
		}
		if (FacebookHandler.IsLoggedIn && IsProfileLinkedToFacebook && LinkedFacebookUserId == FacebookHandler.UserId)
		{
			Name = LinkedFacebookUserName;
			CompleteLoginFlow(_success: true, EFBLoginFlowResult.AlreadyLoggedIn, _callback);
			return;
		}
		FacebookHandler.FBLogin((bool success, FacebookHandlerBase.EFBRequestType requestType, FacebookHandlerBase.EFBReturnCode resultCode) =>
		{
			if (!success)
			{
				CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
				PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.FACEBOOK_LOGIN_FAILED, ErrorCategory.FACEBOOK);
				Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
			}
			else
			{
				FetchFacebookName(_callback);
			}
		});
	}

	public static void Invite(FBInviteFlowCallback _callback = null)
	{
		if (!FacebookHandler.IsAvailable)
		{
			CompleteInviteFlow(_success: false, EFBInviteFlowResult.FacebookUnavailable, _callback);
			ShowFacebookUnavailableError();
			return;
		}
		if (!FacebookHandler.IsInitialized)
		{
			CompleteInviteFlow(_success: false, EFBInviteFlowResult.FacebookNotInitialized, _callback);
			ShowFacebookNotInitializedError();
			return;
		}
		if (!FacebookHandler.IsLoggedIn)
		{
			CompleteInviteFlow(_success: false, EFBInviteFlowResult.FacebookNotLoggedIn, _callback);
			ShowFacebookNotLoggedInError();
			return;
		}
		ShowLoadingInitOverlay();
		FacebookHandler.FBInviteFriends(null, (IList<FacebookHandlerBase.FBFriendInfo> friends) =>
		{
			if (friends == null)
			{
				CompleteInviteFlow(_success: false, EFBInviteFlowResult.Cancelled, _callback);
			}
			else
			{
				InviteStatus inviteStatus = new InviteStatus
				{
					ProcessedIndex = 0,
					Friends = friends,
					AppFriends = Singleton<StateManager>.Instance.ProfileManager.Profile.friends.friendProfiles,
					MaxFriends = Singleton<StateManager>.Instance.ProfileManager.Profile.maxFriends
				};
				ProcessInvites(_callback, inviteStatus);
			}
		});
	}

	public static void ShareAchievement(AchievementMapping.Definition _achievementMapping, int _rankIndex, FBShareItemFlowCallback _callback = null)
	{
		if (!FacebookHandler.IsAvailable)
		{
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FacebookUnavailable, _callback);
			ShowFacebookUnavailableError();
			return;
		}
		if (!FacebookHandler.IsInitialized)
		{
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FacebookNotInitialized, _callback);
			ShowFacebookNotInitializedError();
			return;
		}
		if (!FacebookHandler.IsLoggedIn)
		{
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FacebookNotLoggedIn, _callback);
			return;
		}
		AchievementsManager achievementsManager = Singleton<StateManager>.Instance.AchievementsManager;
		Achievement value = null;
		AchievementCategoryData achievementCategoryData = achievementsManager.GetAchievementCategoryData(_achievementMapping.categoryId);
		if (!achievementCategoryData.IdToAchievements.TryGetValue(_achievementMapping.id, out value))
		{
			Debug.LogError("Could not find achievement for mapping ID " + _achievementMapping.id);
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FailedOrCancelled, _callback);
			return;
		}
		if (_rankIndex <= 0 || _rankIndex > AchievementPostRankMappingSuffix.Count)
		{
			Debug.LogError("Achievement rank is incorrect for mapping ID " + _achievementMapping.id);
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FailedOrCancelled, _callback);
			return;
		}
		AchievementDefinition achievementDefinition = achievementsManager.GetAchievementDefinition(_achievementMapping);
		if (achievementDefinition == null)
		{
			Debug.LogError("Achievement definition not found for mapping ID " + _achievementMapping.id);
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FailedOrCancelled, _callback);
			return;
		}
		AchievementMapping.RankDefinition rankDefinitionByIdx = _achievementMapping.GetRankDefinitionByIdx(_rankIndex);
		if (rankDefinitionByIdx == null)
		{
			Debug.LogError("Achievement rank definition not found for mapping ID " + _achievementMapping.id);
			CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FailedOrCancelled, _callback);
			return;
		}
		long value2 = 1L;
		ECounterType counterType = ECounterType.InvalidCounter;
		if (rankDefinitionByIdx.conditions.Length > 0)
		{
			AchievementMapping.Condition condition = rankDefinitionByIdx.conditions[0];
			value2 = condition.goal;
			counterType = condition.counterType;
		}
		string linkName = LocalizationManager.Instance.Get(achievementDefinition.NameLocaKey.Key);
		UIAchievementGoal value3 = new UIAchievementGoal(value2, counterType);
		string linkDescription = LocalizationManager.Instance.Get(achievementDefinition.DescriptionLocaKey.Key, new StringMapper { { "goal", value3 } });
		string arg = AchievementPostRankMappingSuffix[_rankIndex - 1];
		if (_achievementMapping.ranks.Length == 1)
		{
			arg = AchievementPostRankMappingSuffix[2];
		}
		string uriString = string.Format("{0}/achievement_{1}{2}.png", "http://static.cdn.ubi.com/0040/fb/", (uint)achievementDefinition.GameID.Guid, arg);
		FacebookHandlerBase.FBShareItemInfo fBShareItemInfo = new FacebookHandlerBase.FBShareItemInfo();
		fBShareItemInfo.linkName = linkName;
		fBShareItemInfo.linkDescription = linkDescription;
		fBShareItemInfo.linkCaption = LocalizationManager.Instance.Get(6737);
		fBShareItemInfo.picture = new Uri(uriString);
		fBShareItemInfo.reference = _achievementMapping.id;
		FacebookHandler.FBShareItem(fBShareItemInfo, (bool success, FacebookHandlerBase.EFBRequestType requestType, FacebookHandlerBase.EFBReturnCode resultCode) =>
		{
			if (!success)
			{
				CompleteShareItemFlow(_success: false, EFBShareItemFlowResult.FailedOrCancelled, _callback);
			}
			else
			{
				CompleteShareItemFlow(_success: true, EFBShareItemFlowResult.Succeeded, _callback);
			}
		});
	}

	public static void ResetProfile(FBLoginFlowCallback _callback = null)
	{
		FacebookHandler.FBLogout();
		if (PlayerPrefs.HasKey("FacebookUserId"))
		{
			PlayerPrefs.DeleteKey("FacebookUserId");
		}
		if (PlayerPrefs.HasKey("FacebookLoggedIn"))
		{
			PlayerPrefs.DeleteKey("FacebookLoggedIn");
		}
		if (PlayerPrefs.HasKey("FacebookUserName"))
		{
			PlayerPrefs.DeleteKey("FacebookUserName");
		}
		if (PlayerPrefs.HasKey("FacebookGameId"))
		{
			PlayerPrefs.DeleteKey("FacebookGameId");
		}
		UbiCore.Utils.Singleton<LocalStorage>.Instance.Clear();
		MailCache.Clear();
		if (!Singleton<StateManager>.Instance.ProfileManager.AbandonUserProfile())
		{
			PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
		}
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(new PopUpConfiguration
		{
			Data = new ErrorContent.ErrorData
			{
				Description = LocalizationManager.Instance.Get(5819)
			},
			Heading = LocalizationManager.Instance.Get(5817),
			Buttons = new PopUpButtonConfiguration[1]
			{
				new PopUpButtonConfiguration
				{
					Label = LocalizationManager.Instance.Get(1780),
					OnButtonPressed = (object _o) =>
					{
						HideLoadingOverlay();
						CompleteLoginFlow(_success: true, EFBLoginFlowResult.ResetAccount, _callback);
						UIUtil.QuitApplication();
					}
				}
			}
		});
	}

	private static void ProcessInvites(FBInviteFlowCallback _callback, InviteStatus _inviteStatus)
	{
		HideLoadingOverlay();
		IList<FacebookHandlerBase.FBFriendInfo> friends = _inviteStatus.Friends;
		if (_inviteStatus.AppFriends.Count + _inviteStatus.Invited.Count >= _inviteStatus.MaxFriends)
		{
			while (_inviteStatus.ProcessedIndex < friends.Count)
			{
				FacebookHandlerBase.FBFriendInfo item = friends[_inviteStatus.ProcessedIndex];
				_inviteStatus.ProcessedIndex++;
				_inviteStatus.FriendListFull.Add(item);
			}
		}
		else if (_inviteStatus.ProcessedIndex < friends.Count)
		{
			FacebookHandlerBase.FBFriendInfo friend = friends[_inviteStatus.ProcessedIndex];
			ShowLoadingInitOverlay();
			FacebookHandler.FBGetName(friend.Id, null, (string name) =>
			{
				friend.Name = name;
				CheckFriendStatus(friend, _inviteStatus, _callback, ProcessInvites);
			});
		}
		else
		{
			SummarizeInvites(_inviteStatus, _callback);
		}
	}

	private static void CheckFriendStatus(FacebookHandlerBase.FBFriendInfo _friend, InviteStatus _inviteStatus, FBInviteFlowCallback _callback, FBProcessInvitesCallback _processCallback)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.QueryFbStatusById(_friend.Id, ESocialNetwork.Facebook, _asyncResult);
		}, null, (IAsyncResult _response) =>
		{
			if (_response.IsError())
			{
				_inviteStatus.ProcessedIndex++;
				_inviteStatus.Failed.Add(_friend);
				_processCallback(_callback, _inviteStatus);
			}
			else
			{
				FBStatusByIdResponse fBStatusByIdResponse = ((AsyncDataResult<FBStatusByIdResponse>)_response).Result();
				EFBStatus status = fBStatusByIdResponse.status;
				UserProfile userProfile = fBStatusByIdResponse.userProfile;
				if (status != EFBStatus.Registered)
				{
					_inviteStatus.ProcessedIndex++;
					_inviteStatus.DontHaveGame.Add(_friend);
					_processCallback(_callback, _inviteStatus);
				}
				else
				{
					string userIdHash = userProfile.userIdHash;
					for (int i = 0; i < _inviteStatus.AppFriends.Count; i++)
					{
						FriendProfile friendProfile = _inviteStatus.AppFriends[i];
						if (friendProfile.userIdHash == userIdHash)
						{
							_inviteStatus.ProcessedIndex++;
							_inviteStatus.AlreadyFriends.Add(_friend);
							_processCallback(_callback, _inviteStatus);
							return;
						}
					}
					InviteFriend(_friend, _callback, _inviteStatus, userProfile, _processCallback);
				}
			}
		});
	}

	private static void InviteFriend(FacebookHandlerBase.FBFriendInfo _friend, FBInviteFlowCallback _callback, InviteStatus _inviteStatus, UserProfile _profile, FBProcessInvitesCallback _processCallback)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.SendFriendRequest(_profile.userIdHash, EModuleOrigin.Origin_Facebook, _asyncResult);
		}, null, (IAsyncResult _response) =>
		{
			if (_response.IsError())
			{
				_inviteStatus.ProcessedIndex++;
				_inviteStatus.Failed.Add(_friend);
				_processCallback(_callback, _inviteStatus);
			}
			else
			{
				((AsyncDataResult<SendFriendRequestResponse>)_response).Result();
				_inviteStatus.ProcessedIndex++;
				_inviteStatus.Invited.Add(_friend);
				_processCallback(_callback, _inviteStatus);
			}
		});
	}

	private static void SummarizeInvites(InviteStatus _inviteStatus, FBInviteFlowCallback _callback)
	{
		if (_inviteStatus.Friends.Count == 0)
		{
			CompleteInviteFlow(_success: true, EFBInviteFlowResult.Succeeded, _callback);
			return;
		}
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			HideLoadingOverlay();
			CompleteInviteFlow(_success: true, EFBInviteFlowResult.Succeeded, _callback);
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Top;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(5761);
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[1]
		{
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1780),
				ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					CompleteInviteFlow(_success: true, EFBInviteFlowResult.Succeeded, _callback);
				}
			}
		};
		popUpConfiguration.Data = new FacebookInviteContent.FacebookInviteData
		{
			FacebookName = Name,
			Status = _inviteStatus
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static void ProcessAppRequests()
	{
		FacebookHandler.FBGetAppRequests(null, (IList<FacebookHandlerBase.FBAppRequestInfo> requests) =>
		{
			if (requests != null)
			{
				for (int i = 0; i < requests.Count; i++)
				{
					FacebookHandlerBase.FBAppRequestInfo request = requests[i];
					FacebookHandler.FBDeleteAppRequest(request, (bool success, FacebookHandlerBase.EFBRequestType requestType, FacebookHandlerBase.EFBReturnCode returnCode) =>
					{
						if (!success)
						{
						}
					});
				}
			}
		});
	}

	private static void FetchFacebookName(FBLoginFlowCallback _callback)
	{
		Name = string.Empty;
		FacebookHandler.FBGetName("me", (bool success, FacebookHandlerBase.EFBRequestType requestType, FacebookHandlerBase.EFBReturnCode resultCode) =>
		{
			if (!success)
			{
				CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
				PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.FACEBOOK_LOGIN_FAILED, ErrorCategory.FACEBOOK);
				Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
			}
			else if (!IsProfileLinkedToFacebook)
			{
				OnBackKey = () =>
				{
					HideLoadingOverlay();
					SilentCancel(_callback);
				};
				FetchFacebookAccountStatus((bool registered, UserProfile userProfile) =>
				{
					if (!registered)
					{
						LinkCurrentProfileToFacebook(_callback);
					}
					else
					{
						bool flag = false;
						UserProfile profile = Singleton<StateManager>.Instance.ProfileManager.Profile;
						if (profile != null && userProfile != null && profile.id == userProfile.id)
						{
							flag = true;
						}
						if (flag)
						{
							LinkCurrentProfileToFacebook(_callback);
						}
						else if (WeiboManager.IsWeiboAvailable && WeiboManager.IsProfileLinkedToWeibo)
						{
							ShowLoadProfilePopup(userProfile, _callback);
						}
						else
						{
							ShowKeepCurrentAndLoadProfilePopup(userProfile, _callback);
						}
					}
				}, _callback);
			}
			else
			{
				FetchFacebookAccountStatus((bool registered, UserProfile userProfile) =>
				{
					if (LinkedFacebookUserId == FacebookHandler.UserId && userProfile != null && LinkedFacebookGameId == userProfile.userIdHash)
					{
						CompleteLoginFlow(_success: true, EFBLoginFlowResult.LoggedInSameAccount, _callback);
					}
					else if (registered)
					{
						RecoverProfile(_callback, (userProfile == null) ? string.Empty : userProfile.userIdHash);
					}
					else
					{
						PopUpConfiguration config = new PopUpConfiguration
						{
							ShowCloseButton = true,
							OnCloseButtonClicked = () =>
							{
								HideLoadingOverlay();
								SilentCancel(_callback);
							},
							Aligntarget = UIPopup.EContentAlignTarget.CloseButton,
							Heading = LocalizationManager.Instance.Get(5820),
							Buttons = new PopUpButtonConfiguration[2]
							{
								new PopUpButtonConfiguration
								{
									Label = LocalizationManager.Instance.Get(5825),
									OnButtonPressed = (object _o) =>
									{
										HideLoadingOverlay();
										ResetProfile(_callback);
									}
								},
								new PopUpButtonConfiguration
								{
									Label = LocalizationManager.Instance.Get(1781),
									OnButtonPressed = (object _o) =>
									{
										HideLoadingOverlay();
										SilentCancel(_callback);
									}
								}
							},
							Data = new ErrorContent.ErrorData
							{
								Description = LocalizationManager.Instance.Get(5824, new StringMapper { { "name", Name } })
							}
						};
						Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
					}
				}, _callback);
			}
		}, (string name) =>
		{
			Name = name;
		});
	}

	private static void FetchFacebookAccountStatus(FBAccountStatusCallback _registeredCallback, FBLoginFlowCallback _callback)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.QueryFbStatus(FacebookHandler.AccessToken, ESocialNetwork.Facebook, _asyncResult);
		}, new DefaultHandler(() =>
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
			PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
		}), (IAsyncResult _response) =>
		{
			if (!_response.IsError())
			{
				FBStatusResponse fBStatusResponse = ((AsyncDataResult<FBStatusResponse>)_response).Result();
				EFBStatus status = fBStatusResponse.status;
				UserProfile userProfile = fBStatusResponse.userProfile;
				bool registered = status == EFBStatus.Registered;
				if (_registeredCallback != null)
				{
					_registeredCallback(registered, userProfile);
				}
			}
		});
	}

	public static void QueryNewFbUserStatusById(string _facebookId, QueryFbStatusByIdCallback _newFbUserCallback)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.QueryFbStatusById(_facebookId, ESocialNetwork.Facebook, _asyncResult);
		}, null, (IAsyncResult _response) =>
		{
			if (!_response.IsError())
			{
				FBStatusByIdResponse fBStatusByIdResponse = ((AsyncDataResult<FBStatusByIdResponse>)_response).Result();
				EFBStatus status = fBStatusByIdResponse.status;
				UserProfile userProfile = fBStatusByIdResponse.userProfile;
				bool fbHasProfile = status == EFBStatus.Registered;
				_newFbUserCallback(fbHasProfile, userProfile);
			}
		});
	}

	private static void SilentCancel(FBLoginFlowCallback _callback)
	{
		CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
	}

	private static void LinkCurrentProfileToFacebook(FBLoginFlowCallback _callback)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.ModifyPlayerProfileName(null, ESocialNetwork.Facebook, FacebookHandler.AccessToken, EModuleOrigin.Origin_SocialScreen, _forceFacebookLinkToCurrent: false, _asyncResult);
		}, new DefaultHandler(() =>
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
			PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
		}), (IAsyncResult _response) =>
		{
			if (!_response.IsError())
			{
				PopUpConfiguration config = new PopUpConfiguration
				{
					ShowCloseButton = false,
					Aligntarget = UIPopup.EContentAlignTarget.Heading,
					Heading = LocalizationManager.Instance.Get(5826),
					Buttons = new PopUpButtonConfiguration[1]
					{
						new PopUpButtonConfiguration
						{
							Label = LocalizationManager.Instance.Get(1780),
							OnButtonPressed = (object _o) =>
							{
								HideLoadingOverlay();
								CompleteLoginFlow(_success: true, EFBLoginFlowResult.LinkedToNewProfile, _callback);
							}
						}
					},
					Data = new ErrorContent.ErrorData
					{
						Description = LocalizationManager.Instance.Get(5827, new StringMapper { { "name", Name } })
					}
				};
				Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
			}
		});
	}

	private static void WarnRecoverProfile(FBLoginFlowCallback _callback, string _requestedIdHash)
	{
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			HideLoadingOverlay();
			SilentCancel(_callback);
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.CloseButton;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(5828);
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[2]
		{
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1198),
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					RecoverProfile(_callback, _requestedIdHash);
				}
			},
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1781),
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					SilentCancel(_callback);
				}
			}
		};
		popUpConfiguration.Data = new ErrorContent.ErrorData
		{
			Description = LocalizationManager.Instance.Get(5829)
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static void WarnOverrideOldProfile(FBLoginFlowCallback _callback, string _requestedIdHash)
	{
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			HideLoadingOverlay();
			SilentCancel(_callback);
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.CloseButton;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(8021);
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[2]
		{
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1198),
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					OverrideOldProfile(_callback, _requestedIdHash);
				}
			},
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1781),
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					SilentCancel(_callback);
				}
			}
		};
		popUpConfiguration.Data = new ErrorContent.ErrorData
		{
			Description = LocalizationManager.Instance.Get(8022)
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static void RecoverProfile(FBLoginFlowCallback _callback, string _requestedIdHash)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.ModifyPlayerProfileName(null, ESocialNetwork.Facebook, FacebookHandler.AccessToken, EModuleOrigin.Origin_SocialScreen, _forceFacebookLinkToCurrent: false, _asyncResult);
		}, new DefaultHandler(() =>
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
			PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
		}), (IAsyncResult _response) =>
		{
			if (!_response.IsError())
			{
				if (Singleton<StateManager>.Instance.ProfileManager.Profile.userIdHash != _requestedIdHash)
				{
					CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
					PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
					Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
				}
				else
				{
					if (WeiboManager.IsWeiboAvailable && WeiboManager.IsProfileLinkedToWeibo)
					{
						WeiboManager.Logout((bool success, WeiboManager.EWeiboLogoutResult result) =>
						{
						});
					}
					Singleton<UIScreenManager>.Instance.ShowPopUpScreen(new PopUpConfiguration
					{
						Data = new ErrorContent.ErrorData
						{
							Description = LocalizationManager.Instance.Get(5831, new StringMapper { { "name", Name } })
						},
						Heading = LocalizationManager.Instance.Get(5830),
						Buttons = new PopUpButtonConfiguration[1]
						{
							new PopUpButtonConfiguration
							{
								Label = LocalizationManager.Instance.Get(1780),
								OnButtonPressed = (object _o) =>
								{
									HideLoadingOverlay();
									CompleteLoginFlow(_success: true, EFBLoginFlowResult.RecoveredAccount, _callback);
									UIUtil.QuitApplication();
								}
							}
						}
					});
				}
			}
		});
	}

	private static void OverrideOldProfile(FBLoginFlowCallback _callback, string _requestedIdHash)
	{
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.ModifyPlayerProfileName(null, ESocialNetwork.Facebook, FacebookHandler.AccessToken, EModuleOrigin.Origin_SocialScreen, _forceFacebookLinkToCurrent: true, _asyncResult);
		}, new DefaultHandler(() =>
		{
			CompleteLoginFlow(_success: false, EFBLoginFlowResult.FailedOrCancelled, _callback);
			PopUpConfiguration defaultPopUpConfiguration = UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.UnknownCode, ErrorCategory.SERVER_USERPROFILE);
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(defaultPopUpConfiguration);
		}), (IAsyncResult _response) =>
		{
			if (!_response.IsError())
			{
				PopUpConfiguration config = new PopUpConfiguration
				{
					ShowCloseButton = false,
					Aligntarget = UIPopup.EContentAlignTarget.Heading,
					Heading = LocalizationManager.Instance.Get(5826),
					Buttons = new PopUpButtonConfiguration[1]
					{
						new PopUpButtonConfiguration
						{
							Label = LocalizationManager.Instance.Get(1780),
							OnButtonPressed = (object _o) =>
							{
								HideLoadingOverlay();
								CompleteLoginFlow(_success: true, EFBLoginFlowResult.OldProfileOverriden, _callback);
							}
						}
					},
					Data = new ErrorContent.ErrorData
					{
						Description = LocalizationManager.Instance.Get(5827, new StringMapper { { "name", Name } })
					}
				};
				Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
			}
		});
	}

	private static void CompleteLoginFlow(bool _success, EFBLoginFlowResult _result, FBLoginFlowCallback _callback)
	{
		HideLoadingOverlay();
		if (_success && FacebookHandler.IsLoggedIn)
		{
			PlayerPrefs.SetString("FacebookUserId", FacebookHandler.UserId);
			PlayerPrefs.SetString("FacebookUserName", Name);
			UserProfile profile = Singleton<StateManager>.Instance.ProfileManager.Profile;
			if (profile != null)
			{
				PlayerPrefs.SetString("FacebookGameId", profile.userIdHash);
			}
			else
			{
				PlayerPrefs.DeleteKey("FacebookGameId");
			}
			PlayerPrefs.SetInt("FacebookLoggedIn", 1);
		}
		else
		{
			if (FacebookHandler.IsLoggedIn)
			{
				FacebookHandler.FBLogout();
			}
			PlayerPrefs.SetInt("FacebookLoggedIn", 0);
		}
		if (_success && _result != EFBLoginFlowResult.ResetAccount && _result != EFBLoginFlowResult.AlreadyLoggedIn && _result != EFBLoginFlowResult.RecoveredAccount && _result != EFBLoginFlowResult.OldProfileOverriden)
		{
			ProcessAppRequests();
		}
		_callback?.Invoke(_success, _result);
	}

	private static void CompleteLogoutFlow(bool _success, EFBLogoutFlowResult _result, FBLogoutFlowCallback _callback)
	{
		HideLoadingOverlay();
		PlayerPrefs.SetInt("FacebookLoggedIn", 0);
		Name = string.Empty;
		_callback?.Invoke(_success, _result);
	}

	private static void CompleteInviteFlow(bool _success, EFBInviteFlowResult _result, FBInviteFlowCallback _callback)
	{
		HideLoadingOverlay();
		_callback?.Invoke(_success, _result);
	}

	private static void CompleteShareItemFlow(bool _success, EFBShareItemFlowResult _result, FBShareItemFlowCallback _callback)
	{
		HideLoadingOverlay();
		_callback?.Invoke(_success, _result);
	}

	private static IEnumerator TryInit()
	{
		while (!FacebookHandler.IsInitialized)
		{
			FacebookHandler.FBInit((bool success, FacebookHandlerBase.EFBRequestType requestType, FacebookHandlerBase.EFBReturnCode returnCode) =>
			{
				if (success)
				{
					RegisterAppInstall();
				}
			});
			yield return new WaitForSeconds((Application.internetReachability != NetworkReachability.NotReachable) ? 2 : 10);
		}
	}

	private static void RegisterAppInstall()
	{
		if (PlayerPrefs.GetInt("FacebookRegistered", 0) != 1)
		{
			if (NativeTools.BundleIdentifier != "com.ubisoft.assassinscreed.identity" || NativeTools.IsShipping())
			{
			}
			PlayerPrefs.SetInt("FacebookRegistered", 1);
		}
	}

	private static void ShowLoadProfilePopup(UserProfile userProfile, FBLoginFlowCallback _callback)
	{
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			HideLoadingOverlay();
			SilentCancel(_callback);
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Top;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(5820);
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[2]
		{
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(1781),
				ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					LogoutSilently();
					SilentCancel(_callback);
				}
			},
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(5823),
				ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					RecoverProfile(_callback, (userProfile == null) ? string.Empty : userProfile.userIdHash);
				}
			}
		};
		popUpConfiguration.Data = new ThirdPartyAccountContent.ThirdPartyAccountData
		{
			ShowOnlyLoadProfile = true,
			ThirdPartyType = ThirdPartyAccountContent.ThirdPartyAccountData.EThirdPartyType.Facebook,
			ThirdPartyName = Name,
			CurrentProfile = Singleton<StateManager>.Instance.ProfileManager.Profile,
			ThirdPartyProfile = userProfile
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static void ShowKeepCurrentAndLoadProfilePopup(UserProfile userProfile, FBLoginFlowCallback _callback)
	{
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			HideLoadingOverlay();
			SilentCancel(_callback);
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Top;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(5820);
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[2]
		{
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(8020),
				ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					WarnOverrideOldProfile(_callback, (userProfile == null) ? string.Empty : userProfile.userIdHash);
				}
			},
			new PopUpButtonConfiguration
			{
				Label = LocalizationManager.Instance.Get(5823),
				ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
				OnButtonPressed = (object _o) =>
				{
					HideLoadingOverlay();
					WarnRecoverProfile(_callback, (userProfile == null) ? string.Empty : userProfile.userIdHash);
				}
			}
		};
		popUpConfiguration.Data = new ThirdPartyAccountContent.ThirdPartyAccountData
		{
			ThirdPartyType = ThirdPartyAccountContent.ThirdPartyAccountData.EThirdPartyType.Facebook,
			ThirdPartyName = Name,
			CurrentProfile = Singleton<StateManager>.Instance.ProfileManager.Profile,
			ThirdPartyProfile = userProfile
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static void ShowFacebookUnavailableError()
	{
		string label = LocalizationManager.Instance.Get(1780);
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(new PopUpConfiguration
		{
			Heading = LocalizationManager.Instance.Get(5832),
			Data = new ErrorContent.ErrorData
			{
				Description = LocalizationManager.Instance.Get(5833)
			},
			Buttons = new PopUpButtonConfiguration[1]
			{
				new PopUpButtonConfiguration
				{
					Label = label,
					OnButtonPressed = (object _o) =>
					{
						HideLoadingOverlay();
					}
				}
			}
		});
	}

	private static void ShowFacebookNotInitializedError()
	{
		string label = LocalizationManager.Instance.Get(1780);
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(new PopUpConfiguration
		{
			Heading = LocalizationManager.Instance.Get(5834),
			Data = new ErrorContent.ErrorData
			{
				Description = LocalizationManager.Instance.Get(5835)
			},
			Buttons = new PopUpButtonConfiguration[1]
			{
				new PopUpButtonConfiguration
				{
					Label = label,
					OnButtonPressed = (object _o) =>
					{
						HideLoadingOverlay();
					}
				}
			}
		});
	}

	private static void ShowFacebookNotLoggedInError()
	{
		string label = LocalizationManager.Instance.Get(1780);
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(new PopUpConfiguration
		{
			Heading = LocalizationManager.Instance.Get(6431),
			Data = new ErrorContent.ErrorData
			{
				Description = LocalizationManager.Instance.Get(6432)
			},
			Buttons = new PopUpButtonConfiguration[1]
			{
				new PopUpButtonConfiguration
				{
					Label = label,
					OnButtonPressed = (object _o) =>
					{
						HideLoadingOverlay();
					}
				}
			}
		});
	}

	public static void ShowLoadingInitOverlay()
	{
		Singleton<UIScreenManager>.Instance.ShowLoadingOverlay(LoadingOverlayConfiguration.Default);
	}

	public static void HideLoadingOverlay()
	{
		OnBackKey = null;
		Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
	}

	public static void LogPurchase(float rawPrice, string currency = "USD", Dictionary<string, object> receiptDict = null)
	{
		if (NativeTools.IsShipping() && IsInitialized)
		{
		}
	}
}
