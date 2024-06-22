using System;

namespace BestHTTP
{
	/// <summary>
	///
	/// </summary>
	public sealed class HTTPRange
	{
		/// <summary>
		/// The first byte's position that the server sent.
		/// </summary>
		public long FirstBytePos { get; private set; }

		/// <summary>
		/// The last byte's position that the server sent.
		/// </summary>
		public long LastBytePos { get; private set; }

		/// <summary>
		/// Indicates the total length of the full entity-body on the server, -1 if this length is unknown or difficult to determine.
		/// </summary>
		public long ContentLength { get; private set; }

		/// <summary>
		///
		/// </summary>
		public bool IsValid { get; private set; }

		internal HTTPRange()
		{
			ContentLength = -1;
			IsValid = false;
		}

		internal HTTPRange(int contentLength)
		{
			ContentLength = contentLength;
			IsValid = false;
		}

		internal HTTPRange(long firstBytePosition, long lastBytePosition, long contentLength)
		{
			FirstBytePos = firstBytePosition;
			LastBytePos = lastBytePosition;
			ContentLength = contentLength;

			// A byte-content-range-spec with a byte-range-resp-spec whose last-byte-pos value is less than its first-byte-pos value, or whose instance-length value is less than or equal to its last-byte-pos value, is invalid.
			IsValid = FirstBytePos <= LastBytePos && ContentLength > LastBytePos;
		}

		public override string ToString()
		{
			return string.Format("{0}-{1}/{2} (valid: {3})", FirstBytePos, LastBytePos, ContentLength, IsValid);
		}
	}
}