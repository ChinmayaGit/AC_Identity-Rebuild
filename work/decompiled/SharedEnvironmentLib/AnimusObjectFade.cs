using System.Collections.Generic;
using AcierProtocol;
using UnityEngine;

public class AnimusObjectFade : StaticObjectMeta
{
	private struct DependentFadeEntry
	{
		public ObjectFade fade;

		public ObjectFade.Description desc;
	}

	private float m_elapsedTime;

	private List<Material> m_fadeMaterials = new List<Material>();

	private List<Renderer> m_fadeRenderers = new List<Renderer>();

	private List<DependentFadeEntry> m_dependentFadeEffects = new List<DependentFadeEntry>();

	private List<ObjectFade> m_tempFadeEffects = new List<ObjectFade>();

	private List<Renderer> m_disabledRenderers = new List<Renderer>();

	private bool m_isActive;

	private bool m_isFadeIn;

	private const float kDuration = 1.5f;

	private static int s_FadeColorId = -1;

	public void Start()
	{
		if (s_FadeColorId < 0)
		{
			s_FadeColorId = Shader.PropertyToID("_FadeColor");
		}
		Collect();
	}

	public static bool CanBeFaded(Material _mat)
	{
		if (!(_mat.shader.name == "Animus/MeshFade") && !(_mat.shader.name == "Animus/4Splat"))
		{
			if (_mat.shader.name.Contains("Generated/"))
			{
				return _mat.shader.name.Contains("Fade");
			}
			return false;
		}
		return true;
	}

	public void Collect()
	{
		m_fadeMaterials.Clear();
		m_fadeRenderers.Clear();
		MeshRenderer[] componentsInChildren = gameObject.GetComponentsInChildren<MeshRenderer>();
		foreach (MeshRenderer meshRenderer in componentsInChildren)
		{
			Material[] materials = meshRenderer.materials;
			bool flag = false;
			for (int j = 0; j < meshRenderer.sharedMaterials.Length; j++)
			{
				if (CanBeFaded(meshRenderer.sharedMaterials[j]))
				{
					flag = true;
					m_fadeMaterials.Add(materials[j]);
				}
			}
			if (flag)
			{
				m_fadeRenderers.Add(meshRenderer);
			}
		}
	}

	public void Update()
	{
		if (m_isActive)
		{
			if (m_isFadeIn)
			{
				m_elapsedTime += Time.deltaTime;
			}
			else
			{
				m_elapsedTime -= Time.deltaTime;
			}
			SetFadingProgress();
			if ((m_isFadeIn && m_elapsedTime >= 1.5f) || (!m_isFadeIn && m_elapsedTime <= 0f))
			{
				HandleFadeFinished();
			}
		}
	}

	private void SetFadingProgress()
	{
		float a = Mathf.Clamp01(m_elapsedTime * (2f / 3f));
		for (int i = 0; i < m_fadeMaterials.Count; i++)
		{
			Color color = m_fadeMaterials[i].GetColor(s_FadeColorId);
			color.a = a;
			m_fadeMaterials[i].SetColor(s_FadeColorId, color);
		}
	}

	private void HandleFadeFinished()
	{
		m_isActive = false;
		for (int i = 0; i < m_fadeMaterials.Count; i++)
		{
			m_fadeMaterials[i].DisableKeyword("FANCY_FADE");
			m_fadeMaterials[i].EnableKeyword("SIMPLE_FADE");
		}
		for (int j = 0; j < m_fadeRenderers.Count; j++)
		{
			m_fadeRenderers[j].enabled = m_isFadeIn;
		}
		for (int k = 0; k < m_disabledRenderers.Count; k++)
		{
			m_disabledRenderers[k].enabled = m_isFadeIn;
		}
		for (int l = 0; l < m_tempFadeEffects.Count; l++)
		{
			Object.Destroy(m_tempFadeEffects[l]);
		}
		for (int m = 0; m < m_dependentFadeEffects.Count; m++)
		{
			ObjectFade fade = m_dependentFadeEffects[m].fade;
			if (fade != null)
			{
				fade.InitFromDescription(m_dependentFadeEffects[m].desc);
			}
		}
	}

	public void Reset(bool _fadeIn, bool _startAnimation)
	{
		Collect();
		m_elapsedTime = (_fadeIn ? 0f : 1.5f);
		m_isFadeIn = _fadeIn;
		for (int i = 0; i < m_fadeMaterials.Count; i++)
		{
			m_fadeMaterials[i].EnableKeyword("FANCY_FADE");
			m_fadeMaterials[i].DisableKeyword("SIMPLE_FADE");
		}
		for (int j = 0; j < m_fadeRenderers.Count; j++)
		{
			m_fadeRenderers[j].enabled = true;
		}
		CollectAndPrepareDependentFadeEffects();
		for (int k = 0; k < m_dependentFadeEffects.Count; k++)
		{
			ObjectFade fade = m_dependentFadeEffects[k].fade;
			if (fade != null)
			{
				fade.Reset(m_isFadeIn, _startAnimation);
			}
		}
		for (int l = 0; l < m_tempFadeEffects.Count; l++)
		{
			ObjectFade objectFade = m_tempFadeEffects[l];
			if (objectFade != null)
			{
				objectFade.Reset(m_isFadeIn, _startAnimation);
			}
		}
	}

	private void CollectAndPrepareDependentFadeEffects()
	{
		m_dependentFadeEffects.Clear();
		ObjectFade[] componentsInChildren = gameObject.GetComponentsInChildren<ObjectFade>();
		ObjectFade.Description description = new ObjectFade.Description();
		description.fadeIn = m_isFadeIn;
		description.fadeMode = EFadeMode.Raster_EFadeMode;
		description.fadeDuration = 1.5f;
		description.onFadeDoneCallback = null;
		DependentFadeEntry item = default;
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			item.fade = componentsInChildren[i];
			item.fade.FadeInProgress = false;
			item.desc = componentsInChildren[i].GetDescription();
			m_dependentFadeEffects.Add(item);
			componentsInChildren[i].InitFromDescription(description);
		}
		m_disabledRenderers.Clear();
		m_tempFadeEffects.Clear();
		Renderer[] componentsInChildren2 = gameObject.GetComponentsInChildren<Renderer>();
		foreach (Renderer renderer in componentsInChildren2)
		{
			if (!(renderer.GetComponent<ObjectFade>() != null))
			{
				bool flag = true;
				bool flag2 = false;
				for (int k = 0; k < renderer.sharedMaterials.Length; k++)
				{
					Material mat = renderer.sharedMaterials[k];
					flag2 |= CanBeFaded(mat);
					flag &= ObjectFade.CanBeFaded(mat);
				}
				if (flag && !flag2)
				{
					ObjectFade objectFade = renderer.gameObject.AddComponent<ObjectFade>();
					objectFade.InitFromDescription(description);
					m_tempFadeEffects.Add(objectFade);
				}
				else if (!flag2)
				{
					renderer.enabled = false;
					m_disabledRenderers.Add(renderer);
				}
			}
		}
	}

	[ContextMenu("Fade In")]
	public void FadeIn()
	{
		Reset(_fadeIn: true, _startAnimation: true);
		m_isActive = true;
	}

	[ContextMenu("To End")]
	public void ToEnd()
	{
		Reset(_fadeIn: false, _startAnimation: false);
		SetFadingProgress();
		m_isActive = false;
	}

	[ContextMenu("Fade Out")]
	public void FadeOut()
	{
		Reset(_fadeIn: false, _startAnimation: true);
		m_isActive = true;
	}

	[ContextMenu("To Start")]
	public void ToStart()
	{
		Reset(_fadeIn: true, _startAnimation: false);
		SetFadingProgress();
		m_isActive = false;
	}

	public override void UpdateUniqueID()
	{
		UniqueID = StaticObjectMeta.GenerateUniqueID(this, "B");
	}
}
