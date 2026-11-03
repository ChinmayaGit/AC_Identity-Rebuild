using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AcierNetwork.Communication;
using AssetBundleData;
using SharedBaseLib.Core;
using SharedBaseLib.System.Cache;
using SharedBaseLib.System.Extensions;
using UbiCore.Helpers;
using UbiCore.Utils;
using UnityEngine;

namespace AcierNetwork.AssetBundles;

public class AssetBundleManager
{
	public enum EBundleEvent
	{
		EStart,
		EProgress,
		EFail,
		EAbort,
		ESuccess
	}

	internal class BundleInfo
	{
		internal readonly string BaseURL;

		internal readonly AssetBundleInfo Info;

		internal readonly BBList<IProvider> Requesters = new BBList<IProvider>();

		internal readonly bool LoadOnDemand;

		internal AssetBundle Bundle;

		internal bool Unloaded;

		internal bool Loading;

		internal readonly string FullURL;

		internal bool Done => Bundle != null;

		internal string GetInfoStr()
		{
			return string.Format("{0} Bundle ref: {1}", FullURL, (Bundle == null) ? "no" : "yes");
		}

		internal BundleInfo(IProvider _requester, string _url, AssetBundleInfo _info)
		{
			FullURL = GameFolders.GetAcierDataURL(_url, _info.URL);
			Requesters.Add(_requester);
			Info = _info;
			BaseURL = _url;
			Unloaded = true;
			LoadOnDemand = Info.Type == AssetBundleInfo.EType.RuntimeAssets || Info.Type == AssetBundleInfo.EType.SoundContainer;
		}

		internal IEnumerator LoadAsync()
		{
			if (!Unloaded)
			{
				yield break;
			}
			if (Loading)
			{
				yield return 0;
				yield break;
			}
			Loading = true;
			string acierDataURL = GameFolders.GetAcierDataURL(BaseURL, Info.URL);
			Item cacheEntry = Singleton<Manager>.Instance.GetEntry(acierDataURL, (uint)Info.BuildNumber);
			switch (Info.Type)
			{
			case AssetBundleInfo.EType.RuntimeAssets:
			case AssetBundleInfo.EType.SoundContainer:
			{
				ZippedItem zippedItem = (ZippedItem)cacheEntry;
				_ = Time.realtimeSinceStartup;
				Instance.InvokeEvent(EBundleEvent.EProgress, this, 0.665f);
				GameFolders.FixURL("file://" + zippedItem.CacheFile);
				Bundle = AssetBundle.CreateFromFile(zippedItem.CacheFile);
				yield return new WaitForEndOfFrame();
				Instance.InvokeEvent(EBundleEvent.EProgress, this, 0.83000004f);
				break;
			}
			case AssetBundleInfo.EType.Environment:
			{
				SimpleItem simpleItem = (SimpleItem)cacheEntry;
				string url = GameFolders.FixURL("file://" + simpleItem.CacheFile);
				using (WWW download = new WWW(url))
				{
					while (!download.isDone)
					{
						Instance.InvokeEvent(EBundleEvent.EProgress, this, 0.5f * (1f + download.progress));
						yield return new WaitForEndOfFrame();
					}
					Bundle = download.assetBundle;
				}
				break;
			}
			}
			Unloaded = false;
			Loading = false;
		}

		internal void Load()
		{
			if (Unloaded)
			{
				Loading = true;
				string acierDataURL = GameFolders.GetAcierDataURL(BaseURL, Info.URL);
				ZippedItem zippedItem = (ZippedItem)Singleton<Manager>.Instance.GetEntry(acierDataURL, (uint)Info.BuildNumber);
				Bundle = AssetBundle.CreateFromFile(zippedItem.CacheFile);
				Unloaded = false;
				Loading = false;
			}
		}

		internal void Unload()
		{
			if (!Unloaded && Info.Type != AssetBundleInfo.EType.SoundContainer)
			{
				if (Bundle != null)
				{
					Bundle.Unload(unloadAllLoadedObjects: false);
				}
				Bundle = null;
				Unloaded = true;
			}
		}
	}

	private const float c_DownloadPart = 0.5f;

	private static AssetBundleManager s_instance;

	private readonly BBList<BundleInfo> m_DownloadQueue = new BBList<BundleInfo>();

	private readonly BBDictionary<int, BBList<Func<object, AssetBundleInfo, float, bool>>> m_Listeners = new BBDictionary<int, BBList<Func<object, AssetBundleInfo, float, bool>>>();

	private readonly BBDictionary<string, BundleInfo> m_dictAssetBundleRefs = new BBDictionary<string, BundleInfo>();

	private bool m_callbackAdded;

	private int m_autoUnloadEnabled;

	private BundleInfo CurrentJob { get; set; }

	public static AssetBundleManager Instance => s_instance ?? (s_instance = new AssetBundleManager());

	public bool PotentialConnectionIssue { get; private set; }

	public string LastError { get; private set; }

	public float LastDownloadStart { get; private set; }

	public bool AutoUnloadEnabled
	{
		get
		{
			return m_autoUnloadEnabled == 0;
		}
		set
		{
			if (value)
			{
				m_autoUnloadEnabled--;
			}
			else
			{
				m_autoUnloadEnabled++;
			}
			if (AutoUnloadEnabled)
			{
				OnUnloadCallback(0f);
			}
			else if (m_callbackAdded)
			{
				RemoveUnloadCallback();
			}
		}
	}

	private AssetBundleManager()
	{
		foreach (object value in Enum.GetValues(typeof(EBundleEvent)))
		{
			m_Listeners[(int)value] = new BBList<Func<object, AssetBundleInfo, float, bool>>();
		}
		RegisterListener(EBundleEvent.EFail, OnDownloadFailedImpl);
		RegisterListener(EBundleEvent.EStart, OnDownloadStartImpl);
		RegisterListener(EBundleEvent.ESuccess, OnDownloadSucceededImpl);
		RegisterListener(EBundleEvent.EAbort, OnDownloadAbortedImpl);
		Timer instance = Singleton<Timer>.Instance;
		instance.OnReset = (Timer.OnResetHandler)Delegate.Combine(instance.OnReset, (Timer.OnResetHandler)(() =>
		{
			if (m_callbackAdded)
			{
				Singleton<Timer>.Instance.AddCallback(OnUnloadCallback, 5f, _unscaled: true);
			}
		}));
		Singleton<Manager>.Instance.ProgressDelegate.AddUnique(OnCacheProgress);
	}

	private void OnCacheProgress(params object[] _params)
	{
		if (CurrentJob != null)
		{
			InvokeEvent(EBundleEvent.EProgress, CurrentJob, (float)(uint)_params[1] / (float)CurrentJob.Info.AssetBundleFileSize * 0.5f);
		}
	}

	private bool OnDownloadStartImpl(object requester, AssetBundleInfo info, float progress)
	{
		LastDownloadStart = Singleton<Timer>.Instance.realTime;
		return true;
	}

	private bool OnDownloadAbortedImpl(object requester, AssetBundleInfo info, float progress)
	{
		return true;
	}

	private bool OnDownloadSucceededImpl(object _requester, AssetBundleInfo _info, float _progress)
	{
		return true;
	}

	private bool OnDownloadFailedImpl(object _requester, AssetBundleInfo _info, float _progress)
	{
		return true;
	}

	public bool RegisterListener(EBundleEvent _type, Func<object, AssetBundleInfo, float, bool> _handler)
	{
		return RegisterListener(m_Listeners[(int)_type], _handler);
	}

	public bool UnregisterListener(EBundleEvent _type, Func<object, AssetBundleInfo, float, bool> _handler)
	{
		return UnregisterListener(m_Listeners[(int)_type], _handler);
	}

	private void InvokeEvent(EBundleEvent _type, BundleInfo _info, float _progress)
	{
		BBList<Func<object, AssetBundleInfo, float, bool>> bBList = m_Listeners[(int)_type];
		int num = bBList.Count;
		for (int i = 0; i < _info.Requesters.Count; i++)
		{
			IProvider arg = _info.Requesters[i];
			int num2 = 0;
			while (num2 < num)
			{
				if (!bBList[num2](arg, _info.Info, _progress))
				{
					num--;
				}
				else
				{
					num2++;
				}
			}
		}
	}

	private bool RegisterListener(BBList<Func<object, AssetBundleInfo, float, bool>> _handlers, Func<object, AssetBundleInfo, float, bool> _func)
	{
		if (!_handlers.Contains(_func))
		{
			_handlers.Add(_func);
			return true;
		}
		return false;
	}

	private bool UnregisterListener(BBList<Func<object, AssetBundleInfo, float, bool>> _handlers, Func<object, AssetBundleInfo, float, bool> _func)
	{
		int num = _handlers.IndexOf(_func);
		if (num != -1)
		{
			_handlers.RemoveAt(num);
			return true;
		}
		return false;
	}

	private void QueueJob()
	{
		if (CurrentJob == null && m_DownloadQueue.Count > 0)
		{
			CurrentJob = m_DownloadQueue[0];
			m_DownloadQueue.RemoveAt(0);
			CurrentJob.Requesters[0].StartCoroutine(DownloadAssetBundleJob());
		}
	}

	public void DownloadAssetBundle(IProvider _requester, string _baseURL, AssetBundleInfo _bundleInfo)
	{
		RemoveUnloadCallback();
		BundleInfo _info = null;
		if (!GetBundleInfo(_bundleInfo, out _info))
		{
			m_DownloadQueue.Add(new BundleInfo(_requester, _baseURL, _bundleInfo));
		}
		else
		{
			_info.Requesters.Add(_requester);
			m_DownloadQueue.Add(_info);
		}
		QueueJob();
	}

	public void AbortOutstandingDownloads()
	{
		foreach (BundleInfo item in m_DownloadQueue)
		{
			InvokeEvent(EBundleEvent.EAbort, item, 0f);
		}
		m_DownloadQueue.Clear();
	}

	private IEnumerator DownloadAssetBundleJob()
	{
		AssetBundleInfo bundleInfo = CurrentJob.Info;
		PotentialConnectionIssue = false;
		LastError = null;
		InvokeEvent(EBundleEvent.EStart, CurrentJob, 0f);
		bool forced = bundleInfo.BuildNumber == 0;
		if (!m_dictAssetBundleRefs.ContainsKey(bundleInfo.ToString()))
		{
			IEnumerator ite = Singleton<Manager>.Instance.PrepareFile(CurrentJob.FullURL, (uint)bundleInfo.BuildNumber, forced, Manager.GetCacheItemType(bundleInfo), (ulong)bundleInfo.AssetBundleFileSize);
			while (ite.MoveNext())
			{
				yield return 0;
			}
			if (!string.IsNullOrEmpty(Singleton<Manager>.Instance.LastError))
			{
				PotentialConnectionIssue = Singleton<Manager>.Instance.PotentialConnectionIssue;
				LastError = Singleton<Manager>.Instance.LastError;
				InvokeEvent(EBundleEvent.EFail, CurrentJob, 0f);
			}
			else
			{
				Item ce = Singleton<Manager>.Instance.GetEntry(CurrentJob.FullURL, (uint)bundleInfo.BuildNumber);
				if (ce != null)
				{
					yield return CurrentJob.Requesters[0].StartCoroutine(CurrentJob.LoadAsync());
					if (!CurrentJob.Done)
					{
						Singleton<Manager>.Instance.Remove(ce);
						LastError = "AssetBundleManager Error: downloaded assetbundle is invalid " + CurrentJob.FullURL;
						InvokeEvent(EBundleEvent.EFail, CurrentJob, 0f);
					}
					else
					{
						if (!m_dictAssetBundleRefs.ContainsKey(bundleInfo.ToString()))
						{
							m_dictAssetBundleRefs.Add(bundleInfo.ToString(), CurrentJob);
						}
						InvokeEvent(EBundleEvent.ESuccess, CurrentJob, 1f);
					}
				}
				else
				{
					LastError = "Internal problem: Cache reported file done, but it got lost?!";
					InvokeEvent(EBundleEvent.EFail, CurrentJob, 0f);
				}
			}
		}
		else
		{
			if (CurrentJob.Unloaded)
			{
				yield return CurrentJob.Requesters[0].StartCoroutine(CurrentJob.LoadAsync());
			}
			InvokeEvent(EBundleEvent.ESuccess, CurrentJob, 1f);
		}
		CurrentJob = null;
		if (string.IsNullOrEmpty(LastError))
		{
			QueueJob();
		}
	}

	private void RemoveUnloadCallback()
	{
		if (m_callbackAdded)
		{
			Singleton<Timer>.Instance.RemoveCallback(OnUnloadCallback);
		}
		m_callbackAdded = false;
	}

	internal void CheckAutoUnload()
	{
		RemoveUnloadCallback();
		if (AutoUnloadEnabled && m_dictAssetBundleRefs.Count > 0 && m_DownloadQueue.Count == 0 && !m_callbackAdded)
		{
			Singleton<Timer>.Instance.AddCallback(OnUnloadCallback, 5f, _unscaled: true);
			m_callbackAdded = true;
		}
	}

	private bool OnUnloadCallback(float _dt)
	{
		int num = 0;
		List<string> list = m_dictAssetBundleRefs.Keys.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			string key = list[i];
			BundleInfo bundleInfo = m_dictAssetBundleRefs[key];
			bool unloaded = bundleInfo.Unloaded;
			bundleInfo.Unload();
			if (bundleInfo.Unloaded != unloaded)
			{
				num++;
			}
			if (!bundleInfo.LoadOnDemand)
			{
				m_dictAssetBundleRefs.Remove(key);
			}
		}
		m_callbackAdded = false;
		return false;
	}

	public UnityEngine.Object GetMainAsset(AssetBundleInfo _bundleInfo)
	{
		if (GetBundleInfo(_bundleInfo, out var _info))
		{
			if (_info.Unloaded)
			{
				_info.Load();
			}
			if (_info.LoadOnDemand)
			{
				CheckAutoUnload();
			}
			return _info.Bundle.mainAsset;
		}
		return null;
	}

	public T LoadAsset<T>(AssetBundleInfo _bundleInfo, string _name) where T : UnityEngine.Object
	{
		if (GetBundleInfo(_bundleInfo, out var _info))
		{
			if (_info.Unloaded)
			{
				_info.Load();
			}
			if (_info.LoadOnDemand)
			{
				CheckAutoUnload();
			}
			if (!(_info.Bundle == null))
			{
				return _info.Bundle.LoadAsset(_name, typeof(T)) as T;
			}
			return null;
		}
		return null;
	}

	public AsyncLoadResult<T> LoadAssetAsync<T>(AssetBundleInfo _bundleInfo, string _name) where T : UnityEngine.Object
	{
		if (GetBundleInfo(_bundleInfo, out var _info))
		{
			return new AsyncLoadResult<T>(_info, _name);
		}
		return null;
	}

	public bool HasBundleLoaded(AssetBundleInfo _bundleInfo)
	{
		BundleInfo _info;
		return GetBundleInfo(_bundleInfo, out _info);
	}

	private bool GetBundleInfo(AssetBundleInfo _bundleInfo, out BundleInfo _info)
	{
		_info = null;
		if (_bundleInfo == null || !m_dictAssetBundleRefs.TryGetValue(_bundleInfo.ToString(), out _info))
		{
			return false;
		}
		return true;
	}

	public void UnloadBundles()
	{
		List<string> list = m_dictAssetBundleRefs.Keys.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			string key = list[i];
			BundleInfo bundleInfo = m_dictAssetBundleRefs[key];
			if (!bundleInfo.LoadOnDemand)
			{
				bundleInfo.Unload();
				m_dictAssetBundleRefs.Remove(key);
			}
		}
		CheckAutoUnload();
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("AssetBundleManager");
		foreach (KeyValuePair<string, BundleInfo> dictAssetBundleRef in m_dictAssetBundleRefs)
		{
			stringBuilder.AppendFormat("  {0} {1}\n", dictAssetBundleRef.Key, dictAssetBundleRef.Value.GetInfoStr());
		}
		return stringBuilder.ToString();
	}

	public Item GetCacheItem(AssetBundleInfo _info)
	{
		BundleInfo _info2 = null;
		if (GetBundleInfo(_info, out _info2))
		{
			return Singleton<Manager>.Instance.GetEntry(_info2.FullURL, (uint)_info2.Info.BuildNumber);
		}
		return null;
	}
}
