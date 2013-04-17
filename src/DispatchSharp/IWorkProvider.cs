namespace DispatchSharp.Unit.Tests
{
	public interface IWorkProvider<T>
	{
		void Enqueue(T work);
		IWorkQueueItem<T> TryDequeue();
	}

	public interface IWorkQueueItem<T>
	{
		bool HasItem { get; }
		T Item { get; }

		void Finish();
		void Cancel();
	}
}