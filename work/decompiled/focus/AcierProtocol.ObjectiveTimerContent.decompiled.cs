using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "ObjectiveTimerContent")]
public class ObjectiveTimerContent : IExtensible
{
	private ObjectiveBaseContent _ObjectiveBaseContent;

	private int _TimeInSeconds;

	private ObjectiveResult _InitialResult;

	private ObjectiveResult _CompletionResult;

	private ObjectiveResult _TerminationResult;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = false, Name = "ObjectiveBaseContent", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ObjectiveBaseContent ObjectiveBaseContent
	{
		get
		{
			return _ObjectiveBaseContent;
		}
		set
		{
			_ObjectiveBaseContent = value;
		}
	}

	[ProtoMember(2, IsRequired = false, Name = "TimeInSeconds", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0)]
	public int TimeInSeconds
	{
		get
		{
			return _TimeInSeconds;
		}
		set
		{
			_TimeInSeconds = value;
		}
	}

	[ProtoMember(3, IsRequired = false, Name = "InitialResult", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ObjectiveResult InitialResult
	{
		get
		{
			return _InitialResult;
		}
		set
		{
			_InitialResult = value;
		}
	}

	[ProtoMember(4, IsRequired = false, Name = "CompletionResult", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ObjectiveResult CompletionResult
	{
		get
		{
			return _CompletionResult;
		}
		set
		{
			_CompletionResult = value;
		}
	}

	[ProtoMember(5, IsRequired = false, Name = "TerminationResult", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public ObjectiveResult TerminationResult
	{
		get
		{
			return _TerminationResult;
		}
		set
		{
			_TerminationResult = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
