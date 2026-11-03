using System.Collections.Generic;
using AcierProtocol;
using UnityEngine;

[ExecuteInEditMode]
public class PlanarReflection : MonoBehaviour
{
	private class CachedQualitySettings
	{
		public int maxMeshLodLevel;

		public float meshLodBias;
	}

	[SerializeField]
	private LayerMask m_reflectionMask;

	[SerializeField]
	private Color m_clearColor = Color.grey;

	[SerializeField]
	private Color m_geometryReflectionColor = Color.blue;

	[SerializeField]
	public string m_propertyName = "_ReflectionTex";

	[SerializeField]
	private float m_clipPlaneOffset = 0.07f;

	[SerializeField]
	private bool m_reflectGeometryWithConstantColor = true;

	[SerializeField]
	private Shader m_GeometryReflectionShader;

	[SerializeField]
	[HideInInspector]
	private Vector3 m_PlanePosition;

	private const EQualityShaderLOD kMinimumSupportedShaderLOD = EQualityShaderLOD.BumpedSpecular_400;

	private Renderer m_renderer;

	private Camera m_reflectionCamera;

	private List<Camera> m_vProcessedCameras = new List<Camera>();

	private CachedQualitySettings m_cachedQualitySettings = new CachedQualitySettings();

	private int m_renderMaskReflectionGeometry;

	private int m_renderMaskSkybox;

	private int m_renderMaskWater;

	private RenderingSkyMesh m_skyboxMesh;

	private int m_reflectionTexShaderPropID;

	private void Start()
	{
		if (!Application.isEditor)
		{
			m_renderMaskReflectionGeometry = ~LayerMask.GetMask("water", "Skybox", "ReflectionSkybox");
			m_renderMaskSkybox = LayerMask.GetMask("Skybox", "ReflectionSkybox");
			m_renderMaskWater = LayerMask.GetMask("Water");
			m_renderer = GetComponent<Renderer>();
			Shader.SetGlobalColor("acid_geometryReflectionColor", m_geometryReflectionColor);
			m_reflectionTexShaderPropID = Shader.PropertyToID(m_propertyName);
			if (Application.isPlaying && m_PlanePosition.sqrMagnitude < 0.001f)
			{
				m_PlanePosition = transform.position;
			}
		}
	}

	private void OnDisable()
	{
		if (m_reflectionCamera != null)
		{
			Object.DestroyImmediate(m_reflectionCamera.gameObject);
		}
		m_reflectionCamera = null;
		m_skyboxMesh = null;
		m_vProcessedCameras.Clear();
	}

	private void OnWillRenderObject()
	{
		if (Application.isEditor)
		{
			return;
		}
		RenderHelpCameras(Camera.current);
		if ((bool)m_reflectionCamera)
		{
			m_renderer = GetComponent<Renderer>();
			for (int i = 0; i < m_renderer.sharedMaterials.Length; i++)
			{
				m_renderer.sharedMaterials[i].SetTexture(m_reflectionTexShaderPropID, m_reflectionCamera.targetTexture);
			}
		}
	}

	private void EnableMetalFeatures(bool _enabled)
	{
		m_reflectGeometryWithConstantColor = !_enabled;
	}

	private Camera CreateReflectionCameraFor(Camera cam)
	{
		string text = base.gameObject.name + "Reflection" + cam.name;
		GameObject gameObject = GameObject.Find(text);
		if (!gameObject)
		{
			gameObject = new GameObject(text, typeof(Camera));
		}
		if (!gameObject.GetComponent(typeof(Camera)))
		{
			gameObject.AddComponent(typeof(Camera));
		}
		Camera component = gameObject.GetComponent<Camera>();
		component.backgroundColor = m_clearColor;
		component.clearFlags = CameraClearFlags.Color;
		component.cullingMask = (int)m_reflectionMask & ~m_renderMaskWater;
		component.backgroundColor = Color.black;
		component.enabled = false;
		if (!component.targetTexture)
		{
			component.targetTexture = CreateTextureFor(cam);
		}
		return component;
	}

	private RenderTexture CreateTextureFor(Camera cam)
	{
		return new RenderTexture(Mathf.FloorToInt((float)cam.pixelWidth * 0.25f), Mathf.FloorToInt((float)cam.pixelHeight * 0.25f), 8)
		{
			hideFlags = HideFlags.DontSave
		};
	}

	public void RenderHelpCameras(Camera currentCam)
	{
		if (!m_vProcessedCameras.Contains(currentCam))
		{
			m_vProcessedCameras.Add(currentCam);
			if (!m_reflectionCamera)
			{
				m_reflectionCamera = CreateReflectionCameraFor(currentCam);
			}
			RenderReflectionFor(currentCam, m_reflectionCamera);
		}
	}

	public void LateUpdate()
	{
		if (!Application.isEditor)
		{
			m_vProcessedCameras.Clear();
			if (!Application.isPlaying)
			{
				m_PlanePosition = transform.TransformPoint(gameObject.GetComponent<MeshFilter>().sharedMesh.bounds.center);
				Shader.SetGlobalColor("acid_geometryReflectionColor", m_geometryReflectionColor);
				m_reflectionTexShaderPropID = Shader.PropertyToID(m_propertyName);
			}
		}
	}

	private void SaneCameraSettings(Camera helperCam)
	{
		helperCam.depthTextureMode = DepthTextureMode.None;
		helperCam.backgroundColor = Color.black;
		helperCam.clearFlags = CameraClearFlags.Color;
		helperCam.renderingPath = RenderingPath.Forward;
	}

	private void SetQualitySettings()
	{
		m_cachedQualitySettings.maxMeshLodLevel = UnityEngine.QualitySettings.maximumLODLevel;
		m_cachedQualitySettings.meshLodBias = UnityEngine.QualitySettings.lodBias;
		UnityEngine.QualitySettings.maximumLODLevel = 1;
		if (!m_reflectGeometryWithConstantColor)
		{
			Vector4 vec = new Vector4(0f, float.MaxValue, 0f);
			if (RenderSettings.fog)
			{
				vec.x = RenderSettings.fogStartDistance;
				vec.y = RenderSettings.fogEndDistance;
				vec.z = 0f;
			}
			Shader.SetGlobalVector("acid_Fog", vec);
		}
	}

	private void RestoreQualitySettings()
	{
		UnityEngine.QualitySettings.maximumLODLevel = m_cachedQualitySettings.maxMeshLodLevel;
		UnityEngine.QualitySettings.lodBias = m_cachedQualitySettings.meshLodBias;
		if (!m_reflectGeometryWithConstantColor)
		{
			Vector4 vec = new Vector4(0f, float.MaxValue, 0f);
			if (RenderSettings.fog)
			{
				vec.x = RenderSettings.fogStartDistance;
				vec.y = RenderSettings.fogEndDistance;
				vec.z = RenderSettings.fogDensity;
			}
			Shader.SetGlobalVector("acid_Fog", vec);
		}
	}

	private bool IsGeometryReflectionSupported()
	{
		return RenderingSettings.Instance.shaderLOD >= 400;
	}

	private void RenderReflectionFor(Camera cam, Camera reflectCamera)
	{
		if (!reflectCamera)
		{
			return;
		}
		SaneCameraSettings(reflectCamera);
		reflectCamera.backgroundColor = m_clearColor;
		reflectCamera.useOcclusionCulling = false;
		GL.invertCulling = true;
		_ = transform;
		Vector3 eulerAngles = cam.transform.eulerAngles;
		reflectCamera.transform.eulerAngles = new Vector3(0f - eulerAngles.x, eulerAngles.y, eulerAngles.z);
		reflectCamera.transform.position = cam.transform.position;
		Vector3 vector = new Vector3(0f, 1f, 0f);
		float w = Vector3.Dot(vector, m_PlanePosition) - m_clipPlaneOffset;
		Vector4 plane = new Vector4(vector.x, 0f - vector.y, vector.z, w);
		Matrix4x4 zero = Matrix4x4.zero;
		zero = CalculateReflectionMatrix(zero, plane);
		Vector3 position = cam.transform.position;
		Vector3 position2 = zero.MultiplyPoint(position);
		reflectCamera.worldToCameraMatrix = cam.worldToCameraMatrix * zero;
		Vector4 clipPlane = CameraSpacePlane(reflectCamera, m_PlanePosition, vector, 1f);
		Matrix4x4 projectionMatrix = cam.projectionMatrix;
		projectionMatrix = CalculateObliqueMatrix(projectionMatrix, clipPlane);
		reflectCamera.projectionMatrix = projectionMatrix;
		reflectCamera.transform.position = position2;
		Vector3 eulerAngles2 = cam.transform.eulerAngles;
		reflectCamera.transform.eulerAngles = new Vector3(0f - eulerAngles2.x, eulerAngles2.y, eulerAngles2.z);
		if (m_skyboxMesh == null)
		{
			m_skyboxMesh = Rendering.RetrieveSkyMesh() as RenderingSkyMesh;
		}
		if (IsGeometryReflectionSupported())
		{
			SetQualitySettings();
			if (m_reflectGeometryWithConstantColor)
			{
				reflectCamera.SetReplacementShader(m_GeometryReflectionShader, "");
			}
			reflectCamera.clearFlags = CameraClearFlags.Color;
			reflectCamera.cullingMask = (int)m_reflectionMask & m_renderMaskReflectionGeometry;
			reflectCamera.Render();
			RestoreQualitySettings();
			if (m_reflectGeometryWithConstantColor)
			{
				reflectCamera.ResetReplacementShader();
			}
			reflectCamera.clearFlags = CameraClearFlags.Nothing;
		}
		else
		{
			reflectCamera.clearFlags = CameraClearFlags.Skybox;
		}
		if (m_skyboxMesh != null)
		{
			m_skyboxMesh.Align(reflectCamera);
		}
		reflectCamera.cullingMask = (int)m_reflectionMask & m_renderMaskSkybox;
		reflectCamera.Render();
		if (m_skyboxMesh != null && Camera.current != null)
		{
			m_skyboxMesh.Align(Camera.current);
		}
		GL.invertCulling = false;
	}

	private static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane)
	{
		Vector4 b = projection.inverse * new Vector4(sgn(clipPlane.x), sgn(clipPlane.y), 1f, 1f);
		Vector4 vector = clipPlane * (2f / Vector4.Dot(clipPlane, b));
		projection[2] = vector.x - projection[3];
		projection[6] = vector.y - projection[7];
		projection[10] = vector.z - projection[11];
		projection[14] = vector.w - projection[15];
		return projection;
	}

	private static Matrix4x4 CalculateReflectionMatrix(Matrix4x4 reflectionMat, Vector4 plane)
	{
		reflectionMat.m00 = -2f * plane[0] * plane[0] + 1f;
		reflectionMat.m01 = -2f * plane[0] * plane[1];
		reflectionMat.m02 = -2f * plane[0] * plane[2];
		reflectionMat.m03 = -2f * plane[0] * plane[3];
		reflectionMat.m10 = -2f * plane[1] * plane[0];
		reflectionMat.m11 = -2f * plane[1] * plane[1] + 1f;
		reflectionMat.m12 = -2f * plane[1] * plane[2];
		reflectionMat.m13 = -2f * plane[1] * plane[3];
		reflectionMat.m20 = -2f * plane[2] * plane[0];
		reflectionMat.m21 = -2f * plane[2] * plane[1];
		reflectionMat.m22 = -2f * plane[2] * plane[2] + 1f;
		reflectionMat.m23 = -2f * plane[2] * plane[3];
		reflectionMat.m30 = 0f;
		reflectionMat.m31 = 0f;
		reflectionMat.m32 = 0f;
		reflectionMat.m33 = 1f;
		return reflectionMat;
	}

	private static float sgn(float a)
	{
		if (a > 0f)
		{
			return 1f;
		}
		if (a < 0f)
		{
			return -1f;
		}
		return 0f;
	}

	private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
	{
		Vector3 v = pos + normal * m_clipPlaneOffset;
		Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
		Vector3 lhs = worldToCameraMatrix.MultiplyPoint(v);
		Vector3 rhs = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;
		return new Vector4(rhs.x, rhs.y, rhs.z, 0f - Vector3.Dot(lhs, rhs));
	}
}
