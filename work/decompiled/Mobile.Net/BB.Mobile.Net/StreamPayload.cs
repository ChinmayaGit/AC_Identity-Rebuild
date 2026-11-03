using System;
using System.IO;

namespace BB.Mobile.Net;

public sealed class StreamPayload : IPayload
{
	private const string mDefaultContentType = "text/plain";

	private string mContentType;

	private readonly Stream mContent;

	private bool mAutoClose;

	public static string DefaultContentType => "text/plain";

	public Stream Content => mContent;

	public bool AutoClose
	{
		get
		{
			return mAutoClose;
		}
		set
		{
			mAutoClose = value;
		}
	}

	public string ContentType
	{
		get
		{
			return mContentType;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			mContentType = value;
		}
	}

	public StreamPayload(Stream content, bool autoClose)
		: this(content, "text/plain", autoClose)
	{
	}

	public StreamPayload(Stream content, string contentType, bool autoClose)
	{
		if (content == null || contentType == null)
		{
			throw new ArgumentNullException();
		}
		mContentType = contentType;
		mContent = content;
		mAutoClose = autoClose;
	}

	public void Serialize(Stream destination)
	{
		try
		{
			if (destination != null)
			{
				mContent.CopyTo(destination);
			}
		}
		finally
		{
			if (mAutoClose)
			{
				mContent.Close();
			}
		}
	}

	public void Generate(Stream source)
	{
		try
		{
			source?.CopyTo(mContent);
		}
		finally
		{
			if (mAutoClose)
			{
				mContent.Close();
			}
		}
	}
}
