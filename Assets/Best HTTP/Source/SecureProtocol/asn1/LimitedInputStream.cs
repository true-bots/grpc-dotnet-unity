#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1
{
	abstract class LimitedInputStream
		: BaseInputStream
	{
		protected readonly Stream _in;
		int _limit;

		internal LimitedInputStream(Stream inStream, int limit)
		{
			_in = inStream;
			_limit = limit;
		}

		internal virtual int Limit
		{
			get { return _limit; }
		}

		protected void SetParentEofDetect()
		{
			if (_in is IndefiniteLengthInputStream)
			{
				((IndefiniteLengthInputStream)_in).SetEofOn00(true);
			}
		}
	}
}
#pragma warning restore
#endif