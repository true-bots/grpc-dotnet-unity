#if !UNITY_WEBGL || UNITY_EDITOR

using System;
using BestHTTP.Core;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.FileSystem;

namespace BestHTTP.Connections
{
	sealed class FileConnection : ConnectionBase
	{
		public FileConnection(string serverAddress)
			: base(serverAddress)
		{
		}

		protected override void ThreadFunc()
		{
			try
			{
				// Step 1 : create a stream with header information
				// Step 2 : create a stream from the file
				// Step 3 : create a StreamList
				// Step 4 : create a HTTPResponse object
				// Step 5 : call the Receive function of the response object

				using (System.IO.Stream fs = HTTPManager.IOService.CreateFileStream(CurrentRequest.CurrentUri.LocalPath, FileStreamModes.OpenRead))
				using (StreamList stream = new StreamList(new BufferPoolMemoryStream(), fs))
				{
					// This will write to the MemoryStream
					stream.Write("HTTP/1.1 200 Ok\r\n");
					stream.Write("Content-Type: application/octet-stream\r\n");
					stream.Write("Content-Length: " + fs.Length.ToString() + "\r\n");
					stream.Write("\r\n");

					stream.Seek(0, System.IO.SeekOrigin.Begin);

					CurrentRequest.Response = new HTTPResponse(CurrentRequest, stream, CurrentRequest.UseStreaming, false);

					if (!CurrentRequest.Response.Receive())
					{
						CurrentRequest.Response = null;
					}
				}
			}
			catch (Exception e)
			{
				CurrentRequest.Response = null;

				if (!CurrentRequest.IsCancellationRequested)
				{
					CurrentRequest.Exception = e;
					CurrentRequest.State = HTTPRequestStates.Error;
				}
			}
			finally
			{
				if (CurrentRequest.IsCancellationRequested)
				{
					CurrentRequest.Response = null;
					CurrentRequest.State = CurrentRequest.IsTimedOut ? HTTPRequestStates.TimedOut : HTTPRequestStates.Aborted;
				}
				else if (CurrentRequest.Response == null)
				{
					CurrentRequest.State = HTTPRequestStates.Error;
				}
				else
				{
					CurrentRequest.State = HTTPRequestStates.Finished;
				}

				ConnectionEventHelper.EnqueueConnectionEvent(new ConnectionEventInfo(this, HTTPConnectionStates.Closed));
			}
		}
	}
}

#endif