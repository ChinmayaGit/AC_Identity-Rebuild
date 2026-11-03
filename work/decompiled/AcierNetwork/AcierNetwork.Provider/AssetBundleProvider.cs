using System;
using System.Collections;
using AcierNetwork.AssetBundles;
using AcierNetwork.Communication;
using AssetBundleData;
using SharedBaseLib.Core;
using UnityEngine;

namespace AcierNetwork.Provider;

internal class AssetBundleProvider : IProvider
{
	private BBList<Query<AssetBundleProviderResult>> m_queuedRequests = new BBList<Query<AssetBundleProviderResult>>();

	private Query<AssetBundleProviderResult> m_currentRequest;

	public string BaseURL { get; set; }

	private int CurrentProcess { get; set; }

	private AssetBundleProviderResult ProviderResult { get; set; }

	public override void Initialize()
	{
	}

	public override bool Accepts(Type _type)
	{
		return _type == typeof(AssetBundleProviderResult);
	}

	public override bool BeginRead<T>(Request _request)
	{
		if (!Accepts(typeof(T)))
		{
			return false;
		}
		m_queuedRequests.Add((Query<AssetBundleProviderResult>)_request);
		return true;
	}

	protected override IEnumerator UpdateAsync()
	{
		while (m_queuedRequests.Count > 0)
		{
			m_currentRequest = m_queuedRequests[0];
			m_queuedRequests.RemoveAt(0);
			AssetBundleManager.Instance.AbortOutstandingDownloads();
			AssetBundleProviderInput assetBundleProviderInput = (AssetBundleProviderInput)m_currentRequest.Param;
			ProviderResult = new AssetBundleProviderResult();
			AssetBundleManager assetBundleManager = AssetBundleManager.Instance;
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.ESuccess, OnSucceedHandler);
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.EProgress, OnProgressHandler);
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.EFail, OnFailedHandler);
			if (assetBundleProviderInput.AssetBundles.Count > 0)
			{
				foreach (AssetBundleInfo assetBundle in assetBundleProviderInput.AssetBundles)
				{
					if (assetBundle == null)
					{
						OnSucceedHandler(this, null, 0f);
					}
					else if (assetBundleManager.HasBundleLoaded(assetBundle))
					{
						OnSucceedHandler(this, assetBundle, 1f);
					}
					else
					{
						assetBundleManager.DownloadAssetBundle(this, BaseURL, assetBundle);
					}
				}
			}
			else
			{
				Result = new QueryResult(RequestResult.Status.Suceeded);
			}
			while (Result == null)
			{
				yield return new WaitForEndOfFrame();
			}
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.ESuccess, OnSucceedHandler);
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.EProgress, OnProgressHandler);
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.EFail, OnFailedHandler);
			CurrentProcess = 0;
			m_currentRequest.EndRead(Result, ProviderResult);
			ProviderResult = null;
			m_currentRequest = null;
			Result = null;
		}
	}

	private bool OnSucceedHandler(object _requester, AssetBundleInfo _info, float _progress)
	{
		if (_requester.Equals(this) && m_currentRequest != null)
		{
			AssetBundleProviderInput assetBundleProviderInput = (AssetBundleProviderInput)m_currentRequest.Param;
			CurrentProcess++;
			ProviderResult.AddAssetBundle(_info);
			if (assetBundleProviderInput.AssetBundleLoaded != null)
			{
				assetBundleProviderInput.AssetBundleLoaded(_info);
			}
			if (CurrentProcess == assetBundleProviderInput.AssetBundles.Count)
			{
				Result = new QueryResult(RequestResult.Status.Suceeded);
			}
		}
		return true;
	}

	private bool OnProgressHandler(object _requester, AssetBundleInfo _info, float _progress)
	{
		if (_requester.Equals(this) && m_currentRequest != null)
		{
			AssetBundleProviderInput assetBundleProviderInput = (AssetBundleProviderInput)m_currentRequest.Param;
			m_currentRequest.Progress((float)CurrentProcess + _progress / (float)assetBundleProviderInput.AssetBundles.Count);
		}
		return true;
	}

	private bool OnFailedHandler(object _requester, AssetBundleInfo _info, float _progress)
	{
		if (_requester.Equals(this) && m_currentRequest != null)
		{
			Result = new QueryResult(RequestResult.Status.Failed);
		}
		return true;
	}
}
