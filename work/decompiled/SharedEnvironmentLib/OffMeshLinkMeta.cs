using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(OffMeshLink))]
public class OffMeshLinkMeta : MonoBehaviour
{
	public eLinkType Type;

	public int UniqueID;

	public int StartLayerID = -1;

	public int EndLayerID = -1;

	public List<EdgeObject> EdgeObjectList;

	public List<ParkourStep> ParkourStepList;

	public ClimbRoute ClimbRoute;

	public float Quality;

	public bool Valid;

	public bool Locked;

	public static int GenerateUniqueID(OffMeshLinkMeta _meshLinkMeta)
	{
		return GetHashCode(_meshLinkMeta.transform.position);
	}

	private static int GetHashCode(Vector3 position)
	{
		int hashCode = (Mathf.Round(position.x / 1f) * 1f).GetHashCode();
		int hashCode2 = (Mathf.Round(position.y / 1f) * 1f).GetHashCode();
		int hashCode3 = (Mathf.Round(position.z / 1f) * 1f).GetHashCode();
		return (hashCode * 73856093) ^ (hashCode2 * 19349663) ^ (hashCode3 * 83492791);
	}
}
