using System;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace AcierNetwork.AssetBundles;

public class AsyncLoadResult<T> : IAsyncResult where T : UnityEngine.Object
{
	private readonly ManualResetEvent WaitHandle = new ManualResetEvent(initialState: false);

	private readonly IEnumerator m_bundleLoadEnum;

	private AssetBundleRequest m_loadAssetEnum;

	private readonly AssetBundleManager.BundleInfo m_Info;

	private readonly string m_Name;

	public object AsyncState { get; internal set; }

	public WaitHandle AsyncWaitHandle => WaitHandle;

	public bool CompletedSynchronously => false;

	public bool IsCompleted
	{
		get
		{
			if (m_bundleLoadEnum != null && !m_bundleLoadEnum.MoveNext())
			{
				m_loadAssetEnum = m_Info.Bundle.LoadAssetAsync(m_Name, typeof(T));
			}
			if (m_loadAssetEnum != null && m_loadAssetEnum.isDone)
			{
				AsyncState = m_loadAssetEnum.asset;
				AssetBundleManager.Instance.CheckAutoUnload();
				WaitHandle.Set();
				AssetBundleManager.Instance.AutoUnloadEnabled = true;
				return true;
			}
			return false;
		}
	}

	internal AsyncLoadResult(AssetBundleManager.BundleInfo _info, string _resource)
	{
		m_Info = _info;
		m_Name = _resource;
		AssetBundleManager.Instance.AutoUnloadEnabled = false;
		if (m_Info.Unloaded)
		{
			m_bundleLoadEnum = m_Info.LoadAsync();
		}
		else
		{
			m_loadAssetEnum = m_Info.Bundle.LoadAssetAsync(m_Name, typeof(T));
		}
	}
}
