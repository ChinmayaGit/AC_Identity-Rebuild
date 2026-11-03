using System;

namespace BB.Mobile.Net;

public sealed class DeferredAsyncCallback
{
	private readonly AsyncCallback m_callback;

	private readonly IAsyncResult m_asyncResult;

	public IAsyncResult AsyncResult => m_asyncResult;

	public DeferredAsyncCallback(AsyncCallback callback, IAsyncResult asyncResult)
	{
		if (callback == null || asyncResult == null)
		{
			throw new ArgumentNullException();
		}
		m_callback = callback;
		m_asyncResult = asyncResult;
	}

	public void Invoke()
	{
		m_callback(m_asyncResult);
	}
}
