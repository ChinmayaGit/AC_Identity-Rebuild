using System;
using System.Collections.Generic;
using SharedBaseLib.System;
using UnityEngine;

public class FogEffectSystem : MonoBehaviour
{
	public GameObject[] fogEffects;

	private List<GameObject> m_effectPool = new List<GameObject>();

	public int m_maxEmitter;

	public float minSpawnRange;

	public float maxSpawnRange;

	public float killRange;

	private Vector3 targetPosition;

	public float distance = 20f;

	private void Update()
	{
		if (Camera.main == null)
		{
			return;
		}
		targetPosition = Camera.main.transform.position + Camera.main.transform.rotation * Vector3.forward * distance;
		if (m_effectPool.Count < m_maxEmitter - 1)
		{
			for (int i = 0; i < m_maxEmitter - m_effectPool.Count; i++)
			{
				placeNewFog();
			}
		}
		foreach (GameObject item in m_effectPool)
		{
			if (Vector3.Distance(targetPosition, item.transform.position) > killRange)
			{
				setNewPosition(item.transform);
			}
		}
	}

	private void setNewPosition(Transform t)
	{
		Vector3 pointOnNavmesh = getPointOnNavmesh();
		if (pointOnNavmesh != Vector3.zero)
		{
			t.position = pointOnNavmesh;
			t.rotation = Quaternion.LookRotation(Camera.main.transform.position - pointOnNavmesh);
		}
	}

	private void placeNewFog()
	{
		Vector3 pointOnNavmesh = getPointOnNavmesh();
		if (pointOnNavmesh != Vector3.zero && fogEffects.Length != 0)
		{
			float num = Mathf.Clamp01(Vector3.Distance(pointOnNavmesh, targetPosition) / maxSpawnRange);
			int num2 = Mathf.FloorToInt((float)(fogEffects.Length - 1) * num);
			GameObject item = UnityEngine.Object.Instantiate(fogEffects[num2], pointOnNavmesh, Quaternion.LookRotation(Camera.main.transform.position - pointOnNavmesh)) as GameObject;
			m_effectPool.Add(item);
		}
	}

	private void SetUpFog()
	{
		for (int i = 0; i < m_maxEmitter; i++)
		{
			Vector3 pointOnNavmesh = getPointOnNavmesh();
			if (pointOnNavmesh != Vector3.zero && fogEffects.Length != 0)
			{
				float num = Mathf.Clamp01(Vector3.Distance(pointOnNavmesh, targetPosition) / maxSpawnRange);
				int num2 = Mathf.FloorToInt((float)(fogEffects.Length - 1) * num);
				GameObject item = UnityEngine.Object.Instantiate(fogEffects[num2], pointOnNavmesh, Quaternion.LookRotation(Camera.main.transform.position - pointOnNavmesh)) as GameObject;
				m_effectPool.Add(item);
			}
		}
	}

	private Vector3 getPointOnNavmesh()
	{
		Vector3 newRandomPosition = getNewRandomPosition();
		NavMeshHit hit = default;
		if (NavMesh.SamplePosition(newRandomPosition, out hit, maxSpawnRange, NavMeshLayers.GroundSurfaceLayerMask))
		{
			return hit.position;
		}
		return Vector3.zero;
	}

	public Vector3 getNewRandomPosition()
	{
		float f = UnityEngine.Random.value * (float)Math.PI * 2f;
		float x = Mathf.Cos(f) * UnityEngine.Random.Range(minSpawnRange, maxSpawnRange);
		float z = Mathf.Sin(f) * UnityEngine.Random.Range(minSpawnRange, maxSpawnRange);
		return targetPosition + new Vector3(x, 0f, z);
	}
}
