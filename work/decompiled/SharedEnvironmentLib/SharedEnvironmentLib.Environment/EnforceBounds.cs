using UnityEngine;

namespace SharedEnvironmentLib.Environment;

[RequireComponent(typeof(MeshFilter))]
internal class EnforceBounds : MonoBehaviour
{
	public MeshFilter TargetFilter;

	public Vector3 Center = Vector3.zero;

	public Vector3 Extends = Vector3.one;

	public void Start()
	{
		UpdateBounds();
	}

	private void UpdateBounds()
	{
		if (TargetFilter != null)
		{
			TargetFilter.sharedMesh.bounds = new Bounds(Center, Extends);
		}
	}
}
