#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using BestHTTP.Core;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using BestHTTP.Decompression;

namespace BestHTTP.Connections.HTTP2
{
	public sealed class HTTP2Response : HTTPResponse
	{
		// For progress report
		public long ExpectedContentLength { get; private set; }

		public bool HasContentEncoding
		{
			get { return !string.IsNullOrEmpty(contentEncoding); }
		}

		string contentEncoding = null;

		bool isPrepared;
		Decompression.IDecompressor decompressor;

		public HTTP2Response(HTTPRequest request, bool isFromCache)
			: base(request, isFromCache)
		{
			VersionMajor = 2;
			VersionMinor = 0;
		}

		internal void AddHeaders(List<KeyValuePair<string, string>> headers)
		{
			ExpectedContentLength = -1;
			Dictionary<string, List<string>> newHeaders = baseRequest.OnHeadersReceived != null ? new Dictionary<string, List<string>>() : null;

			for (int i = 0; i < headers.Count; ++i)
			{
				KeyValuePair<string, string> header = headers[i];

				if (header.Key.Equals(":status", StringComparison.Ordinal))
				{
					StatusCode = int.Parse(header.Value);
					Message = string.Empty;
				}
				else
				{
					if (!HasContentEncoding && header.Key.Equals("content-encoding", StringComparison.OrdinalIgnoreCase))
					{
						contentEncoding = header.Value;
					}
					else if (baseRequest.OnDownloadProgress != null && header.Key.Equals("content-length", StringComparison.OrdinalIgnoreCase))
					{
						long contentLength;
						if (long.TryParse(header.Value, out contentLength))
						{
							ExpectedContentLength = contentLength;
						}
						else
						{
							HTTPManager.Logger.Information("HTTP2Response", string.Format("AddHeaders - Can't parse Content-Length as an int: '{0}'", header.Value),
								baseRequest.Context, Context);
						}
					}

					AddHeader(header.Key, header.Value);
				}

				if (newHeaders != null)
				{
					List<string> values;
					if (!newHeaders.TryGetValue(header.Key, out values))
					{
						newHeaders.Add(header.Key, values = new List<string>(1));
					}

					values.Add(header.Value);
				}
			}

			if (ExpectedContentLength == -1 && baseRequest.OnDownloadProgress != null)
			{
				HTTPManager.Logger.Information("HTTP2Response", "AddHeaders - No Content-Length header found!", baseRequest.Context, Context);
			}

			RequestEventHelper.EnqueueRequestEvent(new RequestEventInfo(baseRequest, newHeaders));
		}

		internal void AddData(Stream stream)
		{
			if (HasContentEncoding)
			{
				Stream decoderStream = Decompression.DecompressorFactory.GetDecoderStream(stream, contentEncoding);

				if (decoderStream == null)
				{
					Data = new byte[stream.Length];
					stream.Read(Data, 0, (int)stream.Length);
				}
				else
				{
					using (BufferPoolMemoryStream ms = new BufferPoolMemoryStream((int)stream.Length))
					{
						byte[] buf = BufferPool.Get(MinReadBufferSize, true);
						int byteCount = 0;

						while ((byteCount = decoderStream.Read(buf, 0, buf.Length)) > 0)
						{
							ms.Write(buf, 0, byteCount);
						}

						BufferPool.Release(buf);

						Data = ms.ToArray();
					}

					decoderStream.Dispose();
				}
			}
			else
			{
				Data = new byte[stream.Length];
				stream.Read(Data, 0, (int)stream.Length);
			}
		}


		internal void ProcessData(byte[] payload, int payloadLength)
		{
			if (!isPrepared)
			{
				isPrepared = true;
				BeginReceiveStreamFragments();
			}

			if (HasContentEncoding)
			{
				if (decompressor == null)
				{
					decompressor = Decompression.DecompressorFactory.GetDecompressor(contentEncoding, Context);
				}

				DecompressedData result = decompressor.Decompress(payload, 0, payloadLength, true, true);

				FeedStreamFragment(result.Data, 0, result.Length);
			}
			else
			{
				FeedStreamFragment(payload, 0, payloadLength);
			}
		}

		internal void FinishProcessData()
		{
			FlushRemainingFragmentBuffer();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				if (decompressor != null)
				{
					decompressor.Dispose();
					decompressor = null;
				}
			}
		}
	}
}

#endif