using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class ParticleTurbulence : MonoBehaviour
{
	public enum CalculationMethod
	{
		none,
		sine,
		cosine,
		perlin,
		precalculatedTexture,
		Audio
	}

	public enum TurbulenceType
	{
		relative,
		absolute
	}

	public enum DivisionType
	{
		center,
		edge
	}

	public enum AssignTo
	{
		velocity,
		position
	}

	public enum MeshFollow
	{
		byDistance,
		byTime,
		physical
	}

	public bool drawTurbulenceField;

	public Vector3 fieldSize = new Vector3(5f, 0f, 5f);

	public Vector3 stepSize = new Vector3(0.1f, 0.15f, 0.15f);

	public Color debugColor = new Color(0f, 1f, 0f, 1f);

	public float rayLength = 1f;

	public bool UseTurbulence = true;

	public Texture2D Turbulence;

	private bool removeCurrentParticle;

	public bool useAlphaMask;

	public float threshold = 0.5f;

	public AudioClip audioClip;

	public int amountOfSamples = 512;

	public int atSample = 10000;

	private float[] hayateSamples;

	private Color32[] turbulence;

	public AssignTo AssignTurbulenceTo;

	public TurbulenceType UseRelativeOrAbsoluteValues;

	public CalculationMethod UseCalculationMethodX;

	public CalculationMethod UseCalculationMethodY;

	public CalculationMethod UseCalculationMethodZ;

	public Vector3 Amplitude = Vector3.one;

	public Vector3 Frequency = Vector3.one;

	public Vector3 GlobalForce = Vector3.zero;

	public Vector3 Offset;

	public Vector3 OffsetSpeed;

	public bool lockOffsetToEmitterPosition;

	public bool randomizeOffsetAtStart;

	public Vector2 randomOffsetRange = new Vector2(-1000f, 1000f);

	public bool burstOnCollision;

	public int burstNum;

	public bool isDeltaIndependent;

	public float targetFps = 60f;

	public bool useTransformParticle;

	public bool detachTransformParticleAfterParticleDeath;

	public float detachedObjectDestructionTimeAfter = 1f;

	public bool transformParticleLookTowardsFlightDirection;

	public GameObject transformParticle;

	private List<GameObject> transformParticles = new List<GameObject>();

	public bool useAttractor;

	public List<GameObject> attractors = new List<GameObject>();

	public List<Vector3> attractorPositions = new List<Vector3>();

	public List<float> attractorStrength = new List<float>();

	public List<float> attractorAttenuation = new List<float>();

	public bool moveToMesh;

	public bool useSkinnedMesh;

	public GameObject meshTarget;

	public GameObject skinnedMeshTarget;

	public float particleSpeedToMesh = 0.5f;

	public MeshFollow meshFollow;

	public bool emitFromMeshTarget;

	private Vector3[] targets;

	private Mesh resetMesh;

	public float smallestTriangle = 0.1f;

	public DivisionType centerDivision;

	private ParticleSystem.Particle[] particles;

	private int particleCount;

	private float deltaTime;

	private List<int> index = new List<int>();

	private List<int> length = new List<int>();

	private int turbulenceWidth;

	private Vector3 currentPosition;

	private float m_lastUpdateTime;

	private Vector3 desiredVelocity;

	private Vector3 _desiredVelocity;

	private Vector3 calculatedTurbulence;

	private float turbX;

	private float turbY;

	private float turbZ;

	private int positionRemapedX;

	private int positionRemapedY;

	private int positionRemapedZ;

	private int indexTurb;

	private float turbulenceValue;

	private GameObject temp;

	private int tempC;

	private int tempT;

	private int safeIndex;

	private Mesh skinnedMeshBuffer;

	private Vector3[] skinnedMeshVertices;

	private ParticleSystem particleSystem;

	private void Start()
	{
		particleSystem = GetComponent<ParticleSystem>();
		m_lastUpdateTime = Time.realtimeSinceStartup;
		UpdateTurbulence();
		if (UseCalculationMethodX == CalculationMethod.Audio || UseCalculationMethodY == CalculationMethod.Audio || UseCalculationMethodZ == CalculationMethod.Audio)
		{
			calculateAudioTurbulence();
		}
		if (randomizeOffsetAtStart)
		{
			Offset = new Vector3(Random.Range(randomOffsetRange.x, randomOffsetRange.y), Random.Range(randomOffsetRange.x, randomOffsetRange.y), Random.Range(randomOffsetRange.x, randomOffsetRange.y));
		}
	}

	private void OnCollisionEnter(Collision col)
	{
		if (burstOnCollision)
		{
			particleSystem.Emit(burstNum);
			particleSystem.Play();
		}
	}

	private void Update()
	{
		if (!UseTurbulence)
		{
			return;
		}
		checkValues();
		if (!particleSystem)
		{
			gameObject.active = false;
			return;
		}
		if (isDeltaIndependent)
		{
			deltaTime = 1f / targetFps;
		}
		else
		{
			deltaTime = Time.deltaTime;
			if (!Application.isPlaying)
			{
				deltaTime = Time.realtimeSinceStartup - m_lastUpdateTime;
				m_lastUpdateTime = Time.realtimeSinceStartup;
			}
		}
		if (particles == null || particleSystem.maxParticles != particles.Length)
		{
			particles = new ParticleSystem.Particle[particleSystem.maxParticles];
		}
		particleCount = particleSystem.GetParticles(particles);
		currentPosition = transform.position;
		for (int i = 0; i < attractors.Count; i++)
		{
			attractorPositions[i] = attractors[i].transform.position;
		}
		HayateTurbulence();
		particleSystem.SetParticles(particles, particleCount);
	}

	private void HayateTurbulence()
	{
		for (int i = 0; i < particleCount; i++)
		{
			if (AssignTurbulenceTo == AssignTo.position)
			{
				if (UseRelativeOrAbsoluteValues == TurbulenceType.absolute)
				{
					particles[i].position = GetTurbulence(particles[i].position);
				}
				else
				{
					particles[i].position += GetTurbulence(particles[i].position);
				}
			}
			else if (UseRelativeOrAbsoluteValues == TurbulenceType.absolute)
			{
				particles[i].velocity = GetTurbulence(particles[i].position);
			}
			else
			{
				particles[i].velocity += GetTurbulence(particles[i].position);
			}
			if (useAttractor)
			{
				for (int j = 0; j < attractors.Count; j++)
				{
					desiredVelocity = attractorPositions[j] - particles[i].position;
					particles[i].velocity += desiredVelocity.normalized * attractorStrength[j] * deltaTime * (1f - Mathf.Clamp01(Vector3.Distance(particles[i].position, attractorPositions[j]) / attractorAttenuation[j]));
				}
			}
			if (useAlphaMask && removeCurrentParticle && particles[i].lifetime >= particles[i].startLifetime - threshold)
			{
				particles[i].lifetime = 0f;
				removeCurrentParticle = false;
			}
		}
		if (useTransformParticle && (bool)transformParticle)
		{
			for (int k = 0; k < transformParticles.Count; k++)
			{
				if (transformParticles[k] == null)
				{
					transformParticles[k] = Object.Instantiate(transformParticle, particles[k].position, Quaternion.identity) as GameObject;
				}
			}
			if (particleCount > transformParticles.Count)
			{
				tempC = transformParticles.Count;
				tempT = particleCount - transformParticles.Count;
				for (int l = 0; l < tempT; l++)
				{
					temp = Object.Instantiate(transformParticle, particles[tempC + l].position, Quaternion.identity) as GameObject;
					transformParticles.Add(temp);
				}
			}
			if (particleCount < transformParticles.Count)
			{
				tempT = transformParticles.Count - particleCount;
				for (int m = 0; m < tempT; m++)
				{
					Object.DestroyImmediate(transformParticles[transformParticles.Count - 1]);
					transformParticles.RemoveAt(transformParticles.Count - 1);
				}
			}
			for (int n = 0; n < transformParticles.Count; n++)
			{
				transformParticles[n].transform.position = particles[n].position;
				if (transformParticleLookTowardsFlightDirection)
				{
					transformParticles[n].transform.LookAt(particles[n].position + particles[n].velocity);
				}
				if (!(particles[n].lifetime < 1f))
				{
					continue;
				}
				if (detachTransformParticleAfterParticleDeath && Application.isPlaying)
				{
					if (detachedObjectDestructionTimeAfter != -1f)
					{
						Object.Destroy(transformParticles[n], detachedObjectDestructionTimeAfter);
					}
					transformParticles[n] = null;
					particles[n].lifetime = -1f;
				}
				else
				{
					Object.DestroyImmediate(transformParticles[n]);
					particles[n].lifetime = -1f;
				}
			}
		}
		else
		{
			for (int num = 0; num < transformParticles.Count; num++)
			{
				Object.DestroyImmediate(transformParticles[num]);
				transformParticles.RemoveAt(num);
			}
		}
		if (lockOffsetToEmitterPosition)
		{
			float num2 = turbulenceWidth / 2;
			if (UseCalculationMethodX == CalculationMethod.precalculatedTexture || UseCalculationMethodY == CalculationMethod.precalculatedTexture || UseCalculationMethodZ == CalculationMethod.precalculatedTexture || UseCalculationMethodX == CalculationMethod.Audio || UseCalculationMethodY == CalculationMethod.Audio || UseCalculationMethodZ == CalculationMethod.Audio)
			{
				Offset = new Vector3(currentPosition.x / Frequency.x, currentPosition.y / Frequency.y, currentPosition.z / Frequency.z) * 100f + new Vector3(num2, num2, num2);
			}
			else
			{
				Offset = new Vector3(currentPosition.x / Frequency.x, currentPosition.y / Frequency.y, currentPosition.z / Frequency.z);
			}
		}
		else
		{
			Offset += OffsetSpeed * deltaTime;
		}
		if (!moveToMesh)
		{
			return;
		}
		if (!useSkinnedMesh && (bool)meshTarget)
		{
			if ((bool)meshTarget.GetComponent<MeshFilter>())
			{
				targets = meshTarget.GetComponent<MeshFilter>().sharedMesh.vertices;
				for (int num3 = 0; num3 < particleCount; num3++)
				{
					if (num3 < targets.Length)
					{
						if (meshFollow == MeshFollow.byDistance)
						{
							particles[num3].position = Vector3.Lerp(particles[num3].position, meshTarget.transform.TransformPoint(targets[num3]), 1f / Mathf.Pow(Vector3.Distance(meshTarget.transform.TransformPoint(targets[num3]), particles[num3].position), 2f) * particleSpeedToMesh);
							continue;
						}
						if (meshFollow == MeshFollow.byTime)
						{
							particles[num3].position = Vector3.Lerp(particles[num3].position, meshTarget.transform.TransformPoint(targets[num3]), particleSpeedToMesh * deltaTime);
							continue;
						}
						_desiredVelocity = meshTarget.transform.TransformPoint(targets[num3]) - particles[num3].position;
						particles[num3].velocity += _desiredVelocity.normalized * particleSpeedToMesh * deltaTime;
					}
				}
			}
			else
			{
				meshTarget = null;
			}
		}
		if (!useSkinnedMesh || !skinnedMeshTarget)
		{
			return;
		}
		if ((bool)skinnedMeshTarget.GetComponent<SkinnedMeshRenderer>())
		{
			if (skinnedMeshBuffer == null)
			{
				skinnedMeshBuffer = new Mesh();
			}
			skinnedMeshTarget.GetComponent<SkinnedMeshRenderer>().BakeMesh(skinnedMeshBuffer);
			skinnedMeshVertices = skinnedMeshBuffer.vertices;
			targets = skinnedMeshBuffer.vertices;
			for (int num4 = 0; num4 < particleCount; num4++)
			{
				safeIndex = num4 % targets.Length;
				if (particles[num4].startLifetime - particles[num4].lifetime <= 0.05f)
				{
					particles[num4].position = skinnedMeshTarget.transform.TransformPoint(skinnedMeshVertices[Random.Range(0, skinnedMeshVertices.Length)]);
					continue;
				}
				if (meshFollow == MeshFollow.byDistance)
				{
					particles[num4].position = Vector3.Lerp(particles[num4].position, skinnedMeshTarget.transform.TransformPoint(targets[safeIndex]), 1f / Mathf.Pow(Vector3.Distance(skinnedMeshTarget.transform.TransformPoint(targets[safeIndex]), particles[num4].position), 2f) * particleSpeedToMesh);
					continue;
				}
				if (meshFollow == MeshFollow.byTime)
				{
					particles[num4].position = Vector3.Lerp(particles[num4].position, skinnedMeshTarget.transform.TransformPoint(targets[safeIndex]), particleSpeedToMesh * deltaTime);
					continue;
				}
				_desiredVelocity = skinnedMeshTarget.transform.TransformPoint(targets[safeIndex]) - particles[num4].position;
				particles[num4].velocity += _desiredVelocity.normalized * particleSpeedToMesh * deltaTime;
			}
		}
		else
		{
			skinnedMeshTarget = null;
		}
	}

	public Vector3 GetTurbulence(Vector3 _position)
	{
		if (UseCalculationMethodX == CalculationMethod.sine)
		{
			turbX = (Mathf.Sin(_position.z / Frequency.x - Offset.x) * Amplitude.x + GlobalForce.x) * deltaTime;
		}
		else if (UseCalculationMethodX == CalculationMethod.cosine)
		{
			turbX = (Mathf.Cos(_position.z / Frequency.x - Offset.x) * Amplitude.x + GlobalForce.x) * deltaTime;
		}
		else if (UseCalculationMethodX == CalculationMethod.perlin)
		{
			turbX = ((Mathf.PerlinNoise(_position.z / Frequency.z - Offset.z, _position.y / Frequency.y - Offset.y) * 2f - 1f) * Amplitude.x + GlobalForce.x) * deltaTime;
		}
		else if (UseCalculationMethodX == CalculationMethod.precalculatedTexture)
		{
			if (turbulence != null && turbulence.Length != 0)
			{
				turbX = (calculateTextureNoiseOnXAxis(_position.z / Frequency.z * 100f - Offset.z, _position.y / Frequency.y * 100f - Offset.y) + GlobalForce.x) * deltaTime;
			}
		}
		else if (UseCalculationMethodX == CalculationMethod.Audio && hayateSamples != null && hayateSamples.Length != 0)
		{
			turbX = (calculateAudioNoiseX(_position.z / Frequency.z * 100f - Offset.z, _position.y / Frequency.y * 100f - Offset.y) + GlobalForce.x) * deltaTime;
		}
		if (UseCalculationMethodY == CalculationMethod.sine)
		{
			turbY = (Mathf.Sin(_position.z / Frequency.y - Offset.y) * Amplitude.y + GlobalForce.y) * deltaTime;
		}
		else if (UseCalculationMethodY == CalculationMethod.cosine)
		{
			turbY = (Mathf.Cos(_position.z / Frequency.y - Offset.y) * Amplitude.y + GlobalForce.y) * deltaTime;
		}
		else if (UseCalculationMethodY == CalculationMethod.perlin)
		{
			turbY = ((Mathf.PerlinNoise(_position.x / Frequency.x - Offset.x, _position.z / Frequency.z - Offset.z) * 2f - 1f) * Amplitude.y + GlobalForce.y) * deltaTime;
		}
		else if (UseCalculationMethodY == CalculationMethod.precalculatedTexture)
		{
			if (turbulence != null && turbulence.Length != 0)
			{
				turbY = (calculateTextureNoiseOnYAxis(_position.x / Frequency.x * 100f - Offset.x, _position.z / Frequency.z * 100f - Offset.z) + GlobalForce.y) * deltaTime;
			}
		}
		else if (UseCalculationMethodY == CalculationMethod.Audio && hayateSamples != null && hayateSamples.Length != 0)
		{
			turbY = (calculateAudioNoiseY(_position.x / Frequency.x * 100f - Offset.x, _position.z / Frequency.z * 100f - Offset.z) + GlobalForce.y) * deltaTime;
		}
		if (UseCalculationMethodZ == CalculationMethod.sine)
		{
			turbZ = (Mathf.Sin(_position.x / Frequency.z - Offset.z) * Amplitude.z + GlobalForce.z) * deltaTime;
		}
		else if (UseCalculationMethodZ == CalculationMethod.cosine)
		{
			turbZ = (Mathf.Cos(_position.x / Frequency.z - Offset.z) * Amplitude.z + GlobalForce.z) * deltaTime;
		}
		else if (UseCalculationMethodZ == CalculationMethod.perlin)
		{
			turbZ = ((Mathf.PerlinNoise(_position.y / Frequency.y - Offset.y, _position.x / Frequency.x - Offset.x) * 2f - 1f) * Amplitude.z + GlobalForce.z) * deltaTime;
		}
		else if (UseCalculationMethodZ == CalculationMethod.precalculatedTexture)
		{
			if (turbulence != null && turbulence.Length != 0)
			{
				turbZ = (calculateTextureNoiseOnZAxis(_position.y / Frequency.y * 100f - Offset.y, _position.x / Frequency.x * 100f - Offset.x) + GlobalForce.z) * deltaTime;
			}
		}
		else if (UseCalculationMethodZ == CalculationMethod.Audio && hayateSamples != null && hayateSamples.Length != 0)
		{
			turbZ = (calculateAudioNoiseZ(_position.y / Frequency.y * 100f - Offset.y, _position.x / Frequency.x * 100f - Offset.x) + GlobalForce.z) * deltaTime;
		}
		calculatedTurbulence = new Vector3(turbX, turbY, turbZ);
		return calculatedTurbulence;
	}

	public void calculateAudioTurbulence()
	{
		if (!audioClip)
		{
			return;
		}
		hayateSamples = new float[amountOfSamples * amountOfSamples];
		float[] array = new float[amountOfSamples * 2];
		float[] array2 = new float[amountOfSamples * 2];
		audioClip.GetData(array, atSample);
		audioClip.GetData(array2, atSample + amountOfSamples * 2);
		for (int i = 0; i < amountOfSamples; i++)
		{
			for (int j = 0; j < amountOfSamples; j++)
			{
				hayateSamples[j * amountOfSamples + i] = array2[i * 2] + array[j * 2] / 2f;
			}
		}
	}

	public float calculateAudioNoiseX(float _positionZ, float _positionY)
	{
		positionRemapedZ = (int)(_positionZ % (float)amountOfSamples);
		positionRemapedY = (int)(_positionY % (float)amountOfSamples);
		indexTurb = positionRemapedZ * amountOfSamples + positionRemapedY;
		indexTurb = Mathf.Abs(indexTurb);
		turbulenceValue = hayateSamples[indexTurb] * Amplitude.x * deltaTime;
		return turbulenceValue;
	}

	public float calculateAudioNoiseY(float _positionX, float _positionZ)
	{
		positionRemapedX = (int)(_positionX % (float)amountOfSamples);
		positionRemapedZ = (int)(_positionZ % (float)amountOfSamples);
		indexTurb = positionRemapedX * amountOfSamples + positionRemapedZ;
		indexTurb = Mathf.Abs(indexTurb);
		turbulenceValue = hayateSamples[indexTurb] * Amplitude.y * deltaTime;
		return turbulenceValue;
	}

	public float calculateAudioNoiseZ(float _positionY, float _positionX)
	{
		positionRemapedY = (int)(_positionY % (float)amountOfSamples);
		positionRemapedX = (int)(_positionX % (float)amountOfSamples);
		indexTurb = positionRemapedX * amountOfSamples + positionRemapedY;
		indexTurb = Mathf.Abs(indexTurb);
		turbulenceValue = hayateSamples[indexTurb] * Amplitude.z * deltaTime;
		return turbulenceValue;
	}

	public float calculateTextureNoiseOnXAxis(float _positionZ, float _positionY)
	{
		positionRemapedX = (int)(_positionZ % (float)Turbulence.width);
		positionRemapedY = (int)(_positionY % (float)Turbulence.height);
		indexTurb = positionRemapedY * Turbulence.width + positionRemapedX;
		indexTurb = Mathf.Abs(indexTurb);
		if (useAlphaMask && turbulence[indexTurb].a == 0)
		{
			removeCurrentParticle = true;
		}
		turbulenceValue = ((float)(int)turbulence[indexTurb].r / 256f * 2f - 1f) * Amplitude.x * deltaTime;
		return turbulenceValue;
	}

	public float calculateTextureNoiseOnYAxis(float _positionX, float _positionZ)
	{
		positionRemapedX = (int)(_positionX % (float)Turbulence.width);
		positionRemapedZ = (int)(_positionZ % (float)Turbulence.height);
		indexTurb = positionRemapedX * Turbulence.width + positionRemapedZ;
		indexTurb = Mathf.Abs(indexTurb);
		if (useAlphaMask && turbulence[indexTurb].a == 0)
		{
			removeCurrentParticle = true;
		}
		turbulenceValue = ((float)(int)turbulence[indexTurb].g / 256f * 2f - 1f) * Amplitude.y * deltaTime;
		return turbulenceValue;
	}

	public float calculateTextureNoiseOnZAxis(float _positionY, float _positionX)
	{
		positionRemapedY = (int)(_positionY % (float)Turbulence.width);
		positionRemapedX = (int)(_positionX % (float)Turbulence.height);
		indexTurb = positionRemapedX * Turbulence.width + positionRemapedY;
		indexTurb = Mathf.Abs(indexTurb);
		if (useAlphaMask && turbulence[indexTurb].a == 0)
		{
			removeCurrentParticle = true;
		}
		turbulenceValue = ((float)(int)turbulence[indexTurb].b / 256f * 2f - 1f) * Amplitude.z * deltaTime;
		return turbulenceValue;
	}

	public void UpdateTurbulence()
	{
		if ((bool)Turbulence)
		{
			turbulenceWidth = Turbulence.width;
			turbulence = Turbulence.GetPixels32(0);
		}
		else if (UseCalculationMethodX != CalculationMethod.precalculatedTexture && UseCalculationMethodY != CalculationMethod.precalculatedTexture)
		{
			_ = UseCalculationMethodZ;
			_ = 4;
		}
	}

	private float blendBetween(float v1, float v2, float perc)
	{
		return v1 + perc * (v2 - v1);
	}

	public void retrieveMeshInfo()
	{
		if (!meshTarget)
		{
			return;
		}
		List<float> list = new List<float>();
		Vector3[] vertices = meshTarget.GetComponent<MeshFilter>().sharedMesh.vertices;
		int[] triangles = meshTarget.GetComponent<MeshFilter>().sharedMesh.triangles;
		for (int i = 0; i < triangles.Length; i += 3)
		{
			float num = SignedVolumeOfTriangle(vertices[triangles[i]], vertices[triangles[i + 1]], vertices[triangles[i + 2]]);
			if (num != 0f)
			{
				list.Add(num);
			}
		}
		list.Sort();
	}

	public void rstMesh()
	{
		if ((bool)resetMesh)
		{
			meshTarget.GetComponent<MeshFilter>().sharedMesh = resetMesh;
		}
	}

	public void UpdateMeshCoordinates()
	{
		if (!meshTarget)
		{
			return;
		}
		resetMesh = meshTarget.GetComponent<MeshFilter>().sharedMesh;
		List<Vector3> list = new List<Vector3>();
		List<int> list2 = new List<int>();
		List<Vector2> list3 = new List<Vector2>();
		List<Vector3> list4 = new List<Vector3>();
		if (!meshTarget)
		{
			return;
		}
		for (int i = 0; i < meshTarget.GetComponent<MeshFilter>().sharedMesh.vertices.Length; i++)
		{
			list.Add(meshTarget.GetComponent<MeshFilter>().sharedMesh.vertices[i]);
		}
		for (int j = 0; j < meshTarget.GetComponent<MeshFilter>().sharedMesh.triangles.Length; j++)
		{
			list2.Add(meshTarget.GetComponent<MeshFilter>().sharedMesh.triangles[j]);
		}
		for (int k = 0; k < meshTarget.GetComponent<MeshFilter>().sharedMesh.uv.Length; k++)
		{
			list3.Add(meshTarget.GetComponent<MeshFilter>().sharedMesh.uv[k]);
		}
		for (int l = 0; l < meshTarget.GetComponent<MeshFilter>().sharedMesh.normals.Length; l++)
		{
			list4.Add(meshTarget.GetComponent<MeshFilter>().sharedMesh.normals[l]);
		}
		List<Vector3> list5 = new List<Vector3>(list);
		List<int> list6 = new List<int>(list2);
		List<Vector2> list7 = new List<Vector2>(list3);
		List<Vector3> list8 = new List<Vector3>(list4);
		if (centerDivision == DivisionType.center)
		{
			for (int m = 0; m < list6.Count; m += 3)
			{
				if (SignedVolumeOfTriangle(list5[list6[m]], list5[list6[m + 1]], list5[list6[m + 2]]) > smallestTriangle)
				{
					if (list5.Count > 60000)
					{
						break;
					}
					int num = list6[m];
					int num2 = list6[m + 1];
					int item = list6[m + 2];
					int num3 = m;
					int num4 = m + 1;
					int num5 = m + 2;
					Vector3 vector = list5[num];
					Vector3 vector2 = list5[num2];
					Vector3 vector3 = list5[item];
					Vector3 vector4 = list8[num];
					Vector3 vector5 = list8[num2];
					Vector3 vector6 = list8[item];
					Vector2 vector7 = list7[num];
					Vector2 vector8 = list7[num2];
					Vector2 vector9 = list7[item];
					Vector3 item2 = (vector + vector2 + vector3) / 3f;
					Vector3 normalized = ((vector4 + vector5 + vector6) / 3f).normalized;
					Vector2 item3 = (vector7 + vector8 + vector9) / 3f;
					int count = list5.Count;
					list5.Add(item2);
					list8.Add(normalized);
					list7.Add(item3);
					list6[num3] = num;
					list6[num4] = num2;
					list6[num5] = count;
					list6.Add(count);
					list6.Add(num2);
					list6.Add(item);
					list6.Add(num);
					list6.Add(count);
					list6.Add(item);
					if (SignedVolumeOfTriangle(list5[num], list5[num2], list5[count]) > smallestTriangle)
					{
						m -= 3;
					}
				}
			}
		}
		else
		{
			for (int n = 0; n < list6.Count; n += 3)
			{
				if (SignedVolumeOfTriangle(list5[list6[n]], list5[list6[n + 1]], list5[list6[n + 2]]) > smallestTriangle)
				{
					if (list5.Count > 40000)
					{
						MonoBehaviour.print("Stoped! Too many vertices");
						break;
					}
					int value = list6[n];
					int item4 = list6[n + 1];
					int item5 = list6[n + 2];
					int num6 = n;
					int num7 = n + 1;
					int num8 = n + 2;
					Vector3 vector10 = list5[value];
					Vector3 vector11 = list5[item4];
					Vector3 vector12 = list5[item5];
					Vector3 vector13 = list8[value];
					Vector3 vector14 = list8[item4];
					Vector3 vector15 = list8[item5];
					Vector2 vector16 = list7[value];
					Vector2 vector17 = list7[item4];
					Vector2 vector18 = list7[item5];
					Vector3 item6 = (vector10 + vector11) / 2f;
					Vector3 item7 = (vector11 + vector12) / 2f;
					Vector3 item8 = (vector12 + vector10) / 2f;
					Vector3 normalized2 = ((vector13 + vector14) / 2f).normalized;
					Vector3 normalized3 = ((vector14 + vector15) / 2f).normalized;
					Vector3 normalized4 = ((vector15 + vector13) / 2f).normalized;
					Vector2 item9 = (vector16 + vector17) / 2f;
					Vector2 item10 = (vector17 + vector18) / 2f;
					Vector2 item11 = (vector18 + vector16) / 2f;
					int count2 = list5.Count;
					int item12 = list5.Count + 1;
					int num9 = list5.Count + 2;
					list5.Add(item6);
					list5.Add(item7);
					list5.Add(item8);
					list8.Add(normalized2);
					list8.Add(normalized3);
					list8.Add(normalized4);
					list7.Add(item9);
					list7.Add(item10);
					list7.Add(item11);
					list6[num6] = value;
					list6[num7] = count2;
					list6[num8] = num9;
					list6.Add(count2);
					list6.Add(item12);
					list6.Add(num9);
					list6.Add(count2);
					list6.Add(item4);
					list6.Add(item12);
					list6.Add(num9);
					list6.Add(item12);
					list6.Add(item5);
					if (SignedVolumeOfTriangle(list5[value], list5[count2], list5[num9]) > smallestTriangle)
					{
						n -= 3;
					}
				}
			}
		}
		Vector3[] array = new Vector3[list5.Count];
		Vector3[] array2 = new Vector3[list8.Count];
		Vector2[] array3 = new Vector2[list5.Count];
		int[] array4 = new int[list6.Count];
		for (int num10 = 0; num10 < list7.Count; num10++)
		{
			array3[num10] = list7[num10];
		}
		for (int num11 = 0; num11 < list8.Count; num11++)
		{
			array2[num11] = list8[num11];
		}
		for (int num12 = 0; num12 < list5.Count; num12++)
		{
			array[num12] = list5[num12];
		}
		for (int num13 = 0; num13 < list6.Count; num13++)
		{
			array4[num13] = list6[num13];
		}
		Mesh mesh = new Mesh();
		mesh.vertices = array;
		mesh.triangles = array4;
		mesh.uv = array3;
		mesh.normals = array2;
		mesh.RecalculateBounds();
		meshTarget.GetComponent<MeshFilter>().mesh = mesh;
	}

	private float SignedVolumeOfTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
	{
		float num = p3.x * p2.y * p1.z;
		float num2 = p2.x * p3.y * p1.z;
		float num3 = p3.x * p1.y * p2.z;
		float num4 = p1.x * p3.y * p2.z;
		float num5 = p2.x * p1.y * p3.z;
		float num6 = p1.x * p2.y * p3.z;
		return Mathf.Abs(1f / 6f * (0f - num + num2 + num3 - num4 - num5 + num6));
	}

	private void checkValues()
	{
		if ((bool)audioClip)
		{
			atSample = (int)Mathf.Clamp(atSample, 0f, (float)audioClip.samples - (float)amountOfSamples * 2f);
			amountOfSamples = (int)Mathf.Clamp(amountOfSamples, 0f, 2048f);
		}
		if (Frequency.x == 0f)
		{
			Frequency = new Vector3(0.001f, Frequency.y, Frequency.z);
		}
		if (Frequency.y == 0f)
		{
			Frequency = new Vector3(Frequency.x, 0.001f, Frequency.z);
		}
		if (Frequency.z == 0f)
		{
			Frequency = new Vector3(Frequency.x, Frequency.y, 0.001f);
		}
		if (Amplitude.x == 0f)
		{
			Debug.LogWarning("Frequency must not be 0, ( x < 0 > x)");
			Amplitude = new Vector3(0.001f, Amplitude.y, Amplitude.z);
		}
		if (Amplitude.y == 0f)
		{
			Amplitude = new Vector3(Amplitude.x, 0.001f, Amplitude.z);
		}
		if (Amplitude.z == 0f)
		{
			Amplitude = new Vector3(Amplitude.x, Amplitude.y, 0.001f);
		}
		if (stepSize.x == 0f)
		{
			stepSize = new Vector3(1f, stepSize.y, stepSize.z);
		}
		if (stepSize.y == 0f)
		{
			stepSize = new Vector3(stepSize.x, 1f, stepSize.z);
		}
		if (stepSize.z == 0f)
		{
			stepSize = new Vector3(stepSize.x, stepSize.y, 1f);
		}
	}
}
