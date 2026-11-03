using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using AcierProtocol;
using Assets.Scripts.Native;
using Assets.Scripts.Tracking;
using Assets.Scripts.UI.Localization;
using Assets.Scripts.UI.Screens.ModuleScreen;
using Assets.Scripts.UI.State;
using BB.Mobile.Net;
using BB.Mobile.Net.Http;
using MiniJSON;
using UnityEngine;

public class PushNotificationsProvider : MonoBehaviour
{
	public delegate void OnMsgCode(bool _success, int _state, int _msgcode);

	public const int STATE_REGISTER_PUSH = 1;

	public const int STATE_UNREGISTER_PUSH = 2;

	public const int STATE_REGISTER_DEVICE_TO_HOUSTON = 3;

	public const int STATE_CHECK_APS_ENVIRONMENT = 4;

	public const int STATE_SET_GCM_VARIABLES = 1001;

	public const int RESULT_ERROR = 0;

	public const int RESULT_OK = 1;

	public const int RESULT_SET_GCM_VARIABLES_DONE = 1;

	public const int RESULT_CONTEXT_NOT_FOUND = 2;

	public const int RESULT_REGISTER_PUSH_FAILED_NO_GOOGLE_PLAY_SERVICES_APK = 99;

	public const int RESULT_REGISTER_PUSH_SUCESSFUL = 100;

	public const int RESULT_REGISTER_PUSH_FAILED = 101;

	public const int RESULT_REGISTER_PUSH_DEPRECATED = 102;

	public const int RESULT_UNREGISTER_PUSH_DONE = 103;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_SUCESSFUL = 104;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_REQUEST_FAILED = 105;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_FAILED_PAYLOAD_EMPTY = 106;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_FAILED_PAYLOAD_ERROR = 107;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_FAILED_ERROR_UNKNOWN = 108;

	public const int RESULT_REGISTER_DEVICE_TO_HOUSTON_ERROR_DESERIALIZING_PAYLOAD = 109;

	public const int RESULT_APS_ENVIRONMENT_PRODUCTION = 110;

	public const int RESULT_APS_ENVIRONMENT_DEVELOPMENT = 111;

	public const int RESULT_APS_ENVIRONMENT_NOT_CONFIGURED = 112;

	public const int RESULT_SCHEDULE_LOCAL_NOTIFICATION_SUCCESSFUL = 2001;

	public const int RESULT_SCHEDULE_LOCAL_NOTIFICATION_FAILED = 2002;

	public const int RESULT_CANCEL_LOCAL_NOTIFICATION_SUCCESSFUL = 2003;

	public const int RESULT_CANCEL_LOCAL_NOTIFICATION_FAILED = 2004;

	private HttpClient m_httpClient = new HttpClient(null, null, useSignatureVerification: false, 60000, 1);

	public static bool isRegisteredToHoustonPNS;

	private static IPushNotifications m_notificationsManager;

	private static PushNotificationsProvider m_mgrInstance;

	public static bool isRegisteredToClientPNS
	{
		get
		{
			return PlayerPrefs.GetInt("IsRegisteredToClientPNS", 1) == 1;
		}
		set
		{
			PlayerPrefs.SetInt("IsRegisteredToClientPNS", value ? 1 : 0);
		}
	}

	public event OnMsgCode PushMsgCodeCallback;

	public static void Init()
	{
		if (Application.platform == RuntimePlatform.Android)
		{
			m_notificationsManager = new PushNotificationsAndroid();
			m_notificationsManager.SetPushObjectName(CreateGameObject().gameObject.name);
			m_notificationsManager.InitializeInstance();
		}
	}

	private static PushNotificationsProvider CreateGameObject()
	{
		if (m_mgrInstance == null)
		{
			GameObject gameObject = new GameObject("PushNotificationGameObject");
			if (gameObject != null)
			{
				m_mgrInstance = gameObject.AddComponent<PushNotificationsProvider>();
			}
			UnityEngine.Object.DontDestroyOnLoad(gameObject);
		}
		return m_mgrInstance;
	}

	public static void ScheduleLocalNotificationWithInterval(long interval, string title, string alertBody, string alertKey, EPushNotificationGroup _type)
	{
		if (IsPushNotificationGroupEnabled(_type) && interval > 0 && m_notificationsManager != null)
		{
			m_notificationsManager.ScheduleLocalNotificationWithInterval(interval, title, alertBody, alertKey, _type);
		}
	}

	public static void CancelLocalNotificationWithKey(string alertKey)
	{
		if (m_notificationsManager != null)
		{
			m_notificationsManager.CancelLocalNotificationWithKey(alertKey);
		}
	}

	private static bool IsPushNotificationGroupEnabled(EPushNotificationGroup _type)
	{
		PushNotificationSetting[] pushNotificationSettings = Singleton<StateManager>.Instance.ProfileManager.Profile.userSettings.pushNotificationSettings;
		PushNotificationSetting[] array = pushNotificationSettings;
		foreach (PushNotificationSetting pushNotificationSetting in array)
		{
			if (_type == pushNotificationSetting.pushNotificationGroup)
			{
				return pushNotificationSetting.value;
			}
		}
		return true;
	}

	private void BeginRegisterDeviceToHouston()
	{
		if (m_notificationsManager != null && m_notificationsManager.GetHoustonURL() != string.Empty)
		{
			m_httpClient.BeginReadRequest(new Uri(m_notificationsManager.GetHoustonURL()), null, new StreamPayload(new MemoryStream(), autoClose: false), EndRegisterDeviceToHouston, null, null);
		}
	}

	private void EndRegisterDeviceToHouston(IAsyncResult asyncResult)
	{
		StreamPayload streamPayload = null;
		try
		{
			HttpResult httpResult = m_httpClient.EndRequest(asyncResult);
			if (httpResult.Succeeded || httpResult.StatusCode == HttpStatusCode.OK)
			{
				streamPayload = httpResult.Payload as StreamPayload;
			}
			else
			{
				MessageCodeResult(_success: false, 3, 105);
			}
		}
		catch (Exception)
		{
			MessageCodeResult(_success: false, 3, 108);
		}
		if (streamPayload == null)
		{
			MessageCodeResult(_success: false, 3, 106);
			return;
		}
		Stream content = streamPayload.Content;
		content.Seek(0L, SeekOrigin.Begin);
		StreamReader streamReader = new StreamReader(content, detectEncodingFromByteOrderMarks: true);
		string json = streamReader.ReadToEnd();
		try
		{
			bool flag = true;
			if (Json.Deserialize(json) is Dictionary<string, object> dictionary)
			{
				Dictionary<string, object> deviceDict = (Dictionary<string, object>)dictionary["device"];
				if (deviceDict != null)
				{
					SharedMain.SafeUnityInvoke((Action)(() =>
					{
						string value = (string)deviceDict["federated_id"];
						NativeTools.SetStringInKeyChain("federated_id", value);
						string value2 = (string)deviceDict["ubimobi_access_token"];
						NativeTools.SetStringInKeyChain("ubimobi_access_token", value2);
					}));
				}
				else
				{
					flag = false;
				}
			}
			else
			{
				flag = false;
			}
			if (flag)
			{
				MessageCodeResult(_success: true, 3, 104);
			}
			else
			{
				MessageCodeResult(_success: false, 3, 107);
			}
		}
		catch (Exception)
		{
			MessageCodeResult(_success: false, 3, 109);
		}
	}

	public void MessageCodeResult(bool _success, int _state, int _msgcode)
	{
		if (PushMsgCodeCallback != null)
		{
			PushMsgCodeCallback(_success, _state, _msgcode);
		}
		if (_success)
		{
			switch (_msgcode)
			{
			case 1:
				m_notificationsManager.RegisterToPushNotifications();
				break;
			case 100:
				isRegisteredToClientPNS = true;
				BeginRegisterDeviceToHouston();
				break;
			case 103:
				isRegisteredToClientPNS = false;
				break;
			case 104:
				SharedMain.SafeUnityInvoke((Action)(() =>
				{
				}));
				isRegisteredToHoustonPNS = true;
				break;
			case 110:
			case 111:
				if (m_notificationsManager != null)
				{
					m_notificationsManager.RegisterToPushNotifications();
				}
				break;
			}
		}
		else
		{
			switch (_state)
			{
			case 1:
				isRegisteredToClientPNS = false;
				break;
			case 3:
				isRegisteredToHoustonPNS = false;
				break;
			case 2:
				break;
			}
		}
	}

	public void OnPushNativeMessage(string _jsonMessage)
	{
		if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
		{
			Dictionary<string, object> dictionary = Json.Deserialize(_jsonMessage) as Dictionary<string, object>;
			int state = int.Parse((string)dictionary["current-state"]);
			int num = int.Parse((string)dictionary["status"]);
			int msgcode = int.Parse((string)dictionary["message"]);
			MessageCodeResult(num == 1, state, msgcode);
		}
	}

	public void onLocalNotificationsReceived(string payload)
	{
		TrackingManager.AddPushNotificationTrackPoint(_isServerTriggered: false);
		Dictionary<string, object> dictionary = Json.Deserialize(payload) as Dictionary<string, object>;
		if (Application.platform == RuntimePlatform.IPhonePlayer)
		{
			string text = (string)dictionary["alertBody"];
			string text2 = (string)dictionary["alertKey"];
		}
		else if (Application.platform == RuntimePlatform.Android)
		{
			string text3 = (string)dictionary["notificationType"];
			string text4 = (string)dictionary["notificationId"];
		}
	}

	public void onPushNotificationsReceived(string payload)
	{
		TrackingManager.AddPushNotificationTrackPoint(_isServerTriggered: true);
	}

	public static string GetLanguageForHouston()
	{
		return LocalizationManager.Instance.CurrentLanguage switch
		{
			ELanguages.de_DE => "de", 
			ELanguages.es_ES => "es", 
			ELanguages.it_IT => "it", 
			ELanguages.ja_JP => "ja", 
			ELanguages.ko_KR => "ko", 
			ELanguages.pt_BR => "pt", 
			ELanguages.ru_RU => "ru", 
			ELanguages.zh_CHS => "zh", 
			ELanguages.zh_CHT => "zh", 
			ELanguages.fr_FR => "fr", 
			ELanguages.pl_PL => "pl", 
			_ => "en", 
		};
	}

	public static void ScheduleNotificationsForNewlyActivatedModule(string _assassinId, ModuleData _newlyActivatedModule)
	{
		if (IsPushNotificationGroupEnabled(EPushNotificationGroup.BoostGroup) && _newlyActivatedModule.durationType == EDurationType.Time)
		{
			string alertKey = _assassinId + "::Boost::12Hr";
			string alertKey2 = _assassinId + "::Boost::30Min";
			string alertKey3 = _assassinId + "::Boost::Expired";
			long num = 43200L;
			long num2 = 1800L;
			long num3 = _newlyActivatedModule.ComputeRemainingMilliSeconds() / 1000;
			if (num3 - num <= 0)
			{
				CancelLocalNotificationWithKey(alertKey);
			}
			else
			{
				long interval = num3 - num;
				ScheduleLocalNotificationWithInterval(interval, LocalizationManager.Instance.Get(6452), LocalizationManager.Instance.Get(5702), alertKey, EPushNotificationGroup.BoostGroup);
			}
			if (num3 - num2 <= 0)
			{
				CancelLocalNotificationWithKey(alertKey2);
			}
			else
			{
				long interval2 = num3 - num2;
				ScheduleLocalNotificationWithInterval(interval2, LocalizationManager.Instance.Get(6452), LocalizationManager.Instance.Get(5702), alertKey2, EPushNotificationGroup.BoostGroup);
			}
			ScheduleLocalNotificationWithInterval(num3, LocalizationManager.Instance.Get(6452), LocalizationManager.Instance.Get(3524), alertKey3, EPushNotificationGroup.BoostGroup);
		}
	}
}
