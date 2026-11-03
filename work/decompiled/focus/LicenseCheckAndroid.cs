using System.Collections.Generic;
using AcierProtocol;
using MiniJSON;
using UnityEngine;

namespace Assets.Scripts.LicenseCheck;

public class LicenseCheckAndroid : LicenseCheckBase
{
	private static AndroidJavaObject m_currentActivity;

	private static AndroidJavaObject ms_nativeInstance;

	private static AndroidJavaObject CurrentActivity
	{
		get
		{
			AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
			return m_currentActivity ?? (m_currentActivity = androidJavaClass.GetStatic<AndroidJavaObject>("currentActivity"));
		}
	}

	private static AndroidJavaObject NativeInstance
	{
		get
		{
			AndroidJavaClass androidJavaClass = new AndroidJavaClass("com.acid.plugins.license.BBLicenseCheckBridge");
			return ms_nativeInstance ?? (ms_nativeInstance = androidJavaClass.CallStatic<AndroidJavaObject>("_Instance", new object[0]));
		}
	}

	public override void StartLicenseCheck(LicenseCheckCallback _callback = null)
	{
		base.StartLicenseCheck(_callback);
		CurrentActivity.Call("runOnUiThread", (AndroidJavaRunnable)(() =>
		{
			NativeInstance.Call("_PerformLC");
		}));
	}

	public override void ProcessResponse(string _jsonResponse)
	{
		base.ProcessResponse(_jsonResponse);
		Dictionary<string, object> dictionary = Json.Deserialize(_jsonResponse) as Dictionary<string, object>;
		string signature = string.Empty;
		string signedData = string.Empty;
		if (dictionary.ContainsKey("kdsst"))
		{
			signature = (string)dictionary["kdsst"];
		}
		if (dictionary.ContainsKey("kdsdd"))
		{
			signedData = (string)dictionary["kdsdd"];
		}
		int num = int.Parse((string)dictionary["response-code"]);
		ELicenseCheckResponseCode eLicenseCheckResponseCode = (ELicenseCheckResponseCode)num;
		ErrorState errorState = null;
		switch (eLicenseCheckResponseCode)
		{
		case ELicenseCheckResponseCode.NOT_LICENSED:
		{
			ErrorState errorState2 = new ErrorState();
			errorState2.ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_GO_TO_STORE;
			errorState2.ErrorGroup = ErrorCategory.LICENSE_CHECK;
			errorState = errorState2;
			break;
		}
		case ELicenseCheckResponseCode.ERROR_INVALID_PACKAGE_NAME:
		{
			ErrorState errorState2 = new ErrorState();
			errorState2.ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_ERROR_INVALID_PACKAGE_NAME;
			errorState2.ErrorGroup = ErrorCategory.LICENSE_CHECK;
			errorState = errorState2;
			break;
		}
		case ELicenseCheckResponseCode.ERROR_NOT_MATCHING_UID:
		{
			ErrorState errorState2 = new ErrorState();
			errorState2.ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_ERROR_NOT_MATCHING_UID;
			errorState2.ErrorGroup = ErrorCategory.LICENSE_CHECK;
			errorState = errorState2;
			break;
		}
		case ELicenseCheckResponseCode.ERROR_NOT_MARKET_MANAGED:
		{
			ErrorState errorState2 = new ErrorState();
			errorState2.ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_ERROR_NOT_MARKET_MANAGED;
			errorState2.ErrorGroup = ErrorCategory.LICENSE_CHECK;
			errorState = errorState2;
			break;
		}
		default:
		{
			ErrorState errorState2 = new ErrorState();
			errorState2.ErrorCode = ErrorCode.LICENSE_CHECK_FAILED_CANNOT_CONNECT;
			errorState2.ErrorGroup = ErrorCategory.LICENSE_CHECK;
			errorState = errorState2;
			break;
		}
		case ELicenseCheckResponseCode.LICENSED:
			break;
		}
		CompleteLicenseCheck(eLicenseCheckResponseCode, signedData, signature, errorState);
	}
}
