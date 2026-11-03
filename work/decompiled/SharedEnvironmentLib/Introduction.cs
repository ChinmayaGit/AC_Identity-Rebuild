using UnityEngine;

public class Introduction : MonoBehaviour
{
	public enum EUIAlignment
	{
		TopLeft,
		TopMiddle,
		TopRight,
		MiddleLeft,
		MiddleMiddle,
		MiddleRight,
		BottomLeft,
		BottomMiddle,
		BottomRight
	}

	[SerializeField]
	private bool m_UseAnimationForFog;

	[SerializeField]
	private AnimationCurve m_fogDensityAnim;

	[SerializeField]
	private AnimationCurve m_fogStartAnim;

	[SerializeField]
	private AnimationCurve m_fogEndAnim;

	[SerializeField]
	private Gradient m_fogColorGradient;

	[SerializeField]
	private Color m_srcFogColor = Color.white;

	[SerializeField]
	private float m_duration = 1f;

	[SerializeField]
	private float m_sceneExp = 0.025f;

	[SerializeField]
	private float m_skyExp = 0.1f;

	[SerializeField]
	private float m_zoneWidth = 20f;

	[SerializeField]
	private float m_zoneStart;

	[SerializeField]
	private EUIAlignment m_uiAlignment = EUIAlignment.BottomLeft;

	[SerializeField]
	private int m_uiLocaStringID = -1;

	[SerializeField]
	private int m_uiFontSize = 48;

	[SerializeField]
	private Vector2 m_uiFontOffset = Vector2.zero;

	[SerializeField]
	private Color m_uiFontColor = Color.white;

	private Color m_destFogColor;

	private float m_destFogStartDistance;

	private float m_destFogEndDistance;

	private float m_destFogDensity;

	private Renderer m_skyRenderer;

	private Material m_skyMaterial;

	private Material m_skyMaterialClone;

	private float m_time;

	private int m_fogId;

	private float AnimationStartTime;

	private bool Started;

	public EUIAlignment UIAlignment => m_uiAlignment;

	public int UILocaStringID => m_uiLocaStringID;

	public int UIFontSize => m_uiFontSize;

	public Vector2 UIFontOffset => m_uiFontOffset;

	public Color UIFontColor => m_uiFontColor;

	public void Abort()
	{
		Debug.Log("Abort!");
		if (enabled)
		{
			FinishFogAnimation();
		}
	}

	private void Awake()
	{
		m_destFogColor = RenderSettings.fogColor;
		m_destFogStartDistance = RenderSettings.fogStartDistance;
		m_destFogEndDistance = RenderSettings.fogEndDistance;
		m_destFogDensity = RenderSettings.fogDensity;
	}

	private void OnStartFogAnimation()
	{
		AnimationStartTime = Time.realtimeSinceStartup;
		Started = true;
		m_time = m_duration;
		enabled = true;
		RenderSettings.fogColor = m_srcFogColor;
		RenderSettings.fogEndDistance = RenderSettings.fogStartDistance;
		RenderingSkyMesh renderingSkyMesh = Object.FindObjectOfType<RenderingSkyMesh>();
		if (renderingSkyMesh != null)
		{
			m_skyRenderer = renderingSkyMesh.GetComponent<Renderer>();
			m_skyMaterial = m_skyRenderer.sharedMaterial;
			m_skyRenderer.sharedMaterial = (m_skyMaterialClone = Object.Instantiate(m_skyMaterial));
			m_skyMaterialClone.shaderKeywords = new string[1] { "FOG_ON" };
		}
		m_fogId = Shader.PropertyToID("_Fog");
	}

	private void Update()
	{
		if (m_time <= 0f && Started)
		{
			Debug.Log("Time is up!");
			FinishFogAnimation();
			return;
		}
		float num = m_time / m_duration;
		float t = 1f - Mathf.Pow(1f - num, m_sceneExp);
		float a = 1f - Mathf.Pow(1f - num, m_skyExp);
		Color color = Color.Lerp(m_destFogColor, m_srcFogColor, t);
		color.a = a;
		float num2 = Mathf.Lerp(m_destFogEndDistance, m_zoneStart, t);
		if (!m_UseAnimationForFog)
		{
			RenderSettings.fogColor = color;
			RenderSettings.fogEndDistance = Mathf.Lerp(m_destFogEndDistance, num2 + m_zoneWidth, t);
			RenderSettings.fogDensity = Mathf.Lerp(m_destFogDensity, 1f, t);
			RenderSettings.fogStartDistance = Mathf.Lerp(m_destFogStartDistance, num2 - m_zoneWidth, t);
		}
		else
		{
			float time = (Time.realtimeSinceStartup - AnimationStartTime) / m_duration;
			float time2 = Time.realtimeSinceStartup - AnimationStartTime;
			RenderSettings.fogColor = m_fogColorGradient.Evaluate(time);
			RenderSettings.fogEndDistance = m_fogEndAnim.Evaluate(time2);
			RenderSettings.fogDensity = m_fogDensityAnim.Evaluate(time2);
			RenderSettings.fogStartDistance = m_fogStartAnim.Evaluate(time2);
		}
		if (m_skyMaterialClone != null)
		{
			m_skyMaterialClone.SetColor(m_fogId, color);
		}
		m_time -= Time.deltaTime / ((Time.timeScale > 0f) ? Time.timeScale : 1f);
	}

	private void FinishFogAnimation()
	{
		Debug.Log(string.Concat(m_destFogColor, " ", m_destFogDensity, " ", m_destFogEndDistance, " ", m_destFogStartDistance));
		RenderSettings.fogColor = m_destFogColor;
		RenderSettings.fogDensity = m_destFogDensity;
		RenderSettings.fogEndDistance = m_destFogEndDistance;
		RenderSettings.fogStartDistance = m_destFogStartDistance;
		if (m_skyRenderer != null)
		{
			m_skyRenderer.sharedMaterial = m_skyMaterial;
		}
		m_time = 0f;
		enabled = false;
	}
}
