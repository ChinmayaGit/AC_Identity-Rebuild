using UnityEngine;

public class AliceTheBuildConfig : MonoBehaviour
{
	public enum PreprocessingSteps
	{
		Wind,
		Atlases,
		Lightmap,
		Occlusion
	}

	public enum ExportSteps
	{
		AssetPackages,
		AssetBundles
	}

	[SerializeField]
	private int m_preprocessingMask = int.MaxValue;

	[SerializeField]
	private bool m_skipScene;

	[SerializeField]
	private int m_exportMask = int.MaxValue;

	public bool skipScene
	{
		get
		{
			return m_skipScene;
		}
		set
		{
			m_skipScene = value;
		}
	}

	public int preprocessingMask
	{
		get
		{
			return m_preprocessingMask;
		}
		set
		{
			m_preprocessingMask = value;
		}
	}

	public int exportMask
	{
		get
		{
			return m_exportMask;
		}
		set
		{
			m_exportMask = value;
		}
	}

	public static AliceTheBuildConfig FindAlice()
	{
		return (AliceTheBuildConfig)Object.FindObjectOfType(typeof(AliceTheBuildConfig));
	}
}
