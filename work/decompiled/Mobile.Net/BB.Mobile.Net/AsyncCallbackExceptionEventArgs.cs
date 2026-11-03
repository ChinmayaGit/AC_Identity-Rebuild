using System;

namespace BB.Mobile.Net;

public class AsyncCallbackExceptionEventArgs : EventArgs
{
	private readonly IAsyncResult m_asyncResult;

	private readonly Exception m_exception;

	public IAsyncResult AsyncResult => m_asyncResult;

	public Exception Exception => m_exception;

	public AsyncCallbackExceptionEventArgs(IAsyncResult asyncResult, Exception exception)
	{
		if (asyncResult == null || exception == null)
		{
			throw new ArgumentNullException();
		}
		m_asyncResult = asyncResult;
		m_exception = exception;
	}
}
