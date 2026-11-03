using System;
using System.ComponentModel;

namespace BB.Mobile.Net;

public interface IExtendedSynchronizeInvoke : ISynchronizeInvoke
{
	IAsyncResult BeginInvoke(Delegate method, object[] args, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler);
}
