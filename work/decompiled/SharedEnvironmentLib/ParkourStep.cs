using System;
using UnityEngine;

[Serializable]
public class ParkourStep
{
	public enum eParkourStepType
	{
		Jump,
		Balance
	}

	public Vector3 StartPosition;

	public Vector3 StartTangent;

	public Vector3 EndPosition;

	public Vector3 EndTangent;

	public eParkourStepType StepType;
}
