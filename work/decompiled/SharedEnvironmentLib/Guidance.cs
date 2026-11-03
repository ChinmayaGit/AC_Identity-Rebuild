using System;
using System.Collections.Generic;
using UnityEngine;

public class Guidance : MonoBehaviour
{
	[Serializable]
	public class Edge
	{
		[SerializeField]
		public Vector3 start;

		[SerializeField]
		public Vector3 end;

		[SerializeField]
		public Vector3 startNormal;

		[SerializeField]
		public Vector3 endNormal;
	}

	[SerializeField]
	private List<Edge> m_edgeList = new List<Edge>();

	public List<Edge> EdgeList => m_edgeList;

	public void AddEdges(List<Edge> _edgeList)
	{
		m_edgeList.AddRange(_edgeList);
	}

	private void OnDrawGizmosSelected()
	{
		Color color = Gizmos.color;
		foreach (Edge edge in m_edgeList)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawLine(transform.TransformPoint(edge.start), transform.TransformPoint(edge.end));
			Gizmos.color = Color.green;
			Vector3 vector = transform.TransformPoint(edge.start + (edge.end - edge.start) / 2f);
			Gizmos.DrawLine(vector, vector + transform.TransformDirection(edge.startNormal) * 0.1f);
			Gizmos.DrawLine(vector, vector + transform.TransformDirection(edge.endNormal) * 0.1f);
		}
		Gizmos.color = color;
	}
}
