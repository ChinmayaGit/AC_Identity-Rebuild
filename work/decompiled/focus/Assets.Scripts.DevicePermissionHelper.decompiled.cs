using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Native;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.ScreenManager;
using Assets.Scripts.UI.Screens.Popup;
using Assets.Scripts.UI.Screens.Popup.ErrorPopup;
using Assets.Scripts.UI.Types;
using UnityEngine;

namespace Assets.Scripts;

public class DevicePermissionHelper
{
	private enum EPermissionMessageType
	{
		ExplainPermissionNeeded,
		PermissionWasDeniedButNeeded,
		RequestActivatePermissionInSettings,
		Quit
	}

	public static bool WaitForRequestPermissionResponse;

	public static bool ForceShowTimer;

	private static bool s_appSettingsShown;

	private static bool s_showNativeStoragePermissionPopup;

	private static UIPopup s_uiPopup;

	public static void OnApplicationPause(bool _pause)
	{
		if (s_appSettingsShown && !_pause)
		{
			s_appSettingsShown = false;
			if (!NativeTools.HaveStoragePermission)
			{
				ShowPermissionMessagePopup(NativeTools.ShouldShowRequestStoragePermissionRationale ? EPermissionMessageType.PermissionWasDeniedButNeeded : EPermissionMessageType.RequestActivatePermissionInSettings);
			}
		}
	}

	public static IEnumerator Co_Initialize_Startup()
	{
		if (!NativeTools.HaveStoragePermission)
		{
			UIRoot root = CreateScene();
			yield return Main.Instance.StartCoroutine(ShowGrantAccessPopup());
			root.gameObject.SetActive(value: false);
			NGUITools.Destroy(root);
			s_uiPopup = null;
		}
	}

	private static UIRoot CreateScene()
	{
		UIPanel uIPanel = NGUITools.CreateUI(advanced3D: false, LayerMask.NameToLayer("UIShared"));
		UIRoot root = uIPanel.root;
		root.scalingStyle = UIRoot.Scaling.Constrained;
		root.manualHeight = 1536;
		s_uiPopup = CreateBarePopup("UI/Animus/Screens/UIPopupBare", "ErrorTest", root.transform);
		return root;
	}

	private static UIPopup CreateBarePopup(UIPath _path, string _name, Transform _parent)
	{
		UIPopup result = null;
		GameObject gameObject = NGUITools.AddChild(_parent.gameObject, Resources.Load(_path) as GameObject);
		if (gameObject != null)
		{
			UIScreen component = gameObject.GetComponent<UIScreen>();
			if (component != null)
			{
				component.Init();
			}
			gameObject.name = _name;
			result = gameObject.GetComponent<UIPopup>();
			UIPanel component2 = gameObject.GetComponent<UIPanel>();
			component2.SetAnchor(_parent.gameObject, 0, 0, 0, 0);
		}
		return result;
	}

	private static IEnumerator ShowGrantAccessPopup()
	{
		ShowPermissionMessagePopup(EPermissionMessageType.ExplainPermissionNeeded);
		WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
		while (!NativeTools.HaveStoragePermission)
		{
			yield return endOfFrame;
		}
	}

	private static void ShowPermissionMessagePopup(EPermissionMessageType _type)
	{
		ForceShowTimer = false;
		s_showNativeStoragePermissionPopup = false;
		string heading = LocalizationManager.Instance.Get(10707);
		string description = string.Empty;
		List<PopUpButtonConfiguration> list = new List<PopUpButtonConfiguration>();
		switch (_type)
		{
		case EPermissionMessageType.ExplainPermissionNeeded:
			if (NativeTools.IsTVDevice)
			{
				s_showNativeStoragePermissionPopup = true;
				ForceShowTimer = true;
			}
			description = LocalizationManager.Instance.Get(10704);
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(1780),
				OnButtonPressed = (object _) =>
				{
					HidePopupScreen();
					RequestStoragePermission();
				}
			});
			break;
		case EPermissionMessageType.PermissionWasDeniedButNeeded:
			if (NativeTools.IsTVDevice)
			{
				s_showNativeStoragePermissionPopup = true;
				ForceShowTimer = true;
			}
			description = LocalizationManager.Instance.Get(10705);
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(1199),
				OnButtonPressed = (object _) =>
				{
					HidePopupScreen();
					RequestStoragePermission();
				}
			});
			if (!ForceShowTimer)
			{
				list.Add(new PopUpButtonConfiguration
				{
					Data = true,
					Label = LocalizationManager.Instance.Get(7272),
					OnButtonPressed = (object _) =>
					{
						HidePopupScreen();
						ShowPermissionMessagePopup(EPermissionMessageType.Quit);
					}
				});
			}
			break;
		case EPermissionMessageType.RequestActivatePermissionInSettings:
			if (NativeTools.IsTVDevice)
			{
				s_showNativeStoragePermissionPopup = false;
				ForceShowTimer = true;
			}
			description = LocalizationManager.Instance.Get(10706);
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(1199),
				OnButtonPressed = (object _) =>
				{
					HidePopupScreen();
					OpenAppInDeviceSettings();
				}
			});
			if (!ForceShowTimer)
			{
				list.Add(new PopUpButtonConfiguration
				{
					Data = true,
					Label = LocalizationManager.Instance.Get(7272),
					OnButtonPressed = (object _) =>
					{
						HidePopupScreen();
						ShowPermissionMessagePopup(EPermissionMessageType.Quit);
					}
				});
			}
			break;
		case EPermissionMessageType.Quit:
			heading = LocalizationManager.Instance.Get(7271);
			description = LocalizationManager.Instance.Get(7274);
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(7272),
				OnButtonPressed = (object _) =>
				{
					HidePopupScreen();
					Application.Quit();
				}
			});
			list.Add(new PopUpButtonConfiguration
			{
				Data = true,
				Label = LocalizationManager.Instance.Get(3904),
				OnButtonPressed = (object _) =>
				{
					HidePopupScreen();
					ShowPermissionMessagePopup(NativeTools.ShouldShowRequestStoragePermissionRationale ? EPermissionMessageType.PermissionWasDeniedButNeeded : EPermissionMessageType.RequestActivatePermissionInSettings);
				}
			});
			break;
		}
		PopUpConfiguration popUpConfiguration2;
		if (ForceShowTimer)
		{
			PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
			popUpConfiguration.TimerDuration = 10f;
			popUpConfiguration.OnTimerRanOut = OnTimerRanOut;
			popUpConfiguration.Heading = heading;
			popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Heading;
			popUpConfiguration.ShowCloseButton = false;
			popUpConfiguration.Data = new ErrorContent.ErrorData
			{
				Description = description
			};
			popUpConfiguration2 = popUpConfiguration;
		}
		else
		{
			PopUpConfiguration popUpConfiguration = new PopUpConfiguration();
			popUpConfiguration.Heading = heading;
			popUpConfiguration.Aligntarget = UIPopup.EContentAlignTarget.Heading;
			popUpConfiguration.ShowCloseButton = false;
			popUpConfiguration.Data = new ErrorContent.ErrorData
			{
				Description = description
			};
			popUpConfiguration2 = popUpConfiguration;
		}
		popUpConfiguration2.Buttons = list.ToArray();
		popUpConfiguration2.DeactivateButtonsOnTap = false;
		ShowPopupScreen(popUpConfiguration2);
	}

	private static void OnTimerRanOut()
	{
		HidePopupScreen();
		if (s_showNativeStoragePermissionPopup)
		{
			RequestStoragePermission();
		}
		else
		{
			OpenAppInDeviceSettings();
		}
	}

	private static void OpenAppInDeviceSettings()
	{
		s_appSettingsShown = true;
		NativeTools.OpenAppInDeviceSettings();
	}

	private static void RequestStoragePermission()
	{
		Main.Instance.StartCoroutine(DoRequestStoragePermission());
	}

	private static IEnumerator DoRequestStoragePermission()
	{
		WaitForRequestPermissionResponse = true;
		WaitForEndOfFrame waitForEnd = new WaitForEndOfFrame();
		NativeTools.RequestStoragePermission();
		while (WaitForRequestPermissionResponse)
		{
			yield return waitForEnd;
		}
		bool showRationale = NativeTools.ShouldShowRequestStoragePermissionRationale;
		if (!NativeTools.HaveStoragePermission)
		{
			ShowPermissionMessagePopup(showRationale ? EPermissionMessageType.PermissionWasDeniedButNeeded : EPermissionMessageType.RequestActivatePermissionInSettings);
		}
	}

	private static void ShowPopupScreen(PopUpConfiguration _config)
	{
		s_uiPopup.Configure(_config);
	}

	private static void HidePopupScreen()
	{
		ForceShowTimer = false;
		s_uiPopup.DeActivate();
	}
}
