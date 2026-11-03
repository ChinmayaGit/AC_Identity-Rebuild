using UnityEngine;

public class CrowdHotspot : MonoBehaviour
{
	public delegate void TriggerDelegate(CrowdHotspotData _data);

	public float Period = 10f;

	public float Randomness = 0.3f;

	public CrowdHotspotData HotspotData = new CrowdHotspotData();

	public TriggerDelegate OnTrigger;

	private float m_nextTriggerTime;

	private void Start()
	{
		Reload();
	}

	private void Update()
	{
		if (Time.time > m_nextTriggerTime)
		{
			Trigger();
			Reload();
		}
	}

	private void Reload()
	{
		m_nextTriggerTime = Time.time + Period + (1f + Random.Range(0f - Randomness, Randomness));
	}

	public void Trigger()
	{
		HotspotData.Position = transform.position;
		HotspotData.EndTime = Time.time + HotspotData.Duration;
		if (OnTrigger != null)
		{
			OnTrigger(HotspotData);
		}
		SendMessage("OnTrigger", HotspotData, SendMessageOptions.DontRequireReceiver);
	}

	public void OnDrawGizmos()
	{
		Mathf.Clamp01((m_nextTriggerTime - Time.time) / Period);
	}
}
