using AcierNetwork.Communication;
using AssetBundleData;
using SharedBaseLib.Core;

namespace AcierNetwork.Provider;

public class MissionData : Model
{
	public BBHashSet<AssetBundleInfo> m_assetBundleInfo = new BBHashSet<AssetBundleInfo>();

	public void AddAssetBundle(AssetBundleInfo _assetBundleInfo)
	{
		m_assetBundleInfo.Add(_assetBundleInfo);
	}
}
