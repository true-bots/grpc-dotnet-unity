#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.Collections
{
	abstract class ReadOnlyCollection<T>
		: ICollection<T>
	{
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public bool IsReadOnly
		{
			get { return true; }
		}

		public void Add(T item)
		{
			throw new NotSupportedException();
		}

		public void Clear()
		{
			throw new NotSupportedException();
		}

		public bool Remove(T item)
		{
			throw new NotSupportedException();
		}

		public abstract bool Contains(T item);
		public abstract int Count { get; }
		public abstract void CopyTo(T[] array, int arrayIndex);
		public abstract IEnumerator<T> GetEnumerator();
	}

	class ReadOnlyCollectionProxy<T>
		: ReadOnlyCollection<T>
	{
		readonly ICollection<T> m_target;

		internal ReadOnlyCollectionProxy(ICollection<T> target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			m_target = target;
		}

		public override bool Contains(T item)
		{
			return m_target.Contains(item);
		}

		public override int Count
		{
			get { return m_target.Count; }
		}

		public override void CopyTo(T[] array, int arrayIndex)
		{
			m_target.CopyTo(array, arrayIndex);
		}

		public override IEnumerator<T> GetEnumerator()
		{
			return m_target.GetEnumerator();
		}
	}
}
#pragma warning restore
#endif