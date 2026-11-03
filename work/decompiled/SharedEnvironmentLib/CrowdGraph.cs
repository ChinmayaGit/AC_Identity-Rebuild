using System;
using System.Collections.Generic;
using UbiCore.Helpers;
using UnityEngine;

public class CrowdGraph : MonoBehaviour
{
	[Serializable]
	public class CrowdNode
	{
		[Serializable]
		public class Flow
		{
			public Vector3 Direction;

			public double NormalizedWeight;

			public float Spread;
		}

		public Vector3 CenterPosition;

		public float Radius;

		public double NormalizedDensity;

		public double NormalizedArea;

		public List<Vector3> Edges = new List<Vector3>();

		public List<Flow> Flows = new List<Flow>();

		public List<int> Neighbors = new List<int>();

		public float DynamicWeight = 1f;
	}

	public SortedList<float, CrowdStation> CrowdStations = new SortedList<float, CrowdStation>();

	public float CrowdStationWeightSum;

	public float NavMeshSampleRadius = 0.5f;

	public Bounds SceneBounds = new Bounds(Vector3.zero, Vector3.one);

	public int PickPointTries = 50;

	public List<CrowdNode> CrowdNodes = new List<CrowdNode>();

	private static CrowdGraph m_instance;

	public static CrowdGraph Instance
	{
		get
		{
			if (m_instance == null)
			{
				m_instance = UnityEngine.Object.FindObjectOfType<CrowdGraph>();
				if (m_instance == null)
				{
					m_instance = new GameObject("CrowdScenario").AddComponent<CrowdGraph>();
					GameObject gameObject = GameObject.Find("EnvScenario");
					if ((bool)gameObject)
					{
						m_instance.transform.parent = gameObject.transform;
					}
				}
				m_instance.Initialize();
			}
			return m_instance;
		}
	}

	public void Release()
	{
		m_instance = null;
	}

	public CrowdStation GetRandomCrowdStation()
	{
		float num = UnityEngine.Random.value * CrowdStationWeightSum;
		foreach (KeyValuePair<float, CrowdStation> crowdStation in CrowdStations)
		{
			if (num <= crowdStation.Key)
			{
				return crowdStation.Value;
			}
		}
		return null;
	}

	public void Start()
	{
		Initialize();
	}

	public void Initialize()
	{
		CrowdStations.Clear();
		CrowdStationWeightSum = 0f;
		CrowdStation[] array = UnityEngine.Object.FindObjectsOfType<CrowdStation>();
		foreach (CrowdStation crowdStation in array)
		{
			Renderer component = crowdStation.GetComponent<Renderer>();
			float num = component.bounds.size.x * component.bounds.size.z;
			CrowdStationWeightSum += num * crowdStation.Weight;
			CrowdStations.Add(CrowdStationWeightSum, crowdStation);
		}
		SceneBounds = SceneTools.GetSceneCollidersBounds();
	}

	public NavMeshPath GetPathToRandomDestination(NavMeshAgent _agent)
	{
		return GetPathWithinBounds(_agent, SceneBounds);
	}

	public NavMeshPath GetPathToRandomCrowdStation(NavMeshAgent _agent)
	{
		CrowdStation randomCrowdStation = GetRandomCrowdStation();
		if (randomCrowdStation == null)
		{
			return null;
		}
		return GetPathWithinBounds(_agent, randomCrowdStation.GetComponent<Renderer>().bounds);
	}

	public NavMeshPath GetPathWithinBounds(NavMeshAgent _agent, Bounds _bounds)
	{
		Vector3 point = Vector3.zero;
		Vector3 zero = Vector3.zero;
		int num = PickPointTries;
		do
		{
			num--;
			if (num < 0)
			{
				return null;
			}
			zero = new Vector3(UnityEngine.Random.Range(_bounds.min.x, _bounds.max.x), _bounds.max.y, UnityEngine.Random.Range(_bounds.min.z, _bounds.max.z));
		}
		while (!SceneTools.PickNavMeshPoint(new Ray(zero, Vector3.down), out point, NavMeshSampleRadius, _agent.areaMask));
		NavMeshPath navMeshPath = new NavMeshPath();
		NavMesh.CalculatePath(_agent.transform.position, point, _agent.areaMask, navMeshPath);
		return navMeshPath;
	}

	public NavMeshPath GetRandomPath(Bounds _bounds, int walkableMask = -1)
	{
		Vector3 point = Vector3.zero;
		Vector3 point2 = Vector3.zero;
		Vector3 zero = Vector3.zero;
		int num = PickPointTries;
		do
		{
			num--;
			if (num < 0)
			{
				return null;
			}
			zero = new Vector3(UnityEngine.Random.Range(_bounds.min.x, _bounds.max.x), _bounds.max.y, UnityEngine.Random.Range(_bounds.min.z, _bounds.max.z));
		}
		while (!SceneTools.PickNavMeshPoint(new Ray(zero, Vector3.down), out point, NavMeshSampleRadius, walkableMask));
		num = PickPointTries;
		do
		{
			num--;
			if (num < 0)
			{
				return null;
			}
			zero = new Vector3(UnityEngine.Random.Range(_bounds.min.x, _bounds.max.x), _bounds.max.y, UnityEngine.Random.Range(_bounds.min.z, _bounds.max.z));
		}
		while (!SceneTools.PickNavMeshPoint(new Ray(zero, Vector3.down), out point2, NavMeshSampleRadius, walkableMask));
		NavMeshPath navMeshPath = new NavMeshPath();
		NavMesh.CalculatePath(point, point2, walkableMask, navMeshPath);
		return navMeshPath;
	}
}
