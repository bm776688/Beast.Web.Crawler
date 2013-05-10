using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Advertising.Analytics.SharedService
{
	class PriorityQueue<T>
	{
		private IComparer<T> comparer;
		private T[] heap;

		internal static int DefaultCapacity = 1000;

		public int Count { get; private set; }

		public PriorityQueue() : this(null) { }
		public PriorityQueue(int capacity) : this(capacity, null) { }
		public PriorityQueue(IComparer<T> comparer) : this(DefaultCapacity, comparer) { }

		public PriorityQueue(int capacity, IComparer<T> comparer)
		{
			this.comparer = (comparer == null) ? Comparer<T>.Default : comparer;
			this.heap = new T[capacity];
		}

		public void Enqueue(T v)
		{
			if (Count >= heap.Length) 
				Array.Resize(ref heap, Count * 2);
			heap[Count] = v;
			SiftUp(Count++);
		}

		public T Dequeue()
		{
			var v = Top();
			heap[0] = heap[--Count];
			if (Count > 0) SiftDown(0);
			return v;
		}

		public T Top()
		{
			if (Count > 0) return heap[0];
			throw new InvalidOperationException("the priority queue is emp");
		}

		private void SiftUp(int n)
		{
			var v = heap[n];
			for (var n2 = n / 2; n > 0 && comparer.Compare(v, heap[n2]) > 0; n = n2, n2 /= 2) heap[n] = heap[n2];
			heap[n] = v;
		}

		private void SiftDown(int n)
		{

			var v = heap[n];
			for (var n2 = n * 2; n2 < this.Count; n = n2, n2 *= 2)
			{
				if (n2 + 1 < Count && this.comparer.Compare(heap[n2 + 1], heap[n2]) > 0) n2++;
				if (this.comparer.Compare(v, heap[n2]) >= 0) break;
				heap[n] = heap[n2];
			}
			heap[n] = v;
		}
	}
}
