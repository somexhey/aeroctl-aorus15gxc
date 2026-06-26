using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AeroCtl.UI;

internal static class Utility
{
	public static void Enqueue(this ConcurrentQueue<Func<Task>> queue, Func<ValueTask> fn)
	{
		queue.Enqueue(() => fn().AsTask());
	}
}