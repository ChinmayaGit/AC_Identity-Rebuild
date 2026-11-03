using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
	[SerializeField]
	private bool m_rain;

	[SerializeField]
	private bool m_stormy;

	[SerializeField]
	private bool m_lightning;

	private void Start()
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag("Bla bla bla, Mr. Freeman");
		for (int i = 0; i < array.Length; i++)
		{
			Object.Destroy(array[i]);
		}
	}

	public void OnLightningEvent()
	{
	}
}
