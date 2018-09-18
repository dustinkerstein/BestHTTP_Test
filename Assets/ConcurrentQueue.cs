using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ConcurrentQueue<T>
{
	private List<T> queue;
	private object syncLock = new object();

	public ConcurrentQueue()
	{
		this.queue = new List<T>();
	}

	public ConcurrentQueue(int capacity)
	{
		this.queue = new List<T>(capacity);
	}

	public int Count
	{
		get
		{
			//lock (syncLock)
			{
				return queue.Count;
			}
		}
	}

	public void Clear()
	{
		lock (syncLock)
		{
			queue.Clear();
		}
	}

	public void Enqueue(T obj)
	{
		lock (syncLock)
		{
			queue.Add(obj);
		}
	}

	public void EnqueueRange(IEnumerable<T> objs)
	{
		lock (syncLock)
		{
			queue.AddRange(objs);
		}
	}

	public bool TryEnqueue(T obj)
	{
		if (Monitor.TryEnter(syncLock, 0))
		{
			try
			{
				queue.Add(obj);
				return true;
			}
			finally
			{
				Monitor.Exit(syncLock);
			}
		}
		else
		{
			return false;
		}
	}

	public bool TryEnqueueRange(IEnumerable<T> objs)
	{
		if (Monitor.TryEnter(syncLock, 0))
		{
			try
			{
				queue.AddRange(objs);
				return true;
			}
			finally
			{
				Monitor.Exit(syncLock);
			}
		}
		else
		{
			return false;
		}
	}

	public bool Dequeue(out T target)
	{
		target = default(T);

		if (queue.Count == 0)
			return false;

		lock (syncLock)
		{
			if (queue.Count == 0)
				return false;

			target = queue[0];
			queue.RemoveAt(0);

			return true;
		}
	}

	public bool DequeueAll(List<T> targetList)
	{
		if (queue.Count == 0)
			return false;

		lock (syncLock)
		{
			if (queue.Count == 0)
				return false;

			targetList.AddRange(queue);
			queue.Clear();

			return true;
		}
	}

	public bool TryDequeue(out T target)
	{
		target = default(T);

		if (queue.Count == 0)
			return false;

		if (Monitor.TryEnter(syncLock, 0))
		{
			try
			{
				if (queue.Count == 0)
					return false;

				target = queue[0];
				queue.RemoveAt(0);

				return true;
			}
			finally
			{
				Monitor.Exit(syncLock);
			}
		}
		else
		{
			return false;
		}
	}

	public bool TryDequeueAll(List<T> targetList)
	{
		if (queue.Count == 0)
			return false;

		if (Monitor.TryEnter(syncLock, 0))
		{
			try
			{
				if (queue.Count == 0)
					return false;

				targetList.AddRange(queue);
				queue.Clear();

				return true;
			}
			finally
			{
				Monitor.Exit(syncLock);
			}
		}
		else
		{
			return false;
		}
	}
}
