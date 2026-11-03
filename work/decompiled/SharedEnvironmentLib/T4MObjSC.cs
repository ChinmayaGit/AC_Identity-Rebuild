using UnityEngine;

[ExecuteInEditMode]
public class T4MObjSC : MonoBehaviour
{
	[HideInInspector]
	public string ConvertType = "";

	[HideInInspector]
	public bool EnabledLODSystem = true;

	[HideInInspector]
	public Vector3[] ObjPosition;

	[HideInInspector]
	public T4MLodObjSC[] ObjLodScript;

	[HideInInspector]
	public int[] ObjLodStatus;

	[HideInInspector]
	public float MaxViewDistance = 60f;

	[HideInInspector]
	public float LOD2Start = 20f;

	[HideInInspector]
	public float LOD3Start = 40f;

	[HideInInspector]
	public float Interval = 0.5f;

	[HideInInspector]
	public Transform PlayerCamera;

	private Vector3 OldPlayerPos;

	[HideInInspector]
	public int Mode = 1;

	[HideInInspector]
	public int Master;

	[HideInInspector]
	public bool enabledBillboard = true;

	[HideInInspector]
	public Vector3[] BillboardPosition;

	[HideInInspector]
	public float BillInterval = 0.05f;

	[HideInInspector]
	public int[] BillStatus;

	[HideInInspector]
	public float BillMaxViewDistance = 30f;

	[HideInInspector]
	public T4MBillBObjSC[] BillScript;

	[HideInInspector]
	public bool enabledLayerCul = true;

	[HideInInspector]
	public float BackGroundView = 1000f;

	[HideInInspector]
	public float FarView = 200f;

	[HideInInspector]
	public float NormalView = 60f;

	[HideInInspector]
	public float CloseView = 30f;

	private float[] distances = new float[32];

	[HideInInspector]
	public int Axis;

	[HideInInspector]
	public bool LODbasedOnScript = true;

	[HideInInspector]
	public bool BilBbasedOnScript = true;

	public Material T4MMaterial;

	public MeshFilter T4MMesh;

	[HideInInspector]
	public Color TranslucencyColor = new Color(0.73f, 0.85f, 0.4f, 1f);

	[HideInInspector]
	public Vector4 Wind = new Vector4(0.85f, 0.075f, 0.4f, 0.5f);

	[HideInInspector]
	public float WindFrequency = 0.75f;

	[HideInInspector]
	public float GrassWindFrequency = 1.5f;

	[HideInInspector]
	public bool ActiveWind;

	public bool LayerCullPreview;

	public bool LODPreview;

	public bool BillboardPreview;

	public Texture2D T4MMaskTex2d;

	public Texture2D T4MMaskTexd;

	public void Awake()
	{
		if (Master != 1)
		{
			return;
		}
		if (PlayerCamera == null && (bool)Camera.main)
		{
			PlayerCamera = Camera.main.transform;
		}
		else if (PlayerCamera == null && !Camera.main)
		{
			Camera[] array = Object.FindObjectsOfType(typeof(Camera)) as Camera[];
			for (int i = 0; i < array.Length; i++)
			{
				if ((bool)array[i].GetComponent<AudioListener>())
				{
					PlayerCamera = array[i].transform;
				}
			}
		}
		if (enabledLayerCul)
		{
			distances[26] = CloseView;
			distances[27] = NormalView;
			distances[28] = FarView;
			distances[29] = BackGroundView;
			PlayerCamera.GetComponent<Camera>().layerCullDistances = distances;
		}
		if (EnabledLODSystem && ObjPosition.Length != 0 && Mode == 1)
		{
			if (ObjLodScript[0].gameObject != null)
			{
				if (LODbasedOnScript)
				{
					InvokeRepeating("LODScript", Random.Range(0f, Interval), Interval);
				}
				else
				{
					InvokeRepeating("LODLay", Random.Range(0f, Interval), Interval);
				}
			}
		}
		else if (EnabledLODSystem && ObjPosition.Length != 0 && Mode == 2 && ObjLodScript[0] != null)
		{
			for (int j = 0; j < ObjPosition.Length; j++)
			{
				if (ObjLodScript[j] != null)
				{
					if (LODbasedOnScript)
					{
						ObjLodScript[j].ActivateLODScrpt();
					}
					else
					{
						ObjLodScript[j].ActivateLODLay();
					}
				}
			}
		}
		if (enabledBillboard && BillboardPosition.Length != 0 && BillScript[0] != null)
		{
			if (BilBbasedOnScript)
			{
				InvokeRepeating("BillScrpt", Random.Range(0f, BillInterval), BillInterval);
			}
			else
			{
				InvokeRepeating("BillLay", Random.Range(0f, BillInterval), BillInterval);
			}
		}
	}

	private void OnGUI()
	{
		if (!Application.isPlaying && Master == 1)
		{
			if (LayerCullPreview && enabledLayerCul)
			{
				GUI.color = Color.green;
				GUI.Label(new Rect(0f, 0f, 200f, 200f), "LayerCull Preview ON");
			}
			else
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(0f, 0f, 200f, 200f), "LayerCull Preview OFF");
			}
			if (LODPreview && ObjPosition.Length != 0)
			{
				GUI.color = Color.green;
				GUI.Label(new Rect(0f, 20f, 200f, 200f), "LOD Preview ON");
			}
			else if (LODPreview && ObjPosition.Length == 0)
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(0f, 20f, 200f, 200f), "Activate the LOD First");
			}
			else
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(0f, 20f, 200f, 200f), "LOD Preview OFF");
			}
			if (BillboardPreview && BillboardPosition.Length != 0)
			{
				GUI.color = Color.green;
				GUI.Label(new Rect(0f, 40f, 200f, 200f), "Billboard Preview ON");
			}
			else if (BillboardPreview && BillboardPosition.Length == 0)
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(0f, 40f, 200f, 200f), "Activate the Billboard First");
			}
			else
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(0f, 40f, 200f, 200f), "Billboard Preview OFF");
			}
		}
	}

	private void LateUpdate()
	{
		if (ActiveWind)
		{
			Color color = Wind * Mathf.Sin(Time.realtimeSinceStartup * WindFrequency);
			color.a = Wind.w;
			Color color2 = Wind * Mathf.Sin(Time.realtimeSinceStartup * GrassWindFrequency);
			color2.a = Wind.w;
			Shader.SetGlobalColor("_Wind", color);
			Shader.SetGlobalColor("_GrassWind", color2);
			Shader.SetGlobalColor("_TranslucencyColor", TranslucencyColor);
			Shader.SetGlobalFloat("_TranslucencyViewDependency;", 0.65f);
		}
		if (!PlayerCamera || Application.isPlaying || Master != 1)
		{
			return;
		}
		Camera component = PlayerCamera.GetComponent<Camera>();
		if (LayerCullPreview && enabledLayerCul)
		{
			distances[26] = CloseView;
			distances[27] = NormalView;
			distances[28] = FarView;
			distances[29] = BackGroundView;
			component.layerCullDistances = distances;
		}
		else
		{
			distances[26] = component.farClipPlane;
			distances[27] = component.farClipPlane;
			distances[28] = component.farClipPlane;
			distances[29] = component.farClipPlane;
			component.layerCullDistances = distances;
		}
		if (LODPreview)
		{
			if (EnabledLODSystem && ObjPosition.Length != 0 && Mode == 1)
			{
				if (ObjLodScript[0].gameObject != null)
				{
					if (LODbasedOnScript)
					{
						LODScript();
					}
					else
					{
						LODLay();
					}
				}
			}
			else if (EnabledLODSystem && ObjPosition.Length != 0 && Mode == 2 && ObjLodScript[0] != null)
			{
				for (int i = 0; i < ObjPosition.Length; i++)
				{
					if (ObjLodScript[i] != null)
					{
						if (LODbasedOnScript)
						{
							ObjLodScript[i].AFLODScrpt();
						}
						else
						{
							ObjLodScript[i].AFLODLay();
						}
					}
				}
			}
		}
		if (BillboardPreview && enabledBillboard && BillboardPosition.Length != 0 && BillScript[0] != null)
		{
			if (BilBbasedOnScript)
			{
				BillScrpt();
			}
			else
			{
				BillLay();
			}
		}
	}

	private void BillScrpt()
	{
		for (int i = 0; i < BillboardPosition.Length; i++)
		{
			if (Vector3.Distance(BillboardPosition[i], PlayerCamera.position) <= BillMaxViewDistance)
			{
				if (BillStatus[i] != 1)
				{
					BillScript[i].Render.enabled = true;
					BillStatus[i] = 1;
				}
				if (Axis == 0)
				{
					BillScript[i].Transf.LookAt(new Vector3(PlayerCamera.position.x, BillScript[i].Transf.position.y, PlayerCamera.position.z), Vector3.up);
				}
				else
				{
					BillScript[i].Transf.LookAt(PlayerCamera.position, Vector3.up);
				}
			}
			else if (BillStatus[i] != 0 && !BillScript[i].Render.enabled)
			{
				BillScript[i].Render.enabled = false;
				BillStatus[i] = 0;
			}
		}
	}

	private void BillLay()
	{
		for (int i = 0; i < BillboardPosition.Length; i++)
		{
			int layer = BillScript[i].gameObject.layer;
			if (Vector3.Distance(BillboardPosition[i], PlayerCamera.position) <= distances[layer])
			{
				if (Axis == 0)
				{
					BillScript[i].Transf.LookAt(new Vector3(PlayerCamera.position.x, BillScript[i].Transf.position.y, PlayerCamera.position.z), Vector3.up);
				}
				else
				{
					BillScript[i].Transf.LookAt(PlayerCamera.position, Vector3.up);
				}
			}
		}
	}

	private void LODScript()
	{
		if (OldPlayerPos == PlayerCamera.position)
		{
			return;
		}
		OldPlayerPos = PlayerCamera.position;
		for (int i = 0; i < ObjPosition.Length; i++)
		{
			float num = Vector3.Distance(new Vector3(ObjPosition[i].x, PlayerCamera.position.y, ObjPosition[i].z), PlayerCamera.position);
			if (num <= MaxViewDistance)
			{
				if (num < LOD2Start && ObjLodStatus[i] != 1)
				{
					Renderer lOD = ObjLodScript[i].LOD2;
					bool flag = (ObjLodScript[i].LOD3.enabled = false);
					lOD.enabled = flag;
					ObjLodScript[i].LOD1.enabled = true;
					ObjLodStatus[i] = 1;
				}
				else if (num >= LOD2Start && num < LOD3Start && ObjLodStatus[i] != 2)
				{
					Renderer lOD2 = ObjLodScript[i].LOD1;
					bool flag = (ObjLodScript[i].LOD3.enabled = false);
					lOD2.enabled = flag;
					ObjLodScript[i].LOD2.enabled = true;
					ObjLodStatus[i] = 2;
				}
				else if (num >= LOD3Start && ObjLodStatus[i] != 3)
				{
					Renderer lOD3 = ObjLodScript[i].LOD2;
					bool flag = (ObjLodScript[i].LOD1.enabled = false);
					lOD3.enabled = flag;
					ObjLodScript[i].LOD3.enabled = true;
					ObjLodStatus[i] = 3;
				}
			}
			else if (ObjLodStatus[i] != 0)
			{
				Renderer lOD4 = ObjLodScript[i].LOD1;
				Renderer lOD5 = ObjLodScript[i].LOD2;
				bool flag5 = (ObjLodScript[i].LOD3.enabled = false);
				bool flag = (lOD5.enabled = flag5);
				lOD4.enabled = flag;
				ObjLodStatus[i] = 0;
			}
		}
	}

	private void LODLay()
	{
		if (OldPlayerPos == PlayerCamera.position)
		{
			return;
		}
		OldPlayerPos = PlayerCamera.position;
		for (int i = 0; i < ObjPosition.Length; i++)
		{
			float num = Vector3.Distance(new Vector3(ObjPosition[i].x, PlayerCamera.position.y, ObjPosition[i].z), PlayerCamera.position);
			int layer = ObjLodScript[i].gameObject.layer;
			if (num <= distances[layer] + 5f)
			{
				if (num < LOD2Start && ObjLodStatus[i] != 1)
				{
					Renderer lOD = ObjLodScript[i].LOD2;
					bool flag = (ObjLodScript[i].LOD3.enabled = false);
					lOD.enabled = flag;
					ObjLodScript[i].LOD1.enabled = true;
					ObjLodStatus[i] = 1;
				}
				else if (num >= LOD2Start && num < LOD3Start && ObjLodStatus[i] != 2)
				{
					Renderer lOD2 = ObjLodScript[i].LOD1;
					bool flag = (ObjLodScript[i].LOD3.enabled = false);
					lOD2.enabled = flag;
					ObjLodScript[i].LOD2.enabled = true;
					ObjLodStatus[i] = 2;
				}
				else if (num >= LOD3Start && ObjLodStatus[i] != 3)
				{
					Renderer lOD3 = ObjLodScript[i].LOD2;
					bool flag = (ObjLodScript[i].LOD1.enabled = false);
					lOD3.enabled = flag;
					ObjLodScript[i].LOD3.enabled = true;
					ObjLodStatus[i] = 3;
				}
			}
		}
	}
}
