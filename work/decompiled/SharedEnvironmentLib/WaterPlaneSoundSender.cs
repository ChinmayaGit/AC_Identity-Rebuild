using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class WaterPlaneSoundSender : MonoBehaviour
{
	public GameObject AudioConfiguration;

	private List<Transform> m_border = new List<Transform>();

	public List<Transform> Border => m_border;

	public void OnEnable()
	{
		Border.AddRange(GetComponentsInChildren<Transform>());
		Border.Remove(transform);
	}

	public void OnDrawGizmosSelected()
	{
		if (Border != null)
		{
			for (int i = 1; i <= Border.Count; i++)
			{
				Vector3 position = Border[(i - 1) % Border.Count].position;
				Vector3 position2 = Border[i % Border.Count].position;
				Gizmos.DrawLine(position, position2);
			}
		}
	}
}
