using System;
using SharedBaseLib.Core;
using UnityEngine;

public class TrianglesNeighbor
{
	public class TriangleNeighbor
	{
		public int m_flag;

		public int[] m_indicesTriangles;

		public TriangleNeighbor()
		{
			m_indicesTriangles = new int[3];
			m_indicesTriangles[0] = (m_indicesTriangles[1] = (m_indicesTriangles[2] = -1));
			m_flag = 0;
		}
	}

	public const int PopTriangleNeighborIndexInvalid = -1;

	private TriangleNeighbor[] m_neighbor;

	private int[] m_sourceMeshShapeTriangles;

	private Vector3[] m_sourceMeshShapeVertices;

	public static float GetAngle(Vector3 _v0, Vector3 _v1, Vector3 _axis)
	{
		return GetAngleNormalized(_v0.normalized, _v1.normalized, _axis.normalized);
	}

	public static float GetAngleNormalized(Vector3 _v0, Vector3 _v1, Vector3 _axis)
	{
		float num = Vector3.Dot(_v0, _v1);
		if (num >= 1f)
		{
			return 0f;
		}
		if (num <= -1f)
		{
			return (float)Math.PI;
		}
		if (GetSinusNormalized(_v0, _v1, _axis) < 0f)
		{
			return 0f - Mathf.Acos(num);
		}
		return Mathf.Acos(num);
	}

	public static float GetSinusNormalized(Vector3 _v0, Vector3 _v1, Vector3 _axis)
	{
		Vector3 lhs = Vector3.Cross(_v0, _v1);
		float sqrMagnitude = lhs.sqrMagnitude;
		if (Vector3.Dot(lhs, _axis) < 0f)
		{
			return 0f - sqrMagnitude;
		}
		return sqrMagnitude;
	}

	public Vector3 GetNormalTriangle(int _index)
	{
		int[] sourceMeshShapeTriangles = m_sourceMeshShapeTriangles;
		Vector3[] sourceMeshShapeVertices = m_sourceMeshShapeVertices;
		Vector3 vector = sourceMeshShapeVertices[sourceMeshShapeTriangles[_index * 3 + 0]];
		Vector3 vector2 = sourceMeshShapeVertices[sourceMeshShapeTriangles[_index * 3 + 1]];
		Vector3 vector3 = sourceMeshShapeVertices[sourceMeshShapeTriangles[_index * 3 + 2]];
		Vector3 lhs = vector2 - vector;
		Vector3 rhs = vector3 - vector;
		return Vector3.Cross(lhs, rhs).normalized;
	}

	private int[] GetTrianglesInAABB(Vector3 _min, Vector3 _max)
	{
		Bounds bounds = new Bounds
		{
			min = _min,
			max = _max
		};
		BBList<int> bBList = new BBList<int>();
		for (int i = 0; i < m_sourceMeshShapeTriangles.Length / 3; i++)
		{
			bool flag = false;
			for (int j = 0; j < 3; j++)
			{
				int num = m_sourceMeshShapeTriangles[i * 3 + j];
				if (bounds.Contains(m_sourceMeshShapeVertices[num]))
				{
					flag = true;
					break;
				}
			}
			if (flag)
			{
				bBList.Add(i);
			}
		}
		return bBList.ToArray();
	}

	public TriangleNeighbor[] GetNeighbor()
	{
		return m_neighbor;
	}

	public void FindTriangleNeighbor()
	{
		m_neighbor = new TriangleNeighbor[m_sourceMeshShapeTriangles.Length];
		for (int i = 0; i < m_neighbor.Length; i++)
		{
			m_neighbor[i] = new TriangleNeighbor();
		}
		int[] sourceMeshShapeTriangles = m_sourceMeshShapeTriangles;
		Vector3[] sourceMeshShapeVertices = m_sourceMeshShapeVertices;
		if (sourceMeshShapeTriangles.Length != 0)
		{
			for (int j = 0; j < m_sourceMeshShapeTriangles.Length / 3; j++)
			{
				int num = sourceMeshShapeTriangles[3 * j];
				int num2 = sourceMeshShapeTriangles[3 * j + 1];
				int num3 = sourceMeshShapeTriangles[3 * j + 2];
				if (TriangleIsDegenerate(3 * j))
				{
					continue;
				}
				Vector3 lhs = sourceMeshShapeVertices[num];
				Vector3 lhs2 = sourceMeshShapeVertices[num2];
				Vector3 rhs = sourceMeshShapeVertices[num3];
				Vector3 min = Vector3.Min(lhs, Vector3.Min(lhs2, rhs));
				Vector3 max = Vector3.Max(lhs, Vector3.Max(lhs2, rhs));
				min += new Vector3(-0.01f, -0.01f, -0.01f);
				max += new Vector3(0.01f, 0.01f, 0.01f);
				int[] trianglesInAABB = GetTrianglesInAABB(min, max);
				for (uint num4 = 0u; num4 < trianglesInAABB.Length; num4++)
				{
					int num5 = trianglesInAABB[num4];
					if (num5 <= j || TriangleIsDegenerate(3 * num5) || !TrianglesAreAttached(3 * j, 3 * num5, out var _indexEdgeTri, out var _indexEdgeTri2))
					{
						continue;
					}
					if (m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] == -1 && m_neighbor[j].m_indicesTriangles[_indexEdgeTri] == -1)
					{
						m_neighbor[j].m_indicesTriangles[_indexEdgeTri] = num5;
						m_neighbor[j].m_flag &= ~(1 << _indexEdgeTri);
						m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] = j;
						m_neighbor[num5].m_flag &= ~(1 << _indexEdgeTri2);
						continue;
					}
					int num6 = m_neighbor[j].m_indicesTriangles[_indexEdgeTri];
					int num7;
					if ((m_neighbor[j].m_flag & (1 << _indexEdgeTri)) == 0 && num6 != -1)
					{
						if (m_neighbor[num6].m_indicesTriangles[0] == j)
						{
							num7 = 0;
						}
						else if (m_neighbor[num6].m_indicesTriangles[1] == j)
						{
							num7 = 1;
						}
						else
						{
							if (m_neighbor[num6].m_indicesTriangles[2] != j)
							{
								continue;
							}
							num7 = 2;
						}
					}
					else
					{
						if ((m_neighbor[num5].m_flag & (1 << _indexEdgeTri2)) != 0)
						{
							continue;
						}
						num6 = m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2];
						if (m_neighbor[num6].m_indicesTriangles[0] == num5)
						{
							num7 = 0;
						}
						else if (m_neighbor[num6].m_indicesTriangles[1] == num5)
						{
							num7 = 1;
						}
						else
						{
							if (m_neighbor[num6].m_indicesTriangles[2] != num5)
							{
								continue;
							}
							num7 = 2;
						}
					}
					Vector3 normalTriangle = GetNormalTriangle(j);
					Vector3 normalTriangle2 = GetNormalTriangle(num5);
					Vector3 normalTriangle3 = GetNormalTriangle(num6);
					Vector3 axis = Vector3.Cross(normalTriangle, normalTriangle2);
					axis.Normalize();
					Vector3 axis2 = Vector3.Cross(normalTriangle2, normalTriangle3);
					axis2.Normalize();
					Vector3 axis3 = Vector3.Cross(normalTriangle3, normalTriangle);
					axis3.Normalize();
					float num8 = 0f;
					if (axis.sqrMagnitude > 1E-05f)
					{
						num8 = GetAngle(normalTriangle, normalTriangle2, axis) + (float)Math.PI;
					}
					else
					{
						num8 = ((Vector3.Dot(normalTriangle, normalTriangle2) > 0f) ? ((float)Math.PI) : ((float)Math.PI * 2f));
					}
					float num9 = 0f;
					if (axis2.sqrMagnitude > 1E-05f)
					{
						num9 = GetAngle(normalTriangle2, normalTriangle3, axis2) + (float)Math.PI;
					}
					else
					{
						num9 = ((Vector3.Dot(normalTriangle2, normalTriangle3) > 0f) ? ((float)Math.PI) : ((float)Math.PI * 2f));
					}
					float num10 = 0f;
					if (axis3.sqrMagnitude > 1E-05f)
					{
						num10 = GetAngle(normalTriangle3, normalTriangle, axis3) + (float)Math.PI;
					}
					else
					{
						num10 = ((Vector3.Dot(normalTriangle3, normalTriangle) > 0f) ? ((float)Math.PI) : ((float)Math.PI * 2f));
					}
					if (num8 < num9)
					{
						if (num8 < num10)
						{
							m_neighbor[j].m_indicesTriangles[_indexEdgeTri] = num5;
							m_neighbor[j].m_flag &= ~(1 << _indexEdgeTri);
							m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] = j;
							m_neighbor[num5].m_flag &= ~(1 << _indexEdgeTri2);
							m_neighbor[num6].m_indicesTriangles[num7] = -1;
							m_neighbor[num6].m_flag |= 1 << num7;
						}
						else
						{
							m_neighbor[j].m_indicesTriangles[_indexEdgeTri] = num6;
							m_neighbor[j].m_flag &= ~(1 << _indexEdgeTri);
							m_neighbor[num6].m_indicesTriangles[num7] = j;
							m_neighbor[num6].m_flag &= ~(1 << num7);
							m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] = -1;
							m_neighbor[num5].m_flag |= 1 << _indexEdgeTri2;
						}
					}
					else if (num9 < num10)
					{
						m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] = num6;
						m_neighbor[num5].m_flag &= ~(1 << _indexEdgeTri2);
						m_neighbor[num6].m_indicesTriangles[num7] = num5;
						m_neighbor[num6].m_flag &= ~(1 << num7);
						m_neighbor[j].m_indicesTriangles[_indexEdgeTri] = -1;
						m_neighbor[j].m_flag |= 1 << _indexEdgeTri;
					}
					else
					{
						m_neighbor[num6].m_indicesTriangles[num7] = j;
						m_neighbor[num6].m_flag &= ~(1 << num7);
						m_neighbor[j].m_indicesTriangles[_indexEdgeTri] = num6;
						m_neighbor[j].m_flag &= ~(1 << _indexEdgeTri);
						m_neighbor[num5].m_indicesTriangles[_indexEdgeTri2] = -1;
						m_neighbor[num5].m_flag |= 1 << _indexEdgeTri2;
					}
				}
			}
		}
		int num11 = 0;
		for (uint num12 = 0u; num12 < m_neighbor.Length; num12++)
		{
			m_neighbor[num12].m_flag = 0;
			if (m_neighbor[num12].m_indicesTriangles[0] != -1)
			{
				num11++;
			}
			if (m_neighbor[num12].m_indicesTriangles[1] != -1)
			{
				num11++;
			}
			if (m_neighbor[num12].m_indicesTriangles[2] != -1)
			{
				num11++;
			}
		}
	}

	private bool TrianglesAreAttached(int _indicesVerticesTri0, int _indicesVerticesTri1, out int _indexEdgeTri0, out int _indexEdgeTri1)
	{
		_indexEdgeTri0 = 0;
		_indexEdgeTri1 = 0;
		int[] array = new int[3];
		array[0] = (array[1] = (array[2] = -1));
		int[] sourceMeshShapeTriangles = m_sourceMeshShapeTriangles;
		for (int i = 0; i < 3; i++)
		{
			for (int j = 0; j < 3; j++)
			{
				if (sourceMeshShapeTriangles[_indicesVerticesTri0 + i] == sourceMeshShapeTriangles[_indicesVerticesTri1 + j])
				{
					array[i] = j;
					continue;
				}
				Vector3[] sourceMeshShapeVertices = m_sourceMeshShapeVertices;
				Vector3 vector = sourceMeshShapeVertices[sourceMeshShapeTriangles[_indicesVerticesTri0 + i]];
				Vector3 vector2 = sourceMeshShapeVertices[sourceMeshShapeTriangles[_indicesVerticesTri1 + j]];
				if (Vector3.SqrMagnitude(vector - vector2) < 1E-05f)
				{
					array[i] = j;
				}
			}
		}
		if (array[0] != -1 && array[1] != -1 && array[0] != array[1])
		{
			_indexEdgeTri0 = 2;
			_indexEdgeTri1 = 3 - array[0] - array[1];
			return true;
		}
		if (array[0] != -1 && array[2] != -1 && array[0] != array[2])
		{
			_indexEdgeTri0 = 1;
			_indexEdgeTri1 = 3 - array[0] - array[2];
			return true;
		}
		if (array[1] != -1 && array[2] != -1 && array[1] != array[2])
		{
			_indexEdgeTri0 = 0;
			_indexEdgeTri1 = 3 - array[1] - array[2];
			return true;
		}
		return false;
	}

	private bool TriangleIsDegenerate(int _indicesVerticesTri)
	{
		int[] array = new int[3]
		{
			m_sourceMeshShapeTriangles[_indicesVerticesTri + 0],
			m_sourceMeshShapeTriangles[_indicesVerticesTri + 1],
			m_sourceMeshShapeTriangles[_indicesVerticesTri + 2]
		};
		if (array[0] == array[1] || array[0] == array[2] || array[1] == array[2])
		{
			return true;
		}
		Vector3[] sourceMeshShapeVertices = m_sourceMeshShapeVertices;
		Vector3 vector = sourceMeshShapeVertices[array[0]];
		Vector3 vector2 = sourceMeshShapeVertices[array[1]];
		Vector3 vector3 = sourceMeshShapeVertices[array[2]];
		if (Vector3.Distance(vector, vector3) < 1E-05f || Vector3.Distance(vector, vector2) < 1E-05f || Vector3.Distance(vector2, vector3) < 1E-05f)
		{
			return true;
		}
		Vector3 normalized = (vector2 - vector).normalized;
		Vector3 normalized2 = (vector3 - vector).normalized;
		Vector3 normalized3 = (vector3 - vector2).normalized;
		float f = Vector3.Dot(normalized, normalized2);
		float f2 = Vector3.Dot(normalized, normalized3);
		if (Mathf.Abs(f) > 0.9999f)
		{
			return Mathf.Abs(f2) > 0.9999f;
		}
		return false;
	}

	public void Init(Mesh _meshShape)
	{
		m_sourceMeshShapeTriangles = _meshShape.triangles;
		m_sourceMeshShapeVertices = _meshShape.vertices;
	}
}
