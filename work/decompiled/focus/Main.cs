using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using AcierNetwork;
using AcierProtocol;
using AssetBundleData;
using Assets.Scripts;
using Assets.Scripts.Audio;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.LicenseCheck;
using Assets.Scripts.Mission;
using Assets.Scripts.Mission.Definitions;
using Assets.Scripts.Mission.Logic.Components;
using Assets.Scripts.Mission.Logic.Stats;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Mission.Utilities.AutoUpdating;
using Assets.Scripts.Native;
using Assets.Scripts.Network;
using Assets.Scripts.Network.Error;
using Assets.Scripts.Network.Session;
using Assets.Scripts.Network.Shop;
using Assets.Scripts.Tracking;
using Assets.Scripts.UI;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.Network;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Screens.Popup;
using Assets.Scripts.UI.Screens.Popup.ErrorPopup;
using Assets.Scripts.UI.State;
using Assets.Scripts.UI.State.EventManager;
using Assets.Scripts.UI.State.ProfileManager;
using Assets.Scripts.UI.State.ThemeManager;
using Assets.Scripts.UI.Tutorial;
using Assets.Scripts.UI.Utils;
using Assets.Scripts.UI.Utils.Extensions;
using BB.Mobile.Net;
using Database;
using InControl;
using SharedBaseLib.Core;
using SharedBaseLib.Storage;
using SharedBaseLib.System;
using SharedBaseLib.System.Cache;
using UbiCore.Debug;
using UbiCore.Debug.UI;
using UbiCore.Mission.Loading;
using UbiCore.Utils;
using UbiCore.Utils.Extensions;
using UnityEngine;

public class Main : SharedMain, INativeShopMessageHandler
{
	private delegate IEnumerator CoRoutineFnc();

	private const string m_singletonName = "__Main";

	private bool m_isInitializeStartupCompleted;

	private InterruptHandler m_interruptHandler;

	private static Main _instance;

	public static bool ProfilingDetailed;

	public static bool ShowConfidentialWarning;

	public static bool OverwriteAnimusMissionLock;

	private static GameDB s_gameDb;

	private float m_resourceUnloadRequestedTime;

	private bool m_resourceUnloadedRequested;

	private bool m_HaveStoragePermission;

	private static bool m_ExternalStoragePopupShown;

	private CoRoutineFnc m_currentStartupCo;

	private DebugMenu m_debugMenu;

	private bool m_menusAdded;

	private int m_lastTouch;

	private ErrorState m_errorState;

	private LicenseCheckBase m_licenseCheckBase;

	protected string m_StoreURL = "http://acid-dev.ubisoft.org:8330/rest-1.0-SNAPSHOT-ha-group-trunk/acier/v1/api";

	protected string m_LocalhostURL = "http://localhost:8080/rest-1.2-SNAPSHOT/acier/v1/api";

	protected bool m_UseLocalHost;

	protected DateTime m_StartTime = DateTime.UtcNow;

	protected bool m_trackSend;

	private DateTime m_lastCheckedDateTime = DateTime.UtcNow;

	private static string kExpectedUnityEditorVersion = "5.2.3p1";

	public Startup Startup { get; private set; }

	public GameDB GameDB { get; private set; }

	public BuildDefinition BuildDefinition { get; private set; }

	public SceneLoadingProvider SceneLoadingProvider { get; private set; }

	public MissionLoaderProvider MissionLoaderProvider { get; private set; }

	public SceneLoader SceneLoader { get; private set; }

	public AcierProtocol.QualitySettings QualitySettings { get; private set; }

	public string SignedData { get; private set; }

	public string Signature { get; private set; }

	public bool LowMemoryDevice { get; private set; }

	public bool FacebookAchievementsEnabled { get; private set; }

	public bool ShowLowEndDeviceWarning { get; private set; }

	public bool FacebookEnabled { get; private set; }

	public bool SupportEnabled { get; private set; }

	public bool AdColonyEnabled { get; private set; }

	public bool UplayEnabled { get; private set; }

	public bool WeiboEnabled { get; private set; }

	public bool VrEnabled => false;

	public PerformanceTracker PerformanceTracker { get; private set; }

	public CameraCustomAnimationHelper CameraCustomAnimationHelper { get; set; }

	public string BaseURL { get; private set; }

	public static GameDB SafeGameDb => Instance.GameDB;

	public static bool IsInitialized => _instance != null;

	public new static Main Instance
	{
		get
		{
			if (_instance == null && !SharedMain.IsShuttingDown)
			{
				GameObject gameObject = GameObject.Find("__Main");
				if (gameObject == null)
				{
					gameObject = new GameObject("__Main");
				}
				_instance = gameObject.AddComponentIfNotExist<Main>();
			}
			return _instance;
		}
	}

	public static bool HasInstance => _instance != null;

	public LicenseCheckBase LicenseChecker
	{
		get
		{
			if (m_licenseCheckBase == null)
			{
				m_licenseCheckBase = new LicenseCheckIOS();
			}
			return m_licenseCheckBase;
		}
	}

	public MATManager MATManager { get; private set; }

	public ClientAchievementsManager ClientAchievementsManager { get; private set; }

	public FlurryAnalyticsManager FlurryManager { get; private set; }

	public IShopDataStore ShopDataStore { get; private set; }

	public bool IsNetworkEnabled { get; private set; }

	public WebProxyMonitor WebProxyManager { get; private set; }

	protected override void Awake()
	{
	}

	public void OnApplicationQuit()
	{
		saveSessionTrack();
		SharedMain.IsShuttingDown = true;
	}

	private void OnApplicationFocus(bool focus)
	{
		DevicePermissionHelper.OnApplicationPause(!focus);
		if (m_isInitializeStartupCompleted)
		{
			InputManager.ClearInputState();
			OnApplicationPause(!focus);
		}
	}

	public void OnApplicationPause(bool _pause)
	{
		if (!m_isInitializeStartupCompleted)
		{
			return;
		}
		InputManager.ClearInputState();
		if (AdColonyManager.HasTriggeredAdColonyVideo)
		{
			return;
		}
		if (!UIDownloadScreen.IsLoadedAndFullyVisible)
		{
			if (m_interruptHandler == null)
			{
				m_interruptHandler = new InterruptHandler();
			}
			m_interruptHandler.OnPause(_pause);
		}
		if (_pause)
		{
			saveSessionTrack();
			return;
		}
		m_trackSend = false;
		m_StartTime = DateTime.UtcNow;
		if (PerformanceTracker != null)
		{
			PerformanceTracker.Restart();
		}
	}

	protected void saveSessionTrack()
	{
		if (!m_trackSend)
		{
			TrackingManager.AddSessionTrackPoint(m_StartTime, DateTime.UtcNow);
			m_trackSend = true;
		}
	}

	public void DisablePostprocessing()
	{
		GameObject gameObject = GameObject.Find("Postprocessing");
		if (!(gameObject != null))
		{
			return;
		}
		PostprocessingSettings[] componentsInChildren = gameObject.GetComponentsInChildren<PostprocessingSettings>(includeInactive: true);
		PostprocessingSettings[] array = componentsInChildren;
		foreach (PostprocessingSettings postprocessingSettings in array)
		{
			postprocessingSettings.gameObject.SetActive(value: false);
			Camera main = Camera.main;
			if (main != null)
			{
				Rendering.RefreshPostEffects(main);
			}
			CombinedPostEffect combinedPostEffect = ((!(main != null)) ? null : main.GetComponent<CombinedPostEffect>());
			if (combinedPostEffect != null)
			{
				combinedPostEffect.Refresh();
			}
		}
	}

	public void OnLevelWasLoaded(int _sceneIndex)
	{
		string loadedLevelName = Application.loadedLevelName;
		SceneLoader.OnLevelWasLoaded(loadedLevelName);
		UbiCore.Utils.Singleton<UiMain>.Instance.OnLevelWasLoaded();
	}

	public void RequestResourceUnload()
	{
		m_resourceUnloadedRequested = true;
		m_resourceUnloadRequestedTime = UbiCore.Utils.Singleton<Timer>.Instance.realTime;
	}

	private IEnumerator ResourceUnloadAsynch()
	{
		while (true)
		{
			if (!m_resourceUnloadedRequested)
			{
				yield return StartCoroutine(Timer.WaitForSecondsUnscaled(2.1f));
			}
			else
			{
				float delta = UbiCore.Utils.Singleton<Timer>.Instance.realTime - m_resourceUnloadRequestedTime;
				if (delta > 2.1f)
				{
					m_resourceUnloadedRequested = false;
					AsyncOperation result = UnityEngine.Resources.UnloadUnusedAssets();
					while (!result.isDone)
					{
						yield return 0;
					}
				}
			}
			yield return 0;
		}
	}

	public void OnDestroy()
	{
		SharedMain.IsShuttingDown = true;
		EveryplayManager.Uninitialize();
		UbiCore.Utils.Singleton<MissionManager>.Shutdown();
		UbiCore.Utils.Singleton<UiMain>.Shutdown();
		UbiCore.Utils.Singleton<DefinitionManager>.Shutdown();
		_instance = null;
	}

	public static string LoadSavedDataPath()
	{
		return SavedDataDir.GetPath();
	}

	public void Start()
	{
		StartCoroutine(Co_Initialize_Startup());
	}

	private IEnumerator Co_Initialize_Startup()
	{
		UbiCore.Utils.Singleton<Timer>.Initialize();
		UbiCore.Utils.Singleton<Timer>.Instance.smoothFactor = 0.17f;
		BuildDefinition = new BuildDefinition();
		UnityEngine.Debug.Log("Starting up ACID - FRODO");
		yield return StartCoroutine(DevicePermissionHelper.Co_Initialize_Startup());
		AndroidExternalStorage = new AndroidExternalStorage();
		base.Awake();
		m_HaveStoragePermission = NativeTools.HaveStoragePermission;
		EveryplayManager.EnableEveryplaySettings(enable: false);
		SetupNetwork();
		LowMemoryDevice = SystemInfo.systemMemorySize < 800 || SystemInfo.maxTextureSize < 4096;
		ValidateUnityEditorVersion();
		Screen.sleepTimeout = -1;
		Profiling.Start("Main::Start()", float.MaxValue);
		gameObject.AddComponentIfNotExist<Rendering>();
		SharedBaseLib.System.Main.InitOnDemand();
		LayerPresets.Init();
		SceneLoader = new SceneLoader();
		BBCollectionsTracker.Enabled = BuildDefinition.EnableCollectionTracker;
		StatTypes.Initialize();
		DefinitionManager.Initialize();
		StateMachines.InitStateMachines();
		UbiCore.Utils.Singleton<UiMain>.Initialize();
		Singleton<EffectManager>.Instance.Init();
		Handheld.PlayFullScreenMovie("UbisoftLogo_AC4.mp4", Color.black, FullScreenMovieControlMode.Hidden);
		Startup = gameObject.AddComponentIfNotExist<Startup>();
		WebProxyManager = gameObject.AddComponentIfNotExist<WebProxyMonitor>();
		PushNotificationsProvider.Init();
		FacebookManager.AutoInit();
		InputHandler.FindOrCreate<AcierInputHandler>(transform);
		Profiling.Stop();
		PerformanceTracker = gameObject.AddComponentIfNotExist<PerformanceTracker>();
		PerformanceTracker.Restart();
		StartCoroutine(ResourceUnloadAsynch());
		StartCoroutine(Co_Initialize_PrepareStartup());
		m_isInitializeStartupCompleted = true;
		yield return null;
	}

	private static void SetupNetwork()
	{
		using (MemoryStream source = new MemoryStream(((TextAsset)UnityEngine.Resources.Load("Pinsets")).bytes))
		{
			CertificateValidator.Enregister(new HostPinsetStore(source));
		}
		CertificateValidator.Enregister((string hostname, X509Certificate certificate, X509Chain chain, CertificatePolicyErrors certificatePolicyErrors) =>
		{
			CertificatePolicyErrors certificatePolicyErrors2 = CertificatePolicyErrors.None;
			if ((certificatePolicyErrors & CertificatePolicyErrors.RemoteCertificateChainErrors) != 0)
			{
				X509ChainStatusFlags x509ChainStatusFlags = X509ChainStatusFlags.NoError;
				for (int i = 0; i < chain.ChainStatus.Length; i++)
				{
					x509ChainStatusFlags |= chain.ChainStatus[i].Status;
				}
				switch (x509ChainStatusFlags)
				{
				case X509ChainStatusFlags.NoError:
					certificatePolicyErrors2 |= CertificatePolicyErrors.RemoteCertificateChainErrors;
					break;
				case X509ChainStatusFlags.RevocationStatusUnknown | X509ChainStatusFlags.PartialChain | X509ChainStatusFlags.OfflineRevocation:
				{
					bool flag = true;
					foreach (X509ChainElement chainElement in chain.ChainElements)
					{
						for (int j = 0; j < chainElement.ChainElementStatus.Length; j++)
						{
							X509ChainStatusFlags status = chainElement.ChainElementStatus[j].Status;
							if (status != X509ChainStatusFlags.RevocationStatusUnknown && status != X509ChainStatusFlags.OfflineRevocation)
							{
								flag = false;
							}
							if (!flag)
							{
								break;
							}
						}
						if (!flag)
						{
							break;
						}
					}
					if (flag)
					{
						certificatePolicyErrors2 |= CertificatePolicyErrors.RemoteCertificateChainErrors;
					}
					break;
				}
				}
			}
			return certificatePolicyErrors2;
		});
	}

	private void SetAndStartCoroutine(CoRoutineFnc _fnc)
	{
		m_currentStartupCo = _fnc;
		if (m_currentStartupCo != null)
		{
			StartCoroutine(m_currentStartupCo());
		}
	}

	private void CreateDebugMenu()
	{
		m_debugMenu = DebugMenu.Create(transform);
		DebugMenuGeneral debugMenuGeneral = m_debugMenu.Register<DebugMenuGeneral>();
		m_debugMenu.Register<DebugMenuRendering>();
		List<string> list = new List<string>();
		list.Add("UNITY_SHIPPING");
		list.Add("UNITY_PRO_LICENSE");
		debugMenuGeneral.RegisterModule("UnityAcier", list.ToArray());
		debugMenuGeneral.RegisterToggleListener(DebugMenuGeneral.EExternalToggle.NetworkLogging, (bool value) =>
		{
			SharedNetworkDataStore.s_LogNetworkResults = value;
		});
	}

	protected override void FixFilePermissionsImpl(string _fn)
	{
		try
		{
			base.FixFilePermissionsImpl(_fn);
		}
		catch (Exception)
		{
		}
	}

	protected override ulong GetFreeDiskspaceImpl()
	{
		return NativeTools.GetFreeDiskspace();
	}

	private void CreateAdditionalDebugMenus()
	{
		if (m_debugMenu != null && !m_menusAdded)
		{
			m_debugMenu.Register<DebugMenuInput>();
			m_debugMenu.Register<DebugMenuDisabler>();
			m_debugMenu.Register<DebugMenuCamera>();
			m_debugMenu.Register<DebugMenuMission>();
			m_debugMenu.Register<DebugMenuAnimation>();
			m_debugMenu.Register<DebugMenuLogic>();
			m_debugMenu.Register<DebugMenuCombat>();
			m_debugMenu.Register<DebugMenuSystem>();
			m_debugMenu.Register<DebugMenuVersion>().Initialize(BuildDefinition);
			m_debugMenu.Register<DebugMenuPlayerProfile>();
			m_debugMenu.Register<DebugMenuMail>();
			m_debugMenu.Register<DebugMenuGimpliEvents>();
			m_menusAdded = true;
		}
	}

	private IEnumerator Co_Initialize_PrepareStartup()
	{
		UbiCore.Utils.Singleton<LocalStorage>.Initialize();
		if (UbiCore.Utils.Singleton<LocalStorage>.Instance.VersionIncrease)
		{
			UbiCore.Utils.Singleton<Manager>.Instance.Clear();
		}
		WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
		if (!TrackingManager.Initialized)
		{
			TrackingManager.InitAsync();
		}
		while (!TrackingManager.Initialized)
		{
			yield return endOfFrame;
		}
		while (UbiCore.Utils.Singleton<UiMain>.Instance.UISharedRoot == null)
		{
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
		Singleton<UIScreenManager>.Instance.Initialize();
		while (!UIDownloadScreen.Instance.IsInitialized)
		{
			yield return endOfFrame;
		}
		SetAndStartCoroutine(Co_PrepareStartup);
	}

	private IEnumerator Co_PrepareStartup()
	{
		m_errorState = null;
		yield return StartCoroutine(RetrieveDiscoveryService());
		if (m_errorState != null)
		{
			yield return StartCoroutine(ShowErrorPopup(m_errorState));
			yield break;
		}
		yield return StartCoroutine(DoStaticRequests());
		if (m_errorState != null)
		{
			yield return StartCoroutine(ShowErrorPopup(m_errorState));
			yield break;
		}
		yield return StartCoroutine(RetrieveQualitySettings());
		if (m_errorState != null)
		{
			yield return StartCoroutine(ShowErrorPopup(m_errorState));
			yield break;
		}
		if (QualitySettings != null)
		{
			EQualityAssetBundle? previousAssetBundlesQualityPtr = UbiCore.Utils.Singleton<LocalStorage>.Instance.AssetBundlesQuality;
			if (previousAssetBundlesQualityPtr.HasValue && previousAssetBundlesQualityPtr.Value != QualitySettings.assetBundleQuality)
			{
				UbiCore.Utils.Singleton<Manager>.Instance.Clear();
			}
			UbiCore.Utils.Singleton<LocalStorage>.Instance.AssetBundlesQuality = QualitySettings.assetBundleQuality;
		}
		GameObject provider = new GameObject("AsyncProvider");
		provider.transform.parent = transform;
		NetworkProvider.Instance.Initialize(provider, BaseURL);
		SceneLoadingProvider = NetworkProvider.Instance.RegisterProvider<SceneLoadingProvider>(provider);
		MissionLoaderProvider = NetworkProvider.Instance.RegisterProvider<MissionLoaderProvider>(provider);
		yield return StartCoroutine(DoLicenseCheckRequest());
		if (m_errorState != null)
		{
			yield return StartCoroutine(ShowErrorPopup(m_errorState));
			yield break;
		}
		yield return StartCoroutine(DoAccountRequests());
		if (m_errorState != null)
		{
			yield return StartCoroutine(ShowErrorPopup(m_errorState));
			yield break;
		}
		yield return StartCoroutine(Co_SupiciousAppVersionCheck());
		WeiboManager.Init();
		SetAndStartCoroutine(Co_InitGameData);
	}

	private IEnumerator Co_SupiciousAppVersionCheck()
	{
		if (Singleton<StateManager>.Instance.ProfileManager.Profile.pirate)
		{
			bool wait = true;
			UIErrorHelper.ShowSupiciousAppVersionCheck(() =>
			{
				wait = false;
			});
			while (wait)
			{
				yield return 0;
			}
		}
	}

	private IEnumerator Co_InitGameData()
	{
		if (!m_HaveStoragePermission)
		{
			ShowStoragePermissionMessage();
		}
		while (!m_HaveStoragePermission)
		{
			yield return null;
		}
		yield return StartCoroutine(Startup.FetchData(BaseURL));
		if (!string.IsNullOrEmpty(Startup.LastError))
		{
			ErrorState errorState = new ErrorState
			{
				ErrorCode = ErrorCode.StartupDataFailed,
				ErrorGroup = ErrorCategory.AcierStartupErrors
			};
			if (Startup.PotentialConnectionIssue)
			{
				yield return StartCoroutine(errorState.DetermineReachabilityError());
			}
			yield return StartCoroutine(ShowErrorPopup(errorState));
			yield break;
		}
		ClientAchievementsManager = new ClientAchievementsManager(gameObject.name);
		if (MATManager.IsMATAvailable)
		{
			MATManager = gameObject.AddComponentIfNotExist<MATManager>();
		}
		if (AdColonyManager.IsAvailable)
		{
			AdColonyManager.Instance.Init();
		}
		EveryplayManager.Initialize();
		GameDB = Startup.GameDB;
		SetAndStartCoroutine(Co_AsyncStartup);
	}

	private IEnumerator Co_AsyncStartup()
	{
		CreateAdditionalDebugMenus();
		yield return StartCoroutine(LocalizationManager.Instance.ChangeLanguageAsync(Startup.Translation));
		yield return new WaitForEndOfFrame();
		if (!UbiCore.Utils.Singleton<GlobalGameIDs>.IsAvailable)
		{
			UbiCore.Utils.Singleton<GlobalGameIDs>.Initialize();
			UbiCore.Utils.Singleton<GlobalGameIDs>.Instance.InitializeIDs(GameDB);
		}
		if (UbiCore.Utils.Singleton<LocalStorage>.IsAvailable)
		{
			UbiCore.Utils.Singleton<LocalStorage>.Instance.BuildIndices();
		}
		Singleton<StateManager>.Instance.EventManager.ForceUpdate();
		EventFlavorThemeManager flavorMgr = (EventFlavorThemeManager)Singleton<StateManager>.Instance.EventManager.GetEventManger(EEventType.Event_Flavor);
		bool pirateThemActivated = flavorMgr.IsThemeEnabled(ThemesManager.EFlavorTheme.Pirate);
		Singleton<StateManager>.Instance.ThemesManager.ActiveTheme = (pirateThemActivated ? ThemesManager.EFlavorTheme.Pirate : ThemesManager.EFlavorTheme.Standard);
		Singleton<StateManager>.Instance.AchievementsManager.InitStatics();
		Singleton<StateManager>.Instance.PersistentStateManager.Initialize();
		Singleton<StateManager>.Instance.CodexManager.Initialize();
		yield return new WaitForEndOfFrame();
		if (ShowLowEndDeviceWarning && (LowMemoryDevice || QualitySettings.lowEndDeviceWarning))
		{
			bool wait = true;
			PopUpButtonConfiguration.ButtonPressHandler handler = (object data) =>
			{
				wait = false;
				Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
			};
			ScreenExtensions.ShowPopUpBareScreen(_config: new PopUpConfiguration
			{
				Heading = LocalizationManager.Instance.Get(5715),
				Aligntarget = UIPopup.EContentAlignTarget.Heading,
				ShowCloseButton = false,
				Data = new ErrorContent.ErrorData
				{
					Description = LocalizationManager.Instance.Get(8017)
				},
				Buttons = new PopUpButtonConfiguration[1]
				{
					new PopUpButtonConfiguration
					{
						Label = LocalizationManager.Instance.Get(1780),
						OnButtonPressed = handler
					}
				}
			}, _manager: Singleton<UIScreenManager>.Instance);
			while (wait)
			{
				yield return 0;
			}
		}
		if (!AudioManager.IsAvailable)
		{
			AudioManager.Init();
			AudioManager.Instance.EventManager.ConnectEvents();
			AudioManager.Instance.Disabled = !BuildDefinition.AudioEnabled;
		}
		UbiCore.Utils.Singleton<UiMain>.Instance.SetUIReadyToUse();
		Singleton<StateManager>.Instance.NewsFeedManager.ProcessFetchedData();
		SetAndStartCoroutine(Co_DownloadData);
	}

	private IEnumerator Co_DownloadData()
	{
		BBList<AssetBundleInfo> bundles = UIDownloadScreen.Instance.GetPreloadBundles(GameDB);
		if (bundles != null)
		{
			yield return StartCoroutine(UIDownloadScreen.Instance.DownloadData(bundles, null));
			if (UIDownloadScreen.Instance.ErrorState != null)
			{
				yield return StartCoroutine(ShowErrorPopup(UIDownloadScreen.Instance.ErrorState));
				yield break;
			}
		}
		yield return StartCoroutine(UIDownloadScreen.Instance.PrepareBundles());
		Singleton<UIScreenManager>.Instance.PurgeManager();
		SetAndStartCoroutine(Co_LoadMission);
	}

	private IEnumerator Co_LoadMission()
	{
		while (UIDownloadScreen.Instance.Details.Cast<UIDownloadScreenDetails>().TweenActive)
		{
			yield return new WaitForSeconds(0.5f);
		}
		if (IsNetworkEnabled)
		{
			UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.InitFromProfile();
			TutorialCategory category = UbiCore.Utils.Singleton<UiMain>.Instance.TutorialManager.GetCategory(ETutorialCategory.TutCatIntroMission);
			if (category != null && !category.Finished)
			{
				yield return StartCoroutine(UILoadingScreen.Instance.LoadMission("Missions/tutorial_0001.bin", _isQuickRestart: false));
				yield break;
			}
		}
		yield return StartCoroutine(UILoadingScreen.Instance.LoadMission(BuildDefinition.AnimusMission, null, _eliteMission: false, _isQuickRestart: false, _isSceneSwitch: true));
	}

	private bool ShouldStartOnline()
	{
		return true;
	}

	private IEnumerator RetrieveDiscoveryService()
	{
		EPlatform platform = EPlatform.GoogleDevice;
		if (!m_UseLocalHost && ShouldStartOnline())
		{
			string discoveryURL = BuildDefinition.Url;
			if (BuildDefinition.UriType == BuildDefinition.EUriType.LocalFileSystem || BuildDefinition.Url.IndexOf("file://", StringComparison.InvariantCultureIgnoreCase) >= 0)
			{
				discoveryURL = "http://acid-dev.ubisoft.org:8330/discovery-1.0-SNAPSHOT-ha-group-trunk/acier/v1/api";
			}
			NetworkDataStore discoveryDataStore = new NetworkDataStore(discoveryURL);
			AsyncDataResult<DiscoveryServiceResponse> discoveryResponse = (AsyncDataResult<DiscoveryServiceResponse>)discoveryDataStore.GetDiscoveryService(BuildDefinition.ServerEnvironment);
			Singleton<StateManager>.Instance.NewsFeedManager.FetchNewDataFromServer(discoveryDataStore);
			while (!discoveryResponse.IsCompleted || !Singleton<StateManager>.Instance.NewsFeedManager.HasFinishedLoading())
			{
				discoveryDataStore.Update();
				yield return new WaitForEndOfFrame();
			}
			if (discoveryResponse.Exception != null)
			{
				yield return StartCoroutine(HandleErrorException(discoveryResponse.Exception));
				yield break;
			}
			DiscoveryServiceResponse discoveryServiceResponse = discoveryResponse.Result();
			if (BuildDefinition.UriType == BuildDefinition.EUriType.LocalFileSystem || BuildDefinition.Url.IndexOf("file://", StringComparison.InvariantCultureIgnoreCase) >= 0)
			{
				BaseURL = BuildDefinition.Url;
			}
			else
			{
				BaseURL = discoveryServiceResponse.cdnUrl;
			}
			ShowLowEndDeviceWarning = discoveryServiceResponse.showLowEndDeviceWarning;
			FacebookAchievementsEnabled = discoveryServiceResponse.fbAchievementsEnabled;
			FacebookEnabled = discoveryServiceResponse.fbEnabled;
			SupportEnabled = discoveryServiceResponse.supportEnabled;
			AdColonyEnabled = discoveryServiceResponse.adColonyEnabled;
			EveryplayManager.HandleDiscoveryServiceResponse(discoveryServiceResponse);
			m_StoreURL = discoveryServiceResponse.serverUrl;
			DeviceManager.UserCountry = discoveryServiceResponse.userGeoIPCountry;
			TrackingManager.AddAppLaunchTrackPoint();
			UplayEnabled = discoveryServiceResponse.uplayEnabled;
			WeiboEnabled = discoveryServiceResponse.weiboEnabled;
			FlurryManager = FlurryAnalyticsManager.Initialize(BuildDefinition.FlurryAPIKey, gameObject);
		}
		else
		{
			BaseURL = BuildDefinition.Url;
		}
	}

	private IEnumerator HandleErrorException(Exception _exception)
	{
		if (_exception != null)
		{
			if (_exception is AcierErrorException acierErrorException)
			{
				m_errorState = new ErrorState
				{
					ErrorCode = acierErrorException.Error.ErrorCode,
					ErrorGroup = acierErrorException.Error.ErrorCategory
				};
				yield return StartCoroutine(m_errorState.DetermineReachabilityError(acierErrorException));
			}
			else
			{
				m_errorState = new ErrorState
				{
					ErrorCode = ErrorCode.UnknownCode,
					ErrorGroup = ErrorCategory.UnknownCategory
				};
			}
		}
	}

	private IEnumerator RetrieveQualitySettings()
	{
		if (ShouldStartOnline())
		{
			NetworkDataStore dataStore = Singleton<StateManager>.Instance.ProfileManager.DataStore;
			AsyncDataResult<GetQualitySettingsResponse> qualitySettingsResponse = (AsyncDataResult<GetQualitySettingsResponse>)dataStore.GetQualitySettings();
			while (!qualitySettingsResponse.IsCompleted)
			{
				dataStore.Update();
				yield return new WaitForEndOfFrame();
			}
			if (qualitySettingsResponse.Exception != null)
			{
				yield return StartCoroutine(HandleErrorException(qualitySettingsResponse.Exception));
				yield break;
			}
			DeviceManager.QualitySettingsName = qualitySettingsResponse.Result().qualitySettingsName;
			QualitySettings = qualitySettingsResponse.Result().qualitySettings;
			QualitySettings.textureType = DeviceManager.GetMostOptimalTextureType();
		}
	}

	private IEnumerator DoStaticRequests()
	{
		if (!ShouldStartOnline())
		{
			yield break;
		}
		Exception exceptionDuringVersionCheck = null;
		NetworkDataStore dataStore = (NetworkDataStore)(ShopDataStore = new NetworkDataStore(_versionCheckErrorCallback: (Exception e) =>
		{
			exceptionDuringVersionCheck = e;
		}, _serverAddress: (!m_UseLocalHost) ? m_StoreURL : m_LocalhostURL, _checkVersion: true));
		while (!dataStore.VersionChecked && exceptionDuringVersionCheck == null)
		{
			dataStore.Update();
			yield return new WaitForEndOfFrame();
		}
		if (exceptionDuringVersionCheck != null)
		{
			yield return StartCoroutine(HandleErrorException(exceptionDuringVersionCheck));
			yield break;
		}
		Singleton<StateManager>.Instance.ProfileManager.SetDataStore(dataStore);
		ProfileManager profileMgr = Singleton<StateManager>.Instance.ProfileManager;
		WaitForEndOfFrame waitForEnd = new WaitForEndOfFrame();
		bool staticsFinished = false;
		bool errorOccurred = false;
		profileMgr.GetStatics((IAsyncResult _result) =>
		{
			if (_result.IsError())
			{
				errorOccurred = true;
				m_errorState = ((_result.GetException() == null) ? m_errorState : _result.GetException().Error.ErrorState);
			}
			staticsFinished = true;
			ShopDataStore.RecoverPurchases();
		});
		while (!staticsFinished && !errorOccurred)
		{
			dataStore.Update();
			yield return waitForEnd;
		}
	}

	private IEnumerator DoLicenseCheckRequest()
	{
		bool wait = true;
		WaitForEndOfFrame waitForEnd = new WaitForEndOfFrame();
		LicenseChecker.StartLicenseCheck((string _signedData, string _signature, ErrorState _errorState) =>
		{
			SignedData = _signedData;
			Signature = _signature;
			wait = false;
			m_errorState = _errorState;
		});
		while (wait)
		{
			yield return waitForEnd;
		}
	}

	private IEnumerator DoAccountRequests()
	{
		if (ShouldStartOnline())
		{
			WaitForEndOfFrame waitForEnd = new WaitForEndOfFrame();
			NetworkDataStore dataStore = Singleton<StateManager>.Instance.ProfileManager.DataStore;
			dataStore.FirstLogin();
			while (dataStore.State() == ESessionState.SessionStateOpening)
			{
				dataStore.Update();
				yield return waitForEnd;
			}
			if (dataStore.State() == ESessionState.SessionLcf)
			{
				m_errorState = new ErrorState
				{
					ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_GO_TO_STORE,
					ErrorGroup = ErrorCategory.LICENSE_CHECK
				};
			}
			else if (dataStore.State() != ESessionState.SessionStateOpen)
			{
				m_errorState = new ErrorState
				{
					ErrorCode = ErrorCode.SesssionNotOpen,
					ErrorGroup = ErrorCategory.AcierStartupErrors
				};
			}
			else
			{
				IsNetworkEnabled = true;
				ShopDataStore.SetGameObjectName(gameObject.name);
				ShopDataStore.SetApplicationUsername(Singleton<StateManager>.Instance.ProfileManager.Profile.id);
				Singleton<StateManager>.Instance.MailManager.Init();
			}
		}
	}

	private IEnumerator ShowErrorPopup(ErrorState _error)
	{
		if (_error != null)
		{
			bool wait = true;
			Singleton<UIScreenManager>.Instance.ShowPopUpScreen(UbiCore.Utils.Singleton<UiMain>.Instance.GetErrorPopUpConfiguration(_error.ErrorCode, _error.ErrorGroup, delegate
			{
				wait = false;
				Singleton<UIScreenManager>.Instance.HideCurrentPopupScreen();
			}));
			while (wait)
			{
				yield return 0;
			}
			yield return StartCoroutine(m_currentStartupCo());
		}
	}

	public void SwitchScene(string _missionUrl, bool _isQuickRestart)
	{
		StartCoroutine(UILoadingScreen.Instance.LoadMission(_missionUrl, _isQuickRestart));
	}

	public void LoadMission(MissionFile _contentProto, MissionGoals _missionGoals, bool _eliteMission)
	{
		StartCoroutine(UILoadingScreen.Instance.LoadMission(_contentProto, _missionGoals, _eliteMission, _isQuickRestart: false, _isSceneSwitch: true));
	}

	public void FastLoadMission(MissionFile _contentProto, MissionGoals _missionGoals, bool _eliteMission)
	{
		StartCoroutine(UILoadingScreen.Instance.LoadMission(_contentProto, _missionGoals, _eliteMission, _isQuickRestart: false, _isSceneSwitch: false));
	}

	public void RetryCurrentMission()
	{
		StartCoroutine(UILoadingScreen.Instance.RetryMission());
	}

	public void LateUpdate()
	{
		if (m_isInitializeStartupCompleted)
		{
			if (IsNetworkEnabled)
			{
				Singleton<StateManager>.Instance.ProfileManager.Update();
			}
			Profiling.Start("Main::Update", 20f);
			TimeScaleManager.Step();
			UbiCore.Utils.Singleton<Timer>.Instance.Step();
			AutoUpdateManager.UpdateFrame(UbiCore.Utils.Singleton<Timer>.Instance.deltaTime);
			if (UbiCore.Utils.Singleton<MissionManager>.Instance != null)
			{
				UbiCore.Utils.Singleton<MissionManager>.Instance.Update();
			}
			Profiling.StopAndLogBreach();
			CheckSupiciousAppVersionCheck();
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				GenericAndroidBackKey.HandleBackKey();
			}
		}
	}

	private void CheckSupiciousAppVersionCheck()
	{
		if (!SceneLoader.InAnimus || !(Singleton<UIScreenManager>.Instance.CurrentModalScreen == null))
		{
			return;
		}
		ProfileManager profileManager = Singleton<StateManager>.Instance.ProfileManager;
		if (profileManager.Profile.pirate && (DateTime.UtcNow - m_lastCheckedDateTime).TotalSeconds > 300.0)
		{
			UIErrorHelper.ShowSupiciousAppVersionCheck(() =>
			{
				m_lastCheckedDateTime = DateTime.UtcNow;
			});
		}
	}

	[Conditional("UNITY_DEBUG")]
	[Conditional("UNITY_RELEASE")]
	private void DebugUpdate()
	{
		if (Input.GetKeyDown(KeyCode.F2) || (Input.touchCount == 4 && Input.touchCount != m_lastTouch))
		{
			if (DebugMenu.IsVisible)
			{
				m_debugMenu.CloseDebugMenu();
			}
			else
			{
				m_debugMenu.OpenDebugMenu();
			}
		}
		UICamera uICamera = ((!(UICamera.mainCamera != null)) ? null : UICamera.mainCamera.GetComponent<UICamera>());
		if (uICamera != null)
		{
			uICamera.useTouch = (uICamera.useMouse = !DebugMenu.IsVisible);
		}
		m_lastTouch = Input.touchCount;
	}

	protected override void OnGUI()
	{
		base.OnGUI();
		if (ShowConfidentialWarning)
		{
			GUIStyle gUIStyle = new GUIStyle(GUI.skin.box);
			gUIStyle.alignment = TextAnchor.MiddleCenter;
			gUIStyle.fontSize = Mathf.RoundToInt((float)Mathf.Min(Screen.height, Screen.width) * 0.035f);
			gUIStyle.normal.textColor = Color.white;
			GUILayout.BeginArea(new Rect(0f, (float)Screen.height * 0.03f, Screen.width, (float)Screen.height * 0.05f));
			GUILayout.FlexibleSpace();
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.Label("Confidential - do not distribute without permission", gUIStyle, GUILayout.ExpandWidth(expand: false));
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.EndArea();
		}
	}

	public void OnNativeShopMessage(string _jsonMessage)
	{
		if (ShopDataStore != null)
		{
			ShopDataStore.OnNativeShopMessage(_jsonMessage);
		}
	}

	public void OnNativeLicenseCheckResponse(string _jsonResponse)
	{
		LicenseChecker.ProcessResponse(_jsonResponse);
	}

	public void OnNativeGGPServicesCallback(string _message)
	{
		ClientAchievementsManager.OnNativeGGPServicesCallback(_message);
	}

	public void OnNativeStoragePermissionResponse(string _result)
	{
		DevicePermissionHelper.WaitForRequestPermissionResponse = false;
	}

	public void OnAppLifeCycleMessage(string _jsonMessage)
	{
	}

	private void ValidateUnityEditorVersion()
	{
	}

	public static void ShowStoragePermissionMessage()
	{
		PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
		popUpConfiguration.Heading = string.Empty;
		popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Heading;
		popUpConfiguration.ShowCloseButton = false;
		popUpConfiguration.Data = new ErrorContent.ErrorData
		{
			Description = LocalizationManager.Instance.Get(8251)
		};
		popUpConfiguration.Buttons = new PopUpButtonConfiguration[1]
		{
			new PopUpButtonConfiguration
			{
				ButtonType = PopUpButtonConfiguration.EButtonType.eNormal,
				Label = LocalizationManager.Instance.Get(7272),
				OnButtonPressed = (object _) =>
				{
					UIUtil.QuitApplication();
				}
			}
		};
		PopUpConfiguration config = popUpConfiguration;
		m_ExternalStoragePopupShown = true;
		Singleton<UIScreenManager>.Instance.ShowPopUpScreen(config);
	}
}
