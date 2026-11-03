using System;

namespace BB.Mobile.Net;

public interface IAsyncCallbackScheduler
{
	event EventHandler<AsyncCallbackExceptionEventArgs> InvocationException;

	void Schedule(AsyncCallback callback, IAsyncResult asyncResult);
}
