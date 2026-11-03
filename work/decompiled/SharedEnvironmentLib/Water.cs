using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class Water : MonoBehaviour
{
	[SerializeField]
	private Mesh m_mesh;

	[SerializeField]
	private Color m_reflectionColor = Color.gray;

	[SerializeField]
	private Color m_specularColor = Color.gray;

	[SerializeField]
	private float m_lightmap = 0.5f;

	[SerializeField]
	private Texture2D m_flowMap;

	[SerializeField]
	private Vector2 m_flowSpeed = new Vector2(1f, 1f);

	[SerializeField]
	private float m_flowCycleLength = 5f;

	[SerializeField]
	private Vector3 m_rotatingLightDirection = new Vector3(10f, 50f, -8f);

	[SerializeField]
	private Vector3 m_staticLightDirection = new Vector3(10f, 50f, -8f);

	[SerializeField]
	private bool m_alwaysUseStaticLightDirection;

	[SerializeField]
	private float m_lightDirFadeStartHeight = 5f;

	[SerializeField]
	private float m_lightDirFadeEndHeight = 2f;

	private List<Material> m_originalSharedMaterials = new List<Material>();

	private Renderer m_renderer;

	private int m_shadowId;

	private int m_reflId;

	private int m_specId;

	private int m_fakeLightDirId;

	private int m_flowPhasesLerpId;

	private int m_flowSpeedId;

	private Vector4 m_shadowInv = new Vector4(0.5f, 0.5f, 0f, 0f);

	private Vector4 m_fakeLightDir;

	private Vector4 m_flowPhasesLerp;

	private void Start()
	{
		RefreshRenderer();
		RefreshIds();
		RefreshInvariantShaderConstants();
	}

	private void OnWillRenderObject()
	{
		if (!Application.isPlaying)
		{
			RefreshRenderer();
			RefreshIds();
			RefreshInvariantShaderConstants();
			RefreshShaderConstants();
		}
	}

	private void Update()
	{
		if (Application.isPlaying)
		{
			RefreshShaderConstants();
		}
	}

	private void RefreshRenderer()
	{
		m_renderer = GetComponent<Renderer>();
		m_originalSharedMaterials.Clear();
		for (int i = 0; i < m_renderer.sharedMaterials.Length; i++)
		{
			m_originalSharedMaterials.Add(m_renderer.sharedMaterials[i]);
		}
	}

	private void RefreshIds()
	{
		m_shadowId = Shader.PropertyToID("_Shadow");
		m_reflId = Shader.PropertyToID("_ReflColor");
		m_specId = Shader.PropertyToID("_SpecularColor");
		m_fakeLightDirId = Shader.PropertyToID("_fakeLightDir");
		m_flowPhasesLerpId = Shader.PropertyToID("_flowPhasesLerp");
		m_flowSpeedId = Shader.PropertyToID("_flowSpeed");
	}

	public void BakeFlowMap()
	{
		if (m_mesh.colors.Length != 0)
		{
			MeshFilter component = GetComponent<MeshFilter>();
			Mesh mesh = Object.Instantiate(m_mesh);
			Bounds bounds = mesh.bounds;
			Color[] array = new Color[mesh.vertexCount];
			Vector3[] vertices = mesh.vertices;
			Vector2 vector = default;
			Color color = new Color(1f, 1f, 0f, 0f);
			for (int i = 0; i < mesh.vertexCount; i++)
			{
				vector.x = (vertices[i].x - bounds.min.x) / (bounds.max.x - bounds.min.x);
				vector.y = 1f - (vertices[i].y - bounds.min.y) / (bounds.max.y - bounds.min.y);
				Color color2 = (m_flowMap ? m_flowMap.GetPixelBilinear(vector.x, vector.y) : color);
				array[i] = mesh.colors[i];
				array[i].r = 1f - color2.r;
				array[i].b = 1f - color2.g;
			}
			mesh.colors = array;
			component.sharedMesh = mesh;
		}
	}

	private void RefreshInvariantShaderConstants()
	{
		m_shadowInv.x = m_lightmap;
		m_shadowInv.y = 1f - m_lightmap;
		Material[] sharedMaterials = m_renderer.sharedMaterials;
		foreach (Material material in sharedMaterials)
		{
			material.SetVector(m_shadowId, m_shadowInv);
			material.SetColor(m_reflId, m_reflectionColor * 2f);
			material.SetColor(m_specId, m_specularColor * 2f);
			material.SetVector(m_flowSpeedId, m_flowSpeed);
			if (m_alwaysUseStaticLightDirection)
			{
				material.SetVector(m_fakeLightDirId, Vector3.Normalize(m_staticLightDirection));
			}
		}
	}

	private void EnableMetalFeatures(bool _enabled)
	{
		if (!_enabled)
		{
			m_renderer.sharedMaterials = m_originalSharedMaterials.ToArray();
		}
		else
		{
			Material[] array = new Material[m_renderer.sharedMaterials.Length];
			for (int i = 0; i < array.Length; i++)
			{
				string text = m_renderer.sharedMaterials[i].name;
				array[i] = Resources.Load<Material>("Materials/Water/" + text + "_Metal");
			}
			m_renderer.sharedMaterials = array;
		}
		RefreshIds();
		RefreshInvariantShaderConstants();
	}

	private void RefreshShaderConstants()
	{
		float num = 0f;
		Camera camera = Camera.current;
		if (!Application.isPlaying)
		{
			num = Time.realtimeSinceStartup * Time.timeScale;
		}
		else
		{
			camera = Camera.main;
			num = Time.timeSinceLevelLoad;
		}
		float num2 = 0.5f * m_flowCycleLength;
		float num3 = 1f / num2;
		m_flowPhasesLerp.x = num % m_flowCycleLength;
		m_flowPhasesLerp.y = (num + num2) % m_flowCycleLength;
		m_flowPhasesLerp.z = Mathf.Abs(num2 - m_flowPhasesLerp.x) * num3;
		if (!m_alwaysUseStaticLightDirection && camera != null)
		{
			float t = Mathf.Clamp01((Mathf.Abs(camera.transform.position.y - transform.position.y) - m_lightDirFadeStartHeight) / (m_lightDirFadeEndHeight - m_lightDirFadeStartHeight));
			Vector3 a = Vector3.Normalize(camera.transform.TransformDirection(m_rotatingLightDirection));
			m_fakeLightDir = Vector3.Normalize(Vector3.Lerp(a, Vector3.Normalize(m_staticLightDirection), t));
		}
		Material[] sharedMaterials = m_renderer.sharedMaterials;
		foreach (Material material in sharedMaterials)
		{
			material.SetVector(m_flowPhasesLerpId, m_flowPhasesLerp);
			if (!m_alwaysUseStaticLightDirection)
			{
				material.SetVector(m_fakeLightDirId, m_fakeLightDir);
			}
		}
	}
}
