using System;
using UnityEngine;

public class CrowdStation : MonoBehaviour
{
	public float Weight = 1f;

	public int MaxCrowdOnStation = 5;

	public int CurrentCrowdOnStation;

	public float MinIdleDuration = 1f;

	public float MaxIdleDuration = 10f;

	public float IdleRadius = 2.5f;

	public float IdleAngle = 360f;

	public float AttractRadius = 10f;

	public float AttractAngle = 360f;

	public float AcceptChance = 0.7f;

	public Vector3 CachedPosition = Vector3.zero;

	public Quaternion CachedRotation = Quaternion.identity;

	private void Start()
	{
		CurrentCrowdOnStation = 0;
		CachedPosition = transform.position;
		CachedRotation = transform.rotation;
	}

	private void Update()
	{
		if (transform.hasChanged)
		{
			CachedPosition = transform.position;
			CachedRotation = transform.rotation;
			transform.hasChanged = false;
		}
	}

	public void OnDrawGizmos()
	{
		Gizmos.color = Color.blue;
		DrawAngle(transform.position, transform.forward, AttractRadius, AttractAngle);
		Gizmos.color = Color.cyan;
		DrawAngle(transform.position, transform.forward, IdleRadius, IdleAngle);
	}

	private static void DrawAngle(Vector3 _position, Vector3 _direction, float _radius, float _angle)
	{
		_angle = Mathf.Min(_angle, 360f);
		Quaternion quaternion = Quaternion.LookRotation(_direction);
		int num = 8;
		Vector3 vector = Vector3.zero;
		Vector3 zero = Vector3.zero;
		for (int i = -num; i <= num; i++)
		{
			float num2 = (float)i * _angle * 0.5f / 360f / (float)num;
			float x = Mathf.Sin(num2 * (float)Math.PI * 2f) * _radius;
			float z = Mathf.Cos(num2 * (float)Math.PI * 2f) * _radius;
			zero = _position + quaternion * new Vector3(x, 0f, z);
			if (i > -num)
			{
				Gizmos.DrawLine(vector, zero);
			}
			vector = zero;
		}
		if (_angle < 360f)
		{
			float num3 = (0f - _angle) * 0.5f / 360f;
			float x2 = Mathf.Sin(num3 * (float)Math.PI * 2f) * _radius;
			float z2 = Mathf.Cos(num3 * (float)Math.PI * 2f) * _radius;
			Gizmos.DrawLine(_position, _position + quaternion * new Vector3(x2, 0f, z2));
			float num4 = _angle * 0.5f / 360f;
			float x3 = Mathf.Sin(num4 * (float)Math.PI * 2f) * _radius;
			float z3 = Mathf.Cos(num4 * (float)Math.PI * 2f) * _radius;
			Gizmos.DrawLine(_position, _position + quaternion * new Vector3(x3, 0f, z3));
		}
	}
}
