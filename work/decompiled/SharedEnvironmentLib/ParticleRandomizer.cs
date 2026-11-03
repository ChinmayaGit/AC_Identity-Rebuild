using System.Collections.Generic;
using UnityEngine;

public class ParticleRandomizer : MonoBehaviour
{
	public List<ParticleSystem> ParticleSystems = new List<ParticleSystem>();

	public List<Vector2> Delays = new List<Vector2>();

	public List<Vector2> Delay = new List<Vector2>();

	public void AddEffect()
	{
		ParticleSystems.Add(null);
		Delays.Add(new Vector2(0f, 1f));
		Delay.Add(new Vector2(0f, 1f));
	}

	public void RemoveEffect()
	{
		if (ParticleSystems.Count > 0)
		{
			ParticleSystems.RemoveAt(ParticleSystems.Count - 1);
			Delays.RemoveAt(Delays.Count - 1);
			Delay.RemoveAt(Delay.Count - 1);
		}
	}

	public void Clear()
	{
		ParticleSystems.Clear();
		Delays.Clear();
		Delay.Clear();
	}

	private void Awake()
	{
		for (int i = 0; i < Delay.Count; i++)
		{
			Vector2 value = new Vector2(0f, Random.Range(Delays[i].x, Delays[i].y));
			Delay[i] = value;
		}
	}

	private void Update()
	{
		for (int i = 0; i < ParticleSystems.Count; i++)
		{
			if (ParticleSystems[i] != null && Delay[i].x + Delay[i].y <= Time.time)
			{
				Vector2 value = new Vector2(Time.time, Delay[i].y);
				Delay[i] = value;
				Vector2 value2 = new Vector2(0f, Random.Range(Delays[i].x, Delays[i].y));
				Delay[i] = value2;
				ParticleSystems[i].Play();
			}
		}
	}
}
