using System;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;

namespace BestHTTP.Logger
{
	public sealed class FileOutput : ILogOutput
	{
		System.IO.Stream fileStream;

		public FileOutput(string fileName)
		{
			fileStream = HTTPManager.IOService.CreateFileStream(fileName, PlatformSupport.FileSystem.FileStreamModes.Create);
		}

		public void Write(Loglevels level, string logEntry)
		{
			if (fileStream != null && !string.IsNullOrEmpty(logEntry))
			{
				int count = System.Text.Encoding.UTF8.GetByteCount(logEntry);
				byte[] buffer = BufferPool.Get(count, true);

				try
				{
					System.Text.Encoding.UTF8.GetBytes(logEntry, 0, logEntry.Length, buffer, 0);

					fileStream.Write(buffer, 0, count);
					fileStream.WriteLine();
				}
				finally
				{
					BufferPool.Release(buffer);
				}

				fileStream.Flush();
			}
		}

		public void Dispose()
		{
			if (fileStream != null)
			{
				fileStream.Close();
				fileStream = null;
			}

			GC.SuppressFinalize(this);
		}
	}
}