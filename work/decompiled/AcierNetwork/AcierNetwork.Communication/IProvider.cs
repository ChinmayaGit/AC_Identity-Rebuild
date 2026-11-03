using System;
using System.Collections;
using SharedBaseLib.Core;
using UnityEngine;

namespace AcierNetwork.Communication;

public abstract class IProvider : MonoBehaviour
{
	protected BBDictionary<Type, Request> m_requests = new BBDictionary<Type, Request>();

	protected const float TimeBetweenUpdates = 0.1f;

	public bool Running { get; private set; }

	protected QueryResult Result { get; set; }

	public abstract bool Accepts(Type _type);

	public bool HasRequestOfType<T>()
	{
		return m_requests.ContainsKey(typeof(T));
	}

	public abstract bool BeginRead<T>(Request _request) where T : Message;

	public virtual void OnDestroy()
	{
		Running = false;
	}

	public abstract void Initialize();

	protected abstract IEnumerator UpdateAsync();

	public IEnumerator UpdateCoroutine()
	{
		Running = true;
		WaitForEndOfFrame wait = new WaitForEndOfFrame();
		if (Running)
		{
			IEnumerator ite = UpdateAsync();
			while (ite.MoveNext())
			{
				yield return wait;
			}
			Running = false;
		}
	}
}
