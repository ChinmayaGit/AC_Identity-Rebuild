using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "CheckVersionResponse")]
public class CheckVersionResponse : IExtensible
{
	private bool _needClientUpdate;

	private uint _serverVersion;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = true, Name = "needClientUpdate", DataFormat = DataFormat.Default)]
	public bool needClientUpdate
	{
		get
		{
			return _needClientUpdate;
		}
		set
		{
			_needClientUpdate = value;
		}
	}

	[ProtoMember(2, IsRequired = false, Name = "serverVersion", DataFormat = DataFormat.TwosComplement)]
	[DefaultValue(0L)]
	public uint serverVersion
	{
		get
		{
			return _serverVersion;
		}
		set
		{
			_serverVersion = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
