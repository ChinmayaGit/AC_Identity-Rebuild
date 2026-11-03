using UnityEngine;

public class LightningListener : MonoBehaviour
{
	private WeatherSystem m_wheather;

	private void Start()
	{
		m_wheather = Object.FindObjectOfType<WeatherSystem>();
	}

	public void OnLightningEvent()
	{
		m_wheather.OnLightningEvent();
	}
}
