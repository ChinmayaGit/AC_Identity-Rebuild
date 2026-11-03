using System;
using System.Collections;
using AcierNetwork.AssetBundles;
using AcierNetwork.Communication;
using AssetBundleData;
using SharedBaseLib.Core;
using SharedBaseLib.System.Extensions;

namespace AcierNetwork.Provider;

public class MissionDataProvider : IProvider
{
	private const float ProgressUpdateDelta = 0.5f;

	private BBList<Query<MissionData>> m_queuedRequests = new BBList<Query<MissionData>>();

	private Query<MissionData> m_currentRequest;

	public string BaseURL { get; set; }

	private int CurrentProcess { get; set; }

	private MissionData Data { get; set; }

	public override void Initialize()
	{
	}

	public override bool Accepts(Type _type)
	{
		return _type == typeof(MissionData);
	}

	public override bool BeginRead<T>(Request _request)
	{
		if (!Accepts(typeof(T)))
		{
			return false;
		}
		m_queuedRequests.Add((Query<MissionData>)_request);
		return true;
	}

	private void OnBundleManagerFinished()
	{
	}

	protected override IEnumerator UpdateAsync()
	{
		while (m_queuedRequests.Count > 0)
		{
			m_currentRequest = m_queuedRequests[0];
			m_queuedRequests.RemoveAt(0);
			AssetBundleManager.Instance.AbortOutstandingDownloads();
			MissionProviderInput missionProviderInput = (MissionProviderInput)m_currentRequest.Param;
			Data = new MissionData();
			AssetBundleManager assetBundleManager = AssetBundleManager.Instance;
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.ESuccess, OnSucceedHandler);
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.EProgress, OnProgressHandler);
			assetBundleManager.RegisterListener(AssetBundleManager.EBundleEvent.EFail, OnFailedHandler);
			BBList<AssetBundleInfo> bBList = missionProviderInput.AssetBundles.ToList();
			if (bBList.Count == 0)
			{
				m_currentRequest.Progress(1f);
				Result = new QueryResult(RequestResult.Status.Suceeded);
			}
			else
			{
				for (int i = 0; i < bBList.Count; i++)
				{
					AssetBundleInfo assetBundleInfo = bBList[i];
					if (assetBundleInfo == null)
					{
						OnSucceedHandler(this, null, 0f);
					}
					else if (assetBundleManager.HasBundleLoaded(assetBundleInfo))
					{
						OnSucceedHandler(this, assetBundleInfo, 1f);
					}
					else
					{
						assetBundleManager.DownloadAssetBundle(this, BaseURL, assetBundleInfo);
					}
				}
			}
			while (Result == null)
			{
				yield return 0;
			}
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.ESuccess, OnSucceedHandler);
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.EProgress, OnProgressHandler);
			assetBundleManager.UnregisterListener(AssetBundleManager.EBundleEvent.EFail, OnFailedHandler);
			CurrentProcess = 0;
			m_currentRequest.EndRead(Result, Data);
			Data = null;
			m_currentRequest = null;
			Result = null;
		}
	}

	private bool OnSucceedHandler(object requester, AssetBundleInfo info, float progress)
	{
		if (requester.Equals(this) && m_currentRequest != null)
		{
			MissionProviderInput missionProviderInput = (MissionProviderInput)m_currentRequest.Param;
			CurrentProcess++;
			Data.AddAssetBundle(info);
			if (missionProviderInput.AssetBundleLoaded != null)
			{
				missionProviderInput.AssetBundleLoaded(info);
			}
			m_currentRequest.Progress((float)CurrentProcess / (float)missionProviderInput.AssetBundles.Count);
			if (CurrentProcess == missionProviderInput.AssetBundles.Count)
			{
				Result = new QueryResult(RequestResult.Status.Suceeded);
			}
		}
		return true;
	}

	private bool OnProgressHandler(object requester, AssetBundleInfo info, float progress)
	{
		if (requester.Equals(this) && m_currentRequest != null)
		{
			MissionProviderInput missionProviderInput = (MissionProviderInput)m_currentRequest.Param;
			m_currentRequest.Progress(((float)CurrentProcess + progress) / (float)missionProviderInput.AssetBundles.Count);
		}
		return true;
	}

	private bool OnFailedHandler(object requester, AssetBundleInfo info, float progress)
	{
		if (requester.Equals(this) && m_currentRequest != null)
		{
			Result = new QueryResult(RequestResult.Status.Failed);
		}
		return true;
	}
}
