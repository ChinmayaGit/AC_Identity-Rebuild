using System;
using UnityEngine;

public class SpawnEventListener : MonoBehaviour
{
	[SerializeField]
	private Spawnable[] m_prefabs;

	public void OnSpawnEvent(string _name)
	{
		Spawnable spawnable = Array.Find(m_prefabs, (Spawnable o) => o.name == _name);
		if (spawnable != null)
		{
			if (spawnable.prefab != null)
			{
				UnityEngine.Object.Instantiate(spawnable.prefab, transform.position, transform.rotation);
			}
			else
			{
				Debug.LogWarning("Found a definition for '" + _name + "' but no object is referenced. Could not spawn the prefab from a timeline.");
			}
		}
		else
		{
			Debug.LogWarning("Could not find '" + _name + "' to spawn it from a timeline. GameObject is not instantiated.");
		}
	}
}
