using System;
using System.Collections.Generic;
using SharedBaseLib.Core;
using SharedBaseLib.System.Extensions;
using UbiCore.Debug;
using UbiCore.Helpers;
using UnityEngine;

public static class EdgeDetector
{
	public static float EdgeSearchRadius = 5f;

	public static float MinEdgeLength = 0.1f;

	private static Octree<EdgeObject> s_edgeOctree;

	public static void RebuildEdgeIndex()
	{
		Profiling.Start("EdgeDetector.RebuildEdgeIndex", float.MaxValue);
		Guidance[] array = SceneTools.FindObjectsOfType<Guidance>();
		Vector3 vector = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
		Vector3 vector2 = new Vector3(float.MinValue, float.MinValue, float.MinValue);
		Guidance[] array2 = array;
		foreach (Guidance guidance in array2)
		{
			foreach (Guidance.Edge edge in guidance.EdgeList)
			{
				Vector3 rhs = guidance.transform.TransformPoint(edge.start);
				Vector3 rhs2 = guidance.transform.TransformPoint(edge.end);
				vector = Vector3.Min(vector, rhs);
				vector = Vector3.Min(vector, rhs2);
				vector2 = Vector3.Max(vector2, rhs);
				vector2 = Vector3.Max(vector2, rhs2);
			}
		}
		int num = 0;
		s_edgeOctree = new Octree<EdgeObject>((vector2 - vector).magnitude, 1.5f, 5, (vector2 + vector) / 2f);
		array2 = array;
		foreach (Guidance guidance2 in array2)
		{
			foreach (Guidance.Edge edge2 in guidance2.EdgeList)
			{
				EdgeObject edgeObject = CreateEdgeObject(guidance2, edge2);
				if (IsEdgeValid(edgeObject))
				{
					Bounds boundingBox = default;
					boundingBox.SetMinMax(Vector3.Min(edgeObject.Start, edgeObject.End), Vector3.Max(edgeObject.Start, edgeObject.End));
					s_edgeOctree.Add(edgeObject, boundingBox);
					num++;
				}
			}
		}
		s_edgeOctree.Fixate();
		Profiling.Stop();
	}

	public static List<EdgeObject> GetEdges()
	{
		if (s_edgeOctree == null)
		{
			RebuildEdgeIndex();
		}
		BBList<EdgeObject> bBList = new BBList<EdgeObject>();
		s_edgeOctree.GetObjects(bBList);
		return bBList;
	}

	public static List<EdgeObject> GetEdges(Vector3 _pos, float _radius)
	{
		BBList<EdgeObject> bBList = new BBList<EdgeObject>();
		if (s_edgeOctree == null)
		{
			RebuildEdgeIndex();
		}
		s_edgeOctree.GetObjects(bBList, _pos, _radius);
		return bBList;
	}

	public static List<EdgeObject> GetEdges(Bounds _bounds)
	{
		BoundsCast(GetEdges(_bounds.center, _bounds.size.magnitude), _bounds, out var _hit);
		return _hit;
	}

	public static List<EdgeObject> GetEdges(OffMeshLinkMeta _linkMeta)
	{
		OffMeshLink component = _linkMeta.GetComponent<OffMeshLink>();
		Vector3 position = component.startTransform.position;
		Vector3 position2 = component.endTransform.position;
		float num = EdgeSearchRadius * 2f;
		return GetEdges(new Bounds((position + position2) * 0.5f, new Vector3(Mathf.Abs(position.x - position2.x) + num, Mathf.Abs(position.y - position2.y) + num, Mathf.Abs(position.z - position2.z) + num)));
	}

	public static List<EdgeObject> GetEdges(Vector3 _startPosition, Vector3 _endPosition)
	{
		float num = EdgeSearchRadius * 2f;
		return GetEdges(new Bounds((_startPosition + _endPosition) * 0.5f, new Vector3(Mathf.Abs(_startPosition.x - _endPosition.x) + num, Mathf.Abs(_startPosition.y - _endPosition.y) + num, Mathf.Abs(_startPosition.z - _endPosition.z) + num)));
	}

	public static void DrawDebug(Vector3 _pos, float _radius)
	{
		Gizmos.color = Color.white;
		if (s_edgeOctree != null)
		{
			s_edgeOctree.DrawDebug(_pos, _radius);
		}
	}

	public static void DeleteGuidance()
	{
		Guidance[] array = SceneTools.FindObjectsOfType<Guidance>();
		for (int i = 0; i < array.Length; i++)
		{
			UnityEngine.Object.DestroyImmediate(array[i]);
		}
	}

	public static EdgeObject CreateEdgeObject(Guidance _guide, Guidance.Edge _e)
	{
		EdgeObject edgeObject = new EdgeObject(_guide.gameObject, _guide.transform.TransformPoint(_e.start), _guide.transform.TransformPoint(_e.end));
		Vector3 vector = _guide.transform.TransformDirection(_e.startNormal);
		Vector3 vector2 = _guide.transform.TransformDirection(_e.endNormal);
		if (vector.y < vector2.y)
		{
			vector.y = 0f;
			vector.Normalize();
			edgeObject.Normals[0] = vector;
			edgeObject.Normals[1] = vector2;
		}
		else
		{
			vector2.y = 0f;
			vector2.Normalize();
			edgeObject.Normals[0] = vector2;
			edgeObject.Normals[1] = vector;
		}
		return edgeObject;
	}

	public static bool IsEdgeValid(EdgeObject _edge)
	{
		return (_edge.End - _edge.Start).sqrMagnitude > MinEdgeLength * MinEdgeLength;
	}

	public static bool BoundsCast(List<EdgeObject> _edges, Bounds _bounds, out List<EdgeObject> _hit)
	{
		Bounds _b = new Bounds(_bounds.center, _bounds.size);
		_hit = _edges.Where((EdgeObject _edge) =>
		{
			Vector3 vector = _edge.End - _edge.Start;
			if (_b.Contains(_edge.Start) && _b.Contains(_edge.End))
			{
				return true;
			}
			float distance;
			return _b.IntersectRay(new Ray(_edge.Start, vector.normalized), out distance) && Math.Abs(distance) <= vector.magnitude;
		}).ToList();
		return _hit.Count > 0;
	}
}
