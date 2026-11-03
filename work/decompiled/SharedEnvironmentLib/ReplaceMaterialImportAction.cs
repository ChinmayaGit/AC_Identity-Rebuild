using UnityEngine;

public class ReplaceMaterialImportAction : StaticImportAction
{
	[SerializeField]
	private Material m_sourceMaterial;

	[SerializeField]
	private Material m_targetMaterial;

	public override void Execute()
	{
		Renderer[] array = (Renderer[])Object.FindObjectsOfType(typeof(Renderer));
		foreach (Renderer renderer in array)
		{
			Material[] sharedMaterials = renderer.sharedMaterials;
			for (int j = 0; j < sharedMaterials.Length; j++)
			{
				if (sharedMaterials[j] == m_sourceMaterial)
				{
					sharedMaterials[j] = m_targetMaterial;
				}
			}
			renderer.sharedMaterials = sharedMaterials;
		}
	}
}
