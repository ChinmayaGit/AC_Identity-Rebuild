using System.Collections.Generic;
using UnityEngine;

public class PipelineWind : PipelineBehaviour
{
	public enum Type
	{
		GreenFlutterMaskBlueBend,
		GreenFlutterAmplitudeBlueBend
	}

	[SerializeField]
	private Type m_type;

	[SerializeField]
	private bool m_ignoreFlutter;

	[SerializeField]
	private float m_objFlutterScale = 1f;

	[SerializeField]
	private bool m_ignoreBend;

	[SerializeField]
	private float m_objBendScale = 1f;

	private void Awake()
	{
		MeshFilter component = GetComponent<MeshFilter>();
		Mesh mesh = Object.Instantiate(component.sharedMesh);
		if (mesh != null)
		{
			Preprocess(mesh);
			component.sharedMesh = mesh;
		}
		Object.Destroy(this);
	}

	public void Preprocess(Mesh mesh)
	{
		Color[] colors = mesh.colors;
		Vector3[] vertices = mesh.vertices;
		Dictionary<int, float> dictionary = new Dictionary<int, float>();
		Dictionary<int, float> dictionary2 = new Dictionary<int, float>();
		Dictionary<int, float> dictionary3 = new Dictionary<int, float>();
		Dictionary<int, float> dictionary4 = new Dictionary<int, float>();
		if (colors == null)
		{
			throw new InitializeException(this, "Vertex animated object does not have the required painted vertex colors in its mesh!");
		}
		int num = colors.Length;
		Color[] array = new Color[num];
		float value = Random.value;
		float value2 = 0f;
		float value3 = 0f;
		float value4 = 0f;
		float value5 = 0f;
		for (int i = 0; i < num; i++)
		{
			bool flag = true;
			Color black = Color.black;
			int hashCode = vertices[i].GetHashCode();
			if (flag)
			{
				flag &= dictionary.TryGetValue(hashCode, out value2);
			}
			if (flag)
			{
				flag &= dictionary2.TryGetValue(hashCode, out value3);
			}
			if (flag)
			{
				flag &= dictionary3.TryGetValue(hashCode, out value4);
			}
			if (flag)
			{
				flag &= dictionary4.TryGetValue(hashCode, out value5);
			}
			if (!flag)
			{
				float num2 = colors[i].g;
				if (m_type == Type.GreenFlutterMaskBlueBend)
				{
					num2 = ((colors[i].g > 0.5f) ? 1f : 0f);
				}
				num2 = (m_ignoreFlutter ? 0f : num2);
				float num3 = (m_ignoreBend ? 0f : colors[i].b);
				float num4 = Random.value;
				if (m_type == Type.GreenFlutterMaskBlueBend)
				{
					num4 *= num2;
				}
				black = new Color(value, num4, num3 * m_objBendScale, num2 * m_objFlutterScale);
				dictionary.Add(hashCode, black.r);
				dictionary2.Add(hashCode, black.g);
				dictionary3.Add(hashCode, black.b);
				dictionary4.Add(hashCode, black.a);
			}
			else
			{
				black = new Color(value2, value3, value4, value5);
			}
			array[i] = black;
		}
		mesh.colors = array;
	}
}
