using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BB.Mobile.Net;

public sealed class StringPayload<T> : IPayload where T : IConvertible
{
	private string mContentType = "text/plain";

	private T mContent;

	private IFormatProvider mFormatProvider;

	private Encoding mEncoding = Encoding.UTF8;

	public T Content => mContent;

	public IFormatProvider FormatProvider => mFormatProvider;

	public Encoding Encoding
	{
		get
		{
			return mEncoding;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException();
			}
			mEncoding = value;
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

	public StringPayload()
		: this((IFormatProvider)CultureInfo.InvariantCulture)
	{
	}

	public StringPayload(T content)
		: this(content, (IFormatProvider)CultureInfo.InvariantCulture)
	{
	}

	public StringPayload(IFormatProvider formatProvider)
		: this(default(T), formatProvider)
	{
	}

	public StringPayload(T content, IFormatProvider formatProvider)
	{
		if (formatProvider == null)
		{
			throw new ArgumentNullException();
		}
		mFormatProvider = formatProvider;
		mContent = content;
	}

	public void Serialize(Stream destination)
	{
		if (mContent == null)
		{
			throw new InvalidOperationException();
		}
		if (destination != null)
		{
			StreamWriter streamWriter = new StreamWriter(destination, mEncoding);
			ref T reference = ref mContent;
			IFormatProvider formatProvider = mFormatProvider;
			streamWriter.Write(reference.ToString(formatProvider));
			streamWriter.Flush();
		}
	}

	public void Generate(Stream source)
	{
		if (source != null)
		{
			StreamReader streamReader = new StreamReader(source);
			mContent = (T)Convert.ChangeType(streamReader.ReadToEnd(), typeof(T), mFormatProvider);
		}
		else
		{
			mContent = default;
		}
	}
}
