using System.Threading;

namespace BB.Mobile.Net.Collections;

public sealed class ConcurrentQueue<T>
{
	private sealed class Node
	{
		private object mNext;

		private T mValue;

		public T Value => mValue;

		public Node(T value)
		{
			mValue = value;
		}

		public void InsertHead(T value)
		{
			Node node = new Node(value);
			Node node2;
			do
			{
				node2 = (Node)(node.mNext = mNext as Node);
			}
			while (Interlocked.CompareExchange(ref mNext, node, node2) != node2);
		}

		public bool TryRemoveTail(out T value)
		{
			Node node;
			do
			{
				node = mNext as Node;
				if (node == null)
				{
					value = default;
					return false;
				}
				if (node.TryRemoveTail(out value))
				{
					return true;
				}
			}
			while (Interlocked.CompareExchange(ref mNext, null, node) != node);
			value = node.Value;
			return true;
		}
	}

	private Node mHead = new Node(default);

	public void Enqueue(T value)
	{
		mHead.InsertHead(value);
	}

	public bool TryDequeue(out T value)
	{
		return mHead.TryRemoveTail(out value);
	}
}
