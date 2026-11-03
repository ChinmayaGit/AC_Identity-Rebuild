using UnityEngine;

public abstract class StaticObjectMeta : MonoBehaviour
{
	public int UniqueID = -1;

	public bool Active = true;

	public Vector3 LastPosition = Vector3.zero;

	public void OnEnable()
	{
		UpdateUniqueID();
	}

	public abstract void UpdateUniqueID();

	public static int GenerateUniqueID(StaticObjectMeta _hotspotMeta, string _prefix)
	{
		int result = _hotspotMeta.UniqueID;
		Vector3 position = _hotspotMeta.transform.position;
		position *= 100f;
		if (position != _hotspotMeta.LastPosition)
		{
			_hotspotMeta.LastPosition = position;
			int num = Mathf.RoundToInt(position.x);
			int num2 = Mathf.RoundToInt(position.y);
			int num3 = Mathf.RoundToInt(position.z);
			result = (_prefix + "_" + num + num2 + num3).GetHashCode();
		}
		return result;
	}
}
