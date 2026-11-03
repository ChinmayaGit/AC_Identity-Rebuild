using System;
using System.Diagnostics;
using System.IO;

namespace BB.Mobile.Net;

public static class StreamExtensions
{
	private sealed class CopyToContext
	{
		private readonly Stream mSource;

		private readonly Stream mDestination;

		public Stream Source => mSource;

		public Stream Destination => mDestination;

		public WaitHandleMonitor CleanUpOperation { get; set; }

		public CopyToContext(Stream source, Stream destination)
		{
			if (source == null || destination == null)
			{
				throw new ArgumentNullException();
			}
			if (source == destination)
			{
				throw new ArgumentException();
			}
			mSource = source;
			mDestination = destination;
		}
	}

	private sealed class InitialReadState
	{
		private readonly AsyncValueResult<int, CopyToContext> m_asyncResult;

		private readonly byte[] m_buffer;

		public AsyncValueResult<int, CopyToContext> AsyncResult => m_asyncResult;

		public byte[] Buffer => m_buffer;

		public InitialReadState(AsyncValueResult<int, CopyToContext> asyncResult, byte[] buffer)
		{
			if (asyncResult == null || buffer == null)
			{
				throw new ArgumentNullException();
			}
			m_asyncResult = asyncResult;
			m_buffer = buffer;
		}
	}

	private delegate void AsyncStreamCallback(Stream source, IAsyncResult asyncResult);

	private const int mBufferSize = 131072;

	public static void CopyTo(this Stream source, Stream destination)
	{
		if (source == null || destination == null)
		{
			throw new ArgumentNullException();
		}
		int num = 0;
		byte[] array = new byte[131072];
		Stopwatch stopwatch = Stopwatch.StartNew();
		int num2;
		do
		{
			num2 = source.Read(array, 0, array.Length);
			destination.Write(array, 0, num2);
			num += num2;
		}
		while (num2 > 0);
		stopwatch.Stop();
	}

	public static IAsyncResult BeginCopyTo(this Stream source, Stream destination, AsyncCallback callback, object state, IAsyncCallbackScheduler scheduler)
	{
		AsyncValueResult<int, CopyToContext> asyncValueResult = new AsyncValueResult<int, CopyToContext>(new CopyToContext(source, destination), callback, state, scheduler);
		byte[] buffer = new byte[131072];
		source.BeginRead(buffer, 0, 131072, InitialReadCompleted, new InitialReadState(asyncValueResult, buffer));
		return asyncValueResult;
	}

	public static int EndCopyTo(this Stream source, IAsyncResult asyncResult)
	{
		AsyncValueResult<int, CopyToContext> asyncValueResult = AsyncValueResult<int, CopyToContext>.Verify(asyncResult);
		asyncValueResult.End();
		return asyncValueResult.Result;
	}

	private static void InitialReadCompleted(IAsyncResult asyncResult)
	{
		InitialReadState initialReadState = (InitialReadState)asyncResult.AsyncState;
		Stream source = initialReadState.AsyncResult.Context.Source;
		_ = initialReadState.AsyncResult.Context.Destination;
		try
		{
			int num = source.EndRead(asyncResult);
			if (num > 0)
			{
				ReadWrite(initialReadState.AsyncResult, num, new byte[131072], initialReadState.Buffer, num);
			}
			else
			{
				initialReadState.AsyncResult.Complete(0, synchronous: false);
			}
		}
		catch (Exception exception)
		{
			initialReadState.AsyncResult.CompleteWithError(exception, synchronous: false);
		}
	}

	private static void ReadWrite(AsyncValueResult<int, CopyToContext> overallAsyncResult, int bytesCopied, byte[] readBuffer, byte[] writeBuffer, int writeBytes)
	{
		Stream source = overallAsyncResult.Context.Source;
		Stream destination = overallAsyncResult.Context.Destination;
		try
		{
			IAsyncResult asyncResult = source.BeginRead(readBuffer, 0, readBuffer.Length, null, readBuffer);
			IAsyncResult asyncResult2;
			try
			{
				asyncResult2 = destination.BeginWrite(writeBuffer, 0, writeBytes, null, writeBuffer);
			}
			catch (Exception)
			{
				overallAsyncResult.Context.CleanUpOperation = new WaitHandleMonitor(asyncResult, new AsyncStreamCallback(CleanUpRead), source, asyncResult);
				throw;
			}
			AggregateAsyncResult<AsyncValueResult<int, CopyToContext>> aggregateAsyncResult = new AggregateAsyncResult<AsyncValueResult<int, CopyToContext>>(overallAsyncResult, ReadWriteCompleted, bytesCopied, ThreadPoolScheduler.DefaultScheduler);
			aggregateAsyncResult.Add(asyncResult);
			aggregateAsyncResult.Add(asyncResult2);
			aggregateAsyncResult.Observe();
		}
		catch (Exception exception)
		{
			overallAsyncResult.CompleteWithError(exception, synchronous: false);
		}
	}

	private static void ReadWriteCompleted(IAsyncResult asyncResult)
	{
		AggregateAsyncResult<AsyncValueResult<int, CopyToContext>> aggregateAsyncResult = (AggregateAsyncResult<AsyncValueResult<int, CopyToContext>>)asyncResult;
		Stream source = aggregateAsyncResult.Context.Context.Source;
		Stream destination = aggregateAsyncResult.Context.Context.Destination;
		try
		{
			int num = (int)asyncResult.AsyncState;
			int num2;
			try
			{
				num2 = source.EndRead(aggregateAsyncResult.Results[0]);
			}
			finally
			{
				destination.EndWrite(aggregateAsyncResult.Results[1]);
			}
			if (num2 > 0)
			{
				ReadWrite(aggregateAsyncResult.Context, num + num2, (byte[])aggregateAsyncResult.Results[1].AsyncState, (byte[])aggregateAsyncResult.Results[0].AsyncState, num2);
			}
			else
			{
				aggregateAsyncResult.Context.Complete(num, synchronous: false);
			}
		}
		catch (Exception exception)
		{
			aggregateAsyncResult.Context.CompleteWithError(exception, synchronous: false);
		}
	}

	private static void CleanUpRead(Stream source, IAsyncResult asyncResult)
	{
		try
		{
			source.EndRead(asyncResult);
		}
		catch (Exception)
		{
		}
	}
}
