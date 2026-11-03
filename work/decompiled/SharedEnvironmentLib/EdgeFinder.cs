using System.Collections.Generic;
using UnityEngine;

public class EdgeFinder
{
	private readonly List<Guidance.Edge> m_edgeList = new List<Guidance.Edge>();

	public List<Guidance.Edge> GetEdgeList()
	{
		return m_edgeList;
	}

	public void BuildEdgeList(Matrix4x4 _matrix, EdgeFilter _edgeFilter, Mesh _meshShape, TrianglesNeighbor _trianglesNeighbor)
	{
		Vector3 vector = Vector3.zero;
		TrianglesNeighbor.TriangleNeighbor[] neighbor = _trianglesNeighbor.GetNeighbor();
		for (int i = 0; i < _meshShape.triangles.Length / 3; i++)
		{
			Vector3 normalTriangle = _trianglesNeighbor.GetNormalTriangle(i);
			for (int j = 0; j < 3; j++)
			{
				bool flag = false;
				int num = neighbor[i].m_indicesTriangles[j];
				if (num != -1)
				{
					if (neighbor[num].m_flag == 0)
					{
						vector = _trianglesNeighbor.GetNormalTriangle(num);
						if (_edgeFilter.AcceptEdge(normalTriangle, vector))
						{
							flag = true;
						}
					}
				}
				else if (_edgeFilter.AcceptEdge(normalTriangle))
				{
					vector = normalTriangle;
					flag = true;
				}
				if (flag)
				{
					int[] triangles = _meshShape.triangles;
					int num2 = triangles[3 * i + (j + 1) % 3];
					int num3 = triangles[3 * i + (j + 2) % 3];
					Guidance.Edge edge = new Guidance.Edge();
					Vector4 vector2 = _meshShape.vertices[num2];
					vector2.w = 1f;
					edge.start = _matrix * vector2;
					Vector4 vector3 = _meshShape.vertices[num3];
					vector3.w = 1f;
					edge.end = _matrix * vector3;
					edge.startNormal = (_matrix * normalTriangle).normalized;
					edge.endNormal = (_matrix * vector).normalized;
					m_edgeList.Add(edge);
				}
			}
			neighbor[i].m_flag = 1;
		}
	}
}
