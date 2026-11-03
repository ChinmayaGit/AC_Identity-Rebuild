using System;
using System.Collections.Generic;
using AcierProtocol;
using Assets.Scripts.Audio;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission.HUD;
using Assets.Scripts.Mission.UI.Combat;
using Assets.Scripts.Mission.UI.LogicInterface;
using Assets.Scripts.Network;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Helpers;
using Assets.Scripts.UI.Helpers.Shop;
using Assets.Scripts.UI.Ingame;
using Assets.Scripts.UI.Ingame.Helpers;
using Assets.Scripts.UI.Ingame.Implementation;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.NGUIExtensions.Tweens;
using Assets.Scripts.UI.Network;
using Assets.Scripts.UI.Progression;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Screens.NewsFeedScreen.Data;
using Assets.Scripts.UI.Screens.Popup;
using Assets.Scripts.UI.Screens.Popup.CallToActionPopup;
using Assets.Scripts.UI.Screens.Popup.ErrorPopup;
using Assets.Scripts.UI.Screens.Popup.FriendHirelingPopup;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.State.EventManager;
using Assets.Scripts.UI.State.ThemeManager;
using Assets.Scripts.UI.Tutorial;
using Assets.Scripts.UI.Types;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using Mission;
using SharedBaseLib.Core;
using SharedBaseLib.System;
using UbiCore.Debug;
using UbiCore.Messenger;
using UbiCore.Utils;
using UbiCore.Utils.Extensions;
using UnityEngine;

namespace Assets.Scripts.Mission.UI;

public class UiMain : UbiCore.Utils.Singleton<UiMain>
{
	private static readonly Vector2 s_deviceResolution = new Vector2(2048f, 1536f);

	private static bool s_uiAlignmentRightHanded = false;

	private bool m_callUnload;

	private bool m_callUnloaded;

	private bool m_uiSharedRootScreenLoading;

	private AsyncOperation m_loadeSharedUIOperation;

	private BBList<IUiSubSystem> m_subSystems = new BBList<IUiSubSystem>();

	private BBList<IUiSubSystem> m_newSubSystems = new BBList<IUiSubSystem>();

	private UIRoot m_ingameUIRoot;

	private readonly UIRevivePlayerPopupHelper m_playerPopupHelper = new UIRevivePlayerPopupHelper();

	private readonly UIBuyHelper m_buyHelper = new UIBuyHelper();

	private readonly UIDirectPurchasesShopHelper m_directPurchasesShopHelper = new UIDirectPurchasesShopHelper();

	public bool ShowMissionSelectionAnimationAfterTutorial;

	public static bool UIAlignmentRightHanded
	{
		get
		{
			return s_uiAlignmentRightHanded;
		}
		set
		{
			if (s_uiAlignmentRightHanded != value)
			{
				s_uiAlignmentRightHanded = value;
				if (UbiCore.Utils.Singleton<UiMain>.Instance != null)
				{
					UbiCore.Utils.Singleton<UiMain>.Instance.OnUIAlignmentChanged();
				}
				AcierInputHandler.Instance.LeftHandedMode = value;
			}
		}
	}

	public static bool UIHideFleeOption { get; set; }

	public static bool UIForceShowSkillWheelInAnimus { get; set; }

	public bool IsRunning { get; private set; }

	public EMissionType MissionType { get; private set; }

	public ILogicInterface LogicInterface { get; private set; }

	public UnityEngine.Camera UICamera { get; private set; }

	public UIRoot UISharedRoot { get; private set; }

	public UILayoutManager LayoutManager { get; private set; }

	public UITutorialManager TutorialManager { get; private set; }

	public UIFlowHandler FlowHandler { get; private set; }

	public bool WasLastMissionSuccessful { get; private set; }

	public bool HasSucceededMissionOnce { get; private set; }

	public bool ShouldShowAnimusStartupScreens { get; private set; }

	public UIBuyHelper BuyHelper => m_buyHelper;

	public UIDirectPurchasesShopHelper DirectPurchasesShopHelper => m_directPurchasesShopHelper;

	public UIRoot IngameUIRoot
	{
		get
		{
			if (m_ingameUIRoot == null)
			{
				for (int i = 0; i < UIRoot.list.Count; i++)
				{
					UIRoot uIRoot = UIRoot.list[i];
					if (uIRoot.gameObject.name == "UI InGame Root")
					{
						m_ingameUIRoot = uIRoot;
					}
				}
			}
			return m_ingameUIRoot;
		}
		set
		{
			m_ingameUIRoot = value;
		}
	}

	public static Vector2 DeviceResolution => s_deviceResolution;

	public bool MissionEnded { get; private set; }

	private bool MissionEndedProcessed { get; set; }

	public event Action UIReadyToUse;

	public UiMain()
	{
		UbiCore.Utils.Singleton<UIDataMapper>.Initialize();
		ShouldShowAnimusStartupScreens = true;
		SetupCamera();
		UpdateUISharedLoading();
		InitUIScreens();
		Timer instance = UbiCore.Utils.Singleton<Timer>.Instance;
		instance.OnReset = (Timer.OnResetHandler)Delegate.Combine(instance.OnReset, new Timer.OnResetHandler(OnTimerReset));
		OnTimerReset();
		UbiCore.Utils.Singleton<UIScreenWidgetManager>.Initialize();
		Main.Instance.SceneLoader.OnStep += SceneLoaderOnOnStep;
	}

	public PopUpConfiguration GetErrorPopUpConfiguration(ErrorCode _code, ErrorCategory _group, PopUpButtonConfiguration.ButtonPressHandler _handler)
	{
		string text = string.Empty;
		List<PopUpButtonConfiguration> list = new List<PopUpButtonConfiguration>();
		if (_group == ErrorCategory.LICENSE_CHECK)
		{
			text = LocalizationManager.Instance.Get(8053);
			list.Add(new PopUpButtonConfiguration
			{
				ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
				Data = true,
				Label = LocalizationManager.Instance.Get(7272),
				OnButtonPressed = (object _) =>
				{
					UIUtil.QuitApplication();
				}
			});
			string label = LocalizationManager.Instance.Get(3947);
			PopUpButtonConfiguration.ButtonPressHandler buttonPressHandler = _handler;
			if (_code != ErrorCode.LICENSE_CHECK_FAILED_CANNOT_CONNECT)
			{
				text = LocalizationManager.Instance.Get(9773);
				label = LocalizationManager.Instance.Get(8055);
				buttonPressHandler = (object _) =>
				{
					string appStoreUrl = UIErrorHelper.AppStoreUrl;
					if (!string.IsNullOrEmpty(appStoreUrl))
					{
						Application.OpenURL(appStoreUrl);
					}
				};
			}
			if (buttonPressHandler != null)
			{
				list.Add(new PopUpButtonConfiguration
				{
					ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
					Data = true,
					Label = label,
					OnButtonPressed = buttonPressHandler
				});
			}
		}
		else
		{
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(3947),
				OnButtonPressed = _handler
			});
			if (UIErrorHelper.ShowTwitterHint(_code, _group))
			{
				list.Add(new PopUpButtonConfiguration
				{
					Data = true,
					Label = LocalizationManager.Instance.Get(4933),
					OnButtonPressed = (object _data) =>
					{
						string getTwitterAccountUrl = NewsFeedTwitter.GetTwitterAccountUrl;
						if (!string.IsNullOrEmpty(getTwitterAccountUrl))
						{
							Application.OpenURL(getTwitterAccountUrl);
						}
					}
				});
			}
		}
		PopUpConfiguration defaultErrorBody = UIErrorHelper.GetDefaultErrorBody(_code, _group);
		if (!string.IsNullOrEmpty(text))
		{
			defaultErrorBody.Heading = text;
		}
		defaultErrorBody.Buttons = list.ToArray();
		defaultErrorBody.DeactivateButtonsOnTap = false;
		return defaultErrorBody;
	}

	public void SetUIReadyToUse()
	{
		Singleton<StateManager>.Instance.MissionSelectionManager.Init();
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnUIReadyToUse();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnUIReadyToUse();
		}
		UIReadyToUse?.Invoke();
		UbiCore.Utils.Singleton<LocalStorage>.Instance.ValidateStorageForOldUserOnStartUp();
	}

	public void SetIngameUiVisibility(bool _visible)
	{
		HealthBar.ForceInvisible = !_visible;
		IconDisplay.ForceInvisible = !_visible;
		BBTweenAlpha component = IngameUIRoot.GetComponent<BBTweenAlpha>();
		if (_visible)
		{
			component.StartTweenPlayForward();
		}
		else
		{
			component.StartTweenPlayReverse();
		}
	}

	public void SetIngameUIStartUpVisibility()
	{
		HealthBar.ForceInvisible = false;
		IconDisplay.ForceInvisible = false;
		BBTweenAlpha component = IngameUIRoot.GetComponent<BBTweenAlpha>();
		component.SetTweenToEnd();
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			IUiSubSystem uiSubSystem = m_subSystems[i];
			uiSubSystem.SetVisible(uiSubSystem.InitiallyVisible);
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			IUiSubSystem uiSubSystem2 = m_newSubSystems[j];
			if (!(uiSubSystem2 is UIBase uIBase) || uIBase.Details == null || !TutorialManager.AdjustVisibility(uIBase.Details.gameObject))
			{
				uiSubSystem2.SetVisible(uiSubSystem2.InitiallyVisible);
			}
		}
	}

	private void SceneLoaderOnOnStep(SceneLoader.ELoadingStep step)
	{
		switch (step)
		{
		case SceneLoader.ELoadingStep.AllDone:
		{
			UbiCore.Utils.Singleton<UIScreenWidgetManager>.Instance.RegisterAllInGameBehaviours();
			for (int k = 0; k < m_subSystems.Count; k++)
			{
				m_subSystems[k].OnSceneLoaded();
			}
			for (int l = 0; l < m_newSubSystems.Count; l++)
			{
				m_newSubSystems[l].OnSceneLoaded();
			}
			break;
		}
		case SceneLoader.ELoadingStep.Unload:
		{
			for (int i = 0; i < m_subSystems.Count; i++)
			{
				m_subSystems[i].OnSceneUnload();
			}
			for (int j = 0; j < m_newSubSystems.Count; j++)
			{
				m_newSubSystems[j].OnSceneUnload();
			}
			UbiCore.Utils.Singleton<UIScreenWidgetManager>.Instance.UnRegisterAllInGameBehaviours();
			break;
		}
		case SceneLoader.ELoadingStep.Unloaded:
			break;
		}
	}

	public void OnMissionLoad(EMissionType _missionType, ILogicInterface _logicInterface)
	{
		m_callUnload = true;
		m_callUnloaded = true;
		MissionType = _missionType;
		LogicInterface = _logicInterface;
		UbiCore.Utils.Singleton<MissionManager>.Instance.OnMissionStateChanged += OnMissionStateChanged;
		Timer instance = UbiCore.Utils.Singleton<Timer>.Instance;
		instance.OnPauseChanged = (Timer.OnPauseChangeHandler)Delegate.Combine(instance.OnPauseChanged, new Timer.OnPauseChangeHandler(OnPauseChanged));
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionLoad();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionLoad();
		}
		if (_missionType != EMissionType.Animus_EMissionType)
		{
			GameObject gameObject = GameObject.FindGameObjectWithTag("UiIconCamera");
			GameObject gameObject2 = gameObject.FindDirectChildByName(Assets.Scripts.UI.Ingame.Constants.IconPoolTag);
			if (gameObject2 == null)
			{
				gameObject2 = new GameObject(Assets.Scripts.UI.Ingame.Constants.IconPoolTag);
				gameObject2.transform.parent = gameObject.transform;
			}
		}
	}

	public void OnMissionLoaded()
	{
		IsRunning = true;
		ShouldShowAnimusStartupScreens = false;
		MissionEnded = false;
		MissionEndedProcessed = false;
		UbiCore.Utils.Singleton<IconManager>.Initialize();
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionLoaded();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionLoaded();
			m_newSubSystems[j].SetVisible(_value: false);
		}
		AcierInputHandler.Instance.OnMissionLoaded();
		AcierCameraController acierCameraController = AcierCameraController.Create();
		AudioManager.Instance.SetAudioListenerParent(acierCameraController.Camera.transform);
	}

	public void OnMissionUnload()
	{
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionUnload();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionUnload();
		}
		GameObject gameObject = GameObject.FindGameObjectWithTag("UiIconCamera");
		if ((bool)gameObject)
		{
			GameObject obj = gameObject.FindDirectChildByName(Assets.Scripts.UI.Ingame.Constants.IconPoolTag);
			UnityEngine.Object.Destroy(obj);
		}
		AcierInputHandler.Instance.OnMissionUnload();
		MissionType = EMissionType.None_EMissionType;
		m_callUnload = false;
	}

	public void OnMissionUnloaded()
	{
		if (m_callUnload)
		{
			OnMissionUnload();
		}
		IsRunning = false;
		UbiCore.Utils.Singleton<MissionManager>.Instance.OnMissionStateChanged -= OnMissionStateChanged;
		Timer instance = UbiCore.Utils.Singleton<Timer>.Instance;
		instance.OnPauseChanged = (Timer.OnPauseChangeHandler)Delegate.Remove(instance.OnPauseChanged, new Timer.OnPauseChangeHandler(OnPauseChanged));
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionUnloaded();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionUnloaded();
		}
		AcierInputHandler.Instance.OnMissionUnloaded();
		AudioManager.Instance.ResetAudioListenerParent();
		AcierCameraController.Shutdown();
		UbiCore.Utils.Singleton<IconManager>.Shutdown();
		GameObject gameObject = GameObject.FindGameObjectWithTag("UiIconCamera");
		if (gameObject != null)
		{
			GameObject gameObject2 = gameObject.FindDirectChildByName(Assets.Scripts.UI.Ingame.Constants.IconPoolTag);
			if (gameObject2 != null)
			{
				for (int k = 0; k < gameObject2.transform.childCount; k++)
				{
					UnityEngine.Object.Destroy(gameObject2.transform.GetChild(k).gameObject);
				}
				gameObject2.transform.parent = null;
				UnityEngine.Object.Destroy(gameObject2);
			}
		}
		if (LocalizationManager.Instance != null)
		{
			LocalizationManager.Instance.PurgeManager();
		}
		if (Singleton<UIScreenManager>.Instance != null)
		{
			Singleton<UIScreenManager>.Instance.ClearStack();
		}
		LogicInterface = null;
		IngameUIRoot = null;
		m_callUnloaded = false;
	}

	public void OnUIAlignmentChanged()
	{
		for (int i = 0; i < m_newSubSystems.Count; i++)
		{
			m_newSubSystems[i].OnUIAlignmentChanged(UIAlignmentRightHanded);
		}
	}

	public float GetScale()
	{
		return (float)Screen.width / DeviceResolution.x;
	}

	public void Tap(TapEvent _event)
	{
		for (int i = 0; i < m_newSubSystems.Count; i++)
		{
			if (m_newSubSystems[i].IsActive && m_newSubSystems[i].IsOnUi(_event.Position))
			{
				return;
			}
		}
		for (int j = 0; j < m_subSystems.Count && (!m_subSystems[j].IsActive || !m_subSystems[j].IsOnUi(_event.Position) || !m_subSystems[j].Tap(_event)); j++)
		{
		}
	}

	public bool IsOnUi(Vector2 _position)
	{
		if (IsRunning)
		{
			for (int i = 0; i < m_newSubSystems.Count; i++)
			{
				if (m_newSubSystems[i].IsActive && m_newSubSystems[i].IsOnUi(_position))
				{
					return true;
				}
			}
			for (int j = 0; j < m_subSystems.Count; j++)
			{
				if (m_subSystems[j].IsActive && m_subSystems[j].IsOnUi(_position))
				{
					return true;
				}
			}
		}
		return false;
	}

	public void EndMission()
	{
		MissionEndedProcessed = true;
		AudioManager.Instance.EventManager.OnResultScreenEnter();
		UbiCore.Utils.Singleton<UiMain>.Instance.SetIngameUiVisibility(_visible: false);
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionEnded();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionEnded();
		}
		HealthBar.ForceInvisible = true;
		IconDisplay.ForceInvisible = true;
	}

	public void StartOutro()
	{
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnMissionOutroStarted();
		}
		for (int j = 0; j < m_newSubSystems.Count; j++)
		{
			m_newSubSystems[j].OnMissionOutroStarted();
		}
	}

	public T GetSubSystemByType<T>() where T : class, IUiSubSystem
	{
		for (int i = 0; i < m_newSubSystems.Count; i++)
		{
			if (m_newSubSystems[i] is T result)
			{
				return result;
			}
		}
		return (T)null;
	}

	public void OnEnterMission()
	{
		UINotificationPopup subSystemByType = GetSubSystemByType<UINotificationPopup>();
		subSystemByType.IsShowingNotificationPaused = false;
		FlowHandler.OnEnterMission();
	}

	public void OnEnterNextMission()
	{
		HasSucceededMissionOnce = false;
		WasLastMissionSuccessful = false;
	}

	public void OnEnterAnimus(bool _isStartup)
	{
		if (ShouldShowAnimusStartupScreens)
		{
			Singleton<StateManager>.Instance.EventManager.GetEventManger(EEventType.Event_Flavor).OnChanged += new SimpleDelegate.CallbackFunction(OnFlavorEventChanged);
			FlowHandler.OnEnterAnimusFromStartUp();
			return;
		}
		if (_isStartup)
		{
			FlowHandler.OnEnterAnimusAtStartup();
			return;
		}
		FlowHandler.OnEnterAnimusAfterMission();
		Singleton<StateManager>.Instance.MissionSelectionManager.ClearMissionAfterTutorial();
		HasSucceededMissionOnce = false;
		WasLastMissionSuccessful = false;
	}

	private void OnFlavorEventChanged(object[] _params)
	{
		if (!Main.Instance.SceneLoader.InAnimus)
		{
			return;
		}
		ThemesManager themesManager = Singleton<StateManager>.Instance.ThemesManager;
		EventManager eventManager = Singleton<StateManager>.Instance.EventManager;
		EventFlavorThemeManager eventFlavorThemeManager = (EventFlavorThemeManager)eventManager.GetEventManger(EEventType.Event_Flavor);
		bool flag = eventFlavorThemeManager.IsThemeEnabled(ThemesManager.EFlavorTheme.Pirate);
		bool flag2 = themesManager.ActiveTheme == ThemesManager.EFlavorTheme.Pirate;
		if (flag != flag2 && Singleton<UIScreenManager>.Instance.CurrentModalScreen == null)
		{
			PopUpButtonConfiguration[] buttons = new PopUpButtonConfiguration[1]
			{
				new PopUpButtonConfiguration
				{
					ButtonType = PopUpButtonConfiguration.EButtonType.eSuggestedClick,
					Label = LocalizationManager.Instance.Get(1780),
					OnButtonPressed = (object _) =>
					{
						UIUtil.QuitApplication();
					}
				}
			};
			PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
			popUpConfiguration.Heading = LocalizationManager.Instance.Get(10154);
			popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Heading;
			popUpConfiguration.ShowCloseButton = false;
			popUpConfiguration.Data = new ErrorContent.ErrorData
			{
				Description = LocalizationManager.Instance.Get(10155)
			};
			popUpConfiguration.Buttons = buttons;
			PopUpConfiguration config = popUpConfiguration;
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
		}
	}

	public void PurgeAnimusScreen()
	{
		TutorialManager.PurgeTutorial();
		DirectPurchasesShopHelper.Clear();
		FlowHandler.PurgeAnimusScreen();
	}

	private void OnTimerReset()
	{
		UbiCore.Utils.Singleton<Timer>.Instance.AddCallback(UpdateFrame, 0f, _unscaled: true);
	}

	private void InitUIScreens()
	{
		m_subSystems = new BBList<IUiSubSystem>
		{
			new NpcInteractionUi(),
			new CombatUI(),
			new TouchFeedbackUI(),
			new MissionTargetIndicatorUI()
		};
		FlowHandler = new UIFlowHandler();
		TutorialManager = new UITutorialManager();
		m_newSubSystems = new BBList<IUiSubSystem>
		{
			new UILoadingScreen(),
			new UIPlayerUnitFrame(),
			new UIEnemyUnitFrame(),
			new UIObjectiveTracker(),
			new UIMissionResult(),
			new UISkills(),
			new UIMiniMap(),
			new UIMovement(),
			new UIAction(),
			new UIPause(),
			new UIOnScreenItemPreview(),
			new UIOnScreenMessage(),
			new UIOnScreenTimer(),
			TutorialManager,
			new UIDownloadScreen(),
			new UIMissionIntroduction(),
			new UIMissionOutro(),
			new UIKillTokenBar(),
			new UINotificationPopup(),
			new UIIngamePopup()
		};
	}

	protected override void OnDestroy()
	{
		if (!SharedMain.IsShuttingDown)
		{
			if (m_callUnload)
			{
				OnMissionUnload();
			}
			if (m_callUnloaded)
			{
				OnMissionUnloaded();
			}
			m_newSubSystems.Clear();
			base.OnDestroy();
			UbiCore.Utils.Singleton<UIScreenWidgetManager>.Shutdown();
			UbiCore.Utils.Singleton<UIDataMapper>.Shutdown();
		}
	}

	private void OnPauseChanged(Timer.EPauseState _pauseReason)
	{
		UIObjectiveTracker subSystemByType = GetSubSystemByType<UIObjectiveTracker>();
		subSystemByType.SetPaused(_pauseReason != Timer.EPauseState.Running);
	}

	private void OnMissionStateChanged(EMissionState _state)
	{
		if (!IsRunning)
		{
			return;
		}
		if (_state == EMissionState.InProgress)
		{
			SetIngameUIStartUpVisibility();
		}
		if (_state != EMissionState.Failed && _state != EMissionState.Success)
		{
			return;
		}
		UINotificationPopup subSystemByType = GetSubSystemByType<UINotificationPopup>();
		subSystemByType.IsShowingNotificationPaused = true;
		WasLastMissionSuccessful = _state == EMissionState.Success;
		HasSucceededMissionOnce |= WasLastMissionSuccessful;
		if (!Assets.Scripts.Engine.Helpers.Verify(Main.Instance.IsNetworkEnabled, "No datastore - working offline?!"))
		{
			return;
		}
		string stringToken = "Invalid Token";
		UIMissionToken missionTicket = Singleton<StateManager>.Instance.ProfileManager.MissionTicket;
		if (missionTicket != null && missionTicket.Valid)
		{
			stringToken = missionTicket.Value;
		}
		TutorialCategory category = TutorialManager.GetCategory(ETutorialCategory.TutCatIntroMission);
		if (category != null && !category.Finished)
		{
			Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
			Singleton<UIScreenManager>.Instance.PurgeManager();
			Singleton<StateManager>.Instance.ProfileManager.EnableUpdates = true;
			Main.Instance.LoadMission(BuildDefinition.AnimusMission, null, _eliteMission: false);
			return;
		}
		Singleton<UIScreenManager>.Instance.ShowLoadingOverlay(LoadingOverlayConfiguration.Default);
		UIMissionResult subSystemByType2 = GetSubSystemByType<UIMissionResult>();
		subSystemByType2.SetOldUserProfile(Singleton<StateManager>.Instance.ProfileManager.Profile);
		UINetworkAdaptor.NetworkRequest((AsyncCallback _asyncCallback) =>
		{
			Singleton<StateManager>.Instance.ProfileManager.EndMission(LogicMain.MissionResult.ToProtobuf(), stringToken, WasLastMissionSuccessful, _asyncCallback);
		}, new RetryHandler(UIMissionHelper.GoToAnimusNoNetwork, 6543), OnEndMission);
		LogicMain.MissionResult.ResetConsumedItems();
	}

	private bool UpdateFrame(float _deltaTime)
	{
		if (m_uiSharedRootScreenLoading)
		{
			UpdateUISharedLoading();
		}
		if (Singleton<UIScreenManager>.Instance.SceneData != null && Singleton<UIScreenManager>.Instance.SceneData.ShopDetails != null)
		{
			Singleton<UIScreenManager>.Instance.SceneData.ShopDetails.UpdateBlockedShopItems();
		}
		if (MissionType == EMissionType.Animus_EMissionType)
		{
			FlowHandler.Update();
		}
		Profiling.Start("UiMain::UpdateFrame()", float.MaxValue);
		if (IsRunning)
		{
			Profiling.Start("LogicMain.LogicInterface.FireEvents()", float.MaxValue);
			LogicMain.LogicInterface.FireEvents();
			Profiling.Stop();
		}
		m_playerPopupHelper.Update();
		if (!m_playerPopupHelper.IsVisible)
		{
			if (Main.ProfilingDetailed)
			{
				for (int i = 0; i < m_subSystems.Count; i++)
				{
					if (m_subSystems[i].IsActive)
					{
						m_subSystems[i].UpdateFrame(_deltaTime);
					}
				}
				for (int j = 0; j < m_newSubSystems.Count; j++)
				{
					if (m_newSubSystems[j].IsActive)
					{
						m_newSubSystems[j].UpdateFrame(_deltaTime);
					}
				}
			}
			else
			{
				for (int k = 0; k < m_subSystems.Count; k++)
				{
					if (m_subSystems[k].IsActive)
					{
						m_subSystems[k].UpdateFrame(_deltaTime);
					}
				}
				for (int l = 0; l < m_newSubSystems.Count; l++)
				{
					if (m_newSubSystems[l].IsActive)
					{
						m_newSubSystems[l].UpdateFrame(_deltaTime);
					}
				}
			}
		}
		Profiling.Stop();
		if (MissionEnded && !MissionEndedProcessed && GetSubSystemByType<UIOnScreenMessage>().IsQueueEmpty)
		{
			EndMission();
		}
		return true;
	}

	private UIRoot GetOrLoadeSharedUIRoot()
	{
		UIRoot result = null;
		if (m_loadeSharedUIOperation != null && !m_loadeSharedUIOperation.isDone)
		{
			return null;
		}
		GameObject gameObject = GameObject.FindGameObjectWithTag("UISharedRoot");
		if (gameObject == null)
		{
			m_loadeSharedUIOperation = Application.LoadLevelAdditiveAsync("UIShared");
			m_uiSharedRootScreenLoading = true;
		}
		else
		{
			result = gameObject.GetComponent<UIRoot>();
		}
		return result;
	}

	private void UpdateUISharedLoading()
	{
		if (UISharedRoot != null)
		{
			return;
		}
		UISharedRoot = GetOrLoadeSharedUIRoot();
		if (UISharedRoot != null)
		{
			m_uiSharedRootScreenLoading = false;
			UbiCore.Utils.Singleton<UIScreenWidgetManager>.Instance.RegisterAllSharedBehaviours(UISharedRoot);
			LayoutManager = UISharedRoot.GetComponentInChildren<UILayoutManager>();
			for (int i = 0; i < m_newSubSystems.Count; i++)
			{
				m_newSubSystems[i].OnSharedUILoaded();
			}
		}
	}

	public void OnLevelWasLoaded()
	{
		if (UIDownloadScreen.Instance != null && UIDownloadScreen.Instance.Details != null)
		{
			((UIDownloadScreenDetails)UIDownloadScreen.Instance.Details).OnLevelWasLoaded();
		}
	}

	private void SetupCamera()
	{
		UICamera = GetOrCreateUiIconCamera();
		for (int i = 0; i < m_subSystems.Count; i++)
		{
			m_subSystems[i].OnCameraCreated();
		}
	}

	public UnityEngine.Camera GetOrCreateUiIconCamera()
	{
		GameObject gameObject = GameObject.FindGameObjectWithTag("UiIconCamera");
		if (gameObject == null)
		{
			gameObject = new GameObject("Camera Icon UI");
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
			gameObject.tag = "UiIconCamera";
			gameObject.transform.position = new Vector3((float)Screen.width * 0.5f, (float)Screen.height * 0.5f, -2f);
		}
		UnityEngine.Camera camera = gameObject.GetComponent<UnityEngine.Camera>();
		if (camera == null)
		{
			camera = gameObject.AddComponent<UnityEngine.Camera>();
			gameObject.AddComponent<GUILayer>();
			camera.nearClipPlane = 1f;
			camera.farClipPlane = 1000f;
			camera.orthographic = true;
			camera.orthographicSize = (float)Screen.height * 0.5f;
			camera.aspect = (float)Screen.width / (float)Screen.height;
			camera.cullingMask = Layers.UILayersMaskPreset;
			camera.depth = 80f;
			camera.clearFlags = CameraClearFlags.Depth;
		}
		return camera;
	}

	private void OnEndMission(IAsyncResult _asyncResult)
	{
		if (!_asyncResult.IsError())
		{
			UIMissionResult subSystemByType = GetSubSystemByType<UIMissionResult>();
			AsyncDataResult<EndMissionResponse> asyncDataResult = (AsyncDataResult<EndMissionResponse>)_asyncResult;
			EndMissionResponse endMissionResponse = asyncDataResult.Result();
			subSystemByType.SetMissionReward(endMissionResponse.missionReward);
			FriendHirelingPopup.RecordMissionData();
			CallToActionPopup.RecordMissionReward(endMissionResponse.missionReward);
			OnMissionFinished();
		}
	}

	private void OnMissionFinished()
	{
		Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
		TutorialCategory category = TutorialManager.GetCategory(ETutorialCategory.TutCatGuideEraSelect);
		bool flag = category != null && TutorialManager.CurrentCategory == category && !category.Finished;
		if (WasLastMissionSuccessful && flag)
		{
			TutorialManager.FinishCategory(ETutorialCategory.TutCatGuideEraSelect, OnFinishCategory);
		}
		if (!flag || !WasLastMissionSuccessful)
		{
			StartMissionEndedSequence();
		}
	}

	private void OnFinishCategory(IAsyncResult _ar)
	{
		StartMissionEndedSequence();
	}

	private void StartMissionEndedSequence()
	{
		MissionEnded = true;
	}
}
