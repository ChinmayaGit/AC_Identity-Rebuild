using System;

namespace BB.Mobile.Net;

public interface IAsyncValueResult<T> : IAsyncResult
{
	Exception Exception { get; }

	T Result { get; }

	bool WaitForCompletion();

	void Complete(T result, bool synchronous);

	void CompleteWithError(Exception exception, bool synchronous);
}
