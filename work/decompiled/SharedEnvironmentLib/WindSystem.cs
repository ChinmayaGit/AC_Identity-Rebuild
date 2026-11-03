using System;
using System.Collections;
using UnityEngine;

[ExecuteInEditMode]
public class WindSystem : MonoBehaviour
{
	[SerializeField]
	private float m_intensity = 1f;

	[SerializeField]
	private float m_bendSpeed = 1f;

	[SerializeField]
	private float m_flutterSpeed = 2f;

	[SerializeField]
	private float m_flutterAmplitude = 0.5f;

	[SerializeField]
	private float m_bend = 0.2f;

	[SerializeField]
	private float m_seedVariation = 0.5f;

	[SerializeField]
	private AnimationCurve m_bendAnimation = AnimationCurve.Linear(0f, 0f, 1f, 0f);

	[SerializeField]
	private Vector2 m_directionChangeDelay = new Vector2(10f, 40f);

	[SerializeField]
	private float m_directionChangeMaxAngle = 180f;

	[SerializeField]
	private float m_windDirectionChangeTime = 2f;

	[SerializeField]
	private float m_windFluctuation;

	private float m_windFluctuationProgress;

	[SerializeField]
	private float m_windFluctuationStrength = 2f;

	[SerializeField]
	private float m_windFluctuatuinChangeSpeed = 0.1f;

	[SerializeField]
	private bool m_raining;

	[SerializeField]
	private GameObject m_rainEffect;

	[SerializeField]
	private bool m_showAsh;

	[SerializeField]
	private GameObject m_ashEffect;

	private GameObject m_rain;

	private GameObject m_ash;

	private static WindSystem m_instance;

	private Color m_color;

	private Color m_params;

	private float m_time;

	private float m_TimeLoop;

	public float seedVariation => m_seedVariation;

	public static WindSystem Instance
	{
		get
		{
			if (m_instance == null)
			{
				m_instance = UnityEngine.Object.FindObjectOfType(typeof(WindSystem)) as WindSystem;
				if (m_instance == null)
				{
					m_instance = new GameObject("Wind System (Fallback)").AddComponent<WindSystem>();
				}
			}
			return m_instance;
		}
	}

	private void Start()
	{
		StartCoroutine("changeWindDirectionDelay");
		m_TimeLoop = 1f;
		if (m_bendAnimation != null)
		{
			m_TimeLoop = m_bendAnimation[m_bendAnimation.length - 1].time;
		}
	}

	private void Update()
	{
		if (Application.isPlaying)
		{
			if (m_raining && Camera.main != null)
			{
				if (m_rain == null)
				{
					m_rain = UnityEngine.Object.Instantiate(m_rainEffect, Vector3.zero, Quaternion.identity) as GameObject;
				}
				m_rain.transform.position = Camera.main.transform.position + Camera.main.transform.rotation * new Vector3(0f, 10f, 10f);
				m_rain.transform.rotation = Camera.main.transform.rotation;
			}
			else if (m_rain != null)
			{
				UnityEngine.Object.Destroy(m_rain);
			}
			if (m_showAsh && Camera.main != null)
			{
				if (m_ash == null)
				{
					m_ash = UnityEngine.Object.Instantiate(m_ashEffect, Vector3.zero, Quaternion.identity) as GameObject;
				}
				m_ash.transform.position = Camera.main.transform.position + Camera.main.transform.rotation * new Vector3(0f, 10f, 10f);
				m_ash.transform.rotation = Camera.main.transform.rotation;
			}
			else if (m_ash != null)
			{
				UnityEngine.Object.Destroy(m_ash);
			}
		}
		m_color.r = transform.forward.x;
		m_color.g = transform.forward.y;
		m_color.b = transform.forward.z;
		m_color.a = m_intensity;
		Shader.SetGlobalColor("_WindDir", m_color);
		m_time = Time.timeSinceLevelLoad;
		m_params.r = Mathf.PingPong(m_time, 60f * m_flutterSpeed);
		m_params.g = m_time * m_bendSpeed + m_bendAnimation.Evaluate(Mathf.Repeat(m_time * m_bendSpeed, m_TimeLoop));
		m_params.b = m_bend;
		m_params.a = m_flutterAmplitude;
		Shader.SetGlobalColor("_WindParams", m_params);
		m_windFluctuation = Mathf.Sin(m_windFluctuationProgress) * m_windFluctuationStrength;
		m_windFluctuationProgress += m_windFluctuatuinChangeSpeed * Time.deltaTime;
		if (m_windFluctuationProgress > (float)Math.PI * 2f)
		{
			m_windFluctuationProgress -= (float)Math.PI * 2f;
		}
		transform.rotation = Quaternion.Euler(new Vector3(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y + m_windFluctuation, transform.rotation.eulerAngles.z));
	}

	private IEnumerator changeWindDirectionDelay()
	{
		float seconds = UnityEngine.Random.Range(m_directionChangeDelay.x, m_directionChangeDelay.y);
		yield return new WaitForSeconds(seconds);
		StartCoroutine("changeWindDirection", UnityEngine.Random.Range(0f - m_directionChangeMaxAngle, m_directionChangeMaxAngle));
	}

	private IEnumerator changeWindDirection(float newAngle)
	{
		Vector3 newRotation = transform.rotation.eulerAngles;
		newRotation.y = newAngle;
		float sTime = Time.time;
		Quaternion startRotation = transform.rotation;
		while (true)
		{
			float t = (Time.time - sTime) / m_windDirectionChangeTime;
			float num = Mathf.SmoothStep(0f, 1f, t);
			transform.rotation = Quaternion.Lerp(startRotation, Quaternion.Euler(newRotation), num);
			if (num >= 0.95f)
			{
				break;
			}
			yield return null;
		}
		StartCoroutine("changeWindDirectionDelay");
	}
}
