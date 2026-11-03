using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BB.Mobile.Net;

public sealed class HostPinsetStore
{
	private static readonly TraceSwitch mTraceSwitch = new TraceSwitch("Certificate Pinning", "Controls tracing level for managing pinsets and verifying certificates.");

	private const int mFormatName = 5460040;

	private const byte mFormatVersion = 2;

	private const byte mMinimumFormatVersion = 1;

	private const string mWildcardPattern = "*.";

	private const int mWildcardStrippingLength = 1;

	private readonly bool mReadOnly;

	private readonly Dictionary<string, X509Certificate2Collection> mStore = new Dictionary<string, X509Certificate2Collection>();

	private readonly Dictionary<string, X509Certificate2Collection> mWildcardHostStore = new Dictionary<string, X509Certificate2Collection>();

	public Dictionary<string, X509Certificate2Collection> Pinsets
	{
		get
		{
			if (mReadOnly)
			{
				throw new InvalidOperationException();
			}
			return mStore;
		}
	}

	public HostPinsetStore()
	{
		mReadOnly = false;
	}

	public HostPinsetStore(string fileName, bool readOnly = false)
		: this()
	{
		Import(fileName);
		mReadOnly = readOnly;
	}

	public HostPinsetStore(Stream source, bool readOnly = false)
		: this()
	{
		Import(source);
		mReadOnly = readOnly;
	}

	public bool Add(string hostname, X509Certificate certificate)
	{
		if (hostname == null || certificate == null)
		{
			throw new ArgumentNullException();
		}
		if (mReadOnly)
		{
			throw new InvalidOperationException();
		}
		if (!mStore.TryGetValue(hostname, out var value))
		{
			value = new X509Certificate2Collection { certificate };
			mStore.Add(hostname, value);
			if (hostname.StartsWith("*."))
			{
				mWildcardHostStore.Add(hostname.Substring(1), value);
			}
		}
		else
		{
			if (value.Contains(certificate))
			{
				return false;
			}
			value.Add(certificate);
		}
		return true;
	}

	public bool Remove(string hostname, X509Certificate certificate)
	{
		if (hostname == null || certificate == null)
		{
			throw new ArgumentNullException();
		}
		if (mReadOnly)
		{
			throw new InvalidOperationException();
		}
		if (!mStore.TryGetValue(hostname, out var value))
		{
			return false;
		}
		int count = value.Count;
		value.Remove(certificate);
		if (value.Count <= 0)
		{
			mStore.Remove(hostname);
			if (hostname.StartsWith("*."))
			{
				mWildcardHostStore.Remove(hostname.Substring(1));
			}
		}
		return count != value.Count;
	}

	public void Import(string fileName)
	{
		using FileStream source = File.OpenRead(fileName);
		Import(source);
	}

	public void Import(Stream source)
	{
		if (mReadOnly)
		{
			throw new InvalidOperationException();
		}
		mStore.Clear();
		mWildcardHostStore.Clear();
		BinaryReader binaryReader = new BinaryReader(source, Encoding.UTF8);
		try
		{
			if (binaryReader.ReadInt32() != 5460040)
			{
				throw new FormatException("Unrecognized file format");
			}
			byte b = binaryReader.ReadByte();
			switch (b)
			{
			case 2:
			{
				X509Certificate2Collection x509Certificate2Collection = new X509Certificate2Collection();
				int num = binaryReader.ReadInt32();
				for (int i = 0; i < num; i++)
				{
					string text2 = binaryReader.ReadString();
					X509Certificate2Collection x509Certificate2Collection2 = new X509Certificate2Collection();
					int num2 = binaryReader.ReadInt32();
					for (int j = 0; j < num2; j++)
					{
						X509Certificate2 certificate;
						if (binaryReader.ReadBoolean())
						{
							certificate = new X509Certificate2(binaryReader.ReadBytes(binaryReader.ReadInt32()));
							x509Certificate2Collection.Add(certificate);
						}
						else
						{
							certificate = x509Certificate2Collection[binaryReader.ReadInt32()];
						}
						x509Certificate2Collection2.Add(certificate);
					}
					try
					{
						mStore.Add(text2, x509Certificate2Collection2);
						if (text2.StartsWith("*."))
						{
							mWildcardHostStore.Add(text2.Substring(1), x509Certificate2Collection2);
						}
					}
					catch (ArgumentException innerException)
					{
						throw new FormatException(text2 + " is already registered in the pinset", innerException);
					}
				}
				break;
			}
			case 1:
			{
				Import(mStore, binaryReader);
				Import(mWildcardHostStore, binaryReader);
				string text = "*.".Substring(0, 1);
				{
					foreach (KeyValuePair<string, X509Certificate2Collection> item in mWildcardHostStore)
					{
						mStore.Add(text + item.Key, item.Value);
					}
					break;
				}
			}
			default:
				throw new FormatException("Only versions " + (byte)1 + "-" + (byte)2 + " are supported, not version " + b);
			}
		}
		catch (EndOfStreamException innerException2)
		{
			throw new FormatException("End of stream encountered before parsing completed", innerException2);
		}
	}

	private static void Import(Dictionary<string, X509Certificate2Collection> pinsetStore, BinaryReader reader)
	{
		int num = reader.ReadInt32();
		for (int i = 0; i < num; i++)
		{
			string text = reader.ReadString();
			X509Certificate2Collection x509Certificate2Collection = new X509Certificate2Collection();
			int num2 = reader.ReadInt32();
			for (int j = 0; j < num2; j++)
			{
				x509Certificate2Collection.Add(new X509Certificate2(reader.ReadBytes(reader.ReadInt32())));
			}
			try
			{
				pinsetStore.Add(text, x509Certificate2Collection);
			}
			catch (ArgumentException innerException)
			{
				throw new FormatException(text + " is already registered in the pinset", innerException);
			}
		}
	}

	public void Export(string fileName)
	{
		using FileStream destination = File.Create(fileName);
		Export(destination);
	}

	public void Export(Stream destination)
	{
		BinaryWriter binaryWriter = new BinaryWriter(destination, Encoding.UTF8);
		binaryWriter.Write(5460040);
		binaryWriter.Write((byte)2);
		X509Certificate2Collection x509Certificate2Collection = new X509Certificate2Collection();
		binaryWriter.Write(mStore.Count);
		foreach (KeyValuePair<string, X509Certificate2Collection> item in mStore)
		{
			binaryWriter.Write(item.Key);
			binaryWriter.Write(item.Value.Count);
			foreach (X509Certificate2 item2 in item.Value)
			{
				int num = x509Certificate2Collection.IndexOf(item2);
				if (num < 0)
				{
					binaryWriter.Write(value: true);
					byte[] array = item2.Export(X509ContentType.Cert);
					binaryWriter.Write(array.Length);
					binaryWriter.Write(array);
					x509Certificate2Collection.Add(item2);
				}
				else
				{
					binaryWriter.Write(value: false);
					binaryWriter.Write(num);
				}
			}
		}
		binaryWriter.Flush();
	}

	public bool Verify(string hostname, X509Certificate certificate)
	{
		bool result = true;
		_ = mTraceSwitch.TraceVerbose;
		if (mStore.TryGetValue(hostname, out var value))
		{
			if (value.Contains(certificate))
			{
				_ = mTraceSwitch.TraceInfo;
				return true;
			}
			_ = mTraceSwitch.TraceVerbose;
			result = false;
		}
		foreach (KeyValuePair<string, X509Certificate2Collection> item in mWildcardHostStore)
		{
			if (hostname.EndsWith(item.Key))
			{
				if (item.Value.Contains(certificate))
				{
					_ = mTraceSwitch.TraceInfo;
					return true;
				}
				_ = mTraceSwitch.TraceVerbose;
				result = false;
			}
		}
		if (mTraceSwitch.TraceWarning)
		{
		}
		return result;
	}
}
