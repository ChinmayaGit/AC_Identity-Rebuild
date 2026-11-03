using AcierNetwork.Communication;
using AcierNetwork.Provider;
using UnityEngine;

namespace AcierNetwork;

public class NetworkProvider
{
	private static NetworkProvider m_instance;

	private readonly ProviderManager m_providerManager;

	public static NetworkProvider Instance
	{
		get
		{
			if (m_instance == null)
			{
				m_instance = new NetworkProvider();
			}
			return m_instance;
		}
	}

	private NetworkProvider()
	{
		m_providerManager = new ProviderManager();
	}

	public bool Request<T>(Query<T>.TQueryCallback _callback, Params _param = null) where T : Model
	{
		Query<T> request = new Query<T>(_callback, _param);
		return m_providerManager.BeginRequest<T>(request);
	}

	public T GetProvider<T>() where T : IProvider
	{
		return m_providerManager.GetProvider<T>();
	}

	public void Initialize(GameObject _gameObject, string _url)
	{
		AssetBundleProvider assetBundleProvider = _gameObject.AddComponent<AssetBundleProvider>();
		assetBundleProvider.BaseURL = _url;
		m_providerManager.RegisterProvider(assetBundleProvider);
		MissionDataProvider missionDataProvider = _gameObject.AddComponent<MissionDataProvider>();
		missionDataProvider.BaseURL = _url;
		m_providerManager.RegisterProvider(missionDataProvider);
	}

	public TProviderType RegisterProvider<TProviderType>(GameObject _gameObject) where TProviderType : IProvider
	{
		TProviderType val = _gameObject.AddComponent<TProviderType>();
		m_providerManager.RegisterProvider(val);
		return val;
	}
}
