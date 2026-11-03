using UnityEngine;

[ExecuteInEditMode]
public class DirectionalFogColor : MonoBehaviour
{
	public Texture2D m_Ramp;

	public Camera m_Camera;

	public float m_VOffset;

	private void Update()
	{
		if (m_Ramp != null)
		{
			Camera camera = m_Camera;
			if (camera == null)
			{
				camera = Camera.main;
			}
			if (camera != null)
			{
				Transform transform = camera.transform;
				Vector3 vector = new Vector3(0f, 0f, 1f);
				float u = ((transform.rotation * vector).AngleZX() + 180f) / 360f;
				RenderSettings.fogColor = m_Ramp.GetPixelBilinear(u, m_VOffset);
			}
		}
	}
}
