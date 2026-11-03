using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "GetStaticsResponse")]
public class GetStaticsResponse : IExtensible
{
	private Statics _statics;

	private IExtension extensionObject;

	[ProtoMember(1, IsRequired = false, Name = "statics", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public Statics statics
	{
		get
		{
			return _statics;
		}
		set
		{
			_statics = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
