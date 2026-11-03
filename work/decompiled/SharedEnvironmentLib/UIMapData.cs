using System;
using UbiCore.Debug;
using UnityEngine;

public class UIMapData : MonoBehaviour
{
	public enum ESoundMood
	{
		Day,
		Night,
		Evening,
		Sunny,
		Storm,
		Mist,
		Overcast,
		NightRain,
		Dusk,
		Siege
	}

	public ESoundMood m_mood;

	public Vector2 m_startPos = new Vector2(-124.8f, -198.5f);

	public Vector2 m_endPos = new Vector2(121.6f, 47.4f);

	public Texture2D m_miniMap;

	private string m_GUID;

	public string GUID => m_GUID;

	private void Awake()
	{
		if (string.IsNullOrEmpty(m_GUID))
		{
			m_GUID = Guid.NewGuid().ToString();
		}
	}

	public void OnDrawGizmosSelected()
	{
		if (!(m_miniMap == null))
		{
			Vector2 vector = m_endPos - m_startPos;
			RuntimeGizmos.DrawQuad(new Vector3(m_startPos.x, 0f, m_startPos.y), vector.x, vector.y, m_miniMap, Color.white);
		}
	}
}
