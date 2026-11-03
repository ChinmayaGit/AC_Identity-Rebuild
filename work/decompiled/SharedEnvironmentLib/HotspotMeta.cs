public class HotspotMeta : StaticObjectMeta
{
	public eHotspotType Type = eHotspotType.Invalid;

	public override void UpdateUniqueID()
	{
		UniqueID = StaticObjectMeta.GenerateUniqueID(this, Type.ToString("F"));
	}
}
