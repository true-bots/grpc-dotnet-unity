#if (!UNITY_WEBGL || UNITY_EDITOR) && !BESTHTTP_DISABLE_ALTERNATE_SSL && !BESTHTTP_DISABLE_HTTP2

using BestHTTP.Extensions;
using BestHTTP.PlatformSupport.Memory;
using System;
using System.Collections.Generic;

namespace BestHTTP.Connections.HTTP2
{
	// https://httpwg.org/specs/rfc7540.html#iana-frames
	public enum HTTP2FrameTypes : byte
	{
		DATA = 0x00,
		HEADERS = 0x01,
		PRIORITY = 0x02,
		RST_STREAM = 0x03,
		SETTINGS = 0x04,
		PUSH_PROMISE = 0x05,
		PING = 0x06,
		GOAWAY = 0x07,
		WINDOW_UPDATE = 0x08,
		CONTINUATION = 0x09,

		// https://tools.ietf.org/html/rfc7838#section-4
		ALT_SVC = 0x0A
	}

	[Flags]
	public enum HTTP2DataFlags : byte
	{
		None = 0x00,
		END_STREAM = 0x01,
		PADDED = 0x08
	}

	[Flags]
	public enum HTTP2HeadersFlags : byte
	{
		None = 0x00,
		END_STREAM = 0x01,
		END_HEADERS = 0x04,
		PADDED = 0x08,
		PRIORITY = 0x20
	}

	[Flags]
	public enum HTTP2SettingsFlags : byte
	{
		None = 0x00,
		ACK = 0x01
	}

	[Flags]
	public enum HTTP2PushPromiseFlags : byte
	{
		None = 0x00,
		END_HEADERS = 0x04,
		PADDED = 0x08
	}

	[Flags]
	public enum HTTP2PingFlags : byte
	{
		None = 0x00,
		ACK = 0x01
	}

	[Flags]
	public enum HTTP2ContinuationFlags : byte
	{
		None = 0x00,
		END_HEADERS = 0x04
	}

	public struct HTTP2FrameHeaderAndPayload
	{
		public uint PayloadLength;
		public HTTP2FrameTypes Type;
		public byte Flags;
		public uint StreamId;
		public byte[] Payload;

		public uint PayloadOffset;
		public bool DontUseMemPool;

		public override string ToString()
		{
			return string.Format("[HTTP2FrameHeaderAndPayload Length: {0}, Type: {1}, Flags: {2}, StreamId: {3}, PayloadOffset: {4}, DontUseMemPool: {5}, Payload: {6}]",
				PayloadLength, Type, Flags.ToBinaryStr(), StreamId, PayloadOffset, DontUseMemPool,
				Payload == null ? BufferSegment.Empty : new BufferSegment(Payload, (int)PayloadOffset, (int)PayloadLength));
		}

		public string PayloadAsHex()
		{
			System.Text.StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get((int)PayloadLength + 2);
			sb.Append("[");
			if (Payload != null && PayloadLength > 0)
			{
				uint idx = PayloadOffset;
				sb.Append(Payload[idx++]);
				for (int i = 1; i < PayloadLength; i++)
				{
					sb.AppendFormat(", {0:X2}", Payload[idx++]);
				}
			}

			sb.Append("]");

			return PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(sb);
		}
	}

	public struct HTTP2SettingsFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2SettingsFlags Flags
		{
			get { return (HTTP2SettingsFlags)Header.Flags; }
		}

		public List<KeyValuePair<HTTP2Settings, uint>> Settings;

		public HTTP2SettingsFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			Settings = null;
		}

		public override string ToString()
		{
			string settings = null;
			if (Settings != null)
			{
				System.Text.StringBuilder sb = PlatformSupport.Text.StringBuilderPool.Get(Settings.Count + 2);

				sb.Append("[");
				foreach (KeyValuePair<HTTP2Settings, uint> kvp in Settings)
				{
					sb.AppendFormat("[{0}: {1}]", kvp.Key, kvp.Value);
				}

				sb.Append("]");

				settings = PlatformSupport.Text.StringBuilderPool.ReleaseAndGrab(sb);
			}

			return string.Format("[HTTP2SettingsFrame Header: {0}, Flags: {1}, Settings: {2}]", Header.ToString(), Flags, settings ?? "Empty");
		}
	}

	public struct HTTP2DataFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2DataFlags Flags
		{
			get { return (HTTP2DataFlags)Header.Flags; }
		}

		public byte? PadLength;
		public uint DataIdx;
		public byte[] Data;
		public uint DataLength;

		public HTTP2DataFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			PadLength = null;
			DataIdx = 0;
			Data = null;
			DataLength = 0;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2DataFrame Header: {0}, Flags: {1}, PadLength: {2}, DataLength: {3}]",
				Header.ToString(),
				Flags,
				PadLength == null ? ":Empty" : PadLength.Value.ToString(),
				DataLength);
		}
	}

	public struct HTTP2HeadersFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2HeadersFlags Flags
		{
			get { return (HTTP2HeadersFlags)Header.Flags; }
		}

		public byte? PadLength;
		public byte? IsExclusive;
		public uint? StreamDependency;
		public byte? Weight;
		public uint HeaderBlockFragmentIdx;
		public byte[] HeaderBlockFragment;
		public uint HeaderBlockFragmentLength;

		public HTTP2HeadersFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			PadLength = null;
			IsExclusive = null;
			StreamDependency = null;
			Weight = null;
			HeaderBlockFragmentIdx = 0;
			HeaderBlockFragment = null;
			HeaderBlockFragmentLength = 0;
		}

		public override string ToString()
		{
			return string.Format(
				"[HTTP2HeadersFrame Header: {0}, Flags: {1}, PadLength: {2}, IsExclusive: {3}, StreamDependency: {4}, Weight: {5}, HeaderBlockFragmentLength: {6}]",
				Header.ToString(),
				Flags,
				PadLength == null ? ":Empty" : PadLength.Value.ToString(),
				IsExclusive == null ? "Empty" : IsExclusive.Value.ToString(),
				StreamDependency == null ? "Empty" : StreamDependency.Value.ToString(),
				Weight == null ? "Empty" : Weight.Value.ToString(),
				HeaderBlockFragmentLength);
		}
	}

	public struct HTTP2PriorityFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public byte IsExclusive;
		public uint StreamDependency;
		public byte Weight;

		public HTTP2PriorityFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			IsExclusive = 0;
			StreamDependency = 0;
			Weight = 0;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2PriorityFrame Header: {0}, IsExclusive: {1}, StreamDependency: {2}, Weight: {3}]",
				Header.ToString(), IsExclusive, StreamDependency, Weight);
		}
	}

	public struct HTTP2RSTStreamFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public uint ErrorCode;

		public HTTP2ErrorCodes Error
		{
			get { return (HTTP2ErrorCodes)ErrorCode; }
		}

		public HTTP2RSTStreamFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			ErrorCode = 0;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2RST_StreamFrame Header: {0}, Error: {1}({2})]", Header.ToString(), Error, ErrorCode);
		}
	}

	public struct HTTP2PushPromiseFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2PushPromiseFlags Flags
		{
			get { return (HTTP2PushPromiseFlags)Header.Flags; }
		}

		public byte? PadLength;
		public byte ReservedBit;
		public uint PromisedStreamId;
		public uint HeaderBlockFragmentIdx;
		public byte[] HeaderBlockFragment;
		public uint HeaderBlockFragmentLength;

		public HTTP2PushPromiseFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			PadLength = null;
			ReservedBit = 0;
			PromisedStreamId = 0;
			HeaderBlockFragmentIdx = 0;
			HeaderBlockFragment = null;
			HeaderBlockFragmentLength = 0;
		}

		public override string ToString()
		{
			return string.Format(
				"[HTTP2Push_PromiseFrame Header: {0}, Flags: {1}, PadLength: {2}, ReservedBit: {3}, PromisedStreamId: {4}, HeaderBlockFragmentLength: {5}]",
				Header.ToString(),
				Flags,
				PadLength == null ? "Empty" : PadLength.Value.ToString(),
				ReservedBit,
				PromisedStreamId,
				HeaderBlockFragmentLength);
		}
	}

	public struct HTTP2PingFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2PingFlags Flags
		{
			get { return (HTTP2PingFlags)Header.Flags; }
		}

		public readonly byte[] OpaqueData;
		public readonly byte OpaqueDataLength;

		public HTTP2PingFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			OpaqueData = BufferPool.Get(8, true);
			OpaqueDataLength = 8;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2PingFrame Header: {0}, Flags: {1}, OpaqueData: {2}]",
				Header.ToString(),
				Flags,
				SecureProtocol.Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(OpaqueData, 0, OpaqueDataLength));
		}
	}

	public struct HTTP2GoAwayFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2ErrorCodes Error
		{
			get { return (HTTP2ErrorCodes)ErrorCode; }
		}

		public byte ReservedBit;
		public uint LastStreamId;
		public uint ErrorCode;
		public byte[] AdditionalDebugData;
		public uint AdditionalDebugDataLength;

		public HTTP2GoAwayFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			ReservedBit = 0;
			LastStreamId = 0;
			ErrorCode = 0;
			AdditionalDebugData = null;
			AdditionalDebugDataLength = 0;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2GoAwayFrame Header: {0}, ReservedBit: {1}, LastStreamId: {2}, Error: {3}({4}), AdditionalDebugData({5}): {6}]",
				Header.ToString(),
				ReservedBit,
				LastStreamId,
				Error,
				ErrorCode,
				AdditionalDebugDataLength,
				AdditionalDebugData == null
					? "Empty"
					: SecureProtocol.Org.BouncyCastle.Utilities.Encoders.Hex.ToHexString(AdditionalDebugData, 0, (int)AdditionalDebugDataLength));
		}
	}

	public struct HTTP2WindowUpdateFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public byte ReservedBit;
		public uint WindowSizeIncrement;

		public HTTP2WindowUpdateFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			ReservedBit = 0;
			WindowSizeIncrement = 0;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2WindowUpdateFrame Header: {0}, ReservedBit: {1}, WindowSizeIncrement: {2}]",
				Header.ToString(), ReservedBit, WindowSizeIncrement);
		}
	}

	public struct HTTP2ContinuationFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public HTTP2ContinuationFlags Flags
		{
			get { return (HTTP2ContinuationFlags)Header.Flags; }
		}

		public byte[] HeaderBlockFragment;

		public uint HeaderBlockFragmentLength
		{
			get { return Header.PayloadLength; }
		}

		public HTTP2ContinuationFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			HeaderBlockFragment = null;
		}

		public override string ToString()
		{
			return string.Format("[HTTP2ContinuationFrame Header: {0}, Flags: {1}, HeaderBlockFragmentLength: {2}]",
				Header.ToString(),
				Flags,
				HeaderBlockFragmentLength);
		}
	}

	/// <summary>
	/// https://tools.ietf.org/html/rfc7838#section-4
	/// </summary>
	public struct HTTP2AltSVCFrame
	{
		public readonly HTTP2FrameHeaderAndPayload Header;

		public string Origin;
		public string AltSvcFieldValue;

		public HTTP2AltSVCFrame(HTTP2FrameHeaderAndPayload header)
		{
			Header = header;
			Origin = null;
			AltSvcFieldValue = null;
		}
	}
}

#endif