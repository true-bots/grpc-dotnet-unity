#if !BESTHTTP_DISABLE_WEBSOCKET && (!UNITY_WEBGL || UNITY_EDITOR)

using System;
using System.Collections.Generic;
using System.IO;
using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using BestHTTP.WebSocket.Extensions;

namespace BestHTTP.WebSocket.Frames
{
	/// <summary>
	/// Represents an incoming WebSocket Frame.
	/// </summary>
	public struct WebSocketFrameReader
	{
		#region Properties

		public byte Header { get; private set; }

		/// <summary>
		/// True if it's a final Frame in a sequence, or the only one.
		/// </summary>
		public bool IsFinal { get; private set; }

		/// <summary>
		/// The type of the Frame.
		/// </summary>
		public WebSocketFrameTypes Type { get; private set; }

		/// <summary>
		/// The decoded array of bytes.
		/// </summary>
		public BufferSegment Data { get; private set; }

		/// <summary>
		/// Textual representation of the received Data.
		/// </summary>
		public string DataAsText { get; private set; }

		#endregion

		#region Internal & Private Functions

		internal unsafe void Read(Stream stream)
		{
			// For the complete documentation for this section see:
			// http://tools.ietf.org/html/rfc6455#section-5.2

			Header = ReadByte(stream);

			// The first byte is the Final Bit and the type of the frame
			IsFinal = (Header & 0x80) != 0;
			Type = (WebSocketFrameTypes)(Header & 0xF);

			byte maskAndLength = ReadByte(stream);

			// The second byte is the Mask Bit and the length of the payload data
			if ((maskAndLength & 0x80) != 0)
			{
				throw new NotImplementedException($"Payload from the server is masked!");
			}

			// if 0-125, that is the payload length.
			ulong length = (ulong)(maskAndLength & 127);

			// If 126, the following 2 bytes interpreted as a 16-bit unsigned integer are the payload length.
			if (length == 126)
			{
				byte[] rawLen = BufferPool.Get(2, true);

				stream.ReadBuffer(rawLen, 2);

				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(rawLen, 0, 2);
				}

				length = (ulong)BitConverter.ToUInt16(rawLen, 0);

				BufferPool.Release(rawLen);
			}
			else if (length == 127)
			{
				// If 127, the following 8 bytes interpreted as a 64-bit unsigned integer (the
				// most significant bit MUST be 0) are the payload length.

				byte[] rawLen = BufferPool.Get(8, true);

				stream.ReadBuffer(rawLen, 8);

				if (BitConverter.IsLittleEndian)
				{
					Array.Reverse(rawLen, 0, 8);
				}

				length = (ulong)BitConverter.ToUInt64(rawLen, 0);

				BufferPool.Release(rawLen);
			}

			if (length == 0L)
			{
				Data = BufferSegment.Empty;
				return;
			}

			byte[] buffer = BufferPool.Get((long)length, true);

			uint readLength = 0;

			try
			{
				do
				{
					int read = stream.Read(buffer, (int)readLength, (int)(length - readLength));

					if (read <= 0)
					{
						throw ExceptionHelper.ServerClosedTCPStream();
					}

					readLength += (uint)read;
				} while (readLength < length);
			}
			catch
			{
				BufferPool.Release(buffer);
				throw;
			}

			Data = new BufferSegment(buffer, 0, (int)length);
		}

		byte ReadByte(Stream stream)
		{
			int read = stream.ReadByte();

			if (read < 0)
			{
				throw ExceptionHelper.ServerClosedTCPStream();
			}

			return (byte)read;
		}

		#endregion

		#region Public Functions

		/// <summary>
		/// Assembles all fragments into a final frame. Call this on the last fragment of a frame.
		/// </summary>
		/// <param name="fragments">The list of previously downloaded and parsed fragments of the frame</param>
		public void Assemble(List<WebSocketFrameReader> fragments)
		{
			// this way the following algorithms will handle this fragment's data too
			fragments.Add(this);

			ulong finalLength = 0;
			for (int i = 0; i < fragments.Count; ++i)
			{
				finalLength += (ulong)fragments[i].Data.Count;
			}

			byte[] buffer = BufferPool.Get((long)finalLength, true);
			ulong pos = 0;
			for (int i = 0; i < fragments.Count; ++i)
			{
				if (fragments[i].Data.Count > 0)
				{
					Array.Copy(fragments[i].Data.Data, fragments[i].Data.Offset, buffer, (int)pos, (int)fragments[i].Data.Count);
				}

				fragments[i].ReleaseData();

				pos += (ulong)fragments[i].Data.Count;
			}

			// All fragments of a message are of the same type, as set by the first fragment's opcode.
			Type = fragments[0].Type;

			// Reserver flags may be contained only in the first fragment

			Header = fragments[0].Header;
			Data = new BufferSegment(buffer, 0, (int)finalLength);
		}

		/// <summary>
		/// This function will decode the received data incrementally with the associated websocket's extensions.
		/// </summary>
		public void DecodeWithExtensions(WebSocket webSocket)
		{
			if (webSocket.Extensions != null)
			{
				for (int i = 0; i < webSocket.Extensions.Length; ++i)
				{
					IExtension ext = webSocket.Extensions[i];
					if (ext != null)
					{
						BufferSegment newData = ext.Decode(Header, Data);
						if (Data != newData)
						{
							ReleaseData();
							Data = newData;
						}
					}
				}
			}

			if (Type == WebSocketFrameTypes.Text)
			{
				if (Data != BufferSegment.Empty)
				{
					DataAsText = System.Text.Encoding.UTF8.GetString(Data.Data, Data.Offset, Data.Count);
					ReleaseData();
				}
				else
				{
					HTTPManager.Logger.Warning("WebSocketFrameReader", "Empty Text frame received!");
				}
			}
		}

		public void ReleaseData()
		{
			BufferPool.Release(Data);
			Data = BufferSegment.Empty;
		}

		public override string ToString()
		{
			return string.Format("[{0} Header: {1:X2}, IsFinal: {2}, Data: {3}]", Type.ToString(), Header, IsFinal, Data);
		}

		#endregion
	}
}

#endif