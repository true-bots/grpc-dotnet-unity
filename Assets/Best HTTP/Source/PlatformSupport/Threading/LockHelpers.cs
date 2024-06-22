using System;
using System.Threading;

namespace BestHTTP.PlatformSupport.Threading
{
	public struct ReadLock : IDisposable
	{
		ReaderWriterLockSlim rwLock;
		bool locked;

		public ReadLock(ReaderWriterLockSlim rwLock)
		{
			this.rwLock = rwLock;

			locked = this.rwLock.IsReadLockHeld;
			if (!locked)
			{
				this.rwLock.EnterReadLock();
			}
		}

		public void Dispose()
		{
			if (!locked)
			{
				rwLock.ExitReadLock();
			}
		}
	}

	public struct WriteLock : IDisposable
	{
		ReaderWriterLockSlim rwLock;
		bool locked;

		public WriteLock(ReaderWriterLockSlim rwLock)
		{
			this.rwLock = rwLock;
			locked = rwLock.IsWriteLockHeld;

			if (!locked)
			{
				this.rwLock.EnterWriteLock();
			}
		}

		public void Dispose()
		{
			if (!locked)
			{
				rwLock.ExitWriteLock();
			}
		}
	}
}