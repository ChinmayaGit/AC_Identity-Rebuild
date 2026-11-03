using System;
using UbiCore;
using UnityEngine;

[Serializable]
public class EdgeObject
{
	public const float FeetShiftOffset = 0.5f;

	public GameObject Gameobject;

	public Vector3 Start;

	public Vector3 End;

	public Vector3[] Normals = new Vector3[2];

	public Vector3 MidPoint => Start + (End - Start) / 2f;

	public float Length => (End - Start).magnitude;

	public EdgeObject(GameObject _gameobject, Vector3 _start, Vector3 _end)
	{
		Gameobject = _gameobject;
		Start = _start;
		End = _end;
	}

	public EdgeObject(EdgeObject _other)
	{
		Gameobject = _other.Gameobject;
		Start = _other.Start;
		End = _other.End;
		Normals = (Vector3[])_other.Normals.Clone();
	}

	public float Distance(Vector3 _p)
	{
		return Vector3.Distance(ClosestPoint(_p), _p);
	}

	public Vector3 ClosestPoint(Vector3 _p)
	{
		float num = Vector3.Distance(Start, End);
		if (MathHelper.Approximately(num, 0f))
		{
			return Start;
		}
		float num2 = Vector3.Dot(_p - Start, End - Start) / (num * num);
		if (num2 < 0f)
		{
			return Start;
		}
		if (num2 > 1f)
		{
			return End;
		}
		return Start + num2 * (End - Start);
	}

	public Vector3 ClosestPointCharacterFoot(Vector3 _footPosition, Vector3 _bodyPosition)
	{
		Vector3 vector = End - Start;
		float magnitude = vector.magnitude;
		if (MathHelper.Approximately(magnitude, 0f))
		{
			return Start;
		}
		float value = Vector3.Dot(_footPosition - Start, vector) / (magnitude * magnitude);
		float num = Mathf.Min(0.5f / magnitude, 1f);
		value = ((!(Vector3.Dot(_footPosition - _bodyPosition, vector) > 0f)) ? Mathf.Clamp(value, 0f, 1f - num) : Mathf.Clamp(value, num, 1f));
		return Start + value * vector;
	}

	public void Draw(Color _color, Vector3 _size)
	{
		Color color = Gizmos.color;
		Gizmos.color = _color;
		Gizmos.DrawLine(Start, End);
		Gizmos.DrawCube(Start, _size);
		Gizmos.DrawCube(End, _size);
		Gizmos.color = Color.blue;
		Gizmos.DrawCube(MidPoint, _size);
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(MidPoint, MidPoint + Normals[0] * 0.2f);
		Gizmos.DrawLine(MidPoint, MidPoint + Normals[1] * 0.2f);
		Gizmos.color = color;
	}
}
