using System.Collections;
using UnityEngine;

internal class AnimatedSpriteSheet : MonoBehaviour
{
	public int Columns = 5;

	public int Rows = 5;

	public float FramesPerSecond = 10f;

	public bool RunOnce = true;

	private Renderer renderer;

	private Material materialCopy;

	public float RunTimeInSeconds => 1f / FramesPerSecond * (float)(Columns * Rows);

	private void Start()
	{
		renderer = GetComponent<Renderer>();
		materialCopy = new Material(renderer.sharedMaterial);
		renderer.sharedMaterial = materialCopy;
		Vector2 scale = new Vector2(1f / (float)Columns, 1f / (float)Rows);
		renderer.sharedMaterial.SetTextureScale("_MainTex", scale);
	}

	private void OnEnable()
	{
		StartCoroutine(UpdateTiling());
	}

	private IEnumerator UpdateTiling()
	{
		Vector2 offset = Vector2.zero;
		do
		{
			for (int i = Rows - 1; i >= 0; i--)
			{
				float y = (float)i / (float)Rows;
				for (int j = 0; j <= Columns - 1; j++)
				{
					float new_x = (float)j / (float)Columns;
					offset.Set(new_x, y);
					renderer.sharedMaterial.SetTextureOffset("_MainTex", offset);
					yield return new WaitForSeconds(1f / FramesPerSecond);
				}
			}
		}
		while (!RunOnce);
	}
}
