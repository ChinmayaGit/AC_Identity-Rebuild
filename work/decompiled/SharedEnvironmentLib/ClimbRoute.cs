using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClimbRoute
{
	[SerializeField]
	private int m_CurrentIndex;

	public List<ClimbRouteEntry> m_ClimbRouteEntries;

	public ClimbRouteEntry CurrentEntry
	{
		get
		{
			if (m_CurrentIndex < 0 || m_CurrentIndex >= m_ClimbRouteEntries.Count)
			{
				return null;
			}
			return m_ClimbRouteEntries[m_CurrentIndex];
		}
	}

	public ClimbRouteEntry NextEntry
	{
		get
		{
			if (m_CurrentIndex < -1 || m_CurrentIndex >= m_ClimbRouteEntries.Count - 1)
			{
				return null;
			}
			return m_ClimbRouteEntries[m_CurrentIndex + 1];
		}
	}

	public int CurrentIndex
	{
		get
		{
			return m_CurrentIndex;
		}
		private set
		{
			m_CurrentIndex = value;
		}
	}

	public List<ClimbRouteEntry> ClimbRouteEntries
	{
		get
		{
			return m_ClimbRouteEntries;
		}
		private set
		{
			m_ClimbRouteEntries = value;
		}
	}

	public bool IsFirstEntry => m_CurrentIndex == 0;

	public bool IsLastEntry
	{
		get
		{
			if (m_ClimbRouteEntries.Count > 0)
			{
				return m_CurrentIndex == m_ClimbRouteEntries.Count - 1;
			}
			return false;
		}
	}

	public bool AdvanceToNextEntry()
	{
		bool result = false;
		if (m_CurrentIndex < m_ClimbRouteEntries.Count - 1)
		{
			m_CurrentIndex++;
			result = true;
		}
		return result;
	}

	public ClimbRoute(List<ClimbRouteEntry> _climbRouteEntries)
	{
		CurrentIndex = -1;
		ClimbRouteEntries = _climbRouteEntries ?? new List<ClimbRouteEntry>();
	}

	public ClimbRoute Clone()
	{
		return new ClimbRoute(m_ClimbRouteEntries);
	}
}
