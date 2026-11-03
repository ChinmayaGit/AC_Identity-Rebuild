using System;
using System.Collections.Generic;
using System.Diagnostics;
using BB.Mobile.Net.Collections;

namespace BB.Mobile.Net;

public class TraceListenerAdapter : TraceListener
{
	public delegate void ProcessMessage(TraceEventType eventType, string message);

	private readonly ConcurrentQueue<KeyValuePair<int, string>> mQueue = new ConcurrentQueue<KeyValuePair<int, string>>();

	public void ProcessMessages(ProcessMessage processMessage)
	{
		if (processMessage == null)
		{
			throw new ArgumentNullException();
		}
		KeyValuePair<int, string> value;
		while (mQueue.TryDequeue(out value))
		{
			processMessage((TraceEventType)value.Key, value.Value);
		}
	}

	public override void Write(string message)
	{
		TraceEventType key = TraceEventType.Verbose;
		int num = message.IndexOf(" : ", StringComparison.InvariantCulture);
		if (num > 0)
		{
			string text = message.Substring(0, num);
			int num2 = text.LastIndexOf(" ", StringComparison.InvariantCulture);
			if (num2 > 0)
			{
				num2++;
				try
				{
					key = (TraceEventType)Enum.Parse(typeof(TraceEventType), text.Substring(num2));
				}
				catch (ArgumentException)
				{
				}
				catch (OverflowException)
				{
				}
			}
		}
		mQueue.Enqueue(new KeyValuePair<int, string>((int)key, message));
	}

	public override void WriteLine(string message)
	{
		Write(message);
	}
}
