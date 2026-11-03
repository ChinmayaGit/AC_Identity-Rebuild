using System;
using System.ComponentModel;
using ProtoBuf;

namespace AcierProtocol;

[Serializable]
[ProtoContract(Name = "GetQualitySettingsResponse")]
public class GetQualitySettingsResponse : IExtensible
{
	private string _qualitySettingsName = "";

	private QualitySettings _qualitySettings;

	private IExtension extensionObject;

	[ProtoMember(11, IsRequired = false, Name = "qualitySettingsName", DataFormat = DataFormat.Default)]
	[DefaultValue("")]
	public string qualitySettingsName
	{
		get
		{
			return _qualitySettingsName;
		}
		set
		{
			_qualitySettingsName = value;
		}
	}

	[ProtoMember(6, IsRequired = false, Name = "qualitySettings", DataFormat = DataFormat.Default)]
	[DefaultValue(null)]
	public QualitySettings qualitySettings
	{
		get
		{
			return _qualitySettings;
		}
		set
		{
			_qualitySettings = value;
		}
	}

	IExtension IExtensible.GetExtensionObject(bool createIfMissing)
	{
		return Extensible.GetExtensionObject(ref extensionObject, createIfMissing);
	}
}
