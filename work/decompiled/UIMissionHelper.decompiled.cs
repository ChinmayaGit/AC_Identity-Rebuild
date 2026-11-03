using System;
using System.Collections;
using System.Collections.Generic;
using AcierProtocol;
using AssetBundleData;
using Assets.Scripts.Audio;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.HUD;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network;
using Assets.Scripts.Network.Error;
using Assets.Scripts.UI.Behaviours.Buttons;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.Network;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Screens.Popup;
using Assets.Scripts.UI.Screens.Popup.AppRatingPopup;
using Assets.Scripts.UI.Screens.Popup.CallToActionPopup;
using Assets.Scripts.UI.Screens.Popup.ErrorPopup;
using Assets.Scripts.UI.Screens.Popup.FriendHirelingPopup;
using Assets.Scripts.UI.Screens.Popup.TutorialMissionPopup;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.State.MissionManager;
using Assets.Scripts.UI.State.ProfileManager;
using Assets.Scripts.UI.Types;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using Mission;
using SharedBaseLib.Core;
using UbiCore.Utils;
using UnityEngine;

namespace Assets.Scripts.UI.Helpers;

public static class UIMissionHelper
{
	public static event Action<bool> OnMissionRequested;

	public static void RetryMission()
	{
		Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		if (Assets.Scripts.Engine.Helpers.Verify(Main.Instance.IsNetworkEnabled, "No datastore - working offline?!"))
		{
			UIScreen screen = Singleton<UIScreenManager>.Instance.GetScreen(EScreenID.OptionsMenu);
			OptionsMenuNavigation componentInChildren = screen.GetComponentInChildren<OptionsMenuNavigation>();
			if (componentInChildren != null)
			{
				componentInChildren.PreviousPauseState = Timer.EPauseState.Running;
			}
			UbiCore.Utils.Singleton<Timer>.Instance.Pause(Timer.EPauseState.Running);
			string missionId = Singleton<StateManager>.Instance.MissionSelectionManager.MissionId;
			UIAssasinId hirelingId = Singleton<StateManager>.Instance.ProfileManager.LastSelectedHirelingId;
			UIAssasinId assassinId = Singleton<StateManager>.Instance.ProfileManager.CurrentAssassinId;
			string campaignId = Singleton<StateManager>.Instance.MissionSelectionManager.CurrentSelectedMissionCampaignId;
			int rank = Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank;
			AsyncCallback callback = OnRetryMissionCallback;
			UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncCallback) =>
			{
				Singleton<StateManager>.Instance.ProfileManager.OpenMission(missionId, assassinId, hirelingId, campaignId, rank, _isRetry: true, _asyncCallback);
			}, null, callback);
		}
		else
		{
			UIMissionResult subSystemByType = UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIMissionResult>();
			subSystemByType.SetVisible(_value: false);
			Main.Instance.RetryCurrentMission();
		}
	}

	public static void PlayNextMission()
	{
		if (!Singleton<StateManager>.Instance.ProfileManager.Profile.CanReciveMissionRewards())
		{
			ShowInventoryAndMailIsFullPopup(OnPlayNextMission);
		}
		else
		{
			OnPlayNextMission();
		}
	}

	public static void GoToAnimusNoNetwork()
	{
		if (EveryplayManager.IsRecording)
		{
			EveryplayManager.StopRecording(_showSharingDialog: false);
		}
		Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		Singleton<UIScreenManager>.Instance.PurgeManager();
		Singleton<StateManager>.Instance.ProfileManager.EnableUpdates = true;
		Main.Instance.LoadMission(BuildDefinition.AnimusMission, null, _eliteMission: false);
	}

	public static void GoToAnimus()
	{
		LogicMain.MissionResult.ResetConsumedItems();
		if (EveryplayManager.IsRecording)
		{
			EveryplayManager.StopRecording(_showSharingDialog: false);
		}
		if (Assets.Scripts.Engine.Helpers.Verify(Main.Instance.IsNetworkEnabled, "No datastore - working offline?!"))
		{
			string campaignId = Singleton<StateManager>.Instance.MissionSelectionManager.CurrentSelectedMissionCampaignId;
			string missionId = Singleton<StateManager>.Instance.MissionSelectionManager.MissionId;
			UIAssasinId assassinId = Singleton<StateManager>.Instance.ProfileManager.CurrentAssassinId;
			int rank = Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank;
			UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
			{
				Singleton<StateManager>.Instance.ProfileManager.RemoveHirelingAndMission(campaignId, (!UbiCore.Utils.Singleton<UiMain>.Instance.HasSucceededMissionOnce) ? null : missionId, assassinId, rank, _asyncResult);
			}, null, OnHirelingRemovedGoToAnimus);
		}
		else
		{
			Singleton<UIScreenManager>.Instance.PurgeManager();
			Singleton<StateManager>.Instance.ProfileManager.EnableUpdates = true;
			Main.Instance.LoadMission(BuildDefinition.AnimusMission, null, _eliteMission: false);
		}
	}

	public static void StartCurrentSelectedMissionWithTutorialPopup()
	{
		if (UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.IsTutorialCategoryActive(ETutorialCategory.TutCatMs26Hirelings))
		{
			return;
		}
		bool flag = Singleton<StateManager>.Instance.ProfileManager.Profile.CanReciveMissionRewards();
		if (Singleton<StateManager>.Instance.MissionSelectionManager.IsSelectedMissionCampaign)
		{
			if (flag)
			{
				OnStartCurrentSelectedMissionWithTutorialPopup();
			}
			else
			{
				ShowInventoryAndMailIsFullPopup(OnStartCurrentSelectedMissionWithTutorialPopup);
			}
		}
		else if (flag)
		{
			StartCurrentSelectedMission();
		}
		else
		{
			ShowInventoryAndMailIsFullPopup(StartCurrentSelectedMission);
		}
	}

	public static void StartCurrentSelectedMission()
	{
		Main.Instance.StartCoroutine(AskForDownloadAndProceed());
	}

	private static void ShowStartTutorialMissionPopup(AcierProtocol.MissionInfo _mission, bool _isCampaignMission)
	{
		TutorialEntitlementDescription tutorialDesc = null;
		if (_isCampaignMission)
		{
			tutorialDesc = _mission.GetTutorialDescription();
		}
		else
		{
			for (int i = 0; i < UbiCore.Utils.Singleton<UIDataMapper>.Instance.TutorialEntitlementDescription.Count; i++)
			{
				TutorialEntitlementDescription tutorialEntitlementDescription = UbiCore.Utils.Singleton<UIDataMapper>.Instance.TutorialEntitlementDescription[i];
				EntitlementDefinition entitlementByGUID = EntitlementExtensions.GetEntitlementByGUID(tutorialEntitlementDescription.EntitlementGUID);
				if (entitlementByGUID != null && Singleton<StateManager>.Instance.ProfileManager.EntitlementManager.HasEntitlements(entitlementByGUID.RequiredEntitlements) && !UbiCore.Utils.Singleton<LocalStorage>.Instance.WasEntitlementShown(tutorialEntitlementDescription.EntitlementGUID.Guid.ToString()))
				{
					tutorialDesc = tutorialEntitlementDescription;
					break;
				}
			}
		}
		string guidId = ((tutorialDesc == null) ? string.Empty : tutorialDesc.EntitlementGUID.Guid.ToString());
		Singleton<StateManager>.Instance.MissionSelectionManager.ClearMissionAfterTutorial();
		if (tutorialDesc != null && _mission != null && tutorialDesc.MissionId == _mission.baseName)
		{
			UbiCore.Utils.Singleton<LocalStorage>.Instance.SetEntitlementShown(guidId);
		}
		if (tutorialDesc == null || UbiCore.Utils.Singleton<LocalStorage>.Instance.WasEntitlementShown(guidId))
		{
			StartCurrentSelectedMission();
			return;
		}
		AcierProtocol.MissionInfo tutorialMission = Singleton<StateManager>.Instance.TemplateManager.CampaignCatalog.GetMissionById(tutorialDesc.MissionId);
		PopUpButtonConfiguration[] buttons = new PopUpButtonConfiguration[2]
		{
			new PopUpButtonConfiguration
			{
				ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
				Label = LocalizationManager.Instance.Get(new StringId(7189)),
				OnButtonPressed = (object _) =>
				{
					Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
					UbiCore.Utils.Singleton<LocalStorage>.Instance.SetEntitlementShown(guidId);
					StartCurrentSelectedMission();
				}
			},
			new PopUpButtonConfiguration
			{
				ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
				Label = LocalizationManager.Instance.Get(new StringId(2245)),
				OnButtonPressed = (object _) =>
				{
					Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
					UbiCore.Utils.Singleton<LocalStorage>.Instance.SetEntitlementShown(guidId);
					if (_isCampaignMission)
					{
						Singleton<StateManager>.Instance.MissionSelectionManager.SetCampaignMissionTutorialMisison(tutorialMission);
					}
					else
					{
						Singleton<StateManager>.Instance.MissionSelectionManager.SetContractMissionTutorialMisison(tutorialMission);
					}
					StartCurrentSelectedMission();
				}
			}
		};
		EntitlementDefinition entitlementByGUID2 = EntitlementExtensions.GetEntitlementByGUID(tutorialDesc.EntitlementGUID);
		string text = LocalizationManager.Instance.Get(entitlementByGUID2.NameLocaKey.Key);
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Top;
		popUpConfiguration.Heading = tutorialMission.LocaName();
		popUpConfiguration.Buttons = buttons;
		popUpConfiguration.ShowCloseButton = false;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		};
		popUpConfiguration.Data = new TutorialMissionPopUpContent.TutorialMissionPopUpData
		{
			MissionName = tutorialMission.baseName,
			Description = LocalizationManager.Initialize().Get(6736, new StringMapper { 
			{
				"unlocked_feature_name",
				new UINGUIFormattedText(text)
			} })
		};
		PopUpConfiguration config = popUpConfiguration;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}

	private static IEnumerator AskForDownloadAndProceed()
	{
		BBList<AssetBundleInfo> list = UIDownloadScreen.Instance.GetSceneBundles(Singleton<StateManager>.Instance.MissionSelectionManager.CurrentMissionLocationSet);
		if (list == null)
		{
			SendOpenMissionDataAsync();
			yield break;
		}
		bool abort = false;
		bool wait = true;
		PopUpButtonConfiguration.ButtonPressHandler handler = (object data) =>
		{
			wait = false;
			abort = !(bool)data;
			if (abort)
			{
				PrepareCampaignMissionAfterTutorial();
			}
			Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		};
		long totalSize = 0L;
		for (int i = 0; i < list.Count; i++)
		{
			totalSize += list[i].AssetBundleFileSize;
		}
		int sizeMiB = (int)((float)totalSize / 1000000f + 0.5f);
		string description = LocalizationManager.Instance.Get(5644, new StringMapper { { "size", sizeMiB } });
		ScreenExtensions.ShowPopUpScreen(_config: new PopUpConfiguration
		{
			Heading = LocalizationManager.Instance.Get(5643),
			Aligntarget = UIPopup.EContentAlignTarget.Heading,
			ShowCloseButton = false,
			Data = new ErrorContent.ErrorData
			{
				Description = description
			},
			Buttons = new PopUpButtonConfiguration[2]
			{
				new PopUpButtonConfiguration
				{
					Data = false,
					Label = LocalizationManager.Instance.Get(1781),
					OnButtonPressed = handler
				},
				new PopUpButtonConfiguration
				{
					Data = true,
					Label = LocalizationManager.Instance.Get(1780),
					OnButtonPressed = handler
				}
			}
		}, _manager: Singleton<UIScreenManager>.Instance);
		while (wait)
		{
			yield return 0;
		}
		if (!abort)
		{
			WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
			if (!Main.Instance.IsNetworkEnabled)
			{
				ScreenExtensions.ShowPopUpScreen(_config: UIErrorHelper.GetDefaultPopUpConfiguration(ErrorCode.NoNetworkConnection, ErrorCategory.Reachability), _manager: Singleton<UIScreenManager>.Instance);
				yield return endOfFrame;
			}
			UIDownloadScreen.Instance.Open();
			while (!UIDownloadScreen.Instance.IsSceneLoaded || !UIDownloadScreen.Instance.Details.FullyVisible)
			{
				yield return endOfFrame;
			}
			if (retinaProAtlasController.sharedInstance != null)
			{
				retinaProAtlasController.sharedInstance.refreshAll();
			}
			while (!UIDownloadScreen.Instance.IsInitialized)
			{
				yield return endOfFrame;
			}
			Main.Instance.StartCoroutine(UIDownloadScreen.Instance.DownloadData(list, () =>
			{
				UIDownloadScreen.Instance.Close(_delayed: false);
				SendOpenMissionDataAsync();
			}));
		}
		else if (!Main.Instance.SceneLoader.InAnimus)
		{
			GoToAnimus();
		}
	}

	private static bool PrepareCampaignMissionAfterTutorial()
	{
		if (!Singleton<StateManager>.Instance.MissionSelectionManager.HaveMissionAfterTutorial)
		{
			return false;
		}
		AcierProtocol.MissionInfo campaignMissionAfterTutorial = Singleton<StateManager>.Instance.MissionSelectionManager.CampaignMissionAfterTutorial;
		if (campaignMissionAfterTutorial != null)
		{
			Singleton<StateManager>.Instance.MissionSelectionManager.CampaignMission = campaignMissionAfterTutorial;
		}
		else if (Singleton<StateManager>.Instance.MissionSelectionManager.ContractMissionAfterTutorial != null)
		{
			Singleton<StateManager>.Instance.MissionSelectionManager.ContractMission = Singleton<StateManager>.Instance.MissionSelectionManager.ContractMissionAfterTutorial;
		}
		Singleton<StateManager>.Instance.MissionSelectionManager.ClearMissionAfterTutorial();
		return true;
	}

	private static void InvokeMissionRequested(bool _failureOccured)
	{
		OnMissionRequested?.Invoke(_failureOccured);
	}

	private static void SendOpenMissionDataAsync()
	{
		InvokeMissionRequested(_failureOccured: false);
		if (!Main.Instance.BuildDefinition.HasUsedDebugAvatarSelection)
		{
			Main.Instance.BuildDefinition.AvatarFixed = false;
		}
		string missionId = Singleton<StateManager>.Instance.MissionSelectionManager.MissionId;
		UIAssasinId hirelingId = Singleton<StateManager>.Instance.ProfileManager.LastSelectedHirelingId;
		UIAssasinId assassinId = Singleton<StateManager>.Instance.ProfileManager.CurrentAssassinId;
		string campaignId = Singleton<StateManager>.Instance.MissionSelectionManager.CurrentSelectedMissionCampaignId;
		int rank = Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank;
		AsyncCallback callback = (IAsyncResult _asyncResult) =>
		{
			if (_asyncResult.IsError())
			{
				InvokeMissionRequested(_failureOccured: true);
				if (Main.Instance.SceneLoader.InAnimus)
				{
					Singleton<UIScreenManager>.Instance.ValidateMissionListOnError();
				}
			}
			else
			{
				if (Main.Instance.SceneLoader.InAnimus)
				{
					Singleton<StateManager>.Instance.ProfileManager.SaveLastProfileDateBeforeMission();
				}
				MissionToken missionToken = ((IAsyncDataResult<OpenMissionResponse>)_asyncResult).Result().missionToken;
				Singleton<StateManager>.Instance.ProfileManager.EnableUpdates = false;
				Main.Instance.LoadMission(missionToken.missionFile, missionToken.missionGoals, missionToken.eliteMission);
			}
		};
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncCallback) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.OpenMission(missionId, assassinId, hirelingId, campaignId, rank, _isRetry: false, _asyncCallback);
		}, null, callback);
	}

	private static void OnRetryMissionCallback(IAsyncResult _asyncResult)
	{
		if (!_asyncResult.IsError())
		{
			AsyncDataResult<OpenMissionResponse> asyncDataResult = (AsyncDataResult<OpenMissionResponse>)_asyncResult;
			try
			{
				OpenMissionResponse response = asyncDataResult.Result();
				Singleton<StateManager>.Instance.ProfileManager.HandleOpenMissionResponse(response);
			}
			catch (AcierErrorException)
			{
			}
			UIMissionResult subSystemByType = UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIMissionResult>();
			subSystemByType.SetVisible(_value: false);
			Main.Instance.RetryCurrentMission();
		}
	}

	private static void OnHirelingRemovedGoToAnimus(IAsyncResult _asyncResult)
	{
		if (!_asyncResult.IsError())
		{
			UIMissionResult subSystemByType = UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIMissionResult>();
			subSystemByType.Exclude();
			Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
			Singleton<UIScreenManager>.Instance.PurgeManager();
			Singleton<StateManager>.Instance.ProfileManager.EnableUpdates = true;
			Main.Instance.LoadMission(BuildDefinition.AnimusMission, null, _eliteMission: false);
		}
	}

	private static void StartNextContractMission()
	{
		ProfileManager profileManager = Singleton<StateManager>.Instance.ProfileManager;
		if (!AppRatingPopup.LastMissionWasSuccess)
		{
			AppRatingPopup.LastMissionWasSuccess = UbiCore.Utils.Singleton<MissionManager>.Instance.MissionState == EMissionState.Success;
		}
		CallToActionPopup.RecordMissionData();
		FriendHirelingPopup.ResetMissionData();
		string campaignId = Singleton<StateManager>.Instance.MissionSelectionManager.CurrentSelectedMissionCampaignId;
		UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncResult) =>
		{
			profileManager.RemoveHirelingAndMission(campaignId, (!UbiCore.Utils.Singleton<UiMain>.Instance.HasSucceededMissionOnce) ? null : Singleton<StateManager>.Instance.MissionSelectionManager.MissionId, profileManager.CurrentAssassinId, Singleton<StateManager>.Instance.MissionSelectionManager.HeroicMissionRank, _asyncResult);
		}, null, (IAsyncResult _ar) =>
		{
			if (!_ar.IsError())
			{
				OnRemovePrepareNextMission();
			}
		});
	}

	private static void OnRemovePrepareNextMission()
	{
		string missionId = Singleton<StateManager>.Instance.MissionSelectionManager.MissionId;
		ProfileManager profileManager = Singleton<StateManager>.Instance.ProfileManager;
		AcierProtocol.Mission[] missions = profileManager.CurrrentRankMissions.missions;
		int num = (int)(UnityEngine.Random.value * (float)missions.Length) + 1;
		AcierProtocol.Mission selectedMission = null;
		for (int i = 0; i < missions.Length - 1; i++)
		{
			AcierProtocol.Mission mission = missions[(num + i) % missions.Length];
			if (mission.id != missionId)
			{
				if (!Main.Instance.BuildDefinition.HasUsedDebugAvatarSelection)
				{
					Main.Instance.BuildDefinition.AvatarFixed = false;
				}
				selectedMission = mission;
				break;
			}
		}
		if (selectedMission != null)
		{
			string missionId2 = selectedMission.id;
			AcierProtocol.Mission contractMission = Singleton<StateManager>.Instance.MissionSelectionManager.ContractMission;
			AsyncCallback asyncCallback = OnOpenNextMissionCallback;
			if (contractMission.locationSet.region != selectedMission.locationSet.region || contractMission.locationSet.mood != selectedMission.locationSet.mood || contractMission.locationSet.era != selectedMission.locationSet.era)
			{
				asyncCallback = OnOpenLoadMissionCallback;
			}
			Singleton<StateManager>.Instance.MissionSelectionManager.ContractMission = selectedMission;
			if (profileManager.Hirelings != null && profileManager.Hirelings.Length > 0)
			{
				profileManager.LastSelectedHirelingId = profileManager.Hirelings[(int)(UnityEngine.Random.value * (float)profileManager.Hirelings.Length)].hirelingId;
			}
			UIAssasinId assassinId = profileManager.CurrentAssassinId;
			profileManager.EnableUpdates = false;
			string campaignId = string.Empty;
			AsyncCallback callback = asyncCallback;
			UINetworkAdaptor.BlockedNetworkRequest((AsyncCallback _asyncCallback) =>
			{
				profileManager.OpenMission(missionId2, assassinId, profileManager.LastSelectedHirelingId, campaignId, selectedMission.rank, _isRetry: false, _asyncCallback);
			}, null, callback);
		}
		else
		{
			profileManager.LastSelectedHirelingId = new UIAssasinId(string.Empty);
			GoToAnimus();
		}
	}

	private static void OnOpenLoadMissionCallback(IAsyncResult _ar)
	{
		if (MissionResponsePrepare(_ar))
		{
			UIMissionResult subSystemByType = UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIMissionResult>();
			subSystemByType.Exclude();
			OpenMissionResponse openMissionResponse = ((AsyncDataResult<OpenMissionResponse>)_ar).Result();
			Main.Instance.LoadMission(openMissionResponse.missionToken.missionFile, openMissionResponse.missionToken.missionGoals, openMissionResponse.missionToken.eliteMission);
		}
	}

	private static void OnOpenNextMissionCallback(IAsyncResult _ar)
	{
		if (MissionResponsePrepare(_ar))
		{
			UIMissionResult subSystemByType = UbiCore.Utils.Singleton<UiMain>.Instance.GetSubSystemByType<UIMissionResult>();
			subSystemByType.Exclude();
			OpenMissionResponse openMissionResponse = ((AsyncDataResult<OpenMissionResponse>)_ar).Result();
			Main.Instance.FastLoadMission(openMissionResponse.missionToken.missionFile, openMissionResponse.missionToken.missionGoals, openMissionResponse.missionToken.eliteMission);
		}
	}

	private static void OnPlayNextMission()
	{
		ProfileManager profileManager = Singleton<StateManager>.Instance.ProfileManager;
		profileManager.EnableUpdates = true;
		if (PrepareCampaignMissionAfterTutorial())
		{
			StartCurrentSelectedMission();
		}
		else
		{
			StartNextContractMission();
		}
	}

	public static void OnStartCurrentSelectedMissionWithTutorialPopup()
	{
		MissionSelectionManager missionSelectionManager = Singleton<StateManager>.Instance.MissionSelectionManager;
		ShowStartTutorialMissionPopup(missionSelectionManager.CampaignMission, missionSelectionManager.IsSelectedMissionCampaign);
	}

	private static bool MissionResponsePrepare(IAsyncResult _ar)
	{
		if (_ar.IsError())
		{
			return false;
		}
		ProfileManager profileManager = Singleton<StateManager>.Instance.ProfileManager;
		AsyncDataResult<OpenMissionResponse> asyncDataResult = (AsyncDataResult<OpenMissionResponse>)_ar;
		OpenMissionResponse openMissionResponse = asyncDataResult.Result();
		UbiCore.Utils.Singleton<UiMain>.Instance.OnEnterNextMission();
		profileManager.MissionTicket = openMissionResponse.missionToken.token;
		UbiCore.Utils.Singleton<AvatarData>.Instance.PrepareMission(profileManager.Profile.GetAssassin(openMissionResponse.missionToken.assassinId), profileManager.GetHireling(openMissionResponse.missionToken.hirelingId), openMissionResponse.missionToken.module);
		return true;
	}

	private static void ShowInventoryAndMailIsFullPopup(Action _onContinueAction)
	{
		List<PopUpButtonConfiguration> list = new List<PopUpButtonConfiguration>();
		list.Add(new PopUpButtonConfiguration
		{
			ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
			Label = LocalizationManager.Instance.Get(3904),
			OnButtonPressed = (object _x) =>
			{
				Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
			},
			SoundIntent = EButtonIntent.Cancel
		});
		list.Add(new PopUpButtonConfiguration
		{
			ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
			Label = LocalizationManager.Instance.Get(5677),
			SoundIntent = EButtonIntent.Accept,
			OnButtonPressed = (object _data) =>
			{
				Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
				if (_onContinueAction != null)
				{
					_onContinueAction();
				}
			}
		});
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.ShowCloseButton = true;
		popUpConfiguration.OnCloseButtonClicked = () =>
		{
			Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		};
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.CloseButton;
		popUpConfiguration.Heading = LocalizationManager.Instance.Get(7801);
		popUpConfiguration.Buttons = list.ToArray();
		popUpConfiguration.Data = new ErrorContent.ErrorData
		{
			Description = LocalizationManager.Instance.Get(7802)
		};
		PopUpConfiguration config = popUpConfiguration;
		AudioManager.Instance.Play(UbiCore.Utils.Singleton<AudioGameIds>.Instance.ac_sfx_ui_prompt_general_info);
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}
}
