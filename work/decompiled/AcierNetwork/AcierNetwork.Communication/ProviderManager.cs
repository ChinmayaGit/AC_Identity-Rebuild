using SharedBaseLib.Core;

namespace AcierNetwork.Communication;

public class ProviderManager
{
	private readonly BBList<IProvider> m_providers;

	public ProviderManager()
	{
		m_providers = new BBList<IProvider>();
	}

	~ProviderManager()
	{
		foreach (IProvider provider in m_providers)
		{
			provider.OnDestroy();
		}
	}

	public void RegisterProvider(IProvider _provider)
	{
		m_providers.Add(_provider);
	}

	public T GetProvider<T>() where T : IProvider
	{
		foreach (IProvider provider in m_providers)
		{
			if (provider is T)
			{
				return (T)provider;
			}
		}
		return null;
	}

	public bool BeginRequest<T>(Request _request) where T : Message
	{
		foreach (IProvider provider in m_providers)
		{
			if (provider.BeginRead<T>(_request))
			{
				if (!provider.Running)
				{
					provider.StartCoroutine(provider.UpdateCoroutine());
				}
				return true;
			}
		}
		return false;
	}
}
