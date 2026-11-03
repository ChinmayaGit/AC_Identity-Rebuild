using System;
using System.Threading;

namespace BB.Mobile.Net;

public sealed class EnumSentinel<TEnum> where TEnum : struct, IConvertible
{
	private volatile int mState;

	public TEnum State => (TEnum)Enum.ToObject(typeof(TEnum), mState);

	public EnumSentinel(TEnum state)
	{
		mState = state.ToInt32(null);
	}

	public bool TryTransition(TEnum newState, TEnum oldState)
	{
		int num = oldState.ToInt32(null);
		return Interlocked.CompareExchange(ref mState, newState.ToInt32(null), num) == num;
	}

	public void Transition(TEnum newState, TEnum oldState)
	{
		if (!TryTransition(newState, oldState))
		{
			throw new InvalidOperationException();
		}
	}

	public TEnum Transition(TEnum newState)
	{
		int value = Interlocked.Exchange(ref mState, newState.ToInt32(null));
		return (TEnum)Enum.ToObject(typeof(TEnum), value);
	}
}
