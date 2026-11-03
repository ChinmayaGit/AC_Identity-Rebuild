using System;
using UnityEngine;

[Serializable]
public class CrowdHotspotData
{
	public Vector3 Position;

	public float DynamicWeight;

	public float Radius = 5f;

	public float Duration = 5f;

	public bool Flee;

	public float EndTime;

	public float TriggerRadius;

	public float TriggerDuration;

	public int TriggerReactionType = -1;
}
