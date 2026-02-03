using System.Collections.Concurrent;

namespace L2D;

public class Channel<T> {

	private bool completed;

	private ConcurrentQueue<T> queue;
	private SemaphoreSlim semaphore;

	public Channel() {
		completed = false;
		queue = new ConcurrentQueue<T>();
		semaphore = new SemaphoreSlim(0);
	}

	public void Push(T item) {
		queue.Enqueue(item);
		semaphore.Release();
	}

	public async Task<T> Read() {
		await semaphore.WaitAsync();
		
		if(queue.TryDequeue(out T item)) {
			return item;
		}

		return default;
	}

}