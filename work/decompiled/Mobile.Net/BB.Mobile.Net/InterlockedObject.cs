using System.Threading;

namespace BB.Mobile.Net;

public sealed class InterlockedObject<T> where T : class
{
	private volatile object mObject;

	public T Value => (T)mObject;

	public InterlockedObject(T value = null)
	{
		mObject = value;
	}

	public T Exchange(T value)
	{
		return (T)Interlocked.Exchange(ref mObject, value);
	}

	public T CompareExchange(T value, T comparand)
	{
		return (T)Interlocked.CompareExchange(ref mObject, value, comparand);
	}
}
