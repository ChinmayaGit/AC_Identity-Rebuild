using System;

namespace BB.Mobile.Net;

public class OwnerContext<O, C>
{
	private readonly O mOwner;

	private C mContext;

	public O Owner => mOwner;

	public C Context
	{
		get
		{
			return mContext;
		}
		set
		{
			mContext = value;
		}
	}

	public OwnerContext(O owner, C context)
	{
		if (owner == null)
		{
			throw new ArgumentNullException();
		}
		mOwner = owner;
		mContext = context;
	}
}
