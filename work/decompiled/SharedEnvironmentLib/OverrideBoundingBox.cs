using UnityEngine;

[ExecuteInEditMode]
public class OverrideBoundingBox : MonoBehaviour
{
	private Vector3 m_bbxMax;

	public float m_bbxSize = 1000000f;

	private Bounds m_boundingBox;

	private void Start()
	{
		m_bbxMax = new Vector3(m_bbxSize, m_bbxSize, m_bbxSize);
		m_boundingBox = new Bounds(Vector3.zero, m_bbxMax);
		SetBoundingBox(gameObject);
		foreach (Transform item in gameObject.transform)
		{
			SetBoundingBox(item.gameObject);
		}
	}

	private void SetBoundingBox(GameObject _go)
	{
		MeshFilter component = _go.GetComponent<MeshFilter>();
		if (component != null)
		{
			component.sharedMesh.bounds = m_boundingBox;
		}
	}
}
