using System;
using System.Text;

namespace BestHTTP.Extensions
{
	public sealed class CircularBuffer<T>
	{
		public int Capacity { get; private set; }
		public int Count { get; private set; }

		public int StartIdx
		{
			get { return startIdx; }
		}

		public int EndIdx
		{
			get { return endIdx; }
		}

		public T this[int idx]
		{
			get
			{
				int realIdx = (startIdx + idx) % Capacity;

				return buffer[realIdx];
			}

			set
			{
				int realIdx = (startIdx + idx) % Capacity;

				buffer[realIdx] = value;
			}
		}

		T[] buffer;
		int startIdx;
		int endIdx;

		public CircularBuffer(int capacity)
		{
			Capacity = capacity;
		}

		public void Add(T element)
		{
			if (buffer == null)
			{
				buffer = new T[Capacity];
			}

			buffer[endIdx] = element;

			endIdx = (endIdx + 1) % Capacity;
			if (endIdx == startIdx)
			{
				startIdx = (startIdx + 1) % Capacity;
			}

			Count = Math.Min(Count + 1, Capacity);
		}

		public void Clear()
		{
			Count = startIdx = endIdx = 0;
		}

		public override string ToString()
		{
			StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get(2);
			sb.Append("[");

			int idx = startIdx;
			while (idx != endIdx)
			{
				sb.Append(buffer[idx].ToString());

				idx = (idx + 1) % Capacity;
				if (idx != endIdx)
				{
					sb.Append("; ");
				}
			}

			sb.Append("]");

			return sb.ToString();
		}
	}
}