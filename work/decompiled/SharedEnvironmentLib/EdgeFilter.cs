using System;
using UnityEngine;

public class EdgeFilter
{
	private class SFlagsFilter
	{
		public bool m_EdgeWallWall;

		public bool m_EdgeWallGround = true;

		public bool m_EdgeWallCeiling;

		public bool m_EdgeWallVoid;

		public bool m_EdgeGroundCeiling = true;

		public bool m_EdgeGroundVoid;

		public bool m_EdgeCeilingVoid;
	}

	private float m_SlopeCosAngle = 0.707f;

	private float m_WallAngle = (float)Math.PI / 8f;

	private float m_MinZDiffAngle = (float)Math.PI / 12f;

	private SFlagsFilter FlagsFilter = new SFlagsFilter();

	private const float DEFAULT_SLOPE_COS_ANGLE = 0.707f;

	private const float DEFAULT_WALL_ANGLE = (float)Math.PI / 8f;

	private const float DEFAULT_MIN_Z_DIFF_ANGLE = (float)Math.PI / 12f;

	private const float popRad45 = (float)Math.PI / 4f;

	public Vector3 UpVector = new Vector3(0f, 0f, 1f);

	private void SetSlopeCosAngle(float value)
	{
		m_SlopeCosAngle = value;
	}

	private float GetSlopeCosAngle()
	{
		return m_SlopeCosAngle;
	}

	private float GetMinZDiffAngle()
	{
		return m_MinZDiffAngle;
	}

	public Vector3 GetBaseK()
	{
		return UpVector;
	}

	private float GetSlopeAngle_Callback()
	{
		return Mathf.Acos(GetSlopeCosAngle());
	}

	private void SetSlopeAngle_Callback(float value)
	{
		SetSlopeCosAngle(Mathf.Cos(value));
	}

	public bool AcceptTriangle(Vector3 normalTriangle)
	{
		float f = Vector3.Dot(normalTriangle, GetBaseK());
		if (Mathf.Abs(f) > m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeWallGround || FlagsFilter.m_EdgeGroundVoid || FlagsFilter.m_EdgeGroundCeiling)
			{
				return true;
			}
		}
		else if (Mathf.Abs(f) < 0f - m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeWallCeiling || FlagsFilter.m_EdgeCeilingVoid || FlagsFilter.m_EdgeGroundCeiling)
			{
				return true;
			}
		}
		else if (FlagsFilter.m_EdgeWallWall || FlagsFilter.m_EdgeWallGround || FlagsFilter.m_EdgeWallCeiling || FlagsFilter.m_EdgeWallVoid)
		{
			return true;
		}
		return false;
	}

	private bool IsZUpEnoughDiff(Vector3 normal0, Vector3 normal1, float cosAngleWithZ0, float cosAngleWithZ1)
	{
		if (Mathf.Acos(Vector3.Dot(normal0, normal1).Clamp(0f, 1f)) < (float)Math.PI / 4f)
		{
			float num = Mathf.Acos(cosAngleWithZ0);
			if (Mathf.Abs(Mathf.Acos(cosAngleWithZ1) - num) < GetMinZDiffAngle())
			{
				return false;
			}
		}
		return true;
	}

	public bool AcceptEdge(Vector3 normal0, Vector3 normal1)
	{
		float val = Vector3.Dot(normal0, GetBaseK());
		float val2 = Vector3.Dot(normal1, GetBaseK());
		val = val.Clamp(-1f, 1f);
		val2 = val2.Clamp(-1f, 1f);
		if (val > m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeWallGround && Mathf.Abs(val2) < m_SlopeCosAngle)
			{
				if (IsZUpEnoughDiff(normal0, normal1, val, val2))
				{
					return true;
				}
				return false;
			}
			if (FlagsFilter.m_EdgeGroundCeiling && val2 < 0f - m_SlopeCosAngle)
			{
				return true;
			}
		}
		else if (val < 0f - m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeWallCeiling && Mathf.Abs(val2) < m_SlopeCosAngle)
			{
				if (IsZUpEnoughDiff(normal0, normal1, val, val2))
				{
					return true;
				}
				return false;
			}
			if (FlagsFilter.m_EdgeGroundCeiling && val2 > m_SlopeCosAngle)
			{
				return true;
			}
		}
		else
		{
			if (FlagsFilter.m_EdgeWallWall && Mathf.Abs(val2) < m_SlopeCosAngle)
			{
				Vector3 vector = Vector3.Cross(normal0, normal1);
				float f;
				if (Vector3.SqrMagnitude(vector) > 1E-05f)
				{
					f = 0f;
				}
				else
				{
					vector.Normalize();
					f = TrianglesNeighbor.GetAngle(normal0, normal1, vector);
				}
				if (Mathf.Abs(f) > m_WallAngle)
				{
					return true;
				}
			}
			if (FlagsFilter.m_EdgeWallGround && val2 > m_SlopeCosAngle)
			{
				if (IsZUpEnoughDiff(normal0, normal1, val, val2))
				{
					return true;
				}
				return false;
			}
			if (FlagsFilter.m_EdgeWallCeiling && val2 < 0f - m_SlopeCosAngle)
			{
				if (IsZUpEnoughDiff(normal0, normal1, val, val2))
				{
					return true;
				}
				return false;
			}
		}
		return false;
	}

	public bool AcceptEdge(Vector3 normal)
	{
		float num = Vector3.Dot(normal, GetBaseK());
		if (num > m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeGroundVoid)
			{
				return true;
			}
		}
		else if (num < 0f - m_SlopeCosAngle)
		{
			if (FlagsFilter.m_EdgeCeilingVoid)
			{
				return true;
			}
		}
		else if (FlagsFilter.m_EdgeWallVoid)
		{
			return true;
		}
		return false;
	}
}
