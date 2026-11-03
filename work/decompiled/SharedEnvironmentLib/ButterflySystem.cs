using System;
using System.Collections.Generic;
using SharedBaseLib.System;
using UnityEngine;

public class ButterflySystem : MonoBehaviour
{
	public int ButterflyQuantity = 12;

	public float MaximumHeight = 25f;

	public float MinimumHeight;

	public float KillRange = 20f;

	public ParticleSystem ButterflyMovement;

	public List<GameObject> ButterflyPrefabs;

	public List<GameObject> Butterflies;

	public List<Vector3> Point;

	public List<float> Radius;

	public bool HideGizmos;

	public float Distance = 20f;

	public float MinSpawnRange = 5f;

	public float MaxSpawnRange = 20f;

	public float MinSpawnHeight = 1f;

	public float MaxSpawnHeight = 6f;

	private Vector3 targetPosition;

	private ParticleSystem.Particle[] Particles;

	private void Awake()
	{
		SetUpSystem();
		ButterflyPrefabs.RemoveAll((GameObject _o) => _o == null);
		if (ButterflyPrefabs.Count == 0)
		{
			Debug.LogError("ButterflySystem attached, but there are no butterfly prefabs! [Max Stolze]");
		}
	}

	private void SetUpSystem()
	{
		if (!(Camera.main == null) && ButterflyPrefabs.Count != 0)
		{
			ButterflyMovement.maxParticles = ButterflyQuantity;
			targetPosition = Camera.main.transform.position + Camera.main.transform.rotation * Vector3.forward * Distance;
			for (int i = 0; i < ButterflyQuantity; i++)
			{
				GameObject item = UnityEngine.Object.Instantiate(ButterflyPrefabs[UnityEngine.Random.Range(0, ButterflyPrefabs.Count)]);
				Butterflies.Add(item);
			}
			ButterflyMovement.Emit(ButterflyQuantity);
			Particles = new ParticleSystem.Particle[ButterflyQuantity];
			ButterflyMovement.GetParticles(Particles);
			for (int j = 0; j < Particles.Length; j++)
			{
				Particles[j].position = getPointOnNavmesh();
			}
			ButterflyMovement.SetParticles(Particles, ButterflyQuantity);
		}
	}

	public void Update()
	{
		if (Camera.main == null || ButterflyMovement == null || ButterflyPrefabs.Count == 0)
		{
			return;
		}
		if (Butterflies.Count == 0)
		{
			SetUpSystem();
		}
		ButterflyMovement.GetParticles(Particles);
		Vector3 position = Camera.main.transform.position;
		targetPosition = position + Camera.main.transform.rotation * Vector3.forward * Distance;
		if (CameraInBounds(position))
		{
			for (int i = 0; i < Butterflies.Count; i++)
			{
				Vector3 position2 = Butterflies[i].transform.position;
				if (!Butterflies[i].activeSelf)
				{
					Butterflies[i].SetActive(value: true);
				}
				if (i >= Particles.Length)
				{
					gameObject.SetActive(value: false);
					break;
				}
				if (Particles[i].lifetime < 0.5f)
				{
					Particles[i].lifetime = ButterflyMovement.startLifetime;
				}
				if (position2.y > MaximumHeight)
				{
					Particles[i].velocity = new Vector3(Particles[i].velocity.x, -1f, Particles[i].velocity.z);
				}
				if (position2.y < MinimumHeight)
				{
					Particles[i].velocity = new Vector3(Particles[i].velocity.x, 1f, Particles[i].velocity.z);
				}
				if (Vector3.Distance(targetPosition, position2) > KillRange)
				{
					Particles[i].position = getPointOnNavmesh();
					Butterflies[i].transform.position = Particles[i].position;
				}
				else
				{
					Butterflies[i].transform.position = Particles[i].position;
				}
			}
		}
		else
		{
			for (int j = 0; j < Butterflies.Count; j++)
			{
				Vector3 position3 = Butterflies[j].transform.position;
				if (j >= Particles.Length)
				{
					gameObject.SetActive(value: false);
					break;
				}
				if (Particles[j].lifetime < 0.5f)
				{
					Particles[j].lifetime = ButterflyMovement.startLifetime;
				}
				if (position3.y > MaximumHeight)
				{
					Particles[j].velocity = new Vector3(Particles[j].velocity.x, -1f, Particles[j].velocity.z);
				}
				if (Vector3.Distance(targetPosition, position3) > KillRange / 2f)
				{
					Particles[j].position = getPointOnNavmesh();
					Butterflies[j].transform.position = Particles[j].position;
					Butterflies[j].SetActive(value: false);
				}
				else
				{
					Butterflies[j].transform.position = Particles[j].position;
				}
			}
		}
		ButterflyMovement.SetParticles(Particles, ButterflyQuantity);
	}

	private Vector3 getPointOnNavmesh()
	{
		Vector3 newRandomPosition = getNewRandomPosition();
		NavMeshHit hit = default;
		if (NavMesh.SamplePosition(newRandomPosition, out hit, MaxSpawnRange, NavMeshLayers.GroundSurfaceLayerMask))
		{
			return hit.position + new Vector3(0f, UnityEngine.Random.Range(MinSpawnHeight, MaxSpawnHeight), 0f);
		}
		return Vector3.zero;
	}

	public Vector3 getNewRandomPosition()
	{
		float f = UnityEngine.Random.value * (float)Math.PI * 2f;
		float x = Mathf.Cos(f) * UnityEngine.Random.Range(MinSpawnRange, MaxSpawnRange);
		float z = Mathf.Sin(f) * UnityEngine.Random.Range(MinSpawnRange, MaxSpawnRange);
		return targetPosition + new Vector3(x, 0f, z);
	}

	private bool CameraInBounds(Vector3 CameraPosition)
	{
		for (int i = 0; i < Point.Count; i++)
		{
			if (Vector3.Distance(CameraPosition, Point[i]) < Radius[i])
			{
				return true;
			}
		}
		return false;
	}

	private void OnDrawGizmos()
	{
		if (!HideGizmos)
		{
			if (Application.isPlaying)
			{
				Gizmos.color = new Color(0f, 0f, 0.5f, 0.5f);
				Gizmos.DrawSphere(targetPosition, MinSpawnRange);
				Gizmos.color = new Color(0f, 0f, 1f, 0.25f);
				Gizmos.DrawSphere(targetPosition, MaxSpawnRange);
				Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
				Gizmos.DrawSphere(targetPosition, KillRange);
			}
			for (int i = 0; i < Point.Count; i++)
			{
				Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
				Gizmos.DrawSphere(Point[i], Radius[i]);
			}
		}
	}
}
