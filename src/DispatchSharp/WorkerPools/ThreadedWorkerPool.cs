using System;
using System.Linq;
using System.Threading;
using DispatchSharp.Internal;

#pragma warning disable 420
namespace DispatchSharp
{
	public class ThreadedWorkerPool<T> : IWorkerPool<T>
	{
		readonly string _name;
		readonly Thread[] _pool;
		IDispatch<T> _dispatch;
		IWorkQueue<T> _queue;
		volatile object _started;
		volatile int _inflight;

		public IWaitHandle Available { get; set; }

		public ThreadedWorkerPool(string name, int threadCount)
		{
			_name = name ?? "UnnamedWorkerPool";
			_pool = new Thread[threadCount];
			_started = null;
			_inflight = 0;
			Available = new CrossThreadWait(true);
		}

		public void SetSource(IDispatch<T> dispatch, IWorkQueue<T> queue)
		{
			_dispatch = dispatch;
			_queue = queue;
		}

		public void Start()
		{
			var closedObject = new object();
			if (Interlocked.CompareExchange(ref _started, closedObject, null) != null) return;

			for (int i = 0; i < _pool.Length; i++)
			{
				_pool[i] = new Thread(() => WorkLoop(closedObject))
				{
					IsBackground = true,
					Name = _name + "_Thread_" + i
				};
				_pool[i].Start();
			}
		}

		public void Stop()
		{
			_started = null;
			while (_inflight > 0) Thread.Sleep(1);
		}

		public void TriggerAvailable()
		{
			Available.Set();
		}

		public int WorkersInflight()
		{
			return _inflight;
		}

		void WorkLoop(object reference)
		{
			Func<bool> running = () => _started == reference;
			while (running())
			{
				if (!Available.WaitOne()) continue;
				IWorkQueueItem<T> work;

				if (_inflight >= _dispatch.MaximumInflight) continue;

				Interlocked.Increment(ref _inflight);
				while (running() && (work = _queue.TryDequeue()).HasItem)
				{
					foreach (var action in _dispatch.AllConsumers().ToArray())
					{
						try
						{
							action(work.Item);
							work.Finish();
						}
						catch (Exception ex)
						{
							work.Cancel();
							_dispatch.OnExceptions(ex);
						}
					}
				}
				Interlocked.Decrement(ref _inflight);
			}
		}

	}
}