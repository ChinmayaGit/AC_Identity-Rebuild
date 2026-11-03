using System;

namespace BB.Mobile.Net;

public abstract class AsyncCallbackScheduler : IAsyncCallbackScheduler
{
	private EventHandler<AsyncCallbackExceptionEventArgs> m_invocationExceptionEventHandler;

	public event EventHandler<AsyncCallbackExceptionEventArgs> InvocationException
	{
		add
		{
			m_invocationExceptionEventHandler = (EventHandler<AsyncCallbackExceptionEventArgs>)Delegate.Combine(m_invocationExceptionEventHandler, value);
		}
		remove
		{
			m_invocationExceptionEventHandler = (EventHandler<AsyncCallbackExceptionEventArgs>)Delegate.Remove(m_invocationExceptionEventHandler, value);
		}
	}

	public void Schedule(AsyncCallback callback, IAsyncResult asyncResult)
	{
		if (callback != null)
		{
			Schedule(new DeferredAsyncCallback(callback, asyncResult));
		}
	}

	protected abstract void Schedule(DeferredAsyncCallback deferredCallback);

	protected void InvokeCallback(DeferredAsyncCallback deferredCallback)
	{
		if (deferredCallback == null)
		{
			throw new ArgumentNullException();
		}
		try
		{
			deferredCallback.Invoke();
		}
		catch (Exception exception)
		{
			try
			{
				m_invocationExceptionEventHandler?.Invoke(this, new AsyncCallbackExceptionEventArgs(deferredCallback.AsyncResult, exception));
			}
			catch (Exception)
			{
			}
		}
	}
}
