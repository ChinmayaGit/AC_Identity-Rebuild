using AcierNetwork.Communication;
using AssetBundleData;
using SharedBaseLib.Core;

namespace AcierNetwork.Provider;

public class MissionProviderInput : Params
{
	public delegate void TAssetBundleLoaded(AssetBundleInfo _bunldeInfo);

	public BBHashSet<AssetBundleInfo> AssetBundles { get; set; }

	public TAssetBundleLoaded AssetBundleLoaded { get; set; }
}
