using System;
using UnityEngine;

namespace UbiCore.Acier;

[Serializable]
public class LinkData
{
	public OffMeshLink Link;

	public Transform Start;

	public Transform End;

	public OffMeshLinkMeta Meta;

	public LinkData(OffMeshLink _link, Transform _start, Transform _end, OffMeshLinkMeta _meta)
	{
		Link = _link;
		Start = _start;
		End = _end;
		Meta = _meta;
	}
}
