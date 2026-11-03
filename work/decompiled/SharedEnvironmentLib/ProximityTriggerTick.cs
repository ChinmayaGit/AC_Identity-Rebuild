using SharedBaseLib.Core;
using UbiCore.Debug;
using UnityEngine;

[ExecuteInEditMode]
internal class ProximityTriggerTick : MonoBehaviour
{
	private class Cell
	{
		public Bounds bounds;

		public ProximityTrigger[] triggers;
	}

	private static ProximityTriggerTick ms_instance;

	[SerializeField]
	private float m_tickRatePerSecond = 4f;

	[SerializeField]
	private float m_unitsPerCell = 10f;

	private Bounds m_bounds;

	private Cell[][] m_cells;

	private BBList<ProximityTrigger> m_triggers;

	private BBList<TriggerTransform> m_transforms;

	private float m_accumTime;

	private float m_cellWidthInUnits;

	private float m_cellDepthInUnits;

	private int m_iterator;

	private bool m_dirty;

	public static ProximityTriggerTick Instance => ms_instance;

	private void Awake()
	{
		ms_instance = this;
		m_triggers = new BBList<ProximityTrigger>();
		m_transforms = new BBList<TriggerTransform>();
		AddTaggedTransforms("Player");
		AddTaggedTransforms("Enemy");
	}

	private void AddTaggedTransforms(string tag)
	{
		GameObject[] array = GameObject.FindGameObjectsWithTag(tag);
		for (int i = 0; i < array.Length; i++)
		{
			array[i].AddComponent<TriggerTransform>();
		}
	}

	private void OnPlayerSpawned(GameObject _player)
	{
		_player.AddComponent<TriggerTransform>();
	}

	private void OnNPCSpawned(GameObject _npc)
	{
		_npc.AddComponent<TriggerTransform>();
	}

	private bool TryEncapsulateSceneBounds(out Bounds bounds)
	{
		TriggerShape[] array = (TriggerShape[])Object.FindObjectsOfType(typeof(TriggerShape));
		int num = array.Length;
		if (array.Length >= 1)
		{
			bounds = array[0].bounds;
			for (int i = 1; i < num; i++)
			{
				bounds.Encapsulate(array[i].bounds);
			}
			return true;
		}
		bounds = default;
		return false;
	}

	private void CreateCells(out Cell[][] _cells)
	{
		if (TryEncapsulateSceneBounds(out m_bounds))
		{
			int num = Mathf.Max(Mathf.CeilToInt(m_bounds.size.x / m_unitsPerCell), 1);
			int num2 = Mathf.Max(Mathf.CeilToInt(m_bounds.size.z / m_unitsPerCell), 1);
			m_cellWidthInUnits = m_bounds.size.x / (float)num;
			m_cellDepthInUnits = m_bounds.size.z / (float)num2;
			float num3 = m_cellWidthInUnits * 0.5f;
			float num4 = m_cellDepthInUnits * 0.5f;
			Vector3 size = new Vector3(m_cellWidthInUnits, m_bounds.size.y, m_cellDepthInUnits);
			_cells = new Cell[num][];
			for (int i = 0; i < num; i++)
			{
				_cells[i] = new Cell[num2];
				for (int j = 0; j < num2; j++)
				{
					Cell cell = (_cells[i][j] = new Cell());
					cell.bounds = new Bounds(new Vector3(m_bounds.min.x + num3 + (float)i * m_cellWidthInUnits, m_bounds.center.y, m_bounds.min.z + num4 + (float)j * m_cellDepthInUnits), size);
					BBList<ProximityTrigger> bBList = new BBList<ProximityTrigger>();
					for (int k = 0; k < m_triggers.Count; k++)
					{
						TriggerShape component = m_triggers[k].GetComponent<TriggerShape>();
						if (cell.bounds.Intersects(component.bounds))
						{
							bBList.Add(m_triggers[k]);
						}
					}
					cell.triggers = bBList.ToArray();
				}
			}
		}
		else
		{
			_cells = null;
		}
	}

	private Cell FindCellForPosition(Vector3 position)
	{
		if (position.x < m_bounds.min.x || position.y < m_bounds.min.y || position.z < m_bounds.min.z)
		{
			return null;
		}
		if (position.x > m_bounds.max.x || position.y > m_bounds.max.y || position.z > m_bounds.max.z)
		{
			return null;
		}
		position -= m_bounds.min;
		int num = Mathf.FloorToInt(position.x / m_cellWidthInUnits);
		int num2 = Mathf.FloorToInt(position.z / m_cellDepthInUnits);
		return m_cells[num][num2];
	}

	public void StartTick()
	{
		if (m_dirty)
		{
			Profiling.Start("ProximityTriggerTick::CreateCells()", float.MaxValue);
			CreateCells(out m_cells);
			Profiling.Stop();
			m_dirty = false;
		}
	}

	public void EndTick()
	{
	}

	public void Tick(float accumTime)
	{
		int count = m_transforms.Count;
		if (count != 0 && m_tickRatePerSecond > 0f && m_cells != null)
		{
			m_accumTime += accumTime;
			float num = 1f / ((float)count * m_tickRatePerSecond);
			int num2 = Mathf.FloorToInt(m_accumTime / num);
			for (int i = m_iterator; i < m_iterator + num2; i++)
			{
				TriggerTransform triggerTransform = m_transforms[i % count];
				Cell cell = FindCellForPosition(triggerTransform.transform.position);
				if (cell != null)
				{
					ProximityTrigger[] triggers = cell.triggers;
					for (int j = 0; j < triggers.Length; j++)
					{
						triggers[j].Tick(triggerTransform);
					}
				}
			}
			m_iterator += num2;
			if (m_iterator >= count)
			{
				foreach (ProximityTrigger trigger in m_triggers)
				{
					trigger.Evalulate();
				}
			}
			m_iterator %= count;
			m_accumTime -= (float)num2 * num;
		}
		else
		{
			m_accumTime = 0f;
		}
	}

	public void Register(ProximityTrigger _trigger)
	{
		m_triggers.Add(_trigger);
		m_dirty = true;
	}

	public void Unregister(ProximityTrigger _trigger)
	{
		m_triggers.Remove(_trigger);
		m_dirty = true;
	}

	public void Register(TriggerTransform _transform)
	{
		m_transforms.Add(_transform);
	}

	public void Unregister(TriggerTransform _transform)
	{
		if (m_transforms.FindIndex((TriggerTransform o) => _transform) < m_iterator)
		{
			m_iterator--;
		}
		m_transforms.Remove(_transform);
		if (m_iterator == m_transforms.Count)
		{
			m_iterator = 0;
		}
	}

	private void OnDrawGizmosSelected()
	{
		if (m_cells == null || m_dirty)
		{
			return;
		}
		for (int i = 0; i < m_cells.Length; i++)
		{
			for (int j = 0; j < m_cells[i].Length; j++)
			{
				Cell cell = m_cells[i][j];
				Gizmos.color = Color.cyan;
				Gizmos.DrawWireCube(cell.bounds.center, cell.bounds.size);
				ProximityTrigger[] triggers = cell.triggers;
				foreach (ProximityTrigger proximityTrigger in triggers)
				{
					Gizmos.color = Color.magenta;
					Gizmos.DrawLine(new Vector3(cell.bounds.center.x, cell.bounds.max.y, cell.bounds.center.z), proximityTrigger.transform.position);
				}
			}
		}
		foreach (TriggerTransform transform in m_transforms)
		{
			Cell cell2 = FindCellForPosition(transform.transform.position);
			if (cell2 != null)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawLine(transform.transform.position, new Vector3(cell2.bounds.center.x, cell2.bounds.max.y, cell2.bounds.center.z));
			}
		}
	}
}
