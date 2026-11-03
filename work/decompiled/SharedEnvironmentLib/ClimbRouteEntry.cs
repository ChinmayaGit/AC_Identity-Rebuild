using System;
using UnityEngine;

[Serializable]
public class ClimbRouteEntry
{
	[SerializeField]
	private EdgeObject m_Edge;

	[SerializeField]
	private Vector3 m_ClosestPoint;

	public EdgeObject Edge
	{
		get
		{
			return m_Edge;
		}
		set
		{
			m_Edge = value;
		}
	}

	public Vector3 ClosestPoint
	{
		get
		{
			return m_ClosestPoint;
		}
		set
		{
			m_ClosestPoint = value;
		}
	}

	public ClimbRouteEntry(EdgeObject _edge, Vector3 _closestPoint)
	{
		Edge = _edge;
		ClosestPoint = _closestPoint;
	}
}
