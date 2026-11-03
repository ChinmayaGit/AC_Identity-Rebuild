using System;
using System.IO;

namespace BB.Mobile.Net;

public sealed class StreamProxy : Stream
{
	private readonly Stream mStream;

	private long mBytesRead;

	private long mBytesWritten;

	public Stream Stream => mStream;

	public long BytesRead => mBytesRead;

	public long BytesWritten => mBytesWritten;

	public override bool CanRead => mStream.CanRead;

	public override bool CanSeek => mStream.CanSeek;

	public override bool CanWrite => mStream.CanWrite;

	public override bool CanTimeout => mStream.CanTimeout;

	public override long Length => mStream.Length;

	public override long Position
	{
		get
		{
			return mStream.Position;
		}
		set
		{
			mStream.Position = value;
		}
	}

	public override int ReadTimeout
	{
		get
		{
			return mStream.ReadTimeout;
		}
		set
		{
			mStream.ReadTimeout = value;
		}
	}

	public override int WriteTimeout
	{
		get
		{
			return mStream.WriteTimeout;
		}
		set
		{
			mStream.WriteTimeout = value;
		}
	}

	public StreamProxy(Stream stream)
	{
		if (stream == null)
		{
			throw new ArgumentNullException();
		}
		mStream = stream;
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			mStream.Dispose();
		}
	}

	public override void Flush()
	{
		mStream.Flush();
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		return mStream.Seek(offset, origin);
	}

	public override void SetLength(long value)
	{
		mStream.SetLength(value);
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		int num = mStream.Read(buffer, offset, count);
		mBytesRead += num;
		return num;
	}

	public override int ReadByte()
	{
		int result = mStream.ReadByte();
		mBytesRead++;
		return result;
	}

	public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
	{
		return mStream.BeginRead(buffer, offset, count, callback, state);
	}

	public override int EndRead(IAsyncResult asyncResult)
	{
		int num = mStream.EndRead(asyncResult);
		mBytesRead += num;
		return num;
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		mStream.Write(buffer, offset, count);
		mBytesWritten += count;
	}

	public override void WriteByte(byte value)
	{
		mStream.WriteByte(value);
		mBytesWritten++;
	}

	public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
	{
		IAsyncResult result = mStream.BeginRead(buffer, offset, count, callback, state);
		mBytesWritten += count;
		return result;
	}

	public override void EndWrite(IAsyncResult asyncResult)
	{
		mStream.EndWrite(asyncResult);
	}

	public override void Close()
	{
		mStream.Close();
	}
}
