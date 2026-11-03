using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AcierNetwork;
using AcierNetwork.Communication;
using AcierNetwork.Provider;
using AcierProtocol;
using AssetBundleData;
using Assets.Scripts;
using Assets.Scripts.Crowd;
using Assets.Scripts.Database;
using Assets.Scripts.Engine;
using Assets.Scripts.Mission.UI;
using Assets.Scripts.Network.Missions;
using Assets.Scripts.Network.Missions.Mission.Data;
using Assets.Scripts.Tracking;
using Assets.Scripts.UI;
using Database;
using SharedBaseLib.Core;
using SharedBaseLib.Serialization;
using SharedBaseLib.System.Cache;
using UbiCore.Helpers;
using UbiCore.Utils;
using UnityEngine;

public class Startup : MonoBehaviour
{
	public static readonly BBList<string> s_PreloadedScenes = new BBList<string>
	{
		"AnimusGlobe", "TutorialScene_01", "TutorialScene_02", "TutorialScene_03", "Firenze_palazzo_daytime", "Firenze_palazzo_nighttime", "Firenze_santacroce_stormy", "Firenze_santacroce_sunny", "Firenze_santacroce_Tutorial", "MonteRiggioni_DayTime",
		"MonteRiggioni_NightTime", "Roma_Castello_SantAngelo_Overcast", "Roma_Castello_SantAngelo_stormy", "Roma_Coloseum_Afternoon", "Roma_Coloseum_Foggy", "Forli_dusk", "Forli_siege"
	};

	private FirstContact m_firstContacts;

	public GameDB GameDB { get; private set; }

	public bool PotentialConnectionIssue { get; private set; }

	public string LastError { get; private set; }

	public TranslationsDef Translation { get; private set; }

	public EQualityAssetBundle Quality { get; private set; }

	private string BaseURL { get; set; }

	public BBDictionary<string, SceneAssetBundle> SceneDictionary { get; private set; }

	public BBHashSet<AssetBundleInfo> PreloadBundles { get; private set; }

	public AcierProtocol.QualitySettings QualitySettings { get; private set; }

	public IEnumerator FetchData(string _baseUrl)
	{
		PotentialConnectionIssue = false;
		LastError = string.Empty;
		BaseURL = _baseUrl;
		yield return StartCoroutine(RetrieveFirstContactData());
		if (!string.IsNullOrEmpty(LastError))
		{
			yield break;
		}
		ApplyRenderQuality();
		uint mem = Profiler.GetMonoUsedSize();
		FirstContact.DataFile dataFile = m_firstContacts.GetDataFile(GameFolders.GetGameDBFilename((GameFolders.ETextureType)QualitySettings.textureType));
		string url = GameFolders.GetAcierDataURL(_baseUrl, dataFile.Filename);
		SimpleItem cacheEntry = UbiCore.Utils.Singleton<Manager>.Instance.GetEntry(url, (uint)dataFile.Hash.GetHashCode()) as SimpleItem;
		GameDB = new GameDB(cacheEntry.CacheFile, Quality);
		mem = Profiler.GetMonoUsedSize() - mem;
		ReadEnvironmentDicFromCache();
		yield return StartCoroutine(RetrieveLocaData());
		if (string.IsNullOrEmpty(LastError))
		{
			yield return StartCoroutine(RetrieveDownloadScreenData());
			if (string.IsNullOrEmpty(LastError))
			{
			}
		}
	}

	private void ApplyRenderQuality()
	{
		QualitySettings = Main.Instance.QualitySettings;
		if (QualitySettings == null)
		{
			if (Main.Instance.IsNetworkEnabled)
			{
			}
			SetForcastedQuality();
		}
		RenderingSettings instance = RenderingSettings.Instance;
		UnityEngine.QualitySettings.SetQualityLevel(QualitySettings.qualityLevel, applyExpensiveChanges: true);
		instance.shaderLOD = (int)QualitySettings.renderingLOD;
		Quality = QualitySettings.assetBundleQuality;
		Crowd.CrowdQuality = QualitySettings.crowdQuality;
		if (QualitySettings.downScaling && QualitySettings.qualityLevel <= 3)
		{
			UnityEngine.QualitySettings.shadowDistance = 0f;
		}
	}

	private void SetForcastedQuality()
	{
		ApplyQualityProfile(QualitySettingsHelper.CurrentDeviceProfile);
	}

	private void ApplyQualityProfile(QualitySettingsHelper.DeviceProfile _profile)
	{
		if (QualitySettingsHelper.FallbackQualityProfiles.ContainsKey((int)_profile))
		{
			QualitySettings = QualitySettingsHelper.FallbackQualityProfiles[(int)_profile];
		}
	}

	private IEnumerator RetrieveFirstContactData()
	{
		m_firstContacts = new FirstContact();
		string url = GameFolders.GetAcierDataURL(BaseURL, GameFolders.GetFirstContactFilename());
		yield return StartCoroutine(UbiCore.Utils.Singleton<Manager>.Instance.PrepareFile(url, 0u, _forced: true, Manager.EItemType.Simple, 0uL));
		if (!string.IsNullOrEmpty(UbiCore.Utils.Singleton<Manager>.Instance.LastError))
		{
			PotentialConnectionIssue = UbiCore.Utils.Singleton<Manager>.Instance.PotentialConnectionIssue;
			LastError = "Fatal: Can't download first contact file: " + UbiCore.Utils.Singleton<Manager>.Instance.LastError;
			yield break;
		}
		SimpleItem cacheEntry = UbiCore.Utils.Singleton<Manager>.Instance.GetEntry(url, 0u) as SimpleItem;
		using (Stream fileStream = new FileStream(cacheEntry.CacheFile, FileMode.Open, FileAccess.Read))
		{
			using DataSerializerBase ser = new DataXmlSerializer(fileStream, EMode.EMode_Read);
			ser.SerializeBeginNode("root");
			m_firstContacts.Serialize(ser);
			ser.SerializeEndNode();
		}
		foreach (FirstContact.DataFile dataFile in m_firstContacts.FileMap)
		{
			url = GameFolders.GetAcierDataURL(BaseURL, dataFile.Filename);
			if (UbiCore.Utils.Singleton<Manager>.Instance.GetEntry(dataFile.Filename, (uint)dataFile.Hash.GetHashCode()) == null)
			{
				yield return StartCoroutine(UbiCore.Utils.Singleton<Manager>.Instance.PrepareFile(url, (uint)dataFile.Hash.GetHashCode(), _forced: true, Manager.EItemType.Simple, 0uL));
				if (!string.IsNullOrEmpty(UbiCore.Utils.Singleton<Manager>.Instance.LastError))
				{
					PotentialConnectionIssue = UbiCore.Utils.Singleton<Manager>.Instance.PotentialConnectionIssue;
					LastError = "Fatal: Can't download first contact bundles";
					break;
				}
			}
		}
	}

	private TranslationsDef GetTranslationDef(ELanguages _preferredLanguage)
	{
		GameDB.SubDB subDB = GameDB.GetSubDB(typeof(TranslationsDef));
		foreach (KeyValuePair<GameID, DBEntry> datum in subDB.Data)
		{
			TranslationsDef translationsDef = (TranslationsDef)datum.Value;
			if (translationsDef.Language != _preferredLanguage)
			{
				continue;
			}
			return translationsDef;
		}
		return null;
	}

	private IEnumerator RetrieveLocaData()
	{
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		AssetBundleProviderInput bundleProviderInput = new AssetBundleProviderInput
		{
			AssetBundles = new BBHashSet<AssetBundleInfo>()
		};
		Translation = GetTranslationDef(Main.Instance.BuildDefinition.PreferredLanguage) ?? GetTranslationDef(ELanguages.en_US);
		bundleProviderInput.AssetBundles.Add(Translation.AssetBundleInfo);
		QueryResult result = null;
		NetworkProvider.Instance.Request((QueryResult _result, AssetBundleProviderResult _type) =>
		{
			result = _result;
			switch (_result.RequestStatus)
			{
			case RequestResult.Status.Failed:
				break;
			case RequestResult.Status.Suceeded:
				break;
			case RequestResult.Status.InProgress:
				break;
			}
		}, bundleProviderInput);
		while (result == null || !result.IsCompleted)
		{
			yield return wait;
		}
		if (result.Failed)
		{
			PotentialConnectionIssue = true;
			LastError = "Failed fetch LocaRuntimeAssets from server";
		}
	}

	private IEnumerator RetrieveDownloadScreenData()
	{
		AssetBundleProviderInput bundleProviderInput = new AssetBundleProviderInput
		{
			AssetBundles = new BBHashSet<AssetBundleInfo>()
		};
		DownloadScreenDef def = null;
		GameDB.SubDB subDB = GameDB.GetSubDB(typeof(DownloadScreenDef));
		AssetBundleInfo downloadScreenBundle = null;
		List<UIDownloadScreenDetails.ImagePackage> imagePackageList = new List<UIDownloadScreenDetails.ImagePackage>();
		foreach (KeyValuePair<GameID, DBEntry> datum in subDB.Data)
		{
			def = (DownloadScreenDef)datum.Value;
			imagePackageList.Add(new UIDownloadScreenDetails.ImagePackage
			{
				imageResource = def.BackgroundScreen,
				alignment = def.Alignment,
				logoType = def.LogoType
			});
			if (def.AssetBundleInfo != null)
			{
				if (!bundleProviderInput.AssetBundles.Contains(def.AssetBundleInfo))
				{
					bundleProviderInput.AssetBundles.Add(def.AssetBundleInfo);
				}
				downloadScreenBundle = def.AssetBundleInfo;
			}
		}
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		BBList<AssetBundleInfo> bundles = UIDownloadScreen.Instance.GetPreloadBundles(GameDB);
		ulong totalSize = 0uL;
		ulong remainingSize = 0uL;
		float downloadScreenSize = 0f;
		if (bundles != null)
		{
			UIDownloadScreen.Instance.GetTotalSizes(bundles, ref totalSize, ref remainingSize);
			for (int i = 0; i < bundles.Count; i++)
			{
				AssetBundleInfo bi = bundles[i];
				if (bi == downloadScreenBundle)
				{
					downloadScreenSize = bi.AssetBundleFileSize;
					totalSize -= (ulong)bi.AssetBundleFileSize;
					break;
				}
			}
		}
		QueryResult ownResult = null;
		DateTime start = DateTime.UtcNow;
		NetworkProvider.Instance.Request((QueryResult _result, AssetBundleProviderResult _type) =>
		{
			UIDownloadScreen.Instance.ShowWidget(UIDownloadScreenDetails.EState.LoadingData);
			ownResult = _result;
		}, bundleProviderInput);
		while (ownResult == null || !ownResult.IsCompleted)
		{
			yield return wait;
		}
		TrackingManager.AddAssetBundleTrackPoint(start, DateTime.UtcNow, "DownloadScreen", !ownResult.Failed);
		if (ownResult.Failed)
		{
			PotentialConnectionIssue = true;
			LastError = "Failed fetch Download screen bundle from server";
		}
		if (string.IsNullOrEmpty(LastError))
		{
			UIDownloadScreen.Instance.imagePackageList = imagePackageList;
			UIDownloadScreen.Instance.ImageLoadingDone = true;
			while (UIDownloadScreen.Instance.Details.Cast<UIDownloadScreenDetails>().TweenActive)
			{
				yield return new WaitForSeconds(0.5f);
			}
		}
	}

	public AssetBundleInfo GetSceneBundle(string _sceneName)
	{
		if (SceneDictionary.ContainsKey(_sceneName))
		{
			return SceneDictionary[_sceneName].GetAssetBundleInfos(Quality);
		}
		return null;
	}

	public MissionDescExt FillBundles(MissionRootData _desc)
	{
		if (SceneDictionary.ContainsKey(_desc.Scene))
		{
			MissionDescExt missionDescExt = new MissionDescExt(_desc);
			missionDescExt.AssetBundle = GetSceneBundle(_desc.Scene);
			return missionDescExt;
		}
		return null;
	}

	private void ReadEnvironmentDicFromCache()
	{
		SceneDictionary = new BBDictionary<string, SceneAssetBundle>();
		PreloadBundles = new BBHashSet<AssetBundleInfo>();
		FirstContact.DataFile dataFile = m_firstContacts.GetDataFile(GameFolders.GetEnvironmentDict((GameFolders.ETextureType)QualitySettings.textureType));
		SimpleItem simpleItem = UbiCore.Utils.Singleton<Manager>.Instance.GetEntry(GameFolders.GetEnvironmentDictURL(BaseURL, (GameFolders.ETextureType)QualitySettings.textureType), (uint)dataFile.Hash.GetHashCode()) as SimpleItem;
		using (Stream stream = new FileStream(simpleItem.CacheFile, FileMode.Open, FileAccess.Read))
		{
			List<SceneAssetBundle> list = new List<SceneAssetBundle>();
			using (DataSerializerBase dataSerializerBase = new DataXmlSerializer(stream, EMode.EMode_Read))
			{
				dataSerializerBase.SerializeBeginNode("root");
				list = dataSerializerBase.SerializeList("Scenes", list);
				dataSerializerBase.SerializeEndNode();
			}
			foreach (SceneAssetBundle item in list)
			{
				SceneDictionary.Add(item.Name, item);
			}
		}
		foreach (KeyValuePair<string, SceneAssetBundle> item2 in SceneDictionary)
		{
			if (s_PreloadedScenes.Contains(item2.Key))
			{
				AssetBundleInfo assetBundleInfos = item2.Value.GetAssetBundleInfos(Quality);
				PreloadBundles.Add(assetBundleInfos);
			}
		}
	}
}
